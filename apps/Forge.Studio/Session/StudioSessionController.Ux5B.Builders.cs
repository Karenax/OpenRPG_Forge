using System.Security.Cryptography;
using System.Text;
using Forge.Application.Assistant;
using Forge.Domain.Areas;
using Forge.Domain.Assistant;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Localization;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    internal StudioUx5BEvidenceProject? CreateUx5BEvidenceProject()
    {
        var project = Project;
        if (project is null || project.Areas.Count == 0 || project.Dialogues.All(item => item.Nodes.Count < 2) ||
            project.Quests.All(item => item.Transitions.Count == 0) ||
            project.Blueprints.All(item => item.Kind != BlueprintKind.Creature))
            return null;
        var dialogue = project.Dialogues.First(item => item.Nodes.Count >= 2);
        var quest = project.Quests.First(item => item.Transitions.Count > 0);
        return new StudioUx5BEvidenceProject(project, dialogue.Id, quest.Id, project.Areas[0].Id,
            UsesRealModule: true, UsesCanonicalFallback: false);
    }

    private static bool DialogueCompleteUx5B(DialogueDefinition dialogue) =>
        dialogue.Authoring is { Enabled: true } && dialogue.Experience is { Enabled: true } &&
        dialogue.Presentation is not null && dialogue.Production is { Enabled: true } &&
        DialogueValidator.Validate(dialogue).Count == 0;

    private static bool QuestCompleteUx5B(QuestDefinition quest) =>
        quest.Operations is not null && quest.Authoring is { Enabled: true } &&
        quest.Experience is { Enabled: true } && quest.Quality is { Enabled: true } &&
        QuestValidator.Validate(quest).Count == 0;

    private static DialogueDefinition CompleteDialogueUx5B(ProjectDefinition project, DialogueDefinition source)
    {
        const string provenance = "studio:ux5-b";
        var speaker = project.Blueprints.First(item =>
            item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn).Id;
        var subgraphSource = source.Nodes.FirstOrDefault(item => item.Id != source.EntryNodeId) ?? source.Nodes[^1];
        var subgraphKey = $"ux5-{IdentifierUx5B(source.Slug, "dialogue")}";
        var entry = source.Nodes.Single(item => item.Id == source.EntryNodeId);
        var (lifecycleAction, wizardAction) = DialogueScriptWizard.BuildAction(
            DialogueActionKind.SetVariable, "ux5.production-reviewed", "true", false, provenance);
        var nodes = source.Nodes.Select(item =>
        {
            var role = item.Id == subgraphSource.Id ? DialogueNodeRole.Subgraph : item.Role;
            var key = item.Id == subgraphSource.Id ? subgraphKey : item.SubgraphKey;
            var actions = item.Id == entry.Id ? item.OnEnterActions.Append(lifecycleAction).ToArray() : item.OnEnterActions;
            return new DialogueNode(item.Id, item.SpeakerBlueprintId, item.Text, actions, item.Choices,
                role, item.ParticipantId, key);
        }).ToArray();
        var provisional = new DialogueDefinition(source.Id, source.Slug, source.DisplayName, source.EntryNodeId,
            nodes, null, source.SchemaVersion, provenance, source.Metadata, source.Participants,
            source.EntryPoints, null);

        var authoring = DialogueAuthoringProfile.CreateDefault(provisional, provenance);
        authoring = authoring with
        {
            RequiredAtRuntime = true,
            Nodes = authoring.Nodes.Select(item => item.NodeId == entry.Id
                ? item with
                {
                    WizardEntries = item.WizardEntries.Append(wizardAction).ToArray(),
                    Notes = item.Notes with
                    {
                        Intent = "Installer le choix, ses conséquences et la mise en scène.",
                        Emotion = "determined",
                        Source = "studio:ux5-b:direct-authoring",
                        Status = DialogueNodeEditorialStatus.Review,
                        Todos = ["Valider la voix et le cadrage en contexte."],
                        AuthorMention = "@narrative-author",
                    },
                }
                : item with
                {
                    Notes = item.Notes with
                    {
                        Intent = "Préserver la cohérence de la branche narrative.",
                        Emotion = "neutral",
                        Source = "studio:ux5-b:direct-authoring",
                        Status = DialogueNodeEditorialStatus.Review,
                        AuthorMention = "@narrative-author",
                    },
                }).ToArray(),
        };

        var experienceDefault = DialogueExperienceProfile.CreateDefault(provisional, provenance);
        var completed = new DialogueAction(StableGuidUx5B(source.Id.Value, "experience:complete"), 1,
            provenance, DialogueActionKind.SetVariable, "dialogue.completed", "true", false);
        var aborted = new DialogueAction(StableGuidUx5B(source.Id.Value, "experience:abort"), 1,
            provenance, DialogueActionKind.SetVariable, "dialogue.aborted", "true", false);
        var memory = new DialogueMemorySlot(StableGuidUx5B(source.Id.Value, "experience:memory"), 1,
            provenance, DialogueMemoryScope.Player, "player", "narrative-memory", DialogueTextTokenType.Text,
            DialogueMemoryDuration.Campaign, "unknown");
        var check = new DialogueSkillCheck(StableGuidUx5B(source.Id.Value, "experience:skill"), 1,
            provenance, entry.Id, "insight", 12, null, false, DialogueSkillCheckVisibility.FullExplanation,
            subgraphSource.Id, entry.Id);
        var experience = experienceDefault with
        {
            RequiredAtRuntime = true,
            Lifecycle = experienceDefault.Lifecycle with
            {
                CompletedActions = [completed],
                AbortedActions = [aborted],
                RollbackOnAbort = true,
            },
            Search = experienceDefault.Search with
            {
                Pattern = source.DisplayName,
                UseRegex = false,
                ReplacementPreview = source.DisplayName,
            },
            MemorySlots = [memory],
            SkillChecks = [check],
        };

        var choiceNode = nodes.FirstOrDefault(item => item.Choices.Count > 0) ?? entry;
        var defaultChoice = choiceNode.Choices.FirstOrDefault()?.Id;
        var presentation = new DialoguePresentationProfile(
            StableGuidUx5B(source.Id.Value, "presentation"), 1, provenance, true, true,
            [new DialogueAnimationCue(StableGuidUx5B(source.Id.Value, "presentation:animation"), entry.Id,
                speaker, "talk.explain", 0, 1_800, 180, DialogueGesture.Explain, null,
                DialogueAnimationInterruptionPolicy.BlendOut, "idle", provenance)],
            [new DialogueVoiceCue(StableGuidUx5B(source.Id.Value, "presentation:voice"), entry.Id, speaker,
                "fr-FR", null, "studio-preview", entry.Text, "viseme-neutral", "determined", 1_800,
                null, null, true, provenance)],
            [new DialogueCameraShot(StableGuidUx5B(source.Id.Value, "presentation:camera"), entry.Id,
                DialogueCameraShotMode.Automatic, null, speaker, CameraFraming.Medium, 52, 1_800, 250,
                true, true, true, .08f, 10, provenance)],
            [new DialogueBookmark(StableGuidUx5B(source.Id.Value, "presentation:bookmark"), entry.Id,
                "Relecture production", DialogueBookmarkScope.Shared,
                "Valider le rythme, le sous-titre et les conséquences.", "narrative-author",
                $"dialogue://{source.Id}/{entry.Id}", provenance)],
            [new DialogueInterjectionRule(StableGuidUx5B(source.Id.Value, "presentation:interjection"),
                entry.Id, subgraphSource.Id, speaker, ["observant"], true, 0, 50, 20,
                "ux5-production", DialogueInterjectionConflictPolicy.HighestPriority, provenance)],
            defaultChoice is null ? [] :
            [new DialogueTimedChoiceRule(StableGuidUx5B(source.Id.Value, "presentation:timed"),
                choiceNode.Id, 8_000, defaultChoice, DialogueTimedChoicePausePolicy.AccessibilityOnly,
                DialogueRuntimeAuthorityScope.Server, 1, null, "server:narrative", provenance)],
            [new DialogueVariantSet(StableGuidUx5B(source.Id.Value, "presentation:variants"), entry.Id,
                "production-tone", DialogueVariantSeedScope.Dialogue, 5, 1,
                DialogueRuntimeAuthorityScope.Server, true,
                [new DialogueTextVariant(StableGuidUx5B(source.Id.Value, "presentation:variant:1"),
                    new LocalizedText($"dialogue.{source.Slug}.variant.production",
                        entry.Text.Fallback, provenance: provenance), 1, [], provenance)], provenance)]);

        var withProfiles = RebuildDialogueUx5B(provisional, authoring, experience, presentation,
            BuildDialogueProductionUx5B(provisional, subgraphSource.Id, subgraphKey));
        return withProfiles;
    }

    private static DialogueProductionProfile BuildDialogueProductionUx5B(
        DialogueDefinition dialogue, DialogueNodeId subgraphNodeId, string subgraphKey)
    {
        const string p = "studio:ux5-b";
        Guid Id(string value) => StableGuidUx5B(dialogue.Id.Value, $"production:{value}");
        var contractId = Id("contract");
        var input = new DialogueSubgraphParameter(Id("input"), 1, p, "trust", DialogueTextTokenType.Integer,
            DialogueSubgraphParameterDirection.Input, true);
        var output = new DialogueSubgraphParameter(Id("output"), 1, p, "accepted", DialogueTextTokenType.Boolean,
            DialogueSubgraphParameterDirection.Output, true);
        var test = new DialogueSubgraphTestCase(Id("test"), 1, p, "trust accepted",
            new Dictionary<string, string> { ["trust"] = "4" },
            new Dictionary<string, string> { ["accepted"] = "true" });
        var contract = new DialogueSubgraphContract(contractId, 1, p, subgraphKey, 1,
            [input, output], ["party:trust"], [test]);
        var invocation = new DialogueSubgraphInvocation(Id("invocation"), 1, p, subgraphNodeId, contractId,
            new Dictionary<string, string> { ["trust"] = "party:trust" },
            new Dictionary<string, string> { ["accepted"] = "memory:accepted" });
        var consequences = Enum.GetValues<DialogueConsequenceTargetKind>().Select((kind, index) =>
            new DialogueTypedConsequence(Id($"consequence:{index}"), 1, p, dialogue.EntryNodeId, kind,
                kind switch
                {
                    DialogueConsequenceTargetKind.Quest => "quest:narrative-production",
                    DialogueConsequenceTargetKind.Faction => "faction:narrative-allies",
                    DialogueConsequenceTargetKind.Item => "item:narrative-token",
                    _ => "world:narrative-production",
                }, kind == DialogueConsequenceTargetKind.Faction ? "reputation" : "set", "+1",
                $"Impact {kind} revu dans le workbench.")).ToArray();
        var evidence = new DialogueCoverageEvidence(Id("coverage:evidence"), 1, p, "parcours production",
            DialogueCoverageEvidenceKind.Test, [dialogue.EntryNodeId, subgraphNodeId],
            new Dictionary<string, string> { ["quest:narrative-production"] = "active" });
        var coverage = new DialogueCoveragePolicy(Id("coverage"), 1, p, 64,
            new Dictionary<string, string> { ["quest:narrative-production"] = "active" }, [evidence]);
        var now = DateTimeOffset.UnixEpoch;
        var collaboration = new DialogueCollaborationProfile(Id("collaboration"), 1, p,
            [new DialogueNodeRevision(Id("revision"), 1, p, subgraphNodeId, 1, "narrative-author",
                "{\"status\":\"review\"}", now)],
            [new DialogueNodeLock(Id("lock"), 1, p, subgraphNodeId, "narrative-author", now.AddHours(1))],
            [new DialogueNodeComment(Id("comment"), 1, p, subgraphNodeId, "reviewer",
                "Valider la conséquence et le ton.", false, now)],
            [new DialogueSemanticConflict(Id("conflict"), 1, p, subgraphNodeId, "text",
                "{\"text\":\"base\"}", "{\"text\":\"ours\"}", "{\"text\":\"theirs\"}",
                DialogueConflictResolution.Manual, "{\"text\":\"merged\"}", true)]);
        var ai = new DialogueAiPlan(Id("ai"), 1, p, Id("ai:transaction"), Id("ai:plan"),
            DialogueAiAuthorizationLevel.AuthorApply, "Proposition narrative contrôlée.",
            ["mise en situation", "choix"], ["style:module"], ["PROJECT_CONTEXT.md"],
            $"preview:dialogue:{dialogue.Id}|nodes:{dialogue.Nodes.Count}", [], true, false);
        return new DialogueProductionProfile(Id("profile"), 1, p, true, true, [contract], [invocation],
            consequences, coverage, collaboration, [ai]);
    }

    private static DialogueDefinition RebuildDialogueUx5B(DialogueDefinition source,
        DialogueAuthoringProfile authoring, DialogueExperienceProfile experience,
        DialoguePresentationProfile presentation, DialogueProductionProfile production) =>
        new(source.Id, source.Slug, source.DisplayName, source.EntryNodeId, source.Nodes, presentation,
            source.SchemaVersion, source.Provenance, source.Metadata, source.Participants, source.EntryPoints,
            source.Edges, authoring, experience, production);

    private static QuestDefinition CompleteQuestUx5B(ProjectDefinition project, QuestDefinition source)
    {
        const string p = "studio:ux5-c";
        Guid Id(string value) => StableGuidUx5B(source.Id.Value, value);
        var area = project.Areas[0];
        var dialogue = project.Dialogues[0];
        var creature = project.Blueprints.First(item => item.Kind == BlueprintKind.Creature);
        var prop = project.Blueprints.FirstOrDefault(item => item.Kind == BlueprintKind.Placeable) ?? creature;
        var objective = new QuestAuthoringObjective(Id("quest:objective"), 1, p, 1,
            QuestObjectiveKind.Boolean,
            new LocalizedText($"quest.{source.Slug}.objective.production", "Achever l’objectif narratif",
                provenance: p), 1, [], null, 0, [], null, false);
        var counterObjective = new QuestAuthoringObjective(Id("quest:objective:counter"), 1, p, 2,
            QuestObjectiveKind.Counter,
            new LocalizedText($"quest.{source.Slug}.objective.progress", "Faire progresser l’enquête",
                provenance: p), 3, [], null, 0, [], null, false);
        var participant = new QuestAuthoringParticipant(Id("quest:participant"), 1, p, 1, "guide",
            QuestParticipantBindingKind.Blueprint, creature.Slug, ["quest:active"], true);
        var authoring = new QuestAuthoringProfile(Id("quest:authoring"), 1, p, true, true,
            [objective, counterObjective],
            new QuestAuthoringRewardBundle(Id("quest:rewards"), 1, p, 10,
                new Dictionary<string, int>(), new Dictionary<string, decimal>(),
                new Dictionary<string, double>(), ["narrative:complete"], []),
            new QuestAuthorNotes("Structurer la progression narrative.", "Respecter le canon du module.",
                "narrative-author", QuestEditorialStatus.Review, ["Vérifier les dépendances."],
                ["Jouer le parcours nominal."], ["PROJECT_CONTEXT.md"],
                [new QuestAuthorReview(Id("quest:review"), 1, p, "reviewer", QuestEditorialStatus.Review,
                    "Parcours prêt pour validation.", DateTimeOffset.UnixEpoch)]), [participant],
            new QuestPlotWizardPlan(Id("quest:wizard"), 1, p, [area.Slug], [participant.Id], [prop.Slug],
                [dialogue.Id], [objective.Id, counterObjective.Id], source.Transitions.Select(item => item.Id).ToArray(),
                ["nominal", "failure", "repeat", "multiplayer", "save-resume"], false));
        var operations = QuestOperationsProfile.CreateDefault(source.Id.Value, p);
        var exclusiveChoices = source.Transitions.Count == 0
            ? []
            : new[]
            {
                new QuestExclusiveChoicePolicy(Id("quest:exclusive"), source.Transitions[0].Id,
                    "narrative-path", true,
                    new LocalizedText($"quest.{source.Slug}.exclusive.confirm", "Confirmer ce choix irréversible ?",
                        provenance: p),
                    new LocalizedText($"quest.{source.Slug}.exclusive.consequence",
                        "Cette voie ferme une autre possibilité.", provenance: p),
                    source.Transitions.Skip(1).Take(1).Select(item => item.Id).ToArray(), p),
            };
        operations = operations with
        {
            Journal = operations.Journal with { Priority = source.Priority == 0 ? 50 : source.Priority },
            Repeat = new QuestRepeatPolicy(true, 3_600, 3, QuestRepeatResetMode.Full, [], 0.75, 505),
            Share = new QuestSharePolicy(QuestShareMode.PartyShared, true,
                QuestSeparationPolicy.SnapshotIndividual, true),
            ExclusiveChoices = exclusiveChoices,
            Notification = operations.Notification with
            {
                Body = new LocalizedText($"quest.{source.Slug}.notification", "Journal de quête mis à jour.",
                    provenance: p),
                AccessibleText = "Journal de quête mis à jour.",
            },
        };
        var baseQuest = new QuestDefinition(source.Id, source.SchemaVersion, p, source.Slug, source.DisplayName,
            source.Title, source.Description, source.Category, source.Priority, source.Owner,
            source.RequiredAtRuntime,
            new QuestInstancePolicy(QuestProgressScope.Party,
                [QuestProgressScope.Player, QuestProgressScope.Party], true),
            source.InitialStateId, source.States,
            source.Transitions, operations, source.Simulation, authoring, null, null);
        var experience = new QuestExperienceProfile(Id("quest:experience"), 1, p, true, true,
            [new QuestExperienceResource(Id("quest:resource"), 1, p, 1, QuestExperienceResourceKind.Zone,
                area.Slug, "module", true, ["quest:active"])],
            [new QuestExperienceStep(Id("quest:step"), 1, p, 1,
                new LocalizedText($"quest.{source.Slug}.step.production", "Suivre l’étape narrative", provenance: p),
                [objective.Id], source.Transitions.Select(item => item.Id).ToArray(), "narrative", 1, true)],
            new QuestExperienceVisibilityPolicy(Id("quest:visibility"), 1, p,
                QuestExperienceVisibility.Hidden,
                new Dictionary<QuestExperienceVisibility, IReadOnlyList<string>>
                {
                    [QuestExperienceVisibility.Rumor] = ["heard"],
                    [QuestExperienceVisibility.Discovered] = ["heard", "seen"],
                }, true),
            [new QuestExperienceHistoryEvent(Id("quest:history"), 1, p, source.Id,
                "hidden", "discovered", "dialogue", "player:preview", DateTimeOffset.UnixEpoch,
                new Dictionary<string, string> { ["source"] = "studio" },
                new LocalizedText($"quest.{source.Slug}.history.production", "La piste est révélée.",
                    provenance: p))],
            [new QuestExperienceMarker(Id("quest:marker"), 1, p, 1, objective.Id,
                QuestExperienceMarkerBindingKind.Zone, area.Slug, QuestExperienceVisibility.Discovered,
                4, true, new LocalizedText($"quest.{source.Slug}.marker.production", area.DisplayName,
                    provenance: p), Vector3Value.Zero)]);
        var withExperience = baseQuest.WithExperience(experience);
        var plan = QuestQualityRuntimeService.CreateAiPlan(withExperience,
            new LocalizedText($"quest.{source.Slug}.ai.synopsis", "Proposition de quête contrôlée.", provenance: p),
            BuildQuestAiDependenciesUx5B(withExperience), ["PROJECT_CONTEXT.md"], ["style:module"],
            "narrative-author", Id("quest:ai:transaction"),
            $"preview:quest:{source.Id}|states:{source.States.Count}");
        plan = QuestQualityRuntimeService.Approve(plan, "narrative-author", DateTimeOffset.UnixEpoch);
        return withExperience.WithQuality(new QuestQualityProfile(Id("quest:quality"), 1, p, true, true,
            QuestQualityRuntimeService.GenerateTests(withExperience, 505), plan));
    }

    private static IReadOnlyList<QuestAiDependency> BuildQuestAiDependenciesUx5B(QuestDefinition quest)
    {
        var participant = quest.Authoring!.Participants[0];
        var zone = quest.Authoring.Wizard.ZoneIds[0];
        return
        [
            new QuestAiDependency(StableGuidUx5B(quest.Id.Value, "quest:dependency:state"), 1,
                "studio:ux5-c", 1, QuestAiDependencyKind.State, quest.InitialStateId.ToString()),
            new QuestAiDependency(StableGuidUx5B(quest.Id.Value, "quest:dependency:participant"), 1,
                "studio:ux5-c", 2, QuestAiDependencyKind.Participant, participant.Id.ToString("D")),
            new QuestAiDependency(StableGuidUx5B(quest.Id.Value, "quest:dependency:zone"), 1,
                "studio:ux5-c", 3, QuestAiDependencyKind.Zone, zone),
        ];
    }

    private static LocalizationCatalog CompleteLocalizationUx5B(LocalizationCatalog source)
    {
        var locales = source.Locales.Append("fr-FR").Append("en-US")
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var messages = source.Messages.Select(item =>
        {
            var translations = locales.ToDictionary(locale => locale, _ => item.Fallback,
                StringComparer.OrdinalIgnoreCase);
            return new LocalizationMessage(item.Key, item.Fallback, translations, item.Provenance);
        }).ToList();
        if (messages.All(item => !string.Equals(item.Key, "audio.important.subtitle", StringComparison.Ordinal)))
            messages.Add(new LocalizationMessage("audio.important.subtitle", "Son important",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["fr-FR"] = "Son important",
                    ["en-US"] = "Important sound",
                }, "studio:ux5-d"));
        return new LocalizationCatalog(source.Id, source.DefaultLocale, locales, messages,
            source.SchemaVersion, source.PackageNamespace, source.LicenseSpdx, "studio:ux5-d",
            source.PlatformByteBudget);
    }

    private static EditorWorkspaceProfile CopyWorkspaceUx5B(EditorWorkspaceProfile source,
        string fontFamily, double textScale) => new(source.Id, source.Name, source.Role, source.DisplayFilter,
        source.RepeatPlacement, source.OpenAreaTabs, source.ActiveAreaId, source.ComparisonAreaId,
        source.ContextualHelpEnabled, "studio:ux5-d", source.SchemaVersion, source.UiScale, textScale,
        fontFamily, source.FallbackFontFamilies, source.PersonalFavoriteBlueprintIds, source.RecentBlueprintIds,
        source.Shell, source.RepeatedPlacementProfile, source.MultiArea, source.Help, source.RolePolicy,
        source.NamedPaletteCollections);

    private static LoreIndexDefinition CompleteLoreUx5B(ProjectDefinition project, LoreIndexDefinition source,
        Guid? entryId)
    {
        var entries = source.Entries.ToList();
        if (entries.Count == 0)
        {
            var moduleEntryId = StableGuidUx5B(project.Id.Value, "lore:module");
            var narrativeEntryId = StableGuidUx5B(project.Id.Value, "lore:narrative");
            entries.Add(new LoreEntryDefinition(moduleEntryId, 1, LoreEntryKind.WorldBible, "module-context",
                project.Module.DisplayName, $"{project.Module.Description} Canon narratif du module.",
                [$"module:{project.Module.Id}"], [narrativeEntryId], ["module", "canon"], "studio:ux5-e"));
            var sourceId = project.Dialogues.Count > 0
                ? $"dialogue:{project.Dialogues[0].Id}"
                : $"project:{project.Id}";
            entries.Add(new LoreEntryDefinition(narrativeEntryId, 1, LoreEntryKind.TimelineEvent,
                "narrative-context", "Contexte narratif", "Références croisées et intentions des dialogues.",
                [sourceId], [], ["dialogue", "reference"], "studio:ux5-e"));
        }
        var selected = entryId is not null ? entries.FirstOrDefault(item => item.Id == entryId) : null;
        if (selected is not null)
        {
            entries.Remove(selected);
            entries.Insert(0, selected);
        }
        return new LoreIndexDefinition(source.Id, 1, true, Math.Max(12_000, source.MaximumContextCharacters),
            entries, "studio:ux5-e");
    }

    private static (ModuleCinematicProfile Module, AreaCinematicProfile Area) CompleteCinematicsUx5B(
        ProjectDefinition project, AreaDefinition area)
    {
        const string p = "studio:ux5-f";
        var current = project.Module.RuntimePolicies.Cinematics;
        var sequenceId = StableGuidUx5B(project.Module.Id.Value, "ux5-f:sequence");
        var dialogueId = project.Dialogues[0].Id.Value;
        var dialogueTrack = new SequenceTrackDefinition(StableGuidUx5B(sequenceId, "dialogue"),
            "Dialogue d’ouverture", SequenceTrackKind.Dialogue, SequenceTargetKind.Dialogue, dialogueId,
            string.Empty, [new SequenceKeyframe(StableGuidUx5B(sequenceId, "dialogue:key"), 250,
                null, "start", SequenceEasing.Linear)], p);
        var stateTrack = new SequenceTrackDefinition(StableGuidUx5B(sequenceId, "state"),
            "Verrouillage des contrôles", SequenceTrackKind.State, SequenceTargetKind.Blackboard, null,
            string.Empty,
            [new SequenceKeyframe(StableGuidUx5B(sequenceId, "lock"), 0, null,
                 "controls.locked=true", SequenceEasing.Linear),
             new SequenceKeyframe(StableGuidUx5B(sequenceId, "unlock"), 4_750, null,
                 "controls.locked=false", SequenceEasing.Linear)], p);
        var sequence = new SequenceDefinition(sequenceId, 1, "ux5-narrative-opening",
            "Ouverture narrative UX5", 5_000, true, false, [dialogueTrack, stateTrack], [], p);
        var sequences = current.Sequences.Where(item => item.Id != sequenceId).Append(sequence).ToArray();
        var startup = new StartupFlowDefinition(current.Startup.Id, 1, StartupFlowKind.Sequence, sequenceId,
            string.Empty, StartupFlowKind.Menu, null, string.Empty, true, "Passer", p);
        return (new ModuleCinematicProfile(current.Id, 1, startup, sequences, p,
                current.TransformOverrides, current.Tweens),
            new AreaCinematicProfile(area.RuntimePolicies.Cinematics.Id, 1,
                area.RuntimePolicies.Cinematics.CameraAnchors, area.RuntimePolicies.Cinematics.Provenance));
    }

    private void EnsureLoreHistoryUx5B()
    {
        if (_loreHistoryUx5B is not null) return;
        var loaded = Session is null ? null : LoreIndexStore.Load(Session.RootDirectory);
        _loreHistoryUx5B = new LoreIndexCommandHistory(loaded is { Succeeded: true }
            ? loaded.Index : LoreIndexDefinition.CreateDefault());
    }

    private bool SaveLoreUx5B() => Session is not null &&
        LoreIndexStore.Save(Session.RootDirectory, _loreHistoryUx5B!.State.Index).Succeeded;

    private static ProjectDefinition ReplaceDialogueUx5B(ProjectDefinition project, DialogueDefinition candidate) =>
        new(project.Id, project.Title, project.Module, project.Blueprints, project.Areas,
            project.Dialogues.Select(item => item.Id == candidate.Id ? candidate : item).ToArray(), project.Quests);

    private static ProjectDefinition ReplaceQuestUx5B(ProjectDefinition project, QuestDefinition candidate) =>
        new(project.Id, project.Title, project.Module, project.Blueprints, project.Areas, project.Dialogues,
            project.Quests.Select(item => item.Id == candidate.Id ? candidate : item).ToArray());

    private static ProjectDefinition ReplaceCinematicsUx5B(ProjectDefinition project,
        StudioCinematicProductionDraftUx5B draft)
    {
        var moduleRuntime = project.Module.RuntimePolicies;
        var nextModuleRuntime = new ModuleRuntimePolicyProfile(moduleRuntime.Id, moduleRuntime.SchemaVersion,
            moduleRuntime.Heartbeats, moduleRuntime.Rest, moduleRuntime.SequenceAbort, "cinematic-command",
            draft.ModuleCandidate, moduleRuntime.Gameplay, moduleRuntime.Rules, moduleRuntime.Extensions,
            moduleRuntime.GameplayEvents, moduleRuntime.World, moduleRuntime.LifecycleEvents);
        var areas = project.Areas.Select(area =>
        {
            if (area.Id != draft.AreaId) return area;
            var runtime = area.RuntimePolicies;
            var nextRuntime = new AreaRuntimePolicyProfile(runtime.Id, runtime.SchemaVersion,
                runtime.Heartbeats, runtime.Rest, "cinematic-command", draft.AreaCandidate,
                runtime.Rules, runtime.Experience);
            return area.WithRuntimePolicies(nextRuntime);
        }).ToArray();
        return new ProjectDefinition(project.Id, project.Title, project.Module.WithRuntimePolicies(nextModuleRuntime),
            project.Blueprints, areas, project.Dialogues, project.Quests);
    }

    private static StudioDialogueProductionDraftUx5B FreezeDialogueDraftUx5B(StudioDialogueProductionDraftUx5B value) => value;
    private static StudioQuestProductionDraftUx5B FreezeQuestDraftUx5B(StudioQuestProductionDraftUx5B value) => value;
    private static StudioLocalizationProductionDraftUx5B FreezeLocalizationDraftUx5B(StudioLocalizationProductionDraftUx5B value) => value;
    private static StudioLoreProductionDraftUx5B FreezeLoreDraftUx5B(StudioLoreProductionDraftUx5B value) => value;
    private static StudioCinematicProductionDraftUx5B FreezeCinematicDraftUx5B(StudioCinematicProductionDraftUx5B value) => value;

    private static string FingerprintProjectUx5B(ProjectDefinition project) => HashUx5B(
        ProjectJsonSerializer.Serialize(project));

    private static string FingerprintLocalizationDraftUx5B(StudioLocalizationProductionDraftUx5B draft) =>
        HashUx5B($"{draft.BaseRevision.Value}|{draft.SourceProjectFingerprint}|{draft.Candidate.ContentSha256}|" +
                 EditorWorkspaceProfileJsonSerializer.Serialize(draft.WorkspaceCandidate));

    private static string FingerprintLoreDraftUx5B(StudioLoreProductionDraftUx5B draft)
    {
        var value = new StringBuilder().Append(draft.BaseRevision.Value).Append('|')
            .Append(draft.SourceProjectFingerprint).Append('|').Append(draft.Candidate.Id).Append('|')
            .Append(draft.Candidate.MaximumContextCharacters).Append('|').Append(draft.Candidate.Provenance);
        foreach (var entry in draft.Candidate.Entries)
            value.Append("|E|").Append(entry.Id).Append('|').Append(entry.Kind).Append('|').Append(entry.Key)
                .Append('|').Append(entry.Title.Length).Append(':').Append(entry.Title)
                .Append('|').Append(entry.Content.Length).Append(':').Append(entry.Content)
                .Append('|').Append(string.Join(',', entry.SourceIds)).Append('|')
                .Append(string.Join(',', entry.RelatedEntryIds)).Append('|').Append(string.Join(',', entry.Tags));
        return HashUx5B(value.ToString());
    }

    private static string HashUx5B(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static Guid StableGuidUx5B(Guid seed, string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{seed:D}:{key}"));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guid);
        guid[7] = (byte)((guid[7] & 0x0f) | 0x50);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }

    private static string RequiredUx5B(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string IdentifierUx5B(string? value, string fallback)
    {
        var source = RequiredUx5B(value, fallback).ToLowerInvariant();
        var builder = new StringBuilder(source.Length);
        foreach (var character in source)
            builder.Append(char.IsLetterOrDigit(character) || character is '.' or '_' or '-'
                ? character : '-');
        var normalized = builder.ToString().Trim('-', '.', '_');
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static IReadOnlyList<string> SplitValuesUx5B(string? value) => (value ?? string.Empty)
        .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(item => IdentifierUx5B(item, "tag")).Distinct(StringComparer.Ordinal).ToArray();
}
