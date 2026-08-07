namespace MindVault.Domain.Common;

public enum ErrorKind
{
    InvalidInput,
    Configuration,
    NotFound,
    Ambiguous,
    Cancelled,
    Conflict,
    ExternalTool,
    InvalidDocument
}
