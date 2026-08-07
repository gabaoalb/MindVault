using MindVault.Application.Abstractions.Configuration;
using MindVault.Domain.Common;

namespace MindVault.Infrastructure.Configuration;

public sealed class VaultDirectory : IVaultDirectory
{
    public string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "O caminho não pode estar vazio.",
                nameof(path));
        }

        return Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(
                Environment.ExpandEnvironmentVariables(path.Trim())));
    }

    public bool Exists(string path) => Directory.Exists(path);

    public Task CreateAsync(
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(path);
        return Task.CompletedTask;
    }

    public Task<Result<bool>> CanReadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = Directory
                .EnumerateFileSystemEntries(path)
                .Take(1)
                .ToArray();
            return Task.FromResult(Result<bool>.Success(true));
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(
                Result<bool>.Failure(
                    new AppError(
                        ErrorKind.Configuration,
                        $"Sem acesso de leitura ao vault: {exception.Message}")));
        }
    }

    public async Task<Result<bool>> CanWriteAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var probe = Path.Combine(
            path,
            $".mind-write-test-{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllTextAsync(
                probe,
                string.Empty,
                cancellationToken);
            File.Delete(probe);
            return Result<bool>.Success(true);
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException)
        {
            if (File.Exists(probe))
            {
                File.Delete(probe);
            }

            return Result<bool>.Failure(
                new AppError(
                    ErrorKind.Configuration,
                    $"Sem acesso de escrita ao vault: {exception.Message}"));
        }
    }
}
