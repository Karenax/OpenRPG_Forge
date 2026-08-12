namespace Forge.Domain.Modules;

public sealed record ProjectLocalizedMetadata
{
    public string Locale { get; }

    public string Name { get; }

    public string Summary { get; }

    public string Description { get; }

    public string Credits { get; }

    public string Installation { get; }

    public string Rules { get; }

    public string Controls { get; }

    public string Licenses { get; }

    public string Warnings { get; }

    public string Compatibility { get; }

    public ProjectLocalizedMetadata(
        string locale,
        string name,
        string summary,
        string description,
        string credits,
        string installation = "",
        string rules = "",
        string controls = "",
        string licenses = "",
        string warnings = "",
        string compatibility = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(credits);
        ArgumentNullException.ThrowIfNull(installation);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(controls);
        ArgumentNullException.ThrowIfNull(licenses);
        ArgumentNullException.ThrowIfNull(warnings);
        ArgumentNullException.ThrowIfNull(compatibility);
        var normalizedLocale = locale.Trim();
        if (!IsValidLocale(normalizedLocale))
        {
            throw new ArgumentException(
                $"Locale '{locale}' must use a language tag such as 'fr' or 'fr-FR'.",
                nameof(locale));
        }

        Locale = normalizedLocale;
        Name = name.Trim();
        Summary = summary.Trim();
        Description = description.Trim();
        Credits = credits.Trim();
        Installation = installation.Trim();
        Rules = rules.Trim();
        Controls = controls.Trim();
        Licenses = licenses.Trim();
        Warnings = warnings.Trim();
        Compatibility = compatibility.Trim();
    }

    private static bool IsValidLocale(string locale)
    {
        var segments = locale.Split('-');
        return segments.Length is 1 or 2 &&
            segments[0].Length is 2 or 3 && segments[0].All(char.IsLetter) &&
            (segments.Length == 1 || segments[1].Length is 2 or 4 && segments[1].All(char.IsLetter));
    }
}
