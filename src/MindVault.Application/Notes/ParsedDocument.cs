using MindVault.Domain.Notes;

namespace MindVault.Application.Notes;

public sealed record ParsedDocument(Note? Note, string? Error)
{
    public bool IsValid => Note is not null;
}
