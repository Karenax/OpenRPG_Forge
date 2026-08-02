using Forge.Application.Commands;

namespace Forge.Mcp.Scripts;

public sealed record ScriptBindingPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string AreaId,
    string EntityId,
    string Binding,
    string RelativeScriptPath);

public sealed record ScriptBindingPlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics);
