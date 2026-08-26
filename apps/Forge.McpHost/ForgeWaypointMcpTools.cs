using System.ComponentModel;
using Forge.Mcp.Navigation;
using Forge.Mcp.Projects;
using ModelContextProtocol.Server;

namespace Forge.McpHost;

[McpServerToolType]
public sealed class ForgeWaypointMcpTools
{
    private readonly ForgeProjectAgentService _project;

    public ForgeWaypointMcpTools(ForgeProjectAgentService project) =>
        _project = project ?? throw new ArgumentNullException(nameof(project));

    [McpServerTool]
    [Description("Plans waypoint authoring JSON schema v1 through the canonical Studio/Application navigation command, returning revision, diff, inverse and audit without writing.")]
    public WaypointAuthoringPlanResult PlanWaypointAuthoring(WaypointAuthoringPlanRequest request) =>
        _project.PlanWaypointAuthoring(request);

    [McpServerTool]
    [Description("Applies a validated waypoint authoring plan schema v1 at its base revision and persists the canonical project.")]
    public WaypointAuthoringPlanResult ApplyWaypointAuthoringPlan(string planId) =>
        _project.ApplyWaypointAuthoringPlan(planId);

    [McpServerTool]
    [Description("Reverts the latest applied waypoint authoring plan schema v1 through canonical project history and persistence.")]
    public WaypointAuthoringPlanResult RevertWaypointAuthoringPlan(string planId) =>
        _project.RevertWaypointAuthoringPlan(planId);

    [McpServerTool]
    [Description("Redoes the latest reverted waypoint authoring plan schema v1 through canonical project history and persistence.")]
    public WaypointAuthoringPlanResult RedoWaypointAuthoringPlan(string planId) =>
        _project.RedoWaypointAuthoringPlan(planId);
}
