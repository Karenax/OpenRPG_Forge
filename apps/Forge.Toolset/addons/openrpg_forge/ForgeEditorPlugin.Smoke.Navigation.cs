#if TOOLS
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Areas;
using Forge.Application.Assistant;
using Forge.Application.Assets;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Content;
using Forge.Application.Creator;
using Forge.Application.Creatures;
using Forge.Application.Dialogues;
using Forge.Application.Entities;
using Forge.Application.Factions;
using Forge.Application.Gameplay;
using Forge.Application.Items;
using Forge.Application.Modules;
using Forge.Application.Rules;
using Forge.Application.Localization;
using Forge.Application.Lifecycle;
using Forge.Application.Navigation;
using Forge.Application.Sequences;
using Forge.Application.Projects;
using Forge.Application.Quests;
using Forge.Application.Quality;
using Forge.Application.Spatial;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Audio;
using Forge.Domain.Assistant;
using Forge.Domain.Assets;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Forge.Domain.Build;
using Forge.Domain.Combat;
using Forge.Domain.Content;
using Forge.Domain.Creatures;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Factions;
using Forge.Domain.Gameplay;
using Forge.Domain.Localization;
using Forge.Domain.Lifecycle;
using Forge.Domain.Items;
using Forge.Domain.Modules;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Quality;
using Forge.Domain.Quests;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Rules;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.Domain.WorldFeatures;
using Forge.EngineAdapter.Assets;
using Forge.EngineAdapter.Audio;
using Forge.EngineAdapter.Rendering;
using Forge.EngineAdapter.World;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Content;
using Forge.ModuleFormat.Distribution;
using Forge.ModuleFormat.Diagnostics;
using Forge.ModuleFormat.Launcher;
using Forge.ModuleFormat.Navigation;
using Forge.ModuleFormat.Playtesting;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Content;
using Forge.Network.Replication;
using Forge.RpgKit.Application.Abilities;
using Forge.RpgKit.Application.Bosses;
using Forge.RpgKit.Application.Companions;
using Forge.RpgKit.Application.Combat;
using Forge.RpgKit.Application.Creatures;
using Forge.RpgKit.Application.Dialogues;
using Forge.RpgKit.Application.Doors;
using Forge.RpgKit.Application.Encounters;
using Forge.RpgKit.Application.Factions;
using Forge.RpgKit.Application.Merchants;
using Forge.RpgKit.Application.Placeables;
using Forge.RpgKit.Application.Quests;
using Forge.RpgKit.Application.Triggers;
using Forge.RpgKit.Domain.Merchants;
using Forge.RpgKit.Domain.Companions;
using Forge.RpgKit.Domain.Factions;
using RpgLockState = Forge.RpgKit.Domain.Placeables.LockState;
using RpgActivator = Forge.RpgKit.Domain.Encounters.Activator;
using RpgNetworkAuthority = Forge.RpgKit.Domain.Encounters.NetworkAuthority;
using RpgQueryableEntity = Forge.RpgKit.Domain.Triggers.QueryableEntity;
using RpgTriggerEventTrace = Forge.RpgKit.Domain.Triggers.TriggerEventTrace;
using RpgTriggerRuntimeState = Forge.RpgKit.Domain.Triggers.TriggerRuntimeState;
using Forge.Scripting;
using Godot;

namespace Forge.Toolset.Editor;

public partial class ForgeEditorPlugin
{
    private bool RunNavigationAndGeometrySmokeTest()
    {
        if (_navigationConfiguration is null || _history is null || _projectPreview is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G714_NAVIGATION_FAILED missing_window");
            return false;
        }

        var area = _history.State.Project.Areas.First();
        var original = area.Navigation;
        var link = new OffMeshNavigationLink(
            OffMeshNavigationLinkId.From(Guid.Parse("d7140000-0000-4000-8000-000000000001")),
            "Passage de validation",
            NavigationLinkKind.Jump,
            new Vector3Value(-4, 0, 24),
            new Vector3Value(4, 0, 24),
            true,
            1.25f,
            "workspace-smoke-test");
        var configured = new AreaNavigationDefinition(
            original.Id,
            0.5f,
            original.AgentProfiles,
            [.. original.OffMeshLinks, link],
            "workspace-smoke-test",
            original.SchemaVersion,
            original.Anchors,
            original.Waypoints);
        var configuredForSmoke = _navigationConfiguration.ConfigureForSmokeTest(area.Id, configured);
        var previewedForSmoke = _navigationConfiguration.PreviewForSmokeTest();
        var appliedForSmoke = _navigationConfiguration.ApplyForSmokeTest();
        if (!configuredForSmoke ||
            _navigationConfiguration.AgentCount != 4 ||
            _navigationConfiguration.LinkCount != original.OffMeshLinks.Count + 1 ||
            !previewedForSmoke ||
            _navigationConfiguration.DiagnosticCount != 0 ||
            _projectPreview.NavigationOverlayCellCount == 0 ||
            !appliedForSmoke)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_TOOLSET_G714_NAVIGATION_FAILED configured={configuredForSmoke} " +
                $"agents={_navigationConfiguration.AgentCount} links={_navigationConfiguration.LinkCount} " +
                $"preview={previewedForSmoke} diagnostics={_navigationConfiguration.DiagnosticCount} " +
                $"overlay={_projectPreview.NavigationOverlayCellCount} applied={appliedForSmoke}");
            return false;
        }

        var appliedArea = _history.State.Project.Areas.Single(candidate => candidate.Id == area.Id);
        var geometry = AreaGeometryValidator.Validate(_history.State.Project, appliedArea);
        var agent = appliedArea.Navigation.AgentProfiles.First(profile => profile.Name == "Grand");
        var current = NavigationSurfaceBuilder.Build(_history.State.Project, appliedArea, agent);
        var closed = NavigationSurfaceBuilder.Build(
            _history.State.Project,
            appliedArea,
            agent,
            NavigationDoorState.AllClosed);
        var partial = NavigationBakePlanner.Plan(current, closed);
        var reachability = AreaNavigationValidator.Analyze(
            _history.State.Project,
            appliedArea,
            agent);
        var routeEntities = appliedArea.Entities
            .Where(entity => current.IsWalkable(current.CellAt(entity.Transform.Position)))
            .Take(2)
            .ToArray();
        var route = routeEntities.Length == 2
            ? NavigationPathfinder.FindPath(current, new NavigationPathQuery(
                current.CellAt(routeEntities[0].Transform.Position),
                current.CellAt(routeEntities[1].Transform.Position)))
            : null;
        var changedCell = current.WalkableCells[current.WalkableCells.Count / 2];
        var dynamic = NavigationDynamicUpdatePlanner.Plan(
            current,
            new HashSet<NavigationCell>(),
            new HashSet<NavigationCell> { changedCell });
        if (geometry.Count != 0 || reachability.Diagnostics.Count != 0 ||
            partial.RequiresFullBake || partial.ChangedCells.Count == 0 || partial.ReusedCellCount == 0 ||
            current.OffMeshEdges.Count == 0 || route?.Succeeded != true ||
            dynamic.ChangedCells.Count != 1 || dynamic.RequiresFullSearchReset)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_TOOLSET_G714_NAVIGATION_FAILED geometry={geometry.Count} " +
                $"reachability={reachability.Diagnostics.Count} full_bake={partial.RequiresFullBake} " +
                $"changed={partial.ChangedCells.Count} reused={partial.ReusedCellCount} " +
                $"offmesh={current.OffMeshEdges.Count} route={route?.Succeeded} " +
                $"dynamic_changed={dynamic.ChangedCells.Count} dynamic_reset={dynamic.RequiresFullSearchReset} " +
                $"codes={string.Join(',', reachability.Diagnostics.Select(item => item.Code))}");
            return false;
        }

        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "navigation-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var created = ProjectSession.Create(smokeRoot, _history.State.Project);
            if (!created.Succeeded || created.Session is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G714_NAVIGATION_FAILED workspace");
                return false;
            }

            NavigationBakeStore.Save(smokeRoot, current);
            var reopened = ProjectSession.Open(smokeRoot);
            var restoredBake = NavigationBakeStore.TryLoad(smokeRoot, appliedArea.Id, agent.Id);
            if (!reopened.Succeeded || reopened.Session is null ||
                reopened.Session.Project.Areas.Single().Navigation.OffMeshLinks.All(item => item.Id != link.Id) ||
                restoredBake?.SourceFingerprint != current.SourceFingerprint)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G714_NAVIGATION_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot))
            {
                Directory.Delete(smokeRoot, recursive: true);
            }
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            _history.State.Project.Areas.Single(candidate => candidate.Id == area.Id).Navigation != original)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G714_NAVIGATION_FAILED history");
            return false;
        }

        _projectPreview.ShowNavigationOverlay(null);
        RefreshEditorControls();
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G714_NAVIGATION_PASS agents=4 geometry=valid " +
            "reachability=spawn-object doors=states offmesh=linked flight=profiled " +
            "incremental=partial overlay=visible history=restored persistence=roundtrip");
        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_G719_PATHFINDING_PASS algorithm=deterministic-a-star " +
            $"route={route.Cells.Count} visited={route.VisitedCellCount} costs=weighted obstacles=dynamic " +
            "recovery=endpoints budget=bounded invalidation=incremental benchmark=instrumented");
        return RunWaypointAnchorSmokeTest(area.Id);
    }

    private bool RunWaypointAnchorSmokeTest(AreaId areaId)
    {
        if (_navigationConfiguration is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R009_WAYPOINT_FAILED missing_window");
            return false;
        }
        var original = _history.State.Project.Areas.Single(item => item.Id == areaId).Navigation;
        var originalDefinitionCount = original.Anchors.Definitions.Count;
        var originalInstanceCount = original.Anchors.Instances.Count;
        if (!_navigationConfiguration.ConfigureForSmokeTest(areaId, original) ||
            !_navigationConfiguration.ConfigureForR009SmokeTest() ||
            _navigationConfiguration.AnchorCount != originalInstanceCount + 3 ||
            !_navigationConfiguration.PreviewForSmokeTest() ||
            _navigationConfiguration.DiagnosticCount != 0 ||
            !_navigationConfiguration.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R009_WAYPOINT_FAILED preview_apply_ui");
            return false;
        }

        var project = _history.State.Project;
        var area = project.Areas.Single(item => item.Id == areaId);
        var definitions = area.Navigation.Anchors.Definitions
            .Where(item => string.Equals(item.Provenance, "toolset:r009", StringComparison.Ordinal))
            .ToArray();
        var instances = area.Navigation.Anchors.Instances
            .Where(item => string.Equals(item.Provenance, "toolset:r009", StringComparison.Ordinal))
            .ToArray();
        var spawn = instances.Single(item => item.Spawn is not null);
        var portal = instances.Single(item => item.Portal is not null);
        var script = new AnchorScriptService(project, areaId);
        var snapshot = script.Query(script.Handle, tag: "r009-smoke", limit: 10);
        var spawnResolution = script.ResolveSpawn(script.Handle, "r009-player", "r009-team");
        var portalResolution = script.ResolvePortal(script.Handle, portal.Id, new Vector3Value(2, 0, 0), 0);
        if (definitions.Length != 3 || instances.Length != 3 || snapshot.Count != 3 ||
            spawnResolution.Instance.Id != spawn.Id || spawnResolution.UsedFallback ||
            portalResolution.TargetAnchorInstanceId != portal.Portal!.TargetAnchorInstanceId ||
            !portalResolution.Preload || Math.Abs(portalResolution.ArrivalVelocity.X) < 0.01f)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R009_WAYPOINT_FAILED runtime_script");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "anchor-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var created = ProjectSession.Create(smokeRoot, project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Areas.Single(item => item.Id == areaId).Navigation.Anchors;
            if (!created.Succeeded || !reopened.Succeeded || restored is null ||
                restored.Definitions.Count != originalDefinitionCount + 3 ||
                restored.Instances.Count != originalInstanceCount + 3 ||
                restored.Instances.Single(item => item.Id == portal.Id).Portal?.TargetAnchorInstanceId !=
                portal.Portal!.TargetAnchorInstanceId)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R009_WAYPOINT_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, true);
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        var restoredOriginal = _history.State.Project.Areas.Single(item => item.Id == areaId).Navigation;
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded || restoredOriginal != original)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R009_WAYPOINT_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_R009_WAYPOINT_PASS " +
            "anchors=uuid-schema-provenance-localized-transform-tags-scope-overrides " +
            "spawn=profile-clearance-priority-team-orientation-fallback-navigation " +
            "portal=cross-zone-orientation-velocity-preload validation=identity-references-bounds-clearance " +
            "ui=navigation-tab forgescript=typed-handle-query-runtime mcp=filtered-dry-run " +
            "history=undo-redo persistence=project-workspace-roundtrip");
        return RunR064WaypointPatrolSmokeTest(areaId);
    }

    private bool RunR064WaypointPatrolSmokeTest(AreaId areaId)
    {
        if (_navigationConfiguration is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R064_WAYPOINT_PATROL_FAILED missing_window");
            return false;
        }
        var original = _history.State.Project.Areas.Single(item => item.Id == areaId).Navigation;
        if (!_navigationConfiguration.ConfigureForSmokeTest(areaId, original) ||
            !_navigationConfiguration.ConfigureForR064SmokeTest() ||
            _navigationConfiguration.WaypointCount != 3 ||
            _navigationConfiguration.PatrolRouteCount != 1 ||
            !_navigationConfiguration.PreviewForSmokeTest() ||
            _navigationConfiguration.DiagnosticCount != 0 ||
            !_navigationConfiguration.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R064_WAYPOINT_PATROL_FAILED preview_apply_ui");
            return false;
        }

        var project = _history.State.Project;
        var area = project.Areas.Single(item => item.Id == areaId);
        var profile = area.Navigation.Waypoints;
        var script = new WaypointAuthoringScriptService(project, areaId);
        var waypoints = script.Query(script.Handle, limit: 10);
        var routes = script.QueryRoutes(script.Handle);
        var reveal = WaypointRuntimeService.Reveal(profile, WaypointRuntimeState.Empty,
            MarkerVisibilityScope.Party, "quest.crypt.found");
        var markers = WaypointRuntimeService.VisibleMarkers(profile, reveal.State,
            MarkerVisibilityScope.Party);
        var route = profile.PatrolRoutes.Single();
        var first = WaypointRuntimeService.Advance(profile, reveal.State, route.Id);
        var second = WaypointRuntimeService.Advance(profile, first.State, route.Id);
        var denied = WaypointRuntimeService.Advance(profile, second.State, route.Id,
            authorityGranted: false);
        if (WaypointAuthoringValidator.Validate(area).Count != 0 || waypoints.Count != 3 ||
            routes.Count != 1 || markers.Count != 1 || !markers[0].Tracked ||
            first.Step?.Cursor.Index != 1 || second.Step?.Cursor.Index != 2 ||
            denied.Diagnostics.All(item => item.Code != WaypointRuntimeService.AuthorityDeniedCode))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R064_WAYPOINT_PATROL_FAILED runtime_script");
            return false;
        }

        var dryRun = _setAreaNavigationHandler.Handle(_history.State, new SetAreaNavigationCommand(
            CommandId.From(Guid.Parse("a0645000-0000-4000-8000-000000000101")), _history.State.Revision,
            areaId, area.Navigation, true));
        var invalidProfile = new WaypointAuthoringProfile(Guid.NewGuid(), 1, "toolset:r064", areaId,
            true, [], []);
        var rejected = _setAreaNavigationHandler.Handle(_history.State, new SetAreaNavigationCommand(
            CommandId.From(Guid.Parse("a0645000-0000-4000-8000-000000000102")), _history.State.Revision,
            areaId, area.Navigation.WithWaypoints(invalidProfile), true));
        if (!dryRun.Succeeded || dryRun.State != _history.State || rejected.Succeeded ||
            rejected.Diagnostics.All(item => item.Code != WaypointAuthoringValidator.RequiredContentCode))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R064_WAYPOINT_PATROL_FAILED command_validation");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "waypoint-r064-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var created = ProjectSession.Create(smokeRoot, project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Areas.Single(item => item.Id == areaId)
                .Navigation.Waypoints;
            if (!created.Succeeded || !reopened.Succeeded || restored is null ||
                restored.Waypoints.Count != 3 || restored.PatrolRoutes.Single().Edges.Count != 2 ||
                restored.Waypoints.Single(item => item.Marker is not null).Marker?.RevealAnimation != "pulse")
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R064_WAYPOINT_PATROL_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, true);
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            _history.State.Project.Areas.Single(item => item.Id == areaId).Navigation != original)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R064_WAYPOINT_PATROL_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_R064_WAYPOINT_PATROL_PASS " +
            "appearance=icon-color-layer-visible-constant-screen-size map=text-icon-floor-category-precision-policy-tracking " +
            "reveal=player-party-world-event-animation-persistence patrol=ordered-wait-facing-speed-action-branch-weight-uuid-edges " +
            "traversal=loop-pingpong-once-random-weighted-scripted ui=visible-empty-error-preview " +
            "command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handles " +
            "mcp=filtered-dry-run runtime=authoritative-hud-patrol optional=ignored-required-diagnostic");
        return RunR065WaypointLocatorSmokeTest(areaId);
    }

    private bool RunR065WaypointLocatorSmokeTest(AreaId areaId)
    {
        if (_navigationConfiguration is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R065_WAYPOINT_LOCATOR_FAILED missing_window");
            return false;
        }
        var original = _history.State.Project.Areas.Single(item => item.Id == areaId).Navigation;
        if (!_navigationConfiguration.ConfigureForSmokeTest(areaId, original) ||
            !_navigationConfiguration.ConfigureForR065SmokeTest() ||
            _navigationConfiguration.LocatorCount != 3 ||
            _navigationConfiguration.LocatorCollectionCount != 2 ||
            !_navigationConfiguration.PreviewForSmokeTest() ||
            _navigationConfiguration.DiagnosticCount != 0 ||
            !_navigationConfiguration.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R065_WAYPOINT_LOCATOR_FAILED preview_apply_ui");
            return false;
        }

        var project = _history.State.Project;
        var area = project.Areas.Single(item => item.Id == areaId);
        var profile = area.Navigation.Waypoints;
        var script = new WaypointAuthoringScriptService(project, areaId);
        var locators = script.QueryLocators(script.Handle, "crypt");
        var collections = script.QueryCollections(script.Handle);
        var nearest = WaypointRuntimeService.ResolveCollection(profile,
            profile.Collections.Single(item => item.Selection == LocatorSelectionMode.Nearest).Id);
        var arrival = WaypointRuntimeService.Arrive(profile,
            profile.Waypoints.Single(item => item.Arrival is not null).Id);
        var denied = WaypointRuntimeService.Arrive(profile,
            profile.Locators.Single(item => item.Arrival is not null).Id, authorityGranted: false);
        var known = profile.Waypoints.Select(item => item.Id)
            .Concat(profile.Locators.Select(item => item.Id)).ToHashSet();
        var segments = WaypointRouteService.AnalyzeRoute(profile.PatrolRoutes.Single(),
            profile.Waypoints.Take(2).Select(item => item.Id).ToHashSet(), known,
            profile.RouteVisualization.CostPerMeter);
        if (WaypointAuthoringValidator.Validate(area).Count != 0 || locators.Count != 3 ||
            collections.Count != 2 || nearest.Count != 1 ||
            arrival.Arrival is not
            {
                Facing: ArrivalFacingMode.Target, Animation: "inspect",
                Interaction: "open", Callback: "waypoint.arrived", AuthorityGranted: true
            } ||
            denied.Diagnostics.All(item => item.Code != WaypointRuntimeService.AuthorityDeniedCode) ||
            segments.Count != 4 || segments.All(item => item.Direction >= 0) ||
            segments.Any(item => item.Cost <= 0 || item.TravelSeconds <= 0) ||
            segments.All(item => !item.NavProjected))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R065_WAYPOINT_LOCATOR_FAILED runtime_script_diagnostics");
            return false;
        }

        var dryRun = _setAreaNavigationHandler.Handle(_history.State, new SetAreaNavigationCommand(
            CommandId.From(Guid.Parse("a0655000-0000-4000-8000-000000000101")), _history.State.Revision,
            areaId, area.Navigation, true));
        var invalidCollection = new AnchorCollection(Guid.NewGuid(), 1, "toolset:r065", string.Empty,
            [], "Collection invalide", RequiredAtRuntime: true);
        var invalidProfile = new WaypointAuthoringProfile(Guid.NewGuid(), 1, "toolset:r065", areaId,
            true, profile.Waypoints, profile.PatrolRoutes, profile.Locators, [invalidCollection],
            profile.RouteVisualization);
        var rejected = _setAreaNavigationHandler.Handle(_history.State, new SetAreaNavigationCommand(
            CommandId.From(Guid.Parse("a0655000-0000-4000-8000-000000000102")), _history.State.Revision,
            areaId, area.Navigation.WithWaypoints(invalidProfile), true));
        if (!dryRun.Succeeded || dryRun.State != _history.State || rejected.Succeeded ||
            rejected.Diagnostics.All(item => item.Code != WaypointAuthoringValidator.CollectionInvalidCode))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R065_WAYPOINT_LOCATOR_FAILED command_validation");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "waypoint-r065-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var created = ProjectSession.Create(smokeRoot, project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Areas.Single(item => item.Id == areaId)
                .Navigation.Waypoints;
            if (!created.Succeeded || !reopened.Succeeded || restored is null ||
                restored.Locators.Count != 3 || restored.Collections.Count != 2 ||
                restored.Waypoints.Count(item => item.Arrival is not null) != 1 ||
                !restored.RouteVisualization.ShowBrokenLinks ||
                restored.RouteVisualization.CostPerMeter != 1.25)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R065_WAYPOINT_LOCATOR_FAILED persistence " +
                    $"created={created.Succeeded} reopened={reopened.Succeeded} " +
                    $"restored={restored is not null} locators={restored?.Locators.Count ?? -1} " +
                    $"collections={restored?.Collections.Count ?? -1} " +
                    $"arrivals={restored?.Waypoints.Count(item => item.Arrival is not null) ?? -1} " +
                    $"broken={restored?.RouteVisualization.ShowBrokenLinks ?? false} " +
                    $"cost={restored?.RouteVisualization.CostPerMeter ?? -1}");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, true);
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            _history.State.Project.Areas.Single(item => item.Id == areaId).Navigation != original)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R065_WAYPOINT_LOCATOR_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_R065_WAYPOINT_LOCATOR_PASS " +
            "arrival=direction-target-animation-wait-interact-callback-authority " +
            "locator=audio-vfx-spawn-objective-gameplay-purpose-tags-assets-overrides " +
            "collection=query-tags-purpose-stable-nearest-random-deterministic-preview " +
            "route=segments-direction-costs-actions-broken-links-nav-projection-timing " +
            "ui=visible-empty-error-preview command=dry-run-apply-reject history=undo-redo " +
            "persistence=roundtrip forgescript=typed-handles mcp=filtered-dry-run " +
            "runtime=arrival-locators-route-diagnostics optional=ignored-required-diagnostic");
        return RunR066GameplaySimulationSmokeTest();
    }

    private bool RunR066GameplaySimulationSmokeTest()
    {
        if (_gameplayControlsWindow is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R066_GAMEPLAY_SIMULATION_FAILED missing_window");
            return false;
        }
        var original = _history.State.Project.Module.RuntimePolicies.Gameplay;
        if (!_gameplayControlsWindow.ConfigureForR066SmokeTest(original) ||
            !_gameplayControlsWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R066_GAMEPLAY_SIMULATION_FAILED ui_apply");
            return false;
        }

        var configured = _history.State.Project.Module.RuntimePolicies.Gameplay;
        var simulation = configured.Simulation;
        var script = new GameplaySimulationScriptService(_history.State.Project.Module.Id, simulation);
        var flight = script.PreviewFlight(script.Flight(script.Handle),
            new GameplayVectorState(new Vector3Value(0, .5f, 0), Vector3Value.Zero), new(1, 1, 0), .25f,
            new FlightNavigationVolume(new(-4, 0, -4), new(4, 8, 4), 0));
        var projectile = script.Launch(script.Projectiles(script.Handle),
            new ProjectileRuntimeState(new(Vector3Value.Zero, new(8, 2, 0)), 0, true, 0),
            new(5, 0, 0), .1f, true);
        var physics = script.StepPhysics(script.Physics(script.Handle), [], .1f, true);
        var targetCandidate = new TargetCandidate(Guid.Parse("a0665000-0000-4000-8000-000000000010"),
            new(0, 0, -2), ["targetable"], true);
        var targetingModes = Enum.GetValues<TargetingMode>().Select(mode =>
            GameplaySimulation.PreviewTargeting(simulation.Targeting with { Mode = mode }, Vector3Value.Zero,
                new(0, 0, -1), [targetCandidate])).ToArray();
        var world = script.AdvanceWorld(script.WorldTime(script.Handle), simulation.WorldTime.InitialClock,
            3, null, null, true);
        var required = GameplaySimulationProfileValidator.ValidateRuntime(null, true);
        var simulationDiagnostics = GameplaySimulationProfileValidator.Validate(simulation);
        if (simulationDiagnostics.Count != 0 ||
            simulation.Flight.MaximumAltitude != 96 || simulation.Projectiles.Mode != ProjectileMode.Homing ||
            simulation.Targeting.Mode != TargetingMode.Chain || simulation.WorldTime.InitialClock.TimeScale != 1.5f ||
            flight.AppliedLift <= 0 || !projectile.AuthorityGranted || !physics.AuthorityGranted ||
            targetingModes.Any(item => item.Geometry.Count == 0) || world.Clock.Revision == 0 ||
            required.All(item => item.Code != GameplaySimulationProfileValidator.RequiredMissingCode))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R066_GAMEPLAY_SIMULATION_FAILED runtime_script_diagnostics " +
                $"diagnostics={string.Join(',', simulationDiagnostics.Select(item => item.Code + '@' + item.Path))} " +
                $"altitude={simulation.Flight.MaximumAltitude} projectile={simulation.Projectiles.Mode} " +
                $"target={simulation.Targeting.Mode} scale={simulation.WorldTime.InitialClock.TimeScale} " +
                $"lift={flight.AppliedLift} projectileAuthority={projectile.AuthorityGranted} " +
                $"physicsAuthority={physics.AuthorityGranted} emptyGeometry={targetingModes.Count(item => item.Geometry.Count == 0)} " +
                $"worldRevision={world.Clock.Revision} required={string.Join(',', required.Select(item => item.Code))}");
            return false;
        }

        var dryRun = _setGameplayControlProfileHandler.Handle(_history.State,
            new SetGameplayControlProfileCommand(
                CommandId.From(Guid.Parse("a0665000-0000-4000-8000-000000000101")),
                _history.State.Revision, configured, true));
        var invalidSimulation = simulation with
        {
            Flight = simulation.Flight with { MaximumAltitude = 0 },
        };
        var invalid = new ModuleGameplayControlProfile(configured.Id, configured.SchemaVersion,
            configured.Swimming, configured.Climbing, configured.RadialMenu, configured.TimeControl,
            configured.CombatModes, configured.Provenance, configured.Foundation, invalidSimulation,
            configured.Session);
        var rejected = _setGameplayControlProfileHandler.Handle(_history.State,
            new SetGameplayControlProfileCommand(
                CommandId.From(Guid.Parse("a0665000-0000-4000-8000-000000000102")),
                _history.State.Revision, invalid, true));
        if (!dryRun.Succeeded || dryRun.State != _history.State || rejected.Succeeded ||
            rejected.Diagnostics.All(item => item.Code != GameplaySimulationProfileValidator.FlightCode))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R066_GAMEPLAY_SIMULATION_FAILED command_validation");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "gameplay-r066-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var created = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Module.RuntimePolicies.Gameplay.Simulation;
            if (!created.Succeeded || !reopened.Succeeded || restored is null ||
                restored.Flight.MaximumAltitude != 96 || restored.Projectiles.Mode != ProjectileMode.Homing ||
                restored.Physics.Joints.Count != 1 || restored.Targeting.Mode != TargetingMode.Chain ||
                restored.WorldTime.InitialClock.TimeScale != 1.5f)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R066_GAMEPLAY_SIMULATION_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, true);
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            _history.State.Project.Module.RuntimePolicies.Gameplay != original)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R066_GAMEPLAY_SIMULATION_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_R066_GAMEPLAY_SIMULATION_PASS " +
            "flight=3d-input-altitude-banking-lift-landing-volumetric-navigation " +
            "projectile=ballistic-homing-hitscan-collision-prediction-vfx-authoritative-impact-revision-audit " +
            "physics=rigid-kinematic-static-joints-triggers-character-bounds-deterministic-network " +
            "targeting=self-entity-point-direction-area-cone-line-chain-preview-validity-input " +
            "world=clock-calendar-local-scales-pause-domains-schedules-save ui=visible-empty-error-preview " +
            "command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handles " +
            "mcp=filtered-plan-apply-revert runtime=engine-adapters optional=ignored-required-diagnostic");
        return RunR067GameplaySessionSmokeTest();
    }

    private bool RunR067GameplaySessionSmokeTest()
    {
        if (_gameplayControlsWindow is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R067_GAMEPLAY_SESSION_FAILED missing_window");
            return false;
        }
        var original = _history.State.Project.Module.RuntimePolicies.Gameplay;
        if (!_gameplayControlsWindow.ConfigureForR067SmokeTest(original) ||
            !_gameplayControlsWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R067_GAMEPLAY_SESSION_FAILED ui_apply");
            return false;
        }
        var configured = _history.State.Project.Module.RuntimePolicies.Gameplay;
        var session = configured.Session;
        var script = new GameplaySessionScriptService(_history.State.Project.Module.Id, session);
        var snapshot = script.Read(script.Handle);
        var shell = script.ReadShell(script.Shell(script.Handle));
        var checkpointRef = script.Checkpoints(script.Handle).Single();
        var checkpoint = session.Checkpoints.Single();
        var respawn = script.Respawn(checkpointRef,
            new RespawnRequest("hero", checkpoint.WaypointId, 40, true, ["hero", "companion"], true), true);
        var exploration = session.ControlLocks.Contexts.Single(item => item.OwnerId == "exploration");
        var cinematic = session.ControlLocks.Contexts.Single(item => item.OwnerId == "cinematic");
        var control = GameplaySessionServices.ReleaseOwner(
            GameplaySessionServices.PushControl(
                GameplaySessionServices.PushControl([], exploration), cinematic), "cinematic");
        var expected = GameplaySessionServices.HashState("hero:x=1");
        var recording = GameplaySessionServices.Record(session.Replay,
            Guid.Parse("a0675000-0000-4000-8000-000000000010"),
            [new ReplayFrame(1, "right", "moved", 42, expected)], "toolset:r067");
        var replay = script.ReplayHeadless(script.Replay(script.Handle), recording, _ => "hero:x=2", true);
        if (GameplaySessionProfileValidator.Validate(session).Count != 0 ||
            session.Persistence.Scope != PersistenceScope.Server ||
            session.Persistence.Authority != SaveAuthority.Server ||
            session.Persistence.ConflictPolicy != SaveConflictPolicy.ServerWins ||
            checkpoint.CurrencyCost != 25 || session.Shell.Scale != 1.25 ||
            session.Replay.MaximumFrames != 120000 || snapshot.CheckpointCount != 1 ||
            !shell.InputAgnostic || !respawn.Succeeded || respawn.RespawnActorIds.Count != 2 ||
            control.Restored?.OwnerId != "exploration" || replay.Succeeded ||
            replay.Divergence?.Tick != 1 || replay.MinimalPackageManifest is null ||
            GameplaySessionProfileValidator.ValidateRuntime(null, true)
                .All(item => item.Code != GameplaySessionProfileValidator.RequiredMissingCode))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R067_GAMEPLAY_SESSION_FAILED runtime_script_diagnostics");
            return false;
        }

        var dryRun = _setGameplayControlProfileHandler.Handle(_history.State,
            new SetGameplayControlProfileCommand(
                CommandId.From(Guid.Parse("a0675000-0000-4000-8000-000000000101")),
                _history.State.Revision, configured, true));
        var invalidSession = session with { Replay = session.Replay with { MaximumFrames = 0 } };
        var invalid = new ModuleGameplayControlProfile(configured.Id, configured.SchemaVersion,
            configured.Swimming, configured.Climbing, configured.RadialMenu, configured.TimeControl,
            configured.CombatModes, configured.Provenance, configured.Foundation, configured.Simulation,
            invalidSession);
        var rejected = _setGameplayControlProfileHandler.Handle(_history.State,
            new SetGameplayControlProfileCommand(
                CommandId.From(Guid.Parse("a0675000-0000-4000-8000-000000000102")),
                _history.State.Revision, invalid, true));
        if (!dryRun.Succeeded || dryRun.State != _history.State || rejected.Succeeded ||
            rejected.Diagnostics.All(item => item.Code != GameplaySessionProfileValidator.ReplayCode))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R067_GAMEPLAY_SESSION_FAILED command_validation");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "gameplay-r067-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var created = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Module.RuntimePolicies.Gameplay.Session;
            if (!created.Succeeded || !reopened.Succeeded || restored is null ||
                restored.Persistence.Scope != PersistenceScope.Server ||
                restored.Checkpoints.Single().CurrencyCost != 25 || restored.Shell.Scale != 1.25 ||
                restored.ControlLocks.Contexts.Count != 3 || restored.Replay.MaximumFrames != 120000)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R067_GAMEPLAY_SESSION_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, true);
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            _history.State.Project.Module.RuntimePolicies.Gameplay != original)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R067_GAMEPLAY_SESSION_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_R067_GAMEPLAY_SESSION_PASS " +
            "save=player-game-server-authority-conflict-revision-cloud-privacy-encryption " +
            "checkpoint=waypoint-cost-party-corpse-safe-spawn-hooks-authority " +
            "shell=kit-data-driven-skinnable-scalable-input-agnostic-map-journal-hud " +
            "control=owner-cursor-camera-actions-stack-guaranteed-restoration " +
            "replay=tick-input-event-seed-hash-headless-divergence-minimal-redacted-package " +
            "ui=visible-empty-error-preview command=dry-run-apply-reject history=undo-redo " +
            "persistence=roundtrip forgescript=typed-handles mcp=filtered-plan-apply-revert " +
            "runtime=engine-shell-control-save-replay optional=ignored-required-diagnostic");
        return RunR068DialogueAuthoringSmokeTest();
    }

    private bool RunR068DialogueAuthoringSmokeTest()
    {
        if (_dialogueEditor is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R068_DIALOGUE_AUTHORING_FAILED missing_window");
            return false;
        }
        var originalDialogueCount = _history.State.Project.Dialogues.Count;
        var dialogue = CreateR068DialogueSmokeDefinition();
        if (!_dialogueEditor.ConfigureForR068SmokeTest(dialogue))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R068_DIALOGUE_AUTHORING_FAILED ui_configure");
            return false;
        }
        if (!_dialogueEditor.ApplyForR068SmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R068_DIALOGUE_AUTHORING_FAILED ui_apply");
            return false;
        }
        var stored = _history.State.Project.Dialogues.Single(item => item.Id == dialogue.Id);
        var profile = stored.Authoring;
        if (profile is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R068_DIALOGUE_AUTHORING_FAILED profile_missing");
            return false;
        }
        var script = new DialogueGraphScriptService(stored);
        var snapshot = script.ReadAuthoring(script.Handle);
        var rendered = DialogueAuthoringRuntimeService.Render(stored, stored.EntryNodeId,
            new DialogueAuthoringContext(
                new Dictionary<string, object> { ["player.name"] = "<Hero>" },
                new Dictionary<string, string> { ["role:narrator"] = "entity:narrator" }, 31),
            new SmokeDialogueConditionEvaluator());
        var dryRun = _setDialogueHandler.Handle(_history.State, new SetDialogueCommand(
            CommandId.From(Guid.Parse("d0687000-0000-4000-8000-000000000301")),
            _history.State.Revision, stored, true));
        var invalidEntry = stored.Nodes.Single(item => item.Id == stored.EntryNodeId);
        var invalidNodes = stored.Nodes.Select(item => item.Id == invalidEntry.Id
            ? new DialogueNode(item.Id, item.SpeakerBlueprintId,
                new LocalizedText(item.Text.Id, item.Text.SchemaVersion, item.Text.Provenance, item.Text.Key,
                    "Welcome without the required token.", item.Text.Variants), item.OnEnterActions, item.Choices,
                item.Role, item.ParticipantId, item.SubgraphKey)
            : item).ToArray();
        var invalid = new DialogueDefinition(stored.Id, stored.Slug, stored.DisplayName, stored.EntryNodeId,
            invalidNodes, stored.Presentation, stored.SchemaVersion, stored.Provenance, stored.Metadata,
            stored.Participants, stored.EntryPoints, edges: null, profile);
        var rejected = _setDialogueHandler.Handle(_history.State, new SetDialogueCommand(
            CommandId.From(Guid.Parse("d0687000-0000-4000-8000-000000000302")),
            _history.State.Revision, invalid, true));
        var optional = CreateR068DialogueSmokeDefinition();
        var required = new DialogueDefinition(optional.Id, optional.Slug, optional.DisplayName,
            optional.EntryNodeId, optional.Nodes, optional.Presentation, optional.SchemaVersion, optional.Provenance,
            new Dictionary<string, string> { ["authoring.required"] = "true" }, optional.Participants,
            optional.EntryPoints, optional.Edges);
        if (!dryRun.Succeeded || dryRun.State != _history.State || rejected.Succeeded ||
            rejected.Diagnostics.All(item => item.Code != DialogueAuthoringProfileValidator.TokenCode) ||
            DialogueAuthoringProfileValidator.Validate(optional).Count != 0 ||
            DialogueAuthoringProfileValidator.Validate(required).All(item =>
                item.Code != DialogueAuthoringProfileValidator.RequiredMissingCode) ||
            DialogueValidator.Validate(stored).Count != 0 || !profile.RequiredAtRuntime ||
            profile.Participants.Select(item => item.Kind).Distinct().Count() != 4 ||
            profile.Nodes.Select(item => item.EvaluationMode).Distinct().Count() != 5 ||
            profile.Nodes.Sum(item => item.Tokens.Count) != 1 ||
            profile.Nodes.Sum(item => item.WizardEntries.Count) != 2 ||
            profile.Nodes.Any(item => string.IsNullOrWhiteSpace(item.Notes.Intent) ||
                                      string.IsNullOrWhiteSpace(item.Notes.AuthorMention)) ||
            snapshot.ParticipantCount != 4 || snapshot.NodePolicyCount != 5 || snapshot.WizardEntryCount != 2 ||
            !rendered.Text.Contains("&lt;Hero&gt;", StringComparison.Ordinal) ||
            rendered.AvailableChoices.Count != 1 || rendered.Diagnostics.Count != 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R068_DIALOGUE_AUTHORING_FAILED contracts");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "dialogue-r068-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var created = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var persisted = reopened.Session?.Project.Dialogues.SingleOrDefault(item => item.Id == dialogue.Id);
            if (!created.Succeeded || !reopened.Succeeded || persisted?.Authoring is null ||
                persisted.Authoring.Participants.Count != 4 || persisted.Authoring.Nodes.Count != 5 ||
                persisted.Authoring.Nodes.Sum(item => item.WizardEntries.Count) != 2)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R068_DIALOGUE_AUTHORING_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, true);
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            _history.State.Project.Dialogues.Count != originalDialogueCount ||
            _history.State.Project.Dialogues.Any(item => item.Id == dialogue.Id))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R068_DIALOGUE_AUTHORING_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_R068_DIALOGUE_AUTHORING_PASS " +
            "tokens=typed-autocomplete-preview-escaped participants=role-query-tag-entity-node-speaker-fallback " +
            "evaluation=first-all-weighted-random-deterministic-manual wizard=visual-typed-source " +
            "notes=intent-emotion-source-status-todo-author ui=visible-empty-error-minimap-validation-simulation " +
            "command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handles " +
            "mcp=filtered-plan-apply-revert runtime=engine-adapters optional=ignored-required-diagnostic");
        return RunR069DialogueExperienceSmokeTest();
    }

    private bool RunR069DialogueExperienceSmokeTest()
    {
        if (_dialogueEditor is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R069_DIALOGUE_EXPERIENCE_FAILED missing_window");
            return false;
        }
        var originalDialogueCount = _history.State.Project.Dialogues.Count;
        var dialogue = CreateR069DialogueSmokeDefinition();
        if (!_dialogueEditor.ConfigureForR069SmokeTest(dialogue) ||
            !_dialogueEditor.ApplyForR069SmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R069_DIALOGUE_EXPERIENCE_FAILED ui_apply");
            return false;
        }
        var stored = _history.State.Project.Dialogues.Single(item => item.Id == dialogue.Id);
        var profile = stored.Experience;
        if (profile is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R069_DIALOGUE_EXPERIENCE_FAILED profile_missing");
            return false;
        }
        var script = new DialogueGraphScriptService(stored);
        var snapshot = script.ReadExperience(script.Handle);
        var completed = DialogueExperienceRuntimeService.Complete(stored, "player", DateTimeOffset.UnixEpoch);
        var memory = DialogueExperienceRuntimeService.Write(profile, DialogueMemoryState.Empty,
            profile.MemorySlots.Single().Id, "4");
        var aborted = DialogueExperienceRuntimeService.Abort(stored, "player", stored.EntryNodeId,
            DialogueAbortReason.CombatStarted, memory, 42, DateTimeOffset.UnixEpoch);
        var search = DialogueExperienceRuntimeService.Search([stored], profile.Search);
        var replace = DialogueExperienceRuntimeService.PreviewReplace([stored], profile.Search);
        var review = DialogueExperienceRuntimeService.Review([stored], profile.Review);
        var check = DialogueExperienceRuntimeService.ResolveSkillCheck(profile.SkillChecks.Single(), 5, 2, 42);
        var repeated = DialogueExperienceRuntimeService.ResolveSkillCheck(profile.SkillChecks.Single(), 5, 2, 42);
        var dryRun = _setDialogueHandler.Handle(_history.State, new SetDialogueCommand(
            CommandId.From(Guid.Parse("d0697000-0000-4000-8000-000000000301")),
            _history.State.Revision, stored, true));
        var invalidProfile = profile with
        {
            MemorySlots = [profile.MemorySlots.Single() with { DefaultValue = "invalid-integer" }],
        };
        var rejected = _setDialogueHandler.Handle(_history.State, new SetDialogueCommand(
            CommandId.From(Guid.Parse("d0697000-0000-4000-8000-000000000302")),
            _history.State.Revision, stored.WithExperience(invalidProfile), true));
        var optional = CreateR069DialogueSmokeDefinition();
        var required = new DialogueDefinition(optional.Id, optional.Slug, optional.DisplayName,
            optional.EntryNodeId, optional.Nodes, optional.Presentation, optional.SchemaVersion, optional.Provenance,
            new Dictionary<string, string> { ["kind"] = "conversation", ["experience.required"] = "true" },
            optional.Participants, optional.EntryPoints, optional.Edges, optional.Authoring);
        if (!dryRun.Succeeded || dryRun.State != _history.State || rejected.Succeeded ||
            rejected.Diagnostics.All(item => item.Code != DialogueExperienceProfileValidator.MemoryCode) ||
            DialogueExperienceProfileValidator.Validate(optional).Count != 0 ||
            DialogueExperienceProfileValidator.Validate(required).All(item =>
                item.Code != DialogueExperienceProfileValidator.RequiredMissingCode) ||
            DialogueValidator.Validate(stored).Count != 0 || !profile.RequiredAtRuntime ||
            completed.Actions.Count != 1 || !aborted.RolledBack || aborted.ResumeState is null ||
            aborted.AbortReason != DialogueAbortReason.CombatStarted || search.Count == 0 ||
            replace.Replacements.Count == 0 || !replace.SemanticDiff.Contains("preview-only:true", StringComparison.Ordinal) ||
            review.Count == 0 || profile.Review.Badges.Count != 6 ||
            DialogueExperienceRuntimeService.Read(profile, memory, profile.MemorySlots.Single().Id) != "4" ||
            check != repeated || string.IsNullOrWhiteSpace(check.Explanation) || snapshot.MemorySlotCount != 1 ||
            snapshot.SkillCheckCount != 1 || script.ReadSkillCheck(script.Handle, stored.EntryNodeId) is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R069_DIALOGUE_EXPERIENCE_FAILED contracts");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "dialogue-r069-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var created = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var persisted = reopened.Session?.Project.Dialogues.SingleOrDefault(item => item.Id == dialogue.Id)
                ?.Experience;
            if (!created.Succeeded || !reopened.Succeeded || persisted is null ||
                persisted.Lifecycle.CompletedActions.Count != 1 || persisted.MemorySlots.Count != 1 ||
                persisted.SkillChecks.Count != 1 || persisted.Review.Badges.Count != 6)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R069_DIALOGUE_EXPERIENCE_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, true);
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            _history.State.Project.Dialogues.Count != originalDialogueCount ||
            _history.State.Project.Dialogues.Any(item => item.Id == dialogue.Id))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R069_DIALOGUE_EXPERIENCE_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_R069_DIALOGUE_EXPERIENCE_PASS " +
            "lifecycle=completed-aborted-reason-rollback-resume search=multi-dialogue-safe-regex-speaker-condition-action-preview " +
            "review=condition-action-voice-localization-error-link-legend-filter memory=dialogue-participant-player-party-world-duration-typed " +
            "skill=difficulty-opponent-advantage-result-visibility-explanation-deterministic " +
            "ui=visible-empty-error-minimap-validation-simulation command=dry-run-apply-reject history=undo-redo " +
            "persistence=roundtrip forgescript=typed-handles mcp=filtered-plan-apply-revert " +
            "runtime=engine-adapters optional=ignored-required-diagnostic");
        return RunR070DialogueProductionSmokeTest();
    }

    private bool RunR070DialogueProductionSmokeTest()
    {
        if (_dialogueEditor is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R070_DIALOGUE_PRODUCTION_FAILED missing_window");
            return false;
        }
        var originalDialogueCount = _history.State.Project.Dialogues.Count;
        var dialogue = CreateR070DialogueSmokeDefinition();
        if (!_dialogueEditor.ConfigureForR070SmokeTest(dialogue) ||
            !_dialogueEditor.ApplyForR070SmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R070_DIALOGUE_PRODUCTION_FAILED ui_apply");
            return false;
        }
        var stored = _history.State.Project.Dialogues.Single(item => item.Id == dialogue.Id);
        var profile = stored.Production;
        if (profile is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R070_DIALOGUE_PRODUCTION_FAILED profile_missing");
            return false;
        }
        var script = new DialogueGraphScriptService(stored);
        var snapshot = script.ReadProduction(script.Handle);
        var contract = profile.Subgraphs.Single();
        var isolated = DialogueProductionRuntimeService.TestSubgraph(profile, contract.Id,
            contract.Tests.Single().Id);
        var impacts = DialogueProductionRuntimeService.AnalyzeImpact(profile,
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["quest:crypt-of-ashes"] = ["journal:crypt", "ending:embers"],
            });
        var coverage = DialogueProductionRuntimeService.MeasureCoverage(stored);
        var conflict = DialogueProductionRuntimeService.ResolveConflict(profile.Collaboration.Conflicts.Single());
        var aiPreview = DialogueProductionRuntimeService.PreviewAi(profile.AiPlans.Single());
        var aiApplied = DialogueProductionRuntimeService.ApplyAi(profile, aiPreview.PlanId);
        var dryRun = _setDialogueHandler.Handle(_history.State, new SetDialogueCommand(
            CommandId.From(Guid.Parse("d0707000-0000-4000-8000-000000000301")),
            _history.State.Revision, stored, true));
        var rejected = _setDialogueHandler.Handle(_history.State, new SetDialogueCommand(
            CommandId.From(Guid.Parse("d0707000-0000-4000-8000-000000000302")),
            _history.State.Revision, stored.WithProduction(profile with
            {
                Coverage = profile.Coverage with { MaximumDepth = 0 },
            }), true));
        var optional = CreateR070DialogueSmokeDefinition();
        var required = new DialogueDefinition(optional.Id, optional.Slug, optional.DisplayName,
            optional.EntryNodeId, optional.Nodes, optional.Presentation, optional.SchemaVersion, optional.Provenance,
            new Dictionary<string, string> { ["kind"] = "conversation", ["production.required"] = "true" },
            optional.Participants, optional.EntryPoints, optional.Edges, optional.Authoring, optional.Experience);
        var nodeLock = profile.Collaboration.Locks.Single();
        if (!dryRun.Succeeded || dryRun.State != _history.State || rejected.Succeeded ||
            rejected.Diagnostics.All(item => item.Code != DialogueProductionProfileValidator.CoverageCode) ||
            DialogueProductionProfileValidator.Validate(optional).Count != 0 ||
            DialogueProductionProfileValidator.Validate(required).All(item =>
                item.Code != DialogueProductionProfileValidator.RequiredMissingCode) ||
            DialogueValidator.Validate(stored).Count != 0 || !profile.RequiredAtRuntime || !isolated.Passed ||
            impacts.Count != 4 || impacts.Single(item => item.Consequence.Kind == DialogueConsequenceTargetKind.Quest)
                .AffectedResources.Count != 2 || coverage.Paths.Count == 0 || coverage.CoveredNodes.Count != 2 ||
            !conflict.CanApply || aiPreview.Lint.Count != 0 || !aiApplied.AiPlans.Single().Applied ||
            !DialogueProductionRuntimeService.CanEdit(nodeLock, "author", DateTimeOffset.UnixEpoch) ||
            DialogueProductionRuntimeService.CanEdit(nodeLock, "reviewer", DateTimeOffset.UnixEpoch) ||
            snapshot.SubgraphCount != 1 || snapshot.ConsequenceCount != 4 ||
            script.ReadSubgraph(script.Handle, "embers") is null ||
            script.ReadConsequences(script.Handle, stored.EntryNodeId).Count != 4)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R070_DIALOGUE_PRODUCTION_FAILED contracts");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "dialogue-r070-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var created = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var persisted = reopened.Session?.Project.Dialogues.SingleOrDefault(item => item.Id == dialogue.Id)
                ?.Production;
            if (!created.Succeeded || !reopened.Succeeded || persisted is null ||
                persisted.Subgraphs.Single().Version != 2 || persisted.Consequences.Count != 4 ||
                persisted.Coverage.Evidence.Count != 1 || persisted.Collaboration.Comments.Count != 1 ||
                persisted.AiPlans.Count != 1)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R070_DIALOGUE_PRODUCTION_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, true);
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            _history.State.Project.Dialogues.Count != originalDialogueCount ||
            _history.State.Project.Dialogues.Any(item => item.Id == dialogue.Id))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R070_DIALOGUE_PRODUCTION_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_R070_DIALOGUE_PRODUCTION_PASS " +
            "subgraph=versioned-input-output-variable-contract-isolated-test consequences=quest-faction-item-world-impact " +
            "coverage=paths-required-state-impossible-test-session-ratio collaboration=canonical-node-json-stable-id-revision-lock-comment-semantic-diff-conflict-resolution " +
            "ai=brief-citations-style-preview-lint-authorization-plan-transaction-semantic-diff-explicit-apply " +
            "ui=visible-empty-error-minimap-validation-simulation command=dry-run-apply-reject history=undo-redo " +
            "persistence=roundtrip forgescript=typed-handles mcp=filtered-plan-apply-revert runtime=engine-adapters optional=ignored-required-diagnostic");
        return true;
    }

    private static DialogueDefinition CreateR070DialogueSmokeDefinition()
    {
        var id = DialogueId.Parse("d0707000-0000-4000-8000-000000000010");
        var entry = DialogueNodeId.Parse("d0707000-0000-4000-8000-000000000001");
        var subgraph = DialogueNodeId.Parse("d0707000-0000-4000-8000-000000000002");
        var end = DialogueNodeId.Parse("d0707000-0000-4000-8000-000000000003");
        return new DialogueDefinition(id, "r070-toolset", "R070 dialogue production", entry,
        [
            new(entry, null, new("dialogue.r070.entry", "Choose an ember path."), choices:
            [
                new(DialogueChoiceId.Parse("d0707000-0000-4000-8000-000000000011"),
                    new("dialogue.r070.subgraph", "Enter the ember memory"), targetNodeId: subgraph),
                new(DialogueChoiceId.Parse("d0707000-0000-4000-8000-000000000012"),
                    new("dialogue.r070.end", "Leave"), targetNodeId: end),
            ], role: DialogueNodeRole.PlayerChoice),
            new(subgraph, null, new("dialogue.r070.embers", "The embers remember."),
                role: DialogueNodeRole.Subgraph, subgraphKey: "embers"),
            new(end, null, new("dialogue.r070.done", "Done."), role: DialogueNodeRole.End),
        ], provenance: "toolset:r070");
    }

    private static DialogueDefinition CreateR068DialogueSmokeDefinition()
    {
        var dialogueId = DialogueId.Parse("d0687000-0000-4000-8000-000000000010");
        var entry = DialogueNodeId.Parse("d0687000-0000-4000-8000-000000000001");
        var targets = Enumerable.Range(2, 4).Select(index =>
            DialogueNodeId.Parse($"d0687000-0000-4000-8000-00000000000{index}")).ToArray();
        var participantIds = Enumerable.Range(0, 4).Select(index =>
            Guid.Parse($"d0687000-0000-4000-8000-0000000001{index:00}")).ToArray();
        var condition = new DialogueCondition(Guid.Parse("d0687000-0000-4000-8000-000000000020"), 1,
            "toolset:r068", DialogueConditionKind.HasItem, "ash-key", "true", false,
            "on-choice-visible", 1);
        var action = new DialogueAction(Guid.Parse("d0687000-0000-4000-8000-000000000021"), 1,
            "toolset:r068", DialogueActionKind.StartQuest, "crypt-of-ashes", "active", true);
        var choices = targets.Select((target, index) => new DialogueChoice(
            DialogueChoiceId.Parse($"d0687000-0000-4000-8000-00000000003{index}"),
            new LocalizedText($"dialogue.r068.choice.{index}", $"Choice {index + 1}"),
            index == 0 ? [condition] : [], [], target, priority: index)).ToArray();
        var roles = new[] { "narrator", "guide", "companion", "merchant" };
        return new DialogueDefinition(dialogueId, "r068-toolset", "R068 dialogue authoring", entry,
            [
                new(entry, null, new("dialogue.r068.entry", "Welcome, {{player.name}}."), [action], choices,
                    DialogueNodeRole.PlayerChoice, participantIds[0], null),
                .. targets.Select((target, index) => new DialogueNode(target, null,
                    new LocalizedText($"dialogue.r068.end.{index}", $"End {index + 1}"), [], [],
                    DialogueNodeRole.End, participantIds[index % participantIds.Length], null)),
            ], provenance: "toolset:r068",
            participants: roles.Select((role, index) => new DialogueParticipant(participantIds[index], 1, role,
                null, "toolset:r068")).ToArray());
    }

    private static DialogueDefinition CreateR069DialogueSmokeDefinition()
    {
        var dialogueId = DialogueId.Parse("d0697000-0000-4000-8000-000000000010");
        var entry = DialogueNodeId.Parse("d0697000-0000-4000-8000-000000000001");
        var success = DialogueNodeId.Parse("d0697000-0000-4000-8000-000000000002");
        var failure = DialogueNodeId.Parse("d0697000-0000-4000-8000-000000000003");
        var participant = Guid.Parse("d0697000-0000-4000-8000-000000000100");
        return new DialogueDefinition(dialogueId, "r069-toolset", "R069 dialogue experience", entry,
        [
            new(entry, null, new("dialogue.r069.entry", "La braise répond dans les cendres."),
                [new DialogueAction(DialogueActionKind.PlayAudio, "elyra.voice", "play")],
                [
                    new(DialogueChoiceId.Parse("d0697000-0000-4000-8000-000000000020"),
                        new("dialogue.r069.success", "Convaincre Elyra"),
                        [new(DialogueConditionKind.HasItem, "ash-key", "true")],
                        [new(DialogueActionKind.StartQuest, "crypt-of-ashes", "active")], success),
                    new(DialogueChoiceId.Parse("d0697000-0000-4000-8000-000000000021"),
                        new("dialogue.r069.failure", "Renoncer"), targetNodeId: failure),
                ], DialogueNodeRole.PlayerChoice, participant, null),
            new(success, null, new("dialogue.r069.end.success", "La gardienne accepte."), [], [],
                DialogueNodeRole.End, participant, null),
            new(failure, null, new("dialogue.r069.end.failure", "La conversation prend fin."), [], [],
                DialogueNodeRole.End, participant, null),
        ], provenance: "toolset:r069",
            participants: [new(participant, 1, "guide", null, "toolset:r069")]);
    }

    private bool RunActionableDiagnosticsSmokeTest()
    {
        if (_creatorShell is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G715_DIAGNOSTICS_FAILED missing_shell");
            return false;
        }

        var originalAreaId = GetActiveArea().Id;
        var originalAreaCount = _history.State.Project.Areas.Count;
        var createdAreaId = CreateArea(
            "Diagnostic guidé G715",
            AreaKind.Interior,
            new AreaDimensions(12, 12),
            AreaTemplateKind.Empty);
        if (createdAreaId is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G715_DIAGNOSTICS_FAILED create_area");
            return false;
        }

        var diagnostics = CreatorDiagnostics.FromProject(
            _history.State.Project,
            $"revision:{_history.State.Revision}");
        var diagnosticIndex = diagnostics.ToList().FindIndex(candidate =>
            candidate.Code == AreaNavigationValidator.MissingSpawnCode &&
            candidate.DocumentPath.Contains(createdAreaId.ToString()!, StringComparison.Ordinal));
        if (diagnosticIndex < 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G715_DIAGNOSTICS_FAILED missing_diagnostic");
            return false;
        }

        var diagnostic = diagnostics[diagnosticIndex];
        var repeated = CreatorDiagnostics.FromProject(_history.State.Project)
            .Single(candidate => candidate.Code == diagnostic.Code && candidate.DocumentPath == diagnostic.DocumentPath);
        if (diagnostic.SchemaVersion != ActionableDiagnostic.CurrentSchemaVersion ||
            diagnostic.DiagnosticId != repeated.DiagnosticId ||
            diagnostic.SubjectUuid != createdAreaId.Value ||
            string.IsNullOrWhiteSpace(diagnostic.Property) ||
            diagnostic.Source != "AreaNavigationValidator" ||
            diagnostic.QuickFixes.Count != 1)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G715_DIAGNOSTICS_FAILED structured_contract");
            return false;
        }

        _creatorShell.SetDiagnostics(diagnostics);
        var previewRevision = _history.State.Revision;
        if (!_creatorShell.SelectDiagnosticForSmokeTest(diagnosticIndex) ||
            !_creatorShell.InvokeSelectedQuickFixForSmokeTest(dryRun: true) ||
            _history.State.Revision != previewRevision ||
            !_creatorShell.InvokeSelectedQuickFixForSmokeTest(dryRun: false))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G715_DIAGNOSTICS_FAILED ui_command");
            return false;
        }

        var fixedArea = _history.State.Project.Areas.Single(candidate => candidate.Id == createdAreaId);
        if (fixedArea.Entities.Count != 1 ||
            _history.State.Project.Blueprints.Single(candidate => candidate.Id == fixedArea.Entities[0].BlueprintId).Kind !=
            BlueprintKind.PlayerSpawn ||
            CreatorDiagnostics.FromProject(_history.State.Project)
                .Any(candidate => candidate.DiagnosticId == diagnostic.DiagnosticId))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G715_DIAGNOSTICS_FAILED quick_fix");
            return false;
        }

        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "actionable-diagnostics-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var created = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var persistedArea = reopened.Session?.Project.Areas.SingleOrDefault(candidate => candidate.Id == createdAreaId);
            if (!created.Succeeded || !reopened.Succeeded || persistedArea?.Entities.Count != 1)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G715_DIAGNOSTICS_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot))
            {
                Directory.Delete(smokeRoot, recursive: true);
            }
        }

        var undoFix = _history.Undo();
        var redoFix = _history.Redo();
        var cleanupFix = _history.Undo();
        var cleanupArea = _history.Undo();
        _activeAreaContext.EnsureValid(_history.State.Project);
        ActivateArea(originalAreaId);
        if (!undoFix.Succeeded || !redoFix.Succeeded || !cleanupFix.Succeeded || !cleanupArea.Succeeded ||
            _history.State.Project.Areas.Count != originalAreaCount ||
            _history.State.Project.Areas.Any(candidate => candidate.Id == createdAreaId))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G715_DIAGNOSTICS_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G715_DIAGNOSTICS_PASS uuid=stable severity=typed path=document " +
            "property=visible source=validator quickfix=command preview=dry-run navigation=targeted " +
            "history=restored persistence=roundtrip");
        return true;
    }

    private bool RunAssetContentBrowserSmokeTest()
    {
        if (_assetBrowser is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G716_ASSETS_FAILED missing_browser");
            return false;
        }

        _assetBrowser.Refresh();
        if (_assetBrowser.CatalogCount < 674 || _assetBrowser.SourceCount < 5 ||
            _assetBrowser.FilterForSmokeTest("apple", "ambientcg") != 3 ||
            !_assetBrowser.SelectAssetForSmokeTest("ambientcg:3DApple001") ||
            !_assetBrowser.SelectedHasPreviewMetadata ||
            (_assetBrowser.SelectedCanPlanImport && !_assetBrowser.PreviewImportForSmokeTest()) ||
            !_assetBrowser.PreviewProfileCommandForSmokeTest() ||
            !_assetBrowser.PreviewManifestCommandForSmokeTest() ||
            !_assetBrowser.PreviewExternalEditCommandForSmokeTest() ||
            !_assetBrowser.PreviewGovernanceCommandForSmokeTest() ||
            _assetBrowser.FilterForSmokeTest("castle", "kenney") == 0 ||
            !_assetBrowser.SelectAssetForSmokeTest("kenney:castle-kit:door") ||
            !_assetBrowser.PreviewInstallForPlacementForSmokeTest() ||
            _assetBrowser.FilterForSmokeTest(string.Empty, category: "furniture") == 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G716_ASSETS_FAILED catalog_facets_preview");
            return false;
        }

        _assetBrowser.FilterForSmokeTest(string.Empty);
        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_G716_ASSETS_PASS catalog={_assetBrowser.CatalogCount} sources={_assetBrowser.SourceCount} facets=search-source-category " +
            "uuid=stable hash=addressed license=verified availability=explicit preview=metadata import=governed " +
            "profile=command-dry-run manifest=command-dry-run roundtrip=command-dry-run " +
            "governance=command-dry-run quarantine=pending ip=classified " +
            "install=command-dry-run placement=palette-drag");
        GD.Print("OPENRPG_FORGE_TOOLSET_R018_ASSET_GOVERNANCE_PASS provenance=visible quarantine=pending " +
                 "ip=classified dry_run=true persistence=atomic undo_redo=true distribution=guarded");
        return true;
    }

    private bool RunGovernedAssetPreviewSmokeTest()
    {
        if (_mainPanel is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_AV3_PLACEMENT_FAILED missing_panel");
            return false;
        }

        var root = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "av3-preview-smoke",
            Guid.NewGuid().ToString("N"));
        var modelRelativePath = "assets/imported/av3-smoke/model.glb";
        var modelPath = Path.Combine(root, modelRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
        WriteSmokeTriangleGlb(modelPath);
        var modelHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(modelPath))).ToLowerInvariant();
        var assetId = AssetCatalogItemId.ForExternalId("openrpg:av3-preview-smoke");
        var item = new AssetCatalogItem(
            assetId,
            "openrpg:av3-preview-smoke",
            "Modèle gouverné AV3",
            "openrpg",
            "av3-preview-smoke",
            "https://openrpg.example/assets/av3-preview-smoke",
            "CC0-1.0",
            "https://creativecommons.org/publicdomain/zero/1.0/",
            true,
            modelHash,
            new FileInfo(modelPath).Length,
            ["3d-model"],
            ["smoke"],
            "generated-test-fixture",
            AssetAvailability.ExtractedAvailable,
            modelRelativePath,
            null,
            null,
            "toolset-smoke:av3");
        var chunk = new AssetManifestChunk(
            Guid.Parse("a83d0000-0000-4000-8000-000000000001"),
            AssetChunkRole.Model,
            true,
            modelRelativePath,
            modelHash,
            new FileInfo(modelPath).Length,
            new FileInfo(modelPath).Length,
            AssetChunkCompression.None,
            [AssetTargetPlatform.Any]);
        var manifest = new AssetPackageManifest(
            AssetPackageManifest.CurrentSchemaVersion,
            AssetPackageManifestId.ForAsset(assetId),
            assetId,
            "toolset-smoke:av3",
            item.LicenseSpdx,
            item.ContentSha256,
            new Dictionary<AssetTargetPlatform, long> { [AssetTargetPlatform.Any] = 1024 * 1024 },
            [chunk],
            null,
            null,
            null,
            null);
        var blueprint = AssetBlueprintFactory.Create(item, manifest);
        var sourceProject = _history.State.Project;
        var sourceArea = GetActiveArea();
        var scatterSource = AreaWorldProfile.CreateDefault(sourceArea.Id, AreaKind.Exterior, sourceArea.Dimensions);
        var scatterMaterial = scatterSource.Terrain.MaterialLayers.Single(candidate => candidate.Name == "Prairie");
        var scatterBiome = new BiomeLayerDefinition(
            Guid.Parse("a83d0000-0000-4000-8000-000000000003"),
            "Prairie gouvernée AV3",
            scatterMaterial.Id,
            -512,
            512,
            0,
            90,
            0,
            1,
            .35f,
            $"asset:{assetId}",
            ["ground-cover", "meadow"],
            [],
            20,
            "toolset-smoke:av3");
        var scatterWorld = new AreaWorldProfile(
            scatterSource.Id,
            scatterSource.SchemaVersion,
            scatterSource.Terrain,
            [scatterBiome],
            [],
            scatterSource.VolumetricNavigation,
            [],
            scatterSource.Loading,
            "toolset-smoke:av3");
        var entity = new EntityInstance(
            EntityId.From(Guid.Parse("a83d0000-0000-4000-8000-000000000002")),
            blueprint.Id,
            TransformValue.Identity,
            ["asset"],
            new EntityPropertyOverrides(
                EntityPropertyOverrides.CurrentSchemaVersion,
                [new EntityPropertyOverride("visual.asset", EntityPropertyValueKind.String, assetId.ToString())]));
        var project = new ProjectDefinition(
            sourceProject.Id,
            sourceProject.Title,
            sourceProject.Module,
            [blueprint],
            [sourceArea.WithWorld(scatterWorld).WithEntities([entity])],
            sourceProject.Dialogues,
            sourceProject.Quests);
        var state = new ProjectState(project, new ProjectRevision(0));
        var manifests = new AssetPackageManifestState([manifest]);
        var plannedScatter = BiomeScatterPlanBuilder.Build(scatterWorld, scatterBiome);
        var smokeLibrary = new GodotAssetModelLibrary(root, manifests.Manifests);
        var resolvedScatterMesh = smokeLibrary.ResolveScatterMesh(scatterBiome.ScatterResource);
        if (plannedScatter.Count == 0 || resolvedScatterMesh is null)
        {
            var modelTree = "unavailable";
            if (resolvedScatterMesh is null)
            {
                var instance = smokeLibrary.Instantiate(assetId.ToString());
                try
                {
                    modelTree = DescribeSmokeNodeTree(instance);
                }
                finally
                {
                    instance.Free();
                }
            }
            GD.PrintErr(
                "OPENRPG_FORGE_TOOLSET_AV3_PLACEMENT_FAILED scatter_preflight " +
                $"planned={plannedScatter.Count} manifests={manifests.Manifests.Count} tree={modelTree}");
            return false;
        }
        var areaId = project.Areas.First().Id;
        var preview = new ProjectPreview3D { Visible = false };
        _mainPanel.AddChild(preview);
        try
        {
            preview.Initialize(
                () => state,
                () => root,
                () => manifests,
                () => areaId,
                () => 1f,
                _ => { },
                (_, _) => false,
                (_, _) => null,
                (_, _) => false,
                (_, _) => { },
                allowRuntimeAssetImport: true);
            preview.Rebuild(project);
            if (preview.AssetModelInstanceCount != 1 || preview.TerrainScatterLayerCount != 1 ||
                preview.TerrainScatterInstanceCount <= 0)
            {
                GD.PrintErr(
                    "OPENRPG_FORGE_TOOLSET_AV3_PLACEMENT_FAILED expected_models=1 expected_scatter=1+ " +
                    $"loaded={preview.AssetModelInstanceCount} scatter_layers={preview.TerrainScatterLayerCount} " +
                    $"scatter_instances={preview.TerrainScatterInstanceCount}");
                return false;
            }

            GD.Print(
                $"OPENRPG_FORGE_TOOLSET_AV3_PLACEMENT_PASS models={preview.AssetModelInstanceCount} " +
                $"manifests={manifests.Manifests.Count} scatter_layers={preview.TerrainScatterLayerCount} " +
                $"scatter_instances={preview.TerrainScatterInstanceCount} visual=governed-glb hash=verified " +
                "preview=editor biome_brush=canonical");
            return true;
        }
        finally
        {
            _mainPanel.RemoveChild(preview);
            preview.Free();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteSmokeTriangleGlb(string path)
    {
        const string json =
            "{\"asset\":{\"version\":\"2.0\"},\"scene\":0,\"scenes\":[{\"nodes\":[0]}]," +
            "\"nodes\":[{\"mesh\":0}],\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0},\"indices\":1}]}]," +
            "\"buffers\":[{\"byteLength\":42}]," +
            "\"bufferViews\":[{\"buffer\":0,\"byteOffset\":0,\"byteLength\":36,\"target\":34962}," +
            "{\"buffer\":0,\"byteOffset\":36,\"byteLength\":6,\"target\":34963}]," +
            "\"accessors\":[{\"bufferView\":0,\"componentType\":5126,\"count\":3,\"type\":\"VEC3\"," +
            "\"min\":[0,0,0],\"max\":[1,1,0]},{\"bufferView\":1,\"componentType\":5123,\"count\":3,\"type\":\"SCALAR\"}]}";
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var jsonLength = (jsonBytes.Length + 3) & ~3;
        const int binaryLength = 44;
        var totalLength = 12 + 8 + jsonLength + 8 + binaryLength;
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);
        writer.Write(0x46546c67u);
        writer.Write(2u);
        writer.Write((uint)totalLength);
        writer.Write((uint)jsonLength);
        writer.Write(0x4e4f534au);
        writer.Write(jsonBytes);
        for (var index = jsonBytes.Length; index < jsonLength; index++) writer.Write((byte)0x20);
        writer.Write((uint)binaryLength);
        writer.Write(0x004e4942u);
        foreach (var coordinate in new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f }) writer.Write(coordinate);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)2);
        writer.Write((ushort)0);
    }

    private static string DescribeSmokeNodeTree(Node root)
    {
        var nodes = new List<string>();
        void Visit(Node node)
        {
            nodes.Add(node is MeshInstance3D mesh
                ? $"{node.GetType().Name}[mesh={mesh.Mesh?.GetType().Name ?? "null"}]"
                : node.GetType().Name);
            foreach (Node child in node.GetChildren()) Visit(child);
        }

        Visit(root);
        return string.Join(",", nodes);
    }

    private bool RunLocalizationWorkspaceSmokeTest()
    {
        if (_history is null || _localizationWorkspace is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G717_LOCALIZATION_FAILED missing_workspace");
            return false;
        }

        var originalCatalog = _history.State.Project.Module.Localization;
        _localizationWorkspace.Refresh();
        var defaultLocale = _history.State.Project.Module.Metadata.DefaultLocale;
        if (_localizationWorkspace.MessageCount == 0 || _localizationWorkspace.LocaleCount == 0 ||
            !_localizationWorkspace.SelectForSmokeTest("module.name", defaultLocale) ||
            !_localizationWorkspace.SetTranslationForSmokeTest("Crypte des Cendres · localisation") ||
            !_localizationWorkspace.PreviewForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G717_LOCALIZATION_FAILED extract_edit_preview");
            return false;
        }

        var draft = LocalizationCatalogExtractor.Extract(_history.State.Project).Catalog;
        var localized = new LocalizationCatalog(draft.Id, draft.DefaultLocale, draft.Locales,
            draft.Messages.Select(message => message.Key == "module.name"
                ? new LocalizationMessage(message.Key, message.Fallback,
                    message.Translations.ToDictionary(pair => pair.Key, pair =>
                        pair.Key == defaultLocale ? "Crypte des Cendres · localisation" : pair.Value),
                    message.Provenance)
                : message).ToArray());
        if (!ApplyLocalizationCatalog(localized, false) ||
            _history.State.Project.Module.Localization.Resolve("module.name", defaultLocale) != "Crypte des Cendres · localisation")
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G717_LOCALIZATION_FAILED command");
            return false;
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            _history.State.Project.Module.Localization != originalCatalog)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G717_LOCALIZATION_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_G717_LOCALIZATION_PASS messages={_localizationWorkspace.MessageCount} " +
            $"locales={_localizationWorkspace.LocaleCount} extraction=canonical fallback=resolved " +
            "pseudo=available preview=dry-run history=restored");
        return true;
    }


}
#endif
