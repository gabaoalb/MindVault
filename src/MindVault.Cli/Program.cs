using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using MindVault.Application;
using MindVault.Domain;
using MindVault.Infrastructure;

var services = new ServiceCollection();
services.AddSingleton<IConfigurationStore>(_ => new JsonConfigurationStore(ConfigurationPathLocator.GetPath()));
services.AddSingleton<IVaultDirectory, VaultDirectory>();
services.AddSingleton<INoteFileStore, PhysicalNoteFileStore>();
services.AddSingleton<INoteDocumentSerializer, YamlNoteDocumentSerializer>();
services.AddSingleton<IFileNameGenerator, SlugFileNameGenerator>();
services.AddSingleton<IEditorCommandParser, EditorCommandParser>();
services.AddSingleton<IExternalEditor, ProcessExternalEditor>();
services.AddSingleton(TimeProvider.System);
services.AddSingleton<ConfigurationService>();
services.AddSingleton<NoteService>();
services.AddSingleton<DoctorService>();
await using var provider = services.BuildServiceProvider();

var root = new RootCommand("MindVault — anotações Markdown no terminal");
var config = new Command("config", "Configura o vault e o editor");
var note = new Command("note", "Cria, lista, abre e exclui notas");
root.Subcommands.Add(config);
root.Subcommands.Add(note);

var vaultArgument = new Argument<string>("path") { Description = "Diretório usado como vault" };
var createDirectoryOption = new Option<bool>("--create") { Description = "Cria o diretório quando ele não existe" };
var setVault = new Command("set-vault", "Configura o diretório do vault");
setVault.Arguments.Add(vaultArgument);
setVault.Options.Add(createDirectoryOption);
setVault.SetAction(async (parseResult, cancellationToken) => await SafeAsync(async () =>
{
    var path = parseResult.GetValue(vaultArgument)!;
    var create = parseResult.GetValue(createDirectoryOption);
    var service = provider.GetRequiredService<ConfigurationService>();
    var result = await service.SetVaultAsync(path, create, cancellationToken);
    if (!result.IsSuccess && result.Error!.Kind == ErrorKind.NotFound && !create && !Console.IsInputRedirected)
    {
        Console.Write($"{result.Error.Message} Criar agora? [s/N] ");
        var answer = Console.ReadLine();
        if (string.Equals(answer, "s", StringComparison.OrdinalIgnoreCase) || string.Equals(answer, "sim", StringComparison.OrdinalIgnoreCase))
            result = await service.SetVaultAsync(path, true, cancellationToken);
        else return ExitCodes.Cancelled;
    }
    if (!result.IsSuccess) return PrintError(result.Error!);
    Console.WriteLine($"Vault configurado: {result.Value!.VaultPath}");
    return ExitCodes.Success;
}));
config.Subcommands.Add(setVault);

var editorArgument = new Argument<string>("command") { Description = "Executável e argumentos fixos do editor" };
var setEditor = new Command("set-editor", "Configura o editor externo");
setEditor.Arguments.Add(editorArgument);
setEditor.SetAction(async (parseResult, cancellationToken) => await SafeAsync(async () =>
{
    var result = await provider.GetRequiredService<ConfigurationService>().SetEditorAsync(parseResult.GetValue(editorArgument)!, cancellationToken);
    if (!result.IsSuccess) return PrintError(result.Error!);
    Console.WriteLine($"Editor configurado: {FormatEditor(result.Value!.Editor!)}");
    return ExitCodes.Success;
}));
config.Subcommands.Add(setEditor);

var showConfig = new Command("show", "Exibe a configuração atual");
showConfig.SetAction(async (_, cancellationToken) => await SafeAsync(async () =>
{
    var result = await provider.GetRequiredService<ConfigurationService>().ShowAsync(cancellationToken);
    if (!result.IsSuccess) return PrintError(result.Error!);
    var read = result.Value!;
    Console.WriteLine($"Vault:  {read.Configuration!.VaultPath ?? "(não configurado)"}");
    Console.WriteLine($"Editor: {(read.Configuration.Editor is null ? "(não configurado)" : FormatEditor(read.Configuration.Editor))}");
    Console.WriteLine($"Config: {read.Path}");
    return ExitCodes.Success;
}));
config.Subcommands.Add(showConfig);

var titleArgument = new Argument<string>("title") { Description = "Título da nova nota" };
var noOpenOption = new Option<bool>("--no-open") { Description = "Não abre o editor depois da criação" };
var createNote = new Command("create", "Cria uma nota Markdown");
createNote.Arguments.Add(titleArgument);
createNote.Options.Add(noOpenOption);
createNote.SetAction(async (parseResult, cancellationToken) => await SafeAsync(async () =>
{
    var result = await provider.GetRequiredService<NoteService>().CreateAsync(parseResult.GetValue(titleArgument)!, !parseResult.GetValue(noOpenOption), cancellationToken);
    if (!result.IsSuccess) return PrintError(result.Error!);
    Console.WriteLine($"Nota criada: {result.Value!.FileName}");
    Console.WriteLine($"ID: {result.Value.Id}");
    return ExitCodes.Success;
}));
note.Subcommands.Add(createNote);

var listNotes = new Command("list", "Lista as notas do vault");
listNotes.SetAction(async (_, cancellationToken) => await SafeAsync(async () =>
{
    var result = await provider.GetRequiredService<NoteService>().ListAsync(cancellationToken);
    if (!result.IsSuccess) return PrintError(result.Error!);
    if (result.Value!.Count == 0) { Console.WriteLine("Nenhuma nota encontrada."); return ExitCodes.Success; }
    Console.WriteLine($"{"TITLE",-34} {"FILE",-42} MODIFIED");
    foreach (var item in result.Value)
    {
        var title = item.HasInvalidMetadata ? $"{item.Title} [metadados inválidos]" : item.Title;
        Console.WriteLine($"{Truncate(title, 34),-34} {Truncate(item.FileName, 42),-42} {item.ModifiedAt.ToLocalTime():yyyy-MM-dd HH:mm}");
        if (item.HasInvalidMetadata) Console.Error.WriteLine($"Aviso: {item.MetadataError}");
    }
    return ExitCodes.Success;
}));
note.Subcommands.Add(listNotes);

var openQueryArgument = new Argument<string>("query") { Description = "ID, arquivo ou parte do título" };
var openNote = new Command("open", "Abre uma nota no editor configurado");
openNote.Arguments.Add(openQueryArgument);
openNote.SetAction(async (parseResult, cancellationToken) => await SafeAsync(async () =>
{
    var result = await provider.GetRequiredService<NoteService>().OpenAsync(parseResult.GetValue(openQueryArgument)!, cancellationToken);
    if (!result.IsSuccess) return PrintError(result.Error!);
    Console.WriteLine($"Nota aberta: {result.Value!.FileName}");
    return ExitCodes.Success;
}));
note.Subcommands.Add(openNote);

var deleteQueryArgument = new Argument<string>("query") { Description = "ID, arquivo ou parte do título" };
var forceOption = new Option<bool>("--force") { Description = "Exclui sem solicitar confirmação" };
var deleteNote = new Command("delete", "Exclui uma nota");
deleteNote.Arguments.Add(deleteQueryArgument);
deleteNote.Options.Add(forceOption);
deleteNote.SetAction(async (parseResult, cancellationToken) => await SafeAsync(async () =>
{
    var query = parseResult.GetValue(deleteQueryArgument)!;
    var force = parseResult.GetValue(forceOption);
    var service = provider.GetRequiredService<NoteService>();
    var result = await service.DeleteAsync(query, force, cancellationToken);
    if (!result.IsSuccess) return PrintError(result.Error!);
    if (result.Value!.RequiresConfirmation)
    {
        Console.WriteLine($"Arquivo a excluir: {result.Value.Note.FullPath}");
        if (Console.IsInputRedirected) { Console.Error.WriteLine("Confirmação indisponível. Use --force para excluir sem interação."); return ExitCodes.Cancelled; }
        Console.Write("Confirmar exclusão? [s/N] ");
        var answer = Console.ReadLine();
        if (!string.Equals(answer, "s", StringComparison.OrdinalIgnoreCase) && !string.Equals(answer, "sim", StringComparison.OrdinalIgnoreCase)) return ExitCodes.Cancelled;
        result = await service.DeleteAsync(query, true, cancellationToken);
        if (!result.IsSuccess) return PrintError(result.Error!);
    }
    Console.WriteLine($"Nota excluída: {result.Value!.Note.FileName}");
    return ExitCodes.Success;
}));
note.Subcommands.Add(deleteNote);

var doctor = new Command("doctor", "Verifica configuração, vault e editor");
doctor.SetAction(async (_, cancellationToken) => await SafeAsync(async () =>
{
    var report = await provider.GetRequiredService<DoctorService>().RunAsync(cancellationToken);
    foreach (var check in report.Checks) Console.WriteLine($"{(check.IsSuccess ? "✓" : "✗")} {check.Message}");
    return report.IsHealthy ? ExitCodes.Success : ExitCodes.Configuration;
}));
root.Subcommands.Add(doctor);

var parsed = root.Parse(args);
if (parsed.Errors.Count > 0)
{
    foreach (var error in parsed.Errors) Console.Error.WriteLine(error.Message);
    return ExitCodes.InvalidArguments;
}
return await parsed.InvokeAsync();

static async Task<int> SafeAsync(Func<Task<int>> action)
{
    try { return await action(); }
    catch (OperationCanceledException) { Console.Error.WriteLine("Operação cancelada."); return ExitCodes.Cancelled; }
    catch (Exception ex) { Console.Error.WriteLine($"Erro inesperado: {ex.Message}"); return ExitCodes.Unexpected; }
}
static int PrintError(AppError error)
{
    Console.Error.WriteLine(error.Message);
    if (error.Details is not null) foreach (var detail in error.Details) Console.Error.WriteLine($"  - {detail}");
    return error.Kind switch
    {
        ErrorKind.InvalidInput or ErrorKind.Conflict => ExitCodes.InvalidArguments,
        ErrorKind.NotFound => ExitCodes.NotFound,
        ErrorKind.Ambiguous => ExitCodes.Ambiguous,
        ErrorKind.Cancelled => ExitCodes.Cancelled,
        ErrorKind.Configuration or ErrorKind.ExternalTool => ExitCodes.Configuration,
        _ => ExitCodes.Unexpected
    };
}
static string FormatEditor(EditorSettings editor) => string.Join(' ', new[] { editor.Executable }.Concat(editor.Arguments.Select(Quote)));
static string Quote(string value) => value.Any(char.IsWhiteSpace) ? $"\"{value.Replace("\"", "\\\"")}\"" : value;
static string Truncate(string value, int length) => value.Length <= length ? value : value[..(length - 1)] + "…";

static class ExitCodes
{
    public const int Success = 0;
    public const int Unexpected = 1;
    public const int InvalidArguments = 2;
    public const int Configuration = 3;
    public const int NotFound = 4;
    public const int Ambiguous = 5;
    public const int Cancelled = 6;
}
