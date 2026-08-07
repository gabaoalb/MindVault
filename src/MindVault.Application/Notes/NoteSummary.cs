namespace MindVault.Application.Notes;

public sealed record NoteSummary(
    string? Id,
    string Title,
    string FileName,
    string FullPath,
    DateTimeOffset ModifiedAt,
    bool HasInvalidMetadata,
    string? MetadataError);
