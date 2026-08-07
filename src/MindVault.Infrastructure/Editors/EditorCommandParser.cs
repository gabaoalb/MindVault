using System.Text;
using MindVault.Application.Abstractions.Editors;
using MindVault.Application.Configuration;
using MindVault.Domain.Common;

namespace MindVault.Infrastructure.Editors;

public sealed class EditorCommandParser : IEditorCommandParser
{
    public Result<EditorSettings> Parse(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return Result<EditorSettings>.Failure(
                new AppError(
                    ErrorKind.InvalidInput,
                    "O comando do editor não pode estar vazio."));
        }

        var tokens = new List<string>();
        var current = new StringBuilder();
        char? quote = null;
        var escaped = false;

        foreach (var character in command.Trim())
        {
            if (escaped)
            {
                current.Append(character);
                escaped = false;
                continue;
            }

            if (character == '\\' && quote is not null)
            {
                escaped = true;
                continue;
            }

            if (character is '\'' or '"')
            {
                if (quote == character)
                {
                    quote = null;
                }
                else if (quote is null)
                {
                    quote = character;
                }
                else
                {
                    current.Append(character);
                }

                continue;
            }

            if (char.IsWhiteSpace(character) && quote is null)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(character);
            }
        }

        if (escaped)
        {
            current.Append('\\');
        }

        if (quote is not null)
        {
            return Result<EditorSettings>.Failure(
                new AppError(
                    ErrorKind.InvalidInput,
                    "O comando do editor contém aspas não fechadas."));
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        if (tokens.Count == 0)
        {
            return Result<EditorSettings>.Failure(
                new AppError(
                    ErrorKind.InvalidInput,
                    "O executável do editor não foi informado."));
        }

        return Result<EditorSettings>.Success(
            new EditorSettings(tokens[0], tokens.Skip(1).ToArray()));
    }
}
