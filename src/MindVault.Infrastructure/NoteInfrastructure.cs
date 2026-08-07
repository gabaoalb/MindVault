using System.Globalization;
using System.Text;
using MindVault.Application;
using MindVault.Domain;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MindVault.Infrastructure;

public sealed class SlugFileNameGenerator : IFileNameGenerator
{
    public Result<string> CreateSlug(string title)
    {
        var normalized = title.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var pendingSeparator = false;
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0) builder.Append('-');
                builder.Append(char.ToLowerInvariant(character));
                pendingSeparator = false;
            }
            else pendingSeparator = true;
        }
        var slug = builder.ToString();
        return slug.Length == 0
            ? Result<string>.Failure(new AppError(ErrorKind.InvalidInput, "O título não produz um nome de arquivo válido."))
            : Result<string>.Success(slug.Length <= 120 ? slug : slug[..120].TrimEnd('-'));
    }
}

public sealed class YamlNoteDocumentSerializer : INoteDocumentSerializer
{
    private readonly ISerializer yamlSerializer = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
    private readonly IDeserializer yamlDeserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();

    public string Serialize(Note note, string body)
    {
        var metadata = new Metadata
        {
            Id = note.Id.ToString(), Title = note.Title.Value,
            Created = note.CreatedAt.ToString("O"), Updated = note.UpdatedAt.ToString("O")
        };
        var yaml = yamlSerializer.Serialize(metadata).TrimEnd();
        return $"---\n{yaml}\n---\n\n{body.TrimStart()}";
    }

    public ParsedDocument Deserialize(string content, string fileName)
    {
        try
        {
            using var reader = new StringReader(content);
            if (!string.Equals(reader.ReadLine(), "---", StringComparison.Ordinal))
                return new(null, $"'{fileName}' não possui frontmatter YAML.");
            var yaml = new StringBuilder();
            string? line;
            var closed = false;
            while ((line = reader.ReadLine()) is not null)
            {
                if (line == "---") { closed = true; break; }
                yaml.AppendLine(line);
            }
            if (!closed) return new(null, $"Frontmatter de '{fileName}' não foi fechado.");
            var metadata = yamlDeserializer.Deserialize<Metadata>(yaml.ToString());
            if (metadata is null) return new(null, $"Frontmatter de '{fileName}' está vazio.");
            var id = NoteId.Parse(metadata.Id ?? string.Empty);
            var title = NoteTitle.Create(metadata.Title);
            if (!id.IsSuccess) return new(null, $"ID inválido em '{fileName}'.");
            if (!title.IsSuccess) return new(null, $"Título inválido em '{fileName}'.");
            if (!DateTimeOffset.TryParseExact(metadata.Created, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var created))
                return new(null, $"Data created inválida em '{fileName}'.");
            if (!DateTimeOffset.TryParseExact(metadata.Updated, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var updated))
                return new(null, $"Data updated inválida em '{fileName}'.");
            return new(new Note(id.Value!, title.Value!, fileName, created, updated), null);
        }
        catch (Exception ex) when (ex is YamlDotNet.Core.YamlException or InvalidOperationException)
        { return new(null, $"Frontmatter inválido em '{fileName}': {ex.Message}"); }
    }

    public sealed class Metadata
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Created { get; set; }
        public string? Updated { get; set; }
    }
}

public sealed class PhysicalNoteFileStore : INoteFileStore
{
    public async Task<IReadOnlyList<StoredFile>> ListAsync(string vaultPath, CancellationToken cancellationToken)
    {
        var root = NormalizeRoot(vaultPath);
        var results = new List<StoredFile>();
        foreach (var path in Directory.EnumerateFiles(root, "*.md", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsLink(path)) continue;
            var content = await File.ReadAllTextAsync(path, cancellationToken);
            results.Add(new(Path.GetFileName(path), path, File.GetLastWriteTimeUtc(path), content));
        }
        return results;
    }

    public Task<bool> ExistsAsync(string vaultPath, string fileName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(SafeChild(vaultPath, fileName)));
    }

    public async Task<Result<string>> CreateAsync(string vaultPath, string fileName, string content, CancellationToken cancellationToken)
    {
        var path = SafeChild(vaultPath, fileName);
        try
        {
            await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteAsync(content.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
            return Result<string>.Success(path);
        }
        catch (IOException) when (File.Exists(path))
        { return Result<string>.Failure(new AppError(ErrorKind.Conflict, $"O arquivo '{fileName}' já existe; nenhuma nota foi sobrescrita.")); }
    }

    public Task<Result<bool>> DeleteAsync(string vaultPath, string fullPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = NormalizeRoot(vaultPath);
        var path = Path.GetFullPath(fullPath);
        EnsureDirectChild(root, path);
        if (!File.Exists(path)) return Task.FromResult(Result<bool>.Failure(new AppError(ErrorKind.NotFound, "O arquivo da nota não existe mais.")));
        if (IsLink(path)) return Task.FromResult(Result<bool>.Failure(new AppError(ErrorKind.InvalidInput, "Links simbólicos não podem ser excluídos pelo MindVault.")));
        File.Delete(path);
        return Task.FromResult(Result<bool>.Success(true));
    }

    private static string SafeChild(string vaultPath, string fileName)
    {
        if (!string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal) || !fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("O nome do arquivo não é um Markdown simples.");
        var root = NormalizeRoot(vaultPath);
        var path = Path.GetFullPath(Path.Combine(root, fileName));
        EnsureDirectChild(root, path);
        return path;
    }
    private static string NormalizeRoot(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    private static void EnsureDirectChild(string root, string path)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(Path.GetDirectoryName(path), root, comparison))
            throw new InvalidOperationException("O caminho solicitado está fora da raiz do vault.");
    }
    private static bool IsLink(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
}
