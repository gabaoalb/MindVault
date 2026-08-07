using System.Globalization;
using System.Text;
using MindVault.Application.Abstractions.Notes;
using MindVault.Domain.Common;

namespace MindVault.Infrastructure.Notes;

public sealed class SlugFileNameGenerator : IFileNameGenerator
{
    public Result<string> CreateSlug(string title)
    {
        var normalized = title.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var pendingSeparator = false;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) ==
                UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(character));
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }
        }

        var slug = builder.ToString();
        return slug.Length == 0
            ? Result<string>.Failure(
                new AppError(
                    ErrorKind.InvalidInput,
                    "O título não produz um nome de arquivo válido."))
            : Result<string>.Success(
                slug.Length <= 120
                    ? slug
                    : slug[..120].TrimEnd('-'));
    }
}
