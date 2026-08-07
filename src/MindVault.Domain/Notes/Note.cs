namespace MindVault.Domain.Notes;

public sealed record Note(
    NoteId Id,
    NoteTitle Title,
    string FileName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
