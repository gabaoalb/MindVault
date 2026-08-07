using MindVault.Domain.Common;

namespace MindVault.Domain.Notes;

public readonly record struct NoteId
{
    private NoteId(Guid value) => Value = value;

    public Guid Value { get; }

    public static NoteId New(TimeProvider timeProvider) =>
        new(Guid.CreateVersion7(timeProvider.GetUtcNow()));

    public static Result<NoteId> Parse(string value) =>
        Guid.TryParseExact(value, "D", out var parsed) && parsed.Version == 7
            ? Result<NoteId>.Success(new NoteId(parsed))
            : Result<NoteId>.Failure(
                new AppError(
                    ErrorKind.InvalidInput,
                    "O identificador da nota não é um UUIDv7 válido."));

    public override string ToString() => Value.ToString("D");
}
