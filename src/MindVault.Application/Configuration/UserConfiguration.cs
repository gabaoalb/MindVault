namespace MindVault.Application.Configuration;

public sealed record UserConfiguration(string? VaultPath, EditorSettings? Editor)
{
    public static UserConfiguration Empty { get; } = new(null, null);
}
