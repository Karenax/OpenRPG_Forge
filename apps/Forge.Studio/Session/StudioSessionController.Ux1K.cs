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
    internal StudioUx1KSmokeResult RunUx1KContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux1k-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1a70000-0000-4000-8000-000000000001")),
                "balise-astrale",
                "Balise astrale",
                BlueprintKind.Placeable);
            var neighborBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1a70000-0000-4000-8000-000000000002")),
                "socle-astral",
                "Socle astral",
                BlueprintKind.Placeable);
            var first = new EntityInstance(
                EntityId.From(Guid.Parse("c1a70000-0000-4000-8000-000000000003")),
                blueprint.Id,
                new TransformValue(Vector3Value.Zero, QuaternionValue.Identity, Vector3Value.One));
            var second = new EntityInstance(
                EntityId.From(Guid.Parse("c1a70000-0000-4000-8000-000000000004")),
                blueprint.Id,
                new TransformValue(new(5, 0, 1), QuaternionValue.Identity, Vector3Value.One));
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("c1a70000-0000-4000-8000-000000000005")),
                "atelier-etats-visuels",
                "Atelier des états visuels",
                [first, second],
                AreaKind.Interior,
                new AreaDimensions(32, 32));
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1a70000-0000-4000-8000-000000000006")),
                "Studio UX1-K",
                [blueprint, neighborBlueprint],
                [area]);

            var stateMachine = new PlaceableVisualStateMachineProfile(
                Guid.Parse("c1a70000-0000-4000-8000-000000000010"),
                "closed",
                Ux1KStateProfiles("legacy"),
                [
                    new(
                        Guid.Parse("c1a70000-0000-4000-8000-000000000020"),
                        "closed",
                        "open",
                        "open",
                        .5,
                        "access.allowed"),
                ]);
            var container = new PlaceableContainerProfile(
                Guid.Parse("c1a70000-0000-4000-8000-000000000021"),
                18,
                120,
                "party",
                ["container.open", "container.withdraw"],
                true,
                "loot:balise-astrale",
                "ui:placeable-chest",
                true,
                "inventory:balise-astrale");
            var damageable = new PlaceableDamageableProfile(
                Guid.Parse("c1a70000-0000-4000-8000-000000000022"),
                100,
                100,
                8,
                new Dictionary<string, double> { ["force"] = .25 },
                [.75, .5, .25],
                "vfx:balise-break",
                "debris:balise-astrale",
                true,
                12);
            var protection = new PlaceableProtectionProfile(
                Guid.Parse("c1a70000-0000-4000-8000-000000000023"),
                ["destroy", "move"],
                ["placeable.astral.override"],
                "La balise est protégée.",
                true,
                "AstralPlaceableOverride");
            var lockProfile = new PlaceableLockProfile(
                Guid.Parse("c1a70000-0000-4000-8000-000000000024"),
                PlaceableAuthoredLockState.Locked,
                ["key:astral"],
                "mechanics",
                11,
                null,
                true,
                true,
                true,
                ["lock.admin"]);
            var foundation = new PlaceableWorldProfile.FoundationPolicy(
                Guid.Parse("c1a70000-0000-4000-8000-000000000025"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-k-foundation",
                blueprint.Slug,
                [new("fr-FR", blueprint.DisplayName, "Observer la balise")],
                ["astral", "balise"],
                "Machine d’état initiale à préserver puis modifier.",
                new(
                    "asset:balise-astrale",
                    ["mesh:balise-astrale"],
                    ["material:cristal"],
                    "asset:balise-icon",
                    "asset:balise-portrait",
                    ["vfx:astral-idle"]),
                new("box", new(1, 3, 1), true, true, "world"),
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
                        ["onStateChanged"] = "scripts/balise_state_changed",
                    }),
                new(Vector3Value.Zero, Vector3Value.Zero, Vector3Value.One, new(0, 1.5f, 0), .25,
                    null, true, null),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["variant"] = "cristal",
                },
                Validation: new(
                    stateMachine.States.Select(state => state.AnimationId).ToArray(),
                    ["lock:astral"],
                    [],
                    ["inventory:balise-astrale"],
                    [],
                    4,
                    2));
            var operations = new PlaceableOperationsProfile(
                Guid.Parse("c1a70000-0000-4000-8000-000000000026"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-k-operations",
                container,
                stateMachine,
                damageable,
                protection,
                lockProfile);
            var originalProfile = new PlaceableWorldProfile(
                Guid.Parse("c1a70000-0000-4000-8000-000000000027"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-k-profile",
                blueprint.Id,
                "neutral",
                null,
                null,
                new([], new(false, 2, 2, true, "visual-state")),
                null,
                foundation,
                true,
                operations,
                true);
            var neighborId = Guid.Parse("c1a70000-0000-4000-8000-000000000028");
            var neighbor = CreatePlaceableTransformParent(
                neighborBlueprint,
                neighborId,
                "test:ux1-k-neighbor");
            var seededCatalog = new WorldFeatureCatalog(
                project.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-k-catalog",
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
                    "OPENRPG_FORGE_STUDIO_UX1K_PROJECT_CREATE_FAILED diagnostics=" +
                    string.Join(" | ", created.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx1KSmoke();
            }
            var opened = editing.Open(tempRoot);
            if (!opened.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX1K_PROJECT_OPEN_FAILED diagnostics=" +
                    string.Join(" | ", opened.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx1KSmoke();
            }

            var originalProject = editing.Project!;
            var loadedOriginal = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var loadedNeighbor = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == neighborBlueprint.Id);
            var loadedFoundation = loadedOriginal.Foundation!;
            var loadedOperations = loadedOriginal.Operations!;
            var sourceDraft = editing.CreatePlaceableDraft(blueprint.Id)!;
            var editedStates = sourceDraft.StateMachine.States.Select(state => state with
            {
                AnimationId = $"animation:ux1-k-{state.StateId}",
                VisualOverrideId = $"visual:ux1-k-{state.StateId}",
                Interactable = state.StateId != "broken",
            }).ToArray();
            var editedTransitions = new StudioPlaceableVisualTransitionDraft[]
            {
                new(
                    Guid.Parse("c1a70000-0000-4000-8000-000000000040"),
                    "closed",
                    "active",
                    "power-on",
                    .65,
                    "power.available"),
                new(
                    Guid.Parse("c1a70000-0000-4000-8000-000000000041"),
                    "active",
                    "closed",
                    "power-off",
                    .35,
                    string.Empty),
            };
            var draft = sourceDraft with
            {
                HasStateMachine = true,
                StateMachine = sourceDraft.StateMachine with
                {
                    InitialStateId = "active",
                    States = editedStates,
                    Transitions = editedTransitions,
                },
                RequireOperationsRuntime = true,
                AuthorNotes = "États visuels et transitions gouvernés par UX1-K.",
            };

            var missingState = editing.PreviewPlaceable(draft with
            {
                StateMachine = draft.StateMachine with
                {
                    States = editedStates.Where(state => state.StateId != "custom").ToArray(),
                },
            });
            var duplicateState = editing.PreviewPlaceable(draft with
            {
                StateMachine = draft.StateMachine with
                {
                    States = editedStates[..^1].Append(editedStates[^1] with { StateId = "idle" }).ToArray(),
                },
            });
            var duplicateIdentity = editing.PreviewPlaceable(draft with
            {
                StateMachine = draft.StateMachine with
                {
                    States = editedStates[..^1].Append(editedStates[^1] with { Id = editedStates[0].Id }).ToArray(),
                },
            });
            var missingInitial = editing.PreviewPlaceable(draft with
            {
                StateMachine = draft.StateMachine with { InitialStateId = "unknown" },
            });
            var unknownTransition = editing.PreviewPlaceable(draft with
            {
                StateMachine = draft.StateMachine with
                {
                    Transitions =
                    [editedTransitions[0] with { FromStateId = "unknown" }],
                },
            });
            var invalidDuration = editing.PreviewPlaceable(draft with
            {
                StateMachine = draft.StateMachine with
                {
                    Transitions =
                    [editedTransitions[0] with { DurationSeconds = -1 }],
                },
            });
            var invalidResults = new[]
            {
                missingState,
                duplicateState,
                duplicateIdentity,
                missingInitial,
                unknownTransition,
                invalidDuration,
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
                         preview.Summary.Contains("Animation initiale : Actif", StringComparison.Ordinal) &&
                         ReferenceEquals(originalProject, editing.Project);
            var instanceSelection = preview.InstanceIds.SequenceEqual([first.Id, second.Id]);
            var applied = editing.ApplyApprovedPlaceable();
            var appliedProject = editing.Project!;
            var profile = appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var machine = profile.Operations?.StateMachine;
            var initialState = applied.Succeeded && machine?.InitialStateId == "active";
            var sixStates = machine?.States.Select(state => state.StateId).Order(StringComparer.Ordinal)
                .SequenceEqual(["active", "broken", "closed", "custom", "idle", "open"]) == true;
            var animationsAndOverrides = machine?.States.All(state =>
                state.AnimationId == $"animation:ux1-k-{state.StateId}" &&
                state.VisualOverrideId == $"visual:ux1-k-{state.StateId}") == true;
            var interactableStates = machine?.States.Single(state => state.StateId == "broken").Interactable == false &&
                                     machine.States.Where(state => state.StateId != "broken")
                                         .All(state => state.Interactable);
            var transitions = machine?.Transitions.Count == 2 && machine.Transitions.Any(transition =>
                transition is
                {
                    FromStateId: "closed",
                    ToStateId: "active",
                    Trigger: "power-on",
                    DurationSeconds: .65,
                    Condition: "power.available",
                });
            var commandParity = directBuilt &&
                                directProfile?.OperationsRequiredAtRuntime == profile.OperationsRequiredAtRuntime &&
                                StateMachineValuesEqual(directProfile?.Operations?.StateMachine, machine);
            var preservation = profile.Id == loadedOriginal.Id && profile.Provenance == loadedOriginal.Provenance &&
                               profile.FactionId == loadedOriginal.FactionId &&
                               ReferenceEquals(profile.Reactive, loadedOriginal.Reactive) &&
                               profile.Operations?.Id == loadedOperations.Id &&
                               profile.Operations?.Provenance == loadedOperations.Provenance &&
                               ReferenceEquals(profile.Operations?.Container, loadedOperations.Container) &&
                               ReferenceEquals(profile.Operations?.Damageable, loadedOperations.Damageable) &&
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
            var transitionPreview = adapter.PreviewTransition(
                resolution.Handle!,
                profile,
                "closed",
                "power-on",
                new HashSet<string> { "power.available" });
            var previewTransition = transitionPreview.CurrentState == "active" &&
                                    transitionPreview.PreviewDuration == TimeSpan.FromSeconds(.65);
            var runtimeProjection = resolution.Available && runtime.Profile.StateMachine.InitialState == "active" &&
                                    runtime.Profile.StateMachine.States.Count == 6 &&
                                    runtime.Profile.StateMachine.States.Single(state => state.Id == "active") is
                                    {
                                        AnimationId: "animation:ux1-k-active",
                                        VisualOverrideId: "visual:ux1-k-active",
                                        Interactable: true,
                                    } &&
                                    adapter.Validate(resolution.Handle!, profile).Count == 0;
            var scriptHandle = new WorldFeatureScriptService().Query(
                editing.History!.State,
                "placeable",
                profile.Id.ToString("D"),
                1).Single();
            var scriptRead = scriptHandle.Properties["initialVisualState"] == "active" &&
                             scriptHandle.Properties["visualStates"] ==
                             "active,broken,closed,custom,idle,open" &&
                             scriptHandle.Properties["visualTransitions"] == "2" &&
                             scriptHandle.Properties["visualStateAnimations"].Contains(
                                 "active=animation:ux1-k-active", StringComparison.Ordinal) &&
                             scriptHandle.Properties["visualStateOverrides"].Contains(
                                 "broken=visual:ux1-k-broken", StringComparison.Ordinal) &&
                             scriptHandle.Properties["interactableVisualStates"] ==
                             "active,closed,custom,idle,open" &&
                             scriptHandle.Properties["visualTransitionTriggers"] ==
                             "active->closed:power-off@0.35,closed->active:power-on@0.65[power.available]";
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
                              StateMachineValuesEqual(restored.Operations?.StateMachine, machine) &&
                              restored.Operations?.Container?.Id == container.Id &&
                              restored.Operations?.Damageable?.Id == damageable.Id &&
                              restored.Operations?.Protection?.Id == protection.Id &&
                              restored.Operations?.Lock?.Id == lockProfile.Id &&
                              restoredNeighbor?.Id == neighborId;
            var succeeded = initialState && sixStates && animationsAndOverrides && interactableStates &&
                            transitions && previewTransition && dryRun && commandParity && instanceSelection &&
                            preservation && runtimeProjection && scriptRead && optionalRequired && undoRedo &&
                            persistence && invalidBlocked && humanDiagnostics;
            return new(
                succeeded,
                initialState,
                sixStates,
                animationsAndOverrides,
                interactableStates,
                transitions,
                previewTransition,
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
                $"OPENRPG_FORGE_STUDIO_UX1K_CONTRACT_EXCEPTION type={exception.GetType().Name} message={exception.Message}");
            return FailedUx1KSmoke();
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

    private static IReadOnlyList<PlaceableVisualStateProfile> Ux1KStateProfiles(string suffix)
    {
        var states = new[] { "idle", "open", "closed", "broken", "active", "custom" };
        return states.Select((state, index) => new PlaceableVisualStateProfile(
            Guid.Parse($"c1a70000-0000-4000-8000-{index + 30:D12}"),
            state,
            $"animation:{suffix}-{state}",
            $"visual:{suffix}-{state}",
            state != "broken")).ToArray();
    }

    private static bool StateMachineValuesEqual(
        PlaceableVisualStateMachineProfile? left,
        PlaceableVisualStateMachineProfile? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }
        return left.Id == right.Id &&
               left.InitialStateId == right.InitialStateId &&
               left.States.SequenceEqual(right.States) &&
               left.Transitions.SequenceEqual(right.Transitions);
    }

    private static StudioUx1KSmokeResult FailedUx1KSmoke() => new(
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
