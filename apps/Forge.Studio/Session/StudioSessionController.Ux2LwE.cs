using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2LwEProvenance = "studio:ux2-lw-e";
    private const string Ux2LwEEvidenceProvenance = "studio:ux2-lw-e:evidence";
    private readonly SetWorldFeatureCatalogHandler _setLivingWorldTriggerCatalogUx2LwE = new(new SystemClock());
    private StudioUx2LwEDraft? _approvedLivingWorldTriggerUx2LwE;
    private ProjectRevision? _approvedLivingWorldTriggerRevisionUx2LwE;
    private string? _approvedLivingWorldTriggerSourceFingerprintUx2LwE;
    private string? _approvedLivingWorldTriggerDraftFingerprintUx2LwE;

    internal IReadOnlyList<StudioUx2LwETriggerChoice> LivingWorldTriggerChoicesUx2LwE
    {
        get
        {
            var project = Project;
            var area = ActiveArea;
            if (project is null || area is null) return [];
            var blueprints = project.Blueprints.ToDictionary(item => item.Id);
            return project.Module.WorldFeatures.Triggers
                .Where(item => item.AreaId == area.Id)
                .Select(item =>
                {
                    var blueprint = blueprints.GetValueOrDefault(item.BlueprintId);
                    var name = blueprint?.DisplayName ?? "Déclencheur sans blueprint";
                    var slug = item.Foundation?.Slug ?? blueprint?.Slug ?? "volume absent";
                    var configured = new[]
                    {
                        item.Rest is null ? null : "repos",
                        item.Clue is null ? null : "piste",
                        item.Narrative is null ? null : "interjection",
                    }.Where(item => item is not null).ToArray();
                    return new StudioUx2LwETriggerChoice(
                        item.Id,
                        item.BlueprintId,
                        $"{name} · {slug} · " +
                        (item.Foundation is null ? "volume de base requis" :
                            configured.Length == 0 ? "aucune politique vivante" :
                            string.Join(" + ", configured!)),
                        item.Foundation is not null,
                        item.Rest is not null,
                        item.Clue is not null,
                        item.Narrative is not null,
                        string.Equals(item.Provenance, Ux2LwEEvidenceProvenance, StringComparison.Ordinal));
                })
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioUx2LwEReferenceChoice<BlueprintId>>
        LivingWorldParticipantChoicesUx2LwE()
    {
        var project = Project;
        if (project is null) return [];
        return project.Blueprints
            .Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
            .Select(item => new StudioUx2LwEReferenceChoice<BlueprintId>(
                item.Id,
                $"{item.DisplayName} · {item.Slug} · {HumanBlueprintKindUx2LwE(item.Kind)}",
                HumanReferenceProvenanceUx2LwE(item.Provenance.Source, "Blueprint")))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal IReadOnlyList<StudioUx2LwEReferenceChoice<Guid>> LivingWorldDialogueChoicesUx2LwE()
    {
        var project = Project;
        if (project is null) return [];
        return project.Dialogues
            .Select(item => new StudioUx2LwEReferenceChoice<Guid>(
                item.Id.Value,
                $"{item.DisplayName} · {item.Slug}",
                HumanReferenceProvenanceUx2LwE(item.Provenance, "Dialogue")))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal IReadOnlyList<StudioUx2LwEReferenceChoice<Guid>> LivingWorldQuestChoicesUx2LwE()
    {
        var project = Project;
        if (project is null) return [];
        return project.Quests
            .Select(item => new StudioUx2LwEReferenceChoice<Guid>(
                item.Id.Value,
                $"{item.DisplayName} · {item.Slug}",
                HumanReferenceProvenanceUx2LwE(item.Provenance, "Quête")))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal StudioUx2LwEDraft? CreateLivingWorldTriggerDraftUx2LwE(Guid? profileId = null)
    {
        var history = History;
        var project = Project;
        var area = ActiveArea;
        if (history is null || project is null || area is null || profileId is null) return null;
        var profile = project.Module.WorldFeatures.Triggers.SingleOrDefault(item =>
            item.Id == profileId.Value && item.AreaId == area.Id);
        if (profile?.Foundation is null) return null;
        return DraftFromLivingWorldTriggerUx2LwE(profile, history.State.Revision);
    }

    internal StudioUx2LwEPreview PreviewLivingWorldTriggerUx2LwE(StudioUx2LwEDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelLivingWorldTriggerPreviewUx2LwE();
        var history = History;
        var project = Project;
        if (history is null || project is null)
            return FailedLivingWorldTriggerPreviewUx2LwE("Aucun module n’est ouvert.");
        if (draft.BaseRevision != history.State.Revision)
            return FailedLivingWorldTriggerPreviewUx2LwE(
                "Le projet a changé depuis l’ouverture de cette fiche. Rechargez le déclencheur.");
        var current = FindLivingWorldTriggerUx2LwE(project, draft);
        if (current is null || !string.Equals(draft.SourceFingerprint,
                LivingWorldTriggerSourceFingerprintUx2LwE(current), StringComparison.Ordinal))
            return FailedLivingWorldTriggerPreviewUx2LwE(
                "Le déclencheur a changé. Rechargez la fiche avant l’aperçu.");
        if (!TryBuildLivingWorldTriggerCandidateUx2LwE(draft, dryRun: true,
                out var command, out var candidateProject, out var candidate, out var diagnostics))
        {
            return new(false, false,
                diagnostics.FirstOrDefault()?.Message ?? "La fiche contient une valeur invalide.",
                string.Empty,
                BuildLivingWorldTriggerDiffUx2LwE(current, candidate),
                diagnostics,
                "Repos non validé", "Piste non validée", "Interjection non validée", true);
        }

        var result = _setLivingWorldTriggerCatalogUx2LwE.Handle(history.State, command!);
        if (!result.Succeeded)
        {
            var commandDiagnostics = result.Diagnostics.Select(item => new StudioUx2LwEDiagnostic(
                item.Code,
                $"module/worldFeatures/triggers/{draft.ProfileId:D}",
                HumanizeLivingWorldTriggerDiagnosticUx2LwE(item.Code))).ToArray();
            return new(false, false,
                commandDiagnostics.FirstOrDefault()?.Message ?? "La commande canonique a refusé la fiche.",
                result.Patch?.Summary ?? string.Empty,
                BuildLivingWorldTriggerDiffUx2LwE(current, candidate),
                commandDiagnostics,
                "Repos refusé", "Piste refusée", "Interjection refusée", true);
        }

        var runtime = BuildLivingWorldRuntimePreviewUx2LwE(candidate!);
        var canApply = BuildLivingWorldTriggerDiffUx2LwE(current, candidate).Any(item => item.Changed);
        if (canApply)
        {
            _approvedLivingWorldTriggerUx2LwE = draft;
            _approvedLivingWorldTriggerRevisionUx2LwE = history.State.Revision;
            _approvedLivingWorldTriggerSourceFingerprintUx2LwE = draft.SourceFingerprint;
            _approvedLivingWorldTriggerDraftFingerprintUx2LwE =
                LivingWorldTriggerDraftFingerprintUx2LwE(draft);
        }
        return new(true, canApply,
            canApply
                ? "Aperçu sec validé · aucun document, script, moteur ou réseau n’a été exécuté."
                : "La fiche est déjà identique au document canonique.",
            result.Patch?.Summary ?? string.Empty,
            BuildLivingWorldTriggerDiffUx2LwE(current, candidate),
            [], runtime.RestPreview, runtime.Clue, runtime.Narrative, true);
    }

    internal StudioUx2LwEActionResult ApplyApprovedLivingWorldTriggerUx2LwE()
    {
        var history = History;
        var project = Project;
        var draft = _approvedLivingWorldTriggerUx2LwE;
        if (history is null || project is null || draft is null ||
            _approvedLivingWorldTriggerRevisionUx2LwE != history.State.Revision ||
            !string.Equals(_approvedLivingWorldTriggerSourceFingerprintUx2LwE,
                draft.SourceFingerprint, StringComparison.Ordinal) ||
            !string.Equals(_approvedLivingWorldTriggerDraftFingerprintUx2LwE,
                LivingWorldTriggerDraftFingerprintUx2LwE(draft), StringComparison.Ordinal))
        {
            CancelLivingWorldTriggerPreviewUx2LwE();
            return new(false, "Prévisualisez à nouveau cette fiche avant de l’appliquer.");
        }
        var current = FindLivingWorldTriggerUx2LwE(project, draft);
        if (current is null || !string.Equals(draft.SourceFingerprint,
                LivingWorldTriggerSourceFingerprintUx2LwE(current), StringComparison.Ordinal))
        {
            CancelLivingWorldTriggerPreviewUx2LwE();
            return new(false, "Le déclencheur a changé. Rechargez la fiche avant de l’appliquer.");
        }
        if (!TryBuildLivingWorldTriggerCandidateUx2LwE(draft, dryRun: false,
                out var command, out _, out _, out var diagnostics))
        {
            CancelLivingWorldTriggerPreviewUx2LwE();
            return new(false, diagnostics.FirstOrDefault()?.Message ?? "La fiche est invalide.");
        }
        var result = _setLivingWorldTriggerCatalogUx2LwE.Handle(history.State, command!);
        CancelLivingWorldTriggerPreviewUx2LwE();
        if (!result.Succeeded)
            return new(false, HumanizeLivingWorldTriggerDiagnosticUx2LwE(
                result.Diagnostics.FirstOrDefault()?.Code));
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, "Politiques appliquées · Ctrl+Z pour annuler, Ctrl+Y pour rétablir.");
    }

    internal void CancelLivingWorldTriggerPreviewUx2LwE()
    {
        _approvedLivingWorldTriggerUx2LwE = null;
        _approvedLivingWorldTriggerRevisionUx2LwE = null;
        _approvedLivingWorldTriggerSourceFingerprintUx2LwE = null;
        _approvedLivingWorldTriggerDraftFingerprintUx2LwE = null;
    }

    internal StudioUx2LwEApprovalSnapshot CaptureLivingWorldTriggerApprovalUx2LwE() => new(
        _approvedLivingWorldTriggerUx2LwE,
        _approvedLivingWorldTriggerRevisionUx2LwE,
        _approvedLivingWorldTriggerSourceFingerprintUx2LwE,
        _approvedLivingWorldTriggerDraftFingerprintUx2LwE);

    internal void RestoreLivingWorldTriggerApprovalUx2LwE(StudioUx2LwEApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedLivingWorldTriggerUx2LwE = snapshot.Draft;
        _approvedLivingWorldTriggerRevisionUx2LwE = snapshot.Revision;
        _approvedLivingWorldTriggerSourceFingerprintUx2LwE = snapshot.SourceFingerprint;
        _approvedLivingWorldTriggerDraftFingerprintUx2LwE = snapshot.DraftFingerprint;
    }

    internal void CloseUx2LwE() => CancelLivingWorldTriggerPreviewUx2LwE();

    internal ProjectDefinition? CreateUx2LwEEvidenceProject()
    {
        var project = Project;
        var area = ActiveArea;
        if (project is null || area is null) return null;
        if (project.Module.WorldFeatures.Triggers.Any(item => item.AreaId == area.Id && item.Foundation is not null))
            return project;
        var triggerBlueprint = project.Blueprints.FirstOrDefault(item => item.Kind == BlueprintKind.Trigger);
        var participant = project.Blueprints.FirstOrDefault(item => item.Kind == BlueprintKind.Creature);
        var dialogue = project.Dialogues.FirstOrDefault();
        if (triggerBlueprint is null || participant is null || dialogue is null)
            return CreateUx2LwESmokeProject().Project;
        var trigger = CreateUx2LwEEvidenceTrigger(
            triggerBlueprint.Id, participant.Id, area.Id, dialogue.Id.Value,
            project.Quests.FirstOrDefault()?.Id.Value);
        var source = project.Module.WorldFeatures;
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, [.. source.Triggers, trigger], source.Encounters,
            source.VehicleRoutes, source.Merchants, source.Companions, source.Scripts);
        return new ProjectDefinition(project.Id, project.Title, project.Module.WithWorldFeatures(catalog),
            project.Blueprints, project.Areas, project.Dialogues, project.Quests);
    }

    private bool TryBuildLivingWorldTriggerCandidateUx2LwE(
        StudioUx2LwEDraft draft,
        bool dryRun,
        out SetWorldFeatureCatalogCommand? command,
        out ProjectDefinition? candidateProject,
        out TriggerWorldProfile? candidate,
        out IReadOnlyList<StudioUx2LwEDiagnostic> diagnostics)
    {
        command = null;
        candidateProject = null;
        candidate = null;
        diagnostics = [];
        var history = History;
        var project = Project;
        if (history is null || project is null || draft.BaseRevision != history.State.Revision)
        {
            diagnostics = [new("command.revision_conflict", "project/revision",
                "Le projet a changé. Rechargez le déclencheur.")];
            return false;
        }
        var current = FindLivingWorldTriggerUx2LwE(project, draft);
        if (current?.Foundation is null)
        {
            diagnostics = [new("world.trigger.foundation_required",
                $"module/worldFeatures/triggers/{draft.ProfileId:D}/foundation",
                "Configurez d’abord le volume de base avec l’outil Déclencheur.")];
            return false;
        }

        var rest = draft.RestEnabled
            ? new TriggerWorldProfile.RestPolicy(draft.RestSafe, draft.DangerQuery.Trim(),
                draft.WholePartyRequired, draft.AllowCampSetup, draft.AllowedMessageKey.Trim(),
                draft.DeniedMessageKey.Trim())
            : null;
        var clue = draft.ClueEnabled
            ? new TriggerWorldProfile.CluePolicy(draft.SkillId.Trim(), draft.Difficulty, draft.TraceId.Trim(),
                draft.DirectionDegrees, draft.AgeHours,
                string.IsNullOrWhiteSpace(draft.FalseClueId) ? null : draft.FalseClueId.Trim(), draft.QuestId)
            : null;
        if (draft.NarrativeEnabled && draft.DialogueId is null)
        {
            diagnostics = [new("world.reference.dialogue_missing",
                $"module/worldFeatures/triggers/{draft.ProfileId:D}/narrative/dialogueId",
                "Choisissez un dialogue canonique pour l’interjection.")];
            return false;
        }
        var narrative = draft.NarrativeEnabled
            ? new TriggerWorldProfile.NarrativeCuePolicy(
                draft.Participants.Distinct().OrderBy(item => item.Value).ToArray(),
                draft.Priority, draft.Once, draft.CooldownSeconds, draft.MultiplayerPolicy.Trim(),
                draft.DialogueId!.Value)
            : null;
        var updatedProfile = current with
        {
            Provenance = Ux2LwEProvenance,
            Rest = rest,
            Clue = clue,
            Narrative = narrative,
        };
        candidate = updatedProfile;
        var source = project.Module.WorldFeatures;
        var triggers = source.Triggers.Select(item => item.Id == updatedProfile.Id ? updatedProfile : item).ToArray();
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, triggers, source.Encounters, source.VehicleRoutes,
            source.Merchants, source.Companions, source.Scripts);
        candidateProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(catalog), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests);
        var targetPath = $"module/worldFeatures/triggers/{updatedProfile.Id}";
        var validation = WorldFeatureCatalogValidator.Validate(candidateProject)
            .Where(item => item.Path.StartsWith(targetPath, StringComparison.Ordinal))
            .Select(item => new StudioUx2LwEDiagnostic(item.Code, item.Path,
                HumanizeLivingWorldTriggerDiagnosticUx2LwE(item.Code))).ToArray();
        if (validation.Length > 0)
        {
            diagnostics = validation;
            return false;
        }
        command = new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            catalog, dryRun);
        return true;
    }

    private static StudioUx2LwEDraft DraftFromLivingWorldTriggerUx2LwE(
        TriggerWorldProfile profile,
        ProjectRevision revision)
    {
        var rest = profile.Rest;
        var clue = profile.Clue;
        var narrative = profile.Narrative;
        return new StudioUx2LwEDraft(
            profile.Id,
            profile.BlueprintId,
            profile.AreaId,
            revision,
            LivingWorldTriggerSourceFingerprintUx2LwE(profile),
            rest is not null,
            rest?.Safe ?? true,
            rest?.DangerQuery ?? "danger.nearby",
            rest?.WholePartyRequired ?? true,
            rest?.AllowCampSetup ?? false,
            rest?.AllowedMessageKey ?? "rest.allowed",
            rest?.DeniedMessageKey ?? "rest.denied",
            clue is not null,
            clue?.SkillId ?? "tracking",
            clue?.Difficulty ?? 10,
            clue?.TraceId ?? "trail.authored",
            clue?.DirectionDegrees ?? 0,
            clue?.AgeHours ?? 0,
            clue?.FalseClueId,
            clue?.QuestId,
            narrative is not null,
            narrative?.Participants.ToArray() ?? [],
            narrative?.Priority ?? 0,
            narrative?.Once ?? true,
            narrative?.CooldownSeconds ?? 60,
            narrative?.MultiplayerPolicy ?? "party-vote",
            narrative?.DialogueId,
            profile);
    }

    private static TriggerWorldProfile? FindLivingWorldTriggerUx2LwE(
        ProjectDefinition project,
        StudioUx2LwEDraft draft)
    {
        var matches = project.Module.WorldFeatures.Triggers.Where(item =>
            item.Id == draft.ProfileId && item.BlueprintId == draft.BlueprintId && item.AreaId == draft.AreaId)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static IReadOnlyList<StudioUx2LwEDiff> BuildLivingWorldTriggerDiffUx2LwE(
        TriggerWorldProfile source,
        TriggerWorldProfile? candidate)
    {
        if (candidate is null) return [];
        var restBefore = RestSummaryUx2LwE(source.Rest);
        var restAfter = RestSummaryUx2LwE(candidate.Rest);
        var clueBefore = ClueSummaryUx2LwE(source.Clue);
        var clueAfter = ClueSummaryUx2LwE(candidate.Clue);
        var narrativeBefore = NarrativeSummaryUx2LwE(source.Narrative);
        var narrativeAfter = NarrativeSummaryUx2LwE(candidate.Narrative);
        return
        [
            new("Repos sûr", restBefore, restAfter,
                !string.Equals(restBefore, restAfter, StringComparison.Ordinal)),
            new("Piste", clueBefore, clueAfter,
                !string.Equals(clueBefore, clueAfter, StringComparison.Ordinal)),
            new("Interjection", narrativeBefore, narrativeAfter,
                !string.Equals(narrativeBefore, narrativeAfter, StringComparison.Ordinal)),
        ];
    }

    private static (string RestPreview, string Clue, string Narrative)
        BuildLivingWorldRuntimePreviewUx2LwE(TriggerWorldProfile profile)
    {
        var rest = TriggerLivingWorldRuntimeService.ResolveRest(profile, new(false, 2, 2));
        var clue = TriggerLivingWorldRuntimeService.ResolveClue(profile,
            new(profile.Clue?.Difficulty, true));
        var narrative = TriggerLivingWorldRuntimeService.ResolveNarrative(profile,
            TriggerNarrativeRuntimeState.Empty,
            new(DateTimeOffset.UnixEpoch, true,
                string.Equals(profile.Narrative?.MultiplayerPolicy, "party-vote", StringComparison.Ordinal),
                profile.Narrative?.Participants.ToHashSet() ?? new HashSet<BlueprintId>()));
        return (
            rest.Available
                ? rest.Allowed
                    ? $"Autorisé si danger absent et groupe {2}/{2} · camp " +
                      (rest.CampSetupAllowed ? "possible" : "désactivé")
                    : $"Refusé · {rest.DiagnosticCode}"
                : "Non configuré · la politique de zone reste seule autorité",
            clue.Available
                ? clue.Revealed
                    ? $"Trace {clue.TraceId} · seuil {profile.Clue!.Difficulty} · direction " +
                      $"{clue.DirectionDegrees:0.#}° · âge {clue.AgeHours:0.#} h"
                    : $"Non révélée · {clue.DiagnosticCode}"
                : "Non configurée · aucun indice émis",
            narrative.Available
                ? narrative.Fired
                    ? $"Dialogue canonique prêt · priorité {narrative.Priority} · " +
                      "aucune ouverture exécutée dans l’aperçu"
                    : $"Interjection bloquée · {narrative.DiagnosticCode}"
                : "Non configurée · aucun dialogue demandé");
    }

    private static string RestSummaryUx2LwE(TriggerWorldProfile.RestPolicy? policy) => policy is null
        ? "Désactivé"
        : $"{(policy.Safe ? "sûr" : "non sûr")} · danger {policy.DangerQuery} · " +
          $"groupe {(policy.WholePartyRequired ? "complet" : "individuel")} · " +
          $"camp {(policy.AllowCampSetup ? "autorisé" : "désactivé")} · " +
          $"messages {policy.AllowedMessageKey} / {policy.DeniedMessageKey}";

    private static string ClueSummaryUx2LwE(TriggerWorldProfile.CluePolicy? policy) => policy is null
        ? "Désactivée"
        : $"{policy.SkillId} ≥ {policy.Difficulty} · trace {policy.TraceId} · " +
          $"direction {policy.DirectionDegrees:0.#}° · âge {policy.AgeHours:0.#} h · " +
          $"faux indice {policy.FalseClueId ?? "aucun"} · " +
          $"quête {(policy.QuestId is null ? "aucune" : "liée")}";

    private static string NarrativeSummaryUx2LwE(TriggerWorldProfile.NarrativeCuePolicy? policy) =>
        policy is null
            ? "Désactivée"
            : $"{policy.Participants.Count} participant(s) · priorité {policy.Priority} · " +
              $"{(policy.Once ? "une fois" : "répétable")} · délai {policy.CooldownSeconds:0.#} s · " +
              $"politique {policy.MultiplayerPolicy} · dialogue lié";

    private static StudioUx2LwEPreview FailedLivingWorldTriggerPreviewUx2LwE(string message) =>
        new(false, false, message, string.Empty, [], [],
            "Repos non évalué", "Piste non évaluée", "Interjection non évaluée", true);

    private static string LivingWorldTriggerSourceFingerprintUx2LwE(TriggerWorldProfile profile)
    {
        var builder = new StringBuilder();
        AppendFingerprintUx2LwE(builder, "profile", profile.Id.ToString("D"));
        AppendFingerprintUx2LwE(builder, "blueprint", profile.BlueprintId.ToString());
        AppendFingerprintUx2LwE(builder, "area", profile.AreaId.ToString());
        AppendFingerprintUx2LwE(builder, "provenance", profile.Provenance);
        AppendFingerprintUx2LwE(builder, "rest", RestSummaryUx2LwE(profile.Rest));
        AppendFingerprintUx2LwE(builder, "clue", ClueSummaryUx2LwE(profile.Clue));
        AppendFingerprintUx2LwE(builder, "narrative", NarrativeSummaryUx2LwE(profile.Narrative));
        AppendFingerprintUx2LwE(builder, "participants",
            profile.Narrative is null ? string.Empty :
                string.Join(',', profile.Narrative.Participants.OrderBy(item => item.Value)));
        AppendFingerprintUx2LwE(builder, "dialogue", profile.Narrative?.DialogueId.ToString("D") ?? string.Empty);
        AppendFingerprintUx2LwE(builder, "quest", profile.Clue?.QuestId?.ToString("D") ?? string.Empty);
        return HashFingerprintUx2LwE(builder);
    }

    private static string LivingWorldTriggerDraftFingerprintUx2LwE(StudioUx2LwEDraft draft)
    {
        var builder = new StringBuilder();
        void Add(string key, object? value) => AppendFingerprintUx2LwE(builder, key, value?.ToString() ?? string.Empty);
        Add("profile", draft.ProfileId.ToString("D"));
        Add("revision", draft.BaseRevision.Value);
        Add("source", draft.SourceFingerprint);
        Add("rest.enabled", draft.RestEnabled);
        Add("rest.safe", draft.RestSafe);
        Add("rest.danger", draft.DangerQuery);
        Add("rest.party", draft.WholePartyRequired);
        Add("rest.camp", draft.AllowCampSetup);
        Add("rest.allowed", draft.AllowedMessageKey);
        Add("rest.denied", draft.DeniedMessageKey);
        Add("clue.enabled", draft.ClueEnabled);
        Add("clue.skill", draft.SkillId);
        Add("clue.difficulty", draft.Difficulty);
        Add("clue.trace", draft.TraceId);
        Add("clue.direction", draft.DirectionDegrees);
        Add("clue.age", draft.AgeHours);
        Add("clue.false", draft.FalseClueId);
        Add("clue.quest", draft.QuestId?.ToString("D"));
        Add("narrative.enabled", draft.NarrativeEnabled);
        Add("narrative.participants", string.Join(',', draft.Participants.OrderBy(item => item.Value)));
        Add("narrative.priority", draft.Priority);
        Add("narrative.once", draft.Once);
        Add("narrative.cooldown", draft.CooldownSeconds);
        Add("narrative.policy", draft.MultiplayerPolicy);
        Add("narrative.dialogue", draft.DialogueId?.ToString("D"));
        return HashFingerprintUx2LwE(builder);
    }

    private static void AppendFingerprintUx2LwE(StringBuilder builder, string key, string value) =>
        builder.Append(key.Length).Append(':').Append(key).Append('=').Append(value.Length).Append(':')
            .Append(value).Append(';');

    private static string HashFingerprintUx2LwE(StringBuilder builder) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));

    private static string HumanizeLivingWorldTriggerDiagnosticUx2LwE(string? code) => code switch
    {
        "world.trigger.rest_invalid" => "Complétez la requête de danger et les deux clés de message du repos.",
        "world.trigger.clue_invalid" => "Corrigez la compétence, le seuil, la trace, la direction ou l’âge de la piste.",
        "world.reference.quest_missing" => "La quête choisie n’existe plus dans le projet.",
        "world.trigger.narrative_invalid" => "Corrigez les participants, la priorité, le délai ou la politique multijoueur.",
        "world.trigger.narrative_participant_missing" => "Un participant choisi n’existe plus dans le projet.",
        "world.reference.dialogue_missing" => "Le dialogue choisi n’existe plus dans le projet.",
        "command.revision_conflict" => "Le projet a changé. Rechargez la fiche.",
        null or "" => "La commande canonique a refusé cette fiche.",
        _ => $"Contrat non satisfait · {code}",
    };

    private static string HumanBlueprintKindUx2LwE(BlueprintKind kind) => kind switch
    {
        BlueprintKind.Creature => "créature",
        BlueprintKind.PlayerSpawn => "joueur",
        _ => "participant",
    };

    private static string HumanReferenceProvenanceUx2LwE(string? source, string subject)
    {
        if (string.IsNullOrWhiteSpace(source)) return $"{subject} gouverné";
        if (source.StartsWith("project://", StringComparison.OrdinalIgnoreCase))
            return $"{subject} du projet";
        if (source.StartsWith("builtin", StringComparison.OrdinalIgnoreCase))
            return $"{subject} intégré";
        if (source.StartsWith("sample", StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith("module", StringComparison.OrdinalIgnoreCase))
            return $"{subject} du module";
        return $"{subject} gouverné";
    }

    private static TriggerWorldProfile CreateUx2LwEEvidenceTrigger(
        BlueprintId triggerBlueprint,
        BlueprintId participant,
        AreaId areaId,
        Guid dialogueId,
        Guid? questId)
    {
        var seed = Guid.Parse("e2160000-0000-4000-8000-000000000001");
        return new TriggerWorldProfile(
            StableUx2LwE(seed, "profile"), 1, Ux2LwEEvidenceProvenance, triggerBlueprint, areaId,
            null, null, null, "trigger.living-world", null,
            new(true, "danger.nearby", true, true, "rest.allowed", "rest.denied"),
            new("tracking", 8, "trail.evidence", 45, 2, "trail.decoy", questId),
            new([participant], 6, true, 60, "party-vote", dialogueId), null,
            CreateUx2LwEFoundation(seed), true);
    }

    private static TriggerWorldProfile.FoundationPolicy CreateUx2LwEFoundation(Guid seed) => new(
        StableUx2LwE(seed, "foundation"), 1, Ux2LwEEvidenceProvenance, "volume-monde-vivant",
        new(TriggerWorldProfile.ShapeKind.Box3D, [], [], new(6, 3, 6), 3, .25, true),
        new(["creature"], ["player"], [], ["characters"]), [], 0,
        TriggerWorldProfile.FirePolicy.Repeat,
        [new(StableUx2LwE(seed, "action"), "living-world-preview", "trigger.living-world", new Dictionary<string, string>())],
        null,
        new(Vector3Value.Zero, Vector3Value.Zero, Vector3Value.One, null),
        new Dictionary<string, string> { ["editor.overlay"] = "living-world" },
        new(StableUx2LwE(seed, "enter"), 1, Ux2LwEEvidenceProvenance, "1.0.0",
            "VolumeEntered", null, null),
        new(StableUx2LwE(seed, "exit"), 1, Ux2LwEEvidenceProvenance, "1.0.0",
            "VolumeExited", null, null));

    private static Guid StableUx2LwE(Guid seed, string suffix)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed:D}:{suffix}"));
        return new Guid(bytes[..16]);
    }
}

internal sealed partial class StudioSessionController
{
    internal StudioUx2LwESmokeResult RunUx2LwEContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-ux2-lw-e-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var fixture = CreateUx2LwESmokeProject();
            var created = ProjectSession.Create(tempRoot, fixture.Project);
            if (!created.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX2_LW_E_CONTRACT_CREATE_FAILED " +
                    string.Join(" | ", created.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx2LwESmoke();
            }
            var initialOpen = editing.Open(tempRoot);
            if (!initialOpen.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX2_LW_E_CONTRACT_OPEN_FAILED " +
                    string.Join(" | ", initialOpen.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx2LwESmoke();
            }
            editing.SelectArea(fixture.AreaId);
            var initialProject = editing.Project!;
            var initialCatalog = initialProject.Module.WorldFeatures;
            var loadedTarget = initialCatalog.Triggers.Single(item => item.Id == fixture.TargetId);
            var loadedNeighbor = initialCatalog.Triggers.Single(item => item.Id == fixture.NeighborId);

            var explicitSelection = editing.CreateLivingWorldTriggerDraftUx2LwE() is null &&
                                    editing.LivingWorldTriggerChoicesUx2LwE.Count == 3 &&
                                    editing.LivingWorldTriggerChoicesUx2LwE.All(item =>
                                        IsHumanUx2LwEText(item.Label));
            var foundationRequired = editing.CreateLivingWorldTriggerDraftUx2LwE(
                                         fixture.FoundationlessId) is null &&
                                     editing.LivingWorldTriggerChoicesUx2LwE.Single(item =>
                                         item.ProfileId == fixture.FoundationlessId).HasFoundation == false;

            var staleDraft = editing.CreateLivingWorldTriggerDraftUx2LwE(fixture.TargetId)!;
            var changedBeforePreview = ApplyUx2LwEIntermediateMutation(editing, fixture.NeighborId,
                "trigger.changed-before-preview");
            var stalePreview = editing.PreviewLivingWorldTriggerUx2LwE(staleDraft);
            var staleDraftBlocked = changedBeforePreview && !stalePreview.Succeeded &&
                                    stalePreview.Message.Contains("projet a changé", StringComparison.Ordinal) &&
                                    editing.Undo().Succeeded;

            var invalidSource = editing.CreateLivingWorldTriggerDraftUx2LwE(fixture.TargetId)!;
            var invalidRest = editing.PreviewLivingWorldTriggerUx2LwE(invalidSource with
            {
                DangerQuery = string.Empty,
                AllowedMessageKey = "message invalide",
            });
            var invalidClue = editing.PreviewLivingWorldTriggerUx2LwE(invalidSource with
            {
                DirectionDegrees = 360,
                AgeHours = -1,
            });
            var invalidNarrative = editing.PreviewLivingWorldTriggerUx2LwE(invalidSource with
            {
                DialogueId = null,
            });
            var restValidation = !invalidRest.Succeeded && invalidRest.Diagnostics.Any(item =>
                item.Code == "world.trigger.rest_invalid" && item.Path.EndsWith("/rest", StringComparison.Ordinal));
            var clueValidation = !invalidClue.Succeeded && invalidClue.Diagnostics.Any(item =>
                item.Code == "world.trigger.clue_invalid" && item.Path.EndsWith("/clue", StringComparison.Ordinal));
            var narrativeValidation = !invalidNarrative.Succeeded && invalidNarrative.Diagnostics.Any(item =>
                item.Code == "world.reference.dialogue_missing" &&
                item.Path.EndsWith("/narrative/dialogueId", StringComparison.Ordinal));

            var draft = ConfigureUx2LwESmokeDraft(
                editing.CreateLivingWorldTriggerDraftUx2LwE(fixture.TargetId)!, fixture);
            var historyBefore = editing.History!;
            var projectBefore = editing.Project;
            var revisionBefore = historyBefore.State.Revision;
            var journalBefore = historyBefore.Journal.ToArray();
            var canUndoBefore = historyBefore.CanUndo;
            var canRedoBefore = historyBefore.CanRedo;
            var dirtyBefore = editing.IsDirty;
            var preview = editing.PreviewLivingWorldTriggerUx2LwE(draft);
            var dryRunPure = preview.Succeeded && preview.CanApply && preview.DryRunPure &&
                             ReferenceEquals(projectBefore, editing.Project) &&
                             ReferenceEquals(historyBefore, editing.History) &&
                             historyBefore.State.Revision == revisionBefore &&
                             historyBefore.Journal.SequenceEqual(journalBefore) &&
                             historyBefore.CanUndo == canUndoBefore && historyBefore.CanRedo == canRedoBefore &&
                             editing.IsDirty == dirtyBefore;
            var runtimePreview = preview.RestSummary.Contains("groupe 2/2", StringComparison.Ordinal) &&
                                 preview.ClueSummary.Contains("Trace", StringComparison.Ordinal) &&
                                 preview.NarrativeSummary.Contains("aucune ouverture exécutée", StringComparison.Ordinal);

            var changedAfterPreview = ApplyUx2LwEIntermediateMutation(editing, fixture.NeighborId,
                "trigger.changed-after-preview");
            var staleApply = editing.ApplyApprovedLivingWorldTriggerUx2LwE();
            var staleApprovalBlocked = changedAfterPreview && !staleApply.Succeeded &&
                                       staleApply.Message.Contains("Prévisualisez", StringComparison.Ordinal) &&
                                       editing.Undo().Succeeded;

            var freshDraft = ConfigureUx2LwESmokeDraft(
                editing.CreateLivingWorldTriggerDraftUx2LwE(fixture.TargetId)!, fixture);
            var freshPreview = editing.PreviewLivingWorldTriggerUx2LwE(freshDraft);
            var applied = editing.ApplyApprovedLivingWorldTriggerUx2LwE();
            if (!freshPreview.Succeeded || !applied.Succeeded) return FailedUx2LwESmoke();
            var appliedCatalog = editing.Project!.Module.WorldFeatures;
            var appliedTarget = appliedCatalog.Triggers.Single(item => item.Id == fixture.TargetId);
            var appliedNeighbor = appliedCatalog.Triggers.Single(item => item.Id == fixture.NeighborId);
            var targetNeighborsPreserved =
                loadedTarget.Id == appliedTarget.Id &&
                loadedTarget.SchemaVersion == appliedTarget.SchemaVersion &&
                loadedTarget.BlueprintId == appliedTarget.BlueprintId &&
                loadedTarget.AreaId == appliedTarget.AreaId &&
                ReferenceEquals(loadedTarget.Discovery, appliedTarget.Discovery) &&
                ReferenceEquals(loadedTarget.Interaction, appliedTarget.Interaction) &&
                ReferenceEquals(loadedTarget.Scheduler, appliedTarget.Scheduler) &&
                loadedTarget.CustomEventNamespace == appliedTarget.CustomEventNamespace &&
                ReferenceEquals(loadedTarget.Access, appliedTarget.Access) &&
                ReferenceEquals(loadedTarget.Director, appliedTarget.Director) &&
                ReferenceEquals(loadedTarget.Foundation, appliedTarget.Foundation) &&
                loadedTarget.FoundationRequiredAtRuntime == appliedTarget.FoundationRequiredAtRuntime &&
                ReferenceEquals(loadedTarget.Advanced, appliedTarget.Advanced) &&
                loadedTarget.AdvancedRequiredAtRuntime == appliedTarget.AdvancedRequiredAtRuntime;
            var projectNeighborsPreserved = ReferenceEquals(loadedNeighbor, appliedNeighbor) &&
                                            initialProject.Blueprints.SequenceEqual(editing.Project.Blueprints) &&
                                            initialProject.Areas.SequenceEqual(editing.Project.Areas) &&
                                            initialProject.Dialogues.SequenceEqual(editing.Project.Dialogues) &&
                                            initialProject.Quests.SequenceEqual(editing.Project.Quests);
            var collectionPreserved = SameReferencesUx2LwE(initialCatalog.Placeables, appliedCatalog.Placeables) &&
                                      SameReferencesUx2LwE(initialCatalog.Doors, appliedCatalog.Doors) &&
                                      SameReferencesUx2LwE(initialCatalog.Encounters, appliedCatalog.Encounters) &&
                                      SameReferencesUx2LwE(initialCatalog.VehicleRoutes,
                                          appliedCatalog.VehicleRoutes) &&
                                      SameReferencesUx2LwE(initialCatalog.Merchants, appliedCatalog.Merchants) &&
                                      SameReferencesUx2LwE(initialCatalog.Companions, appliedCatalog.Companions) &&
                                      ReferenceEquals(initialCatalog.Scripts, appliedCatalog.Scripts) &&
                                      appliedCatalog.Triggers.Select(item => item.Id)
                                          .SequenceEqual(initialCatalog.Triggers.Select(item => item.Id));

            var undo = editing.Undo();
            var undoneTarget = editing.Project!.Module.WorldFeatures.Triggers.Single(item =>
                item.Id == fixture.TargetId);
            var redo = editing.Redo();
            var redoneTarget = editing.Project!.Module.WorldFeatures.Triggers.Single(item =>
                item.Id == fixture.TargetId);
            var undoRedo = undo.Succeeded && redo.Succeeded &&
                           LivingWorldTriggerSourceFingerprintUx2LwE(undoneTarget) ==
                           LivingWorldTriggerSourceFingerprintUx2LwE(loadedTarget) &&
                           LivingWorldTriggerSourceFingerprintUx2LwE(redoneTarget) ==
                           LivingWorldTriggerSourceFingerprintUx2LwE(appliedTarget);

            var saved = editing.Save();
            var opened = saved.Succeeded && reopened.Open(tempRoot).Succeeded;
            var persistedTarget = opened ? reopened.Project!.Module.WorldFeatures.Triggers.Single(item =>
                item.Id == fixture.TargetId) : null;
            var persistedNeighbor = opened ? reopened.Project!.Module.WorldFeatures.Triggers.Single(item =>
                item.Id == fixture.NeighborId) : null;
            var persistence = persistedTarget is not null && persistedNeighbor is not null &&
                              LivingWorldTriggerSourceFingerprintUx2LwE(persistedTarget) ==
                              LivingWorldTriggerSourceFingerprintUx2LwE(appliedTarget) &&
                              LivingWorldTriggerSourceFingerprintUx2LwE(persistedNeighbor) ==
                              LivingWorldTriggerSourceFingerprintUx2LwE(appliedNeighbor) &&
                              ProjectJsonSerializer.Serialize(editing.Project!) ==
                              ProjectJsonSerializer.Serialize(reopened.Project!);
            reopened.Close();

            var optionalDraft = editing.CreateLivingWorldTriggerDraftUx2LwE(fixture.TargetId)! with
            {
                RestEnabled = false,
                ClueEnabled = false,
                NarrativeEnabled = false,
            };
            var optionalPreview = editing.PreviewLivingWorldTriggerUx2LwE(optionalDraft);
            var optionalApply = editing.ApplyApprovedLivingWorldTriggerUx2LwE();
            var optionalTarget = editing.Project!.Module.WorldFeatures.Triggers.Single(item =>
                item.Id == fixture.TargetId);
            var optionalUndo = editing.Undo();
            var optionalRedo = editing.Redo();
            var optionalSaved = editing.Save();
            var optionalOpened = optionalSaved.Succeeded && reopened.Open(tempRoot).Succeeded;
            var optionalPersisted = optionalOpened ? reopened.Project!.Module.WorldFeatures.Triggers.Single(item =>
                item.Id == fixture.TargetId) : null;
            var optionalRemoval = optionalPreview.Succeeded && optionalPreview.CanApply &&
                                  optionalApply.Succeeded && optionalTarget is
                                  { Rest: null, Clue: null, Narrative: null } && optionalUndo.Succeeded &&
                                  optionalRedo.Succeeded && optionalPersisted is
                                  { Rest: null, Clue: null, Narrative: null };
            reopened.Close();

            var journal = editing.History!.Journal;
            var canonicalCommand = journal.Count >= 4 &&
                                   journal.All(item => item.CommandName == "set_world_feature_catalog");
            var structuredDiagnostics = new[] { invalidRest, invalidClue, invalidNarrative }
                .SelectMany(item => item.Diagnostics)
                .All(item => !string.IsNullOrWhiteSpace(item.Code) && !string.IsNullOrWhiteSpace(item.Path));
            var humanText = editing.LivingWorldTriggerChoicesUx2LwE.All(item => IsHumanUx2LwEText(item.Label)) &&
                            editing.LivingWorldParticipantChoicesUx2LwE().All(item =>
                                IsHumanUx2LwEText(item.Label) && IsHumanUx2LwEText(item.Provenance)) &&
                            editing.LivingWorldDialogueChoicesUx2LwE().All(item =>
                                IsHumanUx2LwEText(item.Label) && IsHumanUx2LwEText(item.Provenance));
            if (!humanText)
            {
                var visibleValues = editing.LivingWorldTriggerChoicesUx2LwE.Select(item => item.Label)
                    .Concat(editing.LivingWorldParticipantChoicesUx2LwE().SelectMany(item =>
                        new[] { item.Label, item.Provenance }))
                    .Concat(editing.LivingWorldDialogueChoicesUx2LwE().SelectMany(item =>
                        new[] { item.Label, item.Provenance }));
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_LW_E_HUMAN_TEXT_FAILED " +
                                 string.Join(" | ", visibleValues.Where(item => !IsHumanUx2LwEText(item))));
            }

            var succeeded = explicitSelection && foundationRequired && restValidation && clueValidation &&
                            narrativeValidation && runtimePreview && optionalRemoval && dryRunPure &&
                            staleDraftBlocked && staleApprovalBlocked && canonicalCommand && undoRedo &&
                            persistence && targetNeighborsPreserved && projectNeighborsPreserved &&
                            collectionPreserved && structuredDiagnostics && humanText;
            return new(succeeded, explicitSelection, foundationRequired, restValidation, clueValidation,
                narrativeValidation, runtimePreview, optionalRemoval, dryRunPure, staleDraftBlocked,
                staleApprovalBlocked, canonicalCommand, undoRedo, persistence, targetNeighborsPreserved,
                projectNeighborsPreserved, collectionPreserved, structuredDiagnostics, humanText);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_LW_E_CONTRACT_FAILED exception={exception.Message}");
            return FailedUx2LwESmoke();
        }
        finally
        {
            reopened.Close();
            editing.Close();
            try
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup of isolated evidence only.
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort cleanup of isolated evidence only.
            }
        }
    }

    private static StudioUx2LwEDraft ConfigureUx2LwESmokeDraft(
        StudioUx2LwEDraft draft,
        Ux2LwESmokeFixture fixture) => draft with
        {
            RestEnabled = true,
            RestSafe = true,
            DangerQuery = "danger.nearby",
            WholePartyRequired = true,
            AllowCampSetup = true,
            AllowedMessageKey = "rest.allowed",
            DeniedMessageKey = "rest.denied",
            ClueEnabled = true,
            SkillId = "tracking",
            Difficulty = 12,
            TraceId = "trail.warden",
            DirectionDegrees = 90,
            AgeHours = 1.5,
            FalseClueId = "trail.decoy",
            QuestId = null,
            NarrativeEnabled = true,
            Participants = [fixture.ParticipantId],
            Priority = 9,
            Once = true,
            CooldownSeconds = 120,
            MultiplayerPolicy = "party-vote",
            DialogueId = fixture.DialogueId,
        };

    private static bool ApplyUx2LwEIntermediateMutation(
        StudioSessionController session,
        Guid profileId,
        string customNamespace)
    {
        var history = session.History;
        var project = session.Project;
        if (history is null || project is null) return false;
        var source = project.Module.WorldFeatures;
        var triggers = source.Triggers.Select(item => item.Id == profileId
            ? item with { CustomEventNamespace = customNamespace }
            : item).ToArray();
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, triggers, source.Encounters, source.VehicleRoutes,
            source.Merchants, source.Companions, source.Scripts);
        var command = new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            catalog, false);
        var result = new SetWorldFeatureCatalogHandler(new SystemClock()).Handle(history.State, command);
        if (!result.Succeeded) return false;
        history.Record(result);
        return true;
    }

    private static Ux2LwESmokeFixture CreateUx2LwESmokeProject()
    {
        var narrativeProject = SampleModuleAuthoringService.Create().State.Project;
        var dialogue = narrativeProject.Dialogues.Single();
        var participant = narrativeProject.Blueprints.First(item =>
            item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn).Id;
        var targetBlueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("e2160000-0000-4000-8100-000000000001")),
            "seuil-des-pistes", "Seuil des pistes", BlueprintKind.Trigger);
        var neighborBlueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("e2160000-0000-4000-8200-000000000001")),
            "seuil-voisin", "Seuil voisin", BlueprintKind.Trigger);
        var foundationlessBlueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("e2160000-0000-4000-8300-000000000001")),
            "seuil-inacheve", "Seuil inachevé", BlueprintKind.Trigger);
        var area = new AreaDefinition(AreaId.From(Guid.Parse("e2160000-0000-4000-8400-000000000001")),
            "clairiere-des-pistes", "Clairière des pistes", []);
        var shell = new ProjectDefinition(
            ProjectId.From(Guid.Parse("e2160000-0000-4000-8500-000000000001")),
            "Studio UX2-LW-E",
            [targetBlueprint, neighborBlueprint, foundationlessBlueprint, .. narrativeProject.Blueprints],
            [area]);
        var target = CreateUx2LwESmokeTrigger(
            Guid.Parse("e2160000-0000-4000-8600-000000000001"), targetBlueprint.Id,
            participant, area.Id, dialogue.Id.Value, foundation: true);
        var neighbor = CreateUx2LwESmokeTrigger(
            Guid.Parse("e2160000-0000-4000-8700-000000000001"), neighborBlueprint.Id,
            participant, area.Id, dialogue.Id.Value, foundation: true);
        var foundationless = CreateUx2LwESmokeTrigger(
            Guid.Parse("e2160000-0000-4000-8800-000000000001"), foundationlessBlueprint.Id,
            participant, area.Id, dialogue.Id.Value, foundation: false);
        var catalog = new WorldFeatureCatalog(shell.Module.Id.Value, 1, "test:ux2-lw-e",
            triggers: [target, neighbor, foundationless]);
        var project = new ProjectDefinition(shell.Id, shell.Title, shell.Module.WithWorldFeatures(catalog),
            shell.Blueprints, shell.Areas, [dialogue], shell.Quests);
        return new(project, area.Id, target.Id, neighbor.Id, foundationless.Id, participant,
            dialogue.Id.Value);
    }

    private static TriggerWorldProfile CreateUx2LwESmokeTrigger(
        Guid seed,
        BlueprintId blueprintId,
        BlueprintId participant,
        AreaId areaId,
        Guid dialogueId,
        bool foundation) => new(
        StableUx2LwE(seed, "profile"), 1, "test:ux2-lw-e", blueprintId, areaId,
        new(["sight"], 8, StableUx2LwE(seed, "reveal"), "trigger.reveal", true,
            TriggerWorldProfile.DiscoveryScope.Party),
        new("inspect", "cursor.inspect", "outline.gold", "trigger.inspect", "scripts/inspect", true,
            "player"),
        new(5, 12, true, true), "trigger.living-world", null,
        new(true, "danger.nearby", true, false, "rest.allowed", "rest.denied"),
        new("tracking", 8, "trail.authored", 45, 2, "trail.decoy", null),
        new([participant], 4, false, 60, "party-vote", dialogueId),
        new(true, true, true, true, true),
        foundation ? CreateUx2LwEFoundation(seed) : null,
        foundation);

    private static bool SameReferencesUx2LwE<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
        where T : class => left.Count == right.Count &&
                           left.Zip(right).All(pair => ReferenceEquals(pair.First, pair.Second));

    private static bool IsHumanUx2LwEText(string value) =>
        !string.IsNullOrWhiteSpace(value) && !value.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !value.Contains("module/worldFeatures", StringComparison.Ordinal) &&
        !value.Split([' ', '·', '/', ':', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Any(token => Guid.TryParse(token, out _));

    private static StudioUx2LwESmokeResult FailedUx2LwESmoke() => new(
        false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false);

    private sealed record Ux2LwESmokeFixture(
        ProjectDefinition Project,
        AreaId AreaId,
        Guid TargetId,
        Guid NeighborId,
        Guid FoundationlessId,
        BlueprintId ParticipantId,
        Guid DialogueId);
}
