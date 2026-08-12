using Forge.Application.Blueprints;
using Forge.Application.Commands;

namespace Forge.Mcp.Projects;

public sealed record BlueprintPaletteResource(
    int SchemaVersion,
    long Revision,
    string Query,
    IReadOnlyList<BlueprintPaletteResourceItem> Items);

public sealed record BlueprintPaletteResourceItem(
    string BlueprintId,
    string Slug,
    string DisplayName,
    string Version,
    string ManifestPath,
    string LicenseSpdx,
    string ContentHash,
    string Provenance,
    IReadOnlyList<string> CategoryPaths,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> PromotedDefaultKeys,
    IReadOnlyList<BlueprintInstanceMigrationImpact> Instances);

public sealed record BlueprintUpdatePlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string BlueprintId,
    string SourceEntityId,
    IReadOnlyList<string> PromotedOverrideKeys,
    string VersionBump,
    IReadOnlyList<string> ExcludedInstanceIds,
    IReadOnlyList<string>? CategoryPaths = null);

public sealed record BlueprintUpdatePlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    string? PreviousVersion,
    string? TargetVersion,
    IReadOnlyList<BlueprintInstanceMigrationImpact> Instances,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<BlueprintUpdateAuditEntry> Audit);

public sealed record BlueprintUpdateAuditEntry(
    string PlanId,
    string Action,
    long Revision,
    DateTimeOffset OccurredAt,
    bool Succeeded);
