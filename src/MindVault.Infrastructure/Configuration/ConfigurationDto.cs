namespace MindVault.Infrastructure.Configuration;

internal sealed record ConfigurationDto(
    string? VaultPath,
    string? Editor,
    string[]? EditorArguments);
