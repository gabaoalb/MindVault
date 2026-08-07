namespace MindVault.Domain.Common;

public sealed record AppError(
    ErrorKind Kind,
    string Message,
    IReadOnlyList<string>? Details = null);
