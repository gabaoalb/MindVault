namespace MindVault.Application.Diagnostics;

public sealed record DoctorReport(IReadOnlyList<DiagnosticCheck> Checks)
{
    public bool IsHealthy => Checks.All(check => !check.IsBlocking || check.IsSuccess);
}
