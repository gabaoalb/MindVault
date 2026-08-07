using MindVault.Domain.Common;

namespace MindVault.Application.Abstractions.Notes;

public interface IFileNameGenerator
{
    Result<string> CreateSlug(string title);
}
