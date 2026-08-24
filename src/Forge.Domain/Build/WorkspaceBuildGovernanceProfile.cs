namespace Forge.Domain.Build;

public enum ReferenceFixtureKind
{
    Tiny,
    VerticalSlice,
    Stress,
    Malicious,
    Migration,
}

public sealed record ReferenceFixtureDefinition(
    Guid Id,
    string Name,
    ReferenceFixtureKind Kind,
    string RelativePath,
    string ExpectedOutcome,
    string Provenance);

public sealed record WorktreeAgentPolicy(
    bool RequireDedicatedBranchOrWorktree,
    bool RequireExclusiveOwnership,
    bool RequireDeclaredFileScopes,
    string BranchPrefix,
    string OwnershipManifestPath,
    IReadOnlyList<string> IntegrationChecks);

public sealed record ContinuousIntegrationPolicy(
    string WorkflowPath,
    bool ValidateSchemas,
    bool BuildManagedCode,
    bool BuildNativeCode,
    bool CompileForgeScript,
    bool RunTests,
    bool PackReferenceModule,
    bool VerifyLicenses);

public sealed record ReferenceFixtureCatalogPolicy(
    string CatalogPath,
    IReadOnlyList<ReferenceFixtureDefinition> Fixtures);

public sealed record SemanticVersioningPolicy(
    string CoreVersion,
    IReadOnlyDictionary<string, string> KitVersions,
    string PackageVersion,
    IReadOnlyDictionary<string, string> SchemaVersions,
    IReadOnlyList<string> CompatibilityRules,
    string ChangelogPath);

public sealed record ArchitectureDecisionPolicy(
    string DirectoryPath,
    string TemplatePath,
    IReadOnlyList<string> AllowedStatuses,
    bool RequireTestLinks,
    bool RequireBenchmarksForPerformanceDecisions,
    bool ReplaceBySupersedingDecision);

public sealed record WorkspaceBuildGovernanceProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    WorktreeAgentPolicy Worktrees,
    ContinuousIntegrationPolicy ContinuousIntegration,
    ReferenceFixtureCatalogPolicy ReferenceFixtures,
    SemanticVersioningPolicy Versioning,
    ArchitectureDecisionPolicy ArchitectureDecisions)
{
    public const int CurrentSchemaVersion = 1;
    public static readonly Guid DefaultId = Guid.Parse("b0020000-0000-4000-8000-000000000001");

    public static WorkspaceBuildGovernanceProfile CreateDefault() => new(
        DefaultId,
        CurrentSchemaVersion,
        "openrpg-forge:p0-r002",
        new(true, true, true, "codex/", ".forge/task-ownership.json",
            ["tracker", "backlog", "graph", "verify"]),
        new(".github/workflows/ci.yml", true, true, true, true, true, true, true),
        new("tests/fixtures/build/reference-fixtures.json",
        [
            Fixture("b0020000-0000-4000-8000-000000000011", "tiny", ReferenceFixtureKind.Tiny,
                "apps/Forge.Runtime/content/crypt-of-ashes/project.json", "valid"),
            Fixture("b0020000-0000-4000-8000-000000000012", "vertical-slice", ReferenceFixtureKind.VerticalSlice,
                "apps/Forge.Runtime/content/crypt-of-ashes/project.json", "valid"),
            Fixture("b0020000-0000-4000-8000-000000000013", "stress", ReferenceFixtureKind.Stress,
                "apps/Forge.Runtime/content/crypt-of-ashes/project.json", "valid"),
            Fixture("b0020000-0000-4000-8000-000000000014", "malicious", ReferenceFixtureKind.Malicious,
                "tests/Forge.ModuleFormat.Tests/Packages/ModulePackageTests.cs", "rejected"),
            Fixture("b0020000-0000-4000-8000-000000000015", "migration", ReferenceFixtureKind.Migration,
                "tests/Forge.ModuleFormat.Tests/Serialization/ProjectJsonSerializerTests.cs", "migrated"),
        ]),
        new("0.6.34",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["live-director"] = "0.6.34",
                ["network"] = "0.6.34",
                ["rpg"] = "0.6.34",
            },
            "1.2.0",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["module-package"] = "1.2.0",
                ["project"] = "0.6.34",
                ["workspace"] = "1.30.0",
            },
            ["core-major-minor", "kits-require-core", "packages-major", "schemas-migrate-explicitly"],
            "CHANGELOG.md"),
        new("docs/ADR", "docs/ADR/ADR-TEMPLATE.md",
            ["proposed", "accepted", "superseded", "rejected"], true, true, true));

    private static ReferenceFixtureDefinition Fixture(
        string id,
        string name,
        ReferenceFixtureKind kind,
        string path,
        string outcome) =>
        new(Guid.Parse(id), name, kind, path, outcome, "openrpg-forge:p0-r002");
}
