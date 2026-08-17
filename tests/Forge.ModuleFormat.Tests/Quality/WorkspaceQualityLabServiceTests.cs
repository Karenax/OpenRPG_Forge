using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Tests.Serialization;
using Forge.Scripting;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Quality;
using Forge.Domain.WorldFeatures;

namespace Forge.ModuleFormat.Tests.Quality;

public sealed class WorkspaceQualityLabServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OpenRPGForgeTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Workspace_lab_runs_all_five_domains_writes_pseudo_snapshot_and_is_deterministic()
    {
        Directory.CreateDirectory(Path.Combine(_root, "scripts"));
        File.WriteAllText(Path.Combine(_root, "scripts", "quality.forge"),
            "void main() { variable(\"quality\", \"pass\"); say(\"Quality pass\"); }");
        var service = new WorkspaceQualityLabService(new NativeForgeScriptRuntime());

        var first = service.Run(_root, TestProjectFactory.CreateNavigationValid());
        var second = service.Run(_root, TestProjectFactory.CreateNavigationValid());

        Assert.True(first.Passed, string.Join("; ", first.CanonicalValidation.Diagnostics
            .Select(item => $"{item.Category}:{item.Code}:{item.Path}:{item.Message}")));
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.True(first.Localization.Passed);
        Assert.True(Assert.Single(first.Dialogues).Passed);
        Assert.True(Assert.Single(first.Quests).Passed);
        Assert.True(first.Encounters.Passed);
        Assert.True(first.Scripts.Passed);
        Assert.True(first.CanonicalValidation.Passed,
            string.Join("; ", first.CanonicalValidation.Diagnostics.Select(item => $"{item.Code}:{item.Path}")));
        Assert.Equal(5, first.CanonicalValidation.CategoryCounts.Count);
        Assert.Equal(first.CanonicalValidation.Fingerprint, second.CanonicalValidation.Fingerprint);
        Assert.Equal(WorkspaceQualityLabService.PseudoSnapshotRelativePath, first.PseudoLocalizationSnapshotPath);
        Assert.True(File.Exists(Path.Combine(_root, ".forge", "quality", "pseudo-localization.svg")));
    }

    [Fact]
    public void Optional_navigation_diagnostics_remain_visible_without_blocking_legacy_quality_gate()
    {
        Directory.CreateDirectory(Path.Combine(_root, "scripts"));
        File.WriteAllText(Path.Combine(_root, "scripts", "quality.forge"),
            "void main() { variable(\"quality\", \"pass\"); say(\"Quality pass\"); }");
        var report = new WorkspaceQualityLabService(new NativeForgeScriptRuntime())
            .Run(_root, TestProjectFactory.CreateValid());

        Assert.True(report.Passed);
        Assert.False(report.CanonicalValidation.Passed);
        Assert.Contains(report.CanonicalValidation.Diagnostics,
            item => item.Category == "navigation" && item.Code == "area.navigation.spawn_missing");
    }

    [Fact]
    public void Required_area_lifecycle_script_is_blocked_before_build_and_passes_when_present()
    {
        var source = TestProjectFactory.CreateNavigationValid();
        var area = source.Areas[0];
        var defaults = area.Foundation;
        var foundation = new AreaFoundationProfile(defaults.Id, 1, "test:r028",
            defaults.Creation, defaults.TileKit,
            new(new(Guid.Parse("a0284000-0000-4000-8000-000000000001"), "area.entered",
                "scripts/required_enter.forge", "1.0.0", true, "test:r028"), null), [], []);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, source.Blueprints,
            source.Areas.Select(item => item.Id == area.Id ? item.WithFoundation(foundation) : item).ToArray(),
            source.Dialogues, source.Quests);
        Directory.CreateDirectory(Path.Combine(_root, "scripts"));

        var missing = WorkspaceCanonicalValidationService.Run(_root, project,
            QualityFoundationProfile.CreateDefault(project.Id.Value));
        File.WriteAllText(Path.Combine(_root, "scripts", "required_enter.forge"), "void main() {}");
        var present = WorkspaceCanonicalValidationService.Run(_root, project,
            QualityFoundationProfile.CreateDefault(project.Id.Value));

        Assert.False(missing.Passed);
        Assert.Contains(missing.Diagnostics, item =>
            item.Code == WorkspaceCanonicalValidationService.AreaLifecycleScriptMissingCode &&
            item.Path.Contains(area.Id.ToString(), StringComparison.Ordinal));
        Assert.DoesNotContain(present.Diagnostics,
            item => item.Code == WorkspaceCanonicalValidationService.AreaLifecycleScriptMissingCode);
    }

    [Fact]
    public void Ux2LwCTypedAreaEventRequiresUpgradeAndRequiredSourceBeforeBuild()
    {
        var source = TestProjectFactory.CreateNavigationValid();
        var area = source.Areas[0];
        var authoring = area.Authoring;
        var eventName = $"area.{area.Slug}.quality";
        var current = new AreaUserEvent(Guid.Parse("a0240000-0000-4000-8000-000000000030"),
            eventName, "schema://area/quality/v1", "scripts/area_quality.forge", 1, ["main"], true,
            "test:ux2-lw-c", AreaUserEvent.CurrentSchemaVersion,
            new Dictionary<string, string> { ["message"] = "string" },
            AreaUserEvent.CurrentForgeScriptApiVersion);
        var legacy = new AreaUserEvent(current.Id, current.NamespacedName, current.PayloadSchema,
            current.ScriptPath, current.ApiVersion, current.Subscriptions, false, "migration:1.9");
        var currentArea = area.WithAuthoring(new(authoring.SchemaVersion, [current], authoring.Map,
            authoring.Layers, authoring.Experience, authoring.RequireLayerRuntimeMetadata));
        var legacyArea = area.WithAuthoring(new(authoring.SchemaVersion, [legacy], authoring.Map,
            authoring.Layers, authoring.Experience, authoring.RequireLayerRuntimeMetadata));
        var currentProject = new ProjectDefinition(source.Id, source.Title, source.Module, source.Blueprints,
            source.Areas.Select(item => item.Id == area.Id ? currentArea : item).ToArray(),
            source.Dialogues, source.Quests);
        var legacyProject = new ProjectDefinition(source.Id, source.Title, source.Module, source.Blueprints,
            source.Areas.Select(item => item.Id == area.Id ? legacyArea : item).ToArray(),
            source.Dialogues, source.Quests);
        Directory.CreateDirectory(Path.Combine(_root, "scripts"));
        var profile = QualityFoundationProfile.CreateDefault(source.Id.Value);

        var missing = WorkspaceCanonicalValidationService.Run(_root, currentProject, profile);
        var upgrade = WorkspaceCanonicalValidationService.Run(_root, legacyProject, profile);
        File.WriteAllText(Path.Combine(_root, "scripts", "area_quality.forge"), "void main() {}");
        var present = WorkspaceCanonicalValidationService.Run(_root, currentProject, profile);

        Assert.Contains(missing.Diagnostics,
            item => item.Code == WorkspaceCanonicalValidationService.AreaUserEventScriptMissingCode);
        Assert.Contains(upgrade.Diagnostics,
            item => item.Code == AreaUserEventValidator.SchemaUpgradeRequiredCode);
        Assert.DoesNotContain(present.Diagnostics,
            item => item.Code is WorkspaceCanonicalValidationService.AreaUserEventScriptMissingCode or
                AreaUserEventValidator.SchemaUpgradeRequiredCode);
    }

    [Fact]
    public void Authored_door_runtime_semantics_are_blocked_by_the_canonical_prebuild_gate()
    {
        var source = TestProjectFactory.CreateNavigationValid();
        var blueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("a0290000-0000-4000-8000-000000000001")),
            "quality-door", "Quality door", BlueprintKind.Door);
        var door = new DoorWorldProfile(
            Guid.Parse("a0290000-0000-4000-8000-000000000002"),
            WorldFeatureCatalog.CurrentSchemaVersion,
            "test:ux1-aj-quality",
            blueprint.Id,
            null,
            null,
            [],
            null,
            null,
            null,
            new(DoorWorldProfile.PlacementMode.FreePlacement, null, null, "closed",
                [
                    new("open", DoorWorldProfile.StateKind.Open, "animation:open", false, true),
                    new("closed", DoorWorldProfile.StateKind.Closed, "animation:closed", true, true),
                    new("opening", DoorWorldProfile.StateKind.Opening, "animation:opening", false, false),
                ],
                [new("closed", "opening", "interact", 1, null)],
                new(2, "both", 1, 2, 1, "server", 2, "door.unavailable"),
                null,
                [],
                new Dictionary<string, string>
                {
                    [DoorWorldProfile.CollisionProfileOverrideKey] = "collision:quality-door",
                },
                new("nav-obstacle:quality-door", "nav-link:quality-door", ["closed"], ["open"],
                    .75, .25, true)),
            null,
            false,
            new([], [], ["collision:other"], [], []),
            true);
        var catalog = source.Module.WorldFeatures;
        var withDoorCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion, catalog.Provenance,
            catalog.Placeables, catalog.Doors.Append(door).ToArray(), catalog.Triggers, catalog.Encounters,
            catalog.VehicleRoutes, catalog.Merchants, catalog.Companions, catalog.Scripts);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module.WithWorldFeatures(withDoorCatalog),
            source.Blueprints.Append(blueprint).ToArray(), source.Areas, source.Dialogues, source.Quests);

        var report = WorkspaceCanonicalValidationService.Run(_root, project,
            QualityFoundationProfile.CreateDefault(project.Id.Value));
        var profile = QualityFoundationProfile.CreateDefault(project.Id.Value);
        var blocking = WorkspaceCanonicalValidationService.BlockingDiagnostics(project, profile, report);
        var semantic = report.Diagnostics.Where(item =>
            item.Code is "RPG_DOOR_ANIMATION_MISSING" or "RPG_DOOR_COLLISION_PROFILE_MISMATCH").ToArray();

        Assert.False(report.Passed);
        Assert.Equal(2, semantic.Length);
        Assert.All(semantic, item => Assert.Equal("references", item.Category));
        Assert.Equal(semantic.Length, semantic.DistinctBy(item => (item.Code, item.Path)).Count());
        Assert.False(profile.EffectiveValidation.References.RequiredBeforeBuild);
        Assert.Equal(semantic.Select(item => (item.Code, item.Path)).Order().ToArray(),
            blocking.Select(item => (item.Code, item.Path)).Order().ToArray());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
