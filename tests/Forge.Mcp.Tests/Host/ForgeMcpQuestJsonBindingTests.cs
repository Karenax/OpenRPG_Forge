using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Forge.Application.Gameplay;
using Forge.Mcp.Serialization;
using Forge.McpHost;
using Microsoft.Extensions.AI;

namespace Forge.Mcp.Tests.Host;

public sealed class ForgeMcpQuestJsonBindingTests
{
    [Theory]
    [InlineData(nameof(ForgeMcpTools.PreviewQuestJournal))]
    [InlineData(nameof(ForgeMcpTools.PlanAssistedQuest))]
    public async Task Quest_fixture_crosses_the_real_mcp_json_function_binder(string methodName)
    {
        using var workspace = new TestWorkspace();
        var fixture = LoadQuestProposalFixture();
        var proposal = fixture["proposal"]?.DeepClone()
            ?? throw new InvalidDataException("The quest proposal fixture has no proposal.");
        var request = methodName switch
        {
            nameof(ForgeMcpTools.PreviewQuestJournal) => new JsonObject
            {
                ["schemaVersion"] = 1,
                ["baseRevision"] = workspace.Project.GetCurrentRevision(),
                ["questId"] = proposal["quest"]?["id"]?["value"]?.GetValue<string>(),
                ["quest"] = proposal["quest"]?.DeepClone(),
            },
            nameof(ForgeMcpTools.PlanAssistedQuest) => BuildApprovedAssistedQuestRequest(
                proposal,
                workspace.Project.GetCurrentRevision()),
            _ => throw new ArgumentOutOfRangeException(nameof(methodName)),
        };

        var response = await InvokeToolAsync(workspace, methodName, request);
        Assert.True(response.GetProperty("succeeded").GetBoolean(), response.ToString());
    }

    [Theory]
    [InlineData("quest", "id")]
    [InlineData("quest", "states", "0", "id")]
    [InlineData("quest", "transitions", "0", "id")]
    public async Task Invalid_adjacent_quest_ids_return_a_stable_mcp_diagnostic(params string[] path)
    {
        using var workspace = new TestWorkspace();
        var proposal = LoadQuestProposalFixture()["proposal"]?.DeepClone()
            ?? throw new InvalidDataException("The quest proposal fixture has no proposal.");
        JsonNode current = proposal;
        foreach (var segment in path)
        {
            current = int.TryParse(segment, out var index)
                ? current[index]!
                : current[segment]!;
        }
        current["value"] = "not-a-uuid";
        var request = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["baseRevision"] = workspace.Project.GetCurrentRevision(),
            ["questId"] = proposal["quest"]?["id"]?["value"]?.GetValue<string>() ?? "not-a-uuid",
            ["quest"] = proposal["quest"]?.DeepClone(),
        };

        var response = await InvokeToolAsync(workspace, nameof(ForgeMcpTools.PreviewQuestJournal), request);

        Assert.False(response.GetProperty("succeeded").GetBoolean());
        Assert.Contains(response.GetProperty("diagnostics").EnumerateArray(),
            item => item.GetProperty("code").GetString() == "quest.json.invalid");
    }

    [Fact]
    public async Task Bound_quest_json_plans_applies_queries_reopens_runs_in_runtime_and_reverts()
    {
        using var workspace = new TestWorkspace();
        var proposal = LoadQuestProposalFixture()["proposal"]?.DeepClone()
            ?? throw new InvalidDataException("The quest proposal fixture has no proposal.");
        var questId = proposal["quest"]?["id"]?["value"]?.GetValue<string>()
            ?? throw new InvalidDataException("The quest proposal fixture has no quest ID.");
        var staleRequest = BuildApprovedAssistedQuestRequest(
            proposal.DeepClone(), workspace.Project.GetCurrentRevision() + 1);

        var stale = await InvokeToolAsync(workspace, nameof(ForgeMcpTools.PlanAssistedQuest), staleRequest);
        Assert.False(stale.GetProperty("succeeded").GetBoolean());
        Assert.Contains(stale.GetProperty("diagnostics").EnumerateArray(), item =>
            item.GetProperty("code").GetString() == "assistant.quest.revision_conflict");

        var request = BuildApprovedAssistedQuestRequest(
            proposal, workspace.Project.GetCurrentRevision());
        var planned = await InvokeToolAsync(workspace, nameof(ForgeMcpTools.PlanAssistedQuest), request);
        Assert.True(planned.GetProperty("succeeded").GetBoolean(), planned.ToString());
        var planId = planned.GetProperty("planId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(planId));

        var applied = await InvokeToolAsync(
            workspace, nameof(ForgeMcpTools.ApplyAssistedQuestPlan), "planId", planId!);
        Assert.True(applied.GetProperty("applied").GetBoolean(), applied.ToString());
        Assert.Equal(questId, Assert.Single(workspace.Project.QueryQuestJournals(questId, 1).Items).QuestId);
        var reopened = Forge.ModuleFormat.Workspaces.ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var quest = Assert.Single(reopened.Quests, item => item.Id.ToString() == questId);
        var runtime = new RuntimeQuestStateTracker(reopened.Quests);
        Assert.Equal(quest.Id, runtime.ResolveQuest(quest.Slug)?.Id);
        Assert.True(runtime.Accept(quest.Id.ToString(), track: true));
        Assert.Equal(quest.Id, runtime.TrackedQuestId);
        Assert.Contains(runtime.Snapshot(), item => item.QuestId == quest.Id && item.IsAccepted);

        var reverted = await InvokeToolAsync(
            workspace, nameof(ForgeMcpTools.RevertAssistedQuestPlan), "planId", planId!);
        Assert.True(reverted.GetProperty("reverted").GetBoolean(), reverted.ToString());
        Assert.DoesNotContain(Forge.ModuleFormat.Workspaces.ProjectSession.Open(workspace.RootDirectory)
            .Session!.Project.Quests, item => item.Id.ToString() == questId);
        Assert.Equal(new[] { "planned", "apply", "revert" },
            reverted.GetProperty("audit").EnumerateArray()
                .Select(item => item.GetProperty("action").GetString()).ToArray());
    }

    private static async Task<JsonElement> InvokeToolAsync(
        TestWorkspace workspace,
        string methodName,
        JsonObject request)
        => await InvokeToolAsync(workspace, methodName, "request", request);

    private static async Task<JsonElement> InvokeToolAsync(
        TestWorkspace workspace,
        string methodName,
        string parameterName,
        object argument)
    {
        var tools = new ForgeMcpTools(workspace.Project, null!, null!, null!, null!, null!, null!);
        var method = typeof(ForgeMcpTools).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMethodException(typeof(ForgeMcpTools).FullName, methodName);
        var function = AIFunctionFactory.Create(method, tools, new AIFunctionFactoryOptions
        {
            SerializerOptions = CreateMcpSerializerOptions(),
        });
        var result = await function.InvokeAsync(new AIFunctionArguments
        {
            [parameterName] = JsonSerializer.SerializeToElement(argument, CreateMcpSerializerOptions()),
        });
        return Assert.IsType<JsonElement>(result);
    }

    private static JsonObject BuildApprovedAssistedQuestRequest(JsonNode proposal, long baseRevision)
    {
        proposal["approvedForApplication"] = true;
        proposal["approvalEvidence"] = "author-approved:mcp-json-boundary-test";
        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["baseRevision"] = baseRevision,
            ["proposal"] = proposal,
            ["permission"] = "ApplyApprovedPlan",
        };
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

    private static JsonObject LoadQuestProposalFixture()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenRPGForge.sln")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the OpenRPG Forge repository root.");
        var path = Path.Combine(root, "tests", "Forge.Mcp.Tests", "Fixtures", "quest-proposal.json");
        return JsonNode.Parse(File.ReadAllText(path))?.AsObject()
            ?? throw new InvalidDataException($"The fixture '{path}' is not a JSON object.");
    }
}
