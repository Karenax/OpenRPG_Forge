using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Items;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Placeables;
using Forge.RpgKit.Domain.Placeables;
using DomainVector3 = Forge.Domain.Spatial.Vector3Value;
using RuntimeLootTiming = Forge.RpgKit.Domain.Placeables.LootSpawnTiming;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    internal StudioUx1SSmokeResult RunUx1SContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux1s-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1af0000-0000-4000-8000-000000000001")),
                "coffre-des-sylves",
                "Coffre des sylves",
                BlueprintKind.Placeable);
            var neighborBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1af0000-0000-4000-8000-000000000002")),
                "borne-voisine",
                "Borne voisine",
                BlueprintKind.Placeable);
            var herb = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1af0000-0000-4000-8000-000000000003")),
                "herbe-lunaire",
                "Herbe lunaire",
                BlueprintKind.Item);
            var gem = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1af0000-0000-4000-8000-000000000004")),
                "gemme-de-rosée",
                "Gemme de rosée",
                BlueprintKind.Item);
            var first = new EntityInstance(
                EntityId.From(Guid.Parse("c1af0000-0000-4000-8000-000000000005")),
                blueprint.Id,
                new TransformValue(DomainVector3.Zero, QuaternionValue.Identity, DomainVector3.One));
            var second = new EntityInstance(
                EntityId.From(Guid.Parse("c1af0000-0000-4000-8000-000000000006")),
                blueprint.Id,
                new TransformValue(new(5, 0, 2), QuaternionValue.Identity, DomainVector3.One));
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("c1af0000-0000-4000-8000-000000000007")),
                "atelier-tresors",
                "Atelier des trésors",
                [first, second],
                AreaKind.Interior,
                new AreaDimensions(32, 32));
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1af0000-0000-4000-8000-000000000008")),
                "Studio UX1-S",
                [blueprint, neighborBlueprint, herb, gem],
                [area]);
            var originalProfile = CreatePlaceableTransformParent(
                blueprint,
                Guid.Parse("c1af0000-0000-4000-8000-000000000010"),
                "test:ux1-s-profile");
            var hiddenTrap = new PlaceableTrapProfile(
                Guid.Parse("c1af0000-0000-4000-8000-000000000011"),
                "interaction:open",
                "damage:old-thorns",
                8,
                9,
                20,
                "anciens-gardiens",
                "vfx:old-thorns",
                "anciens-gardiens",
                true,
                false);
            var originalLoot = new PlaceableLootSpawnerProfile(
                Guid.Parse("c1af0000-0000-4000-8000-000000000012"),
                Guid.Parse("c1af0000-0000-4000-8000-000000000013"),
                PlaceableAuthoredLootSpawnTiming.Open,
                17,
                true,
                [new(herb.Id.Value, 2, 1, "plantes")],
                3,
                new ItemLootTablePolicy(
                    Guid.Parse("c1af0000-0000-4000-8000-000000000013"),
                    1,
                    "test:ux1-s-loot",
                    [
                        new(herb.Id.Value, "plantes", 2, 1, 2, 1, string.Empty, false),
                        new(gem.Id.Value, "rares", 1, 1, 1, 2, string.Empty, true),
                    ]));
            originalProfile = originalProfile with
            {
                Foundation = originalProfile.Foundation! with
                {
                    Components = originalProfile.Foundation.Components with { HasTrap = true },
                },
                SecurityEvents = new(
                    Guid.Parse("c1af0000-0000-4000-8000-000000000014"),
                    1,
                    "test:ux1-s-security-events",
                    Trap: hiddenTrap,
                    Loot: originalLoot),
                SecurityEventsRequiredAtRuntime = true,
            };
            var neighbor = CreatePlaceableTransformParent(
                neighborBlueprint,
                Guid.Parse("c1af0000-0000-4000-8000-000000000015"),
                "test:ux1-s-neighbor");
            var catalog = new WorldFeatureCatalog(
                project.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-s-catalog",
                placeables: [originalProfile, neighbor]);
            project = new ProjectDefinition(
                project.Id,
                project.Title,
                project.Module.WithWorldFeatures(catalog),
                project.Blueprints,
                project.Areas);
            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded)
            {
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1S_PROJECT_CREATE_FAILED diagnostics=" +
                                  string.Join(" | ", created.Diagnostics.Select(item =>
                                      $"{item.Code}:{item.Message}")));
                return FailedUx1SSmoke();
            }
            var opened = editing.Open(tempRoot);
            if (!opened.Succeeded)
            {
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1S_PROJECT_OPEN_FAILED diagnostics=" +
                                  string.Join(" | ", opened.Diagnostics.Select(item =>
                                      $"{item.Code}:{item.Message}")));
                return FailedUx1SSmoke();
            }

            var originalProject = editing.Project!;
            var loadedOriginal = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var loadedNeighbor = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == neighborBlueprint.Id);
            var sourceDraft = editing.CreatePlaceableDraft(blueprint.Id)!;
            var sourceLootDraft = sourceDraft.Loot!;
            var unchangedBuilt = editing.TryBuildPlaceable(
                sourceDraft,
                dryRun: true,
                out _,
                out var unchangedProfile,
                out _);
            var generatedStatePreserved = unchangedBuilt &&
                                          ReferenceEquals(
                                              loadedOriginal.SecurityEvents!.Loot,
                                              unchangedProfile!.SecurityEvents!.Loot) &&
                                          unchangedProfile.SecurityEvents.Loot!.Generation == 3 &&
                                          unchangedProfile.SecurityEvents.Loot.SavedResults.Count == 1;

            var selectedEntryId = sourceLootDraft.Entries[1].Id;
            var editedEntries = sourceLootDraft.Entries.Select(entry => entry.Id == selectedEntryId
                ? entry with
                {
                    Weight = 3,
                    MinimumQuantity = 1,
                    MaximumQuantity = 2,
                    MinimumQuality = 3,
                }
                : entry).ToArray();
            var stableEntrySelection = editedEntries[0] == sourceLootDraft.Entries[0] &&
                                       editedEntries.Single(entry => entry.Id == selectedEntryId).Weight == 3 &&
                                       editedEntries.Select(entry => entry.Id).SequenceEqual(
                                           sourceLootDraft.Entries.Select(entry => entry.Id));
            var changedLootDraft = sourceLootDraft with
            {
                Timing = PlaceableAuthoredLootSpawnTiming.Reset,
                Seed = 4242,
                PersistGeneratedState = true,
                Entries = editedEntries,
            };
            var guardedDraft = sourceDraft with
            {
                HasLoot = true,
                Loot = changedLootDraft,
                RequireLootRuntime = true,
            };
            var resetGuardResult = editing.PreviewPlaceable(guardedDraft);
            var draft = guardedDraft with
            {
                Loot = changedLootDraft with { ResetGeneratedState = true },
            };
            var missingItem = BlueprintId.From(Guid.Parse("c1af0000-0000-4000-8000-000000000099"));
            var invalidResults = new[]
            {
                editing.PreviewPlaceable(draft with { Loot = draft.Loot! with { Entries = [] } }),
                editing.PreviewPlaceable(draft with
                {
                    Loot = draft.Loot! with
                    {
                        Entries = [editedEntries[0] with { ItemBlueprintId = missingItem }],
                    },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Loot = draft.Loot! with { Entries = [editedEntries[0] with { Group = string.Empty }] },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Loot = draft.Loot! with { Entries = [editedEntries[0] with { Weight = 0 }] },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Loot = draft.Loot! with
                    {
                        Entries = [editedEntries[0] with { MinimumQuantity = 0 }],
                    },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Loot = draft.Loot! with
                    {
                        Entries = [editedEntries[0] with { MinimumQuantity = 3, MaximumQuantity = 2 }],
                    },
                }),
                editing.PreviewPlaceable(draft with { HasLoot = false, RequireLootRuntime = true }),
            };
            var invalidBlocked = invalidResults.All(result => !result.Succeeded);
            var humanDiagnostics = !resetGuardResult.Succeeded &&
                                   IsHumanPlaceableMessage(resetGuardResult.Message) &&
                                   invalidBlocked && invalidResults.All(result =>
                                       IsHumanPlaceableMessage(result.Message));

            var directBuilt = editing.TryBuildPlaceable(
                draft,
                dryRun: true,
                out _,
                out var directProfile,
                out _);
            var preview = editing.PreviewPlaceable(draft);
            var lootPreview = editing.PreviewPlaceableLoot(draft, StudioPlaceableLootPreviewMode.Reset);
            var dryRun = preview.Succeeded && preview.CanApply &&
                         preview.Summary.Contains("Trésor initial", StringComparison.Ordinal) &&
                         ReferenceEquals(originalProject, editing.Project);
            var instanceSelection = preview.InstanceIds.SequenceEqual([first.Id, second.Id]);
            var applied = editing.ApplyApprovedPlaceable();
            var appliedProject = editing.Project!;
            var profile = appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var loot = profile.SecurityEvents?.Loot;
            var tableEntries = loot is { LootTable.Entries.Count: 2 } &&
                               loot.LootTable.Entries[0].ItemDefinitionId == herb.Id.Value &&
                               loot.LootTable.Entries[1].ItemDefinitionId == gem.Id.Value &&
                               loot.LootTable.Entries[1].Weight == 3;
            var timingAndSeed = loot is
            {
                Timing: PlaceableAuthoredLootSpawnTiming.Reset,
                Seed: 4242,
                PersistGeneratedState: true,
            };
            var explicitResetGuard = !resetGuardResult.Succeeded && loot is
            {
                Generation: 0,
                SavedResults.Count: 0,
            };
            var commandParity = directBuilt &&
                                directProfile?.LootRequiredAtRuntime == profile.LootRequiredAtRuntime &&
                                LootValuesEqual(directProfile?.SecurityEvents?.Loot, loot);
            var preservation = profile.Id == loadedOriginal.Id &&
                               profile.Provenance == loadedOriginal.Provenance &&
                               profile.Foundation?.Id == loadedOriginal.Foundation?.Id &&
                               profile.Operations == loadedOriginal.Operations &&
                               profile.SecurityEvents?.Id == loadedOriginal.SecurityEvents?.Id &&
                               profile.SecurityEvents?.Provenance == loadedOriginal.SecurityEvents?.Provenance &&
                               profile.SecurityEventsRequiredAtRuntime ==
                               loadedOriginal.SecurityEventsRequiredAtRuntime &&
                               ReferenceEquals(profile.SecurityEvents?.Trap, loadedOriginal.SecurityEvents?.Trap) &&
                               profile.SecurityEvents?.ContainerEvents is null &&
                               profile.SecurityEvents?.LockEvents is null &&
                               profile.SecurityEvents?.ContentsChanged is null &&
                               ReferenceEquals(profile.Reactive, loadedOriginal.Reactive) &&
                               ReferenceEquals(
                                   appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                                       item.BlueprintId == neighborBlueprint.Id),
                                   loadedNeighbor) &&
                               appliedProject.Blueprints.SequenceEqual(originalProject.Blueprints);

            var adapter = new PlaceableSecurityEventsRuntimeAdapter();
            var resolution = adapter.ResolveLoot(profile);
            var runtime = adapter.BuildLoot(resolution.Handle!, profile);
            var skipped = adapter.Spawn(resolution.Handle!, profile, RuntimeLootTiming.Open,
                new HashSet<string>());
            var spawned = adapter.Spawn(resolution.Handle!, profile, RuntimeLootTiming.Reset,
                new HashSet<string>());
            var deterministic = adapter.Spawn(resolution.Handle!, profile, RuntimeLootTiming.Reset,
                new HashSet<string>());
            var restored = adapter.Spawn(resolution.Handle!, profile, RuntimeLootTiming.Reset,
                new HashSet<string>(), spawned.Spawner);
            var runtimeProjection = applied.Succeeded && resolution.Available &&
                                    profile.LootRequiredAtRuntime && runtime.LootSpawner.Seed == 4242 &&
                                    runtime.LootSpawner.Timing == RuntimeLootTiming.Reset &&
                                    skipped.EventName == "LootSpawnSkipped" &&
                                    spawned.EventName == "LootSpawned" &&
                                    spawned.Results.SequenceEqual(deterministic.Results) &&
                                    restored.ReusedSavedState &&
                                    restored.Results.SequenceEqual(spawned.Results);
            var lootPreviewResolved = lootPreview.Succeeded &&
                                      lootPreview.EventName == "LootSpawned" &&
                                      lootPreview.Results.Count > 0 &&
                                      lootPreview.Message.Contains("Aperçu", StringComparison.OrdinalIgnoreCase);

            var scriptHandle = new WorldFeatureScriptService().Query(
                editing.History!.State,
                "placeable",
                profile.Id.ToString("D"),
                1).Single();
            var scriptRead = scriptHandle.Properties["lootTableId"] == loot?.LootTableId.ToString("D") &&
                             scriptHandle.Properties["lootTiming"] == "Reset" &&
                             scriptHandle.Properties["lootSeed"] == "4242" &&
                             scriptHandle.Properties["lootPersistGeneratedState"] == "True" &&
                             scriptHandle.Properties["lootGeneration"] == "0" &&
                             scriptHandle.Properties["lootEntries"] == "2" &&
                             scriptHandle.Properties["lootTableEntries"].Contains(
                                 gem.Id.Value.ToString("D"), StringComparison.Ordinal) &&
                             scriptHandle.Properties["lootRequiredAtRuntime"] == "True";
            var optionalProfile = profile with
            {
                SecurityEvents = profile.SecurityEvents! with { Loot = null },
                LootRequiredAtRuntime = false,
            };
            var requiredProfile = optionalProfile with { LootRequiredAtRuntime = true };
            var optional = adapter.ResolveLoot(optionalProfile);
            var required = adapter.ResolveLoot(requiredProfile);
            var optionalRequired = optional.OptionalFallback && optional.DiagnosticCode is null &&
                                   !required.Available &&
                                   required.DiagnosticCode == "WORLD_PLACEABLE_LOOT_REQUIRED_MISSING";

            var undo = editing.Undo();
            var undone = ReferenceEquals(originalProject, editing.Project);
            var redo = editing.Redo();
            var redone = ReferenceEquals(appliedProject, editing.Project);
            var undoRedo = undo.Succeeded && undone && redo.Succeeded && redone;
            var save = editing.Save();
            editing.Close();
            var reopen = reopened.Open(tempRoot);
            var persisted = reopened.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
                item.BlueprintId == blueprint.Id);
            var persistedNeighbor = reopened.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
                item.BlueprintId == neighborBlueprint.Id);
            var persistence = save.Succeeded && reopen.Succeeded && persisted is not null &&
                              persisted.LootRequiredAtRuntime &&
                              LootValuesEqual(persisted.SecurityEvents?.Loot, loot) &&
                              persisted.SecurityEventsRequiredAtRuntime &&
                              persisted.SecurityEvents?.Trap?.PayloadId == "damage:old-thorns" &&
                              persisted.Operations is null &&
                              persistedNeighbor?.Id == neighbor.Id &&
                              reopened.Project?.Blueprints.Count == 4;
            var succeeded = tableEntries && timingAndSeed && generatedStatePreserved &&
                            explicitResetGuard && lootPreviewResolved && dryRun && commandParity &&
                            instanceSelection && preservation && runtimeProjection && scriptRead &&
                            optionalRequired && stableEntrySelection && undoRedo && persistence &&
                            invalidBlocked && humanDiagnostics;
            return new(
                succeeded,
                tableEntries,
                timingAndSeed,
                generatedStatePreserved,
                explicitResetGuard,
                lootPreviewResolved,
                dryRun,
                commandParity,
                instanceSelection,
                preservation,
                runtimeProjection,
                scriptRead,
                optionalRequired,
                stableEntrySelection,
                undoRedo,
                persistence,
                invalidBlocked,
                humanDiagnostics);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1S_CONTRACT_EXCEPTION type={exception.GetType().Name} message={exception.Message}");
            return FailedUx1SSmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    internal StudioPlaceableLootPreview PreviewPlaceableLoot(
        StudioPlaceableDraft draft,
        StudioPlaceableLootPreviewMode mode)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!draft.HasLoot)
        {
            return new(false,
                "Trésor initial désactivé · activez-le avant l’aperçu.",
                "WORLD_PLACEABLE_LOOT_DISABLED",
                "LootUnavailable",
                false,
                draft.Loot?.Generation ?? 0,
                []);
        }
        if (!TryBuildPlaceable(draft, dryRun: true, out _, out var profile, out var message) ||
            profile?.SecurityEvents?.Loot is null)
        {
            return new(false,
                message,
                LastPlaceableDiagnosticCode,
                "LootInvalid",
                false,
                draft.Loot?.Generation ?? 0,
                []);
        }

        var adapter = new PlaceableSecurityEventsRuntimeAdapter();
        var resolution = adapter.ResolveLoot(profile);
        if (!resolution.Available || resolution.Handle is null)
        {
            return new(false,
                HumanizePlaceable(resolution.DiagnosticCode),
                resolution.DiagnosticCode ?? "WORLD_PLACEABLE_LOOT_UNAVAILABLE",
                "LootUnavailable",
                false,
                profile.SecurityEvents.Loot.Generation,
                []);
        }

        var timing = mode switch
        {
            StudioPlaceableLootPreviewMode.Initialize => RuntimeLootTiming.Initialize,
            StudioPlaceableLootPreviewMode.Open => RuntimeLootTiming.Open,
            StudioPlaceableLootPreviewMode.Reset => RuntimeLootTiming.Reset,
            _ => RuntimeLootTiming.Open,
        };
        var result = adapter.Spawn(resolution.Handle, profile, timing, new HashSet<string>());
        var authoredResults = result.Results.Select(item => new PlaceableSavedLootResultProfile(
            item.ItemDefinitionId,
            item.Quantity,
            item.Quality,
            item.Group)).ToArray();
        var decision = result.EventName switch
        {
            "LootSpawnSkipped" => "aucun tirage : ce n’est pas le moment configuré",
            "LootSpawnRestored" => "l’état généré existant est restauré",
            "LootSpawned" => $"{authoredResults.Length} résultat(s) déterministe(s) sont tirés",
            _ => "le trésor est évalué",
        };
        return new(
            true,
            $"Aperçu · {decision} · génération {result.Spawner.Generation} · aucun module modifié.",
            $"RPG_PLACEABLE_{result.EventName.ToUpperInvariant()}",
            result.EventName,
            result.ReusedSavedState,
            result.Spawner.Generation,
            authoredResults);
    }

    private static bool LootValuesEqual(
        PlaceableLootSpawnerProfile? left,
        PlaceableLootSpawnerProfile? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return LootConfigurationValuesEqual(left, right) &&
               left.Generation == right.Generation &&
               left.SavedResults.SequenceEqual(right.SavedResults);
    }

    private static string HumanizeLootTiming(PlaceableAuthoredLootSpawnTiming timing) => timing switch
    {
        PlaceableAuthoredLootSpawnTiming.Initialize => "au chargement",
        PlaceableAuthoredLootSpawnTiming.Open => "à l’ouverture",
        PlaceableAuthoredLootSpawnTiming.Reset => "à la réinitialisation",
        _ => "au moment configuré",
    };

    private static StudioUx1SSmokeResult FailedUx1SSmoke() => new(
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false);
}
