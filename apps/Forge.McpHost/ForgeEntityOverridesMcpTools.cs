using System.ComponentModel;
using Forge.Mcp.Projects;
using ModelContextProtocol.Server;

namespace Forge.McpHost;

[McpServerToolType]
public sealed class ForgeEntityOverridesMcpTools
{
    private readonly ForgeProjectAgentService _project;

    public ForgeEntityOverridesMcpTools(ForgeProjectAgentService project) => _project = project;

    [McpServerTool]
    [Description("Validates and dry-runs replacement of one existing entity's complete local override set.")]
    public EntityOverridesPlanResult PlanEntityOverrides(EntityOverridesPlanRequest request) =>
        _project.PlanEntityOverrides(request);

    [McpServerTool]
    [Description("Applies a previously validated existing-entity override plan at its base revision.")]
    public EntityOverridesPlanResult ApplyEntityOverridesPlan(string planId) =>
        _project.ApplyEntityOverridesPlan(planId);

    [McpServerTool]
    [Description("Reverts the most recently applied existing-entity override plan when no later mutation exists.")]
    public EntityOverridesPlanResult RevertEntityOverridesPlan(string planId) =>
        _project.RevertEntityOverridesPlan(planId);
}
