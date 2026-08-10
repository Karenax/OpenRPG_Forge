using Forge.Application.Commands;
using Forge.Domain.Modules;

namespace Forge.Mcp.Projects;

public sealed record ProjectVariableResource(int SchemaVersion, long Revision, string Query, string? Scope,
    IReadOnlyList<ProjectVariableDefinition> Variables);

public sealed record ProjectVariablePlanRequest(int SchemaVersion, long BaseRevision,
    IReadOnlyList<ProjectVariableDefinition> Variables);

public sealed record ProjectVariablePlanResult(bool Succeeded, string? PlanId, long BaseRevision,
    ProjectPatch? Diff, ProjectPatch? Inverse, IReadOnlyList<AgentDiagnostic> Diagnostics, bool Applied,
    bool Reverted, IReadOnlyList<ProjectVariableAuditEntry> Audit);

public sealed record ProjectVariableAuditEntry(string PlanId, string Action, long Revision,
    DateTimeOffset OccurredAt, bool Succeeded);

public sealed record RulesetPluginResource(int SchemaVersion, long Revision, string Query,
    string ActivePackageKey, IReadOnlyList<RulesetPluginItem> Packages);

public sealed record RulesetPluginItem(Guid Id, int SchemaVersion, string Key, string Version,
    string ContentPackageId, int AttributeCount, int EffectCount, int AbilityCount, int InventoryCount,
    int ProgressionCount, int UiPanelCount, string Provenance);
