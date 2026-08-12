using Forge.Domain.Assistant;
using Forge.Domain.Projects;
using Forge.RpgKit.Application.Combat;

namespace Forge.Mcp.Assistant;

public sealed record CombatSimulationAgentRequest(
    Guid PlanId,
    Guid TransactionId,
    AssistantPermissionLevel Permission,
    bool DryRun,
    CombatSimulationProfile Profile,
    IReadOnlyDictionary<string, float>? PathingDistancesMeters = null);

public sealed record CombatSimulationAgentResult(
    bool Succeeded,
    bool Applied,
    string Code,
    Guid PlanId,
    Guid TransactionId,
    CombatSimulationReport? Report,
    string SemanticDiff);

public static class CombatSimulationAgentService
{
    public static CombatSimulationAgentResult Run(ProjectDefinition project, CombatSimulationAgentRequest request)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(request);
        if (request.PlanId == Guid.Empty || request.TransactionId == Guid.Empty)
            return Failure(request, "MCP_COMBAT_SIMULATION_PLAN_REQUIRED");
        if (request.Permission < AssistantPermissionLevel.Propose)
            return Failure(request, "MCP_COMBAT_SIMULATION_PERMISSION_DENIED");
        var archetypes = project.Blueprints.Where(item => item.Combat is not null)
            .Select(item => new CombatSimulationArchetype(item.Id, item.DisplayName, item.Combat!)).ToArray();
        var report = CombatBalanceSimulationService.Run(request.Profile, archetypes,
            request.PathingDistancesMeters);
        return new(report.Succeeded, false,
            report.Succeeded ? request.DryRun ? "MCP_COMBAT_SIMULATION_PREVIEW_READY" : "MCP_COMBAT_SIMULATION_READY"
                : report.Diagnostics[0].Code,
            request.PlanId, request.TransactionId, report,
            $"read-only combat simulation: {archetypes.Length} archetypes, {request.Profile.RunsPerMatchup} seeds, 0 project mutations");
    }

    private static CombatSimulationAgentResult Failure(CombatSimulationAgentRequest request, string code) =>
        new(false, false, code, request.PlanId, request.TransactionId, null, "no changes");
}
