using System.Text.Json;
using Forge.Application.Areas;
using Forge.Application.Gameplay;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Entities;
using Forge.Domain.Items;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Saves;
using Forge.ModuleFormat.Tests.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.RuntimePresentation;

public sealed class RuntimePresentationBoundaryTests
{
    [Fact]
    public void Dialogue_overlay_uses_the_active_authored_participant_portrait_without_crypt_defaults()
    {
        var runtimeRoot = Path.Combine(FindRepositoryRoot(), "apps", "Forge.Runtime", "Runtime");
        var overlay = File.ReadAllText(Path.Combine(runtimeRoot, "RuntimeDialogueOverlay.cs"));
        var gameRoot = ReadRuntimeGameRootSources(runtimeRoot);

        Assert.DoesNotContain("ElyraPortrait", overlay, StringComparison.Ordinal);
        Assert.DoesNotContain("elyra.png", overlay, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Gardienne des braises", overlay, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("string? portraitPath = null", overlay, StringComparison.Ordinal);
        Assert.Contains("ResolveActiveDialoguePortrait", gameRoot, StringComparison.Ordinal);
        Assert.Contains("_dialogueOverlay.ChoiceSelected += HandleDialogueOverlayChoiceSelected;", gameRoot,
            StringComparison.Ordinal);
        Assert.Contains("_dialogueOverlay.Closed += HandleDialogueOverlayClosed;", gameRoot,
            StringComparison.Ordinal);
        Assert.DoesNotContain("var choice = availableChoices.FirstOrDefault();", gameRoot,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Surface_wetness_localizes_regular_materials_without_deep_mesh_duplication()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Forge.EngineAdapter",
            "World",
            "GodotSurfaceWetnessAdapter.cs"));

        Assert.Contains(
            "meshInstance.GetSurfaceOverrideMaterial(surface) ?? mesh.SurfaceGetMaterial(surface)",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("meshInstance.Mesh = LocalizeMesh(mesh);", source, StringComparison.Ordinal);
        Assert.Contains("multiMeshInstance.Multimesh.Mesh = LocalizeMesh(multiMesh);", source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Authored_starting_inventory_is_available_to_runtime_conditions_without_being_saved_twice()
    {
        var fixture = CreateSyntheticInventoryProject();
        var inventory = new RuntimeInventoryState();

        inventory.SetAuthoredBaseline(RuntimeAuthoredInventoryResolver.Resolve(
            fixture.Project,
            fixture.AreaId,
            fixture.PlayerEntityId));

        Assert.True(inventory.HasItem("governed-item"));
        Assert.Empty(inventory.Snapshot());

        inventory.Grant("governed-item", 1);
        Assert.Equal([new RuntimeInventoryStack("governed-item", 1)], inventory.Snapshot());

        var presentation = RuntimeModulePresentationProjector.Project(
            fixture.Project,
            fixture.AreaId,
            new RuntimeModulePresentationContext(
                PlayerEntityId: fixture.PlayerEntityId,
                RuntimeInventory: inventory.Snapshot()));
        Assert.Equal(3, presentation.Inventory.Items.Single(item =>
            item.BlueprintId == fixture.ItemBlueprintId.ToString()).Quantity);

        inventory.Replace([new RuntimeInventoryStack("loaded-item", 2)]);
        Assert.True(inventory.HasItem("governed-item"));
        Assert.True(inventory.HasItem("loaded-item"));
        Assert.Equal([new RuntimeInventoryStack("loaded-item", 2)], inventory.Snapshot());
    }

    [Fact]
    public void Runtime_inventory_state_aggregates_grants_and_rejects_invalid_quantities()
    {
        var inventory = new RuntimeInventoryState();

        inventory.Grant("governed-item", 2);
        inventory.Grant("governed-item", 3);
        inventory.Grant("unknown-alpha", 1);

        Assert.True(inventory.HasItem("governed-item"));
        Assert.False(inventory.HasItem("missing-item"));
        Assert.Equal(
            [new RuntimeInventoryStack("governed-item", 5), new RuntimeInventoryStack("unknown-alpha", 1)],
            inventory.Snapshot());
        Assert.Throws<ArgumentOutOfRangeException>(() => inventory.Grant("governed-item", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeInventoryStack("governed-item", -1));
    }

    [Fact]
    public void Runtime_item_grants_and_has_item_checks_use_the_single_inventory_boundary()
    {
        var runtimeRoot = Path.Combine(FindRepositoryRoot(), "apps", "Forge.Runtime", "Runtime");
        var gameRoot = ReadRuntimeGameRootSources(runtimeRoot);
        var smoke = File.ReadAllText(Path.Combine(runtimeRoot, "GameRoot.Smoke.cs"));

        Assert.DoesNotContain("_inventory.Add(", gameRoot, StringComparison.Ordinal);
        Assert.DoesNotContain("_inventory.Contains(", gameRoot, StringComparison.Ordinal);
        Assert.Contains("private void GrantInventoryItem(string slug, int quantity)", gameRoot,
            StringComparison.Ordinal);
        Assert.Contains("_inventory.SetAuthoredBaseline(RuntimeAuthoredInventoryResolver.Resolve(", gameRoot,
            StringComparison.Ordinal);
        Assert.Contains("GrantInventoryItem(grant.ItemSlug, grant.Quantity)", gameRoot,
            StringComparison.Ordinal);
        Assert.Contains("GrantInventoryItem(action.Key, 1)", gameRoot, StringComparison.Ordinal);
        Assert.Contains("HasInventoryItem(condition.Key)", smoke, StringComparison.Ordinal);
    }

    [Fact]
    public void Single_projection_refresh_applies_live_damage_resource_spend_and_xp_to_hud_and_character_views()
    {
        var fixture = CreateSyntheticInventoryProject();

        var presentation = RuntimeModulePresentationProjector.Project(
            fixture.Project,
            fixture.AreaId,
            new RuntimeModulePresentationContext(
                PlayerEntityId: fixture.PlayerEntityId,
                RuntimePlayerResources:
                [
                    new RuntimeActorResourceSnapshot("health", 37, 100),
                    new RuntimeActorResourceSnapshot("stamina", 4, 10),
                ],
                RuntimeProgressionTracks:
                [
                    new RuntimeProgressionTrackSnapshot("adventurer", "Aventurier", 3, 20, 240),
                ]));

        var health = presentation.Hero.Resources.Single(resource => resource.Id == "health");
        var stamina = presentation.Hero.Resources.Single(resource => resource.Id == "stamina");
        var characterTrack = Assert.Single(presentation.Progression.Tracks);
        Assert.Equal((37d, 100d), (health.Current, health.Maximum));
        Assert.Equal((4d, 10d), (stamina.Current, stamina.Maximum));
        Assert.Equal(3, presentation.Hero.Level);
        Assert.Equal(presentation.Hero.Level, presentation.Progression.Level);
        Assert.Equal(240, presentation.Progression.Experience);
        Assert.Equal((3, 20, 240d),
            (characterTrack.Level, characterTrack.MaximumLevel, characterTrack.Experience));
    }

    [Fact]
    public void Primary_resource_selector_falls_back_to_the_first_authored_non_health_pool()
    {
        IReadOnlyList<RuntimeModulePresentation.Resource> resources =
        [
            new("health", 37, 100),
            new("mana", 12, 30),
            new("resolve", 4, 10),
        ];

        var selected = RuntimePrimaryResourceSelector.Select(resources);

        Assert.NotNull(selected);
        Assert.Equal("mana", selected.Id);
        Assert.Equal((12d, 30d), (selected.Current, selected.Maximum));
    }

    [Fact]
    public void Explicitly_absent_live_resources_and_tracks_remain_unavailable()
    {
        var fixture = CreateSyntheticInventoryProject();

        var presentation = RuntimeModulePresentationProjector.Project(
            fixture.Project,
            fixture.AreaId,
            new RuntimeModulePresentationContext(
                PlayerEntityId: fixture.PlayerEntityId,
                RuntimePlayerResources: [],
                RuntimeProgressionTracks: []));

        Assert.Empty(presentation.Hero.Resources);
        Assert.Empty(presentation.Progression.Tracks);
        Assert.Null(presentation.Hero.Level);
        Assert.Null(presentation.Progression.Level);
        Assert.Null(presentation.Progression.Experience);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RuntimeActorResourceSnapshot("health", 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RuntimeProgressionTrackSnapshot("adventurer", "Aventurier", 3, 2, 0));
    }

    [Fact]
    public void Runtime_refresh_rebuilds_once_before_hud_reads_and_pushes_the_same_live_presentation_to_menu()
    {
        var runtimeRoot = Path.Combine(FindRepositoryRoot(), "apps", "Forge.Runtime", "Runtime");
        var gameRoot = File.ReadAllText(Path.Combine(runtimeRoot, "GameRoot.cs"));
        var hud = File.ReadAllText(Path.Combine(runtimeRoot, "RuntimeHud.cs"));
        var updateHudStart = gameRoot.IndexOf("private void UpdateHud()", StringComparison.Ordinal);
        var updateHudEnd = gameRoot.IndexOf("private void PresentHudState", updateHudStart, StringComparison.Ordinal);
        var updateHud = gameRoot[updateHudStart..updateHudEnd];

        Assert.True(
            updateHud.IndexOf("RefreshRuntimePresentation();", StringComparison.Ordinal) <
            updateHud.IndexOf("UpdateTacticalHud();", StringComparison.Ordinal));
        Assert.Contains("RuntimePlayerResources: BuildLivePlayerResourceSnapshots()", gameRoot,
            StringComparison.Ordinal);
        Assert.Contains("RuntimeProgressionTracks: BuildLiveProgressionTrackSnapshots()", gameRoot,
            StringComparison.Ordinal);
        Assert.Contains("_rpgMenu?.UpdatePresentation(_runtimePresentation);", gameRoot,
            StringComparison.Ordinal);
        Assert.Contains("RuntimePrimaryResourceSelector.Select(hero.Resources)", gameRoot,
            StringComparison.Ordinal);
        Assert.DoesNotContain("if (refreshMenu)", gameRoot, StringComparison.Ordinal);
        Assert.DoesNotContain("healthResource?.Maximum ?? 1", hud, StringComparison.Ordinal);
        Assert.DoesNotContain("focusResource?.Maximum ?? 1", hud, StringComparison.Ordinal);
        Assert.Contains("RuntimePrimaryResourceSelector.Select(member.Resources)", hud, StringComparison.Ordinal);
        Assert.Contains("RuntimePrimaryResourceSelector.Select(hero.Resources)", hud, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(resource.Id, \"focus\"", hud, StringComparison.Ordinal);
        Assert.Contains("Indisponible", hud, StringComparison.Ordinal);
    }

    [Fact]
    public void Dialogue_traversal_is_bound_to_the_active_dialogue_identity()
    {
        var runtimeRoot = Path.Combine(FindRepositoryRoot(), "apps", "Forge.Runtime", "Runtime");
        var gameRoot = ReadRuntimeGameRootSources(runtimeRoot);

        Assert.Contains("_activeDialogueTraversalDialogueId != _activeDialogue.Id", gameRoot,
            StringComparison.Ordinal);
        Assert.Contains("_activeDialogueTraversal = new DialogueTraversal(dialogue, dialogue.EntryNodeId);", gameRoot,
            StringComparison.Ordinal);
        Assert.Contains("_activeDialogueTraversalDialogueId = dialogue.Id;", gameRoot,
            StringComparison.Ordinal);
        Assert.Contains("Accept(action.Key, track: true, out var newlyAccepted)", gameRoot,
            StringComparison.Ordinal);
        Assert.Contains("if (!newlyAccepted) break;", gameRoot, StringComparison.Ordinal);
    }

    [Fact]
    public void Synthetic_runtime_inventory_merges_authored_and_acquired_stacks_with_stable_unknown_ids_and_weight()
    {
        var fixture = CreateSyntheticInventoryProject();
        var runtimeState = new RuntimeInventoryState();
        runtimeState.Grant("governed-item", 1);
        runtimeState.Grant("governed-item", 2);
        runtimeState.Grant("unknown-alpha", 1);
        runtimeState.Grant("unknown-beta", 1);
        var runtimeInventory = runtimeState.Snapshot();

        var presentation = RuntimeModulePresentationProjector.Project(
            fixture.Project,
            fixture.AreaId,
            new RuntimeModulePresentationContext(
                PlayerEntityId: fixture.PlayerEntityId,
                RuntimeInventory: runtimeInventory));

        var governed = presentation.Inventory.Items.Single(item =>
            item.BlueprintId == fixture.ItemBlueprintId.ToString());
        var unknown = presentation.Inventory.Items.Where(item => item.ContainerId == "runtime" &&
            string.IsNullOrEmpty(item.BlueprintId)).OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();

        Assert.Equal(5, governed.Quantity);
        Assert.Equal(fixture.AuthoredInstanceId, governed.InstanceId);
        Assert.Equal(fixture.AuthoredInstanceId,
            Assert.Single(presentation.Inventory.Equipment).Item.InstanceId);
        Assert.Equal(2, unknown.Length);
        Assert.All(unknown, item => Assert.NotEqual(Guid.Empty, item.InstanceId));
        Assert.NotEqual(unknown[0].InstanceId, unknown[1].InstanceId);
        Assert.Equal(3, presentation.Inventory.OccupiedSlots);
        Assert.Equal(12.5, presentation.Inventory.Weight);

        var saveDirectory = Path.Combine(Path.GetTempPath(), "openrpg-runtime-inventory-" + Guid.NewGuid().ToString("N"));
        var savePath = Path.Combine(saveDirectory, "inventory.orpsave");
        var contentHash = new string('a', 64);
        try
        {
            var save = new RuntimeSaveDocument(
                RuntimeSaveDocument.CurrentFormatVersion,
                fixture.Project.Module.Id.ToString(),
                contentHash,
                fixture.AreaId.ToString(),
                new DateTimeOffset(2026, 8, 25, 18, 0, 0, TimeSpan.Zero),
                new RuntimeVector3(0, 0, 0),
                new RuntimeCombatantSave(fixture.PlayerEntityId.ToString(), 1, 0),
                runtimeInventory.Select(stack => new RuntimeInventorySaveStack(stack.Slug, stack.Quantity)).ToArray(),
                [],
                null,
                null,
                [],
                new RuntimeProgressFlags(false, false, false, false, false),
                null);

            Assert.True(RuntimeSaveStore.Save(savePath, save).Succeeded);
            var loaded = RuntimeSaveStore.Load(savePath, fixture.Project.Module.Id.ToString(), contentHash);
            Assert.True(loaded.Succeeded);

            var reopened = RuntimeModulePresentationProjector.Project(
                fixture.Project,
                fixture.AreaId,
                new RuntimeModulePresentationContext(
                    PlayerEntityId: fixture.PlayerEntityId,
                    RuntimeInventory: loaded.Save!.Inventory
                        .Select(stack => new RuntimeInventoryStack(stack.Slug, stack.Quantity))
                        .ToArray()));
            var reopenedGoverned = reopened.Inventory.Items.Single(item =>
                item.BlueprintId == fixture.ItemBlueprintId.ToString());
            var reopenedUnknownIds = reopened.Inventory.Items
                .Where(item => item.ContainerId == "runtime" && string.IsNullOrEmpty(item.BlueprintId))
                .OrderBy(item => item.Name, StringComparer.Ordinal)
                .Select(item => item.InstanceId)
                .ToArray();

            Assert.Equal(5, reopenedGoverned.Quantity);
            Assert.Equal(fixture.AuthoredInstanceId, reopenedGoverned.InstanceId);
            Assert.Equal(unknown.Select(item => item.InstanceId), reopenedUnknownIds);
            Assert.Equal(3, reopened.Inventory.OccupiedSlots);
            Assert.Equal(12.5, reopened.Inventory.Weight);
        }
        finally
        {
            if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, recursive: true);
        }
    }

    [Fact]
    public void Solaren_runtime_presentation_does_not_contain_crypt_demo_content()
    {
        var repositoryRoot = FindRepositoryRoot();
        var workspace = Path.Combine(repositoryRoot, "apps", "Forge.Runtime", "content", "solaren");
        var session = Assert.IsType<ProjectSession>(ProjectSession.Open(workspace).Session);
        var actorNames = session.Project.Areas
            .SelectMany(area => area.Entities)
            .SelectMany(entity => entity.Overrides.Properties)
            .Where(property => string.Equals(property.Key, "runtime.displayName", StringComparison.Ordinal))
            .Select(property => property.Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("Aerin", actorNames);
        Assert.Contains("Maélis", actorNames);
        Assert.Contains("Toman", actorNames);
        Assert.Contains("Orin", actorNames);

        var area = session.Project.Areas.Single();
        var player = FindPlayer(session, area, "Aerin");
        var presentation = RuntimeModulePresentationProjector.Project(
            session.Project,
            area.Id,
            new RuntimeModulePresentationContext(PlayerEntityId: player.Id));
        var serializedPresentation = JsonSerializer.Serialize(presentation);

        var runtimeSources = new[]
        {
            "RuntimeHud.cs",
            "RuntimeRpgMenu.cs",
            "RuntimeModuleMap.cs",
        }.Select(fileName => File.ReadAllText(Path.Combine(
            repositoryRoot, "apps", "Forge.Runtime", "Runtime", fileName)));
        var presentationSource = string.Join('\n', runtimeSources);

        foreach (var forbidden in new[]
                 {
                     "Elyra", "Brom", "Nahla", "Soren", "Lame d'aurore",
                     "assets/ui/portraits/elyra.png", "assets/ui/portraits/brom.png",
                     "assets/ui/portraits/nahla.png", "assets/ui/portraits/soren.png",
                 })
        {
            Assert.DoesNotContain(forbidden, presentationSource, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(forbidden, serializedPresentation, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Entity_authored_portrait_override_precedes_shared_blueprint_portrait()
    {
        var session = OpenRuntimeWorkspace("solaren");
        var area = session.Project.Areas.Single();
        var player = FindPlayer(session, area, "Aerin");
        const string portrait = "assets/imported/portrait/aerin.png";
        var overrides = new EntityPropertyOverrides(
            EntityPropertyOverrides.CurrentSchemaVersion,
            player.Overrides.Properties
                .Where(item => !string.Equals(item.Key, "runtime.portrait", StringComparison.Ordinal) &&
                               !string.Equals(item.Key, "runtime.portraitAlt", StringComparison.Ordinal))
                .Concat([
                    new EntityPropertyOverride("runtime.portrait", EntityPropertyValueKind.String, portrait),
                    new EntityPropertyOverride("runtime.portraitAlt", EntityPropertyValueKind.String,
                        "Portrait authored d’Aerin"),
                ])
                .ToArray());
        var authoredPlayer = new EntityInstance(
            player.Id,
            player.BlueprintId,
            player.Transform,
            player.Tags,
            overrides,
            player.BlueprintVersionRequirement);
        var authoredArea = area.WithEntities(area.Entities
            .Select(entity => entity.Id == player.Id ? authoredPlayer : entity)
            .ToArray());
        var authoredProject = new ProjectDefinition(
            session.Project.Id,
            session.Project.Title,
            session.Project.Module,
            session.Project.Blueprints,
            session.Project.Areas.Select(candidate => candidate.Id == area.Id ? authoredArea : candidate).ToArray(),
            session.Project.Dialogues,
            session.Project.Quests);

        var presentation = RuntimeModulePresentationProjector.Project(
            authoredProject,
            area.Id,
            new RuntimeModulePresentationContext(PlayerEntityId: player.Id));

        Assert.Equal(portrait, presentation.Hero.PortraitPath);
        Assert.Equal("Portrait authored d’Aerin", presentation.Hero.PortraitAltText);
    }

    [Fact]
    public void Solaren_projection_has_honest_empty_party_inventory_progression_and_journal()
    {
        var session = OpenRuntimeWorkspace("solaren");
        var area = session.Project.Areas.Single();
        var player = FindPlayer(session, area, "Aerin");

        var presentation = RuntimeModulePresentationProjector.Project(
            session.Project,
            area.Id,
            new RuntimeModulePresentationContext(PlayerEntityId: player.Id));

        Assert.Equal("Aerin", presentation.Hero.DisplayName);
        Assert.Equal(
            "assets/imported/ccd0edb9-5d82-7357-a905-f687f4b181ff/aerin-portrait.png",
            presentation.Hero.PortraitPath);
        Assert.Equal("Portrait d’Aerin, rôdeur des Vallées Libres", presentation.Hero.PortraitAltText);
        Assert.Empty(presentation.Party);
        Assert.Empty(presentation.Inventory.Items);
        Assert.Empty(presentation.Inventory.Equipment);
        Assert.Empty(presentation.Inventory.Currencies);
        Assert.Empty(presentation.Progression.Tracks);
        Assert.Null(presentation.Progression.Level);
        Assert.Empty(presentation.Journal.Entries);
        Assert.Null(presentation.Journal.ActiveEntry);
        Assert.Null(presentation.Journal.ActiveObjective);
    }

    [Fact]
    public void Solaren_map_projection_uses_workspace_counts_weather_description_and_exploration()
    {
        var session = OpenRuntimeWorkspace("solaren");
        var project = session.Project;
        var area = project.Areas.Single();
        var player = FindPlayer(session, area, "Aerin");
        var exploration = new AreaMapExplorationState(
            area.Authoring.Map.ExplorationScope,
            player.Id.ToString(),
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<Guid>());

        var presentation = RuntimeModulePresentationProjector.Project(
            project,
            area.Id,
            new RuntimeModulePresentationContext(
                PlayerEntityId: player.Id,
                WeatherStateId: area.Environment.Weather.InitialStateId,
                Exploration: exploration));
        var map = presentation.Map;
        var metadata = project.Module.Metadata.GetDefaultLocalization();
        var expectedDescription = new[] { metadata.Description, metadata.Summary, project.Module.Description }
            .First(value => !string.IsNullOrWhiteSpace(value));
        var expectedCells = (int)Math.Ceiling(area.Dimensions.Width / area.Authoring.Map.ExplorationCellSize) *
                            (int)Math.Ceiling(area.Dimensions.Depth / area.Authoring.Map.ExplorationCellSize) *
                            area.Authoring.Map.FloorCount;
        var expectedVisiblePins = area.Authoring.Map.Pins.Count(pin => pin.InitiallyVisible);
        var expectedVisibleWaypoints = area.Navigation.Waypoints.Waypoints.Count(waypoint =>
            waypoint.Appearance.Visible && waypoint.Marker is not null &&
            (waypoint.Marker.VisibilityPolicy == MarkerVisibilityPolicy.Always ||
             waypoint.Marker.Revealed || waypoint.Marker.Tracked));
        var expectedPortalMarkers = area.Portals.Count(portal =>
            area.Entities.Any(entity => entity.Id == portal.SourceEntityId));

        Assert.Equal(project.Areas.Count, map.AreaCount);
        Assert.Equal(project.Areas.Sum(candidate => candidate.Portals.Count), map.PortalCount);
        Assert.Equal(project.Areas.Sum(candidate => candidate.Navigation.Waypoints.PatrolRoutes.Count),
            map.PatrolRouteCount);
        Assert.Equal(area.Authoring.Map.Pins.Count, map.PinCount);
        Assert.Equal(area.Navigation.Waypoints.Waypoints.Count, map.WaypointCount);
        Assert.Equal(area.Environment.Weather.States.Single(state =>
            state.Id == area.Environment.Weather.InitialStateId).Name, map.Weather);
        Assert.Equal(expectedDescription, map.Description);
        Assert.Equal(0, map.ExplorationPercent);
        Assert.Equal(0, map.RevealedCells);
        Assert.Equal(expectedCells, map.TotalCells);
        Assert.Equal(project.Areas.Sum(candidate => candidate.Portals.Count), map.Portals.Count);
        Assert.Equal(expectedVisiblePins + expectedVisibleWaypoints + expectedPortalMarkers, map.Markers.Count);
        Assert.Equal(area.Authoring.Map.Pins.Where(pin => pin.InitiallyVisible).Select(pin => pin.Label)
            .Order(StringComparer.Ordinal), map.Markers.Where(marker => marker.Kind == "pin").Select(marker => marker.Label)
            .Order(StringComparer.Ordinal));
        Assert.Equal(area.Navigation.Waypoints.PatrolRoutes.Count, map.PatrolRoutes.Count);

        var serialized = JsonSerializer.Serialize(map);
        Assert.DoesNotContain("68%", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("3 routes", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Brume claire", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Modéré", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Crypt_regression_is_projected_from_its_workspace_without_module_branching()
    {
        var session = OpenRuntimeWorkspace("crypt-of-ashes");
        var project = session.Project;
        var area = project.Areas.Single(candidate => candidate.Id == project.Module.EntryAreaId);
        var player = area.Entities.First(entity => project.Blueprints.Single(blueprint =>
            blueprint.Id == entity.BlueprintId).Kind == BlueprintKind.PlayerSpawn);
        var quest = Assert.Single(project.Quests);

        var presentation = RuntimeModulePresentationProjector.Project(
            project,
            area.Id,
            new RuntimeModulePresentationContext(
                PlayerEntityId: player.Id,
                PartyBlueprintIds: project.Module.WorldFeatures.Companions
                    .Select(companion => companion.BlueprintId)
                    .Distinct()
                    .ToArray(),
                QuestStates: [new RuntimeQuestStateSnapshot(quest.Id, quest.InitialStateId, IsAccepted: true)],
                TrackedQuestId: quest.Id,
                WeatherStateId: area.Environment.Weather.InitialStateId,
                Exploration: new AreaMapExplorationState(
                    area.Authoring.Map.ExplorationScope,
                    player.Id.ToString(),
                    new HashSet<string>(StringComparer.Ordinal),
                    new HashSet<Guid>())));

        Assert.Equal(project.Module.DisplayName, presentation.ModuleTitle);
        Assert.Equal(area.DisplayName, presentation.AreaTitle);
        Assert.Equal(quest.Title.Fallback, presentation.Journal.ActiveEntry?.Title);
        Assert.Equal(quest.States.Single(state => state.Id == quest.InitialStateId).Journal.Objectives
            .Select(objective => objective.Fallback), presentation.Journal.ActiveEntry?.Objectives);
        Assert.Equal(project.Areas.Count, presentation.Map.AreaCount);
        Assert.Equal(project.Areas.Sum(candidate => candidate.Portals.Count), presentation.Map.PortalCount);
        Assert.Equal(area.Environment.Weather.States.Single(state =>
            state.Id == area.Environment.Weather.InitialStateId).Name, presentation.Map.Weather);
        Assert.Equal(project.Module.WorldFeatures.Companions.Select(companion => companion.BlueprintId)
            .Distinct().Count(), presentation.Party.Count);

        var projectorSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "Forge.Application", "Gameplay", "RuntimeModulePresentationProjector.cs"));
        Assert.DoesNotContain("crypt-of-ashes", projectorSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("solaren", projectorSource, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Runtime_journal_keeps_title_description_situation_and_active_objective_in_distinct_controls()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "apps", "Forge.Runtime", "Runtime", "RuntimeRpgMenu.cs"));
        var updateStart = source.IndexOf("public void UpdateContext", StringComparison.Ordinal);
        var updateEnd = source.IndexOf("public void UpdatePresentation", updateStart, StringComparison.Ordinal);
        var updateContext = source[updateStart..updateEnd];

        Assert.Contains("JournalQuestTitle", source, StringComparison.Ordinal);
        Assert.Contains("JournalQuestDescription", source, StringComparison.Ordinal);
        Assert.Contains("JournalQuestSituation", source, StringComparison.Ordinal);
        Assert.Contains("JournalActiveObjective", source, StringComparison.Ordinal);
        Assert.Contains("var selected = journal.ActiveEntry;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("journal.Entries.FirstOrDefault", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_journalTitle", updateContext, StringComparison.Ordinal);
        Assert.DoesNotContain("_journalDescription", updateContext, StringComparison.Ordinal);
        Assert.Contains("_journalActiveObjective", updateContext, StringComparison.Ordinal);
    }

    private static ProjectSession OpenRuntimeWorkspace(string slug)
    {
        var workspace = Path.Combine(FindRepositoryRoot(), "apps", "Forge.Runtime", "content", slug);
        return Assert.IsType<ProjectSession>(ProjectSession.Open(workspace).Session);
    }

    private static string ReadRuntimeGameRootSources(string runtimeRoot) => string.Join(
        '\n',
        Directory.EnumerateFiles(runtimeRoot, "GameRoot*.cs", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .Select(File.ReadAllText));

    private static EntityInstance FindPlayer(ProjectSession session, Forge.Domain.Areas.AreaDefinition area,
        string displayName) => area.Entities.Single(entity => entity.Overrides.Properties.Any(property =>
        string.Equals(property.Key, "runtime.displayName", StringComparison.Ordinal) &&
        string.Equals(property.Value, displayName, StringComparison.Ordinal)) &&
        session.Project.Blueprints.Single(blueprint => blueprint.Id == entity.BlueprintId).Kind ==
        BlueprintKind.PlayerSpawn);

    private static SyntheticInventoryFixture CreateSyntheticInventoryProject()
    {
        var source = TestProjectFactory.CreateNavigationValid();
        var sourceArea = source.Areas.Single();
        var sourcePlayerEntity = sourceArea.Entities.Single(entity => source.Blueprints.Single(blueprint =>
            blueprint.Id == entity.BlueprintId).Kind == BlueprintKind.PlayerSpawn);
        var sourcePlayer = source.Blueprints.Single(blueprint => blueprint.Id == sourcePlayerEntity.BlueprintId);
        var itemBlueprintId = BlueprintId.From(Guid.Parse("d2100000-0000-4000-8000-000000000001"));
        var authoredInstanceId = Guid.Parse("d2100000-0000-4000-8000-000000000002");
        var creation = ItemCreationProfile.CreateDefault(
            Guid.Parse("d2100000-0000-4000-8000-000000000003"),
            Guid.Parse("d2100000-0000-4000-8000-000000000004"),
            "test:runtime-inventory") with
        {
            Inventory = new ItemInventoryPolicy(2.5, 1, 1, 1, 1, 1),
            Stack = new ItemStackPolicy(99, "governed-item", true),
        };
        var itemFoundation = ItemFoundationProfile.CreateDefault(
            Guid.Parse("d2100000-0000-4000-8000-000000000005"),
            "governed-item",
            "Governed item",
            "test:runtime-inventory") with
        {
            Creation = creation,
        };
        var itemBlueprint = new BlueprintDefinition(
            itemBlueprintId,
            "governed-item",
            "Governed item",
            BlueprintKind.Item,
            itemFoundation: itemFoundation);

        var defaultRules = CreatureRulesCompositionProfile.CreateDefault(
            Guid.Parse("d2100000-0000-4000-8000-000000000006"),
            "test:runtime-inventory");
        var inventory = new CreatureRulesInventoryProfile(
            Guid.Parse("d2100000-0000-4000-8000-000000000007"),
            [new CreatureRulesInventoryContainer(
                Guid.Parse("d2100000-0000-4000-8000-000000000008"),
                "backpack",
                8,
                100,
                100,
                [new CreatureRulesInventoryEntry(authoredInstanceId, itemBlueprintId, 2, 99, 1)])],
            new Dictionary<string, Guid>(StringComparer.Ordinal) { ["main-hand"] = authoredInstanceId },
            "player:test",
            true);
        var rules = new CreatureRulesCompositionProfile(
            defaultRules.Id,
            defaultRules.SchemaVersion,
            defaultRules.Provenance,
            defaultRules.AssetId,
            defaultRules.PreviewTransform,
            defaultRules.InitialState,
            defaultRules.Attributes,
            defaultRules.Defenses,
            defaultRules.Abilities,
            inventory,
            defaultRules.Overrides);
        var defaultFoundation = CreatureFoundationProfile.CreateDefault(
            Guid.Parse("d2100000-0000-4000-8000-000000000009"),
            sourcePlayer.Slug,
            sourcePlayer.DisplayName,
            "test:runtime-inventory");
        var foundation = new CreatureFoundationProfile(
            defaultFoundation.Id,
            defaultFoundation.SchemaVersion,
            defaultFoundation.Provenance,
            defaultFoundation.RequiredAtRuntime,
            defaultFoundation.Identity,
            defaultFoundation.DefaultTransform,
            defaultFoundation.Resources,
            defaultFoundation.Movement,
            defaultFoundation.References,
            defaultFoundation.Spawned,
            defaultFoundation.Overrides,
            defaultFoundation.AuthoringComposition,
            defaultFoundation.AuthoringCompositionRequiredAtRuntime,
            rules,
            true,
            defaultFoundation.InteractionComposition,
            defaultFoundation.InteractionCompositionRequiredAtRuntime,
            defaultFoundation.EventAiComposition,
            defaultFoundation.EventAiCompositionRequiredAtRuntime);
        var playerBlueprint = new BlueprintDefinition(
            sourcePlayer.Id,
            sourcePlayer.Slug,
            sourcePlayer.DisplayName,
            sourcePlayer.Kind,
            sourcePlayer.Combat,
            sourcePlayer.PaletteOrigin,
            sourcePlayer.IsFavorite,
            sourcePlayer.Provenance,
            sourcePlayer.CreaturePresentation,
            CreatureProgressionProfile.CreateDefault(
                Guid.Parse("d2100000-0000-4000-8000-000000000010"),
                "test:runtime-presentation"),
            sourcePlayer.CreatureLoot,
            sourcePlayer.CreatureRoleBehavior,
            sourcePlayer.CreatureEvents,
            sourcePlayer.ItemAdvanced,
            sourcePlayer.Authoring,
            foundation,
            sourcePlayer.ItemFoundation);
        var blueprints = source.Blueprints.Where(blueprint => blueprint.Id != sourcePlayer.Id)
            .Append(playerBlueprint)
            .Append(itemBlueprint)
            .ToArray();
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, blueprints, source.Areas,
            source.Dialogues, source.Quests);
        return new SyntheticInventoryFixture(project, sourceArea.Id, sourcePlayerEntity.Id, itemBlueprintId,
            authoredInstanceId);
    }

    private sealed record SyntheticInventoryFixture(
        ProjectDefinition Project,
        Forge.Domain.Areas.AreaId AreaId,
        EntityId PlayerEntityId,
        BlueprintId ItemBlueprintId,
        Guid AuthoredInstanceId);

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "PROJECT_CONTEXT.md")) &&
                    Directory.Exists(Path.Combine(directory.FullName, "apps", "Forge.Runtime")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("OpenRPG Forge repository root was not found.");
    }
}
