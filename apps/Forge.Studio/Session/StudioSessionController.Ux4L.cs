using System.Security.Cryptography;
using System.Text;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetCreaturePresentationHandler _setCreatureVoiceUx4L = new(new SystemClock());
    private StudioCreatureVoiceDraftUx4L? _approvedCreatureVoiceDraftUx4L;
    private CreaturePresentationProfile? _approvedCreatureVoiceCandidateUx4L;
    private ProjectRevision? _approvedCreatureVoiceRevisionUx4L;
    private string? _approvedCreatureVoiceProjectFingerprintUx4L;
    private string? _approvedCreatureVoiceDraftFingerprintUx4L;
    private string? _approvedCreatureVoiceCandidateFingerprintUx4L;

    internal IReadOnlyList<StudioCreatureVoiceChoiceUx4L> CreatureVoiceChoicesUx4L => Project?.Blueprints
        .Where(item => (item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn) &&
                       item.CreaturePresentation is not null)
        .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
        .ThenBy(item => item.Slug, StringComparer.Ordinal)
        .Select(item => new StudioCreatureVoiceChoiceUx4L(item.Id,
            $"{item.DisplayName} · {(item.CreaturePresentation!.Voice is null ? "voix absente" : item.CreaturePresentation.Voice.Language)}",
            item.IsReadOnly())).ToArray() ?? [];

    internal StudioCreatureVoiceDraftUx4L? CreateCreatureVoiceDraftUx4L(BlueprintId blueprintId)
    {
        var project = Project;
        var history = History;
        var blueprint = project?.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        if (project is null || history is null || blueprint?.CreaturePresentation is null || blueprint.IsReadOnly() ||
            blueprint.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn)) return null;
        var voice = blueprint.CreaturePresentation.Voice;
        return new(history.State.Revision, FingerprintProjectUx4L(project), blueprint.Id, blueprint,
            blueprint.CreaturePresentation, voice is not null, voice?.ProfileId ?? "voice.default",
            voice?.Language ?? "fr-FR", JoinUx4L(voice?.AudioBankIds ?? ["audio.voice.default"]),
            JoinUx4L(voice?.EmotionIds ?? ["neutral"]), voice?.LipSyncProfileId ?? string.Empty,
            JoinUx4L(voice?.VariantIds ?? ["default"]), voice?.LicenseSpdx ?? "CC0-1.0",
            voice?.RightsProvenance ?? "author-recorded");
    }

    internal StudioCreatureVoicePreviewUx4L PreviewCreatureVoiceUx4L(StudioCreatureVoiceDraftUx4L draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelCreatureVoiceUx4L();
        var project = Project;
        var history = History;
        if (project is null || history is null)
            return FailedCreatureVoiceUx4L("Aucun module n’est ouvert.", "studio.creature.project_missing");
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintProjectUx4L(project), draft.SourceProjectFingerprint, StringComparison.Ordinal))
            return FailedCreatureVoiceUx4L("Le projet a changé ; rechargez la fiche.",
                SetCreaturePresentationHandler.RevisionConflictCode);
        var source = project.Blueprints.SingleOrDefault(item => item.Id == draft.BlueprintId);
        if (source?.CreaturePresentation is null)
            return FailedCreatureVoiceUx4L("La présentation de créature n’existe plus.",
                "creature.presentation.blueprint_missing");
        if (!TryBuildCreatureVoiceCandidateUx4L(draft, source.CreaturePresentation, out var candidate,
                out var diagnostics))
            return new(false, false, "Le profil voix est invalide.", string.Empty, string.Empty, diagnostics,
                "Voix non évaluée.", "Gouvernance non évaluée.", "Runtime non évalué.");
        var dryRun = _setCreatureVoiceUx4L.Handle(history.State, new(CommandId.From(Guid.NewGuid()),
            history.State.Revision, draft.BlueprintId, candidate, DryRun: true));
        if (!dryRun.Succeeded)
            return new(false, false, "La commande canonique refuse la voix.", string.Empty, string.Empty,
                CreatureVoiceDiagnosticsUx4L(dryRun.Diagnostics), VoiceSummaryUx4L(candidate.Voice),
                GovernanceSummaryUx4L(candidate.Voice), "Runtime non évalué.");
        var runtime = CreaturePresentationRuntimeResolver.Resolve(candidate, required: true);
        if (!runtime.Succeeded || runtime.Profile is null)
            return new(false, false, "La projection typée refuse la voix.", string.Empty, string.Empty,
                runtime.Diagnostics.Select(CreatureVoiceDiagnosticUx4L).ToArray(),
                VoiceSummaryUx4L(candidate.Voice), GovernanceSummaryUx4L(candidate.Voice), "Runtime refusé.");
        var frozen = draft with { };
        _approvedCreatureVoiceDraftUx4L = frozen;
        _approvedCreatureVoiceCandidateUx4L = candidate;
        _approvedCreatureVoiceRevisionUx4L = history.State.Revision;
        _approvedCreatureVoiceProjectFingerprintUx4L = FingerprintProjectUx4L(project);
        _approvedCreatureVoiceDraftFingerprintUx4L = FingerprintCreatureVoiceDraftUx4L(frozen);
        _approvedCreatureVoiceCandidateFingerprintUx4L =
            FingerprintCreatureVoiceCandidateUx4L(project, draft.BlueprintId, candidate);
        return new(true, true, candidate.Voice is null ? "Voix optionnelle retirée de l’aperçu." :
                "Profil voix valide · prêt à être appliqué.",
            dryRun.Patch?.Summary ?? "Définir la voix de créature.",
            dryRun.Inverse?.Summary ?? "Restaurer la voix précédente.", [], VoiceSummaryUx4L(candidate.Voice),
            GovernanceSummaryUx4L(candidate.Voice),
            "Présentation typée résolue en mémoire. Aucun son, lipsync, TTS, acteur ou réseau n’est exécuté.");
    }

    internal StudioCreatureVoiceActionResultUx4L ApplyApprovedCreatureVoiceUx4L(
        StudioCreatureVoiceDraftUx4L currentDraft)
    {
        ArgumentNullException.ThrowIfNull(currentDraft);
        var project = Project;
        var history = History;
        var approved = _approvedCreatureVoiceDraftUx4L;
        var candidate = _approvedCreatureVoiceCandidateUx4L;
        if (project is null || history is null || approved is null || candidate is null ||
            _approvedCreatureVoiceRevisionUx4L is null || _approvedCreatureVoiceProjectFingerprintUx4L is null ||
            _approvedCreatureVoiceDraftFingerprintUx4L is null ||
            _approvedCreatureVoiceCandidateFingerprintUx4L is null)
            return new(false, "Prévisualisez le profil voix avant de l’appliquer.");
        var stale = history.State.Revision != _approvedCreatureVoiceRevisionUx4L ||
                    !string.Equals(FingerprintProjectUx4L(project),
                        _approvedCreatureVoiceProjectFingerprintUx4L, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureVoiceDraftUx4L(currentDraft),
                        _approvedCreatureVoiceDraftFingerprintUx4L, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureVoiceDraftUx4L(approved),
                        _approvedCreatureVoiceDraftFingerprintUx4L, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureVoiceCandidateUx4L(project, currentDraft.BlueprintId,
                            candidate), _approvedCreatureVoiceCandidateFingerprintUx4L, StringComparison.Ordinal);
        if (stale)
        {
            CancelCreatureVoiceUx4L();
            return new(false, "Le brouillon, le projet ou l’approbation a changé ; prévisualisez à nouveau.");
        }
        var validation = _setCreatureVoiceUx4L.Handle(history.State, new(CommandId.From(Guid.NewGuid()),
            history.State.Revision, currentDraft.BlueprintId, candidate, DryRun: true));
        if (!validation.Succeeded)
        {
            CancelCreatureVoiceUx4L();
            return new(false, "La revalidation canonique refuse le profil voix.");
        }
        var result = _setCreatureVoiceUx4L.Handle(history.State, new(CommandId.From(Guid.NewGuid()),
            history.State.Revision, currentDraft.BlueprintId, candidate, DryRun: false));
        CancelCreatureVoiceUx4L();
        if (!result.Succeeded) return new(false, "La commande n’a pas appliqué le profil voix.");
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, result.Patch?.Summary ?? "Profil voix appliqué.");
    }

    internal StudioCreatureVoiceActionResultUx4L UndoCreatureVoiceUx4L()
    { CancelCreatureVoiceUx4L(); var result = Undo(); return new(result.Succeeded, result.Message); }
    internal StudioCreatureVoiceActionResultUx4L RedoCreatureVoiceUx4L()
    { CancelCreatureVoiceUx4L(); var result = Redo(); return new(result.Succeeded, result.Message); }

    internal void CancelCreatureVoiceUx4L()
    {
        _approvedCreatureVoiceDraftUx4L = null;
        _approvedCreatureVoiceCandidateUx4L = null;
        _approvedCreatureVoiceRevisionUx4L = null;
        _approvedCreatureVoiceProjectFingerprintUx4L = null;
        _approvedCreatureVoiceDraftFingerprintUx4L = null;
        _approvedCreatureVoiceCandidateFingerprintUx4L = null;
    }

    internal void CloseUx4L() => CancelCreatureVoiceUx4L();
    internal StudioCreatureVoiceApprovalSnapshotUx4L CaptureCreatureVoiceApprovalUx4L() => new(
        _approvedCreatureVoiceDraftUx4L, _approvedCreatureVoiceCandidateUx4L,
        _approvedCreatureVoiceRevisionUx4L, _approvedCreatureVoiceProjectFingerprintUx4L,
        _approvedCreatureVoiceDraftFingerprintUx4L, _approvedCreatureVoiceCandidateFingerprintUx4L);
    internal void RestoreCreatureVoiceApprovalUx4L(StudioCreatureVoiceApprovalSnapshotUx4L snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedCreatureVoiceDraftUx4L = snapshot.Draft;
        _approvedCreatureVoiceCandidateUx4L = snapshot.Candidate;
        _approvedCreatureVoiceRevisionUx4L = snapshot.Revision;
        _approvedCreatureVoiceProjectFingerprintUx4L = snapshot.ProjectFingerprint;
        _approvedCreatureVoiceDraftFingerprintUx4L = snapshot.DraftFingerprint;
        _approvedCreatureVoiceCandidateFingerprintUx4L = snapshot.CandidateFingerprint;
    }

    internal StudioCreatureVoiceEvidenceProjectUx4L? CreateUx4LEvidenceProject()
    {
        var evidence = CreateUx4EEvidenceProject();
        if (evidence is null) return null;
        var blueprint = evidence.Project.Blueprints.Single(item => item.Id == evidence.BlueprintId);
        var presentation = blueprint.CreaturePresentation;
        if (presentation is null) return null;
        var voice = presentation.Voice ?? CanonicalCreatureVoiceUx4L();
        var withVoice = WithCreatureVoiceUx4L(presentation, voice);
        var project = WithCreatureVoicePresentationUx4L(evidence.Project, evidence.BlueprintId, withVoice);
        var real = evidence.UsesRealPresentation && presentation.Voice is not null;
        return new(project, evidence.BlueprintId, real, !real, evidence.PortraitAssetPath, evidence.PortraitBytes);
    }

    internal StudioUx4LSmokeResult RunUx4LContractSmoke()
    {
        var empty = new StudioUx4LSmokeResult(false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false);
        var evidence = CreateUx4LEvidenceProject();
        if (evidence is null) return empty;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4l-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        StudioUx4LSmokeResult Execute()
        {
            try
            {
                if (!ProjectSession.Create(root, evidence.Project).Succeeded ||
                    !WriteEvidencePortraitUx4E(root, evidence.PortraitAssetPath, evidence.PortraitBytes) ||
                    !editing.Open(root).Succeeded) return empty;
                var explicitSelection = editing.CreatureVoiceChoicesUx4L.Count > 0 &&
                    editing.CreatureVoiceChoicesUx4L.All(item =>
                        !item.Label.Contains(item.BlueprintId.ToString(), StringComparison.OrdinalIgnoreCase));
                var draft = editing.CreateCreatureVoiceDraftUx4L(evidence.BlueprintId);
                if (draft is null) return empty with { ExplicitSelection = explicitSelection };
                var changed = ChangedCreatureVoiceDraftUx4L(draft);
                var sourceProject = editing.Project!;
                var sourceHistory = editing.History!;
                var preview = editing.PreviewCreatureVoiceUx4L(changed);
                var dryRun = preview.Succeeded && ReferenceEquals(sourceProject, editing.Project) &&
                             ReferenceEquals(sourceHistory, editing.History) &&
                             sourceHistory.State.Revision == draft.BaseRevision;
                var runtimeProjection = preview.Succeeded &&
                                        preview.RuntimeSummary.Contains("Aucun son", StringComparison.Ordinal);
                var invalid = editing.PreviewCreatureVoiceUx4L(changed with { AudioBankIds = string.Empty });
                var invalidBlocked = !invalid.Succeeded && invalid.Diagnostics.Any(item =>
                    item.Code is "creature.presentation.voice_invalid" or "studio.creature.voice_required");
                var duplicate = editing.PreviewCreatureVoiceUx4L(changed with { EmotionIds = "calm, calm" });
                var duplicateBlocked = !duplicate.Succeeded && duplicate.Diagnostics.Any(item =>
                    item.Code == "creature.presentation.voice_invalid");
                var optional = editing.PreviewCreatureVoiceUx4L(changed with { Enabled = false });
                var optionalRemoval = optional.Succeeded && optional.VoiceSummary.Contains("désactivée", StringComparison.OrdinalIgnoreCase);

                var approvedBeforeRevision = editing.PreviewCreatureVoiceUx4L(changed);
                var neighborChanged = ToggleCreatureVoiceNeighborUx4L(editing, evidence.BlueprintId);
                var staleDraftBlocked = approvedBeforeRevision.Succeeded && neighborChanged &&
                    !editing.ApplyApprovedCreatureVoiceUx4L(changed).Succeeded;
                if (neighborChanged) editing.Undo();

                var fresh = editing.CreateCreatureVoiceDraftUx4L(evidence.BlueprintId)!;
                var freshChanged = ChangedCreatureVoiceDraftUx4L(fresh);
                var approvedBeforeTamper = editing.PreviewCreatureVoiceUx4L(freshChanged);
                var staleApprovalBlocked = approvedBeforeTamper.Succeeded &&
                    !editing.ApplyApprovedCreatureVoiceUx4L(freshChanged with { Language = "en-US" }).Succeeded;

                fresh = editing.CreateCreatureVoiceDraftUx4L(evidence.BlueprintId)!;
                freshChanged = ChangedCreatureVoiceDraftUx4L(fresh);
                var beforeApply = editing.Project!;
                var sourcePresentation = beforeApply.Blueprints.Single(item => item.Id == evidence.BlueprintId)
                    .CreaturePresentation!;
                var finalPreview = editing.PreviewCreatureVoiceUx4L(freshChanged);
                var apply = finalPreview.Succeeded ? editing.ApplyApprovedCreatureVoiceUx4L(freshChanged) :
                    new StudioCreatureVoiceActionResultUx4L(false, "preview failed");
                var appliedProject = editing.Project;
                var applied = appliedProject?.Blueprints.Single(item => item.Id == evidence.BlueprintId)
                    .CreaturePresentation?.Voice;
                var banks = applied is not null && applied.AudioBankIds.SequenceEqual(new[] { "audio.warden.base", "audio.warden.combat" }) &&
                            applied.EmotionIds.SequenceEqual(new[] { "calm", "fear", "hostile" });
                var lipVariants = applied?.LipSyncProfileId == "lipsync.fr-v2" &&
                                  applied.VariantIds.SequenceEqual(new[] { "armored", "default" });
                var governance = applied is { LicenseSpdx: "CC-BY-4.0", RightsProvenance: "actor-consent:ux4-l" };
                var canonical = apply.Succeeded && banks && lipVariants && governance;
                var expected = WithCreatureVoiceUx4L(sourcePresentation, applied);
                var neighbors = canonical && ProjectJsonSerializer.Serialize(WithCreatureVoicePresentationUx4L(
                                    beforeApply, evidence.BlueprintId, expected)) ==
                                ProjectJsonSerializer.Serialize(appliedProject!);
                var appliedJson = appliedProject is null ? string.Empty : ProjectJsonSerializer.Serialize(appliedProject);
                var undo = editing.UndoCreatureVoiceUx4L();
                var undone = undo.Succeeded && ReferenceEquals(beforeApply, editing.Project);
                var redo = editing.RedoCreatureVoiceUx4L();
                var redone = redo.Succeeded && ReferenceEquals(appliedProject, editing.Project);
                var undoRedo = undone && redone;
                var saved = undoRedo && editing.Save().Succeeded;
                editing.Close();
                var persistence = saved && reopened.Open(root).Succeeded &&
                                  ProjectJsonSerializer.Serialize(reopened.Project!) == appliedJson;
                reopened.Close();
                var succeeded = explicitSelection && banks && lipVariants && governance && optionalRemoval &&
                                invalidBlocked && duplicateBlocked && dryRun && runtimeProjection && staleDraftBlocked &&
                                staleApprovalBlocked && canonical && undoRedo && persistence && neighbors;
                return new(succeeded, explicitSelection, banks, lipVariants, governance, optionalRemoval,
                    invalidBlocked, duplicateBlocked, dryRun, runtimeProjection, staleDraftBlocked,
                    staleApprovalBlocked, canonical, undoRedo, persistence, neighbors, false);
            }
            catch { return empty; }
            finally { editing.Close(); reopened.Close(); }
        }
        var result = Execute();
        var released = DeleteUx4LSmokeRoot(root);
        return result with { TemporaryWorkspaceReleased = released, Succeeded = result.Succeeded && released };
    }

    internal static StudioCreatureVoiceDraftUx4L ChangedCreatureVoiceDraftUx4L(StudioCreatureVoiceDraftUx4L draft) =>
        draft with
        {
            Enabled = true,
            ProfileId = "voice.warden",
            Language = "fr-FR",
            AudioBankIds = "audio.warden.base, audio.warden.combat",
            EmotionIds = "calm, hostile, fear",
            LipSyncProfileId = "lipsync.fr-v2",
            VariantIds = "default, armored",
            LicenseSpdx = "CC-BY-4.0",
            RightsProvenance = "actor-consent:ux4-l",
        };

    private static bool TryBuildCreatureVoiceCandidateUx4L(StudioCreatureVoiceDraftUx4L draft,
        CreaturePresentationProfile source, out CreaturePresentationProfile candidate,
        out IReadOnlyList<StudioCreatureVoiceDiagnosticUx4L> diagnostics)
    {
        var errors = new List<StudioCreatureVoiceDiagnosticUx4L>();
        CreaturePresentationProfile.VoiceProfile? voice = null;
        if (draft.Enabled)
        {
            if (!IsCanonicalVoiceInputUx4L(draft.ProfileId) ||
                (!string.IsNullOrWhiteSpace(draft.LipSyncProfileId) && !IsCanonicalVoiceInputUx4L(draft.LipSyncProfileId)))
                errors.Add(new("studio.creature.voice_identifier_invalid", "creaturePresentation/voice",
                    "Profil voix et lipsync doivent utiliser des identifiants canoniques en minuscules."));
            var banks = SplitUx4L(draft.AudioBankIds);
            var emotions = SplitUx4L(draft.EmotionIds);
            var variants = SplitUx4L(draft.VariantIds);
            if (banks.Count == 0 || emotions.Count == 0 || variants.Count == 0 ||
                string.IsNullOrWhiteSpace(draft.Language) || string.IsNullOrWhiteSpace(draft.LicenseSpdx) ||
                string.IsNullOrWhiteSpace(draft.RightsProvenance))
                errors.Add(new("studio.creature.voice_required", "creaturePresentation/voice",
                    "Langue, banque, émotion, variante, licence et provenance des droits sont obligatoires."));
            voice = new(draft.ProfileId, draft.Language, banks, emotions,
                string.IsNullOrWhiteSpace(draft.LipSyncProfileId) ? null : draft.LipSyncProfileId,
                variants, draft.LicenseSpdx, draft.RightsProvenance);
        }
        candidate = WithCreatureVoiceUx4L(source, voice);
        errors.AddRange(CreaturePresentationValidator.Validate(candidate,
                $"blueprints/{draft.BlueprintId}/creaturePresentation")
            .Select(CreatureVoiceDiagnosticUx4L));
        diagnostics = errors.GroupBy(item => (item.Code, item.Path)).Select(item => item.First()).ToArray();
        return diagnostics.Count == 0;
    }

    private static CreaturePresentationProfile WithCreatureVoiceUx4L(CreaturePresentationProfile source,
        CreaturePresentationProfile.VoiceProfile? voice) => new(source.Id, source.SchemaVersion, source.Provenance,
        source.GenderId, source.BodyTypeId, source.Pronouns, source.Portraits, source.UseModelFallback,
        source.RigId, source.BodySlots, source.MaterialColors, source.Attachments, voice);

    private static ProjectDefinition WithCreatureVoicePresentationUx4L(ProjectDefinition project,
        BlueprintId blueprintId, CreaturePresentationProfile presentation) => new(project.Id, project.Title,
        project.Module, project.Blueprints.Select(item => item.Id == blueprintId
            ? new BlueprintDefinition(item.Id, item.Slug, item.DisplayName, item.Kind, item.Combat,
                item.PaletteOrigin, item.IsFavorite, item.Provenance, presentation, item.CreatureProgression,
                item.CreatureLoot, item.CreatureRoleBehavior, item.CreatureEvents, item.ItemAdvanced, item.Authoring,
                item.CreatureFoundation, item.ItemFoundation)
            : item).ToArray(), project.Areas, project.Dialogues, project.Quests);

    private static CreaturePresentationProfile.VoiceProfile CanonicalCreatureVoiceUx4L() =>
        new("voice.warden", "fr-FR", ["audio.warden.base"], ["calm"], "lipsync.fr-v1", ["default"],
            "CC-BY-4.0", "actor-consent:ux4-l-evidence");

    private static bool ToggleCreatureVoiceNeighborUx4L(StudioSessionController session, BlueprintId blueprintId)
    {
        var project = session.Project;
        var history = session.History;
        var source = project?.Blueprints.SingleOrDefault(item => item.Id == blueprintId)?.CreaturePresentation;
        if (project is null || history is null || source is null) return false;
        var changed = new CreaturePresentationProfile(source.Id, source.SchemaVersion, source.Provenance,
            source.GenderId, source.BodyTypeId, source.Pronouns, source.Portraits, !source.UseModelFallback,
            source.RigId, source.BodySlots, source.MaterialColors, source.Attachments, source.Voice);
        if (!changed.UseModelFallback && changed.Portraits.Count == 0) return false;
        var result = new SetCreaturePresentationHandler(new SystemClock()).Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, blueprintId, changed, DryRun: false));
        if (!result.Succeeded) return false;
        history.Record(result);
        session.InvalidateContentIndexes();
        return true;
    }

    private static StudioCreatureVoicePreviewUx4L FailedCreatureVoiceUx4L(string message, string code) =>
        new(false, false, message, string.Empty, string.Empty,
            [new(code, "creaturePresentation/voice", HumanCreatureVoiceMessageUx4L(code, message))],
            "Voix non évaluée.", "Gouvernance non évaluée.", "Runtime non évalué.");
    private static StudioCreatureVoiceDiagnosticUx4L CreatureVoiceDiagnosticUx4L(
        CreaturePresentationDiagnostic item) => new(item.Code, item.Path,
        HumanCreatureVoiceMessageUx4L(item.Code, item.Message));
    private static IReadOnlyList<StudioCreatureVoiceDiagnosticUx4L> CreatureVoiceDiagnosticsUx4L(
        IReadOnlyList<CommandDiagnostic> diagnostics) => diagnostics.Select(item =>
        {
            var separator = item.Message.IndexOf(": ", StringComparison.Ordinal);
            var path = separator > 0 ? item.Message[..separator] : "creaturePresentation/voice";
            var message = separator > 0 ? item.Message[(separator + 2)..] : item.Message;
            return new StudioCreatureVoiceDiagnosticUx4L(item.Code, path,
                HumanCreatureVoiceMessageUx4L(item.Code, message));
        }).ToArray();
    private static string HumanCreatureVoiceMessageUx4L(string code, string fallback) => code switch
    {
        "creature.presentation.voice_invalid" =>
            "La voix exige langue, banques, émotions, variantes uniques, licence et provenance des droits.",
        _ => fallback,
    };
    private static string VoiceSummaryUx4L(CreaturePresentationProfile.VoiceProfile? voice) => voice is null
        ? "Voix désactivée · le profil reste optionnel."
        : $"Voix {voice.ProfileId} · langue {voice.Language} · {voice.AudioBankIds.Count} banque(s) · " +
          $"{voice.EmotionIds.Count} émotion(s) · {voice.VariantIds.Count} variante(s) · " +
          $"lipsync {(voice.LipSyncProfileId ?? "aucun")}.";
    private static string GovernanceSummaryUx4L(CreaturePresentationProfile.VoiceProfile? voice) => voice is null
        ? "Aucun média distribué par ce profil."
        : $"Droits : {voice.LicenseSpdx} · provenance : {voice.RightsProvenance}.";
    private static IReadOnlyList<string> SplitUx4L(string value) => value
        .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Order(StringComparer.Ordinal).ToArray();
    private static string JoinUx4L(IEnumerable<string> values) => string.Join(", ", values);
    private static bool IsCanonicalVoiceInputUx4L(string value) => !string.IsNullOrWhiteSpace(value) &&
        string.Equals(value, value.Trim().ToLowerInvariant(), StringComparison.Ordinal) && !value.Contains(' ');
    private static string FingerprintProjectUx4L(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));
    private static string FingerprintCreatureVoiceCandidateUx4L(ProjectDefinition project, BlueprintId id,
        CreaturePresentationProfile candidate) => FingerprintProjectUx4L(
        WithCreatureVoicePresentationUx4L(project, id, candidate));
    private static string FingerprintCreatureVoiceDraftUx4L(StudioCreatureVoiceDraftUx4L draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(draft.BaseRevision.Value); WriteUx4L(writer, draft.SourceProjectFingerprint);
        WriteUx4L(writer, draft.BlueprintId.ToString()); writer.Write(draft.Enabled);
        WriteUx4L(writer, draft.ProfileId); WriteUx4L(writer, draft.Language); WriteUx4L(writer, draft.AudioBankIds);
        WriteUx4L(writer, draft.EmotionIds); WriteUx4L(writer, draft.LipSyncProfileId);
        WriteUx4L(writer, draft.VariantIds); WriteUx4L(writer, draft.LicenseSpdx);
        WriteUx4L(writer, draft.RightsProvenance); writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
    private static void WriteUx4L(BinaryWriter writer, string value)
    { var bytes = Encoding.UTF8.GetBytes(value); writer.Write(bytes.Length); writer.Write(bytes); }
    private static bool DeleteUx4LSmokeRoot(string root)
    {
        try
        {
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4l-smoke-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, true);
            return !Directory.Exists(full);
        }
        catch { return false; }
    }
}
