namespace MindVault.Application.Diagnostics;

public sealed record DiagnosticCheck(string Message,
    bool IsSuccess,
    bool IsBlocking);
