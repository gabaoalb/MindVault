using MindVault.Application.Notes;
using MindVault.Domain.Common;

namespace MindVault.Application.Abstractions.Notes;

public interface INoteFileStore
{
    Task<IReadOnlyList<StoredFile>> ListAsync(
        string vaultPath,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(
        string vaultPath,
        string fileName,
        CancellationToken cancellationToken);

    Task<Result<string>> CreateAsync(
        string vaultPath,
        string fileName,
        string content,
        CancellationToken cancellationToken);

    Task<Result<bool>> DeleteAsync(
        string vaultPath,
        string fullPath,
        CancellationToken cancellationToken);
}
