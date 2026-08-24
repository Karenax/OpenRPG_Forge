using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Placeables;
using DomainVector3 = Forge.Domain.Spatial.Vector3Value;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    internal StudioUx1TSmokeResult RunUx1TContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux1t-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1b00000-0000-4000-8000-000000000001")),
                "coffre-des-veilleurs",
                "Coffre des veilleurs",
                BlueprintKind.Placeable);
            var neighborBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1b00000-0000-4000-8000-000000000002")),
                "borne-voisine",
                "Borne voisine",
                BlueprintKind.Placeable);
            var first = new EntityInstance(
                EntityId.From(Guid.Parse("c1b00000-0000-4000-8000-000000000003")),
                blueprint.Id,
                new TransformValue(DomainVector3.Zero, QuaternionValue.Identity, DomainVector3.One));
            var second = new EntityInstance(
                EntityId.From(Guid.Parse("c1b00000-0000-4000-8000-000000000004")),
                blueprint.Id,
                new TransformValue(new(4, 0, 2), QuaternionValue.Identity, DomainVector3.One));
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("c1b00000-0000-4000-8000-000000000005")),
                "atelier-evenements-conteneur",
                "Atelier des événements de conteneur",
                [first, second],
                AreaKind.Interior,
                new AreaDimensions(32, 32));
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1b00000-0000-4000-8000-000000000006")),
                "Studio UX1-T",
                [blueprint, neighborBlueprint],
                [area]);
            var originalProfile = CreatePlaceableTransformParent(
                blueprint,
                Guid.Parse("c1b00000-0000-4000-8000-000000000010"),
                "test:ux1-t-profile");
            var hiddenTrap = new PlaceableTrapProfile(
                Guid.Parse("c1b00000-0000-4000-8000-000000000011"),
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
            var originalContainerEvents = new PlaceableContainerStateEventsProfile(
                Guid.Parse("c1b00000-0000-4000-8000-000000000012"),
                "1.0.0",
                "ContainerOpened",
                "ContainerClosed",
                true,
                true,
                true,
                true);
            originalProfile = originalProfile with
            {
                Foundation = originalProfile.Foundation! with
                {
                    Components = originalProfile.Foundation.Components with { HasTrap = true },
                },
                SecurityEvents = new(
                    Guid.Parse("c1b00000-0000-4000-8000-000000000013"),
                    1,
                    "test:ux1-t-security-events",
                    Trap: hiddenTrap,
                    ContainerEvents: originalContainerEvents),
                SecurityEventsRequiredAtRuntime = true,
            };
            var neighbor = CreatePlaceableTransformParent(
                neighborBlueprint,
                Guid.Parse("c1b00000-0000-4000-8000-000000000014"),
                "test:ux1-t-neighbor");
            var catalog = new WorldFeatureCatalog(
                project.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-t-catalog",
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
                    "OPENRPG_FORGE_STUDIO_UX1T_PROJECT_CREATE_FAILED diagnostics=" +
                    string.Join(" | ", created.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx1TSmoke();
            }
            var opened = editing.Open(tempRoot);
            if (!opened.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX1T_PROJECT_OPEN_FAILED diagnostics=" +
                    string.Join(" | ", opened.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx1TSmoke();
            }

            var originalProject = editing.Project!;
            var loadedOriginal = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var loadedNeighbor = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == neighborBlueprint.Id);
            var sourceDraft = editing.CreatePlaceableDraft(blueprint.Id)!;
            var eventDraft = sourceDraft.ContainerEvents! with
            {
                ForgeScriptApiVersion = "1.1.0",
                OpenedBinding = "CoffreOuvert",
                ClosedBinding = "CoffreFerme",
                IncludeActor = true,
                IncludeResult = true,
                IncludePreviousState = true,
                IncludeAuthority = true,
            };
            var draft = sourceDraft with
            {
                HasContainerEvents = true,
                ContainerEvents = eventDraft,
                RequireContainerEventsRuntime = true,
            };
            var invalidResults = new[]
            {
                editing.PreviewPlaceable(draft with
                    { ContainerEvents = eventDraft with { ForgeScriptApiVersion = string.Empty } }),
                editing.PreviewPlaceable(draft with
                    { ContainerEvents = eventDraft with { OpenedBinding = string.Empty } }),
                editing.PreviewPlaceable(draft with
                    { ContainerEvents = eventDraft with { ClosedBinding = string.Empty } }),
                editing.PreviewPlaceable(draft with
                    { ContainerEvents = eventDraft with { IncludeActor = false } }),
                editing.PreviewPlaceable(draft with
                    { ContainerEvents = eventDraft with { IncludeResult = false } }),
                editing.PreviewPlaceable(draft with
                    { ContainerEvents = eventDraft with { IncludePreviousState = false } }),
                editing.PreviewPlaceable(draft with
                    { ContainerEvents = eventDraft with { IncludeAuthority = false } }),
                editing.PreviewPlaceable(draft with
                    { ContainerEvents = eventDraft with { Id = Guid.Empty } }),
                editing.PreviewPlaceable(draft with
                    { HasContainerEvents = false, RequireContainerEventsRuntime = true }),
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
            var openPreview = editing.PreviewPlaceableContainerEvent(
                draft,
                StudioPlaceableContainerEventPreviewMode.Open);
            var closePreview = editing.PreviewPlaceableContainerEvent(
                draft,
                StudioPlaceableContainerEventPreviewMode.Close);
            var dryRun = preview.Succeeded && preview.CanApply &&
                         preview.Summary.Contains("Ouverture et fermeture", StringComparison.Ordinal) &&
                         ReferenceEquals(originalProject, editing.Project);
            var instanceSelection = preview.InstanceIds.SequenceEqual([first.Id, second.Id]);
            var applied = editing.ApplyApprovedPlaceable();
            var appliedProject = editing.Project!;
            var profile = appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var containerEvents = profile.SecurityEvents?.ContainerEvents;
            var bindingsAndApi = containerEvents is
            {
                ForgeScriptApiVersion: "1.1.0",
                OpenedBinding: "CoffreOuvert",
                ClosedBinding: "CoffreFerme",
            };
            var canonicalPayload = containerEvents is
            {
                IncludeActor: true,
                IncludeResult: true,
                IncludePreviousState: true,
                IncludeAuthority: true,
            };
            var openClosePreview = openPreview.Succeeded && closePreview.Succeeded &&
                                   openPreview.Binding == "CoffreOuvert" &&
                                   openPreview.EventName == "ContainerOpened" &&
                                   closePreview.Binding == "CoffreFerme" &&
                                   closePreview.EventName == "ContainerClosed" &&
                                   openPreview.ActorId == "joueuse-locale" &&
                                   closePreview.PreviousState == "open" &&
                                   openPreview.Authority == "server";
            var commandParity = directBuilt &&
                                directProfile?.ContainerEventsRequiredAtRuntime ==
                                profile.ContainerEventsRequiredAtRuntime &&
                                ContainerEventValuesEqual(
                                    directProfile?.SecurityEvents?.ContainerEvents,
                                    containerEvents);
            var preservation = profile.Id == loadedOriginal.Id &&
                               profile.Provenance == loadedOriginal.Provenance &&
                               profile.Foundation?.Id == loadedOriginal.Foundation?.Id &&
                               profile.Operations == loadedOriginal.Operations &&
                               profile.SecurityEvents?.Id == loadedOriginal.SecurityEvents?.Id &&
                               profile.SecurityEvents?.Provenance == loadedOriginal.SecurityEvents?.Provenance &&
                               profile.SecurityEventsRequiredAtRuntime ==
                               loadedOriginal.SecurityEventsRequiredAtRuntime &&
                               ReferenceEquals(profile.SecurityEvents?.Trap, loadedOriginal.SecurityEvents?.Trap) &&
                               profile.SecurityEvents?.Loot is null &&
                               profile.SecurityEvents?.LockEvents is null &&
                               profile.SecurityEvents?.ContentsChanged is null &&
                               ReferenceEquals(profile.Reactive, loadedOriginal.Reactive) &&
                               ReferenceEquals(
                                   appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                                       item.BlueprintId == neighborBlueprint.Id),
                                   loadedNeighbor) &&
                               appliedProject.Blueprints.SequenceEqual(originalProject.Blueprints);

            var adapter = new PlaceableSecurityEventsRuntimeAdapter();
            var resolution = adapter.ResolveContainerEvents(profile);
            var runtime = adapter.BuildContainerEvents(resolution.Handle!, profile);
            var openedEvent = adapter.ContainerStateChanged(
                resolution.Handle!, profile, true, "joueuse-locale", "success", "closed", "server",
                DateTimeOffset.UnixEpoch);
            var closedEvent = adapter.ContainerStateChanged(
                resolution.Handle!, profile, false, "joueuse-locale", "success", "open", "server",
                DateTimeOffset.UnixEpoch);
            var runtimeProjection = applied.Succeeded && resolution.Available &&
                                    profile.ContainerEventsRequiredAtRuntime &&
                                    runtime.Id == containerEvents?.Id &&
                                    openedEvent.Binding == "CoffreOuvert" &&
                                    openedEvent.Payload.Name == "ContainerOpened" &&
                                    openedEvent.Payload.ActorId == "joueuse-locale" &&
                                    openedEvent.Payload.Result == "success" &&
                                    openedEvent.Payload.PreviousState == "closed" &&
                                    openedEvent.Payload.Authority == "server" &&
                                    closedEvent.Binding == "CoffreFerme" &&
                                    closedEvent.Payload.Name == "ContainerClosed";

            var scriptHandle = new WorldFeatureScriptService().Query(
                editing.History!.State,
                "placeable",
                profile.Id.ToString("D"),
                1).Single();
            var scriptRead = scriptHandle.Properties["containerOpened"] == "CoffreOuvert" &&
                             scriptHandle.Properties["containerClosed"] == "CoffreFerme" &&
                             scriptHandle.Properties["containerEventApi"] == "1.1.0" &&
                             scriptHandle.Properties["containerEventPayload"] ==
                             "actor-result-previous-state-authority" &&
                             scriptHandle.Properties["containerEventsRequiredAtRuntime"] == "True";
            var optionalProfile = profile with
            {
                SecurityEvents = profile.SecurityEvents! with { ContainerEvents = null },
                ContainerEventsRequiredAtRuntime = false,
            };
            var requiredProfile = optionalProfile with { ContainerEventsRequiredAtRuntime = true };
            var optional = adapter.ResolveContainerEvents(optionalProfile);
            var required = adapter.ResolveContainerEvents(requiredProfile);
            var optionalRequired = optional.OptionalFallback && optional.DiagnosticCode is null &&
                                   !required.Available &&
                                   required.DiagnosticCode ==
                                   "WORLD_PLACEABLE_CONTAINER_EVENTS_REQUIRED_MISSING";

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
                              persisted.ContainerEventsRequiredAtRuntime &&
                              ContainerEventValuesEqual(
                                  persisted.SecurityEvents?.ContainerEvents,
                                  containerEvents) &&
                              persisted.SecurityEventsRequiredAtRuntime &&
                              persisted.SecurityEvents?.Trap?.PayloadId == "damage:old-thorns" &&
                              persisted.SecurityEvents?.Loot is null &&
                              persisted.SecurityEvents?.LockEvents is null &&
                              persisted.SecurityEvents?.ContentsChanged is null &&
                              persisted.Operations is null &&
                              persistedNeighbor?.Id == neighbor.Id &&
                              reopened.Project?.Blueprints.Count == 2;
            var succeeded = bindingsAndApi && canonicalPayload && openClosePreview && dryRun &&
                            commandParity && instanceSelection && preservation && runtimeProjection &&
                            scriptRead && optionalRequired && undoRedo && persistence && invalidBlocked &&
                            humanDiagnostics;
            return new(
                succeeded,
                bindingsAndApi,
                canonicalPayload,
                openClosePreview,
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
                $"OPENRPG_FORGE_STUDIO_UX1T_CONTRACT_EXCEPTION type={exception.GetType().Name} message={exception.Message}");
            return FailedUx1TSmoke();
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

    internal StudioPlaceableContainerEventPreview PreviewPlaceableContainerEvent(
        StudioPlaceableDraft draft,
        StudioPlaceableContainerEventPreviewMode mode)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!draft.HasContainerEvents)
        {
            return new(
                false,
                "Événements d’ouverture et de fermeture désactivés · activez-les avant l’aperçu.",
                "WORLD_PLACEABLE_CONTAINER_EVENTS_DISABLED",
                string.Empty,
                "ContainerEventUnavailable",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
        }
        if (!TryBuildPlaceable(draft, dryRun: true, out _, out var profile, out var message) ||
            profile?.SecurityEvents?.ContainerEvents is null)
        {
            return new(
                false,
                message,
                LastPlaceableDiagnosticCode,
                string.Empty,
                "ContainerEventInvalid",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
        }

        var adapter = new PlaceableSecurityEventsRuntimeAdapter();
        var resolution = adapter.ResolveContainerEvents(profile);
        if (!resolution.Available || resolution.Handle is null)
        {
            return new(
                false,
                HumanizePlaceable(resolution.DiagnosticCode),
                resolution.DiagnosticCode ?? "WORLD_PLACEABLE_CONTAINER_EVENTS_UNAVAILABLE",
                string.Empty,
                "ContainerEventUnavailable",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
        }

        var opened = mode == StudioPlaceableContainerEventPreviewMode.Open;
        var previousState = opened ? "closed" : "open";
        var dispatch = adapter.ContainerStateChanged(
            resolution.Handle,
            profile,
            opened,
            "joueuse-locale",
            "success",
            previousState,
            "server",
            DateTimeOffset.UnixEpoch);
        var action = opened ? "ouverture" : "fermeture";
        return new(
            true,
            $"Aperçu de {action} · {dispatch.Binding} · actrice locale · résultat réussi · " +
            $"état précédent {(opened ? "fermé" : "ouvert")} · autorité serveur · aucun module modifié.",
            $"RPG_PLACEABLE_{dispatch.Payload.Name.ToUpperInvariant()}",
            dispatch.Binding,
            dispatch.Payload.Name,
            dispatch.Payload.ActorId,
            dispatch.Payload.Result,
            dispatch.Payload.PreviousState,
            dispatch.Payload.Authority);
    }

    private static StudioUx1TSmokeResult FailedUx1TSmoke() => new(
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
