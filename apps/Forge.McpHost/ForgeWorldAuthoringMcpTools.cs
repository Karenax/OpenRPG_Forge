using System.ComponentModel;
using Forge.Mcp.Projects;
using ModelContextProtocol.Server;

namespace Forge.McpHost;

[McpServerToolType]
public sealed class ForgeWorldAuthoringMcpTools
{
    private readonly ForgeProjectAgentService _project;

    public ForgeWorldAuthoringMcpTools(ForgeProjectAgentService project) => _project = project;

    [McpServerTool]
    [Description("Returns one area's typed biome and scatter authoring values without mutation.")]
    public AreaBiomeResource GetAreaBiomes(string areaId) => _project.GetAreaBiomes(areaId);

    [McpServerTool]
    [Description("Validates and dry-runs a typed biome/scatter patch while preserving terrain and neighbouring world data.")]
    public AreaBiomePlanResult PlanAreaBiomes(AreaBiomePlanRequest request) => _project.PlanAreaBiomes(request);

    [McpServerTool]
    [Description("Applies a previously validated biome/scatter plan if its base revision still matches.")]
    public AreaBiomePlanResult ApplyAreaBiomePlan(string planId) => _project.ApplyAreaBiomePlan(planId);

    [McpServerTool]
    [Description("Reverts the most recently applied biome/scatter plan through canonical project history.")]
    public AreaBiomePlanResult RevertAreaBiomePlan(string planId) => _project.RevertAreaBiomePlan(planId);
}
