using System.Globalization;
using System.Text;

namespace Forge.Application.Projects;

public static class SlugGenerator
{
    public static string FromDisplayName(string value, string fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallback);

        var result = new StringBuilder();
        var separatorPending = false;
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                if (separatorPending && result.Length > 0)
                {
                    result.Append('-');
                }

                result.Append(char.ToLowerInvariant(character));
                separatorPending = false;
            }
            else
            {
                separatorPending = result.Length > 0;
            }
        }

        return result.Length == 0 ? fallback : result.ToString();
    }
}
