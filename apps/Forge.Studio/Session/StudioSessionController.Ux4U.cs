using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Companions;
using Forge.RpgKit.Domain.Companions;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetWorldFeatureCatalogHandler _setCompanionCatalogUx4U = new(new SystemClock());
    private StudioCompanionDraftUx4U? _approvedCompanionDraftUx4U;
    private ProjectRevision? _approvedCompanionRevisionUx4U;
    private string? _approvedCompanionProjectFingerprintUx4U;
    private string? _approvedCompanionCandidateFingerprintUx4U;

    internal IReadOnlyList<StudioCompanionChoiceUx4U> CompanionChoicesUx4U =>
        Project?.Module.WorldFeatures.Companions
            .OrderBy(item => CompanionBlueprintLabelUx4U(item.BlueprintId), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Id)
            .Select(item => new StudioCompanionChoiceUx4U(item.Id,
                $"{CompanionBlueprintLabelUx4U(item.BlueprintId)} · " +
                (CompanionCompleteUx4U(item) ? "profil complet" : "profil à compléter"),
                CompanionCompleteUx4U(item)))
            .ToArray() ?? [];

    internal IReadOnlyList<StudioCompanionBlueprintChoiceUx4U> CompanionBlueprintChoicesUx4U =>
        Project?.Blueprints.Where(item => item.Kind == BlueprintKind.Creature)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioCompanionBlueprintChoiceUx4U(item.Id, $"{item.DisplayName} · {item.Slug}"))
            .ToArray() ?? [];

    internal IReadOnlyList<StudioCompanionDialogueChoiceUx4U> CompanionDialogueChoicesUx4U =>
        Project?.Dialogues.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioCompanionDialogueChoiceUx4U(item.Id.Value,
                $"{item.DisplayName} · {item.Slug}"))
            .ToArray() ?? [];

    internal IReadOnlyList<StudioCompanionQuestChoiceUx4U> CompanionQuestChoicesUx4U =>
        Project?.Quests.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioCompanionQuestChoiceUx4U(item.Id.Value,
                $"{item.DisplayName} · {item.Slug}"))
            .ToArray() ?? [];

    internal IReadOnlyList<StudioCompanionItemChoiceUx4U> CompanionItemChoicesUx4U =>
        Project?.Blueprints.Where(item => item.Kind == BlueprintKind.Item)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioCompanionItemChoiceUx4U(item.Id, $"{item.DisplayName} · {item.Slug}"))
            .ToArray() ?? [];

    internal IReadOnlyList<StudioCompanionAreaChoiceUx4U> CompanionAreaChoicesUx4U =>
        Project?.Areas.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioCompanionAreaChoiceUx4U(item.Id, $"{item.DisplayName} · {item.Slug}"))
            .ToArray() ?? [];

    internal StudioCompanionDraftUx4U? CreateCompanionDraftUx4U(Guid? companionId = null)
    {
        var project = Project; var history = History;
        if (project is null || history is null || CompanionBlueprintChoicesUx4U.Count == 0 ||
            CompanionDialogueChoicesUx4U.Count == 0 || CompanionQuestChoicesUx4U.Count == 0 ||
            CompanionItemChoicesUx4U.Count == 0 || CompanionAreaChoicesUx4U.Count == 0) return null;
        var original = companionId is null
            ? null
            : project.Module.WorldFeatures.Companions.SingleOrDefault(item => item.Id == companionId.Value);
        if (companionId is not null && original is null) return null;
        var id = original?.Id ?? StableCompanionGuidUx4U(project.Id.Value,
            $"ux4-u:companion:{project.Module.WorldFeatures.Companions.Count + 1}");
        var complete = CompleteCompanionProfileUx4U(project, id, original);
        return new(history.State.Revision, FingerprintCompanionProjectUx4U(project), id, original, complete);
    }

    internal StudioCompanionDraftUx4U CompleteCompanionDraftUx4U(StudioCompanionDraftUx4U draft)
    {
        var project = Project ?? throw new InvalidOperationException("Aucun projet ouvert.");
        return draft with { Profile = CompleteCompanionProfileUx4U(project, draft.CompanionId, draft.Profile) };
    }

    internal StudioCompanionPreviewUx4U PreviewCompanionUx4U(StudioCompanionDraftUx4U draft)
    {
        ArgumentNullException.ThrowIfNull(draft); CancelCompanionUx4U();
        var project = Project; var history = History;
        if (project is null || history is null)
            return FailedCompanionUx4U("Aucun module n’est ouvert.", "studio.companion.project_missing");
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintCompanionProjectUx4U(project), draft.SourceProjectFingerprint,
                StringComparison.Ordinal))
            return FailedCompanionUx4U("Le projet a changé ; reconstruisez l’aperçu.",
                "command.revision_conflict");

        var frozen = FreezeCompanionDraftUx4U(draft);
        var command = CompanionCommandUx4U(project.Module.WorldFeatures, frozen.Profile,
            history.State.Revision, true);
        var dryRun = _setCompanionCatalogUx4U.Handle(history.State, command);
        if (!dryRun.Succeeded)
            return new(false, false,
                HumanCompanionMessageUx4U(dryRun.Diagnostics.FirstOrDefault()?.Code,
                    dryRun.Diagnostics.FirstOrDefault()?.Message ?? "Le compagnon est invalide."),
                string.Empty, string.Empty,
                dryRun.Diagnostics.Select(item => new StudioCompanionDiagnosticUx4U(item.Code,
                    DiagnosticPathUx4U(item.Message), HumanCompanionMessageUx4U(item.Code, item.Message))).ToArray(),
                "Recrutement non évalué.", "Contrôle non évalué.", "Narration non évaluée.",
                "Expérience non évaluée.", CompanionRuntimeBoundaryUx4U);

        var probe = _setCompanionCatalogUx4U.Handle(history.State, command with { DryRun = false });
        if (!probe.Succeeded)
            return FailedCompanionUx4U("Le candidat compagnon n’a pas pu être construit.",
                "studio.companion.candidate_invalid");
        var runtime = ResolveCompanionRuntimeUx4U(frozen.Profile, project);
        var canApply = runtime.Diagnostics.Count == 0;
        if (canApply)
        {
            _approvedCompanionDraftUx4U = frozen;
            _approvedCompanionRevisionUx4U = history.State.Revision;
            _approvedCompanionProjectFingerprintUx4U = FingerprintCompanionProjectUx4U(project);
            _approvedCompanionCandidateFingerprintUx4U = FingerprintCompanionProjectUx4U(probe.State.Project);
        }
        return new(true, canApply,
            canApply ? "Compagnon complet prêt à appliquer." :
                "Le dry-run passe mais une résolution métier refuse le compagnon.",
            dryRun.Patch?.Summary ?? "Prévisualiser le catalogue compagnon.",
            dryRun.Inverse?.Summary ?? "Restaurer le catalogue précédent.", runtime.Diagnostics,
            runtime.Recruitment, runtime.Control, runtime.Narrative, runtime.Experience,
            CompanionRuntimeBoundaryUx4U);
    }

    internal StudioCompanionActionResultUx4U ApplyApprovedCompanionUx4U(StudioCompanionDraftUx4U current)
    {
        var project = Project; var history = History; var approved = _approvedCompanionDraftUx4U;
        if (project is null || history is null || approved is null || _approvedCompanionRevisionUx4U is null ||
            _approvedCompanionProjectFingerprintUx4U is null || _approvedCompanionCandidateFingerprintUx4U is null)
            return new(false, "Prévisualisez d’abord le compagnon complet.");
        var frozenCurrent = FreezeCompanionDraftUx4U(current);
        if (history.State.Revision != _approvedCompanionRevisionUx4U ||
            !string.Equals(FingerprintCompanionProjectUx4U(project),
                _approvedCompanionProjectFingerprintUx4U, StringComparison.Ordinal) ||
            !string.Equals(FingerprintCompanionDraftUx4U(frozenCurrent),
                FingerprintCompanionDraftUx4U(approved), StringComparison.Ordinal))
        {
            CancelCompanionUx4U();
            return new(false, "Le brouillon ou le projet a changé ; reconstruisez l’aperçu.");
        }

        var command = CompanionCommandUx4U(project.Module.WorldFeatures, approved.Profile,
            history.State.Revision, true);
        var dryRun = _setCompanionCatalogUx4U.Handle(history.State, command);
        var probe = dryRun.Succeeded
            ? _setCompanionCatalogUx4U.Handle(history.State, command with { DryRun = false })
            : dryRun;
        if (!dryRun.Succeeded || !probe.Succeeded ||
            !string.Equals(FingerprintCompanionProjectUx4U(probe.State.Project),
                _approvedCompanionCandidateFingerprintUx4U, StringComparison.Ordinal))
        {
            CancelCompanionUx4U();
            return new(false, "Le re-dry-run ne correspond plus au candidat approuvé.");
        }
        var result = _setCompanionCatalogUx4U.Handle(history.State, command with { DryRun = false });
        CancelCompanionUx4U();
        if (!result.Succeeded)
            return new(false, HumanCompanionMessageUx4U(result.Diagnostics.FirstOrDefault()?.Code,
                result.Diagnostics.FirstOrDefault()?.Message ?? "Application refusée."));
        history.Record(result);
        return new(true, result.Patch?.Summary ?? "Compagnon complet appliqué.");
    }

    internal StudioCompanionActionResultUx4U UndoCompanionUx4U()
    { CancelCompanionUx4U(); var result = Undo(); return new(result.Succeeded, result.Message); }

    internal StudioCompanionActionResultUx4U RedoCompanionUx4U()
    { CancelCompanionUx4U(); var result = Redo(); return new(result.Succeeded, result.Message); }

    internal void CancelCompanionUx4U()
    {
        _approvedCompanionDraftUx4U = null; _approvedCompanionRevisionUx4U = null;
        _approvedCompanionProjectFingerprintUx4U = null; _approvedCompanionCandidateFingerprintUx4U = null;
    }

    internal void CloseUx4U() => CancelCompanionUx4U();

    internal StudioCompanionApprovalSnapshotUx4U CaptureCompanionApprovalUx4U() => new(
        _approvedCompanionDraftUx4U, _approvedCompanionRevisionUx4U,
        _approvedCompanionProjectFingerprintUx4U, _approvedCompanionCandidateFingerprintUx4U);

    internal void RestoreCompanionApprovalUx4U(StudioCompanionApprovalSnapshotUx4U snapshot)
    {
        _approvedCompanionDraftUx4U = snapshot.Draft; _approvedCompanionRevisionUx4U = snapshot.Revision;
        _approvedCompanionProjectFingerprintUx4U = snapshot.ProjectFingerprint;
        _approvedCompanionCandidateFingerprintUx4U = snapshot.CandidateFingerprint;
    }

    internal StudioCompanionEvidenceProjectUx4U? CreateUx4UEvidenceProject()
    {
        var source = Project;
        if (source is null || source.Areas.Count == 0 || CompanionBlueprintChoicesUx4U.Count == 0 ||
            CompanionDialogueChoicesUx4U.Count == 0 || CompanionQuestChoicesUx4U.Count == 0 ||
            CompanionItemChoicesUx4U.Count == 0) return null;
        var real = source.Module.WorldFeatures.Companions.FirstOrDefault(CompanionCompleteUx4U);
        if (real is not null) return new(source, real.Id, true, false);
        var id = StableCompanionGuidUx4U(source.Id.Value, "ux4-u:companion");
        var profile = CompleteCompanionProfileUx4U(source, id, null);
        var catalog = ReplaceCompanionUx4U(source.Module.WorldFeatures, profile);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module.WithWorldFeatures(catalog),
            source.Blueprints, source.Areas, source.Dialogues, source.Quests);
        return new(project, id, false, true);
    }

    internal StudioUx4USmokeResult RunUx4UContractSmoke()
    {
        var empty = new StudioUx4USmokeResult(false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false, false, false, false);
        var evidence = CreateUx4UEvidenceProject(); if (evidence is null) return empty;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4u-smoke-" +
            Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController(); var reopened = new StudioSessionController();
        StudioUx4USmokeResult Execute()
        {
            try
            {
                if (!ProjectSession.Create(root, evidence.Project).Succeeded ||
                    (Session is { } sourceWorkspace &&
                     !CopyForgeScriptsForEvidence(sourceWorkspace.RootDirectory, root)) ||
                    !editing.Open(root).Succeeded)
                    return empty;
                var draft = editing.CreateCompanionDraftUx4U(evidence.CompanionId); if (draft is null) return empty;
                draft = editing.CompleteCompanionDraftUx4U(draft);
                draft = TouchCompanionDraftUx4U(draft);
                var profile = draft.Profile; var foundation = profile.Foundation!; var control = profile.Control!;
                var experience = profile.Experience!;
                var explicitSelection = editing.CompanionChoicesUx4U.Any(item =>
                    item.CompanionId == evidence.CompanionId);
                var recruitableDialogues = foundation.Recruitable.Enabled &&
                    foundation.PreRecruitment.DialogueId != Guid.Empty &&
                    foundation.OwnerDialogue.DialogueId != Guid.Empty && profile.Dialogue.DialogueId != Guid.Empty;
                var recruitDismissParty = foundation.Membership.Atomic && foundation.Membership.TransferInventory &&
                    foundation.Party.CompanionSlots > 0 && foundation.Party.ServerActorLimit > 0;
                var ownershipMovement = control.Ownership.ControllerIds.Count > 0 &&
                    control.Movement.CommandKinds.Count == 6;
                var tacticalAiFormation = control.Tactical.CommandKinds.Count == 6 &&
                    control.Ai.AllowedAbilityTags.Count > 0 && profile.Formation.Spacing > 0;
                var equipmentProgression = control.Equipment.SuggestedLoadout.Count > 0 &&
                    profile.Progression.MaximumLevel >= profile.Progression.CurrentLevel;
                var relationshipBanterQuest = profile.Relationship.Loyalty is >= -1 and <= 1 &&
                    profile.Banter.RequiredParticipants.Count > 0 && profile.PersonalQuest is not null;
                var deathCohesion = experience.Death.Mode.Length > 0 &&
                    experience.Cohesion.TeleportThreshold >= experience.Cohesion.PortalThreshold;
                var trapAbilitiesDirect = profile.Traps.Detect && profile.Abilities.Allowed.Count > 0 &&
                    profile.DirectControl.Enabled;
                var hudPartySplit = experience.Hud.ScreenReaderLabels && experience.Hud.Keyboard &&
                    profile.PartySplit.AllowRegroupRequest;
                var validation = profile.Validation is not null &&
                    WorldFeatureCatalogValidator.Validate(editing.Project!).Count == 0;
                var before = editing.Project!; var history = editing.History!; var revision = history.State.Revision;
                var journal = history.Journal.ToArray(); var preview = editing.PreviewCompanionUx4U(draft);
                var dryRun = preview.Succeeded && preview.CanApply && ReferenceEquals(before, editing.Project) &&
                             ReferenceEquals(history, editing.History) && history.State.Revision == revision &&
                             history.Journal.SequenceEqual(journal);

                var changedCatalog = new WorldFeatureCatalog(before.Module.WorldFeatures.Id,
                    before.Module.WorldFeatures.SchemaVersion, "studio:ux4-u-stale-source",
                    before.Module.WorldFeatures.Placeables, before.Module.WorldFeatures.Doors,
                    before.Module.WorldFeatures.Triggers, before.Module.WorldFeatures.Encounters,
                    before.Module.WorldFeatures.VehicleRoutes, before.Module.WorldFeatures.Merchants,
                    before.Module.WorldFeatures.Companions, before.Module.WorldFeatures.Scripts);
                var changed = editing._setCompanionCatalogUx4U.Handle(history.State,
                    new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                        changedCatalog, false));
                if (changed.Succeeded) history.Record(changed);
                var staleSource = changed.Succeeded && !editing.ApplyApprovedCompanionUx4U(draft).Succeeded;
                if (changed.Succeeded) editing.Undo();

                draft = editing.CompleteCompanionDraftUx4U(
                    editing.CreateCompanionDraftUx4U(evidence.CompanionId)!);
                preview = editing.PreviewCompanionUx4U(draft);
                var staleApproval = preview.Succeeded &&
                    !editing.ApplyApprovedCompanionUx4U(TouchCompanionDraftUx4U(draft)).Succeeded;

                draft = editing.CompleteCompanionDraftUx4U(
                    editing.CreateCompanionDraftUx4U(evidence.CompanionId)!);
                draft = TouchCompanionDraftUx4U(draft);
                var source = editing.Project!; var sourceCompanion = source.Module.WorldFeatures.Companions
                    .Single(item => item.Id == evidence.CompanionId);
                var finalPreview = editing.PreviewCompanionUx4U(draft);
                var appliedResult = finalPreview.CanApply
                    ? editing.ApplyApprovedCompanionUx4U(draft)
                    : new StudioCompanionActionResultUx4U(false, "preview failed");
                var applied = editing.Project; var appliedJson = applied is null ? string.Empty :
                    ProjectJsonSerializer.Serialize(applied);
                var appliedCompanion = applied?.Module.WorldFeatures.Companions
                    .SingleOrDefault(item => item.Id == evidence.CompanionId);
                var canonical = appliedResult.Succeeded && appliedCompanion == draft.Profile &&
                                editing.History!.Journal.LastOrDefault()?.CommandName == "set_world_feature_catalog";
                var restoredProject = applied is null ? null : new ProjectDefinition(applied.Id, applied.Title,
                    applied.Module.WithWorldFeatures(ReplaceCompanionUx4U(applied.Module.WorldFeatures,
                        sourceCompanion)), applied.Blueprints, applied.Areas, applied.Dialogues, applied.Quests);
                var neighbors = restoredProject is not null &&
                                ProjectJsonSerializer.Serialize(restoredProject) ==
                                ProjectJsonSerializer.Serialize(source);
                var undo = editing.UndoCompanionUx4U();
                var undone = undo.Succeeded && ProjectJsonSerializer.Serialize(editing.Project!) ==
                    ProjectJsonSerializer.Serialize(source);
                var redo = editing.RedoCompanionUx4U();
                var redone = redo.Succeeded && ProjectJsonSerializer.Serialize(editing.Project!) == appliedJson;
                var saved = redone && editing.Save().Succeeded; editing.Close();
                var persistence = saved && reopened.Open(root).Succeeded &&
                                  ProjectJsonSerializer.Serialize(reopened.Project!) == appliedJson;
                reopened.Close();
                var succeeded = explicitSelection && recruitableDialogues && recruitDismissParty &&
                                ownershipMovement && tacticalAiFormation && equipmentProgression &&
                                relationshipBanterQuest && deathCohesion && trapAbilitiesDirect && hudPartySplit &&
                                validation && dryRun && staleSource && staleApproval && canonical && undone && redone &&
                                persistence && neighbors;
                return new(succeeded, explicitSelection, recruitableDialogues, recruitDismissParty,
                    ownershipMovement, tacticalAiFormation, equipmentProgression, relationshipBanterQuest,
                    deathCohesion, trapAbilitiesDirect, hudPartySplit, validation, dryRun, staleSource,
                    staleApproval, canonical, undone && redone, persistence, neighbors, false);
            }
            catch { return empty; }
            finally { editing.Close(); reopened.Close(); }
        }
        var result = Execute(); var released = DeleteCompanionRootUx4U(root);
        return result with { Succeeded = result.Succeeded && released, TemporaryWorkspaceReleased = released };
    }

    private (IReadOnlyList<StudioCompanionDiagnosticUx4U> Diagnostics, string Recruitment, string Control,
        string Narrative, string Experience) ResolveCompanionRuntimeUx4U(
        CompanionWorldProfile profile, ProjectDefinition project)
    {
        var diagnostics = new List<StudioCompanionDiagnosticUx4U>();
        try
        {
            var foundation = CompanionFoundationService.Resolve(profile);
            var control = CompanionControlFoundationService.Resolve(profile);
            var experience = CompanionExperienceFoundationService.Resolve(profile);
            diagnostics.AddRange(foundation.Diagnostics.Select(item => CompanionDiagnosticUx4U(item)));
            diagnostics.AddRange(control.Diagnostics.Select(item => CompanionDiagnosticUx4U(item)));
            diagnostics.AddRange(experience.Diagnostics.Select(item => CompanionDiagnosticUx4U(item)));
            diagnostics.AddRange(CompanionExperienceFoundationService.Validate(profile)
                .Select(item => CompanionDiagnosticUx4U(item)));
            var validation = CompanionValidationRuntimeResolver.Resolve(profile, project.Areas.First().Id);
            diagnostics.AddRange(validation.Diagnostics.Select(item => new StudioCompanionDiagnosticUx4U(
                item.Code, item.Path, HumanCompanionMessageUx4U(item.Code, item.Message))));
            if (profile.Foundation!.Recruitable.AllowScript)
            {
                var scripts = FindForgeScripts().Select(item => item.RelativePath).ToHashSet(StringComparer.Ordinal);
                if (!scripts.Contains(profile.Foundation.Recruitable.ScriptId))
                    diagnostics.Add(new("studio.companion.script_missing", "companion/foundation/recruitable/script",
                        "Le script de recrutement sélectionné n’existe pas dans le workspace."));
            }
            if (diagnostics.Count > 0)
                return (diagnostics, "Recrutement indisponible.", "Contrôle indisponible.",
                    "Narration indisponible.", "Expérience indisponible.");

            var component = CompanionFoundationService.CreateComponent(profile, string.Empty,
                TransformValue.Identity);
            var party = CompanionFoundationService.CreatePartyState(profile, "party:studio", [],
                new Dictionary<string, decimal>
                {
                    [profile.Foundation.Recruitable.CurrencyId] = 10_000,
                }, profile.Foundation.Recruitable.Conditions.ToHashSet(StringComparer.Ordinal), 1);
            var world = new CompanionWorldState(component, party,
                new Dictionary<string, string> { [component.InventoryId] = "companion" },
                TransformValue.Identity);
            var recruit = CompanionFoundationService.Recruit(profile, world, "player:owner", true);
            var dismiss = recruit.Result.Committed
                ? CompanionFoundationService.Dismiss(profile, recruit.Result.State, "player:owner",
                    TransformValue.Identity, true)
                : null;
            if (!recruit.Result.Committed || dismiss?.Result.Committed != true)
                diagnostics.AddRange(recruit.Result.Diagnostics.Select(item => CompanionDiagnosticUx4U(item)));

            var ownership = CompanionControlFoundationService.CreateOwnership(profile);
            var movement = CompanionControlFoundationService.IssueMovement(profile, "move", "player:owner",
                null, new Vector3Value(2, 0, 2), 10, []);
            var tactical = CompanionControlFoundationService.IssueTactical(profile, "use-ability",
                "player:owner", "target:ally", "balanced", profile.Control!.Tactical.AbilityIds[0], 20,
                movement.Queue);
            if (!movement.Accepted || !tactical.Accepted)
                diagnostics.Add(new("studio.companion.order_preview_failed", "companion/control",
                    "Les ordres de déplacement ou de combat sont refusés par le profil."));
            var transfer = CompanionControlFoundationService.Transfer(profile, profile.Id, "party",
                new HashSet<string>(["party:studio"], StringComparer.Ordinal), "player:owner",
                "preuve studio", DateTimeOffset.UnixEpoch, true);
            var item = project.Blueprints.First(value => value.Kind == BlueprintKind.Item).Id.Value;
            var equip = CompanionControlFoundationService.Equip(profile, new Dictionary<string, Guid>(), item,
                "main-hand", "player:owner");
            if (!equip.Accepted)
                diagnostics.Add(new("studio.companion.equipment_preview_failed", "companion/control/equipment",
                    "L’équipement proposé est refusé par le profil."));

            var decision = WorldFeatureRuntimeResolver.EvaluateCompanion(profile, "owner", .25, true,
                Math.Min(1, profile.Traps.MaximumRisk), true, true);
            var defeated = CompanionExperienceFoundationService.Defeat(profile, DateTimeOffset.UnixEpoch);
            var recovered = CompanionExperienceFoundationService.Recover(profile,
                new(100, false, false, false, false,
                [
                    ("ancre sûre", new Vector3Value(1, 0, 1),
                        profile.Experience!.Cohesion.SafeAnchorTags.ToHashSet(StringComparer.Ordinal), true),
                ]));
            var hud = CompanionExperienceFoundationService.CreateHud(profile);
            var creature = project.Blueprints.Single(itemBlueprint => itemBlueprint.Id == profile.BlueprintId);
            var quest = project.Quests.Single(itemQuest => itemQuest.Id.Value == profile.PersonalQuest!.QuestId);
            return (diagnostics,
                $"{creature.DisplayName} · recrutement et renvoi atomiques · dialogue avant/après recrutement.",
                $"Ordres déplacement/combat mis en file · transfert {ownership.Kind} vers " +
                $"{transfer.Current.Kind} · équipement proposé.",
                $"Formation {profile.Formation.FormationId} · relation {profile.Relationship.Loyalty:0.##} · " +
                $"quête {quest.DisplayName} · dialogue, piège, capacité et contrôle direct évalués.",
                $"Défaite {defeated.EventName} · cohésion {recovered.Kind} · HUD clavier=" +
                $"{hud.Keyboard.ToString().ToLowerInvariant()} · manette={hud.Gamepad.ToString().ToLowerInvariant()} · " +
                $"regroupement={decision.Regroup.ToString().ToLowerInvariant()}.");
        }
        catch (Exception exception)
        {
            diagnostics.Add(new("studio.companion.runtime_preview_failed", "companion/runtime", exception.Message));
            return (diagnostics, "Recrutement non simulé.", "Contrôle non simulé.",
                "Narration non simulée.", "Expérience non simulée.");
        }
    }

    private CompanionWorldProfile CompleteCompanionProfileUx4U(ProjectDefinition project, Guid companionId,
        CompanionWorldProfile? original)
    {
        var creature = project.Blueprints.First(item => item.Kind == BlueprintKind.Creature);
        var dialogue = project.Dialogues.First(); var quest = project.Quests.First();
        var item = project.Blueprints.First(candidate => candidate.Kind == BlueprintKind.Item);
        var area = project.Areas.First(); const string provenance = "studio:ux4-u";
        var realScript = FindForgeScripts().FirstOrDefault()?.RelativePath;
        var allowScript = !string.IsNullOrWhiteSpace(realScript);
        var script = realScript ?? string.Empty;
        var commands = new[] { "move", "follow", "hold", "rally", "guard", "interact", "attack",
            "defend", "focus-fire", "protect", "disengage", "use-ability" };
        var foundation = original?.Foundation ?? new CompanionWorldProfile.FoundationProfile(
            StableCompanionGuidUx4U(companionId, "foundation"), 1, provenance,
            new(StableCompanionGuidUx4U(companionId, "recruitable"), 1, provenance, true,
                ["quest.open"], 5, "gold", 0, ["relation.increase"], true, allowScript, script),
            new(StableCompanionGuidUx4U(companionId, "pre-recruitment"), 1, provenance,
                dialogue.Id.Value, "not-recruited", ["quest.open"], "offer.join", "offer.refuse",
                ["relation.increase"]),
            new(StableCompanionGuidUx4U(companionId, "owner-dialogue"), 1, provenance,
                dialogue.Id.Value, "recruited-by-speaker", commands, true, true, true,
                "companion.depart"),
            new(StableCompanionGuidUx4U(companionId, "membership"), 1, provenance, true, true, true,
                true, CompanionWorldProfile.CompanionPersistence.Campaign, "companion.inventory",
                "party.safe", true),
            new(StableCompanionGuidUx4U(companionId, "party"), 1, provenance, 3, 1, 1, 1, 8,
                new Dictionary<string, int> { ["Support"] = 2 }));
        var control = original?.Control ?? new CompanionWorldProfile.ControlProfile(
            StableCompanionGuidUx4U(companionId, "control"), 1, provenance,
            new(StableCompanionGuidUx4U(companionId, "ownership"), 1, provenance, "individual",
                ["player:owner"], true, ["party", "shared", "game-master"], 1),
            new(StableCompanionGuidUx4U(companionId, "movement"), 1, provenance,
                ["move", "follow", "hold", "rally", "guard", "interact"], "line",
                "party-waypoints", 0, 100),
            new(StableCompanionGuidUx4U(companionId, "tactical"), 1, provenance,
                ["attack", "defend", "focus-fire", "protect", "disengage", "use-ability"],
                ["passive", "defensive", "balanced", "aggressive"], ["heal"], 0, 100),
            new(StableCompanionGuidUx4U(companionId, "ai"), 1, provenance, .6, .4, .5, false,
                "Support", 3, ["heal"], StableCompanionGuidUx4U(companionId, "ai-plan"),
                StableCompanionGuidUx4U(companionId, "ai-transaction"),
                "replace:/companion/control/ai|role:Support", "owner-or-gm"),
            new(StableCompanionGuidUx4U(companionId, "equipment"), 1, provenance, "full", [],
                new Dictionary<string, Guid> { ["main-hand"] = item.Id.Value }, true, "player:owner"));
        var experience = original?.Experience ?? new CompanionWorldProfile.ExperienceProfile(
            StableCompanionGuidUx4U(companionId, "experience"), 1, provenance,
            new(StableCompanionGuidUx4U(companionId, "death"), 1, provenance, "recoverable-corpse",
                30, "resource.ember", "creature.heir", "memorial.ally", true),
            new(StableCompanionGuidUx4U(companionId, "cohesion"), 1, provenance,
                10, 30, 60, true, true, ["party.safe"]),
            new(StableCompanionGuidUx4U(companionId, "hud"), 1, provenance,
                true, true, true, true, true, true, true, true, true, true, ["health", "focus"]));
        var validation = original?.Validation ?? new CompanionWorldProfile.ValidationPolicy(
            StableCompanionGuidUx4U(companionId, "validation"), 1, provenance,
            new(true, allowScript, script, "gold", 5, 0),
            new("party", "server", true), commands,
            new("companion.inventory", "companion.standard", ["main-hand", "armor"]),
            new("companion.recoverable", "recoverable-corpse", 30),
            [new("recruit", dialogue.Id.Value, "dialogue.recruit"),
             new("owner", dialogue.Id.Value, "dialogue.owner")],
            new(false, [area.Id], true, true, "party.safe", 15));
        return new(companionId, 1, original?.Provenance ?? provenance,
            original?.BlueprintId ?? creature.Id,
            original?.Dialogue ?? new(dialogue.Id.Value, "full", "limited", "greeting", "full", "deny"),
            original?.Formation ?? new("line", 0, 1, true, true, true),
            original?.Progression ?? new(CompanionWorldProfile.ProgressionMode.Automatic, 1, 20, true, true,
                [5, 10, 15]),
            original?.Relationship ?? new(.25, .5, 0, .75, ["trusted", "support"], ["recruited"]),
            original?.Banter ?? new([creature.Id], 1, 30, [area.Slug], true, dialogue.Id.Value),
            original is null ? new(quest.Id.Value, "quest.available", "departed", .25,
                "party.morale") : original.PersonalQuest,
            original?.Traps ?? new(true, true, 3, "sight", "traps", false),
            original?.Abilities ?? new(["heal"], ["ultimate"], .5, "ally", true),
            original?.DirectControl ?? new(true, true, true, true, "owner"),
            original?.PartySplit ?? new(true, true, "owner", true, 60),
            validation, true, foundation, true, control, true, experience, true);
    }

    private static SetWorldFeatureCatalogCommand CompanionCommandUx4U(WorldFeatureCatalog source,
        CompanionWorldProfile profile, ProjectRevision revision, bool dryRun) =>
        new(CommandId.From(Guid.NewGuid()), revision, ReplaceCompanionUx4U(source, profile), dryRun);

    private static WorldFeatureCatalog ReplaceCompanionUx4U(WorldFeatureCatalog source,
        CompanionWorldProfile profile)
    {
        var companions = source.Companions.Any(item => item.Id == profile.Id)
            ? source.Companions.Select(item => item.Id == profile.Id ? profile : item).ToArray()
            : source.Companions.Append(profile).ToArray();
        return new(source.Id, source.SchemaVersion, source.Provenance, source.Placeables, source.Doors,
            source.Triggers, source.Encounters, source.VehicleRoutes, source.Merchants, companions,
            source.Scripts);
    }

    private static StudioCompanionDraftUx4U TouchCompanionDraftUx4U(StudioCompanionDraftUx4U draft) =>
        draft with
        {
            Profile = draft.Profile with
            { Formation = draft.Profile.Formation with { Spacing = draft.Profile.Formation.Spacing + .125 } }
        };

    private static bool CompanionCompleteUx4U(CompanionWorldProfile item) =>
        item.Foundation is not null && item.Control is not null && item.Experience is not null &&
        item.Validation is not null && item.FoundationRequiredAtRuntime && item.ControlRequiredAtRuntime &&
        item.ExperienceRequiredAtRuntime && item.ValidationRequiredAtRuntime;

    private string CompanionBlueprintLabelUx4U(BlueprintId id) =>
        Project?.Blueprints.SingleOrDefault(item => item.Id == id) is { } blueprint
            ? $"{blueprint.DisplayName} · {blueprint.Slug}"
            : "Créature héritée protégée";

    private static StudioCompanionDraftUx4U FreezeCompanionDraftUx4U(StudioCompanionDraftUx4U draft) => draft;

    private static StudioCompanionPreviewUx4U FailedCompanionUx4U(string message, string code) =>
        new(false, false, HumanCompanionMessageUx4U(code, message), string.Empty, string.Empty,
            [new(code, "companions/preview", HumanCompanionMessageUx4U(code, message))],
            "Recrutement non évalué.", "Contrôle non évalué.", "Narration non évaluée.",
            "Expérience non évaluée.", CompanionRuntimeBoundaryUx4U);

    private static StudioCompanionDiagnosticUx4U CompanionDiagnosticUx4U(CompanionDiagnostic item) =>
        new(item.Code, item.Path, HumanCompanionMessageUx4U(item.Code, item.Message));

    private static string HumanCompanionMessageUx4U(string? code, string fallback) => code switch
    {
        "command.revision_conflict" => "Le projet a changé ; reconstruisez l’aperçu.",
        "world.reference.dialogue_missing" => "Choisissez des dialogues existants.",
        "world.reference.quest_missing" => "Choisissez une quête existante.",
        "world.companion.foundation_required" => "La fondation de recrutement est requise.",
        "world.companion.control_required" => "Le profil de contrôle est requis.",
        "world.companion.experience_required" => "Le profil de groupe et de cohésion est requis.",
        "world.companion.party_hud_inaccessible" => "Le HUD de groupe doit rester accessible.",
        _ => fallback,
    };

    private static string DiagnosticPathUx4U(string message)
    {
        var separator = message.IndexOf(':');
        return separator > 0 ? message[..separator] : "companions/preview";
    }

    private static string FingerprintCompanionProjectUx4U(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));

    private static string FingerprintCompanionDraftUx4U(StudioCompanionDraftUx4U draft) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(draft))));

    internal static Guid StableCompanionGuidUx4U(Guid root, string discriminator)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{root:D}:{discriminator}"));
        Span<byte> value = stackalloc byte[16]; hash.AsSpan(0, 16).CopyTo(value);
        value[6] = (byte)((value[6] & 0x0f) | 0x40); value[8] = (byte)((value[8] & 0x3f) | 0x80);
        return new Guid(value);
    }

    private static bool DeleteCompanionRootUx4U(string root)
    {
        try { if (Directory.Exists(root)) Directory.Delete(root, true); return !Directory.Exists(root); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return false; }
    }

    private const string CompanionRuntimeBoundaryUx4U =
        "Aperçu pur en mémoire : aucun acteur, inventaire, dialogue, quête, sort, déplacement, caméra ou transport réseau live n’est modifié.";
}
