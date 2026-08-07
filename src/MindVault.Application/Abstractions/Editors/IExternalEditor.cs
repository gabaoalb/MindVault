using MindVault.Application.Configuration;
using MindVault.Domain.Common;

namespace MindVault.Application.Abstractions.Editors;

public interface IExternalEditor
{
    Task<Result<int>> OpenAsync(EditorSettings editor, string filePath, CancellationToken cancellationToken);

    bool CanLocate(string executable);
}
