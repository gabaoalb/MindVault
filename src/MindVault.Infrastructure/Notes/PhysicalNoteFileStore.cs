using System.Text;
using MindVault.Application.Abstractions.Notes;
using MindVault.Application.Notes;
using MindVault.Domain.Common;

namespace MindVault.Infrastructure.Notes;

public sealed class PhysicalNoteFileStore : INoteFileStore
{
    public async Task<IReadOnlyList<StoredFile>> ListAsync(
        string vaultPath,
        CancellationToken cancellationToken)
    {
        var root = NormalizeRoot(vaultPath);
        var results = new List<StoredFile>();

        foreach (var path in Directory.EnumerateFiles(
            root,
            "*.md",
            SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsLink(path))
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(
                path,
                cancellationToken);
            results.Add(
                new StoredFile(
                    Path.GetFileName(path),
                    path,
                    File.GetLastWriteTimeUtc(path),
                    content));
        }

        return results;
    }

    public Task<bool> ExistsAsync(
        string vaultPath,
        string fileName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            File.Exists(SafeChild(vaultPath, fileName)));
    }

    public async Task<Result<string>> CreateAsync(
        string vaultPath,
        string fileName,
        string content,
        CancellationToken cancellationToken)
    {
        var path = SafeChild(vaultPath, fileName);
        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                true);
            await using var writer = new StreamWriter(
                stream,
                new UTF8Encoding(false));
            await writer.WriteAsync(
                content.AsMemory(),
                cancellationToken);
            await writer.FlushAsync(cancellationToken);
            return Result<string>.Success(path);
        }
        catch (IOException) when (File.Exists(path))
        {
            return Result<string>.Failure(
                new AppError(
                    ErrorKind.Conflict,
                    $"O arquivo '{fileName}' já existe; nenhuma nota foi sobrescrita."));
        }
    }

    public Task<Result<bool>> DeleteAsync(
        string vaultPath,
        string fullPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = NormalizeRoot(vaultPath);
        var path = Path.GetFullPath(fullPath);
        EnsureDirectChild(root, path);

        if (!File.Exists(path))
        {
            return Task.FromResult(
                Result<bool>.Failure(
                    new AppError(
                        ErrorKind.NotFound,
                        "O arquivo da nota não existe mais.")));
        }

        if (IsLink(path))
        {
            return Task.FromResult(
                Result<bool>.Failure(
                    new AppError(
                        ErrorKind.InvalidInput,
                        "Links simbólicos não podem ser excluídos pelo MindVault.")));
        }

        File.Delete(path);
        return Task.FromResult(Result<bool>.Success(true));
    }

    private static string SafeChild(
        string vaultPath,
        string fileName)
    {
        if (!string.Equals(
                Path.GetFileName(fileName),
                fileName,
                StringComparison.Ordinal) ||
            !fileName.EndsWith(
                ".md",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "O nome do arquivo não é um Markdown simples.");
        }

        var root = NormalizeRoot(vaultPath);
        var path = Path.GetFullPath(Path.Combine(root, fileName));
        EnsureDirectChild(root, path);
        return path;
    }

    private static string NormalizeRoot(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static void EnsureDirectChild(string root, string path)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!string.Equals(
            Path.GetDirectoryName(path),
            root,
            comparison))
        {
            throw new InvalidOperationException(
                "O caminho solicitado está fora da raiz do vault.");
        }
    }

    private static bool IsLink(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
}
