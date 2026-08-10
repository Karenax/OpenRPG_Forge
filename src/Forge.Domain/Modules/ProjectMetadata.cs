using Forge.Domain.Projects;

namespace Forge.Domain.Modules;

public sealed record ProjectMetadata
{
    public const int CurrentSchemaVersion = 1;

    public ProjectMetadataId Id { get; }

    public int SchemaVersion { get; }

    public string DefaultLocale { get; }

    public IReadOnlyList<ProjectLocalizedMetadata> Localizations { get; }

    public string Author { get; }

    public string Version { get; }

    public string License { get; }

    public string? ImagePath { get; }

    public IReadOnlyList<string> Tags { get; }

    public string Classification { get; }

    public string Provenance { get; }

    public ProjectFoundationProfile? Foundation { get; }

    public ProjectMetadata(
        ProjectMetadataId id,
        string defaultLocale,
        IReadOnlyList<ProjectLocalizedMetadata> localizations,
        string author,
        string version,
        string license,
        string? imagePath,
        IReadOnlyList<string>? tags,
        string classification,
        string provenance,
        int schemaVersion = CurrentSchemaVersion,
        ProjectFoundationProfile? foundation = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultLocale);
        ArgumentNullException.ThrowIfNull(localizations);
        ArgumentException.ThrowIfNullOrWhiteSpace(author);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(license);
        ArgumentException.ThrowIfNullOrWhiteSpace(classification);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                $"Project metadata schema '{schemaVersion}' is not supported.");
        }

        var materializedLocalizations = localizations.ToArray();
        if (materializedLocalizations.Length == 0)
        {
            throw new ArgumentException("Project metadata requires at least one localization.", nameof(localizations));
        }

        if (materializedLocalizations.Select(item => item.Locale)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != materializedLocalizations.Length)
        {
            throw new ArgumentException("Project metadata locales must be unique.", nameof(localizations));
        }

        var normalizedLocale = defaultLocale.Trim();
        if (!materializedLocalizations.Any(item =>
                string.Equals(item.Locale, normalizedLocale, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                $"Default locale '{defaultLocale}' does not have localized metadata.",
                nameof(defaultLocale));
        }

        var normalizedImagePath = string.IsNullOrWhiteSpace(imagePath) ? null : imagePath.Trim();
        if (normalizedImagePath is not null && !IsSafeRelativePath(normalizedImagePath))
        {
            throw new ArgumentException(
                "The metadata image path must be a safe relative project path.",
                nameof(imagePath));
        }

        if (!IsSemanticVersion(version.Trim()))
        {
            throw new ArgumentException($"Version '{version}' is not semantic.", nameof(version));
        }

        Id = id;
        SchemaVersion = schemaVersion;
        DefaultLocale = normalizedLocale;
        Localizations = materializedLocalizations
            .OrderBy(item => item.Locale, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Author = author.Trim();
        Version = version.Trim();
        License = license.Trim();
        ImagePath = normalizedImagePath;
        Tags = (tags ?? [])
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Classification = classification.Trim();
        Provenance = provenance.Trim();
        Foundation = foundation;
    }

    public ProjectLocalizedMetadata GetDefaultLocalization() => Localizations.Single(item =>
        string.Equals(item.Locale, DefaultLocale, StringComparison.OrdinalIgnoreCase));

    public static ProjectMetadata CreateDefault(ModuleId moduleId, string displayName, string description)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(description);
        return new ProjectMetadata(
            ProjectMetadataId.From(moduleId.Value),
            "fr",
            [new ProjectLocalizedMetadata("fr", displayName, description, description, string.Empty)],
            "Non renseigné",
            "0.1.0",
            "UNLICENSED",
            imagePath: null,
            tags: [],
            "Non classé",
            "migration-default");
    }

    private static bool IsSafeRelativePath(string path) =>
        !Path.IsPathRooted(path) &&
        !path.Contains('\\') &&
        path.Split('/').All(segment => segment is not "" and not "." and not "..");

    private static bool IsSemanticVersion(string version)
    {
        var core = version.Split('-', 2)[0].Split('+', 2)[0];
        var segments = core.Split('.');
        return segments.Length == 3 && segments.All(segment =>
            int.TryParse(segment, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out _));
    }
}
