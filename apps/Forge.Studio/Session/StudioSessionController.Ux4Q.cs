using System.Security.Cryptography;
using System.Text;
using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetAreaExperienceProfileHandler _setAreaVoiceUx4Q = new(new SystemClock());
    private StudioAreaVoiceDraftUx4Q? _approvedAreaVoiceDraftUx4Q;
    private AreaExperienceProfile? _approvedAreaVoiceCandidateUx4Q;
    private ProjectRevision? _approvedAreaVoiceRevisionUx4Q;
    private string? _approvedAreaVoiceProjectFingerprintUx4Q;
    private string? _approvedAreaVoiceDraftFingerprintUx4Q;
    private string? _approvedAreaVoiceCandidateFingerprintUx4Q;

    internal IReadOnlyList<StudioAreaVoiceAreaChoiceUx4Q> AreaVoiceAreaChoicesUx4Q =>
        Project?.Areas.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioAreaVoiceAreaChoiceUx4Q(item.Id, $"{item.DisplayName} · {item.Slug}"))
            .ToArray() ?? [];

    internal IReadOnlyList<StudioAreaVoiceCreatureChoiceUx4Q> AreaVoiceCreatureChoicesUx4Q =>
        Project?.Blueprints.Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioAreaVoiceCreatureChoiceUx4Q(item.Id, $"{item.DisplayName} · {item.Slug}"))
            .ToArray() ?? [];

    internal StudioAreaVoiceDraftUx4Q? CreateAreaVoiceDraftUx4Q(AreaId areaId, BlueprintId blueprintId)
    {
        var project = Project; var history = History;
        var area = project?.Areas.SingleOrDefault(item => item.Id == areaId);
        var creature = project?.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        if (project is null || history is null || area is null || creature is null ||
            creature.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn)) return null;
        var source = area.RuntimePolicies.Experience;
        var voice = source.Voices.SingleOrDefault(item => item.CreatureBlueprintId == blueprintId);
        var takes = voice?.Takes.Select(item => new StudioLocalizedVoiceTakeUx4Q(item.Locale, item.Resource,
            item.Subtitle, item.PhonemeResource, item.LicenseSpdx, item.ContentSha256)).ToArray() ??
            [new StudioLocalizedVoiceTakeUx4Q("fr-FR", string.Empty, "Nouvelle réplique", string.Empty,
                string.Empty, string.Empty)];
        return new(history.State.Revision, FingerprintAreaVoiceProjectUx4Q(project), areaId, blueprintId, source,
            voice?.Id ?? StableAreaVoiceIdUx4Q(areaId.Value, blueprintId.Value, "voice"),
            voice?.DefaultLocale ?? "fr-FR", takes, voice?.TtsPluginId ?? "tts.local",
            voice?.Consent.SyntheticVoice ?? true, voice?.Consent.TtsUseAllowed ?? true,
            voice?.Consent.ConsentReference ?? "consent:author", voice?.Consent.SpeakerAttribution ?? "voix auteur",
            voice?.Provenance ?? "studio:ux4-q");
    }

    internal StudioAreaVoicePreviewUx4Q PreviewAreaVoiceUx4Q(StudioAreaVoiceDraftUx4Q draft)
    {
        ArgumentNullException.ThrowIfNull(draft); CancelAreaVoiceUx4Q();
        var project = Project; var history = History;
        if (project is null || history is null)
            return FailedAreaVoiceUx4Q("Aucun module n’est ouvert.", "studio.voice.project_missing");
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintAreaVoiceProjectUx4Q(project), draft.SourceProjectFingerprint,
                StringComparison.Ordinal))
            return FailedAreaVoiceUx4Q("Le projet a changé ; reconstruisez l’aperçu.",
                "command.revision_conflict");
        if (!TryBuildAreaVoiceCandidateUx4Q(draft, out var candidate, out var voice, out var diagnostics))
            return new(false, false, "Le profil voix est invalide.", string.Empty, string.Empty, diagnostics,
                AreaVoiceSummaryUx4Q(draft), "Runtime non évalué.");
        var dryRun = _setAreaVoiceUx4Q.Handle(history.State, new SetAreaExperienceProfileCommand(
            CommandId.From(Guid.NewGuid()), history.State.Revision, draft.AreaId, candidate, true));
        if (!dryRun.Succeeded)
            return new(false, false, "La commande canonique refuse cet aperçu.", string.Empty, string.Empty,
                dryRun.Diagnostics.Select(item => new StudioAreaVoiceDiagnosticUx4Q(item.Code,
                    $"areas/{draft.AreaId}/runtimePolicies/experience/voices", HumanAreaVoiceMessageUx4Q(item.Code,
                        item.Message))).ToArray(), AreaVoiceSummaryUx4Q(draft), "Runtime non évalué.");
        ResolvedVoiceTake resolved;
        try { resolved = AreaExperienceRuntimeService.ResolveVoice(voice, draft.DefaultLocale, draft.DefaultLocale); }
        catch (InvalidOperationException exception)
        {
            return FailedAreaVoiceUx4Q(exception.Message, "audio.voice.tts_consent_required");
        }
        var frozen = FreezeAreaVoiceDraftUx4Q(draft);
        _approvedAreaVoiceDraftUx4Q = frozen; _approvedAreaVoiceCandidateUx4Q = candidate;
        _approvedAreaVoiceRevisionUx4Q = history.State.Revision;
        _approvedAreaVoiceProjectFingerprintUx4Q = FingerprintAreaVoiceProjectUx4Q(project);
        _approvedAreaVoiceDraftFingerprintUx4Q = FingerprintAreaVoiceDraftUx4Q(frozen);
        _approvedAreaVoiceCandidateFingerprintUx4Q =
            FingerprintAreaVoiceCandidateUx4Q(project, draft.AreaId, candidate);
        var runtime = $"Résolution locale : {resolved.Locale} · sous-titre présent · " +
                      $"phonèmes={(string.IsNullOrWhiteSpace(resolved.PhonemeResource) ? "absents" : "présents")} · " +
                      $"source={(resolved.UsesTts ? $"TTS {resolved.TtsPluginId} avec consentement" : "prise audio gouvernée")}. " +
                      "Aucun média, TTS, lipsync, acteur ou réseau n’est exécuté.";
        return new(true, true, "Aperçu canonique prêt · aucune donnée du module modifiée.",
            dryRun.Patch?.Summary ?? "Configurer les voix localisées.",
            dryRun.Inverse?.Summary ?? "Restaurer les voix précédentes.", [], AreaVoiceSummaryUx4Q(draft), runtime);
    }

    internal StudioAreaVoiceActionResultUx4Q ApplyApprovedAreaVoiceUx4Q(StudioAreaVoiceDraftUx4Q current)
    {
        var project = Project; var history = History; var approved = _approvedAreaVoiceDraftUx4Q;
        var candidate = _approvedAreaVoiceCandidateUx4Q;
        if (project is null || history is null || approved is null || candidate is null ||
            _approvedAreaVoiceRevisionUx4Q is null || _approvedAreaVoiceProjectFingerprintUx4Q is null ||
            _approvedAreaVoiceDraftFingerprintUx4Q is null || _approvedAreaVoiceCandidateFingerprintUx4Q is null)
            return new(false, "Prévisualisez et approuvez d’abord le profil voix.");
        var stale = history.State.Revision != _approvedAreaVoiceRevisionUx4Q ||
                    !string.Equals(FingerprintAreaVoiceProjectUx4Q(project),
                        _approvedAreaVoiceProjectFingerprintUx4Q, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintAreaVoiceDraftUx4Q(approved),
                        _approvedAreaVoiceDraftFingerprintUx4Q, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintAreaVoiceDraftUx4Q(current),
                        _approvedAreaVoiceDraftFingerprintUx4Q, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintAreaVoiceCandidateUx4Q(project, current.AreaId, candidate),
                        _approvedAreaVoiceCandidateFingerprintUx4Q, StringComparison.Ordinal);
        if (stale) { CancelAreaVoiceUx4Q(); return new(false, "Le brouillon ou le projet a changé."); }
        var preview = _setAreaVoiceUx4Q.Handle(history.State, new SetAreaExperienceProfileCommand(
            CommandId.From(Guid.NewGuid()), history.State.Revision, current.AreaId, candidate, true));
        if (!preview.Succeeded) { CancelAreaVoiceUx4Q(); return new(false, "Le re-dry-run refuse la voix."); }
        var result = _setAreaVoiceUx4Q.Handle(history.State, new SetAreaExperienceProfileCommand(
            CommandId.From(Guid.NewGuid()), history.State.Revision, current.AreaId, candidate, false));
        CancelAreaVoiceUx4Q();
        if (!result.Succeeded) return new(false, result.Diagnostics.FirstOrDefault()?.Message ?? "Application refusée.");
        history.Record(result); return new(true, result.Patch?.Summary ?? "Voix localisée appliquée.");
    }

    internal StudioAreaVoiceActionResultUx4Q UndoAreaVoiceUx4Q()
    { CancelAreaVoiceUx4Q(); var result = Undo(); return new(result.Succeeded, result.Message); }
    internal StudioAreaVoiceActionResultUx4Q RedoAreaVoiceUx4Q()
    { CancelAreaVoiceUx4Q(); var result = Redo(); return new(result.Succeeded, result.Message); }
    internal void CancelAreaVoiceUx4Q()
    {
        _approvedAreaVoiceDraftUx4Q = null; _approvedAreaVoiceCandidateUx4Q = null;
        _approvedAreaVoiceRevisionUx4Q = null; _approvedAreaVoiceProjectFingerprintUx4Q = null;
        _approvedAreaVoiceDraftFingerprintUx4Q = null; _approvedAreaVoiceCandidateFingerprintUx4Q = null;
    }
    internal void CloseUx4Q() => CancelAreaVoiceUx4Q();
    internal StudioAreaVoiceApprovalSnapshotUx4Q CaptureAreaVoiceApprovalUx4Q() => new(
        _approvedAreaVoiceDraftUx4Q, _approvedAreaVoiceCandidateUx4Q, _approvedAreaVoiceRevisionUx4Q,
        _approvedAreaVoiceProjectFingerprintUx4Q, _approvedAreaVoiceDraftFingerprintUx4Q,
        _approvedAreaVoiceCandidateFingerprintUx4Q);
    internal void RestoreAreaVoiceApprovalUx4Q(StudioAreaVoiceApprovalSnapshotUx4Q snapshot)
    {
        _approvedAreaVoiceDraftUx4Q = snapshot.Draft; _approvedAreaVoiceCandidateUx4Q = snapshot.Candidate;
        _approvedAreaVoiceRevisionUx4Q = snapshot.Revision;
        _approvedAreaVoiceProjectFingerprintUx4Q = snapshot.ProjectFingerprint;
        _approvedAreaVoiceDraftFingerprintUx4Q = snapshot.DraftFingerprint;
        _approvedAreaVoiceCandidateFingerprintUx4Q = snapshot.CandidateFingerprint;
    }

    internal StudioAreaVoiceEvidenceProjectUx4Q? CreateUx4QEvidenceProject()
    {
        var source = Project; if (source is null) return null;
        var area = source.Areas.FirstOrDefault();
        var creature = source.Blueprints.FirstOrDefault(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn);
        if (area is null || creature is null) return null;
        var existing = source.Areas.SelectMany(item => item.RuntimePolicies.Experience.Voices
            .Select(voice => (Area: item, Voice: voice))).FirstOrDefault(item =>
            source.Blueprints.Any(blueprint => blueprint.Id == item.Voice?.CreatureBlueprintId));
        if (existing.Voice is not null)
            return new(source, existing.Area.Id, existing.Voice.CreatureBlueprintId, true, false);
        var voice = EvidenceAreaVoiceUx4Q(area.Id, creature.Id);
        var experience = ReplaceVoiceUx4Q(area.RuntimePolicies.Experience, voice);
        return new(ReplaceAreaVoiceProjectUx4Q(source, area.Id, experience), area.Id, creature.Id, false, true);
    }

    internal StudioUx4QSmokeResult RunUx4QContractSmoke()
    {
        var empty = new StudioUx4QSmokeResult(false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false);
        var evidence = CreateUx4QEvidenceProject(); if (evidence is null) return empty;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4q-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController(); var reopened = new StudioSessionController();
        StudioUx4QSmokeResult Execute()
        {
            try
            {
                if (!ProjectSession.Create(root, evidence.Project).Succeeded || !editing.Open(root).Succeeded) return empty;
                var explicitSelection = editing.AreaVoiceAreaChoicesUx4Q.Count > 0 &&
                                        editing.AreaVoiceCreatureChoicesUx4Q.Count > 0;
                var draft = editing.CreateAreaVoiceDraftUx4Q(evidence.AreaId, evidence.BlueprintId);
                if (draft is null) return empty with { ExplicitSelection = explicitSelection };
                var changed = ChangedAreaVoiceDraftUx4Q(draft);
                var beforeProject = editing.Project!; var beforeHistory = editing.History!;
                var preview = editing.PreviewAreaVoiceUx4Q(changed);
                var dryRun = preview.Succeeded && ReferenceEquals(beforeProject, editing.Project) &&
                             ReferenceEquals(beforeHistory, editing.History) && beforeHistory.State.Revision == draft.BaseRevision;
                var runtime = preview.Succeeded && preview.RuntimeSummary.Contains("TTS tts.local avec consentement",
                    StringComparison.Ordinal);
                var invalidConsent = editing.PreviewAreaVoiceUx4Q(changed with
                { TtsUseAllowed = false, ConsentReference = string.Empty });
                var invalidConsentBlocked = !invalidConsent.Succeeded;
                var duplicate = editing.PreviewAreaVoiceUx4Q(changed with
                { Takes = [changed.Takes[0], changed.Takes[0] with { Subtitle = "Doublon" }] });
                var duplicateLocaleBlocked = !duplicate.Succeeded;

                var approved = editing.PreviewAreaVoiceUx4Q(changed);
                var mutation = editing.CreateAreaVoiceDraftUx4Q(evidence.AreaId, evidence.BlueprintId)!;
                var mutationCandidate = ReplaceVoiceUx4Q(mutation.SourceExperience,
                    EvidenceAreaVoiceUx4Q(evidence.AreaId, evidence.BlueprintId) with
                    { Provenance = "studio:ux4-q:neighbor" });
                var mutationResult = editing._setAreaVoiceUx4Q.Handle(editing.History!.State,
                    new SetAreaExperienceProfileCommand(CommandId.From(Guid.NewGuid()), editing.History.State.Revision,
                        evidence.AreaId, mutationCandidate, false));
                if (mutationResult.Succeeded) editing.History.Record(mutationResult);
                var staleSourceBlocked = approved.Succeeded && mutationResult.Succeeded &&
                                         !editing.ApplyApprovedAreaVoiceUx4Q(changed).Succeeded;
                if (mutationResult.Succeeded) editing.Undo();

                draft = editing.CreateAreaVoiceDraftUx4Q(evidence.AreaId, evidence.BlueprintId)!;
                changed = ChangedAreaVoiceDraftUx4Q(draft); approved = editing.PreviewAreaVoiceUx4Q(changed);
                var staleApprovalBlocked = approved.Succeeded &&
                    !editing.ApplyApprovedAreaVoiceUx4Q(changed with { DefaultLocale = "en-US" }).Succeeded;

                draft = editing.CreateAreaVoiceDraftUx4Q(evidence.AreaId, evidence.BlueprintId)!;
                changed = ChangedAreaVoiceDraftUx4Q(draft); var sourceProject = editing.Project!;
                var finalPreview = editing.PreviewAreaVoiceUx4Q(changed);
                var apply = finalPreview.Succeeded ? editing.ApplyApprovedAreaVoiceUx4Q(changed) : new(false, "preview failed");
                var appliedProject = editing.Project; var appliedArea = appliedProject?.Areas.Single(item => item.Id == evidence.AreaId);
                var appliedVoice = appliedArea?.RuntimePolicies.Experience.Voices.SingleOrDefault(item =>
                    item.CreatureBlueprintId == evidence.BlueprintId);
                var localized = apply.Succeeded && appliedVoice?.Takes.Count == 2 &&
                                appliedVoice.Takes.Any(item => item.Locale == "fr-FR") &&
                                appliedVoice.Takes.Any(item => item.Locale == "en-US");
                var subtitlePhoneme = localized && appliedVoice!.Takes.All(item => !string.IsNullOrWhiteSpace(item.Subtitle)) &&
                                      appliedVoice.Takes.Any(item => !string.IsNullOrWhiteSpace(item.PhonemeResource));
                var tts = localized && appliedVoice!.TtsPluginId == "tts.local" &&
                          appliedVoice.Takes.Any(item => string.IsNullOrWhiteSpace(item.Resource));
                var consent = localized && appliedVoice!.Consent is { SyntheticVoice: true, TtsUseAllowed: true } &&
                              appliedVoice.Consent.ConsentReference == "consent:ux4-q:witness";
                var expectedExperience = ReplaceVoiceUx4Q(draft.SourceExperience,
                    BuildAreaVoiceUx4Q(changed));
                var neighbors = appliedProject is not null && ProjectJsonSerializer.Serialize(
                                    ReplaceAreaVoiceProjectUx4Q(sourceProject, evidence.AreaId, expectedExperience)) ==
                                ProjectJsonSerializer.Serialize(appliedProject);
                var afterJson = appliedProject is null ? string.Empty : ProjectJsonSerializer.Serialize(appliedProject);
                var undo = editing.UndoAreaVoiceUx4Q();
                var undone = undo.Succeeded && ProjectJsonSerializer.Serialize(editing.Project!) ==
                             ProjectJsonSerializer.Serialize(sourceProject);
                var redo = editing.RedoAreaVoiceUx4Q();
                var redone = redo.Succeeded && ProjectJsonSerializer.Serialize(editing.Project!) == afterJson;
                var undoRedo = undone && redone;
                var saved = editing.Save().Succeeded; editing.Close();
                var persistence = saved && reopened.Open(root).Succeeded &&
                                  ProjectJsonSerializer.Serialize(reopened.Project!) == afterJson;
                reopened.Close();
                var canonical = apply.Succeeded && finalPreview.Succeeded;
                var succeeded = explicitSelection && localized && subtitlePhoneme && tts && consent &&
                                invalidConsentBlocked && duplicateLocaleBlocked && dryRun && runtime &&
                                staleSourceBlocked && staleApprovalBlocked && canonical && undoRedo && persistence && neighbors;
                return new(succeeded, explicitSelection, localized, subtitlePhoneme, tts, consent,
                    invalidConsentBlocked, duplicateLocaleBlocked, dryRun, runtime, staleSourceBlocked,
                    staleApprovalBlocked, canonical, undoRedo, persistence, neighbors, false);
            }
            catch { return empty; }
            finally { editing.Close(); reopened.Close(); }
        }
        var result = Execute(); var released = DeleteUx4QRoot(root);
        return result with { TemporaryWorkspaceReleased = released, Succeeded = result.Succeeded && released };
    }

    private static bool TryBuildAreaVoiceCandidateUx4Q(StudioAreaVoiceDraftUx4Q draft,
        out AreaExperienceProfile candidate, out CreatureVoiceProfile voice,
        out IReadOnlyList<StudioAreaVoiceDiagnosticUx4Q> diagnostics)
    {
        try
        {
            voice = BuildAreaVoiceUx4Q(draft); candidate = ReplaceVoiceUx4Q(draft.SourceExperience, voice);
            diagnostics = []; return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            candidate = draft.SourceExperience; voice = EvidenceAreaVoiceUx4Q(draft.AreaId, draft.CreatureBlueprintId);
            diagnostics = [new("audio.voice.profile_invalid", $"areas/{draft.AreaId}/runtimePolicies/experience/voices",
                HumanAreaVoiceMessageUx4Q("audio.voice.profile_invalid", exception.Message))]; return false;
        }
    }

    private static CreatureVoiceProfile BuildAreaVoiceUx4Q(StudioAreaVoiceDraftUx4Q draft) => new(
        draft.VoiceId, draft.CreatureBlueprintId, draft.DefaultLocale.Trim(), draft.Takes.Select(item =>
            new LocalizedVoiceTake(item.Locale.Trim(), item.Resource.Trim(), item.Subtitle.Trim(),
                item.PhonemeResource.Trim(), item.LicenseSpdx.Trim(), item.ContentSha256.Trim())).ToArray(),
        draft.TtsPluginId.Trim(), new(draft.SyntheticVoice, draft.TtsUseAllowed, draft.ConsentReference.Trim(),
            draft.SpeakerAttribution.Trim()), draft.Provenance.Trim());

    private static AreaExperienceProfile ReplaceVoiceUx4Q(AreaExperienceProfile source, CreatureVoiceProfile voice) =>
        new(source.Id, source.SchemaVersion, source.AudioCues, source.ScatterEmitters,
            source.Voices.Where(item => item.CreatureBlueprintId != voice.CreatureBlueprintId).Append(voice).ToArray(),
            source.PatrolRoutes, source.Loading, source.Provenance);

    private static ProjectDefinition ReplaceAreaVoiceProjectUx4Q(ProjectDefinition source, AreaId areaId,
        AreaExperienceProfile experience)
    {
        var area = source.Areas.Single(item => item.Id == areaId); var previous = area.RuntimePolicies;
        var policies = new AreaRuntimePolicyProfile(previous.Id, previous.SchemaVersion, previous.Heartbeats,
            previous.Rest, "experience-command", previous.Cinematics, previous.Rules, experience);
        return new(source.Id, source.Title, source.Module, source.Blueprints,
            source.Areas.Select(item => item.Id == areaId ? item.WithRuntimePolicies(policies) : item).ToArray(),
            source.Dialogues, source.Quests);
    }

    private static CreatureVoiceProfile EvidenceAreaVoiceUx4Q(AreaId areaId, BlueprintId blueprintId) => new(
        StableAreaVoiceIdUx4Q(areaId.Value, blueprintId.Value, "voice"), blueprintId, "fr-FR",
        [new("fr-FR", string.Empty, "La route est sûre.", "phonemes://ux4q/fr",
            string.Empty, string.Empty),
         new("en-US", "builtin://voice/ux4q/en", "The road is safe.", "phonemes://ux4q/en",
            "CC0-1.0", new string('a', 64))],
        "tts.local", new(true, true, "consent:ux4-q:witness", "voix témoin"), "studio:ux4-q:evidence");

    private static StudioAreaVoiceDraftUx4Q ChangedAreaVoiceDraftUx4Q(StudioAreaVoiceDraftUx4Q draft)
    {
        var voice = EvidenceAreaVoiceUx4Q(draft.AreaId, draft.CreatureBlueprintId);
        var changedTakes = voice.Takes.Select((item, index) => new StudioLocalizedVoiceTakeUx4Q(
            item.Locale, item.Resource,
            index == 0 ? "La route est sûre · preuve transactionnelle." : item.Subtitle,
            item.PhonemeResource, item.LicenseSpdx, item.ContentSha256)).ToArray();
        return draft with
        {
            VoiceId = voice.Id, DefaultLocale = voice.DefaultLocale,
            Takes = changedTakes,
            TtsPluginId = voice.TtsPluginId, SyntheticVoice = true, TtsUseAllowed = true,
            ConsentReference = voice.Consent.ConsentReference,
            SpeakerAttribution = "voix témoin modifiée", Provenance = "studio:ux4-q:changed",
        };
    }

    private static StudioAreaVoiceDraftUx4Q FreezeAreaVoiceDraftUx4Q(StudioAreaVoiceDraftUx4Q draft) =>
        draft with { Takes = draft.Takes.ToArray() };
    private static string AreaVoiceSummaryUx4Q(StudioAreaVoiceDraftUx4Q draft) =>
        $"Voix : {draft.Takes.Count} prise(s), locale par défaut {draft.DefaultLocale}, " +
        $"TTS={(draft.TtsUseAllowed ? "autorisé avec consentement" : "refusé")}.";
    private static StudioAreaVoicePreviewUx4Q FailedAreaVoiceUx4Q(string message, string code) =>
        new(false, false, message, string.Empty, string.Empty,
            [new(code, "runtimePolicies/experience/voices", HumanAreaVoiceMessageUx4Q(code, message))],
            "Voix non évaluée.", "Runtime non évalué.");
    private static string HumanAreaVoiceMessageUx4Q(string code, string fallback) => code switch
    {
        "audio.voice.tts_consent_required" => "Le TTS exige un plugin, une autorisation et une référence de consentement.",
        "audio.voice.profile_invalid" => "Vérifiez les locales uniques, sous-titres, ressources, licences, hash et consentements.",
        "command.revision_conflict" => "Le projet a changé ; reconstruisez l’aperçu.",
        _ => fallback,
    };
    private static string FingerprintAreaVoiceProjectUx4Q(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));
    private static string FingerprintAreaVoiceCandidateUx4Q(ProjectDefinition project, AreaId areaId,
        AreaExperienceProfile candidate) => FingerprintAreaVoiceProjectUx4Q(
            ReplaceAreaVoiceProjectUx4Q(project, areaId, candidate));
    private static string FingerprintAreaVoiceDraftUx4Q(StudioAreaVoiceDraftUx4Q draft)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(draft.BaseRevision.Value); WriteAreaVoiceUx4Q(writer, draft.SourceProjectFingerprint);
        WriteAreaVoiceUx4Q(writer, draft.AreaId.ToString()); WriteAreaVoiceUx4Q(writer, draft.CreatureBlueprintId.ToString());
        writer.Write(draft.VoiceId.ToByteArray()); WriteAreaVoiceUx4Q(writer, draft.DefaultLocale);
        writer.Write(draft.Takes.Count);
        foreach (var take in draft.Takes)
        { WriteAreaVoiceUx4Q(writer, take.Locale); WriteAreaVoiceUx4Q(writer, take.Resource);
          WriteAreaVoiceUx4Q(writer, take.Subtitle); WriteAreaVoiceUx4Q(writer, take.PhonemeResource);
          WriteAreaVoiceUx4Q(writer, take.LicenseSpdx); WriteAreaVoiceUx4Q(writer, take.ContentSha256); }
        WriteAreaVoiceUx4Q(writer, draft.TtsPluginId); writer.Write(draft.SyntheticVoice);
        writer.Write(draft.TtsUseAllowed); WriteAreaVoiceUx4Q(writer, draft.ConsentReference);
        WriteAreaVoiceUx4Q(writer, draft.SpeakerAttribution); WriteAreaVoiceUx4Q(writer, draft.Provenance);
        writer.Flush(); return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
    private static void WriteAreaVoiceUx4Q(BinaryWriter writer, string? value)
    { var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty); writer.Write(bytes.Length); writer.Write(bytes); }
    private static Guid StableAreaVoiceIdUx4Q(Guid first, Guid second, string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{first:D}:{second:D}:{key}"));
        return new Guid(bytes.AsSpan(0, 16));
    }
    private static bool DeleteUx4QRoot(string root)
    {
        try
        {
            var full = Path.GetFullPath(root); var temp = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4q-smoke-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, true); return !Directory.Exists(full);
        }
        catch { return false; }
    }
}
