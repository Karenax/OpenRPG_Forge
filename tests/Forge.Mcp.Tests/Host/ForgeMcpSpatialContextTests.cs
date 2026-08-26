using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Mcp.Assistant;
using Forge.McpHost;
using Forge.ModuleFormat.Workspaces;
using Microsoft.Extensions.AI;

namespace Forge.Mcp.Tests.Host;

public sealed class ForgeMcpSpatialContextTests
{
    [Fact]
    public void Structured_context_selection_contains_bounded_spatial_transform_data()
    {
        using var workspace = CreateSpatialWorkspace();
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var entity = project.Areas[0].Entities[0];

        var context = workspace.Project.GetStructuredAgentContext(
            new AgentContextQuery(20, [entity.Id.ToString()]));
        var json = JsonSerializer.SerializeToElement(context, CreateMcpSerializerOptions());

        Assert.True(json.TryGetProperty("spatial", out var spatial), json.ToString());
        var item = Assert.Single(spatial.GetProperty("items").EnumerateArray());
        Assert.Equal(entity.Id.ToString(), item.GetProperty("entityId").GetString());
        Assert.Equal(11, item.GetProperty("transform").GetProperty("position").GetProperty("x").GetSingle());
        Assert.True(context.Sections.Sum(section => section.Items.Count) +
            spatial.GetProperty("items").GetArrayLength() <= 20);
    }

    [Fact]
    public async Task Explicit_spatial_query_is_bounded_deterministic_private_scoped_and_workspace_local()
    {
        using var workspace = CreateSpatialWorkspace();
        using var secondWorkspace = new TestWorkspace();
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var area = project.Areas[0];
        var selected = area.Entities[0];
        var query = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["maximumEntities"] = 2,
            ["maximumReferences"] = 4,
            ["areaIds"] = new JsonArray(JsonValue.Create(area.Id.ToString())),
            ["entityIds"] = new JsonArray(JsonValue.Create(selected.Id.ToString())),
            ["permission"] = "ReadOnly",
        };

        var first = await InvokeSpatialToolAsync(workspace, query);
        var second = await InvokeSpatialToolAsync(workspace, query.DeepClone().AsObject());
        var items = first.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(2, items.Length);
        Assert.Equal(items.Select(ItemKey), second.GetProperty("items").EnumerateArray().Select(ItemKey));
        var selectedItem = Assert.Single(items, item =>
            item.GetProperty("entityId").GetString() == selected.Id.ToString());
        var transform = selectedItem.GetProperty("transform");
        Assert.Equal(11, transform.GetProperty("position").GetProperty("x").GetSingle());
        Assert.Equal(0.1f, transform.GetProperty("rotation").GetProperty("x").GetSingle());
        Assert.Equal(2, transform.GetProperty("scale").GetProperty("x").GetSingle());
        Assert.False(string.IsNullOrWhiteSpace(selectedItem.GetProperty("displayName").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(selectedItem.GetProperty("blueprintId").GetString()));
        Assert.All(items, item =>
        {
            var path = item.GetProperty("documentPath").GetString()!;
            Assert.StartsWith($"areas/{item.GetProperty("areaId").GetString()}/entities/", path,
                StringComparison.Ordinal);
            Assert.DoesNotContain("..", path, StringComparison.Ordinal);
            Assert.False(Path.IsPathRooted(path));
        });
        Assert.True(first.GetProperty("references").GetArrayLength() <= 4);
        var serialized = first.ToString();
        Assert.DoesNotContain("do-not-leak", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("password", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-token", serialized, StringComparison.OrdinalIgnoreCase);

        var outside = await InvokeSpatialToolAsync(workspace, new JsonObject
        {
            ["schemaVersion"] = 1,
            ["maximumEntities"] = 2,
            ["maximumReferences"] = 0,
            ["areaIds"] = new JsonArray(JsonValue.Create("../solaren")),
            ["entityIds"] = new JsonArray(),
            ["permission"] = "ReadOnly",
        });
        Assert.Empty(outside.GetProperty("items").EnumerateArray());
        Assert.Contains(outside.GetProperty("diagnostics").EnumerateArray(), item =>
            item.GetProperty("code").GetString() == "assistant.spatial.selector_invalid");

        var overLimit = await InvokeSpatialToolAsync(workspace, new JsonObject
        {
            ["schemaVersion"] = 1,
            ["maximumEntities"] = 65,
            ["maximumReferences"] = 0,
            ["areaIds"] = new JsonArray(JsonValue.Create(area.Id.ToString())),
            ["entityIds"] = new JsonArray(),
            ["permission"] = "ReadOnly",
        });
        Assert.Empty(overLimit.GetProperty("items").EnumerateArray());
        Assert.Contains(overLimit.GetProperty("diagnostics").EnumerateArray(), item =>
            item.GetProperty("code").GetString() == "assistant.spatial.limit_invalid");

        var denied = await InvokeSpatialToolAsync(workspace, new JsonObject
        {
            ["schemaVersion"] = 1,
            ["maximumEntities"] = 1,
            ["maximumReferences"] = 0,
            ["areaIds"] = new JsonArray(),
            ["entityIds"] = new JsonArray(JsonValue.Create(selected.Id.ToString())),
            ["permission"] = "BoundedAutonomous",
        });
        Assert.Empty(denied.GetProperty("items").EnumerateArray());
        Assert.Contains(denied.GetProperty("diagnostics").EnumerateArray(), item =>
            item.GetProperty("code").GetString() == "assistant.spatial.permission_denied");

        var secondProject = ProjectSession.Open(secondWorkspace.RootDirectory).Session!.Project;
        var secondEntity = secondProject.Areas.SelectMany(item => item.Entities)
            .Single(item => item.Id == selected.Id);
        var secondResult = await InvokeSpatialToolAsync(secondWorkspace, new JsonObject
        {
            ["schemaVersion"] = 1,
            ["maximumEntities"] = 1,
            ["maximumReferences"] = 0,
            ["areaIds"] = new JsonArray(),
            ["entityIds"] = new JsonArray(JsonValue.Create(secondEntity.Id.ToString())),
            ["permission"] = "ReadOnly",
        });
        var secondX = Assert.Single(secondResult.GetProperty("items").EnumerateArray())
            .GetProperty("transform").GetProperty("position").GetProperty("x").GetSingle();
        Assert.NotEqual(11, secondX);
    }

    private static string ItemKey(JsonElement item) =>
        $"{item.GetProperty("documentPath").GetString()}|{item.GetProperty("entityId").GetString()}";

    private static async Task<JsonElement> InvokeSpatialToolAsync(TestWorkspace workspace, JsonObject query)
    {
        var toolType = typeof(ForgeMcpTools).Assembly.GetType("Forge.McpHost.ForgeSpatialContextMcpTools");
        Assert.NotNull(toolType);
        var target = Activator.CreateInstance(toolType, workspace.Project);
        Assert.NotNull(target);
        var method = toolType.GetMethod("GetBoundedSpatialContext", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);
        var function = AIFunctionFactory.Create(method, target, new AIFunctionFactoryOptions
        {
            SerializerOptions = CreateMcpSerializerOptions(),
        });
        var result = await function.InvokeAsync(new AIFunctionArguments
        {
            ["query"] = JsonSerializer.SerializeToElement(query),
        });
        return Assert.IsType<JsonElement>(result);
    }

    private static TestWorkspace CreateSpatialWorkspace() => new(project =>
    {
        var area = project.Areas[0];
        var entity = area.Entities[0];
        var changedEntity = new EntityInstance(
            entity.Id,
            entity.BlueprintId,
            new TransformValue(
                new Vector3Value(11, 12, 13),
                new QuaternionValue(0.1f, 0.2f, 0.3f, 0.9f),
                new Vector3Value(2, 3, 4)),
            ["house", "secret-token"],
            new EntityPropertyOverrides(1,
                [new EntityPropertyOverride("password", EntityPropertyValueKind.String, "do-not-leak")]),
            entity.BlueprintVersionRequirement);
        var changedArea = area.WithEntities(
            area.Entities.Select(item => item.Id == entity.Id ? changedEntity : item).ToArray());
        return new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
            project.Areas.Select(item => item.Id == area.Id ? changedArea : item).ToArray(),
            project.Dialogues, project.Quests);
    });

    private static JsonSerializerOptions CreateMcpSerializerOptions() => new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        Converters = { new JsonStringEnumConverter() },
    };
}
