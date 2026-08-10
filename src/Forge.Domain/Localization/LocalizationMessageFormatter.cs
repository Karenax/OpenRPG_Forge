using System.Globalization;
using System.Text;

namespace Forge.Domain.Localization;

public static class LocalizationMessageFormatter
{
    public static string Format(
        string template,
        IReadOnlyDictionary<string, object?> arguments,
        string locale)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(arguments);
        var culture = CreateCulture(locale);
        return FormatCore(template, arguments, culture, 0);
    }

    public static bool IsValid(string template)
    {
        try
        {
            var arguments = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var index = 0; index < template.Length; index++)
            {
                if (template[index] != '{') continue;
                var end = template.IndexOfAny([',', '}'], index + 1);
                if (end <= index + 1) continue;
                var variable = template[(index + 1)..end].Trim();
                if (variable.All(character => char.IsLetterOrDigit(character) || character is '_' or '-'))
                    arguments.TryAdd(variable, 1m);
            }

            _ = Format(template, arguments, "en");
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string FormatCore(
        string template,
        IReadOnlyDictionary<string, object?> arguments,
        CultureInfo culture,
        int depth)
    {
        if (depth > 8)
        {
            throw new FormatException("Localization message nesting exceeds eight levels.");
        }

        var result = new StringBuilder(template.Length);
        for (var index = 0; index < template.Length; index++)
        {
            if (template[index] == '\\' && index + 1 < template.Length && template[index + 1] is '{' or '}')
            {
                result.Append(template[++index]);
                continue;
            }

            if (template[index] != '{')
            {
                if (template[index] == '}')
                {
                    throw new FormatException("Localization message contains an unmatched closing brace.");
                }

                result.Append(template[index]);
                continue;
            }

            var end = FindClosingBrace(template, index);
            var expression = template[(index + 1)..end];
            var parts = SplitTopLevel(expression, ',', 3);
            var variable = parts[0].Trim();
            if (string.IsNullOrWhiteSpace(variable))
            {
                throw new FormatException("Localization placeholders require a variable name.");
            }

            arguments.TryGetValue(variable, out var value);
            if (parts.Count == 1)
            {
                result.Append(value is null ? $"{{{variable}}}" : Convert.ToString(value, culture));
            }
            else
            {
                if (parts.Count != 3)
                {
                    throw new FormatException($"ICU expression '{variable}' is incomplete.");
                }

                var kind = parts[1].Trim();
                var options = ParseOptions(parts[2]);
                var selected = kind switch
                {
                    "plural" => SelectPlural(variable, value, options, culture),
                    "select" => SelectValue(variable, value, options),
                    _ => throw new FormatException($"ICU expression kind '{kind}' is unsupported."),
                };
                var nested = FormatCore(selected.Template, arguments, culture, depth + 1);
                if (selected.Number is not null)
                {
                    nested = nested.Replace("#", selected.Number.Value.ToString(culture), StringComparison.Ordinal);
                }

                result.Append(nested);
            }

            index = end;
        }

        return result.ToString();
    }

    private static SelectedOption SelectPlural(
        string variable,
        object? value,
        IReadOnlyDictionary<string, string> options,
        CultureInfo culture)
    {
        if (value is null || !decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
                NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            throw new FormatException($"Plural variable '{variable}' must be numeric.");
        }

        var exact = $"={number.ToString(CultureInfo.InvariantCulture)}";
        var category = number == 0 && options.ContainsKey("zero") ? "zero" :
            number == 1 ? "one" : number == 2 && options.ContainsKey("two") ? "two" : "other";
        if (!options.TryGetValue(exact, out var template) && !options.TryGetValue(category, out template) &&
            !options.TryGetValue("other", out template))
        {
            throw new FormatException($"Plural variable '{variable}' requires an 'other' option.");
        }

        return new SelectedOption(template, number);
    }

    private static SelectedOption SelectValue(
        string variable,
        object? value,
        IReadOnlyDictionary<string, string> options)
    {
        var key = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim().ToLowerInvariant() ?? "other";
        if (!options.TryGetValue(key, out var template) && !options.TryGetValue("other", out template))
        {
            throw new FormatException($"Select variable '{variable}' requires an 'other' option.");
        }

        return new SelectedOption(template, null);
    }

    private static IReadOnlyDictionary<string, string> ParseOptions(string body)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        var index = 0;
        while (index < body.Length)
        {
            while (index < body.Length && char.IsWhiteSpace(body[index])) index++;
            var keyStart = index;
            while (index < body.Length && !char.IsWhiteSpace(body[index]) && body[index] != '{') index++;
            var key = body[keyStart..index].Trim().ToLowerInvariant();
            while (index < body.Length && char.IsWhiteSpace(body[index])) index++;
            if (string.IsNullOrWhiteSpace(key) || index >= body.Length || body[index] != '{')
            {
                throw new FormatException("ICU options must use 'selector {message}'.");
            }

            var end = FindClosingBrace(body, index);
            if (!options.TryAdd(key, body[(index + 1)..end]))
            {
                throw new FormatException($"ICU option '{key}' is duplicated.");
            }

            index = end + 1;
        }

        if (!options.ContainsKey("other"))
        {
            throw new FormatException("ICU plural/select expressions require an 'other' option.");
        }

        return options;
    }

    private static int FindClosingBrace(string text, int opening)
    {
        var depth = 0;
        for (var index = opening; index < text.Length; index++)
        {
            if (text[index] == '\\')
            {
                index++;
                continue;
            }

            if (text[index] == '{') depth++;
            else if (text[index] == '}' && --depth == 0) return index;
        }

        throw new FormatException("Localization message contains an unmatched opening brace.");
    }

    private static IReadOnlyList<string> SplitTopLevel(string text, char separator, int maximumParts)
    {
        var result = new List<string>();
        var start = 0;
        var depth = 0;
        for (var index = 0; index < text.Length && result.Count < maximumParts - 1; index++)
        {
            if (text[index] == '{') depth++;
            else if (text[index] == '}') depth--;
            else if (text[index] == separator && depth == 0)
            {
                result.Add(text[start..index]);
                start = index + 1;
            }
        }

        result.Add(text[start..]);
        return result;
    }

    private static CultureInfo CreateCulture(string locale)
    {
        try
        {
            return CultureInfo.GetCultureInfo(LocalizationCatalog.NormalizeLocale(locale));
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    private sealed record SelectedOption(string Template, decimal? Number);
}
