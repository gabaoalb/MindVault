using MindVault.Application.Configuration;
using MindVault.Domain.Common;

namespace MindVault.Application.Abstractions.Editors;

public interface IEditorCommandParser
{
    Result<EditorSettings> Parse(string command);
}
