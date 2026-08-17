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
using Forge.RpgKit.Application.Doors;
using Forge.RpgKit.Domain.Placeables;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux1AGProvenance = "studio:ux1-ag:door-access-events";
    private const string ForgeScriptApiUx1AG = "1.0.0";

    private readonly SetWorldFeatureCatalogHandler _setDoorCatalogUx1AG = new(new SystemClock());
    private StudioDoorAccessEventsDraft? _approvedDoorAccessEventsDraftUx1AG;
    private ProjectRevision? _approvedDoorAccessEventsRevisionUx1AG;

    internal IReadOnlyList<StudioDoorAccessEventsChoice> DoorAccessEventsChoicesUx1AG
    {
        get
        {
            var project = Project;
            if (project is null)
            {
                return [];
            }

            var configured = project.Module.WorldFeatures.Doors
                .Where(profile => profile.AccessEvents is not null)
                .Select(profile => profile.BlueprintId)
                .ToHashSet();
            var doors = project.Blueprints
                .Where(blueprint => blueprint.Kind == BlueprintKind.Door)
                .OrderBy(blueprint => blueprint.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(blueprint => blueprint.Slug, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            var duplicateNames = doors
                .GroupBy(blueprint => blueprint.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
            return doors.Select(blueprint => new StudioDoorAccessEventsChoice(
                blueprint.Id,
                duplicateNames.Contains(blueprint.DisplayName)
                    ? $"{blueprint.DisplayName} · {blueprint.Slug}"
                    : blueprint.DisplayName,
                configured.Contains(blueprint.Id))).ToArray();
        }
    }

    internal StudioDoorAccessEventsDraft? CreateDoorAccessEventsDraftUx1AG(BlueprintId blueprintId) =>
        TryCreateDoorAccessEventsDraftUx1AG(blueprintId, out var draft, out _) ? draft : null;

    internal bool TryCreateDoorAccessEventsDraftUx1AG(
        BlueprintId blueprintId,
        out StudioDoorAccessEventsDraft? draft,
        out string message)
    {
        draft = null;
        message = string.Empty;
        var project = Project;
        if (project is null)
        {
            message = "Ouvrez un module avant de configurer une porte.";
            return false;
        }
        if (project.Blueprints.All(blueprint =>
                blueprint.Id != blueprintId || blueprint.Kind != BlueprintKind.Door))
        {
            message = "La porte sélectionnée n’est plus disponible dans le module.";
            return false;
        }

        var matches = project.Module.WorldFeatures.Doors
            .Where(profile => profile.BlueprintId == blueprintId)
            .ToArray();
        if (matches.Length > 1)
        {
            message = "Plusieurs fiches ciblent cette porte · corrigez le module avant l’édition.";
            return false;
        }

        var profile = matches.SingleOrDefault();
        var access = profile?.AccessEvents;
        if (access is not null)
        {
            var diagnostic = DoorAccessEventProfileValidator.Validate(
                    access,
                    "module/worldFeatures/doors/selected/accessEvents")
                .FirstOrDefault();
            if (diagnostic is not null)
            {
                message = HumanizeDoorAccessEventsDiagnosticUx1AG(diagnostic.Code);
                return false;
            }
        }
        var french = access?.Rejection.Texts.FirstOrDefault(item =>
            string.Equals(item.Locale, "fr-FR", StringComparison.OrdinalIgnoreCase))?.Text;
        var english = access?.Rejection.Texts.FirstOrDefault(item =>
            string.Equals(item.Locale, "en-US", StringComparison.OrdinalIgnoreCase))?.Text;
        draft = new StudioDoorAccessEventsDraft(
            blueprintId,
            profile?.AccessEventsRequiredAtRuntime ?? true,
            access?.Lock.State ?? DoorAccessLockState.Locked,
            access?.Lock.CredentialIds.ToArray() ?? ["key:door"],
            access?.Lock.SkillId ?? "mechanics",
            access?.Lock.Difficulty ?? 10,
            access?.Lock.Hackable ?? true,
            access?.Lock.Relockable ?? true,
            access?.Lock.CanJam ?? true,
            access?.Lock.ConsumeCredential ?? false,
            access?.Lock.Permissions.ToArray() ?? ["door.unlock"],
            access?.Rejection.ForgeScriptApiVersion ?? ForgeScriptApiUx1AG,
            french ?? "La porte est verrouillée.",
            english ?? "The door is locked.",
            access?.Rejection.SoundAssetId ?? "audio:door-locked",
            access?.Rejection.AnimationId ?? "animation:door-rattle",
            access?.Rejection.ScriptId ?? "scripts/door_open_rejected",
            access?.Trap.TriggerId ?? "door:interact",
            access?.Trap.PayloadId ?? "damage:door-trap",
            access?.Trap.DetectionDifficulty ?? 10,
            access?.Trap.DisarmDifficulty ?? 12,
            access?.Trap.ResetDelaySeconds ?? 30,
            access?.Trap.OwnerId ?? "door-owner",
            access?.Trap.VisualEffectId ?? "vfx:door-trap",
            access?.Trap.FriendlyPolicy ?? "door-allies",
            access?.Trap.Armed ?? true,
            access?.Trap.OneShot ?? false,
            access?.Trap.Sides.ToArray() ?? ["front", "back"],
            access?.Trap.Events.ToArray() ?? ["interact"],
            access?.StateEvents.ForgeScriptApiVersion ?? ForgeScriptApiUx1AG,
            access?.LockStateEvents is not null || access is null,
            access?.LockStateEventsRequiredAtRuntime ?? true,
            access?.LockStateEvents?.ForgeScriptApiVersion ?? ForgeScriptApiUx1AG);
        return true;
    }

    internal StudioDoorAccessEventsPreview PreviewDoorAccessEventsUx1AG(
        StudioDoorAccessEventsDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelDoorAccessEventsPreviewUx1AG();
        var history = History;
        if (!TryBuildDoorAccessEventsUx1AG(draft, dryRun: true, out var command, out var profile,
                out var message) || command is null || profile?.AccessEvents is null || history is null)
        {
            return FailedDoorAccessEventsPreviewUx1AG(message);
        }

        var originalProject = history.State.Project;
        var originalRevision = history.State.Revision;
        var preview = _setDoorCatalogUx1AG.Handle(history.State, command);
        if (!preview.Succeeded)
        {
            return FailedDoorAccessEventsPreviewUx1AG(
                HumanizeDoorAccessEventsDiagnosticUx1AG(preview.Diagnostics.FirstOrDefault()?.Code));
        }
        if (!ReferenceEquals(originalProject, history.State.Project) ||
            history.State.Revision != originalRevision || preview.NewRevision is not null)
        {
            return FailedDoorAccessEventsPreviewUx1AG(
                "L’aperçu n’a pas pu garantir l’absence d’écriture.");
        }

        try
        {
            var lockAccepted = PreviewDoorLockUx1AG(profile, draft);
            var rejectionAccepted = PreviewDoorRejectionUx1AG(profile, draft);
            var trapAccepted = PreviewDoorTrapUx1AG(profile, draft);
            var statesAccepted = PreviewDoorStateEventsUx1AG(profile);
            var lockEventsAccepted = PreviewDoorLockEventsUx1AG(profile, draft);
            if (!lockAccepted || !rejectionAccepted || !trapAccepted || !statesAccepted ||
                !lockEventsAccepted)
            {
                return FailedDoorAccessEventsPreviewUx1AG(
                    "La simulation du verrou, du refus, du piège ou des événements ne correspond pas aux réglages choisis.");
            }

            var access = profile.AccessEvents;
            var lockSummary =
                $"Verrou · {HumanizeDoorLockStateUx1AG(access.Lock.State)} · " +
                $"{access.Lock.CredentialIds.Count} clé(s) ou credential(s) · difficulté {access.Lock.Difficulty:0.##}";
            var rejectionSummary =
                $"Refus · {access.Rejection.Texts.Count} langue(s) · son, animation et script prêts";
            var trapSummary =
                $"Piège · {(access.Trap.Armed ? "armé" : "désarmé")} · " +
                $"{access.Trap.Sides.Count} côté(s) · reset {access.Trap.ResetDelaySeconds:0.##} s";
            var stateSummary = draft.LockStateEventsEnabled
                ? "Événements · ouverture, fermeture, verrouillage et déverrouillage après transition terminale"
                : "Événements · ouverture et fermeture après transition terminale";
            _approvedDoorAccessEventsDraftUx1AG = draft;
            _approvedDoorAccessEventsRevisionUx1AG = originalRevision;
            return new(
                true,
                "Aperçu validé · aucune porte ni aucun fichier n’a été modifié.",
                $"{lockSummary}\n{rejectionSummary}\n{trapSummary}\n{stateSummary}",
                true,
                lockAccepted,
                rejectionAccepted,
                trapAccepted,
                statesAccepted,
                lockEventsAccepted);
        }
        catch (InvalidOperationException)
        {
            return FailedDoorAccessEventsPreviewUx1AG(
                "La simulation runtime a refusé ces réglages · vérifiez le verrou, le piège et les événements.");
        }
    }

    internal StudioDoorAccessEventsActionResult ApplyApprovedDoorAccessEventsUx1AG()
    {
        var draft = _approvedDoorAccessEventsDraftUx1AG;
        var approvedRevision = _approvedDoorAccessEventsRevisionUx1AG;
        var history = History;
        if (draft is null || approvedRevision is null || history is null)
        {
            CancelDoorAccessEventsPreviewUx1AG();
            return new(false, "Relancez l’aperçu avant d’appliquer.");
        }
        if (history.State.Revision != approvedRevision.Value)
        {
            CancelDoorAccessEventsPreviewUx1AG();
            return new(false, "Le module a changé depuis l’aperçu · relancez-le avant d’appliquer.", true);
        }
        if (!TryBuildDoorAccessEventsUx1AG(draft, dryRun: true, out var command, out _, out var message) ||
            command is null)
        {
            CancelDoorAccessEventsPreviewUx1AG();
            return new(false, message);
        }

        var confirmation = _setDoorCatalogUx1AG.Handle(history.State, command);
        if (!confirmation.Succeeded)
        {
            CancelDoorAccessEventsPreviewUx1AG();
            return new(false,
                HumanizeDoorAccessEventsDiagnosticUx1AG(confirmation.Diagnostics.FirstOrDefault()?.Code));
        }

        var applied = _setDoorCatalogUx1AG.Handle(
            history.State,
            command with
            {
                CommandId = CommandId.From(Guid.NewGuid()),
                DryRun = false,
            });
        if (!applied.Succeeded)
        {
            CancelDoorAccessEventsPreviewUx1AG();
            return new(false,
                HumanizeDoorAccessEventsDiagnosticUx1AG(applied.Diagnostics.FirstOrDefault()?.Code));
        }

        history.Record(applied);
        InvalidateContentIndexes();
        CancelDoorAccessEventsPreviewUx1AG();
        return new(true, "Accès et événements de porte appliqués · enregistrez pour écrire le module.");
    }

    internal void CancelDoorAccessEventsPreviewUx1AG()
    {
        _approvedDoorAccessEventsDraftUx1AG = null;
        _approvedDoorAccessEventsRevisionUx1AG = null;
    }

    private void CloseUx1AG() => CancelDoorAccessEventsPreviewUx1AG();

    internal StudioUx1AGSmokeResult RunUx1AGContractSmoke()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux1ag-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var authored = OriginalModuleAuthoringService.CreateVeilleeDesEclats().State.Project;
            var targetBlueprint = authored.Blueprints.Single(blueprint => blueprint.Kind == BlueprintKind.Door);
            var neighborBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1ed0000-0000-4000-8000-000000000002")),
                "porte-voisine-ag",
                "Porte voisine AG",
                BlueprintKind.Door);
            var freshBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1ed0000-0000-4000-8000-000000000003")),
                "porte-neuve-ag",
                "Porte neuve AG",
                BlueprintKind.Door);
            var targetProfile = CreateCompleteDoorProfileUx1X(
                targetBlueprint.Id,
                Guid.Parse("c1ed0000-0000-4000-8000-000000000010"),
                authored.Dialogues.Single().Id.Value);
            var originalAccess = targetProfile.AccessEvents!;
            var germanFailure = new DoorAccessEventProfile.LocalizedFailure("de-DE", "Verschlossen.");
            targetProfile = targetProfile with
            {
                AccessEvents = originalAccess with
                {
                    Lock = originalAccess.Lock with { CodeHash = "sha256:preserve-hidden" },
                    Rejection = originalAccess.Rejection with
                    {
                        Texts = originalAccess.Rejection.Texts.Append(germanFailure).ToArray(),
                    },
                },
            };
            var neighborProfile = CreateNeighborDoorProfileUx1X(
                neighborBlueprint.Id,
                Guid.Parse("c1ed0000-0000-4000-8000-000000000011"));
            var catalog = new WorldFeatureCatalog(
                authored.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-ag-catalog",
                authored.Module.WorldFeatures.Placeables,
                [targetProfile, neighborProfile],
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
            if (!created.Succeeded || !editing.Open(tempRoot).Succeeded || editing.Project is null ||
                editing.History is null)
            {
                return FailedUx1AGSmoke();
            }

            var loadedProject = editing.Project;
            var history = editing.History;
            var loadedOriginal = loadedProject.Module.WorldFeatures.Doors.Single(profile =>
                profile.BlueprintId == targetBlueprint.Id);
            var loadedNeighbor = loadedProject.Module.WorldFeatures.Doors.Single(profile =>
                profile.BlueprintId == neighborBlueprint.Id);
            var loadedAccess = loadedOriginal.AccessEvents!;
            var loadedPortalScaffold = loadedAccess.PortalUse;
            var loadedUnknownLocale = loadedAccess.Rejection.Texts.Single(item => item.Locale == "de-DE");
            var loadedOrder = loadedProject.Module.WorldFeatures.Doors.Select(profile => profile.Id).ToArray();
            var choices = editing.DoorAccessEventsChoicesUx1AG;
            var source = editing.CreateDoorAccessEventsDraftUx1AG(targetBlueprint.Id);
            var fresh = editing.CreateDoorAccessEventsDraftUx1AG(freshBlueprint.Id);
            if (source is null || fresh is null)
            {
                return FailedUx1AGSmoke();
            }

            var explicitSelection = choices.Count == 3 &&
                                    choices.Single(choice => choice.BlueprintId == targetBlueprint.Id).Configured &&
                                    !choices.Single(choice => choice.BlueprintId == freshBlueprint.Id).Configured &&
                                    source.BlueprintId == targetBlueprint.Id;
            var draft = source with
            {
                AccessRequiredAtRuntime = true,
                LockState = DoorAccessLockState.Locked,
                CredentialIds = ["key:ember-ag", "badge:watch-ag"],
                SkillId = "mechanics",
                Difficulty = 17,
                Hackable = true,
                Relockable = true,
                CanJam = true,
                ConsumeCredential = true,
                Permissions = ["door.unlock.ag"],
                RejectionForgeScriptApiVersion = "1.1.0",
                RejectionFrenchText = "La serrure résiste.",
                RejectionEnglishText = "The lock resists.",
                RejectionSoundAssetId = "audio:door-ag-locked",
                RejectionAnimationId = "animation:door-ag-rattle",
                RejectionScriptId = "scripts/door_ag_rejected",
                TrapTriggerId = "door:ag-interact",
                TrapPayloadId = "damage:ag-embers",
                TrapDetectionDifficulty = 13,
                TrapDisarmDifficulty = 18,
                TrapResetDelaySeconds = 45,
                TrapOwnerId = "ash-watch-ag",
                TrapVisualEffectId = "vfx:ag-embers",
                TrapFriendlyPolicy = "ash-watch-ag",
                TrapArmed = true,
                TrapOneShot = true,
                TrapSides = ["front", "back"],
                TrapEvents = ["interact", "open-attempt"],
                StateForgeScriptApiVersion = "1.1.0",
                LockStateEventsEnabled = true,
                LockStateEventsRequiredAtRuntime = true,
                LockStateForgeScriptApiVersion = "1.1.0",
            };

            var beforePreview = editing.Project;
            var beforeRevision = history.State.Revision;
            var preview = editing.PreviewDoorAccessEventsUx1AG(draft);
            var dryRun = preview.Succeeded && preview.CanApply && preview.LockSimulationAccepted &&
                         preview.RejectionSimulationAccepted && preview.TrapSimulationAccepted &&
                         preview.StateEventsSimulationAccepted && preview.LockEventsSimulationAccepted &&
                         ReferenceEquals(beforePreview, editing.Project) &&
                         history.State.Revision == beforeRevision && !editing.IsDirty;

            var commandBuilt = editing.TryBuildDoorAccessEventsUx1AG(
                draft,
                dryRun: true,
                out var dryRunCommand,
                out var builtProfile,
                out _);
            var commandPreview = dryRunCommand is null
                ? null
                : editing._setDoorCatalogUx1AG.Handle(history.State, dryRunCommand);
            var freshDraft = fresh with
            {
                CredentialIds = ["key:fresh-ag"],
                Permissions = ["door.unlock.fresh-ag"],
                RejectionFrenchText = "Porte neuve verrouillée.",
                RejectionEnglishText = "Fresh door locked.",
                TrapSides = ["front"],
                TrapEvents = ["interact"],
            };
            var freshBuilt = editing.TryBuildDoorAccessEventsUx1AG(
                freshDraft,
                dryRun: true,
                out var freshCommand,
                out var freshProfile,
                out _);
            var freshPreview = freshCommand is null
                ? null
                : editing._setDoorCatalogUx1AG.Handle(history.State, freshCommand);
            var expectedFreshOrder = freshProfile is null
                ? []
                : loadedOrder.Append(freshProfile.Id).OrderBy(id => id).ToArray();
            var freshAccess = freshProfile?.AccessEvents;
            var freshPortalScaffold = freshAccess?.PortalUse;
            var freshHiddenPortalScaffold = freshBuilt && freshPreview?.Succeeded == true &&
                                            freshProfile is { Foundation: null } &&
                                            freshPortalScaffold is
                                            {
                                                ForgeScriptApiVersion: ForgeScriptApiUx1AG,
                                                RequestedBinding: "PortalUseRequested",
                                                CompletedBinding: "PortalUseCompleted",
                                            } && freshPortalScaffold.Id != Guid.Empty &&
                                            freshProfile?.AccessEventsRequiredAtRuntime == true;
            var canonicalDryRunCommand = commandBuilt && commandPreview?.Succeeded == true &&
                                         dryRunCommand is { DryRun: true } &&
                                         builtProfile?.AccessEvents is not null;
            var replacementOrderPreserved = dryRunCommand?.Catalog.Doors.Select(profile => profile.Id)
                .SequenceEqual(loadedOrder) == true;
            var freshDoors = freshCommand?.Catalog.Doors;
            var freshAddedExactlyOnce = false;
            if (freshBuilt && freshProfile is not null && freshDoors is not null)
            {
                var freshProfileId = freshProfile.Id;
                freshAddedExactlyOnce = freshDoors.Count == loadedOrder.Length + 1 &&
                                        freshDoors.Count(profile => profile.Id == freshProfileId) == 1 &&
                                        loadedOrder.All(id => freshDoors.Count(profile => profile.Id == id) == 1);
            }
            var freshCanonicalOrder = freshCommand?.Catalog.Doors.Select(profile => profile.Id)
                .SequenceEqual(expectedFreshOrder) == true;
            var canonicalPreviewSourceUntouched = ReferenceEquals(beforePreview, editing.Project) &&
                                                  history.State.Revision == beforeRevision;
            var canonicalCommand = canonicalDryRunCommand && replacementOrderPreserved &&
                                   freshAddedExactlyOnce && freshCanonicalOrder &&
                                   canonicalPreviewSourceUntouched;
            if (!canonicalCommand)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX1AG_CANONICAL_COMMAND_FAILED " +
                    $"dry_run_command={canonicalDryRunCommand.ToString().ToLowerInvariant()} " +
                    $"replacement_order={replacementOrderPreserved.ToString().ToLowerInvariant()} " +
                    $"fresh_added_once={freshAddedExactlyOnce.ToString().ToLowerInvariant()} " +
                    $"fresh_canonical_order={freshCanonicalOrder.ToString().ToLowerInvariant()} " +
                    $"source_untouched={canonicalPreviewSourceUntouched.ToString().ToLowerInvariant()}");
            }

            var builtAccess = builtProfile?.AccessEvents;
            var lockAndCredentials = builtProfile is not null && builtAccess is
            {
                Lock:
                {
                    State: DoorAccessLockState.Locked,
                    SkillId: "mechanics",
                    Difficulty: 17,
                    Hackable: true,
                    Relockable: true,
                    CanJam: true,
                    ConsumeCredential: true,
                },
            } && builtAccess.Lock.CredentialIds.SequenceEqual(draft.CredentialIds) &&
                                     builtAccess.Lock.Permissions.SequenceEqual(draft.Permissions) &&
                                     PreviewDoorLockUx1AG(builtProfile, draft);
            var rejection = builtProfile is not null && builtAccess is not null &&
                            builtAccess.Rejection.ForgeScriptApiVersion == "1.1.0" &&
                            builtAccess.Rejection.EventBinding == "DoorOpenRejected" &&
                            builtAccess.Rejection.Texts.Any(item => item.Locale == "fr-FR" &&
                                                                   item.Text == draft.RejectionFrenchText) &&
                            builtAccess.Rejection.Texts.Any(item => item.Locale == "en-US" &&
                                                                   item.Text == draft.RejectionEnglishText) &&
                            ReferenceEquals(
                                loadedUnknownLocale,
                                builtAccess.Rejection.Texts.Single(item => item.Locale == "de-DE")) &&
                            PreviewDoorRejectionUx1AG(builtProfile, draft);
            var trap = builtProfile is not null && builtAccess is not null &&
                       builtAccess.Trap.TriggerId == draft.TrapTriggerId &&
                       builtAccess.Trap.PayloadId == draft.TrapPayloadId &&
                       builtAccess.Trap.Sides.SequenceEqual(draft.TrapSides) &&
                       builtAccess.Trap.Events.SequenceEqual(draft.TrapEvents) &&
                       PreviewDoorTrapUx1AG(builtProfile, draft);
            var terminalStateEvents = builtProfile is not null && builtAccess is not null &&
                                      builtAccess.StateEvents.ForgeScriptApiVersion == "1.1.0" &&
                                      builtAccess.StateEvents.OpenedBinding == "DoorOpened" &&
                                      builtAccess.StateEvents.ClosedBinding == "DoorClosed" &&
                                      PreviewDoorStateEventsUx1AG(builtProfile);
            var terminalLockEvents = builtProfile is not null && builtAccess is
            {
                LockStateEvents:
                {
                    ForgeScriptApiVersion: "1.1.0",
                    LockedBinding: "DoorLocked",
                    UnlockedBinding: "DoorUnlocked",
                },
                LockStateEventsRequiredAtRuntime: true,
            } &&
                                     PreviewDoorLockEventsUx1AG(builtProfile, draft);
            var portalScaffoldPreserved = builtAccess is not null &&
                                          ReferenceEquals(loadedPortalScaffold, builtAccess.PortalUse);

            var approvalPreview = editing.PreviewDoorAccessEventsUx1AG(draft);
            var approvalInterveningBuilt = editing.TryBuildDoorAccessEventsUx1AG(
                draft with { Difficulty = 18 },
                dryRun: false,
                out var approvalInterveningCommand,
                out _,
                out _);
            var approvalIntervening = approvalInterveningCommand is null
                ? null
                : editing._setDoorCatalogUx1AG.Handle(history.State, approvalInterveningCommand);
            if (approvalIntervening?.Succeeded == true)
            {
                history.Record(approvalIntervening);
            }
            var staleApprovalApply = editing.ApplyApprovedDoorAccessEventsUx1AG();
            var approvalUndo = editing.Undo();
            var approvalRevisionGuard = approvalPreview.Succeeded && approvalInterveningBuilt &&
                                        approvalIntervening?.Succeeded == true &&
                                        !staleApprovalApply.Succeeded && staleApprovalApply.StaleRevision &&
                                        approvalUndo.Succeeded && ReferenceEquals(loadedProject, editing.Project) &&
                                        !editing.IsDirty;

            var staleCommandBuilt = editing.TryBuildDoorAccessEventsUx1AG(
                draft,
                dryRun: false,
                out var staleCommand,
                out _,
                out _);
            var commandInterveningBuilt = editing.TryBuildDoorAccessEventsUx1AG(
                draft with { Difficulty = 19 },
                dryRun: false,
                out var commandInterveningCommand,
                out _,
                out _);
            var commandIntervening = commandInterveningCommand is null
                ? null
                : editing._setDoorCatalogUx1AG.Handle(history.State, commandInterveningCommand);
            if (commandIntervening?.Succeeded == true)
            {
                history.Record(commandIntervening);
            }
            var staleCommandResult = staleCommand is null
                ? null
                : editing._setDoorCatalogUx1AG.Handle(history.State, staleCommand);
            var commandUndo = editing.Undo();
            var commandRevisionGuard = staleCommandBuilt && commandInterveningBuilt &&
                                       commandIntervening?.Succeeded == true &&
                                       staleCommandResult is { Succeeded: false } &&
                                       staleCommandResult.Diagnostics.Any(item =>
                                           item.Code == "command.revision_conflict") && commandUndo.Succeeded &&
                                       ReferenceEquals(loadedProject, editing.Project) && !editing.IsDirty;

            var invalidPreviews = new[]
            {
                editing.PreviewDoorAccessEventsUx1AG(draft with { CredentialIds = [] }),
                editing.PreviewDoorAccessEventsUx1AG(draft with
                    { CredentialIds = ["key:duplicate", "key:duplicate"] }),
                editing.PreviewDoorAccessEventsUx1AG(draft with { Difficulty = -1 }),
                editing.PreviewDoorAccessEventsUx1AG(draft with { RejectionFrenchText = string.Empty }),
                editing.PreviewDoorAccessEventsUx1AG(draft with
                    { RejectionForgeScriptApiVersion = string.Empty }),
                editing.PreviewDoorAccessEventsUx1AG(draft with { TrapSides = [] }),
                editing.PreviewDoorAccessEventsUx1AG(draft with { TrapResetDelaySeconds = double.NaN }),
                editing.PreviewDoorAccessEventsUx1AG(draft with
                {
                    LockStateEventsEnabled = false,
                    LockStateEventsRequiredAtRuntime = true,
                }),
                editing.PreviewDoorAccessEventsUx1AG(draft with
                    { StateForgeScriptApiVersion = string.Empty }),
            };
            var invalidBlocked = invalidPreviews.All(result => !result.Succeeded && !result.CanApply) &&
                                 ReferenceEquals(loadedProject, editing.Project) && !editing.IsDirty;
            var humanDiagnostics = invalidPreviews.All(result =>
                IsHumanDoorAccessEventsMessageUx1AG(result.Message));

            var approved = editing.PreviewDoorAccessEventsUx1AG(draft);
            var apply = editing.ApplyApprovedDoorAccessEventsUx1AG();
            var appliedProject = editing.Project;
            var appliedProfile = appliedProject?.Module.WorldFeatures.Doors.Single(profile =>
                profile.BlueprintId == targetBlueprint.Id);
            var appliedAccess = appliedProfile?.AccessEvents;
            var preservedNeighbor = appliedProject?.Module.WorldFeatures.Doors.Single(profile =>
                profile.BlueprintId == neighborBlueprint.Id);
            var preservation = approved.Succeeded && apply.Succeeded && appliedProfile is not null &&
                               appliedAccess is not null && appliedProfile.Id == loadedOriginal.Id &&
                               appliedProfile.SchemaVersion == loadedOriginal.SchemaVersion &&
                               appliedProfile.Provenance == loadedOriginal.Provenance &&
                               ReferenceEquals(appliedProfile.Damage, loadedOriginal.Damage) &&
                               ReferenceEquals(appliedProfile.Loading, loadedOriginal.Loading) &&
                               ReferenceEquals(appliedProfile.Events, loadedOriginal.Events) &&
                               appliedProfile.DialogueId == loadedOriginal.DialogueId &&
                               appliedProfile.FactionId == loadedOriginal.FactionId &&
                               ReferenceEquals(appliedProfile.Assembly, loadedOriginal.Assembly) &&
                               ReferenceEquals(appliedProfile.Foundation, loadedOriginal.Foundation) &&
                               appliedAccess.Id == loadedAccess.Id &&
                               appliedAccess.AssetId == loadedAccess.AssetId &&
                               appliedAccess.Transform == loadedAccess.Transform &&
                               ReferenceEquals(appliedAccess.Overrides, loadedAccess.Overrides) &&
                               ReferenceEquals(appliedAccess.PortalUse, loadedPortalScaffold) &&
                               appliedAccess.Lock.CodeHash == loadedAccess.Lock.CodeHash &&
                               ReferenceEquals(
                                   loadedUnknownLocale,
                                   appliedAccess.Rejection.Texts.Single(item => item.Locale == "de-DE")) &&
                               ReferenceEquals(preservedNeighbor, loadedNeighbor) &&
                               appliedProject!.Module.WorldFeatures.Doors.Select(profile => profile.Id)
                                   .SequenceEqual(loadedOrder);

            var undo = editing.Undo();
            var undone = undo.Succeeded && ReferenceEquals(loadedProject, editing.Project) && !editing.IsDirty;
            var redo = editing.Redo();
            var redoneProfile = editing.Project?.Module.WorldFeatures.Doors.Single(profile =>
                profile.BlueprintId == targetBlueprint.Id);
            var redoneAccess = redoneProfile?.AccessEvents;
            var redone = redo.Succeeded && redoneAccess?.Lock.Difficulty == 17 &&
                         redoneAccess?.Trap.PayloadId == "damage:ag-embers";
            var undoRedo = apply.Succeeded && undone && redone;

            var saved = editing.Save();
            editing.Close();
            var reopen = reopened.Open(tempRoot);
            var persisted = reopened.Project?.Module.WorldFeatures.Doors.Single(profile =>
                profile.BlueprintId == targetBlueprint.Id);
            var persistedAccess = persisted?.AccessEvents;
            var persistence = saved.Succeeded && reopen.Succeeded && persisted is not null &&
                              persistedAccess is
                              {
                                  Lock:
                                  {
                                      State: DoorAccessLockState.Locked,
                                      Difficulty: 17,
                                      ConsumeCredential: true,
                                      CodeHash: "sha256:preserve-hidden",
                                  },
                                  Rejection:
                                  {
                                      ForgeScriptApiVersion: "1.1.0",
                                      EventBinding: "DoorOpenRejected",
                                      SoundAssetId: "audio:door-ag-locked",
                                      AnimationId: "animation:door-ag-rattle",
                                      ScriptId: "scripts/door_ag_rejected",
                                  },
                                  Trap:
                                  {
                                      TriggerId: "door:ag-interact",
                                      PayloadId: "damage:ag-embers",
                                      ResetDelaySeconds: 45,
                                  },
                                  StateEvents:
                                  {
                                      ForgeScriptApiVersion: "1.1.0",
                                      OpenedBinding: "DoorOpened",
                                      ClosedBinding: "DoorClosed",
                                  },
                                  LockStateEvents:
                                  {
                                      ForgeScriptApiVersion: "1.1.0",
                                      LockedBinding: "DoorLocked",
                                      UnlockedBinding: "DoorUnlocked",
                                  },
                              } && persistedAccess.Lock.CredentialIds.SequenceEqual(draft.CredentialIds) &&
                              persistedAccess.Lock.Permissions.SequenceEqual(draft.Permissions) &&
                              persistedAccess.Rejection.Texts.Any(item => item.Locale == "de-DE" &&
                                                                          item.Text == "Verschlossen.") &&
                              persistedAccess.PortalUse.RequestedBinding == "PortalUseRequested" &&
                              persistedAccess.PortalUse.CompletedBinding == "PortalUseCompleted" &&
                              persistedAccess.LockStateEventsRequiredAtRuntime &&
                              persisted.AccessEventsRequiredAtRuntime &&
                              persisted.Damage == loadedOriginal.Damage &&
                              persisted.Loading == loadedOriginal.Loading &&
                              persisted.Events.SequenceEqual(loadedOriginal.Events) &&
                              persisted.DialogueId == loadedOriginal.DialogueId &&
                              persisted.FactionId == loadedOriginal.FactionId &&
                              DoorAssemblyEquivalentUx1X(persisted.Assembly, loadedOriginal.Assembly) &&
                              persisted.Foundation?.InitialStateId == loadedOriginal.Foundation?.InitialStateId;

            var succeeded = explicitSelection && freshHiddenPortalScaffold && portalScaffoldPreserved &&
                            lockAndCredentials && rejection && trap && terminalStateEvents && terminalLockEvents &&
                            dryRun && approvalRevisionGuard && commandRevisionGuard && canonicalCommand &&
                            preservation && undoRedo && persistence && invalidBlocked && humanDiagnostics;
            return new StudioUx1AGSmokeResult(
                succeeded,
                explicitSelection,
                freshHiddenPortalScaffold,
                portalScaffoldPreserved,
                lockAndCredentials,
                rejection,
                trap,
                terminalStateEvents,
                terminalLockEvents,
                dryRun,
                approvalRevisionGuard,
                commandRevisionGuard,
                canonicalCommand,
                preservation,
                undoRedo,
                persistence,
                invalidBlocked,
                humanDiagnostics);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1AG_CONTRACT_FAILED " + exception);
            return FailedUx1AGSmoke();
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

    private bool TryBuildDoorAccessEventsUx1AG(
        StudioDoorAccessEventsDraft draft,
        bool dryRun,
        out SetWorldFeatureCatalogCommand? command,
        out DoorWorldProfile? profile,
        out string message)
    {
        command = null;
        profile = null;
        message = string.Empty;
        var history = History;
        var project = Project;
        if (history is null || project is null)
        {
            message = "Ouvrez un module avant de configurer une porte.";
            return false;
        }
        var blueprint = project.Blueprints.SingleOrDefault(candidate =>
            candidate.Id == draft.BlueprintId && candidate.Kind == BlueprintKind.Door);
        if (blueprint is null)
        {
            message = "La porte sélectionnée n’est plus disponible dans le module.";
            return false;
        }
        if (!ValidateDoorAccessEventsDraftUx1AG(draft, out message))
        {
            return false;
        }

        var catalog = project.Module.WorldFeatures;
        var matches = catalog.Doors.Where(candidate => candidate.BlueprintId == draft.BlueprintId).ToArray();
        if (matches.Length > 1)
        {
            message = "Plusieurs fiches ciblent cette porte · corrigez le module avant l’édition.";
            return false;
        }

        try
        {
            var existing = matches.SingleOrDefault();
            var access = BuildDoorAccessEventsUx1AG(existing?.AccessEvents, draft, blueprint.Slug);
            var builtProfile = existing is null
                ? new DoorWorldProfile(
                    StableDoorIdUx1AG(draft.BlueprintId, "profile"),
                    WorldFeatureCatalog.CurrentSchemaVersion,
                    Ux1AGProvenance,
                    draft.BlueprintId,
                    null,
                    null,
                    [],
                    null,
                    null,
                    null,
                    null,
                    access,
                    draft.AccessRequiredAtRuntime)
                : existing with
                {
                    AccessEvents = access,
                    AccessEventsRequiredAtRuntime = draft.AccessRequiredAtRuntime,
                };
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
            message = "La fiche d’accès et d’événements est incomplète · vérifiez les champs signalés.";
            return false;
        }
    }

    private static DoorAccessEventProfile BuildDoorAccessEventsUx1AG(
        DoorAccessEventProfile? existing,
        StudioDoorAccessEventsDraft draft,
        string blueprintSlug)
    {
        var rootId = existing?.Id ?? StableDoorIdUx1AG(draft.BlueprintId, "access-events");
        var lockPolicy = new DoorAccessEventProfile.DoorLockPolicy(
            existing?.Lock.Id ?? StableDoorIdUx1AG(draft.BlueprintId, "lock"),
            draft.LockState,
            NormalizeListUx1AG(draft.CredentialIds),
            draft.SkillId.Trim(),
            draft.Difficulty,
            existing?.Lock.CodeHash,
            draft.Hackable,
            draft.Relockable,
            draft.CanJam,
            draft.ConsumeCredential,
            NormalizeListUx1AG(draft.Permissions));
        var rejection = new DoorAccessEventProfile.DoorOpenRejectedPolicy(
            existing?.Rejection.Id ?? StableDoorIdUx1AG(draft.BlueprintId, "rejection"),
            draft.RejectionForgeScriptApiVersion.Trim(),
            "DoorOpenRejected",
            MergeDoorFailureTextsUx1AG(existing?.Rejection.Texts ?? [], draft),
            draft.RejectionSoundAssetId.Trim(),
            draft.RejectionAnimationId.Trim(),
            draft.RejectionScriptId.Trim());
        var trap = new DoorAccessEventProfile.DoorTrapPolicy(
            existing?.Trap.Id ?? StableDoorIdUx1AG(draft.BlueprintId, "trap"),
            draft.TrapTriggerId.Trim(),
            draft.TrapPayloadId.Trim(),
            draft.TrapDetectionDifficulty,
            draft.TrapDisarmDifficulty,
            draft.TrapResetDelaySeconds,
            draft.TrapOwnerId.Trim(),
            draft.TrapVisualEffectId.Trim(),
            draft.TrapFriendlyPolicy.Trim(),
            draft.TrapArmed,
            draft.TrapOneShot,
            NormalizeListUx1AG(draft.TrapSides),
            NormalizeListUx1AG(draft.TrapEvents));
        var portalUse = existing?.PortalUse ?? new DoorAccessEventProfile.DoorPortalUsePolicy(
            StableDoorIdUx1AG(draft.BlueprintId, "portal-use-scaffold"),
            ForgeScriptApiUx1AG,
            "PortalUseRequested",
            "PortalUseCompleted");
        var stateEvents = new DoorAccessEventProfile.DoorStateEventPolicy(
            existing?.StateEvents.Id ?? StableDoorIdUx1AG(draft.BlueprintId, "state-events"),
            draft.StateForgeScriptApiVersion.Trim(),
            "DoorOpened",
            "DoorClosed");
        var lockEvents = draft.LockStateEventsEnabled
            ? new DoorAccessEventProfile.DoorLockEventPolicy(
                existing?.LockStateEvents?.Id ?? StableDoorIdUx1AG(draft.BlueprintId, "lock-events"),
                DoorAccessEventProfile.CurrentSchemaVersion,
                Ux1AGProvenance,
                draft.LockStateForgeScriptApiVersion.Trim(),
                "DoorLocked",
                "DoorUnlocked")
            : null;
        return new DoorAccessEventProfile(
            rootId,
            DoorAccessEventProfile.CurrentSchemaVersion,
            Ux1AGProvenance,
            existing?.AssetId ?? $"assets/doors/{blueprintSlug}.glb",
            existing?.Transform ?? TransformValue.Identity,
            lockPolicy,
            rejection,
            trap,
            portalUse,
            stateEvents,
            existing?.Overrides ?? new Dictionary<string, string>(StringComparer.Ordinal),
            lockEvents,
            draft.LockStateEventsEnabled && draft.LockStateEventsRequiredAtRuntime);
    }

    private static IReadOnlyList<DoorAccessEventProfile.LocalizedFailure> MergeDoorFailureTextsUx1AG(
        IReadOnlyList<DoorAccessEventProfile.LocalizedFailure> existing,
        StudioDoorAccessEventsDraft draft)
    {
        var merged = new List<DoorAccessEventProfile.LocalizedFailure>();
        var frenchInserted = false;
        var englishInserted = false;
        foreach (var item in existing)
        {
            if (string.Equals(item.Locale, "fr-FR", StringComparison.OrdinalIgnoreCase))
            {
                if (!frenchInserted)
                {
                    merged.Add(new("fr-FR", draft.RejectionFrenchText.Trim()));
                    frenchInserted = true;
                }
                continue;
            }
            if (string.Equals(item.Locale, "en-US", StringComparison.OrdinalIgnoreCase))
            {
                if (!englishInserted)
                {
                    merged.Add(new("en-US", draft.RejectionEnglishText.Trim()));
                    englishInserted = true;
                }
                continue;
            }
            merged.Add(item);
        }
        if (!frenchInserted)
        {
            merged.Add(new("fr-FR", draft.RejectionFrenchText.Trim()));
        }
        if (!englishInserted)
        {
            merged.Add(new("en-US", draft.RejectionEnglishText.Trim()));
        }
        return merged.ToArray();
    }

    private static bool ValidateDoorAccessEventsDraftUx1AG(
        StudioDoorAccessEventsDraft draft,
        out string message)
    {
        message = string.Empty;
        var credentials = NormalizeListUx1AG(draft.CredentialIds);
        if (credentials.Count == 0)
        {
            message = "Ajoutez au moins une clé ou un credential pour le verrou.";
            return false;
        }
        if (credentials.Count != draft.CredentialIds.Count ||
            credentials.Distinct(StringComparer.Ordinal).Count() != credentials.Count)
        {
            message = "Les clés et credentials doivent être nommés, uniques et sans doublon.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(draft.SkillId))
        {
            message = "Choisissez la compétence utilisée pour forcer ou crocheter le verrou.";
            return false;
        }
        if (!double.IsFinite(draft.Difficulty) || draft.Difficulty < 0)
        {
            message = "La difficulté du verrou doit être un nombre positif ou nul.";
            return false;
        }
        if (draft.Permissions.Any(string.IsNullOrWhiteSpace) ||
            draft.Permissions.Distinct(StringComparer.Ordinal).Count() != draft.Permissions.Count)
        {
            message = "Les permissions du verrou doivent être nommées et sans doublon.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(draft.RejectionFrenchText) ||
            string.IsNullOrWhiteSpace(draft.RejectionEnglishText))
        {
            message = "Renseignez les messages français et anglais affichés lorsque l’ouverture échoue.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(draft.RejectionForgeScriptApiVersion))
        {
            message = "Choisissez la version de script utilisée lorsque l’ouverture échoue.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(draft.RejectionSoundAssetId) ||
            string.IsNullOrWhiteSpace(draft.RejectionAnimationId) ||
            string.IsNullOrWhiteSpace(draft.RejectionScriptId))
        {
            message = "Le refus d’ouverture exige un son, une animation et un script nommés.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(draft.TrapTriggerId) ||
            string.IsNullOrWhiteSpace(draft.TrapPayloadId) ||
            string.IsNullOrWhiteSpace(draft.TrapOwnerId) ||
            string.IsNullOrWhiteSpace(draft.TrapVisualEffectId) ||
            string.IsNullOrWhiteSpace(draft.TrapFriendlyPolicy))
        {
            message = "Le piège exige un déclencheur, un effet, un propriétaire, un visuel et une politique alliée.";
            return false;
        }
        if (new[]
            {
                draft.TrapDetectionDifficulty,
                draft.TrapDisarmDifficulty,
                draft.TrapResetDelaySeconds,
            }.Any(value => !double.IsFinite(value) || value < 0))
        {
            message = "Les difficultés et le délai de réarmement du piège doivent être positifs ou nuls.";
            return false;
        }
        if (!ListIsValidUx1AG(draft.TrapSides) || !ListIsValidUx1AG(draft.TrapEvents))
        {
            message = "Ajoutez au moins un côté et un événement de déclenchement, sans doublon.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(draft.StateForgeScriptApiVersion))
        {
            message = "Choisissez la version de script utilisée pour les événements d’ouverture et de fermeture.";
            return false;
        }
        if (!draft.LockStateEventsEnabled && draft.LockStateEventsRequiredAtRuntime)
        {
            message = "Les événements de verrou requis au runtime doivent rester activés.";
            return false;
        }
        if (draft.LockStateEventsEnabled && string.IsNullOrWhiteSpace(draft.LockStateForgeScriptApiVersion))
        {
            message = "Choisissez la version de script utilisée pour le verrouillage et le déverrouillage.";
            return false;
        }
        return true;
    }

    private static bool PreviewDoorLockUx1AG(
        DoorWorldProfile profile,
        StudioDoorAccessEventsDraft draft)
    {
        var access = profile.AccessEvents!;
        var created = DoorAccessEventFoundationService.CreateLock(profile);
        if (!string.Equals(created.SkillId, access.Lock.SkillId, StringComparison.Ordinal) ||
            !created.Difficulty.Equals(access.Lock.Difficulty) ||
            !created.Credentials.SetEquals(access.Lock.CredentialIds))
        {
            return false;
        }
        if (draft.LockState != DoorAccessLockState.Locked)
        {
            return created.State.ToString().Equals(draft.LockState.ToString(), StringComparison.Ordinal);
        }

        var credentials = access.Lock.CredentialIds.ToHashSet(StringComparer.Ordinal);
        var permissions = access.Lock.Permissions.ToHashSet(StringComparer.Ordinal);
        var unlocked = DoorAccessEventFoundationService.Unlock(
            profile,
            new("studio-preview", credentials, permissions, null, 0, false, false),
            credentials);
        if (!unlocked.LockResult.Succeeded || unlocked.LockResult.Method != "credential" ||
            unlocked.CredentialConsumed != access.Lock.ConsumeCredential)
        {
            return false;
        }
        var skill = DoorAccessEventFoundationService.Unlock(
            profile,
            new("studio-preview", new HashSet<string>(), new HashSet<string>(), null,
                access.Lock.Difficulty, false, false),
            new HashSet<string>());
        var denied = DoorAccessEventFoundationService.Unlock(
            profile,
            new("studio-preview", new HashSet<string>(), new HashSet<string>(), null, -1, false, false),
            new HashSet<string>());
        var skillAccepted = skill.LockResult.Succeeded && skill.LockResult.Method == "skill";
        var denialAccepted = access.Lock.Permissions.Count == 0 || !denied.LockResult.Succeeded;
        var hackingAccepted = !access.Lock.Hackable || DoorAccessEventFoundationService.Unlock(
            profile,
            new("studio-preview", new HashSet<string>(), new HashSet<string>(), null,
                access.Lock.Difficulty, true, false),
            new HashSet<string>()).LockResult.Succeeded;
        var relocked = DoorAccessEventFoundationService.Relock(profile, false, "studio-preview");
        return skillAccepted && denialAccepted && hackingAccepted &&
               (access.Lock.Relockable ? relocked.Succeeded : !relocked.Succeeded);
    }

    private static bool PreviewDoorRejectionUx1AG(
        DoorWorldProfile profile,
        StudioDoorAccessEventsDraft draft)
    {
        var rejected = DoorAccessEventFoundationService.Reject(profile, "studio-preview", "locked");
        return rejected.ActorId == "studio-preview" && rejected.ReasonCode == "locked" &&
               rejected.Texts.Any(item => item.Locale == "fr-FR" &&
                                          item.Text == draft.RejectionFrenchText.Trim()) &&
               rejected.Texts.Any(item => item.Locale == "en-US" &&
                                          item.Text == draft.RejectionEnglishText.Trim()) &&
               rejected.SoundAssetId == draft.RejectionSoundAssetId.Trim() &&
               rejected.AnimationId == draft.RejectionAnimationId.Trim() &&
               rejected.ScriptId == draft.RejectionScriptId.Trim();
    }

    private static bool PreviewDoorTrapUx1AG(
        DoorWorldProfile profile,
        StudioDoorAccessEventsDraft draft)
    {
        var trap = DoorAccessEventFoundationService.CreateTrap(profile);
        var side = draft.TrapSides[0].Trim();
        var eventName = draft.TrapEvents[0].Trim();
        var outsider = string.Equals(draft.TrapFriendlyPolicy.Trim(), "studio-outsider", StringComparison.Ordinal)
            ? "studio-outsider-2"
            : "studio-outsider";
        var ignoredSide = DoorAccessEventFoundationService.TriggerTrap(
            profile,
            "studio-side-outside-policy",
            eventName,
            outsider);
        var ignoredFriendly = DoorAccessEventFoundationService.TriggerTrap(
            profile,
            side,
            eventName,
            draft.TrapFriendlyPolicy.Trim());
        var triggered = DoorAccessEventFoundationService.TriggerTrap(profile, side, eventName, outsider);
        var detected = PlaceableSecurityService.Detect(
            trap.Trap,
            draft.TrapDetectionDifficulty,
            outsider);
        var disarmed = PlaceableSecurityService.Disarm(trap.Trap, draft.TrapDisarmDifficulty);
        var reset = PlaceableSecurityService.Reset(disarmed.Trap);
        var lifecycleAccepted = !draft.TrapArmed ||
                                detected.Succeeded && disarmed.Succeeded && !disarmed.Trap.Armed && reset.Armed;
        return trap.Sides.SetEquals(NormalizeListUx1AG(draft.TrapSides)) &&
               trap.Events.SetEquals(NormalizeListUx1AG(draft.TrapEvents)) &&
               trap.Trap.OwnerId == draft.TrapOwnerId.Trim() &&
               trap.Trap.VisualEffectId == draft.TrapVisualEffectId.Trim() &&
               trap.Trap.FriendlyPolicy == draft.TrapFriendlyPolicy.Trim() &&
               trap.Trap.ResetDelay == TimeSpan.FromSeconds(draft.TrapResetDelaySeconds) &&
               !ignoredSide.Succeeded && !ignoredFriendly.Succeeded &&
               lifecycleAccepted && (draft.TrapArmed ? triggered.Succeeded : !triggered.Succeeded);
    }

    private static bool PreviewDoorStateEventsUx1AG(DoorWorldProfile profile)
    {
        var access = profile.AccessEvents!;
        var opened = DoorAccessEventFoundationService.CompleteStateTransition(
            profile,
            "studio-preview",
            "opening",
            "open",
            "animation-complete",
            DateTimeOffset.UnixEpoch);
        var closed = DoorAccessEventFoundationService.CompleteStateTransition(
            profile,
            "studio-preview",
            "closing",
            "closed",
            "animation-complete",
            DateTimeOffset.UnixEpoch);
        var nonTerminalBlocked = false;
        try
        {
            _ = DoorAccessEventFoundationService.CompleteStateTransition(
                profile,
                "studio-preview",
                "closed",
                "opening",
                "interact",
                DateTimeOffset.UnixEpoch);
        }
        catch (InvalidOperationException)
        {
            nonTerminalBlocked = true;
        }
        return opened.Name == access.StateEvents.OpenedBinding &&
               opened.ActorId == "studio-preview" && opened.Trigger == "animation-complete" &&
               opened.PreviousState == "opening" && opened.CurrentState == "open" &&
               closed.Name == access.StateEvents.ClosedBinding &&
               closed.ActorId == "studio-preview" && closed.Trigger == "animation-complete" &&
               closed.PreviousState == "closing" && closed.CurrentState == "closed" &&
               nonTerminalBlocked;
    }

    private static bool PreviewDoorLockEventsUx1AG(
        DoorWorldProfile profile,
        StudioDoorAccessEventsDraft draft)
    {
        if (!draft.LockStateEventsEnabled)
        {
            return profile.AccessEvents is
            {
                LockStateEvents: null,
                LockStateEventsRequiredAtRuntime: false,
            };
        }

        var access = profile.AccessEvents!;
        var lockEvents = access.LockStateEvents;
        if (lockEvents is null)
        {
            return false;
        }
        var unlocked = DoorAccessEventFoundationService.CompleteLockTransition(
            profile,
            "studio-preview",
            "credential",
            access.Lock.CredentialIds[0],
            DoorAccessLockState.Locked,
            DoorAccessLockState.Unlocked,
            DateTimeOffset.UnixEpoch);
        var locked = DoorAccessEventFoundationService.CompleteLockTransition(
            profile,
            "studio-preview",
            "relock",
            null,
            DoorAccessLockState.Unlocked,
            DoorAccessLockState.Locked,
            DateTimeOffset.UnixEpoch);
        var nonTerminalBlocked = false;
        try
        {
            _ = DoorAccessEventFoundationService.CompleteLockTransition(
                profile,
                "studio-preview",
                "jam",
                null,
                DoorAccessLockState.Locked,
                DoorAccessLockState.Jammed,
                DateTimeOffset.UnixEpoch);
        }
        catch (InvalidOperationException)
        {
            nonTerminalBlocked = true;
        }
        return unlocked.Name == lockEvents.UnlockedBinding &&
               unlocked.ActorId == "studio-preview" && unlocked.Method == "credential" &&
               unlocked.Credential == access.Lock.CredentialIds[0] &&
               locked.Name == lockEvents.LockedBinding && locked.ActorId == "studio-preview" &&
               locked.Method == "relock" && locked.Credential is null && nonTerminalBlocked;
    }

    private static IReadOnlyList<string> NormalizeListUx1AG(IEnumerable<string> values) =>
        values.Select(value => value.Trim()).Where(value => value.Length > 0).ToArray();

    private static bool ListIsValidUx1AG(IReadOnlyList<string> values)
    {
        var normalized = NormalizeListUx1AG(values);
        return normalized.Count > 0 && normalized.Count == values.Count &&
               normalized.Distinct(StringComparer.Ordinal).Count() == normalized.Count;
    }

    private static Guid StableDoorIdUx1AG(BlueprintId blueprintId, string purpose)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{blueprintId.Value:D}:ux1-ag:{purpose}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }

    private static StudioDoorAccessEventsPreview FailedDoorAccessEventsPreviewUx1AG(string message) =>
        new(false, message, string.Empty, false);

    private static StudioUx1AGSmokeResult FailedUx1AGSmoke() => new(
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

    private static string HumanizeDoorLockStateUx1AG(DoorAccessLockState state) => state switch
    {
        DoorAccessLockState.Unlocked => "déverrouillé",
        DoorAccessLockState.Locked => "verrouillé",
        DoorAccessLockState.Jammed => "bloqué",
        DoorAccessLockState.Broken => "brisé",
        _ => "inconnu",
    };

    private static string HumanizeDoorAccessEventsDiagnosticUx1AG(string? code) => code switch
    {
        "command.revision_conflict" => "Le module a changé · relancez l’aperçu.",
        "world.door.access_events_identity_invalid" =>
            "La fiche d’accès exige une identité, une provenance et une ressource de porte valides.",
        "world.door.access_events_uuid_invalid" or "world.door.access_events_uuid_duplicate" =>
            "Un composant d’accès de porte utilise déjà un identifiant réservé dans le module.",
        "world.door.lock_invalid" =>
            "Le verrou exige au moins une clé, une compétence et une difficulté valides.",
        "world.door.open_rejected_invalid" =>
            "Le refus d’ouverture exige des textes localisés, un son, une animation et un script.",
        "world.door.trap_invalid" =>
            "Le piège exige un déclencheur, un effet, des difficultés, un propriétaire, des côtés et des événements.",
        "world.door.state_events_invalid" =>
            "Les événements d’ouverture et de fermeture doivent utiliser leurs liaisons terminales canoniques.",
        "world.door.lock_events_invalid" or "world.door.lock_events_required" =>
            "Les événements de verrouillage requis doivent rester complets et activés.",
        "world.reference.blueprint_missing" => "La porte sélectionnée n’est plus disponible.",
        _ => "Les réglages d’accès ou d’événements ne respectent pas le module.",
    };

    private static bool IsHumanDoorAccessEventsMessageUx1AG(string message) =>
        !string.IsNullOrWhiteSpace(message) &&
        !message.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains(".json", StringComparison.OrdinalIgnoreCase);
}
