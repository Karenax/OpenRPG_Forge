using Forge.Domain.RuntimePolicies;

namespace Forge.Domain.RuntimeExtensions;

public sealed record ClickableDiagnosticPolicy(
    Guid Id,
    int SchemaVersion,
    string ActiveProfile,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Severities,
    bool EnableQuickFixes,
    string Provenance)
{
    public const int CurrentSchemaVersion = 1;

    public static IReadOnlySet<string> SupportedProfiles { get; } =
        new HashSet<string>(["authoring", "build", "runtime", "all"], StringComparer.Ordinal);

    public static IReadOnlySet<string> SupportedCategories { get; } =
        new HashSet<string>(["schema", "reference", "graph", "geometry", "navigation", "license", "runtime"],
            StringComparer.Ordinal);

    public static IReadOnlySet<string> SupportedSeverities { get; } =
        new HashSet<string>(["info", "warning", "error"], StringComparer.Ordinal);

    public static ClickableDiagnosticPolicy CreateDefault(Guid ownerId) => new(
        RuntimePolicyIdFactory.Create(ownerId, "quality-assurance:clickable-diagnostics"),
        CurrentSchemaVersion,
        "authoring",
        SupportedCategories.Order(StringComparer.Ordinal).ToArray(),
        SupportedSeverities.Order(StringComparer.Ordinal).ToArray(),
        true,
        "migration-default");
}

public sealed record MaliciousModuleFixturePolicy(
    Guid Id,
    int SchemaVersion,
    string CatalogPath,
    IReadOnlyList<string> Families,
    int MaximumArrayEntries,
    bool RejectInjectionText,
    bool RejectInvalidAssets,
    string Provenance)
{
    public const int CurrentSchemaVersion = 1;

    public static IReadOnlySet<string> RequiredFamilies { get; } = new HashSet<string>(
        ["path-traversal", "zip-bomb", "bad-reference", "infinite-script", "huge-array", "injection-text", "invalid-asset"],
        StringComparer.Ordinal);

    public static MaliciousModuleFixturePolicy CreateDefault(Guid ownerId) => new(
        RuntimePolicyIdFactory.Create(ownerId, "quality-assurance:malicious-fixtures"),
        CurrentSchemaVersion,
        "tests/fixtures/qa/malicious-modules.json",
        RequiredFamilies.Order(StringComparer.Ordinal).ToArray(),
        65_536,
        true,
        true,
        "migration-default");
}

public sealed record AuroraParityCoveragePolicy(
    Guid Id,
    int SchemaVersion,
    string TrackerPath,
    IReadOnlyList<string> AllowedStatuses,
    int ExpectedFeatureCount,
    bool RequireEvidenceForTested,
    string Provenance)
{
    public const int CurrentSchemaVersion = 1;

    public static IReadOnlySet<string> SupportedStatuses { get; } = new HashSet<string>(
        ["NotStarted", "Designed", "Implemented", "Tested", "Deferred", "Rejected", "Researched"],
        StringComparer.Ordinal);

    public static AuroraParityCoveragePolicy CreateDefault(Guid ownerId) => new(
        RuntimePolicyIdFactory.Create(ownerId, "quality-assurance:aurora-parity"),
        CurrentSchemaVersion,
        "tracking/project/AURORA_PARITY_COVERAGE.csv",
        SupportedStatuses.Order(StringComparer.Ordinal).ToArray(),
        717,
        true,
        "migration-default");
}

public sealed record AcceptanceGatePolicy(
    Guid Id,
    int SchemaVersion,
    string GateMatrixPath,
    IReadOnlyList<string> RequiredGates,
    bool RequireLinkedTests,
    bool RequireLinkedDocumentation,
    bool RequireFeatureEvidence,
    string Provenance)
{
    public const int CurrentSchemaVersion = 1;

    public static IReadOnlyList<string> DefaultGates { get; } =
        Enumerable.Range(0, 14).Select(index => $"G{index}").ToArray();

    public static AcceptanceGatePolicy CreateDefault(Guid ownerId) => new(
        RuntimePolicyIdFactory.Create(ownerId, "quality-assurance:acceptance-gates"),
        CurrentSchemaVersion,
        "tracking/project/AURORA_GATE_MATRIX.csv",
        DefaultGates,
        true,
        true,
        true,
        "migration-default");
}
