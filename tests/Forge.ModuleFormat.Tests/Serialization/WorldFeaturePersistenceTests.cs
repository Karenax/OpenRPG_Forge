using System.Text.Json.Nodes;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class WorldFeaturePersistenceTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(), "openrpg-forge-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Aggregate_and_workspace_round_trips_preserve_all_world_feature_families()
    {
        var project = CompleteProject();
        var aggregate = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(aggregate);
        var created = ProjectSession.Create(_temporaryRoot, project);
        var reopened = ProjectSession.Open(_temporaryRoot);

        Assert.Contains("\"formatVersion\": \"0.6.33\"", aggregate, StringComparison.Ordinal);
        Assert.Equal(aggregate, ProjectJsonSerializer.Serialize(restored));
        Assert.True(created.Succeeded);
        Assert.True(reopened.Succeeded);
        Assert.Contains("\"formatVersion\": \"1.27.0\"", File.ReadAllText(reopened.Session!.ProjectFilePath),
            StringComparison.Ordinal);
        var catalog = reopened.Session.Project.Module.WorldFeatures;
        Assert.Single(catalog.Placeables);
        Assert.Single(catalog.Doors);
        Assert.Single(catalog.Triggers);
        Assert.Single(catalog.Encounters);
        Assert.Single(catalog.VehicleRoutes);
        Assert.Single(catalog.Merchants);
        Assert.Single(catalog.Companions);
        Assert.Single(catalog.Scripts.Camera.Shots);
        Assert.Single(catalog.Scripts.TagRouting.Routes);
        Assert.Single(catalog.Scripts.ModuleFlags);
        Assert.Single(catalog.Scripts.AiSettings);
    }

    [Fact]
    public void Previous_formats_without_catalog_migrate_to_empty_optional_fallback()
    {
        var aggregate = JsonNode.Parse(ProjectJsonSerializer.Serialize(CompleteProject()))!.AsObject();
        aggregate["formatVersion"] = ProjectJsonSerializer.PreWorldFeaturesFormatVersion;
        aggregate["project"]!["module"]!.AsObject().Remove("worldFeatures");
        var restored = ProjectJsonSerializer.Deserialize(aggregate.ToJsonString());
        Assert.Empty(restored.Module.WorldFeatures.Placeables);
        Assert.Empty(restored.Module.WorldFeatures.Merchants);
        Assert.Empty(restored.Module.WorldFeatures.Scripts.ModuleFlags);

        var created = ProjectSession.Create(_temporaryRoot, CompleteProject());
        var path = created.Session!.ProjectFilePath;
        var workspace = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        workspace["formatVersion"] = "1.25.0";
        workspace["project"]!["module"]!.AsObject().Remove("worldFeatures");
        File.WriteAllText(path, workspace.ToJsonString());
        var reopened = ProjectSession.Open(_temporaryRoot);
        Assert.True(reopened.Succeeded);
        Assert.Empty(reopened.Session!.Project.Module.WorldFeatures.Companions);
        Assert.True(reopened.Session.Save(reopened.Session.Project).Succeeded);
        Assert.Contains("\"formatVersion\": \"1.27.0\"", File.ReadAllText(path), StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot)) Directory.Delete(_temporaryRoot, true);
    }

    private static ProjectDefinition CompleteProject()
    {
        var source = TestProjectFactory.CreateValid();
        var placeable = new BlueprintDefinition(BlueprintId.From(Guid.Parse("15000000-0000-4000-8000-000000000001")),
            "world-placeable", "World placeable", BlueprintKind.Placeable);
        var door = new BlueprintDefinition(BlueprintId.From(Guid.Parse("15000000-0000-4000-8000-000000000002")),
            "world-door", "World door", BlueprintKind.Door);
        var trigger = new BlueprintDefinition(BlueprintId.From(Guid.Parse("15000000-0000-4000-8000-000000000003")),
            "world-trigger", "World trigger", BlueprintKind.Trigger);
        var creature = source.Blueprints.Single();
        var area = source.Areas.Single();
        var dialogue = source.Dialogues.Single();
        var quest = source.Quests.Single();
        var routeId = Guid.Parse("15000000-0000-4000-8000-000000000004");
        var routePointA = Guid.Parse("15000000-0000-4000-8000-000000000005");
        var routePointB = Guid.Parse("15000000-0000-4000-8000-000000000006");
        var scripts = new ScriptAutomationProfile(source.Module.Id.Value, 1, "test:g13-aa-ab",
            new([new(Guid.Parse("15000000-0000-4000-8000-000000000007"), "rig", "shot", 2, .25, true, true)],
                "restore", true, true),
            new(true, true, [new("door", "open", "scripts/door_open", true)]),
            [new("rest.enabled", ScriptAutomationProfile.SettingKind.Boolean, "true",
                ScriptAutomationProfile.FlagScope.Module, "Safe rest switch.", 1)],
            [new("combat.style", ScriptAutomationProfile.SettingKind.Choice, "balanced", ["balanced", "bold"],
                "Balanced combat style.", true)]);
        var catalog = new WorldFeatureCatalog(source.Module.Id.Value, 1, "test:g13-n-ab",
            [new(Guid.Parse("15100000-0000-4000-8000-000000000001"), 1, "test:g13-n-o", placeable.Id,
                "ash", dialogue.Id.Value,
                new("key", PlaceableWorldProfile.CredentialConsumption.Consume, 1, null, true),
                new([], new(false, 0, 0, true, "placeable")),
                new(PlaceableWorldProfile.BodyMode.Rigid, 4, [], true, 2, true, "server"))],
            [new(Guid.Parse("15100000-0000-4000-8000-000000000002"), 1, "test:g13-p-q", door.Id,
                new(10, 1, false, "damaged", "breached", true, "debris", "repair"),
                new(string.Empty, "loading.title", "loading.tip", true, 0), [], dialogue.Id.Value, "ash",
                new(DoorWorldProfile.AssemblyKind.Double,
                    [new(Guid.Parse("15200000-0000-4000-8000-000000000001"), "left", -1, 0, false)],
                    "open", false, "close"))],
            [new(Guid.Parse("15100000-0000-4000-8000-000000000003"), 1, "test:g13-r-s", trigger.Id, area.Id,
                null, new("inspect", "cursor", "outline", "prompt", "script", true, "use"), null, "trigger",
                new("key", false, "denied"), new(true, "danger", false, true, "allowed", "denied"),
                new("tracking", 1, "trace", 0, 0, null, quest.Id.Value),
                new([creature.Id], 1, true, 0, "server", dialogue.Id.Value), new(true, true, true, true, true))],
            [new(Guid.Parse("15100000-0000-4000-8000-000000000004"), 1, "test:g13-t-u", area.Id,
                new(1, 1, "true", true, true, 1), "ash", new(true, 1, 1, "encounter"),
                new(1, "ash", routeId, 1, 0, "false"),
                new(EncounterWorldProfile.VisibilityMode.Hidden, 0, false, string.Empty), new(1, 1, 1, true, true))],
            [new(routeId, 1, "test:g13-v", area.Id,
                [new(routePointA, Vector3Value.Zero, 0, 2), new(routePointB, new(5, 0, 0), 0, 2)],
                [new("lane", 0, 5, "forward")], [new(Guid.Parse("15200000-0000-4000-8000-000000000002"), routePointB, 1, "stop")],
                5, false, "server")],
            [new(Guid.Parse("15100000-0000-4000-8000-000000000005"), 1, "test:g13-w-x", creature.Id,
                [new("gold", "currency.gold", 1, true, false)], [new("gold", 100, 0, 0, 10, 200)],
                new(50, 100, new Dictionary<string, decimal>(), -1, true),
                new(true, [], false, false, 0, "crime"), new(24, "day", routeId, true, true),
                new("bargain", 1, .1, .1, 0, true), null,
                [new(Guid.Parse("15200000-0000-4000-8000-000000000003"), "repair", "repair", "gold", 1, "repair", "true")],
                new(true, 30, 100, true, true, true))],
            [new(Guid.Parse("15100000-0000-4000-8000-000000000006"), 1, "test:g13-y-z", creature.Id,
                new(dialogue.Id.Value, "full", "limited", "greeting", "full", "deny"),
                new("line", 0, 1, true, true, true),
                new(CompanionWorldProfile.ProgressionMode.Automatic, 1, 20, true, true, [5]),
                new(0, 0, 0, 0, [], []),
                new([creature.Id], 1, 0, [], true, dialogue.Id.Value),
                new(quest.Id.Value, "true", "departed", 0, "none"),
                new(true, true, 1, "sight", "traps", false),
                new(["heal"], [], .5, "ally", true),
                new(true, true, true, true, "owner"), new(true, true, "owner", true, 60))], scripts);
        return new(source.Id, source.Title, source.Module.WithWorldFeatures(catalog),
            [.. source.Blueprints, placeable, door, trigger], source.Areas, source.Dialogues, source.Quests);
    }
}
