using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Forge.Application.Navigation;
using Forge.Domain.Navigation;
using Forge.Domain.Spatial;
using Forge.Mcp.Navigation;
using Forge.Mcp.Serialization;
using Forge.McpHost;
using Forge.ModuleFormat.Workspaces;
using Microsoft.Extensions.AI;

namespace Forge.Mcp.Tests.Host;

public sealed class ForgeMcpWaypointSurfaceTests
{
    [Fact]
    public void Valid_waypoint_preview_has_public_plan_apply_revert_and_redo_tools()
    {
        using var workspace = new TestWorkspace();
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var area = project.Areas[0];
        var preview = workspace.Project.PreviewWaypointAuthoringDomain(new WaypointAuthoringDomainPlanRequest(
            1,
            workspace.Project.GetCurrentRevision(),
            area.Id.ToString(),
            area.Navigation.Waypoints));

        Assert.True(preview.Succeeded, string.Join(" | ", preview.Diagnostics.Select(item => item.Message)));
        var publicMethods = typeof(ForgeMcpTools).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods())
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("PlanWaypointAuthoring", publicMethods);
        Assert.Contains("ApplyWaypointAuthoringPlan", publicMethods);
        Assert.Contains("RevertWaypointAuthoringPlan", publicMethods);
        Assert.Contains("RedoWaypointAuthoringPlan", publicMethods);
    }

    [Fact]
    public async Task Public_waypoint_plan_applies_persists_reverts_redoes_and_rejects_stale_revision()
    {
        using var workspace = new TestWorkspace();
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var area = project.Areas[0];
        var original = area.Navigation.Waypoints;
        var waypointId = Guid.Parse("aa640000-0000-4000-8000-000000000001");
        var waypoint = new WaypointProfile(
            waypointId,
            1,
            "test:mcp-waypoint-boundary",
            "MCP bounded waypoint",
            new Vector3Value(4, 0, 7),
            new WaypointEditorAppearance("flag", "#83d6ae", "quest", true, 24),
            new MapMarker("MCP bounded waypoint", "flag", 0, "quest", 2,
                MarkerVisibilityPolicy.Always, MarkerVisibilityScope.World,
                true, true, string.Empty, string.Empty, true),
            RequiredAtRuntime: true);
        var candidate = new WaypointAuthoringProfile(
            original.Id,
            original.SchemaVersion,
            "test:mcp-waypoint-boundary",
            area.Id,
            true,
            [.. original.Waypoints, waypoint],
            original.PatrolRoutes,
            original.Locators,
            original.Collections,
            original.RouteVisualization);
        var request = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["baseRevision"] = 0,
            ["areaId"] = area.Id.ToString(),
            ["profile"] = JsonNode.Parse(JsonSerializer.Serialize(candidate, CreateMcpSerializerOptions())),
        };

        var planned = await InvokeWaypointToolAsync(workspace, "PlanWaypointAuthoring", request);
        Assert.True(planned.GetProperty("succeeded").GetBoolean(), planned.ToString());
        Assert.Equal(JsonValueKind.Object, planned.GetProperty("semanticDiff").ValueKind);
        Assert.Equal(JsonValueKind.Object, planned.GetProperty("inverse").ValueKind);
        var planId = planned.GetProperty("planId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(planId));

        var applied = await InvokeWaypointToolAsync(workspace, "ApplyWaypointAuthoringPlan", planId!);
        Assert.True(applied.GetProperty("applied").GetBoolean(), applied.ToString());
        Assert.Equal(waypointId.ToString("D"), Assert.Single(workspace.Project.QueryWaypoints(
            waypointId.ToString("D"), 10).Waypoints).WaypointId);
        Assert.Contains(ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas[0]
            .Navigation.Waypoints.Waypoints, item => item.Id == waypointId);
        var runtimeProfile = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas[0]
            .Navigation.Waypoints;
        Assert.Empty(WaypointRuntimeService.ValidateRuntime(runtimeProfile));
        Assert.Equal(waypointId, Assert.Single(WaypointRuntimeService.VisibleMarkers(
            runtimeProfile, WaypointRuntimeState.Empty, MarkerVisibilityScope.World)).WaypointId);

        var stale = await InvokeWaypointToolAsync(workspace, "PlanWaypointAuthoring", request);
        Assert.False(stale.GetProperty("succeeded").GetBoolean());
        Assert.Contains(stale.GetProperty("diagnostics").EnumerateArray(), item =>
            item.GetProperty("code").GetString() == "command.revision_conflict");

        var reverted = await InvokeWaypointToolAsync(workspace, "RevertWaypointAuthoringPlan", planId!);
        Assert.True(reverted.GetProperty("reverted").GetBoolean(), reverted.ToString());
        Assert.DoesNotContain(ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas[0]
            .Navigation.Waypoints.Waypoints, item => item.Id == waypointId);

        var redone = await InvokeWaypointToolAsync(workspace, "RedoWaypointAuthoringPlan", planId!);
        Assert.True(redone.GetProperty("applied").GetBoolean(), redone.ToString());
        Assert.False(redone.GetProperty("reverted").GetBoolean());
        Assert.Contains(ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas[0]
            .Navigation.Waypoints.Waypoints, item => item.Id == waypointId);
        Assert.Equal(new[] { "planned", "apply", "revert", "redo" },
            redone.GetProperty("audit").EnumerateArray()
                .Select(item => item.GetProperty("operation").GetString()).ToArray());
    }

    private static async Task<JsonElement> InvokeWaypointToolAsync(
        TestWorkspace workspace,
        string methodName,
        object argument)
    {
        var toolType = typeof(ForgeMcpTools).Assembly.GetType("Forge.McpHost.ForgeWaypointMcpTools");
        Assert.NotNull(toolType);
        var target = Activator.CreateInstance(toolType, workspace.Project);
        Assert.NotNull(target);
        var method = toolType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);
        var function = AIFunctionFactory.Create(method, target, new AIFunctionFactoryOptions
        {
            SerializerOptions = CreateMcpSerializerOptions(),
        });
        var parameterName = method.GetParameters().Single().Name!;
        var result = await function.InvokeAsync(new AIFunctionArguments
        {
            [parameterName] = JsonSerializer.SerializeToElement(argument, CreateMcpSerializerOptions()),
        });
        return Assert.IsType<JsonElement>(result);
    }

    private static JsonSerializerOptions CreateMcpSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new McpVector3ValueJsonConverter());
        return options;
    }
}
