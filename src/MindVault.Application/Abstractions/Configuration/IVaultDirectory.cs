using MindVault.Domain.Common;

namespace MindVault.Application.Abstractions.Configuration;

public interface IVaultDirectory
{
    string Normalize(string path);

    bool Exists(string path);

    Task CreateAsync(string path, CancellationToken cancellationToken);

    Task<Result<bool>> CanReadAsync(string path, CancellationToken cancellationToken);

    Task<Result<bool>> CanWriteAsync(string path, CancellationToken cancellationToken);
}
