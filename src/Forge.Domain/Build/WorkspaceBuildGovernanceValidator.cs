using System.Text.RegularExpressions;
using Forge.Domain.Validation;

namespace Forge.Domain.Build;

public static partial class WorkspaceBuildGovernanceValidator
{
    public const string SchemaUnsupportedCode = "build.governance.schema_unsupported";
    public const string IdentityInvalidCode = "build.governance.identity_invalid";
    public const string WorktreeInvalidCode = "build.governance.worktree_invalid";
    public const string CiInvalidCode = "build.governance.ci_invalid";
    public const string FixturesInvalidCode = "build.governance.fixtures_invalid";
    public const string VersioningInvalidCode = "build.governance.versioning_invalid";
    public const string AdrInvalidCode = "build.governance.adr_invalid";

    private static readonly string[] RequiredChecks = ["tracker", "backlog", "graph", "verify"];
    private static readonly string[] RequiredCompatibility =
        ["core-major-minor", "kits-require-core", "packages-major", "schemas-migrate-explicitly"];
    private static readonly string[] RequiredStatuses = ["accepted", "proposed", "rejected", "superseded"];

    public static IReadOnlyList<DomainDiagnostic> Validate(WorkspaceBuildGovernanceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.SchemaVersion != WorkspaceBuildGovernanceProfile.CurrentSchemaVersion)
            Add(diagnostics, SchemaUnsupportedCode, "Build governance schema is unsupported.", "build/governance/schemaVersion");
        if (profile.Id == Guid.Empty || string.IsNullOrWhiteSpace(profile.Provenance))
            Add(diagnostics, IdentityInvalidCode, "Build governance requires a UUID and provenance.", "build/governance/id");

        var worktrees = profile.Worktrees;
        if (!worktrees.RequireDedicatedBranchOrWorktree || !worktrees.RequireExclusiveOwnership ||
            !worktrees.RequireDeclaredFileScopes || string.IsNullOrWhiteSpace(worktrees.BranchPrefix) ||
            worktrees.BranchPrefix.Any(char.IsWhiteSpace) || !IsSafePath(worktrees.OwnershipManifestPath) ||
            !Exact(worktrees.IntegrationChecks, RequiredChecks))
            Add(diagnostics, WorktreeInvalidCode,
                "Worktree policy requires exclusive ownership, declared file scopes and tracker/backlog/graph/verify integration checks.",
                "build/governance/worktrees");

        var ci = profile.ContinuousIntegration;
        if (!IsSafePath(ci.WorkflowPath) || !ci.ValidateSchemas || !ci.BuildManagedCode || !ci.BuildNativeCode ||
            !ci.CompileForgeScript || !ci.RunTests || !ci.PackReferenceModule || !ci.VerifyLicenses)
            Add(diagnostics, CiInvalidCode,
                "CI must validate schemas, build managed/native/ForgeScript code, run tests, pack a reference module and verify licenses.",
                "build/governance/ci");

        var fixtures = profile.ReferenceFixtures;
        if (!IsSafePath(fixtures.CatalogPath) || fixtures.Fixtures.Count != 5 ||
            fixtures.Fixtures.Select(item => item.Id).Distinct().Count() != fixtures.Fixtures.Count ||
            fixtures.Fixtures.Select(item => item.Kind).Distinct().Count() != Enum.GetValues<ReferenceFixtureKind>().Length ||
            fixtures.Fixtures.Any(item => item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.Name) ||
                !IsSafePath(item.RelativePath) || item.ExpectedOutcome is not ("valid" or "rejected" or "migrated") ||
                string.IsNullOrWhiteSpace(item.Provenance)))
            Add(diagnostics, FixturesInvalidCode,
                "Reference fixtures require unique tiny, vertical, stress, malicious and migration entries with safe versioned paths.",
                "build/governance/fixtures");

        var versions = profile.Versioning;
        if (!IsSemVer(versions.CoreVersion) || !IsSemVer(versions.PackageVersion) ||
            versions.KitVersions.Count < 3 || versions.KitVersions.Values.Any(value => !IsSemVer(value)) ||
            versions.SchemaVersions.Count < 3 || versions.SchemaVersions.Values.Any(value => !IsSemVer(value)) ||
            !Exact(versions.CompatibilityRules, RequiredCompatibility) || !IsSafePath(versions.ChangelogPath))
            Add(diagnostics, VersioningInvalidCode,
                "Semantic versioning requires Core, Kits and packages, separate schema versions, compatibility rules and a changelog.",
                "build/governance/versioning");

        var adr = profile.ArchitectureDecisions;
        if (!IsSafePath(adr.DirectoryPath) || !IsSafePath(adr.TemplatePath) ||
            !Exact(adr.AllowedStatuses, RequiredStatuses) || !adr.RequireTestLinks ||
            !adr.RequireBenchmarksForPerformanceDecisions || !adr.ReplaceBySupersedingDecision)
            Add(diagnostics, AdrInvalidCode,
                "ADR governance requires a template, formal statuses, test/benchmark evidence and superseding updates.",
                "build/governance/adrs");

        return diagnostics;
    }

    private static bool Exact(IEnumerable<string> actual, IEnumerable<string> expected) =>
        actual.Select(item => item.Trim().ToLowerInvariant()).Order(StringComparer.Ordinal)
            .SequenceEqual(expected.Order(StringComparer.Ordinal), StringComparer.Ordinal);

    private static bool IsSafePath(string? path) =>
        !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) &&
        path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)
            .All(segment => segment is not "." and not "..");

    private static bool IsSemVer(string? value) => SemanticVersionRegex().IsMatch(value ?? string.Empty);

    private static void Add(ICollection<DomainDiagnostic> diagnostics, string code, string message, string path) =>
        diagnostics.Add(new(code, message, path));

    [GeneratedRegex(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$")]
    private static partial Regex SemanticVersionRegex();
}
