using Forge.Domain.Content;

namespace Forge.Domain.Blueprints;

public sealed class BlueprintAuthoringMetadata : IEquatable<BlueprintAuthoringMetadata>
{
    public const int CurrentSchemaVersion = 1;
    public const long DefaultPlatformByteBudget = 1_048_576;

    public int SchemaVersion { get; }
    public string Version { get; }
    public string PackageNamespace { get; }
    public string ManifestPath { get; }
    public IReadOnlyList<string> ExposedParameters { get; }
    public IReadOnlyList<string> Dependencies { get; }
    public string? PreviewPath { get; }
    public long PlatformByteBudget { get; }
    public string Kit { get; }
    public IReadOnlyList<string> Styles { get; }
    public IReadOnlyList<string> Compatibility { get; }
    public IReadOnlyList<string> Tags { get; }

    public BlueprintAuthoringMetadata(
        int schemaVersion,
        string version,
        string packageNamespace,
        string manifestPath,
        IReadOnlyList<string>? exposedParameters = null,
        IReadOnlyList<string>? dependencies = null,
        string? previewPath = null,
        long platformByteBudget = DefaultPlatformByteBudget,
        string? kit = null,
        IReadOnlyList<string>? styles = null,
        IReadOnlyList<string>? compatibility = null,
        IReadOnlyList<string>? tags = null)
    {
        if (schemaVersion != CurrentSchemaVersion)
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), "Unsupported blueprint authoring schema.");
        if (!SemanticVersion.TryParse(version, out _))
            throw new ArgumentException("Blueprint version must use semantic versioning.", nameof(version));
        var normalizedNamespace = (packageNamespace ?? string.Empty).Trim().ToLowerInvariant();
        if (!ValidNamespace(normalizedNamespace))
            throw new ArgumentException("Blueprint package namespace must use lowercase dotted identifiers.", nameof(packageNamespace));
        var normalizedManifest = NormalizePathOrPackUri(manifestPath, nameof(manifestPath));
        var normalizedPreview = string.IsNullOrWhiteSpace(previewPath)
            ? null
            : NormalizeRelativePath(previewPath, nameof(previewPath));
        var parameters = NormalizeTokens(exposedParameters, nameof(exposedParameters));
        var normalizedDependencies = (dependencies ?? [])
            .Select(value => NormalizePathOrPackUri(value, nameof(dependencies)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (platformByteBudget is <= 0 or > 16_777_216)
            throw new ArgumentOutOfRangeException(nameof(platformByteBudget), "Blueprint platform budget must be between 1 byte and 16 MiB.");

        SchemaVersion = schemaVersion;
        Version = version.Trim();
        PackageNamespace = normalizedNamespace;
        ManifestPath = normalizedManifest;
        ExposedParameters = parameters;
        Dependencies = normalizedDependencies;
        PreviewPath = normalizedPreview;
        PlatformByteBudget = platformByteBudget;
        Kit = string.IsNullOrWhiteSpace(kit) ? normalizedNamespace : kit.Trim().ToLowerInvariant();
        if (Kit.Length > 160 || Kit.Any(character => !char.IsLetterOrDigit(character) && character is not '.' and not '-' and not '_'))
            throw new ArgumentException("Blueprint kit must be a stable identifier.", nameof(kit));
        Styles = NormalizeTokens(styles, nameof(styles));
        Compatibility = NormalizeTokens(compatibility, nameof(compatibility));
        Tags = NormalizeTokens(tags, nameof(tags));
    }

    public static BlueprintAuthoringMetadata CreateDefault(
        BlueprintId id,
        string slug,
        BlueprintPaletteOrigin origin,
        BlueprintProvenance provenance)
    {
        if (origin == BlueprintPaletteOrigin.InstalledPack && TryReadPack(provenance.Source, out var pack, out var version))
        {
            return new BlueprintAuthoringMetadata(
                CurrentSchemaVersion,
                version,
                NormalizeNamespace(pack),
                $"pack://{pack}/{version}/manifest.json");
        }

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        return new BlueprintAuthoringMetadata(
            CurrentSchemaVersion,
            "1.0.0",
            $"project.{NormalizeNamespace(normalizedSlug)}",
            $"content/blueprints/{id}/{normalizedSlug}.blueprint.json",
            previewPath: $"content/blueprints/{id}/{normalizedSlug}.preview.png");
    }

    private static bool TryReadPack(string source, out string package, out string version)
    {
        package = string.Empty;
        version = string.Empty;
        if (!source.StartsWith("pack://", StringComparison.Ordinal)) return false;
        var segments = source[7..].Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2 || !SemanticVersion.TryParse(segments[1], out _)) return false;
        package = segments[0].ToLowerInvariant();
        version = segments[1];
        return true;
    }

    private static IReadOnlyList<string> NormalizeTokens(IReadOnlyList<string>? values, string parameter)
    {
        var result = (values ?? []).Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (result.Any(value => value.Length > 96 || !char.IsLetter(value[0]) ||
                                value.Any(character => !char.IsLetterOrDigit(character) && character is not '_' and not '.' and not '-')))
            throw new ArgumentException("Blueprint parameter names must be stable identifiers.", parameter);
        return result;
    }

    private static string NormalizePathOrPackUri(string value, string parameter)
    {
        var normalized = (value ?? string.Empty).Trim().Replace('\\', '/');
        if (normalized.StartsWith("pack://", StringComparison.Ordinal))
        {
            var tail = normalized[7..];
            if (tail.Length == 0 || tail.Split('/').Any(segment => segment is "" or "." or ".."))
                throw new ArgumentException("Pack URI must be canonical and traversal-free.", parameter);
            return normalized;
        }
        return NormalizeRelativePath(normalized, parameter);
    }

    private static string NormalizeRelativePath(string value, string parameter)
    {
        var normalized = (value ?? string.Empty).Trim().Replace('\\', '/').TrimStart('/');
        if (normalized.Length == 0 || Path.IsPathRooted(value) ||
            normalized.Split('/').Any(segment => segment is "" or "." or ".."))
            throw new ArgumentException("Blueprint paths must be safe project-relative paths.", parameter);
        return normalized;
    }

    public bool Equals(BlueprintAuthoringMetadata? other) => other is not null &&
        SchemaVersion == other.SchemaVersion &&
        string.Equals(Version, other.Version, StringComparison.Ordinal) &&
        string.Equals(PackageNamespace, other.PackageNamespace, StringComparison.Ordinal) &&
        string.Equals(ManifestPath, other.ManifestPath, StringComparison.Ordinal) &&
        ExposedParameters.SequenceEqual(other.ExposedParameters, StringComparer.Ordinal) &&
        Dependencies.SequenceEqual(other.Dependencies, StringComparer.Ordinal) &&
        string.Equals(PreviewPath, other.PreviewPath, StringComparison.Ordinal) &&
        PlatformByteBudget == other.PlatformByteBudget &&
        string.Equals(Kit, other.Kit, StringComparison.Ordinal) &&
        Styles.SequenceEqual(other.Styles, StringComparer.Ordinal) &&
        Compatibility.SequenceEqual(other.Compatibility, StringComparer.Ordinal) &&
        Tags.SequenceEqual(other.Tags, StringComparer.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as BlueprintAuthoringMetadata);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(SchemaVersion);
        hash.Add(Version, StringComparer.Ordinal);
        hash.Add(PackageNamespace, StringComparer.Ordinal);
        hash.Add(ManifestPath, StringComparer.Ordinal);
        foreach (var value in ExposedParameters) hash.Add(value, StringComparer.Ordinal);
        foreach (var value in Dependencies) hash.Add(value, StringComparer.Ordinal);
        hash.Add(PreviewPath, StringComparer.Ordinal);
        hash.Add(PlatformByteBudget);
        hash.Add(Kit, StringComparer.Ordinal);
        foreach (var value in Styles) hash.Add(value, StringComparer.Ordinal);
        foreach (var value in Compatibility) hash.Add(value, StringComparer.Ordinal);
        foreach (var value in Tags) hash.Add(value, StringComparer.Ordinal);
        return hash.ToHashCode();
    }

    private static string NormalizeNamespace(string value) => string.Join('.',
        value.Split(['-', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(segment => char.IsDigit(segment[0]) ? $"n{segment}" : segment));

    private static bool ValidNamespace(string value) => value.Length is > 0 and <= 160 &&
        value.Split('.').All(segment => segment.Length > 0 && char.IsLetter(segment[0]) &&
            segment.All(character => char.IsLower(character) || char.IsDigit(character) || character == '_'));
}

public static class BlueprintVersionConstraint
{
    public static bool IsValid(string? constraint) => constraint?.Trim() == "*" || SemanticVersionConstraint.IsValid(constraint);

    public static bool Matches(string constraint, string version) =>
        constraint.Trim() == "*" || SemanticVersionConstraint.Matches(constraint, version);
}
