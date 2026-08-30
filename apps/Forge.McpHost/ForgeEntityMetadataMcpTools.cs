using System.ComponentModel;
using Forge.Mcp.Projects;
using ModelContextProtocol.Server;

namespace Forge.McpHost;

[McpServerToolType]
public sealed class ForgeEntityMetadataMcpTools
{
    private readonly ForgeProjectAgentService _project;

    public ForgeEntityMetadataMcpTools(ForgeProjectAgentService project) => _project = project;

    [McpServerTool]
    [Description("Validates and dry-runs replacement of one existing entity's complete canonical transform.")]
    public EntityTransformPlanResult PlanEntityTransform(EntityTransformPlanRequest request) =>
        _project.PlanEntityTransform(request);

    [McpServerTool]
    [Description("Applies a previously validated existing-entity transform plan at its base revision.")]
    public EntityTransformPlanResult ApplyEntityTransformPlan(string planId) =>
        _project.ApplyEntityTransformPlan(planId);

    [McpServerTool]
    [Description("Reverts the most recently applied existing-entity transform plan when no later mutation exists.")]
    public EntityTransformPlanResult RevertEntityTransformPlan(string planId) =>
        _project.RevertEntityTransformPlan(planId);

    [McpServerTool]
    [Description("Validates and dry-runs replacement of one existing entity's complete semantic tag set.")]
    public EntityTagsPlanResult PlanEntityTags(EntityTagsPlanRequest request) =>
        _project.PlanEntityTags(request);

    [McpServerTool]
    [Description("Applies a previously validated existing-entity tag plan at its base revision.")]
    public EntityTagsPlanResult ApplyEntityTagsPlan(string planId) =>
        _project.ApplyEntityTagsPlan(planId);

    [McpServerTool]
    [Description("Reverts the most recently applied existing-entity tag plan when no later mutation exists.")]
    public EntityTagsPlanResult RevertEntityTagsPlan(string planId) =>
        _project.RevertEntityTagsPlan(planId);
}
