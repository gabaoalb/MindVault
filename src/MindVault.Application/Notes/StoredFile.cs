namespace MindVault.Application.Notes;

public sealed record StoredFile(
    string FileName,
    string FullPath,
    DateTimeOffset ModifiedAt,
    string Content);
