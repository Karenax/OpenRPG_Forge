using Forge.Domain.Build;
using Forge.ModuleFormat.Build;

namespace Forge.Mcp.Build;

public sealed record BuildProfileResource(
    int SchemaVersion,
    string Id,
    string Provenance,
    bool FileMissing,
    IReadOnlyDictionary<string, string> Layout,
    IReadOnlyList<string> CliOperations,
    bool Deterministic,
    bool Incremental,
    BuildGovernanceResource Governance,
    BuildCollaborationResource Collaboration,
    ProjectCompatibilityResource Compatibility,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record ProjectCompatibilityResource(
    bool Found,
    string Id,
    int SchemaVersion,
    string Provenance,
    string MinimumRuntimeVersion,
    string ProjectFormatVersion,
    bool AutomaticMigrations,
    bool RequiredAtRuntime,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record ProjectCompatibilityPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string MinimumRuntimeVersion,
    string ProjectFormatVersion,
    bool AutomaticMigrations,
    bool RequiredAtRuntime);

public sealed record ProjectCompatibilityPlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    long? NewRevision,
    bool Applied,
    bool Reverted,
    ProjectCompatibilityResource? Compatibility,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record BuildCollaborationResource(
    string Id,
    int SchemaVersion,
    string Provenance,
    string CacheIndex,
    int CacheMaximumEntries,
    int NormalizationMaximumBytes,
    string MigrationBackupDirectory,
    int MigrationMaximumBackups,
    int ReferenceMaximumNodes,
    int SemanticDiffMaximumEntries,
    IReadOnlyList<string> Guarantees);

public sealed record BuildGovernanceResource(
    string Id,
    int SchemaVersion,
    string BranchPrefix,
    string CiWorkflow,
    IReadOnlyList<string> FixtureKinds,
    string CoreVersion,
    IReadOnlyDictionary<string, string> KitVersions,
    string PackageVersion,
    string ChangelogPath,
    string AdrTemplate,
    IReadOnlyList<string> AdrStatuses);

public sealed record BuildPlanResource(
    int SchemaVersion,
    string GraphId,
    IReadOnlyList<string> Changed,
    IReadOnlyList<string> Removed,
    IReadOnlyList<string> Rebuilt,
    IReadOnlyList<string> Reused,
    IReadOnlyList<BuildPlanNode> Resources);

public sealed record BuildPlanNode(
    string Id,
    string Path,
    BuildResourceKind Kind,
    string Sha256,
    IReadOnlyList<string> Dependencies);

public sealed record BuildValidationResource(
    bool Valid,
    string ProjectId,
    string ProfileId,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record BuildExecutionResource(
    bool Succeeded,
    string Output,
    string ContentSha256,
    bool Reproducible,
    IReadOnlyList<string> Rebuilt,
    IReadOnlyList<string> Reused,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record BuildCacheResource(
    bool Succeeded,
    bool DryRun,
    string CacheDirectory,
    string IndexPath,
    IReadOnlyList<DisposableCacheEntry> Entries,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record BuildNormalizationResource(
    bool Succeeded,
    bool DryRun,
    IReadOnlyList<NormalizedDocumentChange> Changes,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record BuildMigrationResource(
    bool Succeeded,
    bool DryRun,
    string FromVersion,
    string ToVersion,
    bool Applied,
    bool Idempotent,
    bool DowngradeSupported,
    string? BackupDirectory,
    IReadOnlyList<string> ChangedDocuments,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record BuildReferenceIndexResource(
    string GraphId,
    IReadOnlyList<WorkspaceReferenceNode> Nodes,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record BuildSemanticDiffResource(
    bool Succeeded,
    string Text,
    IReadOnlyList<WorkspaceSemanticChange> Changes,
    IReadOnlyList<AgentDiagnostic> Diagnostics);
