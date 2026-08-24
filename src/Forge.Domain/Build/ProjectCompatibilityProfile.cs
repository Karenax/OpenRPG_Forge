using System.Text.RegularExpressions;
using Forge.Domain.Validation;

namespace Forge.Domain.Build;

public sealed record ProjectCompatibilityProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string MinimumRuntimeVersion,
    string ProjectFormatVersion,
    bool AutomaticMigrations,
    bool RequiredAtRuntime)
{
    public const int CurrentSchemaVersion = 1;
    public static readonly Guid DefaultId = Guid.Parse("b0260000-0000-4000-8000-000000000025");

    public static ProjectCompatibilityProfile CreateDefault() => new(
        DefaultId,
        CurrentSchemaVersion,
        "openrpg-forge:p0-r026",
        "0.1.0",
        "1.30.0",
        true,
        true);
}

public static partial class ProjectCompatibilityProfileValidator
{
    public const string SchemaUnsupportedCode = "build.compatibility.schema_unsupported";
    public const string IdentityInvalidCode = "build.compatibility.identity_invalid";
    public const string VersionInvalidCode = "build.compatibility.version_invalid";
    public const string AutomaticMigrationRequiredCode = "build.compatibility.automatic_migration_required";

    public static IReadOnlyList<DomainDiagnostic> Validate(ProjectCompatibilityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.SchemaVersion != ProjectCompatibilityProfile.CurrentSchemaVersion)
            diagnostics.Add(new(SchemaUnsupportedCode, "Project compatibility schema is unsupported.",
                "build/compatibility/schemaVersion"));
        if (profile.Id == Guid.Empty || string.IsNullOrWhiteSpace(profile.Provenance))
            diagnostics.Add(new(IdentityInvalidCode, "Project compatibility requires a UUID and provenance.",
                "build/compatibility/id"));
        if (!SemanticVersionRegex().IsMatch(profile.MinimumRuntimeVersion ?? string.Empty))
            diagnostics.Add(new(VersionInvalidCode, "Minimum runtime version must be a semantic version.",
                "build/compatibility/minimumRuntimeVersion"));
        if (!SemanticVersionRegex().IsMatch(profile.ProjectFormatVersion ?? string.Empty))
            diagnostics.Add(new(VersionInvalidCode, "Project format version must be a semantic version.",
                "build/compatibility/projectFormatVersion"));
        if (!profile.AutomaticMigrations)
            diagnostics.Add(new(AutomaticMigrationRequiredCode,
                "Supported project schemas require automatic versioned migrations.",
                "build/compatibility/automaticMigrations"));
        return diagnostics;
    }

    [GeneratedRegex(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$")]
    private static partial Regex SemanticVersionRegex();
}
