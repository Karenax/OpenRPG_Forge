using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.Domain.WorldFeatures;

namespace Forge.Application.Tests.WorldFeatures;

public sealed class WorldFeatureCatalogWorkflowTests
{
    [Fact]
    public void Command_supports_preview_apply_history_and_typed_script_query()
    {
        var state = CreateState();
        var catalog = Catalog(state.Project.Module.Id.Value);
        var handler = new SetWorldFeatureCatalogHandler(new FixedClock(DateTimeOffset.UnixEpoch));
        var command = new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), state.Revision, catalog, true);

        var preview = handler.Handle(state, command);
        var applied = handler.Handle(state, command with { DryRun = false });
        var history = new ProjectCommandHistory(state);
        history.Record(applied);
        var script = new WorldFeatureScriptService();
        var handles = script.Query(history.State, "script");

        Assert.True(preview.Succeeded);
        Assert.Same(state, preview.State);
        Assert.Contains("flags=1", preview.Patch!.Summary);
        Assert.True(applied.Succeeded);
        Assert.Equal(state.Revision.Next(), applied.NewRevision);
        Assert.Single(handles);
        Assert.Equal("1", handles[0].Properties["flags"]);
        Assert.True(history.Undo().Succeeded);
        Assert.Empty(history.State.Project.Module.WorldFeatures.Scripts.ModuleFlags);
        Assert.True(history.Redo().Succeeded);
        Assert.Single(history.State.Project.Module.WorldFeatures.Scripts.ModuleFlags);
    }

    [Fact]
    public void Command_rejects_revision_identity_and_invalid_script_contracts()
    {
        var state = CreateState();
        var handler = new SetWorldFeatureCatalogHandler(new FixedClock(DateTimeOffset.UnixEpoch));
        var catalog = Catalog(state.Project.Module.Id.Value);
        var stale = handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision.Next(), catalog, false));
        var wrongId = handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision,
            Catalog(Guid.Parse("14000000-0000-4000-8000-000000000099")), false));
        var invalidScripts = catalog.Scripts with
        {
            ModuleFlags = [catalog.Scripts.ModuleFlags[0], catalog.Scripts.ModuleFlags[0]],
        };
        var invalid = handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision,
            new WorldFeatureCatalog(catalog.Id, 1, "test", scripts: invalidScripts), false));

        Assert.Contains(stale.Diagnostics, item => item.Code == "command.revision_conflict");
        Assert.Contains(wrongId.Diagnostics, item => item.Code == "world.catalog.id_mismatch");
        Assert.Contains(invalid.Diagnostics, item => item.Code == "world.script.flag_invalid");
    }

    [Fact]
    public void R041_DoorValidationReportsNavigationCollisionAndTransitionLoopBeforeApply()
    {
        var state = CreateState();
        var doorBlueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000010")),
            "validation-door", "Validation door", BlueprintKind.Door);
        var validDoor = Door(doorBlueprint.Id);
        var invalidDoor = validDoor with
        {
            Foundation = validDoor.Foundation! with
            {
                Links = [new(Guid.Parse("14000000-0000-4000-8000-000000000011"), validDoor.Id, true, string.Empty)],
                Navigation = validDoor.Foundation.Navigation! with
                {
                    ObstacleId = string.Empty,
                    BlockingStateIds = ["closed", "open"],
                    TraversableStateIds = ["open"],
                },
            },
        };
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r041", doors: [invalidDoor]);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [doorBlueprint], state.Project.Areas);

        var codes = WorldFeatureCatalogValidator.Validate(project).Select(item => item.Code).ToHashSet();

        Assert.Contains("world.door.navigation_invalid", codes);
        Assert.Contains("world.door.navigation_state_mismatch", codes);
        Assert.Contains("world.door.transition_loop", codes);
    }

    [Fact]
    public void R042_EncounterValidationRejectsGeometryPoolReferencesBudgetAnchorsRewardsAndCap()
    {
        var state = CreateState();
        var creatureBlueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000030")),
            "encounter-creature", "Encounter creature", BlueprintKind.Creature);
        var valid = Encounter(state.Project.Areas[0].Id, creatureBlueprint.Id);
        var missingBlueprint = BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000099"));
        var invalid = valid with
        {
            Foundation = valid.Foundation! with
            {
                Volume = valid.Foundation.Volume with { Points = [], Height = 0, ThreatRadius = -1 },
                Candidates = [valid.Foundation.Candidates[0] with
                {
                    BlueprintId = missingBlueprint,
                    Weight = 0,
                    Cost = 0,
                    Conditions = [string.Empty],
                    MaximumAlive = 5,
                }],
                SpawnBudget = 0,
                SpawnAnchorIds = [],
                RewardIds = [string.Empty],
                NetworkActorCap = 1,
            },
        };
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r042", encounters: [invalid]);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [creatureBlueprint], state.Project.Areas);

        var codes = WorldFeatureCatalogValidator.Validate(project).Select(item => item.Code).ToHashSet();

        Assert.Contains("world.encounter.volume_invalid", codes);
        Assert.Contains("world.encounter.candidate_missing", codes);
        Assert.Contains("world.encounter.candidate_invalid", codes);
        Assert.Contains("world.encounter.budget_invalid", codes);
        Assert.Contains("world.encounter.anchors_invalid", codes);
        Assert.Contains("world.encounter.rewards_invalid", codes);
        Assert.Contains("world.encounter.network_cap_invalid", codes);
    }

    [Fact]
    public void R045_MerchantValidationCoversBindingsStockPricesRestrictionsTreasuryLocalizationScriptsAndRuntimeRequirement()
    {
        var state = CreateState();
        var merchantBlueprint = new BlueprintDefinition(BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000040")),
            "merchant", "Merchant", BlueprintKind.Creature);
        var itemBlueprint = new BlueprintDefinition(BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000041")),
            "item", "Item", BlueprintKind.Item);
        var profile = new MerchantWorldProfile(Guid.Parse("14000000-0000-4000-8000-000000000042"), 1, "test:r045",
            merchantBlueprint.Id, [new("gold", "currency.gold", 1, true, false)],
            [new("gold", 100, 0, 0, 1, 200)], new(50, 100, new Dictionary<string, decimal>(), -1, true),
            new(false, [], false, false, 0, "crime"), new(24, "restock", null, true, true),
            new("bargain", 1, .1, .1, 0, true), null, [], new(true, 30, 100, true, true, true),
            new(Guid.Empty, 2, string.Empty, string.Empty, string.Empty,
                [new(Guid.Empty, BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000099")),
                    string.Empty, -1, -1, "missing", ["unknown"])], [], [string.Empty],
                new Dictionary<string, string> { ["onOpen"] = string.Empty }, 0, 0, false), true);
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r045", merchants: [profile]);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [merchantBlueprint, itemBlueprint], state.Project.Areas);

        var codes = WorldFeatureCatalogValidator.Validate(project).Select(item => item.Code).ToHashSet();

        Assert.Contains("world.merchant.vendor_binding_invalid", codes);
        Assert.Contains("world.merchant.authority_invalid", codes);
        Assert.Contains("world.merchant.capacity_invalid", codes);
        Assert.Contains("world.merchant.localization_invalid", codes);
        Assert.Contains("world.merchant.restrictions_invalid", codes);
        Assert.Contains("world.merchant.scripts_invalid", codes);
        Assert.Contains("world.merchant.stock_identity_invalid", codes);
        Assert.Contains("world.merchant.stock_item_invalid", codes);
        Assert.Contains("world.merchant.stock_values_invalid", codes);
        Assert.Contains("world.merchant.stock_restriction_invalid", codes);

        var requiredWithoutPolicy = profile with { Transactions = null };
        var missingCatalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r045", merchants: [requiredWithoutPolicy]);
        var missingProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(missingCatalog), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(missingProject),
            item => item.Code == "world.merchant.transactions_required_missing");
    }

    [Fact]
    public void R046_PlaceableValidationCoversIdentityVisualRoleCollisionInteractionStaticTransformScriptsAndRequirement()
    {
        var state = CreateState();
        var blueprint = new BlueprintDefinition(BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000050")),
            "placeable", "Placeable", BlueprintKind.Placeable);
        var profileId = Guid.Parse("14000000-0000-4000-8000-000000000051");
        var profile = new PlaceableWorldProfile(profileId, 1, "test:r046", blueprint.Id, null, null, null,
            new([], new(false, 1, 1, true, "placeable")), null,
            new(Guid.Empty, 2, string.Empty, string.Empty, [new(string.Empty, string.Empty, string.Empty)],
                [string.Empty, string.Empty], string.Empty, new(string.Empty, [], [], string.Empty, string.Empty, []),
                new(string.Empty, Vector3Value.Zero, false, true, string.Empty),
                new([new(Guid.Empty, string.Empty, string.Empty, string.Empty)], 0, 0, true, string.Empty, -1,
                    string.Empty, []), new(false, false, true, true, true),
                new(false, false, false, false, false,
                    new Dictionary<string, string> { [string.Empty] = string.Empty }),
                new(Vector3Value.Zero, Vector3Value.Zero, Vector3Value.Zero, Vector3Value.Zero, -1, profileId,
                    true, string.Empty), new Dictionary<string, string> { [string.Empty] = string.Empty }), true);
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r046", placeables: [profile]);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [blueprint], state.Project.Areas);

        var codes = WorldFeatureCatalogValidator.Validate(project).Select(item => item.Code).ToHashSet();

        Assert.Contains("world.placeable.identity_invalid", codes);
        Assert.Contains("world.placeable.visual_invalid", codes);
        Assert.Contains("world.placeable.metadata_invalid", codes);
        Assert.Contains("world.placeable.collision_invalid", codes);
        Assert.Contains("world.placeable.role_invalid", codes);
        Assert.Contains("world.placeable.static_invalid", codes);
        Assert.Contains("world.placeable.interaction_invalid", codes);
        Assert.Contains("world.placeable.transform_invalid", codes);
        Assert.Contains("world.placeable.scripts_invalid", codes);

        var requiredCatalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r046",
            placeables: [profile with { Foundation = null }]);
        var requiredProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(requiredCatalog), project.Blueprints, project.Areas);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(requiredProject),
            item => item.Code == "world.placeable.foundation_required_missing");
    }

    [Fact]
    public void R047_PlaceableValidationCoversOnUsedAnimationLocksTrapsInventoryPhysicsAndBudgets()
    {
        var state = CreateState();
        var blueprint = new BlueprintDefinition(BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000060")),
            "validated-placeable", "Validated placeable", BlueprintKind.Placeable);
        var foundation = new PlaceableWorldProfile.FoundationPolicy(
            Guid.Parse("14000000-0000-4000-8000-000000000061"), 1, "test:r047", "validated-placeable",
            [new("fr-FR", "Placeable", "Utiliser")], ["test"], "notes",
            new("assets/placeable.glb", ["mesh:placeable"], [], "assets/icon.png", "assets/portrait.png", []),
            new("box", new(1, 1, 1), true, true, "world"),
            new([new(Guid.Parse("14000000-0000-4000-8000-000000000062"), "placeable.use", "placeable-used", "true")],
                2, 120, true, "placeable.use", 1, "server", ["true"]), null,
            new(true, true, true, true, true, new Dictionary<string, string> { ["onUsed"] = "scripts/use" }),
            new(Vector3Value.Zero, Vector3Value.Zero, Vector3Value.One, Vector3Value.Zero, .25, null, true,
                "animation:rise"), new Dictionary<string, string>(),
            new(Guid.Parse("14000000-0000-4000-8000-000000000063"), 1, "test:r047", string.Empty,
                string.Empty, null, null), new([], [], [], [], [], 0, 0), true);
        var profile = new PlaceableWorldProfile(Guid.Parse("14000000-0000-4000-8000-000000000064"), 1,
            "test:r047", blueprint.Id, null, null, null, new([], new(false, 1, 1, true, "placeable")), null,
            foundation, true);
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r047", placeables: [profile]);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [blueprint], state.Project.Areas);

        var codes = WorldFeatureCatalogValidator.Validate(project).Select(item => item.Code).ToHashSet();

        Assert.Contains("world.placeable.on_used_invalid", codes);
        Assert.Contains("world.placeable.animation_invalid", codes);
        Assert.Contains("world.placeable.lock_reference_missing", codes);
        Assert.Contains("world.placeable.trap_reference_missing", codes);
        Assert.Contains("world.placeable.inventory_reference_missing", codes);
        Assert.Contains("world.placeable.physics_profile_missing", codes);
        Assert.Contains("world.placeable.budget_exceeded", codes);

        var requiredCatalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r047",
            placeables: [profile with { Foundation = foundation with { OnUsed = null } }]);
        var requiredProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(requiredCatalog), project.Blueprints, project.Areas);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(requiredProject),
            item => item.Code == "world.placeable.on_used_required_missing");
    }

    [Fact]
    public void R048_TriggerValidationCoversIdentityGeometryPolicyActionsPortalTransformEventsAndRequirement()
    {
        var state = CreateState();
        var blueprint = new BlueprintDefinition(BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000070")),
            "trigger", "Trigger", BlueprintKind.Trigger);
        var profileId = Guid.Parse("14000000-0000-4000-8000-000000000071");
        var foundation = new TriggerWorldProfile.FoundationPolicy(Guid.Empty, 2, string.Empty, string.Empty,
            new(TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D, [Vector3Value.Zero], [[Vector3Value.Zero]],
                Vector3Value.Zero, 0, -1, true),
            new([string.Empty], [string.Empty], [], []), [string.Empty], -1,
            TriggerWorldProfile.FirePolicy.Once, [],
            new(TriggerWorldProfile.DestinationKind.Waypoint, Guid.Empty, string.Empty, Vector3Value.Zero, true,
                [string.Empty]),
            new(Vector3Value.Zero, Vector3Value.Zero, Vector3Value.Zero, profileId),
            new Dictionary<string, string> { [string.Empty] = string.Empty },
            new(Guid.Empty, 2, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
            new(Guid.Empty, 2, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty));
        var profile = new TriggerWorldProfile(profileId, 1, "test:r048", blueprint.Id, state.Project.Areas[0].Id,
            null, null, null, string.Empty, null, null, null, null, null, foundation, true);
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r048", triggers: [profile]);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [blueprint], state.Project.Areas);

        var codes = WorldFeatureCatalogValidator.Validate(project).Select(item => item.Code).ToHashSet();

        Assert.Contains("world.trigger.identity_invalid", codes);
        Assert.Contains("world.trigger.geometry_invalid", codes);
        Assert.Contains("world.trigger.policy_invalid", codes);
        Assert.Contains("world.trigger.actions_invalid", codes);
        Assert.Contains("world.trigger.portal_invalid", codes);
        Assert.Contains("world.trigger.transform_invalid", codes);
        Assert.Contains("world.trigger.event_invalid", codes);

        var requiredCatalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r048",
            triggers: [profile with { Foundation = null }]);
        var requiredProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(requiredCatalog), project.Blueprints, project.Areas);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(requiredProject),
            item => item.Code == "world.trigger.foundation_required_missing");
    }

    [Fact]
    public void R049_TriggerValidationRejectsSelfIntersectionContradictoryFiltersTargetsPortalScriptsAndAccessibility()
    {
        var state = CreateState();
        var blueprint = new BlueprintDefinition(BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000080")),
            "validated-trigger", "Validated trigger", BlueprintKind.Trigger);
        var destination = Guid.Parse("14000000-0000-4000-8000-000000000081");
        var foundation = new TriggerWorldProfile.FoundationPolicy(
            Guid.Parse("14000000-0000-4000-8000-000000000082"), 1, "test:r049", "validated-trigger",
            new(TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D,
                [new(0, 0, 0), new(2, 0, 2), new(0, 0, 2), new(2, 0, 0)], [], new(2, 2, 2), 2, .25, true),
            new(["creature"], ["hero"], ["hero"], ["characters"]), [], -1,
            TriggerWorldProfile.FirePolicy.Repeat,
            [new(Guid.Parse("14000000-0000-4000-8000-000000000083"), "script", "missing-target",
                new Dictionary<string, string> { ["script"] = "missing-script" })],
            new(TriggerWorldProfile.DestinationKind.Waypoint, destination, "anchor:exit", Vector3Value.Zero,
                true, ["portal.travel"]), new(Vector3Value.Zero, Vector3Value.Zero, Vector3Value.One, null),
            new Dictionary<string, string>(),
            new(Guid.Parse("14000000-0000-4000-8000-000000000084"), 1, "test:r049", "1.0.0",
                "VolumeEntered", null, "scripts/enter"),
            new(Guid.Parse("14000000-0000-4000-8000-000000000085"), 1, "test:r049", "1.0.0",
                "VolumeExited", null, "scripts/exit"),
            new([Guid.Parse("14000000-0000-4000-8000-000000000086")], ["other-target"],
                ["other-script"], ["vehicles"]), true);
        var profile = new TriggerWorldProfile(Guid.Parse("14000000-0000-4000-8000-000000000087"), 1,
            "test:r049", blueprint.Id, state.Project.Areas[0].Id, null,
            new("inspect", "cursor", "outline", "prompt", "scripts/click", true, "trigger.use"), null,
            "trigger", null, null, null, null, null, foundation, true);
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r049", triggers: [profile]);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [blueprint], state.Project.Areas);

        var codes = WorldFeatureCatalogValidator.Validate(project).Select(item => item.Code).ToHashSet();

        Assert.Contains("world.trigger.geometry_self_intersection", codes);
        Assert.Contains("world.trigger.filter_contradiction", codes);
        Assert.Contains("world.trigger.action_target_missing", codes);
        Assert.Contains("world.trigger.portal_destination_missing", codes);
        Assert.Contains("world.trigger.policy_invalid", codes);
        Assert.Contains("world.trigger.script_reference_missing", codes);
        Assert.Contains("world.trigger.inaccessible", codes);

        var requiredCatalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r049",
            triggers: [profile with { Foundation = foundation with { Validation = null } }]);
        var requiredProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(requiredCatalog), project.Blueprints, project.Areas);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(requiredProject),
            item => item.Code == "world.trigger.validation_required_missing");
    }

    private static ProjectState CreateState()
    {
        var area = new AreaDefinition(AreaId.From(Guid.Parse("14000000-0000-4000-8000-000000000001")),
            "world", "World", []);
        var project = new ProjectDefinition(ProjectId.From(Guid.Parse("14000000-0000-4000-8000-000000000002")),
            "World features", [], [area]);
        return new(project, new ProjectRevision(7));
    }

    private static WorldFeatureCatalog Catalog(Guid moduleId)
    {
        var scripts = new ScriptAutomationProfile(moduleId, 1, "test:g13-aa-ab",
            new([], "restore-camera-and-control", true, true), new(false, true, []),
            [new("rest.enabled", ScriptAutomationProfile.SettingKind.Boolean, "true",
                ScriptAutomationProfile.FlagScope.Module, "Allows safe rest.", 1)],
            [new("combat.aggression", ScriptAutomationProfile.SettingKind.Choice, "balanced",
                ["passive", "balanced", "aggressive"], "Balanced combat behavior.", true)]);
        return new(moduleId, 1, "test:g13-n-ab", scripts: scripts);
    }

    private static DoorWorldProfile Door(BlueprintId blueprintId)
    {
        var id = Guid.Parse("14000000-0000-4000-8000-000000000020");
        return new(id, 1, "test:r041", blueprintId, null, null, [], null, null, null,
            new(DoorWorldProfile.PlacementMode.FreePlacement, null, null, "closed",
                [
                    new("open", DoorWorldProfile.StateKind.Open, "animation:open", false, true),
                    new("closed", DoorWorldProfile.StateKind.Closed, "animation:closed", true, true),
                    new("opening", DoorWorldProfile.StateKind.Opening, "animation:opening", false, false),
                ],
                [new("closed", "opening", "interact", 1, null)],
                new(2, "both", 1, 2, 1, "server", 2, "door.unavailable"), null, [],
                new Dictionary<string, string>(),
                new("nav-obstacle:door", "nav-link:door", ["closed"], ["open"], .75, .25, true)));
    }

    private static EncounterWorldProfile Encounter(AreaId areaId, BlueprintId blueprintId) => new(
        Guid.Parse("14000000-0000-4000-8000-000000000040"), 1, "test:r042", areaId,
        null, null, null, null, new(EncounterWorldProfile.VisibilityMode.Hidden, 0, false, string.Empty),
        new(0, 0, 0, true, true),
        new(new(EncounterWorldProfile.VolumeShape.ExtrudedPolygon2D,
                [new(0, 0, 0), new(4, 0, 0), new(4, 0, 4), new(0, 0, 4)], Vector3Value.Zero,
                4, 4, ["player"], ["party"], true, 12, "scripts/encounter", true),
            [new(Guid.Parse("14000000-0000-4000-8000-000000000041"), blueprintId,
                EncounterWorldProfile.CandidateRole.Controller, 3, 8, ["area.occupied"], ["ember"], 4)],
            12, ["anchor:test"], ["reward:test"], 8));
}
