namespace MindVault.Application.Configuration;

public sealed record EditorSettings(string Executable, IReadOnlyList<string> Arguments);