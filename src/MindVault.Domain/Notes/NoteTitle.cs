using System.Text.RegularExpressions;
using MindVault.Domain.Common;

namespace MindVault.Domain.Notes;

public sealed record NoteTitle
{
    private static readonly Regex Whitespace =
        new(@"\s+", RegexOptions.Compiled);

    private NoteTitle(string value) => Value = value;

    public string Value { get; }

    public static Result<NoteTitle> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<NoteTitle>.Failure(
                new AppError(
                    ErrorKind.InvalidInput,
                    "O título da nota não pode estar vazio."));
        }

        var normalized = Whitespace.Replace(value.Trim(), " ");
        if (normalized.Any(char.IsControl))
        {
            return Result<NoteTitle>.Failure(
                new AppError(
                    ErrorKind.InvalidInput,
                    "O título da nota contém caracteres de controle."));
        }

        if (normalized.Length > 200)
        {
            return Result<NoteTitle>.Failure(
                new AppError(
                    ErrorKind.InvalidInput,
                    "O título da nota deve ter no máximo 200 caracteres."));
        }

        return Result<NoteTitle>.Success(new NoteTitle(normalized));
    }

    public override string ToString() => Value;
}
