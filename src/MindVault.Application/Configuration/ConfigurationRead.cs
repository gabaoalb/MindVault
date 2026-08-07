namespace MindVault.Application.Configuration;

public sealed record ConfigurationRead(
    ConfigurationStatusEnum Status,
    UserConfiguration? Configuration,
    string Path,
    string? Error = null);
