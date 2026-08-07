using MindVault.Domain;

namespace MindVault.Application;

public sealed record EditorSettings(string Executable, IReadOnlyList<string> Arguments);
public sealed record UserConfiguration(string? VaultPath, EditorSettings? Editor)
{
    public static UserConfiguration Empty { get; } = new(null, null);
}

public enum ConfigurationStatus { Missing, Valid, Invalid }
public sealed record ConfigurationRead(ConfigurationStatus Status, UserConfiguration? Configuration, string Path, string? Error = null);
public sealed record StoredFile(string FileName, string FullPath, DateTimeOffset ModifiedAt, string Content);
public sealed record ParsedDocument(Note? Note, string? Error)
{
    public bool IsValid => Note is not null;
}
public sealed record NoteSummary(string? Id, string Title, string FileName, string FullPath, DateTimeOffset ModifiedAt, bool HasInvalidMetadata, string? MetadataError);
public sealed record DeleteNoteOutcome(NoteSummary Note, bool RequiresConfirmation);
public sealed record DiagnosticCheck(string Message, bool IsSuccess, bool IsBlocking);
public sealed record DoctorReport(IReadOnlyList<DiagnosticCheck> Checks)
{
    public bool IsHealthy => Checks.All(x => !x.IsBlocking || x.IsSuccess);
}

public interface IConfigurationStore
{
    Task<ConfigurationRead> ReadAsync(CancellationToken cancellationToken);
    Task WriteAsync(UserConfiguration configuration, CancellationToken cancellationToken);
    string ConfigurationPath { get; }
}

public interface IVaultDirectory
{
    string Normalize(string path);
    bool Exists(string path);
    Task CreateAsync(string path, CancellationToken cancellationToken);
    Task<Result<bool>> CanReadAsync(string path, CancellationToken cancellationToken);
    Task<Result<bool>> CanWriteAsync(string path, CancellationToken cancellationToken);
}

public interface INoteFileStore
{
    Task<IReadOnlyList<StoredFile>> ListAsync(string vaultPath, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string vaultPath, string fileName, CancellationToken cancellationToken);
    Task<Result<string>> CreateAsync(string vaultPath, string fileName, string content, CancellationToken cancellationToken);
    Task<Result<bool>> DeleteAsync(string vaultPath, string fullPath, CancellationToken cancellationToken);
}

public interface INoteDocumentSerializer
{
    string Serialize(Note note, string body);
    ParsedDocument Deserialize(string content, string fileName);
}

public interface IFileNameGenerator { Result<string> CreateSlug(string title); }
public interface IEditorCommandParser { Result<EditorSettings> Parse(string command); }
public interface IExternalEditor
{
    Task<Result<int>> OpenAsync(EditorSettings editor, string filePath, CancellationToken cancellationToken);
    bool CanLocate(string executable);
}

public sealed class ConfigurationService(IConfigurationStore store, IVaultDirectory directory, IEditorCommandParser editorParser)
{
    public async Task<Result<UserConfiguration>> SetVaultAsync(string path, bool create, CancellationToken cancellationToken)
    {
        string normalized;
        try { normalized = directory.Normalize(path); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Result<UserConfiguration>.Failure(new AppError(ErrorKind.InvalidInput, $"Caminho do vault inválido: {ex.Message}"));
        }

        if (!directory.Exists(normalized))
        {
            if (!create)
                return Result<UserConfiguration>.Failure(new AppError(ErrorKind.NotFound, $"O diretório '{normalized}' não existe."));
            await directory.CreateAsync(normalized, cancellationToken);
        }

        var current = await ReadForUpdateAsync(cancellationToken);
        if (!current.IsSuccess) return Result<UserConfiguration>.Failure(current.Error!);
        var updated = current.Value! with { VaultPath = normalized };
        await store.WriteAsync(updated, cancellationToken);
        return Result<UserConfiguration>.Success(updated);
    }

    public async Task<Result<UserConfiguration>> SetEditorAsync(string command, CancellationToken cancellationToken)
    {
        var parsed = editorParser.Parse(command);
        if (!parsed.IsSuccess) return Result<UserConfiguration>.Failure(parsed.Error!);
        var current = await ReadForUpdateAsync(cancellationToken);
        if (!current.IsSuccess) return Result<UserConfiguration>.Failure(current.Error!);
        var updated = current.Value! with { Editor = parsed.Value };
        await store.WriteAsync(updated, cancellationToken);
        return Result<UserConfiguration>.Success(updated);
    }

    public async Task<Result<ConfigurationRead>> ShowAsync(CancellationToken cancellationToken)
    {
        var read = await store.ReadAsync(cancellationToken);
        return read.Status switch
        {
            ConfigurationStatus.Valid => Result<ConfigurationRead>.Success(read),
            ConfigurationStatus.Missing => Result<ConfigurationRead>.Failure(new AppError(ErrorKind.Configuration, $"Arquivo de configuração não encontrado: {read.Path}")),
            _ => Result<ConfigurationRead>.Failure(new AppError(ErrorKind.Configuration, read.Error ?? "A configuração é inválida."))
        };
    }

    private async Task<Result<UserConfiguration>> ReadForUpdateAsync(CancellationToken cancellationToken)
    {
        var read = await store.ReadAsync(cancellationToken);
        return read.Status switch
        {
            ConfigurationStatus.Missing => Result<UserConfiguration>.Success(UserConfiguration.Empty),
            ConfigurationStatus.Valid => Result<UserConfiguration>.Success(read.Configuration!),
            _ => Result<UserConfiguration>.Failure(new AppError(ErrorKind.Configuration, read.Error ?? "A configuração é inválida."))
        };
    }
}

public sealed class NoteService(
    IConfigurationStore configurationStore,
    INoteFileStore fileStore,
    INoteDocumentSerializer serializer,
    IFileNameGenerator fileNameGenerator,
    IExternalEditor editor,
    IVaultDirectory vaultDirectory,
    TimeProvider timeProvider)
{
    public async Task<Result<Note>> CreateAsync(string titleText, bool openEditor, CancellationToken cancellationToken)
    {
        var context = await GetContextAsync(requireEditor: openEditor, cancellationToken);
        if (!context.IsSuccess) return Result<Note>.Failure(context.Error!);
        var title = NoteTitle.Create(titleText);
        if (!title.IsSuccess) return Result<Note>.Failure(title.Error!);
        var slug = fileNameGenerator.CreateSlug(title.Value!.Value);
        if (!slug.IsSuccess) return Result<Note>.Failure(slug.Error!);

        var now = timeProvider.GetLocalNow();
        var id = NoteId.New(timeProvider);
        var fileName = $"{slug.Value}.md";
        if (await fileStore.ExistsAsync(context.Value!.VaultPath!, fileName, cancellationToken))
            fileName = $"{slug.Value}-{id.Value.ToString("N")[^6..]}.md";

        var note = new Note(id, title.Value!, fileName, now, now);
        var content = serializer.Serialize(note, $"# {note.Title.Value}\n");
        var created = await fileStore.CreateAsync(context.Value.VaultPath!, fileName, content, cancellationToken);
        if (!created.IsSuccess) return Result<Note>.Failure(created.Error!);

        if (openEditor)
        {
            var opened = await editor.OpenAsync(context.Value.Editor!, created.Value!, cancellationToken);
            if (!opened.IsSuccess)
                return Result<Note>.Failure(new AppError(opened.Error!.Kind, $"A nota foi criada em '{created.Value}', mas não foi possível abrir o editor: {opened.Error.Message}"));
        }
        return Result<Note>.Success(note);
    }

    public async Task<Result<IReadOnlyList<NoteSummary>>> ListAsync(CancellationToken cancellationToken)
    {
        var context = await GetContextAsync(false, cancellationToken);
        if (!context.IsSuccess) return Result<IReadOnlyList<NoteSummary>>.Failure(context.Error!);
        var notes = await ReadSummariesAsync(context.Value!.VaultPath!, cancellationToken);
        return Result<IReadOnlyList<NoteSummary>>.Success(notes.OrderBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase).ToArray());
    }

    public async Task<Result<NoteSummary>> OpenAsync(string query, CancellationToken cancellationToken)
    {
        var context = await GetContextAsync(true, cancellationToken);
        if (!context.IsSuccess) return Result<NoteSummary>.Failure(context.Error!);
        var resolved = await ResolveAsync(context.Value!.VaultPath!, query, cancellationToken);
        if (!resolved.IsSuccess) return resolved;
        var opened = await editor.OpenAsync(context.Value.Editor!, resolved.Value!.FullPath, cancellationToken);
        return opened.IsSuccess ? resolved : Result<NoteSummary>.Failure(opened.Error!);
    }

    public async Task<Result<DeleteNoteOutcome>> DeleteAsync(string query, bool confirmed, CancellationToken cancellationToken)
    {
        var context = await GetContextAsync(false, cancellationToken);
        if (!context.IsSuccess) return Result<DeleteNoteOutcome>.Failure(context.Error!);
        var resolved = await ResolveAsync(context.Value!.VaultPath!, query, cancellationToken);
        if (!resolved.IsSuccess) return Result<DeleteNoteOutcome>.Failure(resolved.Error!);
        if (!confirmed) return Result<DeleteNoteOutcome>.Success(new DeleteNoteOutcome(resolved.Value!, true));
        var deleted = await fileStore.DeleteAsync(context.Value.VaultPath!, resolved.Value!.FullPath, cancellationToken);
        return deleted.IsSuccess
            ? Result<DeleteNoteOutcome>.Success(new DeleteNoteOutcome(resolved.Value, false))
            : Result<DeleteNoteOutcome>.Failure(deleted.Error!);
    }

    private async Task<Result<UserConfiguration>> GetContextAsync(bool requireEditor, CancellationToken cancellationToken)
    {
        var read = await configurationStore.ReadAsync(cancellationToken);
        if (read.Status != ConfigurationStatus.Valid)
            return Result<UserConfiguration>.Failure(new AppError(ErrorKind.Configuration, read.Error ?? "Configure o vault antes de executar este comando."));
        if (string.IsNullOrWhiteSpace(read.Configuration!.VaultPath))
            return Result<UserConfiguration>.Failure(new AppError(ErrorKind.Configuration, "Nenhum vault foi configurado."));
        if (!vaultDirectory.Exists(read.Configuration.VaultPath))
            return Result<UserConfiguration>.Failure(new AppError(ErrorKind.Configuration, $"O vault configurado não existe: {read.Configuration.VaultPath}"));
        if (requireEditor && read.Configuration.Editor is null)
            return Result<UserConfiguration>.Failure(new AppError(ErrorKind.Configuration, "Nenhum editor foi configurado."));
        return Result<UserConfiguration>.Success(read.Configuration);
    }

    private async Task<IReadOnlyList<NoteSummary>> ReadSummariesAsync(string vaultPath, CancellationToken cancellationToken)
    {
        var files = await fileStore.ListAsync(vaultPath, cancellationToken);
        return files.Select(file =>
        {
            var parsed = serializer.Deserialize(file.Content, file.FileName);
            return parsed.Note is { } note
                ? new NoteSummary(note.Id.ToString(), note.Title.Value, file.FileName, file.FullPath, file.ModifiedAt, false, null)
                : new NoteSummary(null, Path.GetFileNameWithoutExtension(file.FileName), file.FileName, file.FullPath, file.ModifiedAt, true, parsed.Error);
        }).ToArray();
    }

    private async Task<Result<NoteSummary>> ResolveAsync(string vaultPath, string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Result<NoteSummary>.Failure(new AppError(ErrorKind.InvalidInput, "Informe uma nota para localizar."));
        var notes = await ReadSummariesAsync(vaultPath, cancellationToken);
        var q = query.Trim();
        IEnumerable<NoteSummary>[] levels =
        [
            notes.Where(x => x.Id is not null && string.Equals(x.Id, q, StringComparison.OrdinalIgnoreCase)),
            notes.Where(x => string.Equals(x.FileName, q, StringComparison.OrdinalIgnoreCase)),
            notes.Where(x => !x.HasInvalidMetadata && string.Equals(x.Title, q, StringComparison.CurrentCultureIgnoreCase)),
            notes.Where(x => !x.HasInvalidMetadata && x.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase)),
            notes.Where(x => x.FileName.Contains(q, StringComparison.OrdinalIgnoreCase))
        ];
        foreach (var level in levels)
        {
            var matches = level.ToArray();
            if (matches.Length == 1) return Result<NoteSummary>.Success(matches[0]);
            if (matches.Length > 1)
                return Result<NoteSummary>.Failure(new AppError(ErrorKind.Ambiguous, "Mais de uma nota corresponde à consulta.", matches.Select(x => $"{x.Title} ({x.FileName})").ToArray()));
        }
        return Result<NoteSummary>.Failure(new AppError(ErrorKind.NotFound, $"Nenhuma nota encontrada para '{q}'."));
    }
}

public sealed class DoctorService(IConfigurationStore configurationStore, IVaultDirectory directory, IExternalEditor editor)
{
    public async Task<DoctorReport> RunAsync(CancellationToken cancellationToken)
    {
        var checks = new List<DiagnosticCheck>();
        var read = await configurationStore.ReadAsync(cancellationToken);
        checks.Add(new(read.Status == ConfigurationStatus.Missing ? $"Arquivo de configuração não encontrado: {read.Path}" : read.Status == ConfigurationStatus.Valid ? $"Arquivo de configuração válido: {read.Path}" : read.Error ?? "Configuração inválida", read.Status == ConfigurationStatus.Valid, true));
        if (read.Status != ConfigurationStatus.Valid) return new(checks);

        var config = read.Configuration!;
        var hasVault = !string.IsNullOrWhiteSpace(config.VaultPath);
        checks.Add(new(hasVault ? $"Vault configurado: {config.VaultPath}" : "Vault não configurado", hasVault, true));
        if (hasVault)
        {
            var exists = directory.Exists(config.VaultPath!);
            checks.Add(new(exists ? "Diretório do vault existente" : "O vault configurado não existe", exists, true));
            if (exists)
            {
                var readAccess = await directory.CanReadAsync(config.VaultPath!, cancellationToken);
                var writeAccess = await directory.CanWriteAsync(config.VaultPath!, cancellationToken);
                checks.Add(new(readAccess.IsSuccess ? "Vault acessível para leitura" : readAccess.Error!.Message, readAccess.IsSuccess, true));
                checks.Add(new(writeAccess.IsSuccess ? "Vault acessível para escrita" : writeAccess.Error!.Message, writeAccess.IsSuccess, true));
            }
        }
        var hasEditor = config.Editor is not null;
        checks.Add(new(hasEditor ? $"Editor configurado: {config.Editor!.Executable}" : "Editor não configurado", hasEditor, true));
        if (hasEditor)
        {
            var found = editor.CanLocate(config.Editor!.Executable);
            checks.Add(new(found ? "Executável do editor localizado" : $"Executável do editor não localizado: {config.Editor.Executable}", found, true));
        }
        return new(checks);
    }
}
