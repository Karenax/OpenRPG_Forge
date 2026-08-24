using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Placeables;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    internal StudioUx1LSmokeResult RunUx1LContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux1l-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1a80000-0000-4000-8000-000000000001")),
                "balise-destructible",
                "Balise destructible",
                BlueprintKind.Placeable);
            var neighborBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1a80000-0000-4000-8000-000000000002")),
                "socle-protege",
                "Socle protégé",
                BlueprintKind.Placeable);
            var first = new EntityInstance(
                EntityId.From(Guid.Parse("c1a80000-0000-4000-8000-000000000003")),
                blueprint.Id,
                new TransformValue(Vector3Value.Zero, QuaternionValue.Identity, Vector3Value.One));
            var second = new EntityInstance(
                EntityId.From(Guid.Parse("c1a80000-0000-4000-8000-000000000004")),
                blueprint.Id,
                new TransformValue(new(4, 0, 2), QuaternionValue.Identity, Vector3Value.One));
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("c1a80000-0000-4000-8000-000000000005")),
                "atelier-destructibilite",
                "Atelier de destructibilité",
                [first, second],
                AreaKind.Interior,
                new AreaDimensions(32, 32));
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1a80000-0000-4000-8000-000000000006")),
                "Studio UX1-L",
                [blueprint, neighborBlueprint],
                [area]);

            var stateMachine = new PlaceableVisualStateMachineProfile(
                Guid.Parse("c1a80000-0000-4000-8000-000000000010"),
                "closed",
                Ux1KStateProfiles("ux1-l-legacy"),
                [
                    new(
                        Guid.Parse("c1a80000-0000-4000-8000-000000000011"),
                        "closed",
                        "broken",
                        "destroyed",
                        .25,
                        null),
                ]);
            var container = new PlaceableContainerProfile(
                Guid.Parse("c1a80000-0000-4000-8000-000000000012"),
                12,
                90,
                "party",
                ["container.open"],
                true,
                "loot:balise-destructible",
                "ui:placeable-chest",
                true,
                "inventory:balise-destructible");
            var originalDamageable = new PlaceableDamageableProfile(
                Guid.Parse("c1a80000-0000-4000-8000-000000000013"),
                100,
                100,
                5,
                new Dictionary<string, double> { ["physical"] = .1 },
                [.75, .5, .25],
                "vfx:legacy-break",
                "debris:legacy-balise",
                true,
                10);
            var protection = new PlaceableProtectionProfile(
                Guid.Parse("c1a80000-0000-4000-8000-000000000014"),
                ["move"],
                ["placeable.maintain"],
                "La maintenance exige une permission.",
                true,
                "PlaceableMaintenanceOverride");
            var lockProfile = new PlaceableLockProfile(
                Guid.Parse("c1a80000-0000-4000-8000-000000000015"),
                PlaceableAuthoredLockState.Locked,
                ["key:maintenance"],
                "mechanics",
                9,
                null,
                true,
                true,
                false,
                ["lock.admin"]);
            var foundation = new PlaceableWorldProfile.FoundationPolicy(
                Guid.Parse("c1a80000-0000-4000-8000-000000000016"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-l-foundation",
                blueprint.Slug,
                [new("fr-FR", blueprint.DisplayName, "Examiner la balise")],
                ["balise", "destructible"],
                "Profil de dégâts à modifier sans toucher aux opérations voisines.",
                new(
                    "asset:balise-destructible",
                    ["mesh:balise-destructible"],
                    ["material:metal"],
                    "asset:balise-icon",
                    "asset:balise-portrait",
                    ["vfx:balise-idle"]),
                new("box", new(1, 2, 1), true, true, "world"),
                null,
                new(true, true, true, false, true),
                new(
                    true,
                    true,
                    true,
                    false,
                    false,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["onDamaged"] = "scripts/balise_damaged",
                    }),
                new(Vector3Value.Zero, Vector3Value.Zero, Vector3Value.One, Vector3Value.Zero, .25,
                    null, true, null),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["variant"] = "metal",
                },
                Validation: new(
                    stateMachine.States.Select(state => state.AnimationId).ToArray(),
                    ["lock:maintenance"],
                    [],
                    ["inventory:balise-destructible"],
                    [],
                    4,
                    2));
            var operations = new PlaceableOperationsProfile(
                Guid.Parse("c1a80000-0000-4000-8000-000000000017"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-l-operations",
                container,
                stateMachine,
                originalDamageable,
                protection,
                lockProfile);
            var originalProfile = new PlaceableWorldProfile(
                Guid.Parse("c1a80000-0000-4000-8000-000000000018"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-l-profile",
                blueprint.Id,
                "neutral",
                null,
                null,
                new([], new(false, 2, 2, true, "damageable")),
                null,
                foundation,
                true,
                operations,
                true);
            var neighborId = Guid.Parse("c1a80000-0000-4000-8000-000000000019");
            var neighbor = CreatePlaceableTransformParent(
                neighborBlueprint,
                neighborId,
                "test:ux1-l-neighbor");
            var seededCatalog = new WorldFeatureCatalog(
                project.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-l-catalog",
                placeables: [originalProfile, neighbor]);
            project = new ProjectDefinition(
                project.Id,
                project.Title,
                project.Module.WithWorldFeatures(seededCatalog),
                project.Blueprints,
                project.Areas,
                project.Dialogues,
                project.Quests);
            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX1L_PROJECT_CREATE_FAILED diagnostics=" +
                    string.Join(" | ", created.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx1LSmoke();
            }
            var opened = editing.Open(tempRoot);
            if (!opened.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX1L_PROJECT_OPEN_FAILED diagnostics=" +
                    string.Join(" | ", opened.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx1LSmoke();
            }

            var originalProject = editing.Project!;
            var loadedOriginal = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var loadedNeighbor = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == neighborBlueprint.Id);
            var loadedFoundation = loadedOriginal.Foundation!;
            var loadedOperations = loadedOriginal.Operations!;
            var sourceDraft = editing.CreatePlaceableDraft(blueprint.Id)!;
            var damageDraft = sourceDraft.Damageable with
            {
                MaximumHealth = 180,
                CurrentHealth = 140,
                Armor = 12,
                Resistances =
                [
                    new("physical", .15),
                    new("fire", .4),
                    new("force", .05),
                ],
                VisualStageThresholds = [.75, .5, .25],
                DestructionEffectId = "vfx:ux1-l-balise-break",
                DebrisDefinitionId = "debris:ux1-l-balise",
                Repairable = true,
                RepairCost = 18,
            };
            var draft = sourceDraft with
            {
                HasHealth = true,
                Damageable = damageDraft,
                RequireOperationsRuntime = true,
            };

            var invalidResults = new[]
            {
                editing.PreviewPlaceable(draft with
                {
                    Damageable = damageDraft with { MaximumHealth = 0 },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Damageable = damageDraft with { CurrentHealth = 181 },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Damageable = damageDraft with { Armor = -1 },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Damageable = damageDraft with { Resistances = [] },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Damageable = damageDraft with
                    {
                        Resistances = [new("fire", .2), new("fire", .4)],
                    },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Damageable = damageDraft with
                    {
                        Resistances = [new("fire", 1.1)],
                    },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Damageable = damageDraft with { VisualStageThresholds = [] },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Damageable = damageDraft with { VisualStageThresholds = [.5, .5] },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Damageable = damageDraft with { DestructionEffectId = string.Empty },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Damageable = damageDraft with { DebrisDefinitionId = string.Empty },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Damageable = damageDraft with { RepairCost = -1 },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Damageable = damageDraft with { Id = Guid.Empty },
                }),
            };
            var invalidBlocked = invalidResults.All(result => !result.Succeeded);
            var humanDiagnostics = invalidBlocked && invalidResults.All(result =>
                IsHumanPlaceableMessage(result.Message));

            var directBuilt = editing.TryBuildPlaceable(
                draft,
                dryRun: true,
                out _,
                out var directProfile,
                out _);
            var preview = editing.PreviewPlaceable(draft);
            var dryRun = preview.Succeeded && preview.CanApply &&
                         preview.Summary.Contains("Destructibilité : 140/180 PV", StringComparison.Ordinal) &&
                         ReferenceEquals(originalProject, editing.Project);
            var instanceSelection = preview.InstanceIds.SequenceEqual([first.Id, second.Id]);
            var applied = editing.ApplyApprovedPlaceable();
            var appliedProject = editing.Project!;
            var profile = appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var damageable = profile.Operations?.Damageable;
            var healthArmor = applied.Succeeded && profile.Foundation?.Components.HasHealth == true && damageable is
            {
                MaximumHealth: 180,
                CurrentHealth: 140,
                Armor: 12,
            };
            var resistances = damageable?.Resistances.Count == 3 &&
                              damageable.Resistances["physical"] == .15 &&
                              damageable.Resistances["fire"] == .4 &&
                              damageable.Resistances["force"] == .05;
            var visualStages = damageable?.VisualStageThresholds.SequenceEqual([.75, .5, .25]) == true;
            var destructionDebris = damageable is
            {
                DestructionEffectId: "vfx:ux1-l-balise-break",
                DebrisDefinitionId: "debris:ux1-l-balise",
            };
            var repair = damageable is { Repairable: true, RepairCost: 18 };
            var commandParity = directBuilt &&
                                directProfile?.OperationsRequiredAtRuntime == profile.OperationsRequiredAtRuntime &&
                                DamageableValuesEqual(directProfile?.Operations?.Damageable, damageable);
            var preservation = profile.Id == loadedOriginal.Id && profile.Provenance == loadedOriginal.Provenance &&
                               profile.FactionId == loadedOriginal.FactionId &&
                               ReferenceEquals(profile.Reactive, loadedOriginal.Reactive) &&
                               profile.Operations?.Id == loadedOperations.Id &&
                               profile.Operations?.Provenance == loadedOperations.Provenance &&
                               ReferenceEquals(profile.Operations?.Container, loadedOperations.Container) &&
                               ReferenceEquals(profile.Operations?.StateMachine, loadedOperations.StateMachine) &&
                               ReferenceEquals(profile.Operations?.Protection, loadedOperations.Protection) &&
                               ReferenceEquals(profile.Operations?.Lock, loadedOperations.Lock) &&
                               profile.Foundation?.Id == loadedFoundation.Id &&
                               profile.Foundation.Slug == loadedFoundation.Slug &&
                               profile.Foundation.Texts.SequenceEqual(loadedFoundation.Texts) &&
                               profile.Foundation.Tags.SequenceEqual(loadedFoundation.Tags) &&
                               SamePlaceableVisual(profile.Foundation.Visual, loadedFoundation.Visual) &&
                               profile.Foundation.Collision == loadedFoundation.Collision &&
                               profile.Foundation.StaticProp == loadedFoundation.StaticProp &&
                               SamePlaceableComponents(profile.Foundation.Components, loadedFoundation.Components) &&
                               profile.Foundation.Overrides.OrderBy(item => item.Key)
                                   .SequenceEqual(loadedFoundation.Overrides.OrderBy(item => item.Key)) &&
                               ReferenceEquals(
                                   appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                                       item.BlueprintId == neighborBlueprint.Id),
                                   loadedNeighbor);

            var adapter = new PlaceableOperationsRuntimeAdapter();
            var resolution = adapter.Resolve(profile);
            var runtime = adapter.Build(resolution.Handle!, profile);
            var damaged = adapter.Damage(resolution.Handle!, profile, 100, "fire");
            var destroyed = adapter.Damage(resolution.Handle!, profile, 500, "fire");
            var repaired = adapter.Repair(resolution.Handle!, profile, destroyed.Damageable, 25);
            var damagePreview = damaged.Damageable.CurrentHealth == 92 &&
                                damaged.VisualStage == "damaged:0.75" && !damaged.Destroyed &&
                                destroyed.Destroyed && destroyed.Damageable.CurrentHealth == 0 &&
                                destroyed.DebrisDefinitionId == "debris:ux1-l-balise" &&
                                destroyed.EventName == "PlaceableDestroyed" && repaired.CurrentHealth == 25;
            var runtimeProjection = resolution.Available && runtime.Profile.Damageable is
            {
                MaximumHealth: 180,
                CurrentHealth: 140,
                Armor: 12,
                Repairable: true,
                RepairCost: 18,
            } && runtime.Profile.Damageable.Resistances["fire"] == .4 &&
                                    runtime.Profile.Damageable.VisualStageThresholds.SequenceEqual([.75, .5, .25]) &&
                                    runtime.Profile.Damageable.DestructionEffectId == "vfx:ux1-l-balise-break" &&
                                    runtime.Profile.Damageable.DebrisDefinitionId == "debris:ux1-l-balise" &&
                                    adapter.Validate(resolution.Handle!, profile).Count == 0;
            var scriptHandle = new WorldFeatureScriptService().Query(
                editing.History!.State,
                "placeable",
                profile.Id.ToString("D"),
                1).Single();
            var scriptRead = scriptHandle.Properties["maximumHealth"] == "180" &&
                             scriptHandle.Properties["currentHealth"] == "140" &&
                             scriptHandle.Properties["damageArmor"] == "12" &&
                             scriptHandle.Properties["damageResistances"] ==
                             "fire=0.4,force=0.05,physical=0.15" &&
                             scriptHandle.Properties["visualDamageStages"] == "0.75,0.5,0.25" &&
                             scriptHandle.Properties["destructionEffect"] == "vfx:ux1-l-balise-break" &&
                             scriptHandle.Properties["debrisDefinition"] == "debris:ux1-l-balise" &&
                             scriptHandle.Properties["repairable"] == "True" &&
                             scriptHandle.Properties["repairCost"] == "18";
            var optional = adapter.Resolve(profile with
            {
                Operations = null,
                OperationsRequiredAtRuntime = false,
            });
            var required = adapter.Resolve(profile with
            {
                Operations = null,
                OperationsRequiredAtRuntime = true,
            });
            var optionalRequired = optional.OptionalFallback && optional.DiagnosticCode is null &&
                                   !required.Available &&
                                   required.DiagnosticCode == "WORLD_PLACEABLE_OPERATIONS_REQUIRED_MISSING";

            var undo = editing.Undo();
            var undone = ReferenceEquals(originalProject, editing.Project);
            var redo = editing.Redo();
            var redone = ReferenceEquals(appliedProject, editing.Project);
            var undoRedo = undo.Succeeded && undone && redo.Succeeded && redone;
            var save = editing.Save();
            editing.Close();
            var reopen = reopened.Open(tempRoot);
            var restored = reopened.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
                item.BlueprintId == blueprint.Id);
            var restoredNeighbor = reopened.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
                item.BlueprintId == neighborBlueprint.Id);
            var persistence = save.Succeeded && reopen.Succeeded && restored is not null &&
                              restored.OperationsRequiredAtRuntime &&
                              DamageableValuesEqual(restored.Operations?.Damageable, damageable) &&
                              restored.Operations?.Container?.Id == container.Id &&
                              StateMachineValuesEqual(restored.Operations?.StateMachine, stateMachine) &&
                              restored.Operations?.Protection?.Id == protection.Id &&
                              restored.Operations?.Lock?.Id == lockProfile.Id &&
                              restoredNeighbor?.Id == neighborId;
            var succeeded = healthArmor && resistances && visualStages && destructionDebris && repair &&
                            damagePreview && dryRun && commandParity && instanceSelection && preservation &&
                            runtimeProjection && scriptRead && optionalRequired && undoRedo && persistence &&
                            invalidBlocked && humanDiagnostics;
            return new(
                succeeded,
                healthArmor,
                resistances,
                visualStages,
                destructionDebris,
                repair,
                damagePreview,
                dryRun,
                commandParity,
                instanceSelection,
                preservation,
                runtimeProjection,
                scriptRead,
                optionalRequired,
                undoRedo,
                persistence,
                invalidBlocked,
                humanDiagnostics);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1L_CONTRACT_EXCEPTION type={exception.GetType().Name} message={exception.Message}");
            return FailedUx1LSmoke();
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

    private static StudioUx1LSmokeResult FailedUx1LSmoke() => new(
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
