using System.Text.RegularExpressions;

namespace Forge.ModuleFormat.Packages;

public static partial class ModuleManifestValidator
{
    public const string InvalidFormatCode = "module.manifest.format_invalid";
    public const string InvalidVersionCode = "module.manifest.version_invalid";
    public const string InvalidSlugCode = "module.manifest.slug_invalid";
    public const string InvalidHashCode = "module.manifest.hash_invalid";
    public const string DuplicateDependencyCode = "module.manifest.dependency_duplicate";

    public static IReadOnlyList<ModulePackageDiagnostic> Validate(ModuleManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var diagnostics = new List<ModulePackageDiagnostic>();
        if (!string.Equals(
                manifest.FormatVersion,
                ModuleManifest.CurrentFormatVersion,
                StringComparison.Ordinal))
        {
            diagnostics.Add(new ModulePackageDiagnostic(
                InvalidFormatCode,
                $"Unsupported module format '{manifest.FormatVersion}'.",
                "manifest.json/formatVersion"));
        }

        foreach (var (value, path) in new[]
                 {
                     (manifest.ModuleVersion, "manifest.json/moduleVersion"),
                     (manifest.RuntimeMinimumVersion, "manifest.json/runtimeMinimumVersion"),
                     (manifest.ForgeScriptApiVersion, "manifest.json/forgeScriptApiVersion"),
                 })
        {
            if (!SemanticVersionRegex().IsMatch(value ?? string.Empty))
            {
                diagnostics.Add(new ModulePackageDiagnostic(
                    InvalidVersionCode,
                    $"'{value}' is not a semantic version.",
                    path));
            }
        }

        if (!SlugRegex().IsMatch(manifest.Slug ?? string.Empty))
        {
            diagnostics.Add(new ModulePackageDiagnostic(
                InvalidSlugCode,
                $"Module slug '{manifest.Slug}' is invalid.",
                "manifest.json/slug"));
        }

        if (!Sha256Regex().IsMatch(manifest.ContentSha256 ?? string.Empty))
        {
            diagnostics.Add(new ModulePackageDiagnostic(
                InvalidHashCode,
                "Content hash must be a lowercase SHA-256 value.",
                "manifest.json/contentSha256"));
        }

        if (manifest.Dependencies.Distinct(StringComparer.Ordinal).Count() != manifest.Dependencies.Count)
        {
            diagnostics.Add(new ModulePackageDiagnostic(
                DuplicateDependencyCode,
                "Module dependencies must be unique.",
                "manifest.json/dependencies"));
        }

        return diagnostics;
    }

    [GeneratedRegex("^[0-9]+\\.[0-9]+\\.[0-9]+(?:-[0-9A-Za-z.-]+)?$")]
    private static partial Regex SemanticVersionRegex();

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Sha256Regex();
}
