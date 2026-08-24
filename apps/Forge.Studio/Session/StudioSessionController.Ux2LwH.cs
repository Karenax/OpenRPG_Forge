using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Encounters;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2LwHProvenance = "studio:ux2-lw-h";
    private const string Ux2LwHEvidenceProvenance = "studio:ux2-lw-h:evidence";
    private readonly SetWorldFeatureCatalogHandler _setEncounterScenarioCatalogUx2LwH = new(new SystemClock());
    private StudioUx2LwHDraft? _approvedEncounterScenarioUx2LwH;
    private ProjectRevision? _approvedEncounterScenarioRevisionUx2LwH;
    private string? _approvedEncounterScenarioSourceFingerprintUx2LwH;
    private string? _approvedEncounterScenarioDraftFingerprintUx2LwH;

    internal IReadOnlyList<StudioUx2LwHEncounterChoice> EncounterChoicesUx2LwH
    {
        get
        {
            var project = Project;
            var area = ActiveArea;
            if (project is null || area is null) return [];
            return project.Module.WorldFeatures.Encounters.Where(item => item.AreaId == area.Id)
                .Select(item => new StudioUx2LwHEncounterChoice(item.Id,
                    $"{HumanPolicyReferenceUx2LwH(HumanEncounterNameUx2LwF(item))} · " +
                    (!IsFoundationCompleteUx2LwH(item) ? "fiche LW-F incomplète" :
                        item.Scenario is null ? "scénario à créer explicitement" :
                        $"{item.Scenario.SpawnAnchors.Count} ancre(s), " +
                        $"{item.Scenario.Phases.Sum(phase => phase.Waves.Count)} vague(s)"),
                    IsFoundationCompleteUx2LwH(item), item.Scenario is not null,
                    string.Equals(item.Scenario?.Provenance, Ux2LwHEvidenceProvenance,
                        StringComparison.Ordinal)))
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase).ToArray();
        }
    }

    internal IReadOnlyList<StudioUx2LwHReferenceChoice<string>> EncounterAnchorChoicesUx2LwH(Guid profileId)
    {
        var project = Project;
        var profile = project?.Module.WorldFeatures.Encounters.SingleOrDefault(item => item.Id == profileId);
        if (!IsFoundationCompleteUx2LwH(profile)) return [];
        var real = EncounterAnchorChoicesUx2LwF(profile!.AreaId).ToDictionary(item => item.Id,
            StringComparer.Ordinal);
        return profile.Foundation!.SpawnAnchorIds.Where(real.ContainsKey).Distinct(StringComparer.Ordinal)
            .Select(id => new StudioUx2LwHReferenceChoice<string>(id,
                HumanPolicyReferenceUx2LwH(real[id].Label), real[id].Provenance))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    internal IReadOnlyList<StudioUx2LwHReferenceChoice<Guid>> EncounterGroupChoicesUx2LwH(Guid profileId)
    {
        var project = Project;
        var composition = project?.Module.WorldFeatures.Encounters.SingleOrDefault(item => item.Id == profileId)
            ?.Composition;
        return composition?.Groups.Select(item => new StudioUx2LwHReferenceChoice<Guid>(item.Id,
                $"{HumanPolicyReferenceUx2LwH(HumanTokenUx2LwF(item.Slug))} · " +
                $"{item.Minimum} à {item.Maximum}",
                "Groupe canonique de la fiche LW-F"))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase).ToArray() ?? [];
    }

    internal IReadOnlyList<StudioUx2LwHReferenceChoice<string>> EncounterScriptChoicesUx2LwH() =>
        EncounterScriptChoicesUx2LwF().Select(item => new StudioUx2LwHReferenceChoice<string>(
            item.Id, HumanPolicyReferenceUx2LwH(item.Label), item.Provenance)).ToArray();

    internal IReadOnlyList<StudioUx2LwHQuestChoice> EncounterQuestChoicesUx2LwH()
    {
        var project = Project;
        if (project is null) return [];
        return project.Quests.Select(quest => new StudioUx2LwHQuestChoice(quest.Id.Value,
                HumanPolicyReferenceUx2LwH(quest.DisplayName),
                $"Quête réelle du module · {HumanProvenanceUx2LwF(quest.Provenance)}",
                quest.Authoring?.Objectives.Select(objective => new StudioUx2LwHReferenceChoice<string>(
                    objective.Id.ToString("D"), HumanPolicyReferenceUx2LwH(objective.Label.Fallback),
                    $"Objectif canonique · {HumanProvenanceUx2LwF(objective.Provenance)}")).ToArray() ?? []))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    internal IReadOnlyList<StudioUx2LwHReferenceChoice<string>> EncounterAudioChoicesUx2LwH()
    {
        var project = Project;
        if (project is null) return [];
        var choices = new List<StudioUx2LwHReferenceChoice<string>>();
        choices.AddRange(project.Areas.SelectMany(area => area.Navigation.Waypoints.Locators)
            .Where(locator => locator.Purpose == LocatorPurpose.Audio &&
                              locator.AssetId?.StartsWith("audio:", StringComparison.Ordinal) == true)
            .Select(locator => new StudioUx2LwHReferenceChoice<string>(locator.AssetId!,
                HumanPolicyReferenceUx2LwH(locator.AssetId),
                $"Locator audio réel · {HumanProvenanceUx2LwF(locator.Provenance)}")));
        choices.AddRange(project.Module.WorldFeatures.Encounters.SelectMany(item =>
                item.Scenario?.Phases.SelectMany(phase => phase.Waves) ?? [])
            .Where(wave => wave.VoiceCueId?.StartsWith("audio:", StringComparison.Ordinal) == true)
            .Select(wave => new StudioUx2LwHReferenceChoice<string>(wave.VoiceCueId!,
                HumanPolicyReferenceUx2LwH(wave.VoiceCueId),
                "Adresse audio déjà enregistrée · disponibilité runtime non exécutée ici")));
        return choices.DistinctBy(item => item.Id, StringComparer.Ordinal)
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    internal StudioUx2LwHDraft? CreateEncounterScenarioDraftUx2LwH(Guid? profileId)
    {
        var history = History;
        var project = Project;
        var area = ActiveArea;
        if (history is null || project is null || area is null || profileId is null) return null;
        var profile = project.Module.WorldFeatures.Encounters.SingleOrDefault(item =>
            item.Id == profileId.Value && item.AreaId == area.Id);
        if (profile is not { Foundation: not null, Composition: not null, Scenario: not null }) return null;
        return DraftFromScenarioUx2LwH(profile, history.State.Revision, ProjectFingerprintUx2LwF(project));
    }

    internal StudioUx2LwHDraft? CreateNewEncounterScenarioDraftUx2LwH(
        Guid? profileId,
        string? anchorId,
        Guid? groupId)
    {
        var history = History;
        var project = Project;
        var area = ActiveArea;
        if (history is null || project is null || area is null || profileId is null ||
            string.IsNullOrWhiteSpace(anchorId) || groupId is null)
            return null;
        var profile = project.Module.WorldFeatures.Encounters.SingleOrDefault(item =>
            item.Id == profileId.Value && item.AreaId == area.Id);
        if (profile is not { Foundation: not null, Composition: not null, Scenario: null } ||
            !EncounterAnchorChoicesUx2LwH(profile.Id).Any(item => item.Id == anchorId) ||
            !EncounterGroupChoicesUx2LwH(profile.Id).Any(item => item.Id == groupId.Value))
            return null;
        var scenario = CreateScenarioUx2LwH(profile, anchorId.Trim(), groupId.Value,
            Ux2LwHProvenance);
        return new(profile.Id, true, history.State.Revision, ProjectFingerprintUx2LwF(project),
            scenario.Id, scenario.Provenance, scenario.Distribution, scenario.SpawnAnchors,
            scenario.Activator, scenario.Lifecycle, scenario.Phases, scenario.Overrides, true, profile);
    }

    internal StudioUx2LwHPreview PreviewEncounterScenarioUx2LwH(StudioUx2LwHDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelEncounterScenarioPreviewUx2LwH();
        var history = History;
        var project = Project;
        if (history is null || project is null)
            return FailedEncounterScenarioPreviewUx2LwH("Aucun module n’est ouvert.");
        if (draft.BaseRevision != history.State.Revision)
            return FailedEncounterScenarioPreviewUx2LwH(
                "Le projet a changé depuis l’ouverture du scénario. Rechargez la rencontre.");
        if (!string.Equals(draft.SourceFingerprint, ProjectFingerprintUx2LwF(project), StringComparison.Ordinal))
            return FailedEncounterScenarioPreviewUx2LwH(
                "Le catalogue a changé. Rechargez le scénario avant l’aperçu.");
        if (!TryBuildEncounterScenarioCandidateUx2LwH(draft, true, out var command, out var candidate,
                out var diagnostics))
            return new(false, false,
                diagnostics.FirstOrDefault()?.Message ?? "Le scénario contient une valeur invalide.",
                string.Empty, BuildEncounterScenarioDiffUx2LwH(draft.OriginalProfile, candidate), diagnostics,
                EmptyEncounterScenarioRuntimeUx2LwH(), true);
        var result = _setEncounterScenarioCatalogUx2LwH.Handle(history.State, command!);
        if (!result.Succeeded)
        {
            var commandDiagnostics = result.Diagnostics.Select(item => new StudioUx2LwHDiagnostic(item.Code,
                CommandDiagnosticPathUx2LwH(item, draft.ProfileId),
                HumanEncounterScenarioDiagnosticUx2LwH(item.Code))).ToArray();
            return new(false, false,
                commandDiagnostics.FirstOrDefault()?.Message ?? "La commande canonique a refusé le scénario.",
                result.Patch?.Summary ?? string.Empty,
                BuildEncounterScenarioDiffUx2LwH(draft.OriginalProfile, candidate), commandDiagnostics,
                EmptyEncounterScenarioRuntimeUx2LwH(), true);
        }
        var runtime = BuildEncounterScenarioRuntimeUx2LwH(candidate!);
        var diff = BuildEncounterScenarioDiffUx2LwH(draft.OriginalProfile, candidate);
        var canApply = draft.IsNew || diff.Any(item => item.Changed);
        if (canApply)
        {
            _approvedEncounterScenarioUx2LwH = draft;
            _approvedEncounterScenarioRevisionUx2LwH = history.State.Revision;
            _approvedEncounterScenarioSourceFingerprintUx2LwH = draft.SourceFingerprint;
            _approvedEncounterScenarioDraftFingerprintUx2LwH = EncounterScenarioDraftFingerprintUx2LwH(draft);
        }
        return new(true, canApply,
            canApply
                ? "Aperçu sec validé · scénario canonique seulement, sans exécution live."
                : "Le scénario est déjà identique au document canonique.",
            result.Patch?.Summary ?? string.Empty, diff, [], runtime, true);
    }

    internal StudioUx2LwHActionResult ApplyApprovedEncounterScenarioUx2LwH()
    {
        var history = History;
        var project = Project;
        var draft = _approvedEncounterScenarioUx2LwH;
        if (history is null || project is null || draft is null ||
            _approvedEncounterScenarioRevisionUx2LwH != history.State.Revision ||
            !string.Equals(_approvedEncounterScenarioSourceFingerprintUx2LwH,
                ProjectFingerprintUx2LwF(project), StringComparison.Ordinal) ||
            !string.Equals(_approvedEncounterScenarioDraftFingerprintUx2LwH,
                EncounterScenarioDraftFingerprintUx2LwH(draft), StringComparison.Ordinal))
        {
            CancelEncounterScenarioPreviewUx2LwH();
            return new(false, "Prévisualisez à nouveau ce scénario avant de l’appliquer.");
        }
        if (!TryBuildEncounterScenarioCandidateUx2LwH(draft, false, out var command, out _,
                out var diagnostics))
        {
            CancelEncounterScenarioPreviewUx2LwH();
            return new(false, diagnostics.FirstOrDefault()?.Message ?? "Le scénario est invalide.");
        }
        var result = _setEncounterScenarioCatalogUx2LwH.Handle(history.State, command!);
        CancelEncounterScenarioPreviewUx2LwH();
        if (!result.Succeeded)
            return new(false, HumanEncounterScenarioDiagnosticUx2LwH(result.Diagnostics.FirstOrDefault()?.Code));
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, "Scénario appliqué · Ctrl+Z pour annuler, Ctrl+Y pour rétablir.");
    }

    internal void CancelEncounterScenarioPreviewUx2LwH()
    {
        _approvedEncounterScenarioUx2LwH = null;
        _approvedEncounterScenarioRevisionUx2LwH = null;
        _approvedEncounterScenarioSourceFingerprintUx2LwH = null;
        _approvedEncounterScenarioDraftFingerprintUx2LwH = null;
    }

    internal StudioUx2LwHApprovalSnapshot CaptureEncounterScenarioApprovalUx2LwH() => new(
        _approvedEncounterScenarioUx2LwH, _approvedEncounterScenarioRevisionUx2LwH,
        _approvedEncounterScenarioSourceFingerprintUx2LwH, _approvedEncounterScenarioDraftFingerprintUx2LwH);

    internal void RestoreEncounterScenarioApprovalUx2LwH(StudioUx2LwHApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedEncounterScenarioUx2LwH = snapshot.Draft;
        _approvedEncounterScenarioRevisionUx2LwH = snapshot.Revision;
        _approvedEncounterScenarioSourceFingerprintUx2LwH = snapshot.SourceFingerprint;
        _approvedEncounterScenarioDraftFingerprintUx2LwH = snapshot.DraftFingerprint;
    }

    internal void CloseUx2LwH() => CancelEncounterScenarioPreviewUx2LwH();

    internal ProjectDefinition? CreateUx2LwHEvidenceProject()
    {
        var project = CreateUx2LwFEvidenceProject();
        var activeAreaId = ActiveAreaId;
        if (project is null || activeAreaId is null) return null;
        var target = project.Module.WorldFeatures.Encounters.FirstOrDefault(item =>
            item.AreaId == activeAreaId && IsFoundationCompleteUx2LwH(item));
        if (target is null) return null;
        if (target.Scenario is not null) return project;
        var anchorId = target.Foundation!.SpawnAnchorIds.FirstOrDefault();
        var groupId = target.Composition!.Groups.FirstOrDefault()?.Id;
        if (string.IsNullOrWhiteSpace(anchorId) || groupId is null) return null;
        var scenario = CreateScenarioUx2LwH(target, anchorId, groupId.Value, Ux2LwHEvidenceProvenance);
        var updated = target with { Scenario = scenario, ScenarioRequiredAtRuntime = true };
        var source = project.Module.WorldFeatures;
        var encounters = source.Encounters.Select(item => item.Id == updated.Id ? updated : item).ToArray();
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, source.Triggers, encounters, source.VehicleRoutes,
            source.Merchants, source.Companions, source.Scripts);
        return new ProjectDefinition(project.Id, project.Title, project.Module.WithWorldFeatures(catalog),
            project.Blueprints, project.Areas, project.Dialogues, project.Quests);
    }

    private bool TryBuildEncounterScenarioCandidateUx2LwH(
        StudioUx2LwHDraft draft,
        bool dryRun,
        out SetWorldFeatureCatalogCommand? command,
        out EncounterWorldProfile? candidate,
        out IReadOnlyList<StudioUx2LwHDiagnostic> diagnostics)
    {
        command = null; candidate = null; diagnostics = [];
        var history = History;
        var project = Project;
        if (history is null || project is null || draft.BaseRevision != history.State.Revision)
        {
            diagnostics = [new("command.revision_conflict", "project/revision",
                "Le projet a changé. Rechargez la rencontre.")];
            return false;
        }
        if (!string.Equals(draft.SourceFingerprint, ProjectFingerprintUx2LwF(project), StringComparison.Ordinal))
        {
            diagnostics = [new("command.source_changed", "module/worldFeatures/encounters",
                "Le catalogue a changé. Rechargez la rencontre.")];
            return false;
        }
        var current = project.Module.WorldFeatures.Encounters.SingleOrDefault(item => item.Id == draft.ProfileId);
        if (current is not { Foundation: not null, Composition: not null })
        {
            diagnostics = [new("world.encounter.foundation_required",
                $"module/worldFeatures/encounters/{draft.ProfileId:D}",
                "Terminez d’abord la fiche Foundation et composition du lot LW-F.")];
            return false;
        }
        var currentProfile = current;
        if (draft.IsNew ? currentProfile.Scenario is not null : currentProfile.Scenario is null ||
            !draft.IsNew && draft.ScenarioId != currentProfile.Scenario!.Id)
        {
            diagnostics = [new("world.encounter.scenario_selection_stale",
                $"module/worldFeatures/encounters/{draft.ProfileId:D}/scenario",
                "Le scénario sélectionné a changé. Rechargez la rencontre.")];
            return false;
        }
        var realAnchors = EncounterAnchorChoicesUx2LwH(currentProfile.Id).Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (draft.SpawnAnchors.Any(item => !realAnchors.Contains(item.AnchorId)))
        {
            diagnostics = [new("world.encounter.spawn_anchor_reference_missing",
                $"module/worldFeatures/encounters/{draft.ProfileId:D}/scenario/spawnAnchors",
                "Chaque point doit rester lié à une ancre réelle de la fiche LW-F.")];
            return false;
        }
        var realScripts = EncounterScriptChoicesUx2LwH().Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var lifecycleScripts = new[]
        {
            draft.Lifecycle.Entered.ScriptId,
            draft.Lifecycle.Exited.ScriptId,
            draft.Lifecycle.Exhausted.ScriptId,
        };
        if (lifecycleScripts.Any(item => item is not null && !realScripts.Contains(item)))
        {
            diagnostics = [new("world.encounter.lifecycle_script_missing",
                $"module/worldFeatures/encounters/{draft.ProfileId:D}/scenario/lifecycle",
                "Choisissez uniquement des ForgeScripts réels encore présents dans le workspace.")];
            return false;
        }
        var knownAudio = EncounterAudioChoicesUx2LwH().Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        if (draft.Phases.SelectMany(item => item.Waves).Any(wave =>
                wave.VoiceCueId is not null && !knownAudio.Contains(wave.VoiceCueId)))
        {
            diagnostics = [new("world.encounter.wave_voice_reference_missing",
                $"module/worldFeatures/encounters/{draft.ProfileId:D}/scenario/phases",
                "Choisissez une adresse audio déjà enregistrée dans le projet.")];
            return false;
        }
        var quests = project.Quests.ToDictionary(item => item.Id.Value);
        foreach (var wave in draft.Phases.SelectMany(item => item.Waves).Where(item =>
                     item.Completion == EncounterWaveCompletionPolicy.ObjectiveCompleted ||
                     item.QuestId is not null || !string.IsNullOrWhiteSpace(item.ObjectiveId)))
        {
            if (wave.QuestId is null || !quests.TryGetValue(wave.QuestId.Value, out var quest) ||
                string.IsNullOrWhiteSpace(wave.ObjectiveId))
            {
                diagnostics = [new("world.encounter.wave_objective_reference_missing",
                    $"module/worldFeatures/encounters/{draft.ProfileId:D}/scenario/phases",
                    "Choisissez une quête réelle et une clé d’objectif explicite.")];
                return false;
            }
            if (Guid.TryParse(wave.ObjectiveId, out var typedObjective) &&
                quest.Authoring?.Objectives.Any(item => item.Id == typedObjective) != true)
            {
                diagnostics = [new("world.encounter.wave_objective_reference_missing",
                    $"module/worldFeatures/encounters/{draft.ProfileId:D}/scenario/phases",
                    "L’objectif typé n’existe plus dans la quête choisie.")];
                return false;
            }
        }
        var scenario = new EncounterScenarioProfile(draft.ScenarioId,
            EncounterScenarioProfile.CurrentSchemaVersion, draft.Provenance,
            draft.SpawnAnchors.ToArray(), draft.Distribution, draft.Activator, draft.Lifecycle,
            draft.Phases.ToArray(), draft.Overrides);
        var candidateProfile = currentProfile with
        {
            Scenario = scenario,
            ScenarioRequiredAtRuntime = draft.IsNew ? true : currentProfile.ScenarioRequiredAtRuntime,
        };
        candidate = candidateProfile;
        var source = project.Module.WorldFeatures;
        var encounters = source.Encounters.Select(item => item.Id == candidateProfile.Id ? candidateProfile : item)
            .ToArray();
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, source.Triggers, encounters, source.VehicleRoutes,
            source.Merchants, source.Companions, source.Scripts);
        var candidateProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(catalog), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests);
        var path = $"module/worldFeatures/encounters/{candidateProfile.Id}/scenario";
        var canonical = WorldFeatureCatalogValidator.Validate(candidateProject)
            .Where(item => item.Path.StartsWith(path, StringComparison.Ordinal))
            .Select(item => new StudioUx2LwHDiagnostic(item.Code, item.Path,
                HumanEncounterScenarioDiagnosticUx2LwH(item.Code))).ToList();
        if (canonical.Count == 0)
        {
            try
            {
                var activator = new Forge.RpgKit.Domain.Encounters.Activator("studio-preview", true,
                    false, false, false, false, MapAuthorityUx2LwH(draft.Activator.RequiredAuthority),
                    draft.Activator.RequiredTags.ToHashSet(StringComparer.Ordinal));
                canonical.AddRange(EncounterCompositionFoundationService.PreviewScenario(candidateProfile,
                        activator, candidateProfile.Composition!.Difficulty.ExpectedPartySize,
                        candidateProfile.Composition.Difficulty.ExpectedPartyPower,
                        new HashSet<string>(["party"], StringComparer.Ordinal), DateTimeOffset.UnixEpoch,
                        TimeSpan.FromSeconds(3), 1).Diagnostics
                    .Select(item => new StudioUx2LwHDiagnostic(item.Code, item.Path,
                        HumanEncounterScenarioDiagnosticUx2LwH(item.Code))));
            }
            catch (InvalidOperationException exception)
            {
                canonical.Add(new("world.encounter.scenario_runtime_mapping_invalid", path,
                    HumanEncounterScenarioDiagnosticUx2LwH(exception.Message)));
            }
        }
        if (canonical.Count > 0)
        {
            diagnostics = canonical.DistinctBy(item => (item.Code, item.Path)).ToArray();
            return false;
        }
        command = new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()),
            history.State.Revision, catalog, dryRun);
        return true;
    }

    private static StudioUx2LwHDraft DraftFromScenarioUx2LwH(
        EncounterWorldProfile profile,
        ProjectRevision revision,
        string sourceFingerprint)
    {
        var scenario = profile.Scenario!;
        return new(profile.Id, false, revision, sourceFingerprint, scenario.Id, scenario.Provenance,
            scenario.Distribution, scenario.SpawnAnchors, scenario.Activator, scenario.Lifecycle,
            scenario.Phases, scenario.Overrides, profile.ScenarioRequiredAtRuntime, profile);
    }

    private static EncounterScenarioProfile CreateScenarioUx2LwH(
        EncounterWorldProfile profile,
        string anchorId,
        Guid groupId,
        string provenance)
    {
        var scenarioId = StableUx2LwF(profile.Id, "ux2-lw-h:scenario");
        var waveId = StableUx2LwF(profile.Id, "ux2-lw-h:wave:1");
        return new(scenarioId, EncounterScenarioProfile.CurrentSchemaVersion, provenance,
            [new(StableUx2LwF(profile.Id, "ux2-lw-h:anchor:" + anchorId), anchorId,
                TransformValue.Identity, Math.Max(1, profile.Foundation!.NetworkActorCap), true,
                profile.Visibility.Mode, true, 1)],
            EncounterSpawnDistribution.Sequential,
            new(StableUx2LwF(profile.Id, "ux2-lw-h:activator"), true, true, true, true, true,
                EncounterNetworkAuthority.Server, ["party"]),
            new(StableUx2LwF(profile.Id, "ux2-lw-h:lifecycle"), "1.0.0",
                new(StableUx2LwF(profile.Id, "ux2-lw-h:entered"), "EncounterEntered", null),
                new(StableUx2LwF(profile.Id, "ux2-lw-h:exited"), "EncounterExited", null),
                new(StableUx2LwF(profile.Id, "ux2-lw-h:exhausted"), "EncounterExhausted", null)),
            [new(StableUx2LwF(profile.Id, "ux2-lw-h:phase:1"), 1, "Ouverture", [],
                [new(waveId, 1, [groupId], 0, EncounterWaveCompletionPolicy.AllDefeated,
                    null, null, null, null, null)], [])],
            new Dictionary<string, string>());
    }

    private IReadOnlyList<StudioUx2LwHDiff> BuildEncounterScenarioDiffUx2LwH(
        EncounterWorldProfile before,
        EncounterWorldProfile? after)
    {
        if (after?.Scenario is null) return [];
        var rows = new List<StudioUx2LwHDiff>();
        var previous = before.Scenario;
        var current = after.Scenario;
        var anchorLabels = EncounterAnchorChoicesUx2LwH(after.Id)
            .ToDictionary(item => item.Id, item => item.Label, StringComparer.Ordinal);
        string ResolveAnchor(string id) => anchorLabels.GetValueOrDefault(id, "ancre enregistrée absente");
        Add("Points de spawn manuels", AnchorSummaryUx2LwH(previous, ResolveAnchor),
            AnchorSummaryUx2LwH(current, ResolveAnchor),
            previous is null || previous.Distribution != current.Distribution ||
            !previous.SpawnAnchors.SequenceEqual(current.SpawnAnchors));
        Add("Restriction joueurs", ActivatorSummaryUx2LwH(before.Scenario?.Activator),
            ActivatorSummaryUx2LwH(current.Activator), previous is null ||
            !ActivatorPoliciesEqualUx2LwH(previous.Activator, current.Activator));
        Add("OnEnter, OnExit et OnExhausted", LifecycleSummaryUx2LwH(before.Scenario?.Lifecycle),
            LifecycleSummaryUx2LwH(current.Lifecycle), previous is null ||
            previous.Lifecycle != current.Lifecycle);
        Add("Phases et vagues", PhaseSummaryUx2LwH(previous), PhaseSummaryUx2LwH(current),
            previous is null || !PhasePoliciesEqualUx2LwH(previous.Phases, current.Phases));
        Add("Actions de vague", WaveActionSummaryUx2LwH(before),
            WaveActionSummaryUx2LwH(after), previous is null ||
            !WavePoliciesEqualUx2LwH(previous.Phases, current.Phases));
        return rows;

        void Add(string section, string oldValue, string newValue, bool changed) =>
            rows.Add(new(section, oldValue, newValue, changed));
    }

    private StudioUx2LwHRuntimePreview BuildEncounterScenarioRuntimeUx2LwH(
        EncounterWorldProfile profile)
    {
        var scenario = profile.Scenario!;
        var activator = new Forge.RpgKit.Domain.Encounters.Activator("studio-preview", true,
            false, false, false, false, MapAuthorityUx2LwH(scenario.Activator.RequiredAuthority),
            scenario.Activator.RequiredTags.ToHashSet(StringComparer.Ordinal));
        var preview = EncounterCompositionFoundationService.PreviewScenario(profile, activator,
            profile.Composition!.Difficulty.ExpectedPartySize,
            profile.Composition.Difficulty.ExpectedPartyPower,
            new HashSet<string>(["party"], StringComparer.Ordinal), DateTimeOffset.UnixEpoch,
            TimeSpan.FromSeconds(3), 1);
        var anchorLabels = EncounterAnchorChoicesUx2LwH(profile.Id)
            .ToDictionary(item => item.Id, item => item.Label, StringComparer.Ordinal);
        return new(AnchorSummaryUx2LwH(scenario,
                id => anchorLabels.GetValueOrDefault(id, "ancre enregistrée absente")),
            $"{ActivatorSummaryUx2LwH(scenario.Activator)} · " +
            (preview.ActivatorAccepted ? "profil témoin accepté" : "profil témoin refusé"),
            $"bindings {LifecycleSummaryUx2LwH(scenario.Lifecycle)} · payloads Entered/Exited/Exhausted construits en mémoire",
            $"{PhaseSummaryUx2LwH(scenario)} · {WaveActionSummaryUx2LwH(profile)}",
            "Projection de données et événements en mémoire uniquement : aucun gizmo, nav bake, ForgeScript, " +
            "quête, audio, spawn d’acteur, cadence moteur ou transport réseau n’est exécuté.");
    }

    private static StudioUx2LwHRuntimePreview EmptyEncounterScenarioRuntimeUx2LwH() => new(
        "Ancres non évaluées", "Activateur non évalué", "Lifecycle non évalué", "Vagues non évaluées",
        "Aucun gizmo, nav bake, ForgeScript, quête, audio, spawn ou réseau n’a été exécuté.");

    private static StudioUx2LwHPreview FailedEncounterScenarioPreviewUx2LwH(string message) => new(
        false, false, message, string.Empty, [], [], EmptyEncounterScenarioRuntimeUx2LwH(), true);

    private static string AnchorSummaryUx2LwH(
        EncounterScenarioProfile? scenario,
        Func<string, string>? resolveAnchor = null) => scenario is null
        ? "Scénario absent"
        : $"{HumanDistributionUx2LwH(scenario.Distribution)} · " +
          string.Join(" ; ", scenario.SpawnAnchors.Select((anchor, index) =>
              $"ancre {index + 1} {resolveAnchor?.Invoke(anchor.AnchorId) ?? "référence enregistrée"}: " +
              $"position {anchor.Transform.Position.X:0.##}/{anchor.Transform.Position.Y:0.##}/" +
              $"{anchor.Transform.Position.Z:0.##}, échelle {anchor.Transform.Scale.X:0.##}/" +
              $"{anchor.Transform.Scale.Y:0.##}/{anchor.Transform.Scale.Z:0.##}, capacité {anchor.Capacity}, " +
              $"orientation {anchor.Transform.Rotation.X:0.###}/{anchor.Transform.Rotation.Y:0.###}/" +
              $"{anchor.Transform.Rotation.Z:0.###}/{anchor.Transform.Rotation.W:0.###}, " +
              $"{(anchor.Navigable ? "navigable" : "non navigable")}, " +
              $"{HumanVisibilityUx2LwH(anchor.Visibility)}, " +
              $"{(anchor.IsFallback ? "fallback" : "secondaire")}, poids {anchor.Weight:0.##}"));

    private static string ActivatorSummaryUx2LwH(EncounterScenarioProfile.ActivatorPolicy? policy) => policy is null
        ? "Politique absente"
        : $"{(policy.PlayersOnly ? "joueurs uniquement" : "tous activateurs")} · " +
          $"autorité {HumanAuthorityUx2LwH(policy.RequiredAuthority)} · " +
          $"exclusions {string.Join(", ", new[]
          {
              policy.IgnoreSummons ? "invocations" : null,
              policy.IgnoreCompanions ? "compagnons" : null,
              policy.IgnoreGameMasters ? "MJ" : null,
              policy.IgnoreSpectators ? "spectateurs" : null,
          }.OfType<string>().DefaultIfEmpty("aucune"))} · " +
          $"tags {string.Join(", ", policy.RequiredTags.Select(HumanPolicyReferenceUx2LwH)
              .DefaultIfEmpty("aucun"))}";

    private static string LifecycleSummaryUx2LwH(EncounterScenarioProfile.LifecyclePolicy? lifecycle) => lifecycle is null
        ? "Bindings absents"
        : $"API {lifecycle.ForgeScriptApiVersion} · Enter {ScriptStateUx2LwH(lifecycle.Entered.ScriptId)} · " +
          $"Exit {ScriptStateUx2LwH(lifecycle.Exited.ScriptId)} · " +
          $"Exhausted {ScriptStateUx2LwH(lifecycle.Exhausted.ScriptId)}";

    private static string PhaseSummaryUx2LwH(EncounterScenarioProfile? scenario) => scenario is null
        ? "Graphe absent"
        : string.Join(" ; ", scenario.Phases.OrderBy(item => item.Order).Select((phase, index) =>
            $"phase {index + 1} ordre {phase.Order}, {HumanPolicyReferenceUx2LwH(phase.DisplayName)}, " +
            $"{phase.Waves.Count} vague(s), conditions " +
            $"{string.Join(", ", phase.EntryConditions.Select(HumanPolicyReferenceUx2LwH)
                .DefaultIfEmpty("aucune"))}, renforts préservés {phase.ReinforcementEncounterIds.Count}"));

    private string WaveActionSummaryUx2LwH(EncounterWorldProfile profile)
    {
        var scenario = profile.Scenario;
        if (scenario is null) return "Actions absentes";
        var groupLabels = profile.Composition?.Groups.ToDictionary(item => item.Id,
            item => HumanPolicyReferenceUx2LwH(item.Slug)) ?? [];
        var quests = Project?.Quests.ToDictionary(item => item.Id.Value) ?? [];
        var orderedWaves = scenario.Phases.OrderBy(item => item.Order)
            .SelectMany(item => item.Waves.OrderBy(wave => wave.Order)).ToArray();
        var waveLabels = orderedWaves.Select((wave, index) => (wave.Id, Label: $"vague {index + 1}"))
            .ToDictionary(item => item.Id, item => item.Label);
        return string.Join(" ; ", orderedWaves.Select((wave, index) =>
            $"vague {index + 1} ordre {wave.Order}, groupes " +
            $"{string.Join(", ", wave.GroupIds.Select(id => groupLabels.GetValueOrDefault(id,
                "groupe enregistré absent")))}" +
            $", délai {wave.DelaySeconds:0.##}, {HumanCompletionUx2LwH(wave.Completion)}" +
            (wave.CompletionTargetId is null
                ? string.Empty
                : $", cible {HumanPolicyReferenceUx2LwH(wave.CompletionTargetId)}") +
            ObjectiveSummary(wave) +
            (wave.VoiceCueId is null
                ? string.Empty
                : $", voix {HumanPolicyReferenceUx2LwH(wave.VoiceCueId)}") +
            (wave.NextWaveId is null
                ? string.Empty
                : $", transition vers {waveLabels.GetValueOrDefault(wave.NextWaveId.Value, "vague absente")}")));

        string ObjectiveSummary(EncounterScenarioProfile.WavePolicy wave)
        {
            if (wave.QuestId is null) return string.Empty;
            if (!quests.TryGetValue(wave.QuestId.Value, out var quest)) return ", quête enregistrée absente";
            var objective = wave.ObjectiveId;
            var objectiveLabel = HumanPolicyReferenceUx2LwH(objective);
            if (Guid.TryParse(objective, out var objectiveId))
                objectiveLabel = quest.Authoring?.Objectives.FirstOrDefault(item => item.Id == objectiveId)
                    is { } item
                    ? HumanPolicyReferenceUx2LwH(item.Label.Fallback)
                    : "objectif enregistré absent";
            return $", quête {HumanPolicyReferenceUx2LwH(quest.DisplayName)}, objectif {objectiveLabel}";
        }
    }

    private static bool ActivatorPoliciesEqualUx2LwH(
        EncounterScenarioProfile.ActivatorPolicy left,
        EncounterScenarioProfile.ActivatorPolicy right) =>
        left.Id == right.Id && left.PlayersOnly == right.PlayersOnly &&
        left.IgnoreSummons == right.IgnoreSummons &&
        left.IgnoreCompanions == right.IgnoreCompanions &&
        left.IgnoreGameMasters == right.IgnoreGameMasters &&
        left.IgnoreSpectators == right.IgnoreSpectators &&
        left.RequiredAuthority == right.RequiredAuthority &&
        left.RequiredTags.SequenceEqual(right.RequiredTags, StringComparer.Ordinal);

    private static bool PhasePoliciesEqualUx2LwH(
        IReadOnlyList<EncounterScenarioProfile.PhasePolicy> left,
        IReadOnlyList<EncounterScenarioProfile.PhasePolicy> right) =>
        left.Count == right.Count && left.Zip(right).All(pair =>
            pair.First.Id == pair.Second.Id && pair.First.Order == pair.Second.Order &&
            pair.First.DisplayName == pair.Second.DisplayName &&
            pair.First.EntryConditions.SequenceEqual(pair.Second.EntryConditions, StringComparer.Ordinal) &&
            pair.First.ReinforcementEncounterIds.SequenceEqual(pair.Second.ReinforcementEncounterIds));

    private static bool WavePoliciesEqualUx2LwH(
        IReadOnlyList<EncounterScenarioProfile.PhasePolicy> left,
        IReadOnlyList<EncounterScenarioProfile.PhasePolicy> right)
    {
        if (left.Count != right.Count) return false;
        for (var phaseIndex = 0; phaseIndex < left.Count; phaseIndex++)
        {
            var leftWaves = left[phaseIndex].Waves;
            var rightWaves = right[phaseIndex].Waves;
            if (leftWaves.Count != rightWaves.Count) return false;
            for (var waveIndex = 0; waveIndex < leftWaves.Count; waveIndex++)
            {
                var first = leftWaves[waveIndex];
                var second = rightWaves[waveIndex];
                if (first.Id != second.Id || first.Order != second.Order ||
                    !first.GroupIds.SequenceEqual(second.GroupIds) ||
                    first.DelaySeconds != second.DelaySeconds || first.Completion != second.Completion ||
                    first.CompletionTargetId != second.CompletionTargetId || first.QuestId != second.QuestId ||
                    first.ObjectiveId != second.ObjectiveId || first.VoiceCueId != second.VoiceCueId ||
                    first.NextWaveId != second.NextWaveId)
                    return false;
            }
        }
        return true;
    }

    private static string ScriptStateUx2LwH(string? scriptId) => scriptId is null
        ? "sans script"
        : $"script {HumanPolicyReferenceUx2LwH(HumanScriptNameUx2LwF(scriptId))}";

    internal static string HumanEncounterScenarioDiagnosticUx2LwH(string? code) => code switch
    {
        "world.encounter.foundation_required" or "world.encounter.foundation_missing" =>
            "Terminez d’abord la fiche Foundation et composition dans LW-F.",
        "world.encounter.spawn_distribution_invalid" => "Choisissez une répartition prise en charge.",
        "world.encounter.spawn_anchor_policy_invalid" or "world.encounter.spawn_anchor_reference_missing" =>
            "Les ancres doivent être réelles, bornées, capacitaires, navigables et avoir un seul fallback.",
        "world.encounter.activator_policy_invalid" =>
            "Corrigez l’autorité, les exclusions ou les tags requis de l’activateur.",
        "world.encounter.lifecycle_binding_invalid" or "world.encounter.lifecycle_script_missing" =>
            "Conservez les trois bindings versionnés et choisissez uniquement des ForgeScripts réels.",
        "world.encounter.phase_policy_invalid" or "world.encounter.wave_order_invalid" =>
            "Chaque phase et vague exige un ordre positif unique et une composition explicite.",
        "world.encounter.wave_composition_invalid" or "RPG_ENCOUNTER_WAVE_INVALID" =>
            "La vague doit référencer des groupes LW-F réels et un délai fini.",
        "world.encounter.wave_completion_invalid" or "world.encounter.wave_objective_reference_missing" =>
            "La complétion exige son délai, sa cible de binding ou sa quête et son objectif.",
        "world.encounter.wave_voice_reference_invalid" or "world.encounter.wave_voice_reference_missing" =>
            "Choisissez une adresse audio déjà enregistrée dans le projet.",
        "world.encounter.wave_transition_invalid" =>
            "Une transition doit pointer vers une vague ultérieure du scénario.",
        "command.revision_conflict" or "command.source_changed" =>
            "Le projet a changé. Rechargez la rencontre et recommencez l’aperçu.",
        _ => HumanEncounterDiagnosticUx2LwF(code),
    };

    private static string CommandDiagnosticPathUx2LwH(CommandDiagnostic diagnostic, Guid profileId)
    {
        var separator = diagnostic.Message.IndexOf(": ", StringComparison.Ordinal);
        return separator > 0
            ? diagnostic.Message[..separator]
            : $"module/worldFeatures/encounters/{profileId:D}/scenario";
    }

    internal static string HumanDistributionUx2LwH(EncounterSpawnDistribution value) => value switch
    {
        EncounterSpawnDistribution.Sequential => "répartition séquentielle",
        EncounterSpawnDistribution.Random => "répartition aléatoire déterministe",
        EncounterSpawnDistribution.Weighted => "répartition pondérée",
        EncounterSpawnDistribution.Even => "répartition équilibrée",
        _ => "répartition inconnue",
    };

    internal static string HumanAuthorityUx2LwH(EncounterNetworkAuthority value) => value switch
    {
        EncounterNetworkAuthority.Local => "locale",
        EncounterNetworkAuthority.Host => "hôte",
        EncounterNetworkAuthority.Server => "serveur",
        _ => "inconnue",
    };

    internal static string HumanCompletionUx2LwH(EncounterWaveCompletionPolicy value) => value switch
    {
        EncounterWaveCompletionPolicy.AllDefeated => "tous vaincus",
        EncounterWaveCompletionPolicy.ObjectiveCompleted => "objectif accompli",
        EncounterWaveCompletionPolicy.TimerElapsed => "délai écoulé",
        EncounterWaveCompletionPolicy.Script => "binding script",
        _ => "complétion inconnue",
    };

    internal static string HumanVisibilityUx2LwH(EncounterWorldProfile.VisibilityMode value) => value switch
    {
        EncounterWorldProfile.VisibilityMode.Hidden => "cachée",
        EncounterWorldProfile.VisibilityMode.BehindCover => "derrière un couvert",
        EncounterWorldProfile.VisibilityMode.Portal => "portail",
        EncounterWorldProfile.VisibilityMode.Effect => "effet",
        EncounterWorldProfile.VisibilityMode.InViewAllowed => "visible autorisée",
        _ => "inconnue",
    };

    internal static string HumanPolicyReferenceUx2LwH(string? value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "sans référence" : value.Trim();
        if (Guid.TryParse(text, out _)) return "référence protégée";
        var tokens = text.Split([' ', ':', '/', '\\', ',', ';', '='],
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', tokens.Select(token => Guid.TryParse(token, out _)
            ? "référence protégée"
            : token.Replace('-', ' ').Replace('_', ' ')));
    }

    private static bool IsFoundationCompleteUx2LwH(EncounterWorldProfile? profile) =>
        profile?.Foundation is not null && profile.Composition is not null;

    private static Forge.RpgKit.Domain.Encounters.NetworkAuthority MapAuthorityUx2LwH(
        EncounterNetworkAuthority value) => value switch
        {
            EncounterNetworkAuthority.Local => Forge.RpgKit.Domain.Encounters.NetworkAuthority.Local,
            EncounterNetworkAuthority.Host => Forge.RpgKit.Domain.Encounters.NetworkAuthority.Host,
            EncounterNetworkAuthority.Server => Forge.RpgKit.Domain.Encounters.NetworkAuthority.Server,
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private static string EncounterScenarioDraftFingerprintUx2LwH(StudioUx2LwHDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteField("format", "ux2-lw-h-draft-v1");
        WriteField("profile-id", draft.ProfileId.ToString("D"));
        WriteField("is-new", draft.IsNew ? "1" : "0");
        WriteField("base-revision", draft.BaseRevision.Value.ToString(CultureInfo.InvariantCulture));
        WriteField("source-fingerprint", draft.SourceFingerprint);
        WriteField("scenario-id", draft.ScenarioId.ToString("D"));
        WriteField("provenance", draft.Provenance);
        WriteField("distribution", ((int)draft.Distribution).ToString(CultureInfo.InvariantCulture));
        WriteField("anchor-count", draft.SpawnAnchors.Count.ToString(CultureInfo.InvariantCulture));
        for (var anchorIndex = 0; anchorIndex < draft.SpawnAnchors.Count; anchorIndex++)
        {
            var prefix = $"anchor[{anchorIndex}]";
            var anchor = draft.SpawnAnchors[anchorIndex];
            WriteField($"{prefix}.id", anchor.Id.ToString("D"));
            WriteField($"{prefix}.reference", anchor.AnchorId);
            WriteTransform(prefix, anchor.Transform);
            WriteField($"{prefix}.capacity", anchor.Capacity.ToString(CultureInfo.InvariantCulture));
            WriteField($"{prefix}.navigable", anchor.Navigable ? "1" : "0");
            WriteField($"{prefix}.visibility", ((int)anchor.Visibility).ToString(CultureInfo.InvariantCulture));
            WriteField($"{prefix}.fallback", anchor.IsFallback ? "1" : "0");
            WriteField($"{prefix}.weight", Number(anchor.Weight));
        }
        var activator = draft.Activator;
        WriteField("activator.id", activator.Id.ToString("D"));
        WriteField("activator.players-only", activator.PlayersOnly ? "1" : "0");
        WriteField("activator.ignore-summons", activator.IgnoreSummons ? "1" : "0");
        WriteField("activator.ignore-companions", activator.IgnoreCompanions ? "1" : "0");
        WriteField("activator.ignore-game-masters", activator.IgnoreGameMasters ? "1" : "0");
        WriteField("activator.ignore-spectators", activator.IgnoreSpectators ? "1" : "0");
        WriteField("activator.authority", ((int)activator.RequiredAuthority).ToString(CultureInfo.InvariantCulture));
        WriteField("activator.tag-count", activator.RequiredTags.Count.ToString(CultureInfo.InvariantCulture));
        for (var tagIndex = 0; tagIndex < activator.RequiredTags.Count; tagIndex++)
            WriteField($"activator.tag[{tagIndex}]", activator.RequiredTags[tagIndex]);
        var lifecycle = draft.Lifecycle;
        WriteField("lifecycle.id", lifecycle.Id.ToString("D"));
        WriteField("lifecycle.api", lifecycle.ForgeScriptApiVersion);
        WriteBinding("lifecycle.entered", lifecycle.Entered);
        WriteBinding("lifecycle.exited", lifecycle.Exited);
        WriteBinding("lifecycle.exhausted", lifecycle.Exhausted);
        WriteField("phase-count", draft.Phases.Count.ToString(CultureInfo.InvariantCulture));
        for (var phaseIndex = 0; phaseIndex < draft.Phases.Count; phaseIndex++)
        {
            var phasePrefix = $"phase[{phaseIndex}]";
            var phase = draft.Phases[phaseIndex];
            WriteField($"{phasePrefix}.id", phase.Id.ToString("D"));
            WriteField($"{phasePrefix}.order", phase.Order.ToString(CultureInfo.InvariantCulture));
            WriteField($"{phasePrefix}.name", phase.DisplayName);
            WriteField($"{phasePrefix}.condition-count", phase.EntryConditions.Count.ToString(CultureInfo.InvariantCulture));
            for (var conditionIndex = 0; conditionIndex < phase.EntryConditions.Count; conditionIndex++)
                WriteField($"{phasePrefix}.condition[{conditionIndex}]", phase.EntryConditions[conditionIndex]);
            WriteField($"{phasePrefix}.reinforcement-count",
                phase.ReinforcementEncounterIds.Count.ToString(CultureInfo.InvariantCulture));
            for (var reinforcementIndex = 0;
                 reinforcementIndex < phase.ReinforcementEncounterIds.Count;
                 reinforcementIndex++)
                WriteField($"{phasePrefix}.reinforcement[{reinforcementIndex}]",
                    phase.ReinforcementEncounterIds[reinforcementIndex].ToString("D"));
            WriteField($"{phasePrefix}.wave-count", phase.Waves.Count.ToString(CultureInfo.InvariantCulture));
            for (var waveIndex = 0; waveIndex < phase.Waves.Count; waveIndex++)
            {
                var wavePrefix = $"{phasePrefix}.wave[{waveIndex}]";
                var wave = phase.Waves[waveIndex];
                WriteField($"{wavePrefix}.id", wave.Id.ToString("D"));
                WriteField($"{wavePrefix}.order", wave.Order.ToString(CultureInfo.InvariantCulture));
                WriteField($"{wavePrefix}.group-count", wave.GroupIds.Count.ToString(CultureInfo.InvariantCulture));
                for (var groupIndex = 0; groupIndex < wave.GroupIds.Count; groupIndex++)
                    WriteField($"{wavePrefix}.group[{groupIndex}]", wave.GroupIds[groupIndex].ToString("D"));
                WriteField($"{wavePrefix}.delay", Number(wave.DelaySeconds));
                WriteField($"{wavePrefix}.completion",
                    ((int)wave.Completion).ToString(CultureInfo.InvariantCulture));
                WriteField($"{wavePrefix}.completion-target", wave.CompletionTargetId);
                WriteField($"{wavePrefix}.quest", wave.QuestId?.ToString("D"));
                WriteField($"{wavePrefix}.objective", wave.ObjectiveId);
                WriteField($"{wavePrefix}.voice", wave.VoiceCueId);
                WriteField($"{wavePrefix}.next-wave", wave.NextWaveId?.ToString("D"));
            }
        }
        var overrides = draft.Overrides.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
        WriteField("override-count", overrides.Length.ToString(CultureInfo.InvariantCulture));
        for (var overrideIndex = 0; overrideIndex < overrides.Length; overrideIndex++)
        {
            WriteField($"override[{overrideIndex}].key", overrides[overrideIndex].Key);
            WriteField($"override[{overrideIndex}].value", overrides[overrideIndex].Value);
        }
        WriteField("required-at-runtime", draft.RequiredAtRuntime ? "1" : "0");
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));

        void WriteBinding(string prefix, EncounterScenarioProfile.EventBinding binding)
        {
            WriteField($"{prefix}.id", binding.Id.ToString("D"));
            WriteField($"{prefix}.binding", binding.Binding);
            WriteField($"{prefix}.script", binding.ScriptId);
        }
        void WriteTransform(string prefix, TransformValue transform)
        {
            WriteField($"{prefix}.position.x", Number(transform.Position.X));
            WriteField($"{prefix}.position.y", Number(transform.Position.Y));
            WriteField($"{prefix}.position.z", Number(transform.Position.Z));
            WriteField($"{prefix}.rotation.x", Number(transform.Rotation.X));
            WriteField($"{prefix}.rotation.y", Number(transform.Rotation.Y));
            WriteField($"{prefix}.rotation.z", Number(transform.Rotation.Z));
            WriteField($"{prefix}.rotation.w", Number(transform.Rotation.W));
            WriteField($"{prefix}.scale.x", Number(transform.Scale.X));
            WriteField($"{prefix}.scale.y", Number(transform.Scale.Y));
            WriteField($"{prefix}.scale.z", Number(transform.Scale.Z));
        }
        void WriteField(string name, string? value)
        {
            Write(name);
            Write(value);
        }
        void Write(string? value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
        static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }

    internal StudioUx2LwHSmokeResult RunUx2LwHContractSmoke()
    {
        var fixture = CreateUx2LwHSmokeProject();
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-lw-h-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(tempRoot, fixture.Project).Succeeded ||
                !WriteUx2LwFEvidenceScript(tempRoot) || !editing.Open(tempRoot).Succeeded)
                return FailedUx2LwHSmoke();
            var loadedProject = editing.Project!;
            var initialCatalog = loadedProject.Module.WorldFeatures;
            var loadedTarget = initialCatalog.Encounters.Single(item => item.Id == fixture.TargetId);
            var loadedNeighbor = initialCatalog.Encounters.Single(item => item.Id == fixture.NeighborId);
            var draft = editing.CreateEncounterScenarioDraftUx2LwH(fixture.TargetId);
            var explicitSelection = editing.CreateEncounterScenarioDraftUx2LwH(null) is null && draft is not null;
            var explicitCreation = editing.CreateNewEncounterScenarioDraftUx2LwH(fixture.NeighborId, null, null) is null;
            var foundationRequired = !IsFoundationCompleteUx2LwH(loadedTarget with { Foundation = null }) &&
                                     !IsFoundationCompleteUx2LwH(loadedTarget with { Composition = null });
            if (draft is null) return FailedUx2LwHSmoke();
            var invalidAnchor = editing.PreviewEncounterScenarioUx2LwH(draft with
            {
                SpawnAnchors = [draft.SpawnAnchors[0] with { Capacity = 0 }],
            });
            var invalidActivator = editing.PreviewEncounterScenarioUx2LwH(draft with
            {
                Activator = draft.Activator with { RequiredAuthority = (EncounterNetworkAuthority)999 },
            });
            var invalidLifecycle = editing.PreviewEncounterScenarioUx2LwH(draft with
            {
                Lifecycle = draft.Lifecycle with { ForgeScriptApiVersion = "latest" },
            });
            var invalidScript = editing.PreviewEncounterScenarioUx2LwH(draft with
            {
                Lifecycle = draft.Lifecycle with
                { Entered = draft.Lifecycle.Entered with { ScriptId = "scripts/absent.forge" } },
            });
            var invalidWave = editing.PreviewEncounterScenarioUx2LwH(draft with
            {
                Phases = [draft.Phases[0] with
                { Waves = [draft.Phases[0].Waves[0] with { GroupIds = [] }] }],
            });
            var sourceAnchor = draft.SpawnAnchors[0];
            var anchorOnlyPreview = editing.PreviewEncounterScenarioUx2LwH(draft with
            {
                SpawnAnchors = draft.SpawnAnchors.Select((anchor, index) => index == 0
                    ? sourceAnchor with
                    {
                        Transform = new(
                            new(sourceAnchor.Transform.Position.X + 1, sourceAnchor.Transform.Position.Y,
                                sourceAnchor.Transform.Position.Z),
                            sourceAnchor.Transform.Rotation, sourceAnchor.Transform.Scale),
                    }
                    : anchor).ToArray(),
            });
            var activatorOnlyPreview = editing.PreviewEncounterScenarioUx2LwH(draft with
            {
                Activator = draft.Activator with
                { RequiredTags = [.. draft.Activator.RequiredTags, "studio-only"] },
            });
            var realScript = editing.EncounterScriptChoicesUx2LwH().First().Id;
            var lifecycleOnlyPreview = editing.PreviewEncounterScenarioUx2LwH(draft with
            {
                Lifecycle = draft.Lifecycle with
                { Entered = draft.Lifecycle.Entered with { ScriptId = realScript } },
            });
            var phaseOnlyPreview = editing.PreviewEncounterScenarioUx2LwH(draft with
            {
                Phases = draft.Phases.Select((phase, index) => index == 0
                    ? phase with { DisplayName = phase.DisplayName + " bis" }
                    : phase).ToArray(),
            });
            var waveOnlyPreview = editing.PreviewEncounterScenarioUx2LwH(draft with
            {
                Phases = draft.Phases.Select((phase, phaseIndex) => phaseIndex == 0
                    ? phase with
                    {
                        Waves = phase.Waves.Select((wave, waveIndex) => waveIndex == 0
                        ? wave with { DelaySeconds = wave.DelaySeconds + .5 }
                        : wave).ToArray()
                    }
                    : phase).ToArray(),
            });
            var anchorValidation = !invalidAnchor.Succeeded && invalidAnchor.Diagnostics.Any(item =>
                item.Code == "world.encounter.spawn_anchor_policy_invalid") &&
                anchorOnlyPreview is { Succeeded: true, CanApply: true } &&
                anchorOnlyPreview.Diff.Any(item => item.Section == "Points de spawn manuels" && item.Changed);
            var activatorValidation = !invalidActivator.Succeeded && invalidActivator.Diagnostics.Any(item =>
                item.Code == "world.encounter.activator_policy_invalid") &&
                activatorOnlyPreview is { Succeeded: true, CanApply: true } &&
                activatorOnlyPreview.Diff.Any(item => item.Section == "Restriction joueurs" && item.Changed);
            var lifecycleValidation = !invalidLifecycle.Succeeded && invalidLifecycle.Diagnostics.Any(item =>
                item.Code == "world.encounter.lifecycle_binding_invalid") &&
                lifecycleOnlyPreview is { Succeeded: true, CanApply: true } &&
                lifecycleOnlyPreview.Diff.Any(item =>
                    item.Section == "OnEnter, OnExit et OnExhausted" && item.Changed);
            var realScriptValidation = !invalidScript.Succeeded && invalidScript.Diagnostics.Any(item =>
                item.Code == "world.encounter.lifecycle_script_missing") &&
                editing.EncounterScriptChoicesUx2LwH().Any(item => item.Id == "scripts/encounter_activate.forge");
            var waveValidation = !invalidWave.Succeeded && invalidWave.Diagnostics.Any(item =>
                item.Code == "world.encounter.wave_composition_invalid") &&
                phaseOnlyPreview is { Succeeded: true, CanApply: true } &&
                phaseOnlyPreview.Diff.Any(item => item.Section == "Phases et vagues" && item.Changed) &&
                waveOnlyPreview is { Succeeded: true, CanApply: true } &&
                waveOnlyPreview.Diff.Any(item => item.Section == "Actions de vague" && item.Changed);

            var staleDraft = editing.CreateEncounterScenarioDraftUx2LwH(fixture.TargetId)!;
            var changedBeforePreview = ApplyUx2LwFIntermediateMutation(editing, fixture.NeighborId, 31);
            var stalePreview = editing.PreviewEncounterScenarioUx2LwH(staleDraft);
            var staleDraftBlocked = changedBeforePreview && !stalePreview.Succeeded &&
                                    stalePreview.Message.Contains("changé", StringComparison.OrdinalIgnoreCase) &&
                                    editing.Undo().Succeeded;

            var changedDraft = ChangedScenarioDraftUx2LwH(
                editing.CreateEncounterScenarioDraftUx2LwH(fixture.TargetId)!);
            var approval = editing.PreviewEncounterScenarioUx2LwH(changedDraft);
            var changedAfterPreview = ApplyUx2LwFIntermediateMutation(editing, fixture.NeighborId, 32);
            var staleApply = editing.ApplyApprovedEncounterScenarioUx2LwH();
            var staleApprovalBlocked = approval.Succeeded && changedAfterPreview && !staleApply.Succeeded &&
                                       staleApply.Message.Contains("Prévisualisez", StringComparison.Ordinal) &&
                                       editing.Undo().Succeeded;

            changedDraft = ChangedScenarioDraftUx2LwH(
                editing.CreateEncounterScenarioDraftUx2LwH(fixture.TargetId)!);
            var historyBefore = editing.History!;
            var projectBefore = editing.Project;
            var revisionBefore = historyBefore.State.Revision;
            var journalBefore = historyBefore.Journal.ToArray();
            var dirtyBefore = editing.IsDirty;
            var preview = editing.PreviewEncounterScenarioUx2LwH(changedDraft);
            var repeat = editing.PreviewEncounterScenarioUx2LwH(changedDraft);
            var dryRunPure = preview.Succeeded && repeat.Succeeded && preview.DryRunPure &&
                             ReferenceEquals(projectBefore, editing.Project) &&
                             ReferenceEquals(historyBefore, editing.History) &&
                             historyBefore.State.Revision == revisionBefore &&
                             historyBefore.Journal.SequenceEqual(journalBefore) && editing.IsDirty == dirtyBefore;
            var deterministicPreview = preview.Runtime.Anchors == repeat.Runtime.Anchors &&
                                       preview.Runtime.Waves == repeat.Runtime.Waves &&
                                       preview.Runtime.Boundary.Contains("aucun gizmo", StringComparison.Ordinal);
            var applied = editing.ApplyApprovedEncounterScenarioUx2LwH();
            if (!applied.Succeeded) return FailedUx2LwHSmoke();
            var appliedCatalog = editing.Project!.Module.WorldFeatures;
            var appliedTarget = appliedCatalog.Encounters.Single(item => item.Id == fixture.TargetId);
            var appliedNeighbor = appliedCatalog.Encounters.Single(item => item.Id == fixture.NeighborId);
            var targetNeighborsPreserved = loadedTarget.Id == appliedTarget.Id &&
                loadedTarget.SchemaVersion == appliedTarget.SchemaVersion &&
                loadedTarget.Provenance == appliedTarget.Provenance &&
                loadedTarget.AreaId == appliedTarget.AreaId &&
                loadedTarget.SpawnFactionId == appliedTarget.SpawnFactionId &&
                ReferenceEquals(loadedTarget.Foundation, appliedTarget.Foundation) &&
                ReferenceEquals(loadedTarget.Composition, appliedTarget.Composition) &&
                loadedTarget.CompositionRequiredAtRuntime == appliedTarget.CompositionRequiredAtRuntime &&
                ReferenceEquals(loadedTarget.Respawn, appliedTarget.Respawn) &&
                ReferenceEquals(loadedTarget.Scheduler, appliedTarget.Scheduler) &&
                ReferenceEquals(loadedTarget.Reinforcements, appliedTarget.Reinforcements) &&
                ReferenceEquals(loadedTarget.Visibility, appliedTarget.Visibility) &&
                ReferenceEquals(loadedTarget.Cleanup, appliedTarget.Cleanup) &&
                loadedTarget.ScenarioRequiredAtRuntime == appliedTarget.ScenarioRequiredAtRuntime &&
                ReferenceEquals(loadedTarget.Statistics, appliedTarget.Statistics) &&
                loadedTarget.StatisticsRequiredAtRuntime == appliedTarget.StatisticsRequiredAtRuntime;
            var projectNeighborsPreserved = ReferenceEquals(loadedNeighbor, appliedNeighbor) &&
                loadedProject.Id == editing.Project.Id && loadedProject.Title == editing.Project.Title &&
                loadedProject.Module.Id == editing.Project.Module.Id &&
                loadedProject.Module.Slug == editing.Project.Module.Slug &&
                loadedProject.Module.DisplayName == editing.Project.Module.DisplayName &&
                loadedProject.Module.Description == editing.Project.Module.Description &&
                loadedProject.Module.EntryAreaId == editing.Project.Module.EntryAreaId &&
                ReferenceEquals(loadedProject.Module.Metadata, editing.Project.Module.Metadata) &&
                loadedProject.Module.Variables.SequenceEqual(editing.Project.Module.Variables) &&
                ReferenceEquals(loadedProject.Module.Distribution, editing.Project.Module.Distribution) &&
                ReferenceEquals(loadedProject.Module.Localization, editing.Project.Module.Localization) &&
                ReferenceEquals(loadedProject.Module.Content, editing.Project.Module.Content) &&
                ReferenceEquals(loadedProject.Module.ArtisticStyle, editing.Project.Module.ArtisticStyle) &&
                ReferenceEquals(loadedProject.Module.RuntimePolicies, editing.Project.Module.RuntimePolicies) &&
                ReferenceEquals(loadedProject.Module.Factions, editing.Project.Module.Factions) &&
                loadedProject.Blueprints.SequenceEqual(editing.Project.Blueprints) &&
                loadedProject.Areas.SequenceEqual(editing.Project.Areas) &&
                loadedProject.Dialogues.SequenceEqual(editing.Project.Dialogues) &&
                loadedProject.Quests.SequenceEqual(editing.Project.Quests);
            var collectionsPreserved = SameReferencesUx2LwF(initialCatalog.Placeables, appliedCatalog.Placeables) &&
                SameReferencesUx2LwF(initialCatalog.Doors, appliedCatalog.Doors) &&
                SameReferencesUx2LwF(initialCatalog.Triggers, appliedCatalog.Triggers) &&
                SameReferencesUx2LwF(initialCatalog.VehicleRoutes, appliedCatalog.VehicleRoutes) &&
                SameReferencesUx2LwF(initialCatalog.Merchants, appliedCatalog.Merchants) &&
                SameReferencesUx2LwF(initialCatalog.Companions, appliedCatalog.Companions) &&
                ReferenceEquals(initialCatalog.Scripts, appliedCatalog.Scripts) &&
                appliedCatalog.Encounters.Select(item => item.Id).SequenceEqual(
                    initialCatalog.Encounters.Select(item => item.Id));

            var undo = editing.Undo();
            var undone = editing.Project!.Module.WorldFeatures.Encounters.Single(item => item.Id == fixture.TargetId);
            var redo = editing.Redo();
            var redone = editing.Project!.Module.WorldFeatures.Encounters.Single(item => item.Id == fixture.TargetId);
            var targetUndoRedo = undo.Succeeded && redo.Succeeded &&
                EncounterProfileJsonUx2LwF(undone, editing.Project!) ==
                EncounterProfileJsonUx2LwF(loadedTarget, loadedProject) &&
                EncounterProfileJsonUx2LwF(redone, editing.Project!) ==
                EncounterProfileJsonUx2LwF(appliedTarget, editing.Project!);

            var anchor = editing.EncounterAnchorChoicesUx2LwH(fixture.NeighborId).FirstOrDefault();
            var group = editing.EncounterGroupChoicesUx2LwH(fixture.NeighborId).FirstOrDefault();
            var newDraft = editing.CreateNewEncounterScenarioDraftUx2LwH(fixture.NeighborId,
                anchor?.Id, group?.Id);
            if (newDraft is null) return FailedUx2LwHSmoke();
            var newPreview = editing.PreviewEncounterScenarioUx2LwH(newDraft);
            var newApply = editing.ApplyApprovedEncounterScenarioUx2LwH();
            var newUndo = editing.Undo();
            var newUndoneNeighbor = editing.Project!.Module.WorldFeatures.Encounters.Single(item =>
                item.Id == fixture.NeighborId);
            var newRedo = editing.Redo();
            var createdNeighbor = editing.Project!.Module.WorldFeatures.Encounters.Single(item =>
                item.Id == fixture.NeighborId);
            explicitCreation = explicitCreation && newPreview.Succeeded && newApply.Succeeded &&
                               newUndo.Succeeded && newRedo.Succeeded && createdNeighbor.Scenario is not null &&
                               createdNeighbor.ScenarioRequiredAtRuntime && newUndoneNeighbor.Scenario is null;
            var saved = editing.Save();
            var opened = saved.Succeeded && reopened.Open(tempRoot).Succeeded;
            var persistence = opened && ProjectJsonSerializer.Serialize(editing.Project!) ==
                              ProjectJsonSerializer.Serialize(reopened.Project!) &&
                              EncounterProfileJsonUx2LwF(reopened.Project!.Module.WorldFeatures.Encounters.Single(item =>
                                      item.Id == fixture.TargetId), reopened.Project) ==
                              EncounterProfileJsonUx2LwF(appliedTarget, editing.Project!) &&
                              EncounterProfileJsonUx2LwF(reopened.Project.Module.WorldFeatures.Encounters.Single(item =>
                                      item.Id == fixture.NeighborId), reopened.Project) ==
                              EncounterProfileJsonUx2LwF(createdNeighbor, editing.Project!);
            reopened.Close();
            var undoRedo = targetUndoRedo && newUndo.Succeeded && newRedo.Succeeded;
            var canonicalCommand = editing.History!.Journal.Count >= 2 &&
                                   editing.History.Journal.All(item => item.CommandName == "set_world_feature_catalog");
            var structuredDiagnostics = new[]
                { invalidAnchor, invalidActivator, invalidLifecycle, invalidScript, invalidWave }
                .SelectMany(item => item.Diagnostics)
                .All(item => !string.IsNullOrWhiteSpace(item.Code) && !string.IsNullOrWhiteSpace(item.Path));
            var humanText = editing.EncounterChoicesUx2LwH.All(item => IsHumanUx2LwFText(item.Label)) &&
                            editing.EncounterAnchorChoicesUx2LwH(fixture.TargetId).All(item =>
                                IsHumanUx2LwFText(item.Label) && IsHumanUx2LwFText(item.Provenance));
            var succeeded = explicitSelection && explicitCreation && foundationRequired && anchorValidation &&
                            activatorValidation && lifecycleValidation && realScriptValidation && waveValidation &&
                            deterministicPreview && dryRunPure && staleDraftBlocked && staleApprovalBlocked &&
                            canonicalCommand && undoRedo && persistence && targetNeighborsPreserved &&
                            projectNeighborsPreserved && collectionsPreserved && structuredDiagnostics && humanText;
            return new(succeeded, explicitSelection, explicitCreation, foundationRequired, anchorValidation,
                activatorValidation, lifecycleValidation, realScriptValidation, waveValidation,
                deterministicPreview, dryRunPure, staleDraftBlocked, staleApprovalBlocked, canonicalCommand,
                undoRedo, persistence, targetNeighborsPreserved, projectNeighborsPreserved,
                collectionsPreserved, structuredDiagnostics, humanText);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_LW_H_CONTRACT_FAILED exception={exception.Message}");
            return FailedUx2LwHSmoke();
        }
        finally
        {
            reopened.Close(); editing.Close();
            try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static StudioUx2LwHDraft ChangedScenarioDraftUx2LwH(StudioUx2LwHDraft draft)
    {
        var lifecycle = draft.Lifecycle with
        {
            Entered = draft.Lifecycle.Entered with { ScriptId = "scripts/encounter_activate.forge" },
            Exited = draft.Lifecycle.Exited with { ScriptId = "scripts/encounter_activate.forge" },
            Exhausted = draft.Lifecycle.Exhausted with { ScriptId = "scripts/encounter_activate.forge" },
        };
        return draft with
        {
            SpawnAnchors = draft.SpawnAnchors.Select((item, index) => index == 0
                ? item with { Capacity = item.Capacity + 1, Weight = item.Weight + 1 }
                : item).ToArray(),
            Activator = draft.Activator with { IgnoreSpectators = !draft.Activator.IgnoreSpectators },
            Lifecycle = lifecycle,
            Phases = draft.Phases.Select((phase, phaseIndex) => phaseIndex == 0
                ? phase with
                {
                    DisplayName = phase.DisplayName + " révisée",
                    EntryConditions = [.. phase.EntryConditions, "studio.preview"],
                    Waves = phase.Waves.Select((wave, waveIndex) => waveIndex == 0
                        ? wave with { DelaySeconds = wave.DelaySeconds + 1 }
                        : wave).ToArray(),
                }
                : phase).ToArray(),
        };
    }

    private static StudioUx2LwHSmokeResult FailedUx2LwHSmoke() => new(
        false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false);

    private static Ux2LwFSmokeFixture CreateUx2LwHSmokeProject()
    {
        var fixture = CreateUx2LwFSmokeProject();
        var project = fixture.Project;
        var source = project.Module.WorldFeatures;
        var target = source.Encounters.Single(item => item.Id == fixture.TargetId);
        var scenario = target.Scenario!;
        var phase = scenario.Phases.Single();
        var firstWave = phase.Waves.Single();
        var secondWaveId = StableUx2LwF(target.Id, "ux2-lw-h:smoke-wave:2");
        var enrichedScenario = scenario with
        {
            SpawnAnchors = scenario.SpawnAnchors.Select((anchor, index) => index == 0
                ? anchor with
                {
                    Transform = new(new(2.5f, 0, -3.5f), QuaternionValue.Identity,
                        new(1.25f, 1, .75f)),
                    Capacity = 7,
                    Visibility = EncounterWorldProfile.VisibilityMode.Effect,
                    Weight = 2.5,
                }
                : anchor).ToArray(),
            Distribution = EncounterSpawnDistribution.Even,
            Activator = scenario.Activator with
            {
                IgnoreCompanions = false,
                IgnoreGameMasters = true,
                RequiredAuthority = EncounterNetworkAuthority.Host,
                RequiredTags = ["party", "area-ready"],
            },
            Phases =
            [
                phase with
                {
                    EntryConditions = ["area.ready", "party.ready"],
                    Waves =
                    [
                        firstWave with
                        {
                            DelaySeconds = 1,
                            Completion = EncounterWaveCompletionPolicy.Script,
                            CompletionTargetId = "EncounterWaveCompleted",
                            VoiceCueId = "audio:encounters/wave-one",
                            NextWaveId = secondWaveId,
                        },
                        new(secondWaveId, 2, firstWave.GroupIds, 4,
                            EncounterWaveCompletionPolicy.TimerElapsed, null, null, null,
                            "audio:encounters/wave-two", null),
                    ],
                },
            ],
        };
        var enrichedTarget = target with { Scenario = enrichedScenario, ScenarioRequiredAtRuntime = true };
        var encounters = source.Encounters.Select(item => item.Id == target.Id ? enrichedTarget : item).ToArray();
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, source.Triggers, encounters, source.VehicleRoutes,
            source.Merchants, source.Companions, source.Scripts);
        var enrichedProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(catalog), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests);
        return fixture with { Project = enrichedProject };
    }
}
