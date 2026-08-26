using System.ComponentModel;
using Forge.Mcp.Assistant;
using Forge.Mcp.Projects;
using ModelContextProtocol.Server;

namespace Forge.McpHost;

[McpServerToolType]
public sealed class ForgeSpatialContextMcpTools
{
    private readonly ForgeProjectAgentService _project;

    public ForgeSpatialContextMcpTools(ForgeProjectAgentService project) =>
        _project = project ?? throw new ArgumentNullException(nameof(project));

    [McpServerTool]
    [Description("Returns bounded spatial context schema v1 for explicit area/entity UUID selections, including safe display, blueprint/asset, tags, full transform and typed references; never dumps an unselected project or raw folder.")]
    public AgentSpatialContextResource GetBoundedSpatialContext(AgentSpatialContextQuery query) =>
        _project.GetBoundedSpatialContext(query);
}
