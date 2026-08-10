using Forge.Domain.Validation;

namespace Forge.Domain.Build;

public static class WorkspaceBuildProfileValidator
{
    public const string SchemaUnsupportedCode = "build.profile.schema_unsupported";
    public const string IdentityInvalidCode = "build.profile.identity_invalid";
    public const string ProvenanceMissingCode = "build.profile.provenance_missing";
    public const string RepositoryPolicyInvalidCode = "build.profile.repository_policy_invalid";
    public const string LayoutInvalidCode = "build.profile.layout_invalid";
    public const string CliContractInvalidCode = "build.profile.cli_contract_invalid";
    public const string DeterminismInvalidCode = "build.profile.determinism_invalid";
    public const string IncrementalInvalidCode = "build.profile.incremental_invalid";

    private static readonly string[] RequiredLayoutKeys =
        ["assets", "cache", "data", "docs", "localization", "packages", "scripts", "tests"];

    public static IReadOnlyList<DomainDiagnostic> Validate(WorkspaceBuildProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.SchemaVersion != WorkspaceBuildProfile.CurrentSchemaVersion)
            diagnostics.Add(new(SchemaUnsupportedCode, "Workspace build profile schema is unsupported.", "build/schemaVersion"));
        if (profile.Id == Guid.Empty)
            diagnostics.Add(new(IdentityInvalidCode, "Workspace build profile UUID is required.", "build/id"));
        if (string.IsNullOrWhiteSpace(profile.Provenance))
            diagnostics.Add(new(ProvenanceMissingCode, "Workspace build profile provenance is required.", "build/provenance"));
        if (!profile.RequireGitRepository || !profile.TextDocuments ||
            !profile.ExplicitGeneratedFiles || !profile.ReproducibleAssets)
            diagnostics.Add(new(RepositoryPolicyInvalidCode,
                "Canonical repositories require Git, text documents, explicit generated files and reproducible assets.",
                "build/repository"));

        var paths = profile.Layout.Values.Append(profile.CachePath).ToArray();
        if (RequiredLayoutKeys.Any(key => !profile.Layout.ContainsKey(key)) ||
            profile.Layout.Keys.Except(RequiredLayoutKeys, StringComparer.Ordinal).Any() ||
            paths.Any(path => !IsSafeRelativePath(path)) ||
            paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Length - 1 ||
            !profile.Layout.TryGetValue("cache", out var cache) ||
            !string.Equals(cache, profile.CachePath, StringComparison.Ordinal))
            diagnostics.Add(new(LayoutInvalidCode,
                "Build layout requires unique safe relative paths for data, scripts, assets, localization, tests, packages, docs and cache.",
                "build/layout"));

        if (profile.CliOperations.Count != WorkspaceBuildProfile.RequiredCliOperations.Count ||
            WorkspaceBuildProfile.RequiredCliOperations.Except(profile.CliOperations, StringComparer.Ordinal).Any() ||
            profile.CliOperations.Distinct(StringComparer.Ordinal).Count() != profile.CliOperations.Count)
            diagnostics.Add(new(CliContractInvalidCode,
                "Build CLI must expose build, validate, test, pack, migrate, normalize, diff and references.",
                "build/cliOperations"));
        if (!profile.StableOrdering || !profile.NormalizedTimestamps || !profile.LockedDependencies ||
            !profile.ContentHashes || !profile.VerifyReproducibility)
            diagnostics.Add(new(DeterminismInvalidCode,
                "Deterministic builds require stable ordering, normalized timestamps, locked dependencies, hashes and reproducibility verification.",
                "build/determinism"));
        if (!profile.IncrementalBuild || !profile.RebuildAffectedOnly)
            diagnostics.Add(new(IncrementalInvalidCode,
                "Incremental builds must be enabled and rebuild affected resources only.",
                "build/incremental"));
        diagnostics.AddRange(WorkspaceBuildGovernanceValidator.Validate(profile.Governance));
        diagnostics.AddRange(WorkspaceBuildCollaborationProfileValidator.Validate(profile.Collaboration));
        diagnostics.AddRange(ProjectCompatibilityProfileValidator.Validate(profile.Compatibility));
        return diagnostics;
    }

    private static bool IsSafeRelativePath(string path) =>
        !string.IsNullOrWhiteSpace(path) &&
        !Path.IsPathRooted(path) &&
        path.Split('/', StringSplitOptions.RemoveEmptyEntries).All(segment => segment is not "." and not "..");
}
