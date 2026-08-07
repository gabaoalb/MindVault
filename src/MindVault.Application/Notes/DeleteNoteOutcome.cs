namespace MindVault.Application.Notes;

public sealed record DeleteNoteOutcome(NoteSummary Note, bool RequiresConfirmation);