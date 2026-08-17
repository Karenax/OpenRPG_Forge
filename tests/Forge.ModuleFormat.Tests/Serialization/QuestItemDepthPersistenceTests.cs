using System.Text.Json.Nodes;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Items;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class QuestItemDepthPersistenceTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(), "openrpg-forge-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Aggregate_and_workspace_round_trips_preserve_quest_and_item_depth()
    {
        var project = CompleteProject();
        var aggregate = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(aggregate);
        var created = ProjectSession.Create(_temporaryRoot, project);
        var reopened = ProjectSession.Open(_temporaryRoot);

        Assert.Contains("\"formatVersion\": \"0.6.38\"", aggregate, StringComparison.Ordinal);
        Assert.Equal(aggregate, ProjectJsonSerializer.Serialize(restored));
        Assert.True(created.Succeeded);
        Assert.True(reopened.Succeeded);
        Assert.Contains("\"formatVersion\": \"1.30.0\"",
            File.ReadAllText(reopened.Session!.ProjectFilePath), StringComparison.Ordinal);
        Assert.Equal(aggregate, ProjectJsonSerializer.Serialize(reopened.Session.Project));
        Assert.Equal(QuestShareMode.Unanimous, restored.Quests.Single().Operations!.Share.Mode);
        var item = restored.Blueprints.Single(candidate => candidate.Kind == BlueprintKind.Item).ItemAdvanced!;
        Assert.Equal("longsword", item.Weapon!.WeaponClass);
        Assert.True(item.Unique.Enabled);
        Assert.True(item.Generator!.Enabled);
    }

    [Fact]
    public void Previous_aggregate_without_depth_profiles_migrates_to_optional_fallbacks()
    {
        var node = JsonNode.Parse(ProjectJsonSerializer.Serialize(CompleteProject()))!.AsObject();
        node["formatVersion"] = ProjectJsonSerializer.PreAdvancedRpgDepthFormatVersion;
        foreach (var blueprint in node["project"]!["blueprints"]!.AsArray())
            blueprint!.AsObject().Remove("itemAdvanced");
        foreach (var quest in node["project"]!["quests"]!.AsArray())
            quest!.AsObject().Remove("operations");

        var restored = ProjectJsonSerializer.Deserialize(node.ToJsonString());

        Assert.All(restored.Blueprints, blueprint => Assert.Null(blueprint.ItemAdvanced));
        Assert.All(restored.Quests, quest => Assert.Null(quest.Operations));
    }

    [Fact]
    public void Previous_workspace_without_depth_profiles_reopens_and_saves_current_version()
    {
        var created = ProjectSession.Create(_temporaryRoot, CompleteProject());
        var session = Assert.IsType<ProjectSession>(created.Session);
        var node = JsonNode.Parse(File.ReadAllText(session.ProjectFilePath))!.AsObject();
        node["formatVersion"] = "1.24.0";
        foreach (var blueprint in node["project"]!["blueprints"]!.AsArray())
            blueprint!.AsObject().Remove("itemAdvanced");
        foreach (var quest in node["project"]!["quests"]!.AsArray())
            quest!.AsObject().Remove("operations");
        File.WriteAllText(session.ProjectFilePath, node.ToJsonString());

        var reopened = ProjectSession.Open(_temporaryRoot);
        var save = reopened.Session!.Save(reopened.Session.Project);

        Assert.True(reopened.Succeeded);
        Assert.True(save.Succeeded);
        Assert.Contains("\"formatVersion\": \"1.30.0\"",
            File.ReadAllText(reopened.Session.ProjectFilePath), StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot)) Directory.Delete(_temporaryRoot, true);
    }

    private static ProjectDefinition CompleteProject()
    {
        var source = TestProjectFactory.CreateValid();
        var quest = source.Quests.Single();
        var transition = quest.Transitions.Single().Id;
        var operations = new QuestOperationsProfile(
            Guid.Parse("10500000-0000-4000-8000-000000000001"), 1, "test:g13-j", true,
            new(QuestJournalSortingPolicy.ImportanceThenUpdated, 85, true, true, 90, true),
            new(true, 3600, 3, QuestRepeatResetMode.Full, [], .75, 2026),
            new(QuestShareMode.Unanimous, true, QuestSeparationPolicy.BlockSeparation, true),
            [new(Guid.Parse("10500000-0000-4000-8000-000000000002"), transition, "ending", true,
                new LocalizedText("quest.confirm", "Confirmer ce choix ?"),
                new LocalizedText("quest.consequence", "Ce choix est irréversible."), [], "test:g13-j")],
            new(QuestNotificationMode.Cinematic,
                new LocalizedText("quest.notice.title", "Quête mise à jour"),
                new LocalizedText("quest.notice.body", "Le journal a changé."),
                "Quête mise à jour : le journal a changé.", "main-quest", 4));
        var itemProfile = ItemAdvancedProfile.CreateDefault(
            Guid.Parse("10500000-0000-4000-8000-000000000003"), "test:g13-k-m") with
        {
            Weapon = new("longsword", 1.2, false,
                [new("slash", "physical", 4, 9, 1.8, "attack.slash", ["blade"])], ["martial"]),
            Container = new(12, 60, 4, .8, ["loot"], ["inventory.open"], true, true),
            Unique = new(true, "unique:test-blade", ItemUniqueScope.World, 60, true),
            Generator = new(true, 3, 1, 1, ["weapon"], new Dictionary<string, int> { ["common"] = 1 },
                [new(Guid.Parse("10500000-0000-4000-8000-000000000004"), "keen", "weapon", 2, 1,
                    "common", [], ["critical"], "test:g13-m")], 5)
        };
        var item = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("10500000-0000-4000-8000-000000000005")),
            "test-blade", "Test blade", BlueprintKind.Item, itemAdvanced: itemProfile);
        return new(source.Id, source.Title, source.Module, [.. source.Blueprints, item], source.Areas,
            source.Dialogues, [quest.WithOperations(operations)]);
    }
}
