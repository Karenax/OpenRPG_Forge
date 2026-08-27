using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Assets;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Content;
using Forge.Domain.Factions;
using Forge.Domain.Lifecycle;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Validation;
using Forge.Domain.Build;
using Forge.Domain.WorldFeatures;
using Forge.Application.Projects;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Tests.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;

namespace Forge.ModuleFormat.Tests.Packages;

public sealed class ModulePackageTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(),
        "openrpg-forge-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void R120ExportRejectsInvalidAssistantTestAutomationBeforeWritingPackage()
    {
        var workspace = CreateWorkspace();
        Assert.True(AssistantWorkspaceProfileStore.Save(
            workspace,
            Forge.Domain.Assistant.AssistantWorkspaceProfile.CreateDefault()).Succeeded);
        var profilePath = Path.Combine(workspace, AssistantWorkspaceProfileStore.RelativePath);
        var json = File.ReadAllText(profilePath);
        File.WriteAllText(
            profilePath,
            json.Replace("\"maximumScenarioSteps\": 128", "\"maximumScenarioSteps\": 0", StringComparison.Ordinal),
            new UTF8Encoding(false));
        var package = Path.Combine(_temporaryRoot, "invalid-assistant-test.orpmod");

        var exception = Assert.Throws<InvalidDataException>(() => ModulePackager.Export(workspace, package));

        Assert.Contains("assistant.test.profile_bounds_invalid", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(package));
    }

    [Fact]
    public void Ux1AjExportRejectsRequiredDoorSemanticsWhenReferencesAreOptional()
    {
        var workspace = CreateWorkspace();
        var session = ProjectSession.Open(workspace).Session!;
        var catalog = CryptWorldFeatureAuthoringService.Create(session.Project);
        var door = Assert.Single(catalog.Doors);
        var foundation = door.Foundation!;
        var access = door.AccessEvents!;
        var validation = new DoorWorldProfile.RuntimeValidationPolicy(
            foundation.Portal is null ? [] : [foundation.Portal.DestinationId],
            foundation.States.Select(item => item.AnimationId).Append(access.Rejection.AnimationId).ToArray(),
            ["collision:other"],
            access.Lock.CredentialIds,
            door.Events.Select(item => item.ScriptId).Append(access.Rejection.ScriptId).ToArray());
        var invalidDoor = door with { Validation = validation, ValidationRequiredAtRuntime = true };
        var invalidCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion, catalog.Provenance,
            catalog.Placeables, [invalidDoor], catalog.Triggers, catalog.Encounters, catalog.VehicleRoutes,
            catalog.Merchants, catalog.Companions, catalog.Scripts);
        var project = new ProjectDefinition(session.Project.Id, session.Project.Title,
            session.Project.Module.WithWorldFeatures(invalidCatalog), session.Project.Blueprints,
            session.Project.Areas, session.Project.Dialogues, session.Project.Quests);
        Assert.True(session.Save(project).Succeeded);
        var quality = QualityFoundationProfileStore.Load(workspace, project.Id.Value);
        Assert.True(quality.Succeeded);
        Assert.False(quality.Profile.EffectiveValidation.References.RequiredBeforeBuild);
        var package = Path.Combine(_temporaryRoot, "invalid-ux1-aj-door.orpmod");

        var exception = Assert.Throws<InvalidOperationException>(() => ModulePackager.Export(workspace, package));

        Assert.Contains("RPG_DOOR_COLLISION_PROFILE_MISMATCH", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(package));
    }

    [Fact]
    public void R051ExportRejectsInvalidProjectGameplayEventBeforeWritingPackage()
    {
        var workspace = CreateWorkspace();
        var opened = ProjectSession.Open(workspace).Session!;
        Assert.True(opened.Save(opened.Project).Succeeded);
        var projectPath = Path.Combine(workspace, "project.json");
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(projectPath))!.AsObject();
        var bindings = document["project"]!["module"]!["runtimePolicies"]!["gameplayEvents"]!["bindings"]!.AsArray();
        bindings[0]!["handler"] = "bad handler!";
        File.WriteAllText(projectPath, document.ToJsonString(), new UTF8Encoding(false));
        var package = Path.Combine(_temporaryRoot, "invalid-project-event.orpmod");

        var exception = Assert.Throws<InvalidDataException>(() => ModulePackager.Export(workspace, package));

        Assert.Contains(ProjectGameplayEventProfileValidator.BindingContractInvalidCode, exception.Message,
            StringComparison.Ordinal);
        Assert.False(File.Exists(package));
    }

    [Fact]
    public void R052ExportRejectsInvalidProjectWorldAndPlayerDocumentationBeforeWritingPackage()
    {
        var workspace = CreateWorkspace();
        var opened = ProjectSession.Open(workspace).Session!;
        var moduleId = opened.Project.Module.Id.Value;
        var baseline = opened.Project.Module.RuntimePolicies;
        var schema = new Forge.Domain.Lifecycle.CustomEventSchema(Guid.Parse("52000000-0000-4000-8000-000000000052"), 1,
            "test:r052", "crypt", "braziers_lit", 1,
            new Dictionary<string, string> { ["count"] = "int" }, false, "1.0", "OnBraziersLit",
            EventAuthorityScope.Server);
        var world = new ProjectWorldProfile(moduleId, 1, "test:r052", true, true, [schema], baseline.World.Clock);
        var policies = new ModuleRuntimePolicyProfile(baseline.Id, baseline.SchemaVersion, baseline.Heartbeats,
            baseline.Rest, baseline.SequenceAbort, "test:r052", baseline.Cinematics, baseline.Gameplay,
            baseline.Rules, baseline.Extensions, baseline.GameplayEvents, world);
        var project = new ProjectDefinition(opened.Project.Id, opened.Project.Title,
            opened.Project.Module.WithRuntimePolicies(policies), opened.Project.Blueprints, opened.Project.Areas,
            opened.Project.Dialogues, opened.Project.Quests);
        Assert.True(opened.Save(project).Succeeded);
        var projectPath = Path.Combine(workspace, "project.json");
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(projectPath))!.AsObject();
        document["project"]!["module"]!["runtimePolicies"]!["world"]!["customEvents"]![0]!["handler"] =
            "bad handler!";
        document["project"]!["module"]!["metadata"]!["localizations"]![0]!["warnings"] =
            new string('x', ProjectDocumentationValidator.MaximumPageCharacters + 1);
        File.WriteAllText(projectPath, document.ToJsonString(), new UTF8Encoding(false));
        var package = Path.Combine(_temporaryRoot, "invalid-r052.orpmod");

        var exception = Assert.Throws<InvalidDataException>(() => ModulePackager.Export(workspace, package));

        Assert.Contains(ProjectWorldProfileValidator.EventContractInvalidCode, exception.Message,
            StringComparison.Ordinal);
        Assert.Contains(ProjectDocumentationValidator.PageTooLargeCode, exception.Message,
            StringComparison.Ordinal);
        Assert.False(File.Exists(package));
    }

    [Fact]
    public void R053ExportRejectsRequiredMissingTileEditingMetadataBeforeWritingPackage()
    {
        var workspace = CreateWorkspace();
        var opened = ProjectSession.Open(workspace).Session!;
        var area = opened.Project.Areas[0];
        var original = area.Foundation;
        var editing = AreaTileEditingProfile.CreateDefault(area.Id);
        var kit = new ModularTileKitProfile(original.TileKit.Id, original.TileKit.SchemaVersion, "test:r053",
            original.TileKit.GridSizeMeters, original.TileKit.Definitions, original.TileKit.ConnectionRules,
            original.TileKit.Placements, original.TileKit.Groups, original.TileKit.Brushes, editing);
        var foundation = new AreaFoundationProfile(original.Id, original.SchemaVersion, "test:r053",
            original.Creation, kit, original.Lifecycle, original.Volumes, original.Dependencies, true);
        var project = new ProjectDefinition(opened.Project.Id, opened.Project.Title, opened.Project.Module,
            opened.Project.Blueprints,
            opened.Project.Areas.Select(item => item.Id == area.Id ? item.WithFoundation(foundation) : item).ToArray(),
            opened.Project.Dialogues, opened.Project.Quests);
        Assert.True(opened.Save(project).Succeeded);
        var projectPath = Path.Combine(workspace, "project.json");
        var index = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(projectPath))!.AsObject();
        var relativeAreaPath = index["project"]!["areas"]![0]!["documentPath"]!.GetValue<string>();
        var areaPath = Path.Combine(workspace, relativeAreaPath.Replace('/', Path.DirectorySeparatorChar));
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(areaPath))!.AsObject();
        document["area"]!["foundation"]!["tileKit"]!.AsObject().Remove("editing");
        File.WriteAllText(areaPath, document.ToJsonString(), new UTF8Encoding(false));
        var package = Path.Combine(_temporaryRoot, "invalid-r053.orpmod");

        var exception = Assert.Throws<InvalidOperationException>(() => ModulePackager.Export(workspace, package));

        Assert.Contains(AreaTileEditingRuntimeResolver.RequiredMissingCode, exception.Message,
            StringComparison.Ordinal);
        Assert.False(File.Exists(package));
    }

    [Fact]
    public void R054ExportRejectsRequiredMissingEnvironmentMetadataBeforeWritingPackage()
    {
        var workspace = CreateWorkspace();
        var opened = ProjectSession.Open(workspace).Session!;
        var area = opened.Project.Areas[0];
        var source = area.Environment;
        var authoring = new AreaEnvironmentAuthoringProfile(source.Authoring.Id,
            source.Authoring.SchemaVersion, "test:r054", source.Authoring.Classification,
            source.Authoring.Lighting, source.Authoring.Fog, true);
        var environment = new AreaEnvironmentProfile(source.SchemaVersion, source.Classification, source.Tags,
            source.Lighting, source.Fog, source.Audio, source.Rendering, source.Sky, source.Wind, source.Weather,
            source.Water, source.Reverb, authoring, true);
        var project = new ProjectDefinition(opened.Project.Id, opened.Project.Title, opened.Project.Module,
            opened.Project.Blueprints,
            opened.Project.Areas.Select(item => item.Id == area.Id ? item.WithEnvironment(environment) : item)
                .ToArray(), opened.Project.Dialogues, opened.Project.Quests);
        Assert.True(opened.Save(project).Succeeded);
        var projectPath = Path.Combine(workspace, "project.json");
        var index = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(projectPath))!.AsObject();
        var relativeAreaPath = index["project"]!["areas"]![0]!["documentPath"]!.GetValue<string>();
        var areaPath = Path.Combine(workspace, relativeAreaPath.Replace('/', Path.DirectorySeparatorChar));
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(areaPath))!.AsObject();
        document["area"]!["environment"]!.AsObject().Remove("authoring");
        File.WriteAllText(areaPath, document.ToJsonString(), new UTF8Encoding(false));
        var package = Path.Combine(_temporaryRoot, "invalid-r054.orpmod");

        var exception = Assert.Throws<InvalidDataException>(() => ModulePackager.Export(workspace, package));

        Assert.Contains(AreaEnvironmentRuntimeResolver.RequiredMissingCode, exception.Message,
            StringComparison.Ordinal);
        Assert.False(File.Exists(package));
    }

    [Fact]
    public void R055ExportRejectsRequiredMissingAreaExperienceMetadataBeforeWritingPackage()
    {
        var workspace = CreateWorkspace();
        var opened = ProjectSession.Open(workspace).Session!;
        var area = opened.Project.Areas[0];
        var experience = new AreaExperienceAuthoringProfile(
            Guid.Parse("a0555000-0000-4000-8000-000000000001"), 1, "test:r055", [],
            new([], [], [], true, true, true, 16, 128),
            new(AreaLodSourceMode.Hybrid, [20, 50, 100], 1_500_000, 96, 24, .15f, true, true, []),
            true);
        var authoring = new AreaAuthoringProfile(area.Authoring.SchemaVersion, area.Authoring.UserEvents,
            area.Authoring.Map, area.Authoring.Layers, experience);
        var project = new ProjectDefinition(opened.Project.Id, opened.Project.Title, opened.Project.Module,
            opened.Project.Blueprints,
            opened.Project.Areas.Select(item => item.Id == area.Id ? item.WithAuthoring(authoring) : item).ToArray(),
            opened.Project.Dialogues, opened.Project.Quests);
        Assert.True(opened.Save(project).Succeeded);
        var projectPath = Path.Combine(workspace, "project.json");
        var index = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(projectPath))!.AsObject();
        var relativeAreaPath = index["project"]!["areas"]![0]!["documentPath"]!.GetValue<string>();
        var areaPath = Path.Combine(workspace, relativeAreaPath.Replace('/', Path.DirectorySeparatorChar));
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(areaPath))!.AsObject();
        document["area"]!["authoring"]!["experience"]!["provenance"] = "migration-default";
        File.WriteAllText(areaPath, document.ToJsonString(), new UTF8Encoding(false));
        var package = Path.Combine(_temporaryRoot, "invalid-r055.orpmod");

        var exception = Assert.Throws<InvalidDataException>(() => ModulePackager.Export(workspace, package));

        Assert.Contains(AreaExperienceRuntimeResolver.RequiredMissingCode, exception.Message,
            StringComparison.Ordinal);
        Assert.False(File.Exists(package));
    }

    [Fact]
    public void R056ExportRejectsRequiredMissingAreaLayersBeforeWritingPackage()
    {
        var workspace = CreateWorkspace();
        var opened = ProjectSession.Open(workspace).Session!;
        var area = opened.Project.Areas[0];
        var authoring = new AreaAuthoringProfile(area.Authoring.SchemaVersion, area.Authoring.UserEvents,
            area.Authoring.Map,
            [new(Guid.Parse("a0565000-0000-4000-8000-000000000001"), "Architecture", null, true,
                false, "builder", [], provenance: "test:r056")], area.Authoring.Experience, true);
        var project = new ProjectDefinition(opened.Project.Id, opened.Project.Title, opened.Project.Module,
            opened.Project.Blueprints,
            opened.Project.Areas.Select(item => item.Id == area.Id ? item.WithAuthoring(authoring) : item).ToArray(),
            opened.Project.Dialogues, opened.Project.Quests);
        Assert.True(opened.Save(project).Succeeded);
        var projectPath = Path.Combine(workspace, "project.json");
        var index = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(projectPath))!.AsObject();
        var relativeAreaPath = index["project"]!["areas"]![0]!["documentPath"]!.GetValue<string>();
        var areaPath = Path.Combine(workspace, relativeAreaPath.Replace('/', Path.DirectorySeparatorChar));
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(areaPath))!.AsObject();
        document["area"]!["authoring"]!["layers"] = new System.Text.Json.Nodes.JsonArray();
        File.WriteAllText(areaPath, document.ToJsonString(), new UTF8Encoding(false));
        var package = Path.Combine(_temporaryRoot, "invalid-r056.orpmod");

        var exception = Assert.Throws<InvalidDataException>(() => ModulePackager.Export(workspace, package));

        Assert.Contains(AreaLayerRuntimeResolver.RequiredMissingCode, exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(package));
    }

    [Fact]
    public void ExportUsesCanonicalMinimumRuntimeAndLoaderRejectsRequiredIncompatibilityBeforeExtraction()
    {
        var workspace = CreateWorkspace();
        var source = WorkspaceBuildProfile.CreateDefault();
        var profile = new WorkspaceBuildProfile(source.Id, source.SchemaVersion, source.Provenance,
            source.RequireGitRepository, source.TextDocuments, source.ExplicitGeneratedFiles,
            source.ReproducibleAssets, source.Layout, source.CliOperations, source.StableOrdering,
            source.NormalizedTimestamps, source.LockedDependencies, source.ContentHashes,
            source.VerifyReproducibility, source.IncrementalBuild, source.CachePath, source.RebuildAffectedOnly,
            source.Governance, source.Collaboration,
            source.Compatibility with { MinimumRuntimeVersion = "99.0.0", RequiredAtRuntime = true });
        Assert.True(WorkspaceBuildProfileStore.Save(workspace, profile).Succeeded);
        var package = Path.Combine(_temporaryRoot, "future-runtime.orpmod");
        var exported = ModulePackager.Export(workspace, package);

        Assert.Equal("99.0.0", exported.Manifest.RuntimeMinimumVersion);
        Assert.True(exported.Manifest.RuntimeCompatibilityRequired);
        var validation = ModulePackageValidator.Validate(package, availableRuntimeVersion: "0.6.34");
        var diagnostic = Assert.Single(validation.Diagnostics,
            item => item.Code == ModuleRuntimeCompatibility.IncompatibleRuntimeCode);
        Assert.Equal(ModuleRuntimeCompatibility.DiagnosticPath, diagnostic.Path);
        var cache = Path.Combine(_temporaryRoot, "incompatible-cache");
        Assert.Throws<InvalidDataException>(() => ModulePackageLoader.Load(package, cache));
        Assert.False(Directory.Exists(cache));
    }

    [Fact]
    public void OptionalRuntimeCompatibilityDoesNotBlockLoading()
    {
        var workspace = CreateWorkspace();
        var source = WorkspaceBuildProfile.CreateDefault();
        var profile = new WorkspaceBuildProfile(source.Id, source.SchemaVersion, source.Provenance,
            source.RequireGitRepository, source.TextDocuments, source.ExplicitGeneratedFiles,
            source.ReproducibleAssets, source.Layout, source.CliOperations, source.StableOrdering,
            source.NormalizedTimestamps, source.LockedDependencies, source.ContentHashes,
            source.VerifyReproducibility, source.IncrementalBuild, source.CachePath, source.RebuildAffectedOnly,
            source.Governance, source.Collaboration,
            source.Compatibility with { MinimumRuntimeVersion = "99.0.0", RequiredAtRuntime = false });
        Assert.True(WorkspaceBuildProfileStore.Save(workspace, profile).Succeeded);
        var package = Path.Combine(_temporaryRoot, "optional-future-runtime.orpmod");
        ModulePackager.Export(workspace, package);

        var loaded = ModulePackageLoader.Load(package, Path.Combine(_temporaryRoot, "optional-cache"));

        Assert.False(loaded.Manifest.RuntimeCompatibilityRequired);
        Assert.Equal("99.0.0", loaded.Manifest.RuntimeMinimumVersion);
    }

    [Fact]
    public void Export_includes_valid_module_script_permissions_and_rejects_wrong_module_before_build()
    {
        var workspace = CreateWorkspace();
        var opened = ProjectSession.Open(workspace);
        var project = Assert.IsType<ProjectDefinition>(opened.Session?.Project);
        var store = new ForgeScriptModulePermissionStore(workspace);
        store.Save(ForgeScriptModulePermissionPolicy.CreateDefault(project.Module.Id.Value));
        var package = Path.Combine(_temporaryRoot, "script-permissions.orpmod");

        ModulePackager.Export(workspace, package);

        using (var archive = ZipFile.OpenRead(package))
            Assert.Contains(archive.Entries, entry => entry.FullName ==
                "content/scripts/forge-script-permissions.json");

        store.Save(ForgeScriptModulePermissionPolicy.CreateDefault(Guid.NewGuid()));
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ModulePackager.Export(workspace, Path.Combine(_temporaryRoot, "wrong-module.orpmod")));
        Assert.Contains("script.permission.module_mismatch", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportIgnoresGodotImportSidecars()
    {
        var workspace = CreateWorkspace();
        var sidecar = Path.Combine(workspace, "assets", "imported", "tree", "model.glb.import");
        Directory.CreateDirectory(Path.GetDirectoryName(sidecar)!);
        File.WriteAllText(sidecar, "godot-generated-cache");
        var package = Path.Combine(_temporaryRoot, "without-godot-sidecars.orpmod");

        ModulePackager.Export(workspace, package);

        using var archive = ZipFile.OpenRead(package);
        Assert.DoesNotContain(archive.Entries,
            entry => entry.FullName.EndsWith(".import", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExportValidateAndLoad_PreservesAdvancedFactionProfile()
    {
        var source = TestProjectFactory.CreateValid();
        var catalog = FactionPersistenceTestData.Complete();
        var project = new ProjectDefinition(source.Id, source.Title, source.Module.WithFactions(catalog),
            source.Blueprints, source.Areas, source.Dialogues, source.Quests);
        var workspace = Path.Combine(_temporaryRoot, $"factions-{Guid.NewGuid():N}");
        Assert.True(ProjectSession.Create(workspace, project).Succeeded);
        var package = Path.Combine(_temporaryRoot, "factions.orpmod");

        ModulePackager.Export(workspace, package);
        var validation = ModulePackageValidator.Validate(package);
        var loaded = ModulePackageLoader.Load(package, Path.Combine(_temporaryRoot, "factions-cache"));

        Assert.True(validation.IsValid, JoinDiagnostics(validation));
        var restored = Assert.IsType<FactionAdvancedProfile>(loaded.Project.Module.Factions.Advanced);
        Assert.Equal(catalog.Advanced!.Id, restored.Id);
        Assert.Equal(2, restored.Memberships.Count);
        Assert.Equal(FactionDiplomacyStatus.War,
            Assert.Single(Assert.Single(restored.DynamicFactions).Diplomacy).Status);
    }

    [Fact]
    public void ExportValidateAndLoad_PreservesCanonicalCreatureEvents()
    {
        var source = TestProjectFactory.CreateValid();
        var original = source.Blueprints.Single();
        var events = CreateCreatureEvents();
        var blueprint = new BlueprintDefinition(original.Id, original.Slug, original.DisplayName, original.Kind,
            original.Combat, original.PaletteOrigin, original.IsFavorite, original.Provenance,
            original.CreaturePresentation, original.CreatureProgression, original.CreatureLoot,
            original.CreatureRoleBehavior, events);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, [blueprint], source.Areas,
            source.Dialogues, source.Quests);
        var workspace = Path.Combine(_temporaryRoot, $"creature-events-{Guid.NewGuid():N}");
        Assert.True(ProjectSession.Create(workspace, project).Succeeded);
        var package = Path.Combine(_temporaryRoot, "creature-events.orpmod");

        ModulePackager.Export(workspace, package);
        var validation = ModulePackageValidator.Validate(package);
        var loaded = ModulePackageLoader.Load(package, Path.Combine(_temporaryRoot, "creature-events-cache"));

        Assert.True(validation.IsValid, JoinDiagnostics(validation));
        var restored = Assert.IsType<CreatureEventProfile>(loaded.Project.Blueprints.Single().CreatureEvents);
        Assert.Equal(events.Id, restored.Id);
        Assert.Equal(8, restored.Heartbeat!.BudgetMilliseconds);
        Assert.Equal("crypt.braziers_lit", Assert.Single(restored.CustomEvents).QualifiedName);
    }

    [Fact]
    public void ExportValidateAndLoad_PreservesCreatureRoleBehaviorAndAutoLevelTemplate()
    {
        var source = TestProjectFactory.CreateValid();
        var original = source.Blueprints.Single();
        var progression = new CreatureProgressionProfile(
            Guid.Parse("a1450000-0000-4000-8000-000000000001"), 1, "author:test", "forge.generic", false, false,
            [new("warden", "Gardien", 1, 20, 0, [])], [],
            [new("awareness", "Vigilance", "insight", 1, 10, 0, true, false, 10)], [],
            new("standard", AbilityLoadoutMode.Learned, 0, [], []), new Dictionary<string, string>());
        var behavior = new CreatureRoleBehaviorProfile(
            Guid.Parse("a1450000-0000-4000-8000-000000000002"), 1, "author:test", true,
            new("forge.generic", new Dictionary<string, string> { ["occupation"] = "warden" }, ["guardian"]),
            new("ambient.warden",
                [new(Guid.Parse("a1450000-0000-4000-8000-000000000003"), "idle.watch", ["idle"], 1, 2, 6)],
                [], [], new(true, true, true, AmbientResumeMode.Resume, .5)),
            new(Guid.Parse("a1450000-0000-4000-8000-000000000004"), "warden-template", 42, 6, true, false,
                [new("warden", 1)], [new("awareness", 2, 10)], []), new Dictionary<string, string>());
        var blueprint = new BlueprintDefinition(original.Id, original.Slug, original.DisplayName, original.Kind,
            original.Combat, original.PaletteOrigin, original.IsFavorite, original.Provenance,
            original.CreaturePresentation, progression, original.CreatureLoot, behavior);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, [blueprint], source.Areas,
            source.Dialogues, source.Quests);
        var workspace = Path.Combine(_temporaryRoot, $"role-behavior-{Guid.NewGuid():N}");
        Assert.True(ProjectSession.Create(workspace, project).Succeeded);
        var package = Path.Combine(_temporaryRoot, "role-behavior.orpmod");

        ModulePackager.Export(workspace, package);
        var validation = ModulePackageValidator.Validate(package);
        var loaded = ModulePackageLoader.Load(package, Path.Combine(_temporaryRoot, "role-behavior-cache"));

        Assert.True(validation.IsValid, JoinDiagnostics(validation));
        var restored = Assert.IsType<CreatureRoleBehaviorProfile>(
            loaded.Project.Blueprints.Single().CreatureRoleBehavior);
        Assert.Equal(behavior.Id, restored.Id);
        Assert.Equal("warden", restored.Role.LoreFields["occupation"]);
        Assert.Equal(42, restored.AutoLevel.Seed);
    }

    [Fact]
    public void ExportValidateAndLoad_PreservesCanonicalCreatureLootLifecycle()
    {
        var source = TestProjectFactory.CreateValid();
        var original = source.Blueprints.Single();
        var loot = new CreatureLootProfile(
            Guid.Parse("a1410000-0000-4000-8000-000000000001"), 1, "author:test", true,
            [new(Guid.Parse("a1410000-0000-4000-8000-000000000002"), "warden-ember", "relic", 1, 1, 1,
                true, false, LootOwnershipMode.Party, LootInstancingMode.PerParty, null)],
            new(false, "stealth", "awareness", 10, 10, true, "crime.theft", 30, "loot.pickpocket"),
            new(CreatureCorpseMode.RagdollContainer, 60, "corpse.standard", "ragdoll.humanoid", true,
                "party", 8, CorpseCleanupMode.Timed), new Dictionary<string, string>());
        var blueprint = new BlueprintDefinition(original.Id, original.Slug, original.DisplayName, original.Kind,
            original.Combat, original.PaletteOrigin, original.IsFavorite, original.Provenance,
            original.CreaturePresentation, original.CreatureProgression, loot);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, [blueprint], source.Areas,
            source.Dialogues, source.Quests);
        var workspace = Path.Combine(_temporaryRoot, $"loot-{Guid.NewGuid():N}");
        Assert.True(ProjectSession.Create(workspace, project).Succeeded);
        var package = Path.Combine(_temporaryRoot, "loot.orpmod");

        ModulePackager.Export(workspace, package);
        var validation = ModulePackageValidator.Validate(package);
        var loaded = ModulePackageLoader.Load(package, Path.Combine(_temporaryRoot, "loot-cache"));

        Assert.True(validation.IsValid, JoinDiagnostics(validation));
        var restored = Assert.IsType<CreatureLootProfile>(loaded.Project.Blueprints.Single().CreatureLoot);
        Assert.Equal(loot.Id, restored.Id);
        Assert.Equal(CreatureCorpseMode.RagdollContainer, restored.Corpse.Mode);
        Assert.Equal("warden-ember", Assert.Single(restored.Entries).ItemSlug);
    }

    [Fact]
    public void ExportValidateAndLoad_PreservesCanonicalCreatureProgression()
    {
        var source = TestProjectFactory.CreateValid();
        var original = source.Blueprints.Single();
        var progression = new CreatureProgressionProfile(
            Guid.Parse("a1350000-0000-4000-8000-000000000001"), 1, "author:test", "forge.generic", true, true,
            [new("warden", "Gardien", 3, 20, 2_400, ["ember-mastery"])],
            [new("mercy", "Impitoyable", "Miséricordieux", 0.5)],
            [new("lore", "Savoir", "insight", 2, 10, 2, true, false, 12)],
            [new("ember-mastery", "Maîtrise des braises", 1, 3, true,
                [new("track", "warden", 2)], ["ember-strike"], ["combat.on-hit"])],
            new("prepared", AbilityLoadoutMode.Prepared, 2, ["ember-strike"],
                [new("ember-strike", 1, 2, 0.5, "focus", 1, ["empowered"])]),
            new Dictionary<string, string> { ["xp-rate"] = "1.0" });
        var blueprint = new BlueprintDefinition(original.Id, original.Slug, original.DisplayName, original.Kind,
            original.Combat, original.PaletteOrigin, original.IsFavorite, original.Provenance,
            original.CreaturePresentation, progression);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, [blueprint], source.Areas,
            source.Dialogues, source.Quests);
        var workspace = Path.Combine(_temporaryRoot, $"progression-{Guid.NewGuid():N}");
        Assert.True(ProjectSession.Create(workspace, project).Succeeded);
        var package = Path.Combine(_temporaryRoot, "progression.orpmod");

        ModulePackager.Export(workspace, package);
        var validation = ModulePackageValidator.Validate(package);
        var loaded = ModulePackageLoader.Load(package, Path.Combine(_temporaryRoot, "progression-cache"));

        Assert.True(validation.IsValid, JoinDiagnostics(validation));
        var restored = Assert.IsType<CreatureProgressionProfile>(
            loaded.Project.Blueprints.Single().CreatureProgression);
        Assert.Equal(progression.Id, restored.Id);
        Assert.Equal("ember-strike", Assert.Single(restored.Loadout.PreparedAbilities).AbilityId);
    }

    [Fact]
    public void ExportValidateAndLoad_PreservesGovernedCreaturePresentation()
    {
        var source = TestProjectFactory.CreateValid();
        var portraitBytes = Encoding.UTF8.GetBytes("deterministic portrait fixture");
        var portraitHash = Convert.ToHexString(SHA256.HashData(portraitBytes)).ToLowerInvariant();
        var original = source.Blueprints.Single();
        var presentation = new CreaturePresentationProfile(
            Guid.Parse("a1330000-0000-4000-8000-000000000001"), 1, "author:test", "nonbinary", "humanoid-medium",
            new("iel", "iel", "son", "soi"),
            [new("neutral", "assets/portraits/warden.png", 256, 256, "CC0-1.0", portraitHash, "Gardien")],
            false, "rig.humanoid", [], [new("primary", "#7A5CFF")],
            [new("back", "wings", "asset.wings", "rig.humanoid")],
            new("voice.warden", "fr-FR", ["audio.warden"], ["calm"], "lipsync.fr-v1", ["a"], "CC0-1.0", "author-recorded"));
        var blueprint = new BlueprintDefinition(original.Id, original.Slug, original.DisplayName, original.Kind,
            original.Combat, original.PaletteOrigin, original.IsFavorite, original.Provenance, presentation);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, [blueprint], source.Areas,
            source.Dialogues, source.Quests);
        var workspace = Path.Combine(_temporaryRoot, $"presentation-{Guid.NewGuid():N}");
        var created = ProjectSession.Create(workspace, project);
        Assert.True(created.Succeeded, string.Join("; ", created.Diagnostics.Select(item => item.Message)));
        var portraitPath = Path.Combine(workspace, "assets", "portraits", "warden.png");
        Directory.CreateDirectory(Path.GetDirectoryName(portraitPath)!);
        File.WriteAllBytes(portraitPath, portraitBytes);
        var package = Path.Combine(_temporaryRoot, "presentation.orpmod");

        ModulePackager.Export(workspace, package);
        var validation = ModulePackageValidator.Validate(package);
        var loaded = ModulePackageLoader.Load(package, Path.Combine(_temporaryRoot, "presentation-cache"));

        Assert.True(validation.IsValid, JoinDiagnostics(validation));
        var restored = Assert.IsType<CreaturePresentationProfile>(loaded.Project.Blueprints.Single().CreaturePresentation);
        Assert.Equal(presentation.Id, restored.Id);
        Assert.True(File.Exists(Path.Combine(loaded.CachePath, "content", "assets", "portraits", "warden.png")));
    }

    [Fact]
    public void Export_RejectsMissingCreaturePortraitBeforeWritingPackage()
    {
        var source = TestProjectFactory.CreateValid();
        var original = source.Blueprints.Single();
        var profile = CreaturePresentationProfile.CreateDefault(
            Guid.Parse("a1330000-0000-4000-8000-000000000002"), "author:test");
        profile = new CreaturePresentationProfile(profile.Id, profile.SchemaVersion, profile.Provenance,
            profile.GenderId, profile.BodyTypeId, profile.Pronouns,
            [new("neutral", "assets/portraits/missing.png", 256, 256, "CC0-1.0", new string('a', 64), "Missing")],
            false, profile.RigId, profile.BodySlots, profile.MaterialColors, profile.Attachments, profile.Voice);
        var blueprint = new BlueprintDefinition(original.Id, original.Slug, original.DisplayName, original.Kind,
            original.Combat, original.PaletteOrigin, original.IsFavorite, original.Provenance, profile);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, [blueprint], source.Areas,
            source.Dialogues, source.Quests);
        var workspace = Path.Combine(_temporaryRoot, $"missing-presentation-{Guid.NewGuid():N}");
        Assert.True(ProjectSession.Create(workspace, project).Succeeded);
        var package = Path.Combine(_temporaryRoot, "missing-presentation.orpmod");

        var exception = Assert.Throws<InvalidOperationException>(() => ModulePackager.Export(workspace, package));

        Assert.Contains("missing or unsafe", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(package));
    }

    [Fact]
    public void ExportValidateAndLoad_RoundTripsAtomicallyThroughCache()
    {
        var workspace = CreateWorkspace();
        Directory.CreateDirectory(Path.Combine(workspace, ".forge"));
        File.WriteAllText(Path.Combine(workspace, ".forge", "editor-camera.json"), "author preference");
        var assetPath = Path.Combine(workspace, "assets", "imported", "test", "model.glb");
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        File.WriteAllBytes(assetPath, [1, 2, 3, 4]);
        var assetHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assetPath))).ToLowerInvariant();
        var assetId = AssetCatalogItemId.ForExternalId("test:packaged-model");
        var chunkId = Guid.Parse("b1100000-0000-4000-8000-000000000020");
        var assetManifest = new AssetPackageManifest(
            AssetPackageManifest.CurrentSchemaVersion,
            AssetPackageManifestId.ForAsset(assetId),
            assetId,
            "author:package-test",
            "CC0-1.0",
            new string('a', 64),
            new Dictionary<AssetTargetPlatform, long> { [AssetTargetPlatform.Windows] = 1024 },
            [new AssetManifestChunk(
                chunkId,
                AssetChunkRole.Model,
                true,
                "assets/imported/test/model.glb",
                assetHash,
                4,
                4,
                AssetChunkCompression.None,
                [AssetTargetPlatform.Windows])],
            null,
            null,
            null,
            new AssetLodManifest(false, 0.1m, [new(0, chunkId, 1m, 1000)]));
        Assert.True(AssetPackageManifestStore.Save(workspace, [assetManifest]).Succeeded);
        var package = Path.Combine(_temporaryRoot, "crypt-of-ashes.orpmod");

        var export = ModulePackager.Export(workspace, package);
        var validation = ModulePackageValidator.Validate(package);
        var firstLoad = ModulePackageLoader.Load(package, Path.Combine(_temporaryRoot, "cache"));
        var secondLoad = ModulePackageLoader.Load(package, Path.Combine(_temporaryRoot, "cache"));

        Assert.True(validation.IsValid, JoinDiagnostics(validation));
        Assert.Equal("crypt-of-ashes", validation.Manifest!.Slug);
        Assert.Equal("crypt-of-ashes", firstLoad.Project.Module.Slug);
        Assert.False(firstLoad.CacheHit);
        Assert.True(secondLoad.CacheHit);
        Assert.Equal(firstLoad.CachePath, secondLoad.CachePath);
        Assert.True(File.Exists(Path.Combine(firstLoad.CachePath, "content", "project.json")));
        Assert.True(File.Exists(Path.Combine(firstLoad.CachePath, "content", "scripts", "boss.forge")));
        Assert.True(File.Exists(Path.Combine(firstLoad.CachePath, "content", "assets", "asset-manifests.json")));
        Assert.True(File.Exists(Path.Combine(firstLoad.CachePath, "content", "assets", "imported", "test", "model.glb")));
        Assert.False(File.Exists(Path.Combine(firstLoad.CachePath, "content", ".forge", "editor-camera.json")));
        Assert.Equal(new FileInfo(package).Length, export.ArchiveBytes);
        Assert.Empty(Directory.EnumerateFiles(_temporaryRoot, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void Validate_RejectsPathTraversalFixture()
    {
        var package = ExportValidPackage();
        AddEntry(package, "../escape.json", "{}");

        var validation = ModulePackageValidator.Validate(package);

        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == ModulePackageValidator.UnsafePathCode);
    }

    [Fact]
    public void Validate_RejectsZipBombRatioFixture()
    {
        var package = ExportValidPackage();
        AddEntry(package, "content/assets/repetitive.txt", new string('0', 1024 * 1024));

        var validation = ModulePackageValidator.Validate(
            package,
            new ModulePackageLimits(MaximumCompressionRatio: 20));

        Assert.Contains(validation.Diagnostics, diagnostic =>
            diagnostic.Code == ModulePackageValidator.CompressionRatioCode);
    }

    [Fact]
    public void Validate_DefaultTotalBudgetIsOneGibibyteAndCustomLowerBudgetRemainsEnforced()
    {
        var package = ExportValidPackage();
        var defaults = new ModulePackageLimits();

        var validation = ModulePackageValidator.Validate(
            package,
            defaults with { MaximumTotalBytes = 1 });

        Assert.Equal(1L * 1024 * 1024 * 1024, defaults.MaximumTotalBytes);
        Assert.Contains(validation.Diagnostics, diagnostic =>
            diagnostic.Code == ModulePackageValidator.TotalSizeCode);
    }

    [Fact]
    public void Validate_RejectsNativeDllFixture()
    {
        var package = ExportValidPackage();
        AddEntry(package, "content/plugins/hostile.dll", "MZ-hostile");

        var validation = ModulePackageValidator.Validate(package);

        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == ModulePackageValidator.ForbiddenFileCode);
    }

    [Fact]
    public void Validate_AllowsStructurallyValidMp3ButStillRejectsNativeCommunityContent()
    {
        var package = ExportValidPackage();
        AddEntry(package, "content/audio/dialogues/voice.mp3", FakeMp3("community voice"));
        AddEntry(package, "content/plugins/hostile.dll", "MZ-hostile");

        var validation = ModulePackageValidator.Validate(package);

        Assert.DoesNotContain(validation.Diagnostics, diagnostic =>
            diagnostic.Code == ModulePackageValidator.ForbiddenFileCode &&
            diagnostic.Path == "content/audio/dialogues/voice.mp3");
        Assert.Contains(validation.Diagnostics, diagnostic =>
            diagnostic.Code == ModulePackageValidator.ForbiddenFileCode &&
            diagnostic.Path == "content/plugins/hostile.dll");
    }

    [Fact]
    public void Validate_RejectsTruncatedDialogueAudioEvenWhenExtensionIsAllowed()
    {
        var package = ExportValidPackage();
        AddEntry(package, "content/audio/dialogues/truncated.mp3",
            [(byte)'I', (byte)'D', (byte)'3', 0x04, 0, 0, 0, 0, 1, 0]);

        var validation = ModulePackageValidator.Validate(package);

        Assert.Contains(validation.Diagnostics, diagnostic =>
            diagnostic.Code == ModulePackageValidator.InvalidMediaCode &&
            diagnostic.Path == "content/audio/dialogues/truncated.mp3");
    }

    [Fact]
    public void Validate_AllowsOnlyGltfReferencedBinarySidecars()
    {
        var referencedPackage = ExportValidPackage();
        AddEntry(referencedPackage, "content/assets/model.gltf",
            "{\"asset\":{\"version\":\"2.0\"},\"buffers\":[{\"uri\":\"model.bin\",\"byteLength\":4}]}");
        AddEntry(referencedPackage, "content/assets/model.bin", "data");

        var referenced = ModulePackageValidator.Validate(referencedPackage);

        Assert.DoesNotContain(referenced.Diagnostics, diagnostic =>
            diagnostic.Code == ModulePackageValidator.ForbiddenFileCode &&
            diagnostic.Path == "content/assets/model.bin");

        var unreferencedPackage = ExportValidPackage();
        AddEntry(unreferencedPackage, "content/assets/hostile.bin", "data");

        var unreferenced = ModulePackageValidator.Validate(unreferencedPackage);

        Assert.Contains(unreferenced.Diagnostics, diagnostic =>
            diagnostic.Code == ModulePackageValidator.ForbiddenFileCode &&
            diagnostic.Path == "content/assets/hostile.bin");
    }

    [Fact]
    public void Validate_RejectsHashMismatchFixture()
    {
        var package = ExportValidPackage();
        ReplaceEntry(package, "content/scripts/boss.forge", "void main() { say(\"tampered\"); }");

        var validation = ModulePackageValidator.Validate(package);

        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == ModulePackageValidator.HashMismatchCode);
    }

    [Fact]
    public void Validate_RejectsDuplicateBlueprintIdsFixture()
    {
        var package = ExportValidPackage();
        using (var archive = ZipFile.Open(package, ZipArchiveMode.Update))
        {
            var projectEntry = archive.GetEntry("content/project.json")!;
            string json;
            using (var reader = new StreamReader(projectEntry.Open()))
            {
                json = reader.ReadToEnd();
            }

            projectEntry.Delete();
            json = json.Replace(
                "10000000-0000-4000-8000-000000000002",
                "10000000-0000-4000-8000-000000000001",
                StringComparison.Ordinal);
            var replacement = archive.CreateEntry("content/project.json", CompressionLevel.Optimal);
            using var writer = new StreamWriter(replacement.Open(), new UTF8Encoding(false));
            writer.Write(json);
        }

        var validation = ModulePackageValidator.Validate(package);

        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == ModulePackageValidator.InvalidProjectCode);
    }

    [Fact]
    public void ShippingExport_BlocksUnknownLicensesBeforeWritingAnArchive()
    {
        var package = Path.Combine(_temporaryRoot, "blocked-shipping.orpmod");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ModulePackager.Export(CreateWorkspace(), package, target: ModuleTargetKind.Shipping));

        Assert.Contains(DistributionComplianceValidator.ModuleLicenseUnknownCode, exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(package));
    }

    [Fact]
    public void ShippingExport_EmitsCanonicalNoticeAndApprovedDependencyManifest()
    {
        var workspace = CreateShippingWorkspace();
        var package = Path.Combine(_temporaryRoot, "approved-shipping.orpmod");

        var export = ModulePackager.Export(workspace, package, target: ModuleTargetKind.Shipping);
        var validation = ModulePackageValidator.Validate(package);
        using var archive = ZipFile.OpenRead(package);
        var noticeEntry = archive.GetEntry(DistributionNoticeBuilder.PackagePath);
        using var reader = new StreamReader(Assert.IsType<ZipArchiveEntry>(noticeEntry).Open());
        var notice = reader.ReadToEnd();

        Assert.True(validation.IsValid, JoinDiagnostics(validation));
        Assert.Equal(ModuleTargetKind.Shipping, export.Manifest.Target);
        Assert.Equal("Approved library", Assert.Single(export.Manifest.ThirdPartyDependencies).Name);
        Assert.Contains("Approved library 1.4.0 | MIT", notice, StringComparison.Ordinal);
        Assert.Contains("Update cadence: quarterly", notice, StringComparison.Ordinal);
        Assert.Contains("Removal plan:", notice, StringComparison.Ordinal);
    }

    [Fact]
    public void R050_NoticeAggregatesDataTablesPackagesSourcesAndAssetsDeterministically()
    {
        var source = TestProjectFactory.CreateValid();
        var table = new DataTableDefinition(
            DataTableId.From(Guid.Parse("d7130000-0000-4000-8000-000000000020")), "credits", "Credits",
            [new(DataColumnId.From(Guid.Parse("d7130000-0000-4000-8000-000000000021")), "name", "Name",
                DataColumnKind.Text, true)],
            [new(DataRowId.From(Guid.Parse("d7130000-0000-4000-8000-000000000022")),
                new Dictionary<string, string> { ["name"] = "Author" }, "author:r050")],
            provenance: "source:r050", licenseSpdx: "CC-BY-4.0", contentHash: new string('a', 64));
        var package = new ContentPackageDefinition(Guid.Parse("d7130000-0000-4000-8000-000000000023"), 1,
            "package:r050", "third.party.art", "1.0.0", "MIT", new string('b', 64), 5, [], [],
            [new(Guid.Parse("d7130000-0000-4000-8000-000000000024"), 1, "source:r050", "script:intro",
                "scripts/intro.forge", ContentSourceKind.ForgeScript, true, "Apache-2.0", new string('c', 64), 1024)],
            [new(Guid.Parse("d7130000-0000-4000-8000-000000000025"), 1, "asset:r050", "model:statue",
                "assets/statue.glb", true, "CC0-1.0", new string('d', 64), 2048)]);
        var packages = new ContentPackageCatalog(source.Module.Id.Value, 1, "catalog:r050",
            ContentConflictPolicy.HighestPriority, [package]);
        var existing = source.Module.Content;
        var content = new ContentWorkspaceConfiguration(existing.Id, existing.Batch, [table], existing.MergePolicy,
            existing.Provenance, packages: packages);
        var module = new ModuleDefinition(source.Module.Id, source.Module.Slug, source.Module.DisplayName,
            source.Module.Description, source.Module.EntryAreaId, source.Module.Metadata, source.Module.Variables,
            source.Module.Distribution, source.Module.Localization, content, source.Module.ArtisticStyle,
            source.Module.RuntimePolicies, source.Module.Factions, source.Module.WorldFeatures);
        var project = new ProjectDefinition(source.Id, source.Title, module, source.Blueprints, source.Areas,
            source.Dialogues, source.Quests);

        var first = DistributionNoticeBuilder.Build(project, ModuleTargetKind.Shipping);
        var second = DistributionNoticeBuilder.Build(project, ModuleTargetKind.Shipping);

        Assert.Equal(first, second);
        Assert.Contains("Credits | CC-BY-4.0 | provenance=source:r050", first, StringComparison.Ordinal);
        Assert.Contains("third.party.art 1.0.0 | MIT | provenance=package:r050", first, StringComparison.Ordinal);
        Assert.Contains("source:script:intro | Apache-2.0", first, StringComparison.Ordinal);
        Assert.Contains("asset:model:statue | CC0-1.0", first, StringComparison.Ordinal);
    }

    [Fact]
    public void R050_ShippingExportBlocksUnknownContentPackageLicenseBeforeWritingArchive()
    {
        var workspace = CreateShippingWorkspace(includeUnknownPackage: true);
        var package = Path.Combine(_temporaryRoot, "blocked-content-license.orpmod");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ModulePackager.Export(workspace, package, target: ModuleTargetKind.Shipping));

        Assert.Contains(DistributionComplianceValidator.ContentPackageLicenseUnknownCode, exception.Message,
            StringComparison.Ordinal);
        Assert.False(File.Exists(package));
    }

    [Fact]
    public void R050_ValidatorRejectsNoticeDivergenceFromCanonicalProject()
    {
        var workspace = CreateShippingWorkspace();
        var package = Path.Combine(_temporaryRoot, "tampered-notice.orpmod");
        ModulePackager.Export(workspace, package, target: ModuleTargetKind.Shipping);
        ReplaceEntry(package, DistributionNoticeBuilder.PackagePath, "tampered notice");

        var result = ModulePackageValidator.Validate(package);

        Assert.Contains(result.Diagnostics, item => item.Code == ModulePackageValidator.NoticeMismatchCode);
    }

    [Fact]
    public void ShippingExport_BlocksPendingQuarantineBeforeWritingArchive()
    {
        var workspace = CreateShippingWorkspace();
        var record = CreateGovernanceRecord() with
        {
            Quarantine = CreateGovernanceRecord().Quarantine with
            {
                Status = AssetQuarantineStatus.Pending,
                Reviewer = string.Empty,
                ReviewedAtUtc = null,
            },
        };
        Assert.True(AssetGovernanceStore.Save(workspace, [record]).Succeeded);
        var package = Path.Combine(_temporaryRoot, "pending-governance.orpmod");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ModulePackager.Export(workspace, package, target: ModuleTargetKind.Shipping));

        Assert.Contains(AssetGovernanceValidator.QuarantineApprovalRequiredCode, exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(package));
    }

    [Fact]
    public void ShippingExport_NeverRedistributesNwnOrCepProprietaryAssets()
    {
        var workspace = CreateShippingWorkspace();
        var record = CreateGovernanceRecord() with
        {
            IpBoundary = new AssetIpBoundary(AssetOriginKind.ProprietaryNwn, false, true, "legal-review:R018"),
        };
        Assert.True(AssetGovernanceStore.Save(workspace, [record]).Succeeded);
        var package = Path.Combine(_temporaryRoot, "proprietary-governance.orpmod");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ModulePackager.Export(workspace, package, target: ModuleTargetKind.Shipping));

        Assert.Contains(AssetGovernanceValidator.ProprietaryRedistributionCode, exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(package));
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot))
        {
            Directory.Delete(_temporaryRoot, true);
        }
    }

    private string ExportValidPackage()
    {
        var package = Path.Combine(_temporaryRoot, $"{Guid.NewGuid():N}.orpmod");
        ModulePackager.Export(CreateWorkspace(), package);
        return package;
    }

    private string CreateWorkspace()
    {
        var workspace = Path.Combine(_temporaryRoot, $"workspace-{Guid.NewGuid():N}");
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "crypt-of-ashes");
        Directory.CreateDirectory(Path.Combine(workspace, "scripts"));
        Directory.CreateDirectory(Path.Combine(workspace, "areas"));
        File.Copy(Path.Combine(fixture, "forge.project.json"), Path.Combine(workspace, "forge.project.json"));
        File.Copy(Path.Combine(fixture, "project.json"), Path.Combine(workspace, "project.json"));
        foreach (var areaDocument in Directory.EnumerateFiles(Path.Combine(fixture, "areas"), "*.json"))
        {
            File.Copy(areaDocument, Path.Combine(workspace, "areas", Path.GetFileName(areaDocument)));
        }
        File.WriteAllText(
            Path.Combine(workspace, "scripts", "boss.forge"),
            "void main() { say(\"Braise\"); }",
            new UTF8Encoding(false));
        return workspace;
    }

    private string CreateShippingWorkspace(bool includeUnknownPackage = false)
    {
        var source = TestProjectFactory.CreateValid();
        var metadata = source.Module.Metadata;
        var approvedMetadata = new ProjectMetadata(
            metadata.Id,
            metadata.DefaultLocale,
            metadata.Localizations,
            metadata.Author,
            metadata.Version,
            "MIT",
            metadata.ImagePath,
            metadata.Tags,
            metadata.Classification,
            "test-approved");
        var blueprints = source.Blueprints.Select(blueprint => new BlueprintDefinition(
            blueprint.Id,
            blueprint.Slug,
            blueprint.DisplayName,
            blueprint.Kind,
            blueprint.Combat,
            blueprint.PaletteOrigin,
            blueprint.IsFavorite,
            new BlueprintProvenance(
                BlueprintProvenance.CurrentSchemaVersion,
                $"project://blueprints/{blueprint.Id}/{blueprint.Slug}",
                "Forge team",
                "CC0-1.0",
                new string('a', 64)))).ToArray();
        var defaults = source.Module.Distribution;
        var distribution = new ModuleDistributionConfiguration(
            defaults.Id,
            defaults.TargetProfiles,
            [new ThirdPartyDependencyApproval(
                ThirdPartyDependencyId.From(Guid.Parse("d7130000-0000-4000-8000-000000000011")),
                "Approved library",
                "1.4.0",
                "MIT",
                "Forge team",
                "Required deterministic codec.",
                "quarterly",
                "Replace with the internal codec.",
                "https://example.invalid/approved-library",
                [ModuleTargetKind.Shipping],
                "test")],
            "test");
        var content = source.Module.Content;
        if (includeUnknownPackage)
        {
            var packages = new ContentPackageCatalog(source.Module.Id.Value, 1, "test:r050",
                ContentConflictPolicy.HighestPriority,
                [new(Guid.Parse("d7130000-0000-4000-8000-000000000030"), 1, "test:r050",
                    "unknown.license", "1.0.0", "BUSL-1.1", new string('e', 64), 1, [], [], [], [])]);
            content = new ContentWorkspaceConfiguration(content.Id, content.Batch, content.DataTables,
                content.MergePolicy, content.Provenance, packages: packages);
        }
        var module = new ModuleDefinition(
            source.Module.Id,
            source.Module.Slug,
            source.Module.DisplayName,
            source.Module.Description,
            source.Module.EntryAreaId,
            approvedMetadata,
            source.Module.Variables,
            distribution,
            content: content);
        var project = new ProjectDefinition(
            source.Id,
            source.Title,
            module,
            blueprints,
            source.Areas,
            source.Dialogues,
            source.Quests);
        var workspace = Path.Combine(_temporaryRoot, $"shipping-{Guid.NewGuid():N}");
        var created = ProjectSession.Create(workspace, project);
        Assert.True(created.Succeeded, string.Join("; ", created.Diagnostics.Select(item => item.Message)));
        return workspace;
    }

    private static AssetGovernanceRecord CreateGovernanceRecord()
    {
        var assetId = AssetCatalogItemId.ForExternalId("test:shipping-governance");
        return new(1, AssetGovernanceId.ForAsset(assetId), assetId, AssetPackageManifestId.ForAsset(assetId),
            "CC0-1.0", new string('e', 64), "Author", "https://example.invalid/shipping-asset",
            "permission:shipping-fixture", "None.", false, string.Empty, string.Empty, 4096,
            new("incoming/model.glb", "model/gltf-binary", "model/gltf-binary", 12, 4096, "scanner", "clean",
                "valid", AssetQuarantineStatus.Approved, "reviewer", DateTimeOffset.UnixEpoch),
            new(AssetOriginKind.OpenLicensed, true, false, string.Empty));
    }

    private static void AddEntry(string package, string path, string content)
    {
        using var archive = ZipFile.Open(package, ZipArchiveMode.Update);
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static void AddEntry(string package, string path, byte[] content)
    {
        using var archive = ZipFile.Open(package, ZipArchiveMode.Update);
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(content);
    }

    private static byte[] FakeMp3(string payload)
    {
        var frame = new byte[417];
        frame[0] = 0xff;
        frame[1] = 0xfb;
        frame[2] = 0x90;
        frame[3] = 0x00;
        var text = Encoding.UTF8.GetBytes(payload);
        text.AsSpan(0, Math.Min(text.Length, frame.Length - 4)).CopyTo(frame.AsSpan(4));
        return frame;
    }

    private static void ReplaceEntry(string package, string path, string content)
    {
        using var archive = ZipFile.Open(package, ZipArchiveMode.Update);
        archive.GetEntry(path)!.Delete();
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string JoinDiagnostics(ModulePackageValidationResult validation) =>
        string.Join(Environment.NewLine, validation.Diagnostics.Select(diagnostic => diagnostic.Message));

    private static CreatureEventProfile CreateCreatureEvents()
    {
        static EventBinding Binding(string id, string eventName, string handler) => new(
            Guid.Parse(id), 1, "author:test", "1.0", eventName, handler, EventAuthorityScope.Server);

        return new CreatureEventProfile(
            Guid.Parse("a1550000-0000-4000-8000-000000000001"), 1, "author:test", true,
            new(CreatureCombatBoundaryMode.TurnEnded,
                Binding("a1550000-0000-4000-8000-000000000002", "creature.turn_ended", "OnTurnEnded")),
            new(
                Binding("a1550000-0000-4000-8000-000000000003", "creature.inventory_accessed", "OnInventoryAccessed"),
                Binding("a1550000-0000-4000-8000-000000000004", "creature.item_transferred", "OnItemTransferred")),
            new(Binding("a1550000-0000-4000-8000-000000000005", "creature.rest_completed", "OnRestCompleted")),
            new(true, 2_000, 8, true,
                Binding("a1550000-0000-4000-8000-000000000006", "creature.heartbeat", "OnHeartbeat")),
            [new(Guid.Parse("a1550000-0000-4000-8000-000000000010"), "crypt", "braziers_lit", 2,
                new Dictionary<string, CreatureCustomFieldType> { ["count"] = CreatureCustomFieldType.Integer },
                true, Binding("a1550000-0000-4000-8000-000000000011",
                    "custom.crypt.braziers_lit", "OnBraziersLit"))],
            new Dictionary<string, string>());
    }
}
