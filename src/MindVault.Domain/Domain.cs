using System.Text.RegularExpressions;

namespace MindVault.Domain;

public enum ErrorKind
{
    InvalidInput,
    Configuration,
    NotFound,
    Ambiguous,
    Cancelled,
    Conflict,
    ExternalTool,
    InvalidDocument
}

public sealed record AppError(ErrorKind Kind, string Message, IReadOnlyList<string>? Details = null);

public readonly record struct Result<T>
{
    private Result(T? value, AppError? error) { Value = value; Error = error; }
    public T? Value { get; }
    public AppError? Error { get; }
    public bool IsSuccess => Error is null;
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(AppError error) => new(default, error);
}

public readonly record struct NoteId
{
    private NoteId(Guid value) => Value = value;
    public Guid Value { get; }
    public static NoteId New(TimeProvider timeProvider) => new(Guid.CreateVersion7(timeProvider.GetUtcNow()));
    public static Result<NoteId> Parse(string value) =>
        Guid.TryParseExact(value, "D", out var parsed) && parsed.Version == 7
            ? Result<NoteId>.Success(new NoteId(parsed))
            : Result<NoteId>.Failure(new AppError(ErrorKind.InvalidInput, "O identificador da nota não é um UUIDv7 válido."));
    public override string ToString() => Value.ToString("D");
}

public sealed record NoteTitle
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    private NoteTitle(string value) => Value = value;
    public string Value { get; }

    public static Result<NoteTitle> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<NoteTitle>.Failure(new AppError(ErrorKind.InvalidInput, "O título da nota não pode estar vazio."));

        var normalized = Whitespace.Replace(value.Trim(), " ");
        if (normalized.Any(char.IsControl))
            return Result<NoteTitle>.Failure(new AppError(ErrorKind.InvalidInput, "O título da nota contém caracteres de controle."));
        if (normalized.Length > 200)
            return Result<NoteTitle>.Failure(new AppError(ErrorKind.InvalidInput, "O título da nota deve ter no máximo 200 caracteres."));

        return Result<NoteTitle>.Success(new NoteTitle(normalized));
    }

    public override string ToString() => Value;
}

public sealed record Note(NoteId Id, NoteTitle Title, string FileName, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
