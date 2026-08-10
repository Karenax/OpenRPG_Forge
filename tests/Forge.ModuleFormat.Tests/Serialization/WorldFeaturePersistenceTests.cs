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

        Assert.Contains("\"formatVersion\": \"0.6.34\"", aggregate, StringComparison.Ordinal);
        Assert.Equal(aggregate, ProjectJsonSerializer.Serialize(restored));
        Assert.True(created.Succeeded);
        Assert.True(reopened.Succeeded);
        Assert.Contains("\"formatVersion\": \"1.28.0\"", File.ReadAllText(reopened.Session!.ProjectFilePath),
            StringComparison.Ordinal);
        var catalog = reopened.Session.Project.Module.WorldFeatures;
        Assert.Single(catalog.Placeables);
        Assert.True(catalog.Placeables[0].FoundationRequiredAtRuntime);
        Assert.Equal("world-placeable", Assert.IsType<PlaceableWorldProfile.FoundationPolicy>(catalog.Placeables[0].Foundation).Slug);
        Assert.Equal(new Vector3Value(1.25f, 0, -2.5f), catalog.Placeables[0].Foundation!.Transform.Position);
        Assert.Equal("InteractionExecuted", catalog.Placeables[0].Foundation!.OnUsed!.EventBinding);
        Assert.Equal("lock:test", catalog.Placeables[0].Foundation!.Validation!.LockIds.Single());
        Assert.Single(catalog.Doors);
        Assert.Equal(9, catalog.Doors[0].Foundation!.States.Count);
        Assert.Equal(DoorWorldProfile.DestinationKind.Waypoint,
            catalog.Doors[0].Foundation!.Portal!.DestinationKind);
        Assert.Equal("nav-obstacle:test-door", catalog.Doors[0].Foundation!.Navigation!.ObstacleId);
        Assert.True(catalog.Doors[0].Foundation!.Navigation!.VisualizeInEditor);
        Assert.Single(catalog.Triggers);
        Assert.True(catalog.Triggers[0].FoundationRequiredAtRuntime);
        Assert.Equal(TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D, catalog.Triggers[0].Foundation!.Shape.Kind);
        Assert.Equal("VolumeEntered", catalog.Triggers[0].Foundation!.OnEnter.EventBinding);
        Assert.Equal("anchor:exit", catalog.Triggers[0].Foundation!.Portal!.SpawnAnchorId);
        Assert.True(catalog.Triggers[0].Foundation!.ValidationRequiredAtRuntime);
        Assert.Equal("scripts/enter", catalog.Triggers[0].Foundation!.Validation!.KnownScriptIds[0]);
        Assert.Single(catalog.Encounters);
        Assert.Equal(EncounterWorldProfile.VolumeShape.ExtrudedPolygon2D,
            catalog.Encounters[0].Foundation!.Volume.Shape);
        Assert.Single(catalog.Encounters[0].Foundation!.Candidates);
        Assert.Single(catalog.VehicleRoutes);
        Assert.Single(catalog.Merchants);
        Assert.True(catalog.Merchants[0].TransactionsRequiredAtRuntime);
        Assert.Equal("standard", Assert.IsType<MerchantWorldProfile.TransactionPolicy>(catalog.Merchants[0].Transactions)
            .Stock.Single().VariantId);
        Assert.Single(catalog.Companions);
        var companionValidation = Assert.IsType<CompanionWorldProfile.ValidationPolicy>(catalog.Companions[0].Validation);
        Assert.Equal("test:r037", companionValidation.Provenance);
        Assert.Equal(["follow", "hold"], companionValidation.CommandIds);
        Assert.True(catalog.Companions[0].ValidationRequiredAtRuntime);
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
        Assert.Contains("\"formatVersion\": \"1.28.0\"", File.ReadAllText(path), StringComparison.Ordinal);
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
        var item = new BlueprintDefinition(BlueprintId.From(Guid.Parse("15000000-0000-4000-8000-000000000008")),
            "world-item", "World item", BlueprintKind.Item);
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
                new(PlaceableWorldProfile.BodyMode.Rigid, 4, [], true, 2, true, "server"),
                new(Guid.Parse("15200000-0000-4000-8000-000000000040"), 1, "test:r046", "world-placeable",
                    [new("fr-FR", "Placeable", "Examiner")], ["world", "test"], "Test author notes",
                    new("assets/placeable.glb", ["mesh:placeable"], [], "assets/icon.png", "assets/portrait.png", []),
                    new("box", new(1, 1, 1), true, true, "world"),
                    new([new(Guid.Parse("15200000-0000-4000-8000-000000000041"), "placeable.examine",
                            "placeable-used", "true")], 2.5, 120, true, "placeable.examine", 1, "server", ["true"]),
                    null, new(true, true, true, true, true,
                        new Dictionary<string, string> { ["onUsed"] = "scripts/placeable_used" }),
                    new(new(1.25f, 0, -2.5f), new(0, 45, 0), Vector3Value.One, new(0, .5f, 0), .25,
                        null, true, "animation:rise"),
                    new Dictionary<string, string> { ["variant"] = "lit" },
                    new(Guid.Parse("15200000-0000-4000-8000-000000000042"), 1, "test:r047", "1.0.0",
                        "InteractionExecuted", "scripts/before", "scripts/after"),
                    new(["animation:rise"], ["lock:test"], ["trap:test"], ["inventory:test"],
                        ["physics:test"], 4, 4), true), true)],
            [new(Guid.Parse("15100000-0000-4000-8000-000000000002"), 1, "test:g13-p-q", door.Id,
                new(10, 1, false, "damaged", "breached", true, "debris", "repair"),
                new(string.Empty, "loading.title", "loading.tip", true, 0), [], dialogue.Id.Value, "ash",
                new(DoorWorldProfile.AssemblyKind.Double,
                    [new(Guid.Parse("15200000-0000-4000-8000-000000000001"), "left", -1, 0, false)],
                    "open", false, "close"),
                new(DoorWorldProfile.PlacementMode.FreePlacement, null, null, "closed",
                    [
                        new("open", DoorWorldProfile.StateKind.Open, "animation:open", false, true),
                        new("closed", DoorWorldProfile.StateKind.Closed, "animation:closed", true, true),
                        new("opening", DoorWorldProfile.StateKind.Opening, "animation:opening", false, false),
                        new("closing", DoorWorldProfile.StateKind.Closing, "animation:closing", true, false),
                        new("locked", DoorWorldProfile.StateKind.Locked, "animation:locked", true, true),
                        new("jammed", DoorWorldProfile.StateKind.Jammed, "animation:jammed", true, true),
                        new("broken", DoorWorldProfile.StateKind.Broken, "animation:broken", false, false),
                        new("disabled", DoorWorldProfile.StateKind.Disabled, "animation:disabled", true, false),
                        new("custom", DoorWorldProfile.StateKind.Custom, "animation:custom", true, true),
                    ],
                    [new("closed", "opening", "interact", 1, null)],
                    new(2.5, "both", 1, 2, 1, "server", 2, "door.unavailable"),
                    new(DoorWorldProfile.DestinationKind.Waypoint,
                        Guid.Parse("15200000-0000-4000-8000-000000000010"), "anchor:exit", 180, true,
                        ["portal.travel"]),
                    [new(Guid.Parse("15200000-0000-4000-8000-000000000011"),
                        Guid.Parse("15200000-0000-4000-8000-000000000012"), false, "Retour")],
                    new Dictionary<string, string>(),
                    new("nav-obstacle:test-door", "nav-link:test-door", ["closed", "locked", "jammed", "disabled", "custom"],
                        ["open", "broken"], .75, .25, true)))],
            [new(Guid.Parse("15100000-0000-4000-8000-000000000003"), 1, "test:g13-r-s", trigger.Id, area.Id,
                null, new("inspect", "cursor", "outline", "prompt", "script", true, "use"), null, "trigger",
                new("key", false, "denied"), new(true, "danger", false, true, "allowed", "denied"),
                new("tracking", 1, "trace", 0, 0, null, quest.Id.Value),
                new([creature.Id], 1, true, 0, "server", dialogue.Id.Value), new(true, true, true, true, true),
                new(Guid.Parse("15200000-0000-4000-8000-000000000050"), 1, "test:r048", "world-trigger",
                    new(TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D,
                        [new(0, 0, 0), new(4, 0, 0), new(4, 0, 4), new(0, 0, 4)], [],
                        new(4, 3, 4), 3, .25, true), new(["creature"], ["player"], [], ["characters"]),
                    ["area.ready"], 5, TriggerWorldProfile.FirePolicy.OncePerEntity,
                    [new(Guid.Parse("15200000-0000-4000-8000-000000000051"), "transition", "area:next",
                        new Dictionary<string, string>())],
                    new(TriggerWorldProfile.DestinationKind.Waypoint,
                        Guid.Parse("15200000-0000-4000-8000-000000000052"), "anchor:exit", new(0, 180, 0),
                        true, ["portal.travel"]), new(new(1.25f, 0, -2.5f), Vector3Value.Zero, Vector3Value.One, null),
                    new Dictionary<string, string> { ["variant"] = "exit" },
                    new(Guid.Parse("15200000-0000-4000-8000-000000000053"), 1, "test:r048", "1.0.0",
                        "VolumeEntered", null, "scripts/enter"),
                    new(Guid.Parse("15200000-0000-4000-8000-000000000054"), 1, "test:r048", "1.0.0",
                        "VolumeExited", null, "scripts/exit"),
                    new([Guid.Parse("15200000-0000-4000-8000-000000000052")], ["area:next"],
                        ["scripts/enter", "scripts/exit", "script"], ["characters"]), true), true)],
            [new(Guid.Parse("15100000-0000-4000-8000-000000000004"), 1, "test:g13-t-u", area.Id,
                new(1, 1, "true", true, true, 1), "ash", new(true, 1, 1, "encounter"),
                new(1, "ash", routeId, 1, 0, "false"),
                new(EncounterWorldProfile.VisibilityMode.Hidden, 0, false, string.Empty), new(1, 1, 1, true, true),
                new(new(EncounterWorldProfile.VolumeShape.ExtrudedPolygon2D,
                        [new(0, 0, 0), new(4, 0, 0), new(4, 0, 4), new(0, 0, 4)], Vector3Value.Zero,
                        4, 4, ["player"], ["party"], true, 12, "scripts/encounter", true),
                    [new(Guid.Parse("15200000-0000-4000-8000-000000000020"), creature.Id,
                        EncounterWorldProfile.CandidateRole.Controller, 3, 8, ["true"], ["ember"], 1)],
                    12, ["anchor:test"], ["reward:test"], 4))],
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
                new(true, 30, 100, true, true, true),
                new(Guid.Parse("15200000-0000-4000-8000-000000000030"), 1, "test:r045",
                    "interaction:trade", "choice:trade",
                    [new(Guid.Parse("15200000-0000-4000-8000-000000000031"), item.Id, "standard", 12, 5,
                        "gold", ["quest:open"])], ["fr-FR", "en-US"], ["quest:open"],
                    new Dictionary<string, string> { ["onOpen"] = "scripts/merchant_open" }, 100, 1000, true), true)],
            [new(Guid.Parse("15100000-0000-4000-8000-000000000006"), 1, "test:g13-y-z", creature.Id,
                new(dialogue.Id.Value, "full", "limited", "greeting", "full", "deny"),
                new("line", 0, 1, true, true, true),
                new(CompanionWorldProfile.ProgressionMode.Automatic, 1, 20, true, true, [5]),
                new(0, 0, 0, 0, [], []),
                new([creature.Id], 1, 0, [], true, dialogue.Id.Value),
                new(quest.Id.Value, "true", "departed", 0, "none"),
                new(true, true, 1, "sight", "traps", false),
                new(["heal"], [], .5, "ally", true),
                new(true, true, true, true, "owner"), new(true, true, "owner", true, 60),
                new(Guid.Parse("15300000-0000-4000-8000-000000000001"), 1, "test:r037",
                    new(true, true, "scripts/recruit", "gold", 5, 0),
                    new("party", "server", true), ["follow", "hold"],
                    new("companion.inventory", "companion.standard", ["main-hand", "armor"]),
                    new("companion.recoverable", "recoverable-corpse", 30),
                    [new("recruit", dialogue.Id.Value, "dialogue.recruit"),
                     new("owner", dialogue.Id.Value, "dialogue.owner")],
                    new(false, [area.Id], true, true, "party.safe", 15)), true)], scripts);
        return new(source.Id, source.Title, source.Module.WithWorldFeatures(catalog),
            [.. source.Blueprints, placeable, door, trigger, item], source.Areas, source.Dialogues, source.Quests);
    }
}
