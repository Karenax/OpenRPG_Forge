using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Assistant;
using Forge.Application.Commands;
using Forge.Application.Dialogues;
using Forge.Application.Localization;
using Forge.Application.Projects;
using Forge.Application.Quality;
using Forge.Application.Quests;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Assistant;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Localization;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetDialogueHandler _setDialogueUx5B = new(new SystemClock());
    private readonly SetQuestHandler _setQuestUx5B = new(new SystemClock());
    private readonly SetLocalizationCatalogHandler _setLocalizationUx5B = new(new SystemClock());
    private readonly SetCinematicProfilesHandler _setCinematicsUx5B = new(new SystemClock());
    private readonly SetLoreIndexHandler _setLoreUx5B = new(new SystemClock());
    private LoreIndexCommandHistory? _loreHistoryUx5B;

    private StudioDialogueProductionDraftUx5B? _approvedDialogueUx5B;
    private string? _approvedDialogueFingerprintUx5B;
    private StudioQuestProductionDraftUx5B? _approvedQuestUx5B;
    private string? _approvedQuestFingerprintUx5B;
    private StudioLocalizationProductionDraftUx5B? _approvedLocalizationUx5B;
    private string? _approvedLocalizationFingerprintUx5B;
    private StudioLoreProductionDraftUx5B? _approvedLoreUx5B;
    private string? _approvedLoreFingerprintUx5B;
    private StudioCinematicProductionDraftUx5B? _approvedCinematicUx5B;
    private string? _approvedCinematicFingerprintUx5B;

    private const string NarrativeBoundaryUx5B =
        "Aperçu déterministe en mémoire : aucun dialogue, son, caméra, acteur, réseau ou modèle IA n’est exécuté dans Studio.";

    internal IReadOnlyList<StudioNarrativeChoiceUx5B> DialogueProductionChoicesUx5B =>
        Project?.Dialogues.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioNarrativeChoiceUx5B(item.Id.Value,
                $"{item.DisplayName} · {item.Slug} · {(DialogueCompleteUx5B(item) ? "production complète" : "à compléter")}",
                DialogueCompleteUx5B(item))).ToArray() ?? [];

    internal IReadOnlyList<StudioNarrativeChoiceUx5B> QuestProductionChoicesUx5B =>
        Project?.Quests.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioNarrativeChoiceUx5B(item.Id.Value,
                $"{item.DisplayName} · {item.Slug} · {(QuestCompleteUx5B(item) ? "production complète" : "à compléter")}",
                QuestCompleteUx5B(item))).ToArray() ?? [];

    internal IReadOnlyList<StudioNarrativeChoiceUx5B> CinematicAreaChoicesUx5B =>
        Project?.Areas.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioNarrativeChoiceUx5B(item.Id.Value,
                $"{item.DisplayName} · {item.Slug}", item.RuntimePolicies.Cinematics.CameraAnchors.Count > 0))
            .ToArray() ?? [];

    internal IReadOnlyList<StudioNarrativeChoiceUx5B> LoreEntryChoicesUx5B
    {
        get
        {
            EnsureLoreHistoryUx5B();
            var entries = _loreHistoryUx5B?.State.Index.Entries
                .OrderBy(item => item.Kind).ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
                .Select(item => new StudioNarrativeChoiceUx5B(item.Id, $"{item.Title} · {item.Kind}",
                    item.SourceIds.Count > 0)).ToArray() ?? [];
            return entries.Length > 0
                ? entries
                : [new StudioNarrativeChoiceUx5B(Guid.Empty, "Créer l’index de lore du module", false)];
        }
    }

    internal StudioDialogueProductionDraftUx5B? CreateDialogueProductionDraftUx5B(Guid dialogueId)
    {
        var project = Project; var history = History;
        var source = project?.Dialogues.SingleOrDefault(item => item.Id.Value == dialogueId);
        if (project is null || history is null || source is null || source.Nodes.Count < 2 ||
            project.Blueprints.All(item => item.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn)))
            return null;
        var candidate = CompleteDialogueUx5B(project, source);
        return new(history.State.Revision, FingerprintProjectUx5B(project), source, candidate);
    }

    internal StudioDialogueProductionDraftUx5B ConfigureDialogueProductionDraftUx5B(
        StudioDialogueProductionDraftUx5B draft,
        string searchPattern,
        string memoryName,
        string skillId,
        double difficulty,
        string authorNote,
        string subtitle,
        float fieldOfView,
        int timeoutMilliseconds,
        string aiSynopsis)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var candidate = draft.Candidate;
        var authoring = candidate.Authoring!;
        var entryAuthoring = authoring.Nodes.Single(item => item.NodeId == candidate.EntryNodeId);
        authoring = authoring with
        {
            Nodes = authoring.Nodes.Select(item => item.NodeId == candidate.EntryNodeId
                ? item with { Notes = item.Notes with { Intent = RequiredUx5B(authorNote, "Intention auteur") } }
                : item).ToArray(),
        };
        var experience = candidate.Experience!;
        var memory = experience.MemorySlots[0] with { Name = IdentifierUx5B(memoryName, "memoire") };
        var check = experience.SkillChecks[0] with
        {
            SkillId = IdentifierUx5B(skillId, "insight"),
            Difficulty = difficulty,
        };
        experience = experience with
        {
            Search = experience.Search with { Pattern = RequiredUx5B(searchPattern, candidate.DisplayName) },
            MemorySlots = [memory, .. experience.MemorySlots.Skip(1)],
            SkillChecks = [check, .. experience.SkillChecks.Skip(1)],
        };
        var presentation = candidate.Presentation!;
        presentation = new DialoguePresentationProfile(presentation.Id, presentation.SchemaVersion,
            presentation.Provenance, presentation.RequiredAtRuntime, presentation.ServerAuthoritativeInterjections,
            presentation.AnimationCues,
            [presentation.VoiceCues[0] with
            {
                Subtitle = new LocalizedText(presentation.VoiceCues[0].Subtitle.Key,
                    RequiredUx5B(subtitle, presentation.VoiceCues[0].Subtitle.Fallback),
                    provenance: "studio:ux5-b"),
            }, .. presentation.VoiceCues.Skip(1)],
            [presentation.CameraShots[0] with { FieldOfViewDegrees = fieldOfView }, .. presentation.CameraShots.Skip(1)],
            presentation.Bookmarks, presentation.InterjectionRules,
            [presentation.TimedChoices[0] with { DurationMilliseconds = timeoutMilliseconds },
                .. presentation.TimedChoices.Skip(1)], presentation.VariantSets);
        var production = candidate.Production!;
        production = production with
        {
            AiPlans = [production.AiPlans[0] with { Synopsis = RequiredUx5B(aiSynopsis, "Proposition narrative contrôlée") },
                .. production.AiPlans.Skip(1)],
            Collaboration = production.Collaboration with
            {
                Comments = [production.Collaboration.Comments[0] with { Text = RequiredUx5B(authorNote, "Relecture") },
                    .. production.Collaboration.Comments.Skip(1)],
            },
        };
        candidate = RebuildDialogueUx5B(candidate, authoring, experience, presentation, production);
        return draft with { Candidate = candidate };
    }

    internal StudioQuestProductionDraftUx5B? CreateQuestProductionDraftUx5B(Guid questId)
    {
        var project = Project; var history = History;
        var source = project?.Quests.SingleOrDefault(item => item.Id.Value == questId);
        if (project is null || history is null || source is null || source.Transitions.Count == 0 ||
            project.Areas.Count == 0 || project.Dialogues.Count == 0 ||
            project.Blueprints.All(item => item.Kind != BlueprintKind.Creature)) return null;
        return new(history.State.Revision, FingerprintProjectUx5B(project), source,
            CompleteQuestUx5B(project, source));
    }

    internal StudioQuestProductionDraftUx5B ConfigureQuestProductionDraftUx5B(
        StudioQuestProductionDraftUx5B draft,
        string category,
        int priority,
        string objectiveText,
        double progressionReward,
        string authorNote,
        string notification,
        string aiSynopsis,
        int simulationSeed)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var source = draft.Candidate;
        var authoring = source.Authoring!;
        authoring = authoring with
        {
            Objectives = [authoring.Objectives[0] with
            {
                Label = new LocalizedText(authoring.Objectives[0].Label.Key,
                    RequiredUx5B(objectiveText, authoring.Objectives[0].Label.Fallback),
                    provenance: "studio:ux5-c"),
            }, .. authoring.Objectives.Skip(1)],
            Rewards = authoring.Rewards with { Progression = progressionReward },
            Notes = authoring.Notes with { Design = RequiredUx5B(authorNote, "Intention de quête") },
        };
        var operations = source.Operations! with
        {
            Journal = source.Operations.Journal with { Priority = priority },
            Notification = source.Operations.Notification with
            {
                Body = new LocalizedText(source.Operations.Notification.Body.Key,
                    RequiredUx5B(notification, source.Operations.Notification.Body.Fallback),
                    provenance: "studio:ux5-c"),
                AccessibleText = RequiredUx5B(notification, source.Operations.Notification.AccessibleText),
            },
        };
        var provisional = new QuestDefinition(source.Id, source.SchemaVersion, "studio:ux5-c", source.Slug,
            source.DisplayName, source.Title, source.Description, RequiredUx5B(category, "journal"), priority,
            source.Owner, source.RequiredAtRuntime, source.InstancePolicy, source.InitialStateId, source.States,
            source.Transitions, operations, source.Simulation, authoring, source.Experience, source.Quality);
        var dependencies = BuildQuestAiDependenciesUx5B(provisional);
        var plan = QuestQualityRuntimeService.CreateAiPlan(provisional,
            new LocalizedText($"quest.{source.Slug}.ai.synopsis", RequiredUx5B(aiSynopsis, "Proposition de quête contrôlée"),
                provenance: "studio:ux5-c"), dependencies,
            ["tracking/project/AURORA_PARITY_COVERAGE.csv#quest"], ["style:module"], "studio:author",
            StableGuidUx5B(source.Id.Value, "ux5-c:ai-transaction"),
            $"preview:quest:{source.Id}|states:{source.States.Count}|seed:{simulationSeed}");
        plan = QuestQualityRuntimeService.Approve(plan, "studio:author", DateTimeOffset.UnixEpoch);
        var quality = new QuestQualityProfile(source.Quality!.Id, 1, "studio:ux5-c", true, false,
            QuestQualityRuntimeService.GenerateTests(provisional, Math.Max(1, simulationSeed)), plan);
        var candidate = provisional.WithExperience(source.Experience).WithQuality(quality);
        return draft with { Candidate = candidate };
    }

    internal StudioLocalizationProductionDraftUx5B? CreateLocalizationProductionDraftUx5B()
    {
        var project = Project; var history = History;
        if (project is null || history is null) return null;
        var extracted = LocalizationCatalogExtractor.Extract(project).Catalog;
        var candidate = CompleteLocalizationUx5B(extracted);
        var workspace = WorkspaceProfile;
        var workspaceCandidate = CopyWorkspaceUx5B(workspace, workspace.PrimaryFontFamily, workspace.TextScale);
        return new(history.State.Revision, FingerprintProjectUx5B(project), project.Module.Localization,
            candidate, workspace, workspaceCandidate);
    }

    internal StudioLocalizationProductionDraftUx5B ConfigureLocalizationProductionDraftUx5B(
        StudioLocalizationProductionDraftUx5B draft,
        string locale,
        string key,
        string fallback,
        string translation,
        string fontFamily,
        double textScale)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var normalizedLocale = LocalizationCatalog.NormalizeLocale(locale);
        var messages = draft.Candidate.Messages.Where(item => !string.Equals(item.Key, key.Trim(), StringComparison.Ordinal))
            .Append(new LocalizationMessage(IdentifierUx5B(key, "audio.important.subtitle"),
                RequiredUx5B(fallback, "Son important"),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [normalizedLocale] = RequiredUx5B(translation, fallback),
                }, "studio:ux5-d"))
            .ToArray();
        var catalog = new LocalizationCatalog(draft.Candidate.Id, draft.Candidate.DefaultLocale,
            draft.Candidate.Locales.Append(normalizedLocale).ToArray(), messages,
            draft.Candidate.SchemaVersion, draft.Candidate.PackageNamespace, draft.Candidate.LicenseSpdx,
            "studio:ux5-d", draft.Candidate.PlatformByteBudget);
        return draft with
        {
            Candidate = catalog,
            WorkspaceCandidate = CopyWorkspaceUx5B(draft.WorkspaceCandidate,
                RequiredUx5B(fontFamily, draft.WorkspaceCandidate.PrimaryFontFamily), textScale),
        };
    }

    internal StudioLoreProductionDraftUx5B? CreateLoreProductionDraftUx5B(Guid? entryId = null)
    {
        var project = Project;
        if (project is null) return null;
        EnsureLoreHistoryUx5B();
        var history = _loreHistoryUx5B!;
        var candidate = CompleteLoreUx5B(project, history.State.Index, entryId);
        return new(history.State.Revision, FingerprintProjectUx5B(project), history.State.Index, candidate,
            StableGuidUx5B(project.Id.Value, $"ux5-e:transaction:{history.State.Revision.Value}"),
            StableGuidUx5B(project.Id.Value, $"ux5-e:plan:{history.State.Revision.Value}"));
    }

    internal StudioLoreProductionDraftUx5B ConfigureLoreProductionDraftUx5B(
        StudioLoreProductionDraftUx5B draft,
        LoreEntryKind kind,
        string key,
        string title,
        string content,
        string tags,
        string query)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var entry = draft.Candidate.Entries[0];
        var updated = new LoreEntryDefinition(entry.Id, 1, kind, IdentifierUx5B(key, "module-lore"),
            RequiredUx5B(title, "Lore du module"), RequiredUx5B(content, "Contexte narratif gouverné."),
            entry.SourceIds, entry.RelatedEntryIds, SplitValuesUx5B(tags), "studio:ux5-e");
        var entries = draft.Candidate.Entries.Select(item => item.Id == entry.Id ? updated : item).ToArray();
        var candidate = new LoreIndexDefinition(draft.Candidate.Id, 1, true,
            Math.Max(2_000, draft.Candidate.MaximumContextCharacters), entries,
            $"studio:ux5-e:query:{IdentifierUx5B(query, "module")}");
        return draft with { Candidate = candidate };
    }

    internal StudioCinematicProductionDraftUx5B? CreateCinematicProductionDraftUx5B(Guid areaId)
    {
        var project = Project; var history = History;
        var area = project?.Areas.SingleOrDefault(item => item.Id.Value == areaId);
        if (project is null || history is null || area is null || project.Dialogues.Count == 0) return null;
        var (module, areaProfile) = CompleteCinematicsUx5B(project, area);
        return new(history.State.Revision, FingerprintProjectUx5B(project), area.Id,
            project.Module.RuntimePolicies.Cinematics, area.RuntimePolicies.Cinematics, module, areaProfile);
    }

    internal StudioCinematicProductionDraftUx5B ConfigureCinematicProductionDraftUx5B(
        StudioCinematicProductionDraftUx5B draft,
        string displayName,
        int durationMilliseconds,
        bool skippable,
        bool synchronized,
        bool startupEnabled,
        string lockSignal)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var sequenceId = StableGuidUx5B(draft.ModuleCandidate.Id, "ux5-f:sequence");
        var sequence = draft.ModuleCandidate.Sequences.Single(item => item.Id == sequenceId);
        var stateTrack = sequence.Tracks.First(item => item.Kind == SequenceTrackKind.State);
        var firstKeyframe = stateTrack.Keyframes[0];
        stateTrack = new SequenceTrackDefinition(stateTrack.Id, stateTrack.Name, stateTrack.Kind,
            stateTrack.TargetKind, stateTrack.TargetId, stateTrack.ResourcePath,
            [new SequenceKeyframe(firstKeyframe.Id, firstKeyframe.TimeMilliseconds, firstKeyframe.Transform,
                 RequiredUx5B(lockSignal, "controls.locked=true"), firstKeyframe.Easing),
             new SequenceKeyframe(StableGuidUx5B(sequence.Id, "unlock"), Math.Max(1, durationMilliseconds - 250),
                 null, "controls.locked=false", SequenceEasing.Linear)], stateTrack.Provenance);
        var tracks = sequence.Tracks.Select(item => item.Id == stateTrack.Id ? stateTrack : item).ToArray();
        sequence = new SequenceDefinition(sequence.Id, 1, sequence.Slug,
            RequiredUx5B(displayName, sequence.DisplayName), durationMilliseconds, skippable, synchronized,
            tracks, [], "studio:ux5-f");
        var sequences = draft.ModuleCandidate.Sequences.Select(item => item.Id == sequence.Id ? sequence : item).ToArray();
        var startup = startupEnabled
            ? new StartupFlowDefinition(draft.ModuleCandidate.Startup.Id, 1, StartupFlowKind.Sequence, sequence.Id,
                string.Empty, StartupFlowKind.Menu, null, string.Empty, true, "Passer", "studio:ux5-f")
            : new StartupFlowDefinition(draft.ModuleCandidate.Startup.Id, 1, StartupFlowKind.None, null,
                string.Empty, StartupFlowKind.None, null, string.Empty, true, "Passer", "studio:ux5-f");
        var module = new ModuleCinematicProfile(draft.ModuleCandidate.Id, 1, startup, sequences,
            "studio:ux5-f", draft.ModuleCandidate.TransformOverrides, draft.ModuleCandidate.Tweens);
        return draft with { ModuleCandidate = module };
    }

    internal StudioNarrativePreviewUx5B PreviewDialogueProductionUx5B(StudioDialogueProductionDraftUx5B draft)
    {
        CancelNarrativeApprovalUx5B(StudioNarrativeSectionUx5B.Dialogue);
        var project = Project; var history = History;
        if (project is null || history is null || history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintProjectUx5B(project), draft.SourceProjectFingerprint, StringComparison.Ordinal))
            return FailedPreviewUx5B("Le projet a changé ; rechargez le dialogue.", "command.revision_conflict");
        var command = new SetDialogueCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            draft.Candidate, true);
        var dryRun = _setDialogueUx5B.Handle(history.State, command);
        if (!dryRun.Succeeded) return FailedPreviewUx5B(dryRun);
        var validation = DialogueValidator.Validate(draft.Candidate);
        var simulation = DialogueSimulationService.Run(draft.Candidate);
        var coverage = DialogueProductionRuntimeService.MeasureCoverage(draft.Candidate);
        if (validation.Count > 0)
            return FailedPreviewUx5B(validation[0].Message, validation[0].Code, validation[0].Path);
        _approvedDialogueUx5B = FreezeDialogueDraftUx5B(draft);
        _approvedDialogueFingerprintUx5B = FingerprintProjectUx5B(ReplaceDialogueUx5B(project, draft.Candidate));
        return new(true, true, "Dialogue de production prêt à appliquer.",
            dryRun.Patch?.Summary ?? "Prévisualiser le dialogue.", [],
            [
                $"Auteur : {draft.Candidate.Authoring!.Nodes.Count} nœuds · {draft.Candidate.Authoring.Participants.Count} rôles",
                $"Mise en scène : {draft.Candidate.Presentation!.AnimationCues.Count} animation · {draft.Candidate.Presentation.VoiceCues.Count} voix · {draft.Candidate.Presentation.CameraShots.Count} caméra",
                $"Expérience : {draft.Candidate.Experience!.MemorySlots.Count} mémoire · {draft.Candidate.Experience.SkillChecks.Count} test de compétence",
                $"Production : {draft.Candidate.Production!.Subgraphs.Count} sous-graphe · couverture {coverage.Ratio:P0}",
                $"Simulation : {simulation.StopReason} · {simulation.VisitedNodes.Count} nœud(s)",
            ], NarrativeBoundaryUx5B);
    }

    internal StudioNarrativePreviewUx5B PreviewQuestProductionUx5B(StudioQuestProductionDraftUx5B draft)
    {
        CancelNarrativeApprovalUx5B(StudioNarrativeSectionUx5B.Quest);
        var project = Project; var history = History;
        if (project is null || history is null || history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintProjectUx5B(project), draft.SourceProjectFingerprint, StringComparison.Ordinal))
            return FailedPreviewUx5B("Le projet a changé ; rechargez la quête.", "command.revision_conflict");
        var command = new SetQuestCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            draft.Candidate, true);
        var dryRun = _setQuestUx5B.Handle(history.State, command);
        if (!dryRun.Succeeded) return FailedPreviewUx5B(dryRun);
        var validation = QuestValidator.Validate(draft.Candidate);
        if (validation.Count > 0)
            return FailedPreviewUx5B(validation[0].Message, validation[0].Code, validation[0].Path);
        var simulation = QuestSimulationEngine.Run(draft.Candidate, QuestProgressScope.Player, "studio:preview",
            DateTimeOffset.UnixEpoch);
        _approvedQuestUx5B = FreezeQuestDraftUx5B(draft);
        _approvedQuestFingerprintUx5B = FingerprintProjectUx5B(ReplaceQuestUx5B(project, draft.Candidate));
        return new(true, true, "Quête complète prête à appliquer.",
            dryRun.Patch?.Summary ?? "Prévisualiser la quête.", [],
            [
                $"Journal : {draft.Candidate.States.Count} états · {draft.Candidate.Transitions.Count} transitions · priorité {draft.Candidate.Priority}",
                $"Auteur : {draft.Candidate.Authoring!.Objectives.Count} objectif · {draft.Candidate.Authoring.Participants.Count} participant",
                $"Expérience : {draft.Candidate.Experience!.Steps.Count} étape · {draft.Candidate.Experience.Markers.Count} pin",
                $"Qualité : {draft.Candidate.Quality!.TestScenarios.Count} scénarios · plan humain {draft.Candidate.Quality.AiPlan.Authorization}",
                $"Simulation : {simulation.Status} · {simulation.Steps.Count} étape(s)",
            ], NarrativeBoundaryUx5B);
    }

    internal StudioNarrativePreviewUx5B PreviewLocalizationProductionUx5B(
        StudioLocalizationProductionDraftUx5B draft)
    {
        CancelNarrativeApprovalUx5B(StudioNarrativeSectionUx5B.Localization);
        var project = Project; var history = History;
        if (project is null || history is null || history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintProjectUx5B(project), draft.SourceProjectFingerprint, StringComparison.Ordinal))
            return FailedPreviewUx5B("Le projet a changé ; réextrayez le catalogue.", "command.revision_conflict");
        var command = new SetLocalizationCatalogCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            draft.Candidate, true);
        var dryRun = _setLocalizationUx5B.Handle(history.State, command);
        var workspacePreview = PreviewWorkspaceProfile(draft.WorkspaceCandidate);
        if (!dryRun.Succeeded) return FailedPreviewUx5B(dryRun);
        if (!workspacePreview.Succeeded)
            return FailedPreviewUx5B(workspacePreview.Message, "workspace.profile.invalid");
        var quality = LocalizationQualityService.Analyze(draft.Candidate, 180);
        _approvedLocalizationUx5B = FreezeLocalizationDraftUx5B(draft);
        _approvedLocalizationFingerprintUx5B = FingerprintLocalizationDraftUx5B(draft);
        return new(true, quality.Passed, quality.Passed
                ? "Catalogue et typographie accessibles prêts à appliquer en deux transactions explicites."
                : "Le catalogue contient une erreur de localisation bloquante.",
            dryRun.Patch?.Summary ?? "Prévisualiser la localisation.",
            quality.Issues.Where(item => item.Severity == LocalizationQualitySeverity.Error)
                .Select(item => new StudioNarrativeDiagnosticUx5B(item.Code, item.MessageKey, item.Detail)).ToArray(),
            [
                $"Catalogue : {draft.Candidate.Messages.Count} messages · {draft.Candidate.Locales.Count} locales",
                $"Couverture : {string.Join(" · ", quality.Coverage.Select(item => $"{item.Locale} {item.Ratio:P0}"))}",
                $"Sous-titres importants : {draft.Candidate.Messages.Count(item => item.Key.StartsWith("audio.important", StringComparison.Ordinal))}",
                $"Police : {draft.WorkspaceCandidate.PrimaryFontFamily} · texte {draft.WorkspaceCandidate.TextScale:0.##}×",
            ], NarrativeBoundaryUx5B);
    }

    internal StudioNarrativePreviewUx5B PreviewLoreProductionUx5B(StudioLoreProductionDraftUx5B draft,
        string query)
    {
        CancelNarrativeApprovalUx5B(StudioNarrativeSectionUx5B.Lore);
        var project = Project;
        EnsureLoreHistoryUx5B();
        var history = _loreHistoryUx5B!;
        if (project is null || history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintProjectUx5B(project), draft.SourceProjectFingerprint, StringComparison.Ordinal))
            return FailedPreviewUx5B("Le projet ou l’index a changé ; rechargez le lore.",
                SetLoreIndexHandler.RevisionConflictCode);
        var command = new SetLoreIndexCommand(CommandId.From(Guid.NewGuid()), draft.TransactionId, draft.PlanId,
            history.State.Revision, draft.Candidate, AssistantPermissionLevel.Configure, true);
        var dryRun = _setLoreUx5B.Handle(history.State, command, project);
        if (!dryRun.Succeeded) return FailedPreviewUx5B(dryRun.Diagnostics.First().Message,
            dryRun.Diagnostics.First().Code);
        var hits = LoreRetrievalIndex.Build(draft.Candidate, project).Query(query, 8);
        var packet = LoreContextAssembler.Assemble(draft.Candidate, project, query, 8);
        _approvedLoreUx5B = FreezeLoreDraftUx5B(draft);
        _approvedLoreFingerprintUx5B = FingerprintLoreDraftUx5B(draft);
        return new(true, true, "Index de lore et graphe de références prêts à appliquer.",
            dryRun.Patch?.Summary ?? "Prévisualiser l’index de lore.", [],
            [
                $"Index : {draft.Candidate.Entries.Count} entrées · {draft.Candidate.Entries.Sum(item => item.RelatedEntryIds.Count)} relations",
                $"Recherche « {query} » : {hits.Count} résultat(s)",
                $"Contexte borné : {packet.EntryCount} entrée(s) · {packet.Content.Length}/{draft.Candidate.MaximumContextCharacters} caractères",
                $"Sources résolues : {packet.SourceIds.Count}",
            ], NarrativeBoundaryUx5B);
    }

    internal StudioNarrativePreviewUx5B PreviewCinematicProductionUx5B(
        StudioCinematicProductionDraftUx5B draft)
    {
        CancelNarrativeApprovalUx5B(StudioNarrativeSectionUx5B.Cinematic);
        var project = Project; var history = History;
        if (project is null || history is null || history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintProjectUx5B(project), draft.SourceProjectFingerprint, StringComparison.Ordinal))
            return FailedPreviewUx5B("Le projet a changé ; rechargez la timeline.", "command.revision_conflict");
        var command = new SetCinematicProfilesCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            draft.ModuleCandidate, draft.AreaId, draft.AreaCandidate, true);
        var dryRun = _setCinematicsUx5B.Handle(history.State, command);
        if (!dryRun.Succeeded) return FailedPreviewUx5B(dryRun);
        _approvedCinematicUx5B = FreezeCinematicDraftUx5B(draft);
        _approvedCinematicFingerprintUx5B = FingerprintProjectUx5B(ReplaceCinematicsUx5B(project, draft));
        var sequence = draft.ModuleCandidate.Sequences.Single(item =>
            item.Id == StableGuidUx5B(draft.ModuleCandidate.Id, "ux5-f:sequence"));
        return new(true, true, "Timeline et démarrage prêts à appliquer.",
            dryRun.Patch?.Summary ?? "Prévisualiser les cinématiques.", [],
            [
                $"Démarrage : {draft.ModuleCandidate.Startup.Kind} · séquence {sequence.DisplayName}",
                $"Timeline : {sequence.DurationMilliseconds / 1000d:0.##} s · {sequence.Tracks.Count} pistes",
                $"Contrôle : {sequence.Tracks.Count(item => item.Kind == SequenceTrackKind.State)} piste d’état · déverrouillage explicite",
                $"Caméras : {draft.AreaCandidate.CameraAnchors.Count} ancre(s) préservée(s)",
            ], NarrativeBoundaryUx5B);
    }

    internal StudioNarrativeActionResultUx5B ApplyApprovedNarrativeUx5B(
        StudioNarrativeSectionUx5B section,
        object currentDraft)
    {
        return section switch
        {
            StudioNarrativeSectionUx5B.Dialogue when currentDraft is StudioDialogueProductionDraftUx5B dialogue =>
                ApplyDialogueUx5B(dialogue),
            StudioNarrativeSectionUx5B.Quest when currentDraft is StudioQuestProductionDraftUx5B quest =>
                ApplyQuestUx5B(quest),
            StudioNarrativeSectionUx5B.Localization when currentDraft is StudioLocalizationProductionDraftUx5B loc =>
                ApplyLocalizationUx5B(loc),
            StudioNarrativeSectionUx5B.Lore when currentDraft is StudioLoreProductionDraftUx5B lore =>
                ApplyLoreUx5B(lore),
            StudioNarrativeSectionUx5B.Cinematic when currentDraft is StudioCinematicProductionDraftUx5B cinematic =>
                ApplyCinematicUx5B(cinematic),
            _ => new(false, "Le brouillon actif ne correspond pas à l’onglet sélectionné."),
        };
    }

    internal StudioNarrativeActionResultUx5B UndoNarrativeUx5B(StudioNarrativeSectionUx5B section)
    {
        CancelNarrativeApprovalUx5B(section);
        if (section == StudioNarrativeSectionUx5B.Lore)
        {
            EnsureLoreHistoryUx5B();
            var success = _loreHistoryUx5B!.Undo() && SaveLoreUx5B();
            return new(success, success ? "Index de lore annulé." : "Aucune modification de lore à annuler.");
        }
        if (section == StudioNarrativeSectionUx5B.Localization)
        {
            var workspace = UndoWorkspaceProfile(false);
            var project = Undo();
            return new(workspace.Succeeded && project.Succeeded,
                workspace.Succeeded && project.Succeeded ? "Localisation et typographie annulées." : project.Message);
        }
        var result = Undo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioNarrativeActionResultUx5B RedoNarrativeUx5B(StudioNarrativeSectionUx5B section)
    {
        CancelNarrativeApprovalUx5B(section);
        if (section == StudioNarrativeSectionUx5B.Lore)
        {
            EnsureLoreHistoryUx5B();
            var success = _loreHistoryUx5B!.Redo() && SaveLoreUx5B();
            return new(success, success ? "Index de lore rétabli." : "Aucune modification de lore à rétablir.");
        }
        if (section == StudioNarrativeSectionUx5B.Localization)
        {
            var project = Redo();
            var workspace = RedoWorkspaceProfile(false);
            return new(workspace.Succeeded && project.Succeeded,
                workspace.Succeeded && project.Succeeded ? "Localisation et typographie rétablies." : project.Message);
        }
        var result = Redo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioNarrativeActionResultUx5B SaveNarrativeUx5B(StudioNarrativeSectionUx5B section)
    {
        if (section == StudioNarrativeSectionUx5B.Lore)
            return new(SaveLoreUx5B(), "Index de lore enregistré.");
        var project = Save();
        if (!project.Succeeded) return new(false, project.Message);
        if (section != StudioNarrativeSectionUx5B.Localization) return new(true, project.Message);
        var workspace = SaveWorkspaceProfile();
        return new(workspace.Succeeded, workspace.Succeeded
            ? "Catalogue et profil typographique enregistrés." : workspace.Message);
    }

    internal void CancelNarrativeApprovalUx5B(StudioNarrativeSectionUx5B? section = null)
    {
        if (section is null or StudioNarrativeSectionUx5B.Dialogue)
        { _approvedDialogueUx5B = null; _approvedDialogueFingerprintUx5B = null; }
        if (section is null or StudioNarrativeSectionUx5B.Quest)
        { _approvedQuestUx5B = null; _approvedQuestFingerprintUx5B = null; }
        if (section is null or StudioNarrativeSectionUx5B.Localization)
        { _approvedLocalizationUx5B = null; _approvedLocalizationFingerprintUx5B = null; }
        if (section is null or StudioNarrativeSectionUx5B.Lore)
        { _approvedLoreUx5B = null; _approvedLoreFingerprintUx5B = null; }
        if (section is null or StudioNarrativeSectionUx5B.Cinematic)
        { _approvedCinematicUx5B = null; _approvedCinematicFingerprintUx5B = null; }
    }

    internal void CloseUx5B()
    {
        CancelNarrativeApprovalUx5B();
        _loreHistoryUx5B = null;
    }

    internal StudioNarrativeApprovalSnapshotUx5B CaptureNarrativeApprovalUx5B() => new(
        _approvedDialogueUx5B, _approvedDialogueFingerprintUx5B,
        _approvedQuestUx5B, _approvedQuestFingerprintUx5B,
        _approvedLocalizationUx5B, _approvedLocalizationFingerprintUx5B,
        _approvedLoreUx5B, _approvedLoreFingerprintUx5B,
        _approvedCinematicUx5B, _approvedCinematicFingerprintUx5B);

    internal void RestoreNarrativeApprovalUx5B(StudioNarrativeApprovalSnapshotUx5B snapshot)
    {
        _approvedDialogueUx5B = snapshot.Dialogue; _approvedDialogueFingerprintUx5B = snapshot.DialogueCandidateFingerprint;
        _approvedQuestUx5B = snapshot.Quest; _approvedQuestFingerprintUx5B = snapshot.QuestCandidateFingerprint;
        _approvedLocalizationUx5B = snapshot.Localization; _approvedLocalizationFingerprintUx5B = snapshot.LocalizationCandidateFingerprint;
        _approvedLoreUx5B = snapshot.Lore; _approvedLoreFingerprintUx5B = snapshot.LoreCandidateFingerprint;
        _approvedCinematicUx5B = snapshot.Cinematic; _approvedCinematicFingerprintUx5B = snapshot.CinematicCandidateFingerprint;
    }

    private StudioNarrativeActionResultUx5B ApplyDialogueUx5B(StudioDialogueProductionDraftUx5B current)
    {
        var project = Project; var history = History; var approved = _approvedDialogueUx5B;
        if (project is null || history is null || approved is null || _approvedDialogueFingerprintUx5B is null ||
            history.State.Revision != approved.BaseRevision ||
            !string.Equals(FingerprintProjectUx5B(project), approved.SourceProjectFingerprint, StringComparison.Ordinal) ||
            !string.Equals(FingerprintProjectUx5B(ReplaceDialogueUx5B(project, current.Candidate)),
                _approvedDialogueFingerprintUx5B, StringComparison.Ordinal))
        {
            CancelNarrativeApprovalUx5B(StudioNarrativeSectionUx5B.Dialogue);
            return new(false, "Le projet ou le dialogue a changé ; prévisualisez de nouveau.");
        }
        var command = new SetDialogueCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            approved.Candidate, true);
        var dryRun = _setDialogueUx5B.Handle(history.State, command);
        var result = dryRun.Succeeded
            ? _setDialogueUx5B.Handle(history.State, command with { DryRun = false })
            : dryRun;
        CancelNarrativeApprovalUx5B(StudioNarrativeSectionUx5B.Dialogue);
        if (!result.Succeeded) return FailedActionUx5B(result);
        history.Record(result); InvalidateContentIndexes();
        return new(true, "Dialogue de production appliqué en une commande annulable.");
    }

    private StudioNarrativeActionResultUx5B ApplyQuestUx5B(StudioQuestProductionDraftUx5B current)
    {
        var project = Project; var history = History; var approved = _approvedQuestUx5B;
        if (project is null || history is null || approved is null || _approvedQuestFingerprintUx5B is null ||
            history.State.Revision != approved.BaseRevision ||
            !string.Equals(FingerprintProjectUx5B(project), approved.SourceProjectFingerprint, StringComparison.Ordinal) ||
            !string.Equals(FingerprintProjectUx5B(ReplaceQuestUx5B(project, current.Candidate)),
                _approvedQuestFingerprintUx5B, StringComparison.Ordinal))
        {
            CancelNarrativeApprovalUx5B(StudioNarrativeSectionUx5B.Quest);
            return new(false, "Le projet ou la quête a changé ; prévisualisez de nouveau.");
        }
        var command = new SetQuestCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            approved.Candidate, true);
        var dryRun = _setQuestUx5B.Handle(history.State, command);
        var result = dryRun.Succeeded ? _setQuestUx5B.Handle(history.State, command with { DryRun = false }) : dryRun;
        CancelNarrativeApprovalUx5B(StudioNarrativeSectionUx5B.Quest);
        if (!result.Succeeded) return FailedActionUx5B(result);
        history.Record(result); InvalidateContentIndexes();
        return new(true, "Quête complète appliquée en une commande annulable.");
    }

    private StudioNarrativeActionResultUx5B ApplyLocalizationUx5B(StudioLocalizationProductionDraftUx5B current)
    {
        var project = Project; var history = History; var approved = _approvedLocalizationUx5B;
        if (project is null || history is null || approved is null || _approvedLocalizationFingerprintUx5B is null ||
            history.State.Revision != approved.BaseRevision ||
            !string.Equals(FingerprintProjectUx5B(project), approved.SourceProjectFingerprint, StringComparison.Ordinal) ||
            !string.Equals(FingerprintLocalizationDraftUx5B(current), _approvedLocalizationFingerprintUx5B,
                StringComparison.Ordinal))
        {
            CancelNarrativeApprovalUx5B(StudioNarrativeSectionUx5B.Localization);
            return new(false, "Le catalogue ou la typographie a changé ; prévisualisez de nouveau.");
        }
        var command = new SetLocalizationCatalogCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            approved.Candidate, true);
        var dryRun = _setLocalizationUx5B.Handle(history.State, command);
        if (!dryRun.Succeeded)
        {
            CancelNarrativeApprovalUx5B(StudioNarrativeSectionUx5B.Localization);
            return FailedActionUx5B(dryRun);
        }
        var result = _setLocalizationUx5B.Handle(history.State, command with { DryRun = false });
        if (!result.Succeeded)
        {
            CancelNarrativeApprovalUx5B(StudioNarrativeSectionUx5B.Localization);
            return FailedActionUx5B(result);
        }
        history.Record(result);
        var workspace = ApplyWorkspaceProfile(approved.WorkspaceCandidate);
        CancelNarrativeApprovalUx5B(StudioNarrativeSectionUx5B.Localization);
        if (!workspace.Succeeded)
        {
            _ = Undo();
            return new(false, "La typographie a été refusée ; le catalogue a été annulé.");
        }
        InvalidateContentIndexes();
        return new(true, "Catalogue appliqué puis typographie appliquée dans deux historiques explicites.");
    }

    private StudioNarrativeActionResultUx5B ApplyLoreUx5B(StudioLoreProductionDraftUx5B current)
    {
        var project = Project; EnsureLoreHistoryUx5B(); var history = _loreHistoryUx5B!;
        var approved = _approvedLoreUx5B;
        if (project is null || approved is null || _approvedLoreFingerprintUx5B is null ||
            history.State.Revision != approved.BaseRevision ||
            !string.Equals(FingerprintProjectUx5B(project), approved.SourceProjectFingerprint, StringComparison.Ordinal) ||
            !string.Equals(FingerprintLoreDraftUx5B(current), _approvedLoreFingerprintUx5B, StringComparison.Ordinal))
        {
            CancelNarrativeApprovalUx5B(StudioNarrativeSectionUx5B.Lore);
            return new(false, "L’index ou le projet a changé ; prévisualisez de nouveau.");
        }
        var command = new SetLoreIndexCommand(CommandId.From(Guid.NewGuid()), approved.TransactionId,
            approved.PlanId, history.State.Revision, approved.Candidate, AssistantPermissionLevel.Configure, false);
        var result = _setLoreUx5B.Handle(history.State, command, project);
        CancelNarrativeApprovalUx5B(StudioNarrativeSectionUx5B.Lore);
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ?? "Index de lore refusé.");
        history.Record(result);
        if (!SaveLoreUx5B())
        {
            _ = history.Undo();
            return new(false, "L’index n’a pas pu être enregistré ; la mutation a été annulée.");
        }
        return new(true, "Index de lore appliqué et enregistré atomiquement.");
    }

    private StudioNarrativeActionResultUx5B ApplyCinematicUx5B(StudioCinematicProductionDraftUx5B current)
    {
        var project = Project; var history = History; var approved = _approvedCinematicUx5B;
        if (project is null || history is null || approved is null || _approvedCinematicFingerprintUx5B is null ||
            history.State.Revision != approved.BaseRevision ||
            !string.Equals(FingerprintProjectUx5B(project), approved.SourceProjectFingerprint, StringComparison.Ordinal) ||
            !string.Equals(FingerprintProjectUx5B(ReplaceCinematicsUx5B(project, current)),
                _approvedCinematicFingerprintUx5B, StringComparison.Ordinal))
        {
            CancelNarrativeApprovalUx5B(StudioNarrativeSectionUx5B.Cinematic);
            return new(false, "La timeline ou le projet a changé ; prévisualisez de nouveau.");
        }
        var command = new SetCinematicProfilesCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            approved.ModuleCandidate, approved.AreaId, approved.AreaCandidate, true);
        var dryRun = _setCinematicsUx5B.Handle(history.State, command);
        var result = dryRun.Succeeded
            ? _setCinematicsUx5B.Handle(history.State, command with { DryRun = false }) : dryRun;
        CancelNarrativeApprovalUx5B(StudioNarrativeSectionUx5B.Cinematic);
        if (!result.Succeeded) return FailedActionUx5B(result);
        history.Record(result); InvalidateContentIndexes();
        return new(true, "Timeline et séquence de démarrage appliquées en une commande annulable.");
    }

    private static StudioNarrativeActionResultUx5B FailedActionUx5B(CommandResult result) => new(false,
        result.Diagnostics.FirstOrDefault()?.Message ?? "La commande canonique a été refusée.");

    private static StudioNarrativePreviewUx5B FailedPreviewUx5B(CommandResult result) =>
        FailedPreviewUx5B(result.Diagnostics.FirstOrDefault()?.Message ?? "Aperçu refusé.",
            result.Diagnostics.FirstOrDefault()?.Code ?? "studio.narrative.invalid");

    private static StudioNarrativePreviewUx5B FailedPreviewUx5B(string message, string code,
        string path = "studio/narrative") => new(false, false, message, string.Empty,
        [new StudioNarrativeDiagnosticUx5B(code, path, message)], [], NarrativeBoundaryUx5B);
}
