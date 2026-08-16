using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Placeables;
using Forge.RpgKit.Domain.Placeables;
using DomainVector3 = Forge.Domain.Spatial.Vector3Value;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    internal StudioUx1USmokeResult RunUx1UContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux1u-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1c00000-0000-4000-8000-000000000001")),
                "porte-des-veilleurs",
                "Porte des veilleurs",
                BlueprintKind.Placeable);
            var neighborBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1c00000-0000-4000-8000-000000000002")),
                "borne-voisine",
                "Borne voisine",
                BlueprintKind.Placeable);
            var first = new EntityInstance(
                EntityId.From(Guid.Parse("c1c00000-0000-4000-8000-000000000003")),
                blueprint.Id,
                new TransformValue(DomainVector3.Zero, QuaternionValue.Identity, DomainVector3.One));
            var second = new EntityInstance(
                EntityId.From(Guid.Parse("c1c00000-0000-4000-8000-000000000004")),
                blueprint.Id,
                new TransformValue(new(4, 0, 2), QuaternionValue.Identity, DomainVector3.One));
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("c1c00000-0000-4000-8000-000000000005")),
                "atelier-evenements-verrou",
                "Atelier des événements de verrou",
                [first, second],
                AreaKind.Interior,
                new AreaDimensions(32, 32));
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1c00000-0000-4000-8000-000000000006")),
                "Studio UX1-U",
                [blueprint, neighborBlueprint],
                [area]);
            var originalProfile = CreatePlaceableTransformParent(
                blueprint,
                Guid.Parse("c1c00000-0000-4000-8000-000000000010"),
                "test:ux1-u-profile");
            var originalLock = new PlaceableLockProfile(
                Guid.Parse("c1c00000-0000-4000-8000-000000000011"),
                PlaceableAuthoredLockState.Locked,
                ["key:watch"],
                "mechanics",
                12,
                null,
                true,
                true,
                false,
                ["lock.use"]);
            var originalContainerEvents = new PlaceableContainerStateEventsProfile(
                Guid.Parse("c1c00000-0000-4000-8000-000000000012"),
                "1.0.0",
                "ContainerOpened",
                "ContainerClosed",
                true,
                true,
                true,
                true);
            var originalLockEvents = new PlaceableLockStateEventsProfile(
                Guid.Parse("c1c00000-0000-4000-8000-000000000013"),
                "1.0.0",
                "LockStateChanging",
                "LockStateChanged",
                true,
                true,
                true,
                true);
            originalProfile = originalProfile with
            {
                Foundation = originalProfile.Foundation! with
                {
                    Components = originalProfile.Foundation.Components with { HasLock = true },
                },
                Operations = new PlaceableOperationsProfile(
                    Guid.Parse("c1c00000-0000-4000-8000-000000000014"),
                    1,
                    "test:ux1-u-operations",
                    null,
                    CreateContainerCompatibilityStateMachine(blueprint.Id),
                    null,
                    CreateContainerCompatibilityProtection(blueprint.Id),
                    originalLock),
                OperationsRequiredAtRuntime = true,
                SecurityEvents = new PlaceableSecurityEventsProfile(
                    Guid.Parse("c1c00000-0000-4000-8000-000000000015"),
                    1,
                    "test:ux1-u-security-events",
                    ContainerEvents: originalContainerEvents,
                    LockEvents: originalLockEvents),
                SecurityEventsRequiredAtRuntime = true,
            };
            var neighbor = CreatePlaceableTransformParent(
                neighborBlueprint,
                Guid.Parse("c1c00000-0000-4000-8000-000000000016"),
                "test:ux1-u-neighbor");
            var catalog = new WorldFeatureCatalog(
                project.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-u-catalog",
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
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX1U_PROJECT_CREATE_FAILED diagnostics=" +
                    string.Join(" | ", created.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx1USmoke();
            }
            var opened = editing.Open(tempRoot);
            if (!opened.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX1U_PROJECT_OPEN_FAILED diagnostics=" +
                    string.Join(" | ", opened.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx1USmoke();
            }

            var originalProject = editing.Project!;
            var loadedOriginal = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var loadedNeighbor = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == neighborBlueprint.Id);
            var sourceDraft = editing.CreatePlaceableDraft(blueprint.Id)!;
            var eventDraft = sourceDraft.LockEvents! with
            {
                ForgeScriptApiVersion = "1.1.0",
                ChangingBinding = "VerrouAvant",
                ChangedBinding = "VerrouApres",
                IncludeMethod = true,
                IncludeCredential = true,
                IncludeActor = true,
                ControlledVeto = true,
            };
            var draft = sourceDraft with
            {
                HasLock = true,
                HasLockEvents = true,
                LockEvents = eventDraft,
                RequireLockEventsRuntime = true,
            };
            var invalidResults = new[]
            {
                editing.PreviewPlaceable(draft with
                    { LockEvents = eventDraft with { ForgeScriptApiVersion = string.Empty } }),
                editing.PreviewPlaceable(draft with
                    { LockEvents = eventDraft with { ChangingBinding = string.Empty } }),
                editing.PreviewPlaceable(draft with
                    { LockEvents = eventDraft with { ChangedBinding = string.Empty } }),
                editing.PreviewPlaceable(draft with
                    { LockEvents = eventDraft with { IncludeMethod = false } }),
                editing.PreviewPlaceable(draft with
                    { LockEvents = eventDraft with { IncludeCredential = false } }),
                editing.PreviewPlaceable(draft with
                    { LockEvents = eventDraft with { IncludeActor = false } }),
                editing.PreviewPlaceable(draft with
                    { LockEvents = eventDraft with { ControlledVeto = false } }),
                editing.PreviewPlaceable(draft with
                    { LockEvents = eventDraft with { Id = Guid.Empty } }),
                editing.PreviewPlaceable(draft with
                    { HasLockEvents = false, RequireLockEventsRuntime = true }),
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
            var acceptedPreview = editing.PreviewPlaceableLockEvent(
                draft,
                StudioPlaceableLockEventPreviewMode.AcceptedTransition);
            var vetoPreview = editing.PreviewPlaceableLockEvent(
                draft,
                StudioPlaceableLockEventPreviewMode.ControlledVeto);
            var dryRun = preview.Succeeded && preview.CanApply &&
                         preview.Summary.Contains("Verrouillage et déverrouillage", StringComparison.Ordinal) &&
                         ReferenceEquals(originalProject, editing.Project);
            var instanceSelection = preview.InstanceIds.SequenceEqual([first.Id, second.Id]);
            var applied = editing.ApplyApprovedPlaceable();
            var appliedProject = editing.Project!;
            var profile = appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var lockEvents = profile.SecurityEvents?.LockEvents;
            var bindingsAndApi = lockEvents is
            {
                ForgeScriptApiVersion: "1.1.0",
                ChangingBinding: "VerrouAvant",
                ChangedBinding: "VerrouApres",
            };
            var canonicalPayload = lockEvents is
            {
                IncludeMethod: true,
                IncludeCredential: true,
                IncludeActor: true,
                ControlledVeto: true,
            };
            var beforeAfterPreview = acceptedPreview.Succeeded &&
                                     acceptedPreview.ChangingBinding == "VerrouAvant" &&
                                     acceptedPreview.ChangedBinding == "VerrouApres" &&
                                     acceptedPreview.ChangingEventName == "LockStateChanging" &&
                                     acceptedPreview.ChangedEventName == "LockStateChanged" &&
                                     acceptedPreview.Method == "credential" &&
                                     acceptedPreview.Credential == "clé-du-projet" &&
                                     acceptedPreview.ActorId == "joueuse-locale" &&
                                     acceptedPreview.FinalState == nameof(LockState.Unlocked);
            var controlledVeto = vetoPreview.Succeeded && vetoPreview.Vetoed &&
                                 vetoPreview.VetoReason == "condition narrative non remplie" &&
                                 vetoPreview.ChangedBinding is null &&
                                 vetoPreview.ChangedEventName is null &&
                                 vetoPreview.FinalState == nameof(LockState.Locked);
            var commandParity = directBuilt &&
                                directProfile?.LockEventsRequiredAtRuntime ==
                                profile.LockEventsRequiredAtRuntime &&
                                LockEventValuesEqual(
                                    directProfile?.SecurityEvents?.LockEvents,
                                    lockEvents);
            var preservation = profile.Id == loadedOriginal.Id &&
                               profile.Provenance == loadedOriginal.Provenance &&
                               profile.Foundation?.Id == loadedOriginal.Foundation?.Id &&
                               profile.Operations?.Id == loadedOriginal.Operations?.Id &&
                               ReferenceEquals(profile.Operations?.Lock, loadedOriginal.Operations?.Lock) &&
                               profile.SecurityEvents?.Id == loadedOriginal.SecurityEvents?.Id &&
                               profile.SecurityEvents?.Provenance == loadedOriginal.SecurityEvents?.Provenance &&
                               profile.SecurityEventsRequiredAtRuntime ==
                               loadedOriginal.SecurityEventsRequiredAtRuntime &&
                               ReferenceEquals(
                                   profile.SecurityEvents?.ContainerEvents,
                                   loadedOriginal.SecurityEvents?.ContainerEvents) &&
                               profile.SecurityEvents?.Trap is null &&
                               profile.SecurityEvents?.Loot is null &&
                               profile.SecurityEvents?.ContentsChanged is null &&
                               ReferenceEquals(profile.Reactive, loadedOriginal.Reactive) &&
                               ReferenceEquals(
                                   appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                                       item.BlueprintId == neighborBlueprint.Id),
                                   loadedNeighbor) &&
                               appliedProject.Blueprints.SequenceEqual(originalProject.Blueprints);

            var adapter = new PlaceableSecurityEventsRuntimeAdapter();
            var resolution = adapter.ResolveLockEvents(profile);
            var runtimePolicy = adapter.BuildLockEvents(resolution.Handle!, profile);
            var accepted = adapter.ChangeLockState(
                resolution.Handle!,
                profile,
                LockState.Unlocked,
                "credential",
                "clé-du-projet",
                "joueuse-locale");
            var vetoed = adapter.ChangeLockState(
                resolution.Handle!,
                profile,
                LockState.Unlocked,
                "credential",
                "clé-du-projet",
                "joueuse-locale",
                _ => "condition narrative non remplie");
            var runtimeProjection = applied.Succeeded && resolution.Available &&
                                    profile.LockEventsRequiredAtRuntime &&
                                    runtimePolicy.Id == lockEvents?.Id &&
                                    accepted.Changing.Binding == "VerrouAvant" &&
                                    accepted.Changing.Payload.Name == "LockStateChanging" &&
                                    accepted.Changed?.Binding == "VerrouApres" &&
                                    accepted.Changed?.Payload.Name == "LockStateChanged" &&
                                    accepted.Changed?.Payload.Method == "credential" &&
                                    accepted.Changed?.Payload.Credential == "clé-du-projet" &&
                                    accepted.Changed?.Payload.ActorId == "joueuse-locale" &&
                                    accepted.Lock.State == LockState.Unlocked &&
                                    vetoed.Changing.Payload.Vetoed && vetoed.Changed is null &&
                                    vetoed.Lock.State == LockState.Locked;

            var scriptHandle = new WorldFeatureScriptService().Query(
                editing.History!.State,
                "placeable",
                profile.Id.ToString("D"),
                1).Single();
            var scriptRead = scriptHandle.Properties["lockStateChanging"] == "VerrouAvant" &&
                             scriptHandle.Properties["lockStateChanged"] == "VerrouApres" &&
                             scriptHandle.Properties["lockEventApi"] == "1.1.0" &&
                             scriptHandle.Properties["lockEventPayload"] ==
                             "method-credential-actor-controlled-veto" &&
                             scriptHandle.Properties["lockEventsRequiredAtRuntime"] == "True";
            var optionalProfile = profile with
            {
                SecurityEvents = profile.SecurityEvents! with { LockEvents = null },
                LockEventsRequiredAtRuntime = false,
            };
            var requiredProfile = optionalProfile with { LockEventsRequiredAtRuntime = true };
            var optional = adapter.ResolveLockEvents(optionalProfile);
            var required = adapter.ResolveLockEvents(requiredProfile);
            var optionalRequired = optional.OptionalFallback && optional.DiagnosticCode is null &&
                                   !required.Available &&
                                   required.DiagnosticCode == "WORLD_PLACEABLE_LOCK_EVENTS_REQUIRED_MISSING";

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
                              persisted.LockEventsRequiredAtRuntime &&
                              LockEventValuesEqual(persisted.SecurityEvents?.LockEvents, lockEvents) &&
                              persisted.OperationsRequiredAtRuntime &&
                              persisted.Operations?.Lock?.InitialState == PlaceableAuthoredLockState.Locked &&
                              persisted.SecurityEventsRequiredAtRuntime &&
                              persisted.SecurityEvents?.ContainerEvents?.OpenedBinding == "ContainerOpened" &&
                              persisted.SecurityEvents?.Trap is null &&
                              persisted.SecurityEvents?.Loot is null &&
                              persisted.SecurityEvents?.ContentsChanged is null &&
                              persistedNeighbor?.Id == neighbor.Id &&
                              reopened.Project?.Blueprints.Count == 2;
            var succeeded = bindingsAndApi && canonicalPayload && beforeAfterPreview && controlledVeto &&
                            dryRun && commandParity && instanceSelection && preservation && runtimeProjection &&
                            scriptRead && optionalRequired && undoRedo && persistence && invalidBlocked &&
                            humanDiagnostics;
            return new(
                succeeded,
                bindingsAndApi,
                canonicalPayload,
                beforeAfterPreview,
                controlledVeto,
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
                $"OPENRPG_FORGE_STUDIO_UX1U_CONTRACT_EXCEPTION type={exception.GetType().Name} message={exception.Message}");
            return FailedUx1USmoke();
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

    internal StudioPlaceableLockEventPreview PreviewPlaceableLockEvent(
        StudioPlaceableDraft draft,
        StudioPlaceableLockEventPreviewMode mode)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!draft.HasLockEvents)
        {
            return FailedLockEventPreview(
                "Événements de verrouillage et de déverrouillage désactivés · activez-les avant l’aperçu.",
                "WORLD_PLACEABLE_LOCK_EVENTS_DISABLED");
        }
        if (!draft.HasLock)
        {
            return FailedLockEventPreview(
                "Activez aussi le verrou pour prévisualiser son changement d’état.",
                "WORLD_PLACEABLE_LOCK_MISSING");
        }
        if (!TryBuildPlaceable(draft, dryRun: true, out _, out var profile, out var message) ||
            profile?.SecurityEvents?.LockEvents is null)
        {
            return FailedLockEventPreview(message, LastPlaceableDiagnosticCode);
        }

        var adapter = new PlaceableSecurityEventsRuntimeAdapter();
        var resolution = adapter.ResolveLockEvents(profile);
        if (!resolution.Available || resolution.Handle is null)
        {
            return FailedLockEventPreview(
                HumanizePlaceable(resolution.DiagnosticCode),
                resolution.DiagnosticCode ?? "WORLD_PLACEABLE_LOCK_EVENTS_UNAVAILABLE");
        }

        var vetoRequested = mode == StudioPlaceableLockEventPreviewMode.ControlledVeto;
        var dispatch = adapter.ChangeLockState(
            resolution.Handle,
            profile,
            LockState.Unlocked,
            "credential",
            "clé-du-projet",
            "joueuse-locale",
            vetoRequested ? _ => "condition narrative non remplie" : null);
        var changing = dispatch.Changing.Payload;
        var changed = dispatch.Changed?.Payload;
        var summary = changing.Vetoed
            ? $"Aperçu avec veto contrôlé · {dispatch.Changing.Binding} publié avant le changement · " +
              $"{changing.VetoReason} · aucun événement après · état toujours verrouillé · aucun module modifié."
            : $"Aperçu accepté · {dispatch.Changing.Binding} publié avant · " +
              $"{dispatch.Changed?.Binding} publié après · méthode justificatif · actrice locale · " +
              "état final déverrouillé · aucun module modifié.";
        return new(
            true,
            summary,
            changing.Vetoed ? "RPG_PLACEABLE_LOCK_STATE_VETOED" : "RPG_PLACEABLE_LOCK_STATE_CHANGED",
            dispatch.Changing.Binding,
            dispatch.Changed?.Binding,
            changing.Name,
            changed?.Name,
            changing.Method,
            changing.Credential,
            changing.ActorId,
            changing.Vetoed,
            changing.VetoReason,
            dispatch.Lock.State.ToString());
    }

    private static StudioPlaceableLockEventPreview FailedLockEventPreview(string message, string code) => new(
        false,
        message,
        code,
        string.Empty,
        null,
        "LockEventUnavailable",
        null,
        string.Empty,
        null,
        string.Empty,
        false,
        null,
        string.Empty);

    private static StudioUx1USmokeResult FailedUx1USmoke() => new(
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
