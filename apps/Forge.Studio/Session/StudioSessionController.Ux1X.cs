using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux1XProvenance = "studio:ux1-x:door-foundation";
    private readonly SetWorldFeatureCatalogHandler _setDoorCatalogUx1X = new(new SystemClock());
    private StudioDoorFoundationDraft? _approvedDoorDraftUx1X;
    private ProjectRevision? _approvedDoorRevisionUx1X;

    internal IReadOnlyList<StudioDoorFoundationChoice> DoorFoundationChoicesUx1X
    {
        get
        {
            var project = Project;
            if (project is null)
            {
                return [];
            }

            var configuredBlueprints = project.Module.WorldFeatures.Doors
                .Select(profile => profile.BlueprintId)
                .ToHashSet();
            return project.Blueprints
                .Where(blueprint => blueprint.Kind == BlueprintKind.Door)
                .OrderBy(blueprint => blueprint.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Select(blueprint => new StudioDoorFoundationChoice(
                    blueprint.Id,
                    blueprint.DisplayName,
                    configuredBlueprints.Contains(blueprint.Id)))
                .ToArray();
        }
    }

    internal StudioDoorFoundationDraft? CreateDoorFoundationDraftUx1X(BlueprintId blueprintId)
    {
        var project = Project;
        if (project is null || project.Blueprints.All(blueprint =>
                blueprint.Id != blueprintId || blueprint.Kind != BlueprintKind.Door))
        {
            return null;
        }

        var matches = project.Module.WorldFeatures.Doors
            .Where(candidate => candidate.BlueprintId == blueprintId)
            .ToArray();
        if (matches.Length > 1)
        {
            return null;
        }

        var foundation = matches.SingleOrDefault()?.Foundation;
        var interaction = foundation?.Interaction;
        var initialState = foundation?.States.FirstOrDefault(state =>
            string.Equals(state.Id, foundation.InitialStateId, StringComparison.Ordinal))?.Kind;
        if (initialState is not (DoorWorldProfile.StateKind.Open or DoorWorldProfile.StateKind.Closed or
                DoorWorldProfile.StateKind.Locked))
        {
            initialState = DoorWorldProfile.StateKind.Closed;
        }

        return new StudioDoorFoundationDraft(
            blueprintId,
            foundation?.Placement ?? DoorWorldProfile.PlacementMode.FreePlacement,
            foundation?.ArchitectureSocketId ?? "mur-porte-standard",
            foundation?.Adapter?.KitId ?? "kit-architecture-standard",
            foundation?.Adapter?.SocketKind ?? "ouverture-standard",
            foundation?.Adapter?.WidthOffset ?? 0,
            foundation?.Adapter?.HeightOffset ?? 0,
            foundation?.Adapter?.DepthOffset ?? 0,
            initialState.Value,
            interaction?.Range ?? 2.5,
            NormalizeSideUx1X(interaction?.AllowedSide),
            interaction?.ClearanceWidth ?? 1.2,
            interaction?.ClearanceHeight ?? 2.4,
            interaction?.ClearanceDepth ?? 1,
            NormalizeAuthorityUx1X(interaction?.Authority),
            interaction?.QueueCapacity ?? 4,
            string.IsNullOrWhiteSpace(interaction?.FailureFeedbackKey)
                ? "door.unavailable"
                : interaction.FailureFeedbackKey);
    }

    internal StudioDoorFoundationPreview PreviewDoorFoundationUx1X(StudioDoorFoundationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelDoorFoundationPreviewUx1X();
        if (!TryBuildDoorFoundationUx1X(draft, dryRun: true, out var command, out var profile, out var message) ||
            command is null || profile?.Foundation is not { } foundation)
        {
            return FailedDoorFoundationPreviewUx1X(message);
        }

        var history = History!;
        var originalProject = history.State.Project;
        var originalRevision = history.State.Revision;
        var preview = _setDoorCatalogUx1X.Handle(history.State, command);
        if (!preview.Succeeded)
        {
            return FailedDoorFoundationPreviewUx1X(
                HumanizeDoorFoundationDiagnosticUx1X(preview.Diagnostics.FirstOrDefault()?.Code));
        }

        var closed = foundation.States.First(state => state.Kind == DoorWorldProfile.StateKind.Closed);
        var actorSide = foundation.Interaction.AllowedSide == "back" ? "back" : "front";
        var permissions = foundation.Portal?.Permissions.ToHashSet(StringComparer.Ordinal) ?? [];
        var runtime = WorldFeatureRuntimeResolver.InteractWithDoor(
            profile,
            closed.Id,
            actorSide,
            permissions,
            queuedRequests: 0,
            conditions: new HashSet<string>(StringComparer.Ordinal));
        if (!runtime.Accepted)
        {
            return FailedDoorFoundationPreviewUx1X(
                "La transition d’ouverture n’est pas disponible depuis l’état fermé.");
        }

        var dryRunUntouched = ReferenceEquals(originalProject, history.State.Project) &&
                              originalRevision == history.State.Revision &&
                              preview.NewRevision is null;
        if (!dryRunUntouched)
        {
            return FailedDoorFoundationPreviewUx1X(
                "L’aperçu n’a pas pu garantir l’absence d’écriture.");
        }

        var placement = PlacementSummaryUx1X(draft);
        var states = $"État initial · {StateLabelUx1X(draft.InitialState)} · 9 états présents";
        var interaction =
            $"Ouverture · portée {draft.InteractionRange:0.##} m · côté {SideLabelUx1X(draft.AllowedSide)} · " +
            $"passage {draft.ClearanceWidth:0.##} × {draft.ClearanceHeight:0.##} × {draft.ClearanceDepth:0.##} m · " +
            $"file {draft.QueueCapacity} · {AuthorityLabelUx1X(draft.Authority)} · " +
            $"échec « {FailureFeedbackLabelUx1X(draft.FailureFeedbackKey)} »";
        _approvedDoorDraftUx1X = draft;
        _approvedDoorRevisionUx1X = originalRevision;
        return new StudioDoorFoundationPreview(
            true,
            "Aperçu validé · aucune porte ni aucun fichier n’a été modifié.",
            $"{placement}\n{states}\n{interaction}\nInteraction simulée · fermée → ouverture en cours.",
            true,
            placement,
            states,
            interaction,
            true);
    }

    internal StudioDoorFoundationActionResult ApplyApprovedDoorFoundationUx1X()
    {
        var draft = _approvedDoorDraftUx1X;
        var approvedRevision = _approvedDoorRevisionUx1X;
        if (draft is null || approvedRevision is null || History is null)
        {
            CancelDoorFoundationPreviewUx1X();
            return new(false, "Relancez l’aperçu avant d’appliquer.");
        }
        if (History.State.Revision != approvedRevision.Value)
        {
            CancelDoorFoundationPreviewUx1X();
            return new(false, "Le module a changé depuis l’aperçu · relancez-le avant d’appliquer.");
        }
        if (!TryBuildDoorFoundationUx1X(draft, dryRun: true, out var command, out _, out var message) ||
            command is null)
        {
            CancelDoorFoundationPreviewUx1X();
            return new(false, message);
        }

        var preview = _setDoorCatalogUx1X.Handle(History.State, command);
        if (!preview.Succeeded)
        {
            CancelDoorFoundationPreviewUx1X();
            return new(false, HumanizeDoorFoundationDiagnosticUx1X(preview.Diagnostics.FirstOrDefault()?.Code));
        }

        var applied = _setDoorCatalogUx1X.Handle(
            History.State,
            command with
            {
                CommandId = CommandId.From(Guid.NewGuid()),
                DryRun = false,
            });
        if (!applied.Succeeded)
        {
            CancelDoorFoundationPreviewUx1X();
            return new(false, HumanizeDoorFoundationDiagnosticUx1X(applied.Diagnostics.FirstOrDefault()?.Code));
        }

        History.Record(applied);
        InvalidateContentIndexes();
        CancelDoorFoundationPreviewUx1X();
        return new(true, "Fondation de porte appliquée · enregistrez pour écrire le module.");
    }

    internal void CancelDoorFoundationPreviewUx1X()
    {
        _approvedDoorDraftUx1X = null;
        _approvedDoorRevisionUx1X = null;
    }

    private void CloseUx1X() => CancelDoorFoundationPreviewUx1X();

    internal StudioUx1XSmokeResult RunUx1XContractSmoke()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux1x-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var authored = OriginalModuleAuthoringService.CreateVeilleeDesEclats().State.Project;
            var targetBlueprint = authored.Blueprints.Single(blueprint => blueprint.Kind == BlueprintKind.Door);
            var neighborBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1e00000-0000-4000-8000-000000000002")),
                "porte-voisine",
                "Porte voisine",
                BlueprintKind.Door);
            var freshBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1e00000-0000-4000-8000-000000000003")),
                "porte-nouvelle",
                "Porte nouvelle",
                BlueprintKind.Door);
            var dialogueId = authored.Dialogues.Single().Id.Value;
            var originalProfile = CreateCompleteDoorProfileUx1X(
                targetBlueprint.Id,
                Guid.Parse("c1e00000-0000-4000-8000-000000000010"),
                dialogueId);
            var neighborProfile = CreateNeighborDoorProfileUx1X(
                neighborBlueprint.Id,
                Guid.Parse("c1e00000-0000-4000-8000-000000000011"));
            var catalog = new WorldFeatureCatalog(
                authored.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-x-catalog",
                authored.Module.WorldFeatures.Placeables,
                [originalProfile, neighborProfile],
                authored.Module.WorldFeatures.Triggers,
                authored.Module.WorldFeatures.Encounters,
                authored.Module.WorldFeatures.VehicleRoutes,
                authored.Module.WorldFeatures.Merchants,
                authored.Module.WorldFeatures.Companions,
                authored.Module.WorldFeatures.Scripts);
            var project = new ProjectDefinition(
                authored.Id,
                authored.Title,
                authored.Module.WithWorldFeatures(catalog),
                authored.Blueprints.Append(neighborBlueprint).Append(freshBlueprint).ToArray(),
                authored.Areas,
                authored.Dialogues,
                authored.Quests);
            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX1X_PROJECT_CREATE_FAILED diagnostics=" +
                    string.Join(" | ", created.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx1XSmoke();
            }

            var opened = editing.Open(tempRoot);
            if (!opened.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX1X_PROJECT_OPEN_FAILED diagnostics=" +
                    string.Join(" | ", opened.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx1XSmoke();
            }

            var loadedOriginalProfile = editing.Project!.Module.WorldFeatures.Doors.Single(profile =>
                profile.BlueprintId == targetBlueprint.Id);
            var loadedNeighborProfile = editing.Project.Module.WorldFeatures.Doors.Single(profile =>
                profile.BlueprintId == neighborBlueprint.Id);
            var loadedDoorOrder = editing.Project.Module.WorldFeatures.Doors.Select(profile => profile.Id).ToArray();
            var choices = editing.DoorFoundationChoicesUx1X;
            var source = editing.CreateDoorFoundationDraftUx1X(targetBlueprint.Id);
            var fresh = editing.CreateDoorFoundationDraftUx1X(freshBlueprint.Id);
            if (source is null || fresh is null)
            {
                return FailedUx1XSmoke();
            }

            var explicitSelection = choices.Count == 3 &&
                                    choices.Single(choice => choice.BlueprintId == targetBlueprint.Id).Configured &&
                                    !choices.Single(choice => choice.BlueprintId == freshBlueprint.Id).Configured &&
                                    source.BlueprintId == targetBlueprint.Id;
            var freeDraft = source with
            {
                Placement = DoorWorldProfile.PlacementMode.FreePlacement,
                InitialState = DoorWorldProfile.StateKind.Open,
            };
            var socketDraft = source with
            {
                Placement = DoorWorldProfile.PlacementMode.ArchitectureSocket,
                ArchitectureAnchor = "mur-grande-porte",
                ArchitectureKit = "kit-observatoire",
                OpeningKind = "double-battant",
                WidthAdjustment = .15,
                HeightAdjustment = .2,
                DepthAdjustment = -.05,
                InitialState = DoorWorldProfile.StateKind.Closed,
                InteractionRange = 3.25,
                AllowedSide = "front",
                ClearanceWidth = 1.8,
                ClearanceHeight = 2.8,
                ClearanceDepth = 1.25,
                Authority = "server",
                QueueCapacity = 3,
                FailureFeedbackKey = "door.blocked",
            };
            var lockedDraft = socketDraft with { InitialState = DoorWorldProfile.StateKind.Locked };

            var freeBuilt = editing.TryBuildDoorFoundationUx1X(
                freeDraft, true, out _, out var freeProfile, out _);
            var socketBuilt = editing.TryBuildDoorFoundationUx1X(
                socketDraft, true, out var dryRunCommand, out var socketProfile, out _);
            var lockedBuilt = editing.TryBuildDoorFoundationUx1X(
                lockedDraft, true, out _, out var lockedProfile, out _);
            var freshBuilt = editing.TryBuildDoorFoundationUx1X(
                fresh, true, out var freshCommand, out var freshProfile, out _);
            var freePreview = editing.PreviewDoorFoundationUx1X(freeDraft);
            var lockedPreview = editing.PreviewDoorFoundationUx1X(lockedDraft);
            var localAuthorityPreview = editing.PreviewDoorFoundationUx1X(socketDraft with
            {
                Authority = "local",
            });
            var freshDryRun = freshCommand is null
                ? null
                : editing._setDoorCatalogUx1X.Handle(editing.History!.State, freshCommand);
            var placementModes = freeBuilt && socketBuilt &&
                                 freeProfile?.Foundation?.Placement == DoorWorldProfile.PlacementMode.FreePlacement &&
                                 socketProfile?.Foundation is
                                 {
                                     Placement: DoorWorldProfile.PlacementMode.ArchitectureSocket,
                                     ArchitectureSocketId: "mur-grande-porte",
                                     Adapter:
                                     {
                                         KitId: "kit-observatoire",
                                         SocketKind: "double-battant",
                                         WidthOffset: .15,
                                         HeightOffset: .2,
                                         DepthOffset: -.05,
                                     },
                                 };
            var openState = freeProfile?.Foundation?.States.Single(state =>
                state.Kind == DoorWorldProfile.StateKind.Open).Id;
            var closedState = socketProfile?.Foundation?.States.Single(state =>
                state.Kind == DoorWorldProfile.StateKind.Closed).Id;
            var lockedState = lockedProfile?.Foundation?.States.Single(state =>
                state.Kind == DoorWorldProfile.StateKind.Locked).Id;
            var states = socketProfile?.Foundation?.States;
            var transitions = socketProfile?.Foundation?.Transitions;
            var nineStates = freeBuilt && socketBuilt && lockedBuilt &&
                             freePreview.Succeeded && lockedPreview.Succeeded && localAuthorityPreview.Succeeded &&
                             states is not null && transitions is not null &&
                             Enum.GetValues<DoorWorldProfile.StateKind>().All(kind =>
                                 states.Count(state => state.Kind == kind) >= 1) &&
                             freeProfile!.Foundation!.InitialStateId == openState &&
                             socketProfile!.Foundation!.InitialStateId == closedState &&
                             lockedProfile!.Foundation!.InitialStateId == lockedState &&
                             HasTransitionUx1X(transitions, DoorWorldProfile.StateKind.Closed,
                                 DoorWorldProfile.StateKind.Opening, "interact", states) &&
                             HasTransitionUx1X(transitions, DoorWorldProfile.StateKind.Opening,
                                 DoorWorldProfile.StateKind.Open, "animation-complete", states) &&
                             HasTransitionUx1X(transitions, DoorWorldProfile.StateKind.Open,
                                 DoorWorldProfile.StateKind.Closing, "interact", states) &&
                             HasTransitionUx1X(transitions, DoorWorldProfile.StateKind.Closing,
                                 DoorWorldProfile.StateKind.Closed, "animation-complete", states) &&
                             HasTransitionUx1X(transitions, DoorWorldProfile.StateKind.Locked,
                                 DoorWorldProfile.StateKind.Closed, "unlock", states);

            var staleBaseProject = editing.Project;
            var stalePreview = editing.PreviewDoorFoundationUx1X(socketDraft);
            var staleCommandBuilt = editing.TryBuildDoorFoundationUx1X(
                freeDraft,
                dryRun: false,
                out var interveningCommand,
                out _,
                out _);
            var intervening = interveningCommand is null
                ? null
                : editing._setDoorCatalogUx1X.Handle(editing.History!.State, interveningCommand);
            if (intervening?.Succeeded == true)
            {
                editing.History!.Record(intervening);
            }
            var staleApply = editing.ApplyApprovedDoorFoundationUx1X();
            var staleUndo = editing.Undo();
            var staleRevisionBlocked = stalePreview.Succeeded && staleCommandBuilt &&
                                       intervening?.Succeeded == true && !staleApply.Succeeded &&
                                       staleApply.Message.Contains("changé", StringComparison.OrdinalIgnoreCase) &&
                                       staleUndo.Succeeded && ReferenceEquals(staleBaseProject, editing.Project) &&
                                       !editing.IsDirty;

            var beforePreview = editing.Project;
            var beforeRevision = editing.History!.State.Revision;
            var preview = editing.PreviewDoorFoundationUx1X(socketDraft);
            var afterPreview = editing.Project;
            var dryRun = preview.Succeeded && preview.CanApply && ReferenceEquals(beforePreview, afterPreview) &&
                         editing.History.State.Revision == beforeRevision && !editing.IsDirty &&
                         dryRunCommand?.DryRun == true;

            var foundation = socketProfile!.Foundation!;
            var accepted = WorldFeatureRuntimeResolver.InteractWithDoor(
                socketProfile,
                closedState!,
                "front",
                foundation.Portal!.Permissions.ToHashSet(StringComparer.Ordinal),
                0,
                new HashSet<string>(StringComparer.Ordinal));
            var wrongSide = WorldFeatureRuntimeResolver.InteractWithDoor(
                socketProfile,
                closedState!,
                "back",
                foundation.Portal.Permissions.ToHashSet(StringComparer.Ordinal),
                0,
                new HashSet<string>(StringComparer.Ordinal));
            var queueFull = WorldFeatureRuntimeResolver.InteractWithDoor(
                socketProfile,
                closedState!,
                "front",
                foundation.Portal.Permissions.ToHashSet(StringComparer.Ordinal),
                foundation.Interaction.QueueCapacity,
                new HashSet<string>(StringComparer.Ordinal));
            var interaction = accepted is
            {
                Accepted: true,
                Reason: "accepted",
            } && accepted.CurrentState == foundation.States.Single(state =>
                state.Kind == DoorWorldProfile.StateKind.Opening).Id &&
                              wrongSide is { Accepted: false, Reason: "wrong-side" } &&
                              queueFull is { Accepted: false, Reason: "queue-full" } &&
                              foundation.Interaction is
                              {
                                  Range: 3.25,
                                  ClearanceWidth: 1.8,
                                  ClearanceHeight: 2.8,
                                  ClearanceDepth: 1.25,
                                  Authority: "server",
                                  QueueCapacity: 3,
                                  FailureFeedbackKey: "door.blocked",
                              };
            var lockedFailure = WorldFeatureRuntimeResolver.InteractWithDoor(
                socketProfile,
                foundation.States.Single(state => state.Kind == DoorWorldProfile.StateKind.Locked).Id,
                "front",
                foundation.Portal.Permissions.ToHashSet(StringComparer.Ordinal),
                0,
                new HashSet<string>(StringComparer.Ordinal));
            interaction = interaction && lockedFailure is { Accepted: false, Reason: "door.blocked" };

            var invalidPreviews = new[]
            {
                editing.PreviewDoorFoundationUx1X(socketDraft with { ArchitectureAnchor = string.Empty }),
                editing.PreviewDoorFoundationUx1X(socketDraft with { InteractionRange = 0 }),
                editing.PreviewDoorFoundationUx1X(socketDraft with { ClearanceWidth = 0 }),
                editing.PreviewDoorFoundationUx1X(socketDraft with { QueueCapacity = 0 }),
                editing.PreviewDoorFoundationUx1X(socketDraft with { Authority = "peer" }),
                editing.PreviewDoorFoundationUx1X(socketDraft with { FailureFeedbackKey = string.Empty }),
            };
            var invalidBlocked = invalidPreviews.All(result => !result.Succeeded && !result.CanApply) &&
                                 ReferenceEquals(beforePreview, editing.Project) && !editing.IsDirty;
            var humanDiagnostics = invalidPreviews.All(result => IsHumanDoorMessageUx1X(result.Message));

            var approvedForApply = editing.PreviewDoorFoundationUx1X(socketDraft);
            var apply = editing.ApplyApprovedDoorFoundationUx1X();
            var appliedProject = editing.Project;
            var appliedProfile = appliedProject?.Module.WorldFeatures.Doors.Single(profile =>
                profile.BlueprintId == targetBlueprint.Id);
            var preservedNeighbor = appliedProject?.Module.WorldFeatures.Doors.Single(profile =>
                profile.BlueprintId == neighborBlueprint.Id);
            var preservation = approvedForApply.Succeeded && apply.Succeeded && appliedProfile is not null &&
                               appliedProfile.Id == originalProfile.Id &&
                               appliedProfile.SchemaVersion == originalProfile.SchemaVersion &&
                               appliedProfile.Provenance == originalProfile.Provenance &&
                               ReferenceEquals(appliedProfile.Damage, loadedOriginalProfile.Damage) &&
                               ReferenceEquals(appliedProfile.Loading, loadedOriginalProfile.Loading) &&
                               ReferenceEquals(appliedProfile.Events, loadedOriginalProfile.Events) &&
                               appliedProfile.DialogueId == loadedOriginalProfile.DialogueId &&
                               appliedProfile.FactionId == loadedOriginalProfile.FactionId &&
                               ReferenceEquals(appliedProfile.Assembly, loadedOriginalProfile.Assembly) &&
                               ReferenceEquals(appliedProfile.AccessEvents, loadedOriginalProfile.AccessEvents) &&
                               ReferenceEquals(appliedProfile.Foundation!.Portal, loadedOriginalProfile.Foundation!.Portal) &&
                               ReferenceEquals(appliedProfile.Foundation.Links, loadedOriginalProfile.Foundation.Links) &&
                               ReferenceEquals(appliedProfile.Foundation.Overrides, loadedOriginalProfile.Foundation.Overrides) &&
                               ReferenceEquals(appliedProfile.Foundation.Navigation, loadedOriginalProfile.Foundation.Navigation) &&
                               ReferenceEquals(
                                   appliedProfile.Foundation.Adapter!.MaterialBindings,
                                   loadedOriginalProfile.Foundation.Adapter!.MaterialBindings) &&
                               ReferenceEquals(preservedNeighbor, loadedNeighborProfile) &&
                               appliedProject!.Module.WorldFeatures.Doors.Select(profile => profile.Id)
                                   .SequenceEqual(loadedDoorOrder);
            var navigationInternalOnly = freshBuilt && freshDryRun?.Succeeded == true &&
                                         freshProfile?.Foundation?.Navigation is
                                         {
                                             VisualizeInEditor: false,
                                         } && ReferenceEquals(
                                             appliedProfile?.Foundation?.Navigation,
                                             loadedOriginalProfile.Foundation!.Navigation) &&
                                         appliedProfile!.Foundation!.Navigation!.VisualizeInEditor == false;
            var exactCatalogOrder = dryRunCommand is not null &&
                                    freshCommand is not null && freshProfile is not null &&
                                    dryRunCommand.Catalog.Doors.Select(profile => profile.Id)
                                        .SequenceEqual(loadedDoorOrder) &&
                                    freshCommand.Catalog.Doors.Select(profile => profile.Id)
                                        .SequenceEqual(loadedDoorOrder.Append(freshProfile.Id));
            var canonicalCommand = apply.Succeeded && dryRunCommand is not null &&
                                   dryRunCommand.Catalog.Doors.Any(profile =>
                                       profile.BlueprintId == targetBlueprint.Id) &&
                                   exactCatalogOrder &&
                                   editing.History.State.Revision.Value == beforeRevision.Value + 1;

            var undo = editing.Undo();
            var undone = undo.Succeeded && ReferenceEquals(beforePreview, editing.Project) && !editing.IsDirty;
            var redo = editing.Redo();
            var redone = redo.Succeeded && editing.Project?.Module.WorldFeatures.Doors.Single(profile =>
                profile.BlueprintId == targetBlueprint.Id).Foundation?.Placement ==
                DoorWorldProfile.PlacementMode.ArchitectureSocket;
            var undoRedo = apply.Succeeded && undone && redone;

            var saved = editing.Save();
            editing.Close();
            var reopen = reopened.Open(tempRoot);
            var restored = reopened.Project?.Module.WorldFeatures.Doors.Single(profile =>
                profile.BlueprintId == targetBlueprint.Id);
            var persistence = saved.Succeeded && reopen.Succeeded && restored?.Foundation is
            {
                Placement: DoorWorldProfile.PlacementMode.ArchitectureSocket,
                ArchitectureSocketId: "mur-grande-porte",
                InitialStateId: var restoredInitial,
                Interaction:
                {
                    Range: 3.25,
                    AllowedSide: "front",
                    Authority: "server",
                    QueueCapacity: 3,
                    FailureFeedbackKey: "door.blocked",
                },
            } && restored.Foundation.States.Single(state =>
                state.Kind == DoorWorldProfile.StateKind.Closed).Id == restoredInitial &&
                              restored.Foundation.States.Count >= 9 &&
                              restored.Damage == originalProfile.Damage &&
                              restored.Loading == originalProfile.Loading &&
                              restored.Events.SequenceEqual(originalProfile.Events) &&
                              restored.DialogueId == originalProfile.DialogueId &&
                              restored.FactionId == originalProfile.FactionId &&
                              DoorAssemblyEquivalentUx1X(restored.Assembly, originalProfile.Assembly) &&
                              DoorAccessEquivalentUx1X(restored.AccessEvents, originalProfile.AccessEvents) &&
                              DoorPortalEquivalentUx1X(
                                  restored.Foundation!.Portal,
                                  originalProfile.Foundation!.Portal) &&
                              restored.Foundation.Links.SequenceEqual(originalProfile.Foundation.Links) &&
                              restored.Foundation.Adapter!.MaterialBindings.OrderBy(
                                      item => item.Key,
                                      StringComparer.Ordinal)
                                  .SequenceEqual(originalProfile.Foundation.Adapter!.MaterialBindings.OrderBy(
                                      item => item.Key,
                                      StringComparer.Ordinal)) &&
                              restored.Foundation.Overrides.OrderBy(item => item.Key, StringComparer.Ordinal)
                                  .SequenceEqual(originalProfile.Foundation.Overrides.OrderBy(
                                      item => item.Key, StringComparer.Ordinal)) &&
                              DoorNavigationEquivalentUx1X(
                                  restored.Foundation.Navigation,
                                  originalProfile.Foundation.Navigation);

            var succeeded = explicitSelection && placementModes && nineStates && interaction && dryRun &&
                            staleRevisionBlocked && canonicalCommand && navigationInternalOnly && preservation &&
                            undoRedo && persistence && invalidBlocked && humanDiagnostics;
            return new StudioUx1XSmokeResult(
                succeeded,
                explicitSelection,
                placementModes,
                nineStates,
                interaction,
                dryRun,
                staleRevisionBlocked,
                canonicalCommand,
                navigationInternalOnly,
                preservation,
                undoRedo,
                persistence,
                invalidBlocked,
                humanDiagnostics);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX1X_EXCEPTION {exception.GetType().Name}:{exception.Message}");
            return FailedUx1XSmoke();
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

    private bool TryBuildDoorFoundationUx1X(
        StudioDoorFoundationDraft draft,
        bool dryRun,
        out SetWorldFeatureCatalogCommand? command,
        out DoorWorldProfile? profile,
        out string message)
    {
        command = null;
        profile = null;
        message = string.Empty;
        var project = Project;
        var history = History;
        if (project is null || history is null)
        {
            message = "Ouvrez un module avant de configurer une porte.";
            return false;
        }

        var blueprint = project.Blueprints.SingleOrDefault(candidate => candidate.Id == draft.BlueprintId);
        if (blueprint?.Kind != BlueprintKind.Door)
        {
            message = "Sélectionnez explicitement un blueprint de porte.";
            return false;
        }
        if (draft.InitialState is not (DoorWorldProfile.StateKind.Open or DoorWorldProfile.StateKind.Closed or
                DoorWorldProfile.StateKind.Locked))
        {
            message = "L’état initial doit être ouvert, fermé ou verrouillé.";
            return false;
        }
        if (draft.Placement == DoorWorldProfile.PlacementMode.ArchitectureSocket &&
            (string.IsNullOrWhiteSpace(draft.ArchitectureAnchor) ||
             string.IsNullOrWhiteSpace(draft.ArchitectureKit) ||
             string.IsNullOrWhiteSpace(draft.OpeningKind)))
        {
            message = "Un ancrage d’architecture exige un emplacement, un kit et un type d’ouverture.";
            return false;
        }
        if (!double.IsFinite(draft.InteractionRange) || draft.InteractionRange <= 0)
        {
            message = "La portée d’interaction doit être supérieure à zéro.";
            return false;
        }
        if (new[] { draft.ClearanceWidth, draft.ClearanceHeight, draft.ClearanceDepth }
            .Any(value => !double.IsFinite(value) || value <= 0))
        {
            message = "Les trois dimensions du passage doivent être supérieures à zéro.";
            return false;
        }
        if (!double.IsFinite(draft.WidthAdjustment) || !double.IsFinite(draft.HeightAdjustment) ||
            !double.IsFinite(draft.DepthAdjustment))
        {
            message = "Les ajustements d’architecture doivent être des nombres finis.";
            return false;
        }
        if (draft.QueueCapacity < 1)
        {
            message = "La file d’interaction doit accepter au moins une demande.";
            return false;
        }
        if (NormalizeAuthorityUx1X(draft.Authority) != draft.Authority)
        {
            message = "Choisissez une autorité serveur ou une session locale.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(draft.FailureFeedbackKey))
        {
            message = "Choisissez un message à afficher lorsque l’ouverture échoue.";
            return false;
        }
        if (NormalizeSideUx1X(draft.AllowedSide) != draft.AllowedSide)
        {
            message = "Choisissez l’accès par les deux côtés, l’avant ou l’arrière.";
            return false;
        }

        var catalog = project.Module.WorldFeatures;
        var matching = catalog.Doors.Where(candidate => candidate.BlueprintId == draft.BlueprintId).ToArray();
        if (matching.Length > 1)
        {
            message = "Plusieurs fiches ciblent cette porte · corrigez le module avant l’édition.";
            return false;
        }

        try
        {
            var existing = matching.SingleOrDefault();
            var builtProfile = BuildDoorProfileUx1X(existing, draft);
            profile = builtProfile;
            var doors = existing is null
                ? catalog.Doors.Append(builtProfile).ToArray()
                : catalog.Doors.Select(candidate =>
                    candidate.Id == existing.Id ? builtProfile : candidate).ToArray();
            var candidateCatalog = new WorldFeatureCatalog(
                catalog.Id,
                catalog.SchemaVersion,
                catalog.Provenance,
                catalog.Placeables,
                doors,
                catalog.Triggers,
                catalog.Encounters,
                catalog.VehicleRoutes,
                catalog.Merchants,
                catalog.Companions,
                catalog.Scripts);
            command = new SetWorldFeatureCatalogCommand(
                CommandId.From(Guid.NewGuid()),
                history.State.Revision,
                candidateCatalog,
                dryRun);
            return true;
        }
        catch (ArgumentException)
        {
            message = "La fondation de porte est incomplète · vérifiez le placement, les états et l’interaction.";
            return false;
        }
    }

    private static DoorWorldProfile BuildDoorProfileUx1X(
        DoorWorldProfile? existing,
        StudioDoorFoundationDraft draft)
    {
        var profileId = existing?.Id ?? StableDoorIdUx1X(draft.BlueprintId, "profile");
        var original = existing?.Foundation;
        var states = EnsureCanonicalDoorStatesUx1X(original?.States);
        var transitions = EnsureCanonicalDoorTransitionsUx1X(original?.Transitions, states);
        var initialStateId = states.First(state => state.Kind == draft.InitialState).Id;
        var adapter = original?.Adapter;
        if (draft.Placement == DoorWorldProfile.PlacementMode.ArchitectureSocket)
        {
            adapter = new DoorWorldProfile.KitAdapter(
                draft.ArchitectureKit.Trim(),
                draft.OpeningKind.Trim(),
                adapter?.MaterialBindings ?? new Dictionary<string, string>(StringComparer.Ordinal),
                draft.WidthAdjustment,
                draft.HeightAdjustment,
                draft.DepthAdjustment);
        }

        var navigation = original?.Navigation ?? CreateInternalNavigationUx1X(profileId, states);
        var foundation = new DoorWorldProfile.FoundationPolicy(
            draft.Placement,
            draft.Placement == DoorWorldProfile.PlacementMode.ArchitectureSocket
                ? draft.ArchitectureAnchor.Trim()
                : original?.ArchitectureSocketId,
            draft.Placement == DoorWorldProfile.PlacementMode.ArchitectureSocket ? adapter : original?.Adapter,
            initialStateId,
            states,
            transitions,
            new DoorWorldProfile.InteractionPolicy(
                draft.InteractionRange,
                draft.AllowedSide,
                draft.ClearanceWidth,
                draft.ClearanceHeight,
                draft.ClearanceDepth,
                draft.Authority,
                draft.QueueCapacity,
                draft.FailureFeedbackKey.Trim()),
            original?.Portal,
            original?.Links ?? [],
            original?.Overrides ?? new Dictionary<string, string>(StringComparer.Ordinal),
            navigation);

        return existing is null
            ? new DoorWorldProfile(
                profileId,
                WorldFeatureCatalog.CurrentSchemaVersion,
                Ux1XProvenance,
                draft.BlueprintId,
                null,
                null,
                [],
                null,
                null,
                null,
                foundation)
            : existing with { Foundation = foundation };
    }

    private static IReadOnlyList<DoorWorldProfile.State> EnsureCanonicalDoorStatesUx1X(
        IReadOnlyList<DoorWorldProfile.State>? source)
    {
        var states = (source ?? []).ToList();
        foreach (var kind in Enum.GetValues<DoorWorldProfile.StateKind>())
        {
            if (states.Any(state => state.Kind == kind))
            {
                continue;
            }

            var stem = kind.ToString().ToLowerInvariant();
            var id = stem;
            var suffix = 2;
            while (states.Any(state => string.Equals(state.Id, id, StringComparison.Ordinal)))
            {
                id = $"{stem}-{suffix++}";
            }
            states.Add(DefaultDoorStateUx1X(id, kind));
        }
        return states.ToArray();
    }

    private static DoorWorldProfile.State DefaultDoorStateUx1X(
        string id,
        DoorWorldProfile.StateKind kind) => new(
        id,
        kind,
        $"animation:{id}",
        kind is DoorWorldProfile.StateKind.Closed or DoorWorldProfile.StateKind.Closing or
            DoorWorldProfile.StateKind.Locked or DoorWorldProfile.StateKind.Jammed or
            DoorWorldProfile.StateKind.Disabled or DoorWorldProfile.StateKind.Custom,
        kind is DoorWorldProfile.StateKind.Open or DoorWorldProfile.StateKind.Closed or
            DoorWorldProfile.StateKind.Locked or DoorWorldProfile.StateKind.Jammed or
            DoorWorldProfile.StateKind.Custom);

    private static IReadOnlyList<DoorWorldProfile.Transition> EnsureCanonicalDoorTransitionsUx1X(
        IReadOnlyList<DoorWorldProfile.Transition>? source,
        IReadOnlyList<DoorWorldProfile.State> states)
    {
        var transitions = (source ?? []).ToList();
        EnsureTransitionUx1X(transitions, states, DoorWorldProfile.StateKind.Closed,
            DoorWorldProfile.StateKind.Opening, "interact", 1);
        EnsureTransitionUx1X(transitions, states, DoorWorldProfile.StateKind.Opening,
            DoorWorldProfile.StateKind.Open, "animation-complete", .25);
        EnsureTransitionUx1X(transitions, states, DoorWorldProfile.StateKind.Open,
            DoorWorldProfile.StateKind.Closing, "interact", 1);
        EnsureTransitionUx1X(transitions, states, DoorWorldProfile.StateKind.Closing,
            DoorWorldProfile.StateKind.Closed, "animation-complete", .25);
        EnsureTransitionUx1X(transitions, states, DoorWorldProfile.StateKind.Locked,
            DoorWorldProfile.StateKind.Closed, "unlock", .25);
        return transitions.ToArray();
    }

    private static void EnsureTransitionUx1X(
        List<DoorWorldProfile.Transition> transitions,
        IReadOnlyList<DoorWorldProfile.State> states,
        DoorWorldProfile.StateKind from,
        DoorWorldProfile.StateKind to,
        string trigger,
        double duration)
    {
        var fromId = states.First(state => state.Kind == from).Id;
        var toId = states.First(state => state.Kind == to).Id;
        var index = transitions.FindIndex(transition =>
            string.Equals(transition.FromStateId, fromId, StringComparison.Ordinal) &&
            string.Equals(transition.Trigger, trigger, StringComparison.Ordinal) &&
            string.IsNullOrWhiteSpace(transition.Condition));
        if (index < 0)
        {
            transitions.Add(new DoorWorldProfile.Transition(fromId, toId, trigger, duration, null));
            return;
        }

        var current = transitions[index];
        transitions[index] = current with
        {
            ToStateId = toId,
            DurationSeconds = current.DurationSeconds < 0 ? duration : current.DurationSeconds,
        };
    }

    private static DoorWorldProfile.NavigationPolicy CreateInternalNavigationUx1X(
        Guid profileId,
        IReadOnlyList<DoorWorldProfile.State> states)
    {
        var blocking = states
            .Where(state => state.Kind is not (DoorWorldProfile.StateKind.Opening or
                                               DoorWorldProfile.StateKind.Closing) &&
                            state.CollisionEnabled)
            .Select(state => state.Id)
            .ToArray();
        var traversable = states
            .Where(state => state.Kind is not (DoorWorldProfile.StateKind.Opening or
                                               DoorWorldProfile.StateKind.Closing) &&
                            !state.CollisionEnabled)
            .Select(state => state.Id)
            .ToArray();
        return new DoorWorldProfile.NavigationPolicy(
            $"door-obstacle:{profileId:D}",
            $"door-link:{profileId:D}",
            blocking,
            traversable,
            .75,
            .25,
            VisualizeInEditor: false);
    }

    private static DoorWorldProfile CreateCompleteDoorProfileUx1X(
        BlueprintId blueprintId,
        Guid id,
        Guid dialogueId)
    {
        var states = EnsureCanonicalDoorStatesUx1X(null);
        var transitions = EnsureCanonicalDoorTransitionsUx1X(null, states);
        var access = new DoorAccessEventProfile(
            StableDoorComponentUx1X(id, 20),
            DoorAccessEventProfile.CurrentSchemaVersion,
            "test:ux1-x-access",
            "assets/doors/observatory.glb",
            TransformValue.Identity,
            new(
                StableDoorComponentUx1X(id, 21),
                DoorAccessLockState.Locked,
                ["key:observatory"],
                "mechanics",
                12,
                null,
                true,
                true,
                true,
                false,
                ["door.unlock"]),
            new(
                StableDoorComponentUx1X(id, 22),
                "1.0.0",
                "DoorOpenRejected",
                [new("fr-FR", "La porte ne répond pas.")],
                "audio:door-denied",
                "animation:rattle",
                "scripts/door_rejected"),
            new(
                StableDoorComponentUx1X(id, 23),
                "door:open",
                "damage:embers",
                10,
                12,
                30,
                "observatory",
                "vfx:embers",
                "allies",
                true,
                false,
                ["front"],
                ["interact"]),
            new(StableDoorComponentUx1X(id, 24), "1.0.0", "PortalUseRequested", "PortalUseCompleted"),
            new(StableDoorComponentUx1X(id, 25), "1.0.0", "DoorOpened", "DoorClosed"),
            new Dictionary<string, string> { ["hidden-access"] = "preserved" },
            new(
                StableDoorComponentUx1X(id, 26),
                DoorAccessEventProfile.CurrentSchemaVersion,
                "test:ux1-x-lock-events",
                "1.0.0",
                "DoorLocked",
                "DoorUnlocked"),
            true);
        var navigation = new DoorWorldProfile.NavigationPolicy(
            "nav-obstacle:observatory",
            "nav-link:observatory",
            states.Where(state => state.CollisionEnabled && state.Kind is not DoorWorldProfile.StateKind.Closing)
                .Select(state => state.Id).ToArray(),
            states.Where(state => !state.CollisionEnabled && state.Kind is not DoorWorldProfile.StateKind.Opening)
                .Select(state => state.Id).ToArray(),
            .7,
            .3,
            false);
        return new DoorWorldProfile(
            id,
            WorldFeatureCatalog.CurrentSchemaVersion,
            "test:ux1-x-original",
            blueprintId,
            new(90, 4, false, "door.damaged", "door.breached", true, "assets/debris.glb", "repair:door"),
            new("assets/loading/observatory.png", "loading.observatory", "tip.observatory", false, .5),
            [new("heartbeat", "scripts/door_heartbeat", false, true)],
            dialogueId,
            "veilleuses",
            new(
                DoorWorldProfile.AssemblyKind.Double,
                [new(StableDoorComponentUx1X(id, 12), "left", -1, 0, false)],
                "double-open",
                false,
                "close-both"),
            new(
                DoorWorldProfile.PlacementMode.FreePlacement,
                null,
                new(
                    "kit-original",
                    "ouverture-originale",
                    new Dictionary<string, string> { ["bois"] = "materiau-chene" },
                    0,
                    0,
                    0),
                states.Single(state => state.Kind == DoorWorldProfile.StateKind.Closed).Id,
                states,
                transitions,
                new(2.5, "both", 1.2, 2.4, 1, "server", 4, "door.unavailable"),
                new(
                    DoorWorldProfile.DestinationKind.Waypoint,
                    StableDoorComponentUx1X(id, 13),
                    "anchor:observatory",
                    180,
                    true,
                    ["portal.travel"]),
                [new(
                    StableDoorComponentUx1X(id, 14),
                    StableDoorComponentUx1X(id, 15),
                    false,
                    "Retour vers le parvis")],
                new Dictionary<string, string> { ["hidden-foundation"] = "preserved" },
                navigation),
            access,
            true);
    }

    private static DoorWorldProfile CreateNeighborDoorProfileUx1X(BlueprintId blueprintId, Guid id)
    {
        var states = EnsureCanonicalDoorStatesUx1X(null);
        return new DoorWorldProfile(
            id,
            WorldFeatureCatalog.CurrentSchemaVersion,
            "test:ux1-x-neighbor",
            blueprintId,
            null,
            null,
            [],
            null,
            null,
            null,
            new(
                DoorWorldProfile.PlacementMode.FreePlacement,
                null,
                null,
                states.Single(state => state.Kind == DoorWorldProfile.StateKind.Closed).Id,
                states,
                EnsureCanonicalDoorTransitionsUx1X(null, states),
                new(2, "both", 1, 2, 1, "server", 2, "door.unavailable"),
                null,
                [],
                new Dictionary<string, string>(),
                CreateInternalNavigationUx1X(id, states)));
    }

    private static bool HasTransitionUx1X(
        IReadOnlyList<DoorWorldProfile.Transition> transitions,
        DoorWorldProfile.StateKind from,
        DoorWorldProfile.StateKind to,
        string trigger,
        IReadOnlyList<DoorWorldProfile.State> states)
    {
        var fromId = states.First(state => state.Kind == from).Id;
        var toId = states.First(state => state.Kind == to).Id;
        return transitions.Any(transition =>
            transition.FromStateId == fromId && transition.ToStateId == toId &&
            transition.Trigger == trigger && transition.DurationSeconds >= 0);
    }

    private static Guid StableDoorIdUx1X(BlueprintId blueprintId, string purpose)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{blueprintId.Value:D}:ux1-x:{purpose}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }

    private static Guid StableDoorComponentUx1X(Guid root, byte suffix)
    {
        var bytes = root.ToByteArray();
        bytes[^1] = suffix;
        return new Guid(bytes);
    }

    private static bool DoorAssemblyEquivalentUx1X(
        DoorWorldProfile.AssemblyPolicy? left,
        DoorWorldProfile.AssemblyPolicy? right) =>
        left is null && right is null ||
        left is not null && right is not null &&
        left.Kind == right.Kind &&
        left.Sequence == right.Sequence &&
        left.Interlocked == right.Interlocked &&
        left.FailureRecovery == right.FailureRecovery &&
        left.Panels.SequenceEqual(right.Panels);

    private static bool DoorAccessEquivalentUx1X(
        DoorAccessEventProfile? left,
        DoorAccessEventProfile? right) =>
        left is null && right is null ||
        left is not null && right is not null &&
        left.Id == right.Id &&
        left.SchemaVersion == right.SchemaVersion &&
        left.Provenance == right.Provenance &&
        left.AssetId == right.AssetId &&
        left.Transform == right.Transform &&
        left.Lock.Id == right.Lock.Id &&
        left.Lock.State == right.Lock.State &&
        left.Lock.CredentialIds.SequenceEqual(right.Lock.CredentialIds) &&
        left.Lock.SkillId == right.Lock.SkillId &&
        left.Lock.Difficulty.Equals(right.Lock.Difficulty) &&
        left.Lock.CodeHash == right.Lock.CodeHash &&
        left.Lock.Hackable == right.Lock.Hackable &&
        left.Lock.Relockable == right.Lock.Relockable &&
        left.Lock.CanJam == right.Lock.CanJam &&
        left.Lock.ConsumeCredential == right.Lock.ConsumeCredential &&
        left.Lock.Permissions.SequenceEqual(right.Lock.Permissions) &&
        left.Rejection.Id == right.Rejection.Id &&
        left.Rejection.ForgeScriptApiVersion == right.Rejection.ForgeScriptApiVersion &&
        left.Rejection.EventBinding == right.Rejection.EventBinding &&
        left.Rejection.Texts.SequenceEqual(right.Rejection.Texts) &&
        left.Rejection.SoundAssetId == right.Rejection.SoundAssetId &&
        left.Rejection.AnimationId == right.Rejection.AnimationId &&
        left.Rejection.ScriptId == right.Rejection.ScriptId &&
        left.Trap.Id == right.Trap.Id &&
        left.Trap.TriggerId == right.Trap.TriggerId &&
        left.Trap.PayloadId == right.Trap.PayloadId &&
        left.Trap.DetectionDifficulty.Equals(right.Trap.DetectionDifficulty) &&
        left.Trap.DisarmDifficulty.Equals(right.Trap.DisarmDifficulty) &&
        left.Trap.ResetDelaySeconds.Equals(right.Trap.ResetDelaySeconds) &&
        left.Trap.OwnerId == right.Trap.OwnerId &&
        left.Trap.VisualEffectId == right.Trap.VisualEffectId &&
        left.Trap.FriendlyPolicy == right.Trap.FriendlyPolicy &&
        left.Trap.Armed == right.Trap.Armed &&
        left.Trap.OneShot == right.Trap.OneShot &&
        left.Trap.Sides.SequenceEqual(right.Trap.Sides) &&
        left.Trap.Events.SequenceEqual(right.Trap.Events) &&
        left.PortalUse == right.PortalUse &&
        left.StateEvents == right.StateEvents &&
        left.LockStateEvents == right.LockStateEvents &&
        left.LockStateEventsRequiredAtRuntime == right.LockStateEventsRequiredAtRuntime &&
        left.Overrides.OrderBy(item => item.Key, StringComparer.Ordinal)
            .SequenceEqual(right.Overrides.OrderBy(item => item.Key, StringComparer.Ordinal));

    private static bool DoorPortalEquivalentUx1X(
        DoorWorldProfile.PortalPolicy? left,
        DoorWorldProfile.PortalPolicy? right) =>
        left is null && right is null ||
        left is not null && right is not null &&
        left.DestinationKind == right.DestinationKind &&
        left.DestinationId == right.DestinationId &&
        left.SpawnAnchorId == right.SpawnAnchorId &&
        left.ExitYawDegrees.Equals(right.ExitYawDegrees) &&
        left.Preload == right.Preload &&
        left.Permissions.SequenceEqual(right.Permissions);

    private static bool DoorNavigationEquivalentUx1X(
        DoorWorldProfile.NavigationPolicy? left,
        DoorWorldProfile.NavigationPolicy? right) =>
        left is null && right is null ||
        left is not null && right is not null &&
        left.ObstacleId == right.ObstacleId &&
        left.LinkId == right.LinkId &&
        left.BlockingStateIds.SequenceEqual(right.BlockingStateIds) &&
        left.TraversableStateIds.SequenceEqual(right.TraversableStateIds) &&
        left.OpeningPassableAt.Equals(right.OpeningPassableAt) &&
        left.ClosingBlockedAt.Equals(right.ClosingBlockedAt) &&
        left.VisualizeInEditor == right.VisualizeInEditor;

    private static string NormalizeSideUx1X(string? side) => side switch
    {
        "front" => "front",
        "back" => "back",
        _ => "both",
    };

    private static string NormalizeAuthorityUx1X(string? authority) => authority switch
    {
        "local" => "local",
        _ => "server",
    };

    private static string PlacementSummaryUx1X(StudioDoorFoundationDraft draft) =>
        draft.Placement == DoorWorldProfile.PlacementMode.ArchitectureSocket
            ? $"Placement · ancrage {draft.ArchitectureAnchor.Trim()} · kit {draft.ArchitectureKit.Trim()} · " +
              $"ouverture {draft.OpeningKind.Trim()}"
            : "Placement · libre dans la vue 3D";

    private static string StateLabelUx1X(DoorWorldProfile.StateKind state) => state switch
    {
        DoorWorldProfile.StateKind.Open => "ouverte",
        DoorWorldProfile.StateKind.Locked => "verrouillée",
        _ => "fermée",
    };

    private static string SideLabelUx1X(string side) => side switch
    {
        "front" => "avant",
        "back" => "arrière",
        _ => "deux côtés",
    };

    private static string AuthorityLabelUx1X(string authority) => authority == "local"
        ? "autorité de la session locale"
        : "autorité serveur";

    private static string FailureFeedbackLabelUx1X(string key) => key switch
    {
        "door.blocked" => "Passage bloqué",
        "door.busy" => "Porte occupée",
        "door.unavailable" => "Porte indisponible",
        _ => "Message existant conservé",
    };

    private static StudioDoorFoundationPreview FailedDoorFoundationPreviewUx1X(string message) =>
        new(false, message, string.Empty, false);

    private static StudioUx1XSmokeResult FailedUx1XSmoke() => new(
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

    private static string HumanizeDoorFoundationDiagnosticUx1X(string? code) => code switch
    {
        "command.revision_conflict" => "Le module a changé · relancez l’aperçu.",
        "world.door.socket_adapter_missing" =>
            "Complétez l’emplacement, le kit et le type d’ouverture de l’ancrage.",
        "world.door.state_machine_invalid" =>
            "La machine d’états de la porte contient une configuration incomplète.",
        "world.door.transition_invalid" =>
            "Une transition de porte est incomplète ou cible un état absent.",
        "world.door.interaction_invalid" =>
            "La portée, le passage ou la file d’interaction est invalide.",
        "world.reference.blueprint_missing" =>
            "Le blueprint de porte sélectionné n’est plus disponible.",
        _ => "La fondation de porte ne respecte pas les contraintes du module.",
    };

    private static bool IsHumanDoorMessageUx1X(string message) =>
        !string.IsNullOrWhiteSpace(message) &&
        !message.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("world.door", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("module/worldFeatures", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains(".json", StringComparison.OrdinalIgnoreCase) &&
        !Guid.TryParse(message, out _);
}
