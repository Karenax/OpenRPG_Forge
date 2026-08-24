using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    internal StudioUx5BSmokeResult RunUx5BContractSmoke()
    {
        var evidence = CreateUx5BEvidenceProject();
        if (evidence is null) return FailedSmokeUx5B();
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux5b-contract-" + Guid.NewGuid().ToString("N"));
        StudioSessionController? reopened = null;
        var editing = new StudioSessionController();
        var released = false;
        try
        {
            var created = ProjectSession.Create(root, evidence.Project);
            if (!created.Succeeded || !editing.Open(root).Succeeded) return FailedSmokeUx5B();
            var initial = editing.Project!;
            var initialBlueprintIds = initial.Blueprints.Select(item => item.Id).ToArray();
            var initialAreaIds = initial.Areas.Select(item => item.Id).ToArray();
            var initialDialogueIds = initial.Dialogues.Select(item => item.Id).ToArray();
            var initialQuestIds = initial.Quests.Select(item => item.Id).ToArray();
            var dryRun = true;
            var undoRedo = true;
            var canonical = true;

            var dialogueDraft = editing.CreateDialogueProductionDraftUx5B(evidence.DialogueId.Value)!;
            var beforeDialogue = editing.Project;
            var beforeDialogueRevision = editing.History!.State.Revision;
            var dialoguePreview = editing.PreviewDialogueProductionUx5B(dialogueDraft);
            dryRun &= dialoguePreview.Succeeded && ReferenceEquals(beforeDialogue, editing.Project) &&
                      editing.History.State.Revision == beforeDialogueRevision;
            var dialogueApply = editing.ApplyApprovedNarrativeUx5B(StudioNarrativeSectionUx5B.Dialogue, dialogueDraft);
            canonical &= dialogueApply.Succeeded;
            var dialogueApplied = editing.Project!.Dialogues.Single(item => item.Id == evidence.DialogueId);
            var dialogueAuthoring = dialogueApplied.Authoring is { Enabled: true, Nodes.Count: > 0 };
            var dialoguePresentation = dialogueApplied.Presentation is
            {
                AnimationCues.Count: > 0, VoiceCues.Count: > 0, CameraShots.Count: > 0,
                TimedChoices.Count: > 0, VariantSets.Count: > 0
            };
            var dialogueExperience = dialogueApplied.Experience is { MemorySlots.Count: > 0, SkillChecks.Count: > 0 };
            var dialogueProduction = dialogueApplied.Production is
            { Subgraphs.Count: > 0, Invocations.Count: > 0, AiPlans.Count: > 0 };
            var dialogueSimulation = dialoguePreview.Metrics.Any(item => item.StartsWith("Simulation", StringComparison.Ordinal));
            undoRedo &= UndoRedoUx5B(editing, StudioNarrativeSectionUx5B.Dialogue);

            var questDraft = editing.CreateQuestProductionDraftUx5B(evidence.QuestId.Value)!;
            var beforeQuest = editing.Project;
            var beforeQuestRevision = editing.History.State.Revision;
            var questPreview = editing.PreviewQuestProductionUx5B(questDraft);
            dryRun &= questPreview.Succeeded && ReferenceEquals(beforeQuest, editing.Project) &&
                      editing.History.State.Revision == beforeQuestRevision;
            var questApply = editing.ApplyApprovedNarrativeUx5B(StudioNarrativeSectionUx5B.Quest, questDraft);
            canonical &= questApply.Succeeded;
            var questApplied = editing.Project!.Quests.Single(item => item.Id == evidence.QuestId);
            var questFoundation = questApplied.Operations is
            {
                Repeat.Enabled: true, Share.Mode: Forge.Domain.Quests.QuestShareMode.PartyShared,
                ExclusiveChoices.Count: > 0
            };
            var questAuthoring = questApplied.Authoring is
            { Objectives.Count: > 1, Participants.Count: > 0 } &&
                questApplied.Authoring.Objectives.Any(item =>
                    item.Kind == Forge.Domain.Quests.QuestObjectiveKind.Counter);
            var questExperience = questApplied.Experience is { Resources.Count: > 0, Steps.Count: > 0, Markers.Count: > 0 };
            var questQuality = questApplied.Quality is { TestScenarios.Count: 5 };
            var questSimulation = questPreview.Metrics.Any(item => item.StartsWith("Simulation", StringComparison.Ordinal));
            undoRedo &= UndoRedoUx5B(editing, StudioNarrativeSectionUx5B.Quest);

            var localizationDraft = editing.CreateLocalizationProductionDraftUx5B()!;
            var beforeLocalization = editing.Project;
            var beforeLocalizationRevision = editing.History.State.Revision;
            var localizationPreview = editing.PreviewLocalizationProductionUx5B(localizationDraft);
            dryRun &= localizationPreview.Succeeded && ReferenceEquals(beforeLocalization, editing.Project) &&
                      editing.History.State.Revision == beforeLocalizationRevision;
            var localizationApply = editing.ApplyApprovedNarrativeUx5B(
                StudioNarrativeSectionUx5B.Localization, localizationDraft);
            canonical &= localizationApply.Succeeded;
            var localizationCatalog = editing.Project!.Module.Localization.Messages.Count > 0 &&
                                      editing.Project.Module.Localization.Locales.Contains("fr-FR", StringComparer.OrdinalIgnoreCase);
            var accessibleTypography = editing.WorkspaceProfile.TextScale >= 1 &&
                                       !string.IsNullOrWhiteSpace(editing.WorkspaceProfile.PrimaryFontFamily);
            undoRedo &= UndoRedoUx5B(editing, StudioNarrativeSectionUx5B.Localization);

            var loreDraft = editing.CreateLoreProductionDraftUx5B()!;
            var lorePreview = editing.PreviewLoreProductionUx5B(loreDraft, "module");
            dryRun &= lorePreview.Succeeded;
            var loreApply = editing.ApplyApprovedNarrativeUx5B(StudioNarrativeSectionUx5B.Lore, loreDraft);
            canonical &= loreApply.Succeeded;
            editing.EnsureLoreHistoryUx5B();
            var loreIndex = editing._loreHistoryUx5B!.State.Index.Entries.Count >= 2;
            var loreGraph = editing._loreHistoryUx5B.State.Index.Entries.Any(item => item.RelatedEntryIds.Count > 0);
            undoRedo &= UndoRedoUx5B(editing, StudioNarrativeSectionUx5B.Lore);

            var cinematicDraft = editing.CreateCinematicProductionDraftUx5B(evidence.AreaId.Value)!;
            var beforeCinematic = editing.Project;
            var beforeCinematicRevision = editing.History.State.Revision;
            var cinematicPreview = editing.PreviewCinematicProductionUx5B(cinematicDraft);
            dryRun &= cinematicPreview.Succeeded && ReferenceEquals(beforeCinematic, editing.Project) &&
                      editing.History.State.Revision == beforeCinematicRevision;
            var cinematicApply = editing.ApplyApprovedNarrativeUx5B(
                StudioNarrativeSectionUx5B.Cinematic, cinematicDraft);
            canonical &= cinematicApply.Succeeded;
            var cinematic = editing.Project!.Module.RuntimePolicies.Cinematics;
            var sequence = cinematic.Sequences.Single(item => item.Slug == "ux5-narrative-opening");
            var cinematicStartup = cinematic.Startup.ReferenceId == sequence.Id;
            var cinematicTimeline = sequence.Tracks.Any(item => item.Kind == Forge.Domain.Sequences.SequenceTrackKind.Dialogue);
            var controlLocks = sequence.Tracks.Where(item => item.Kind == Forge.Domain.Sequences.SequenceTrackKind.State)
                .SelectMany(item => item.Keyframes).Select(item => item.Value).Contains("controls.locked=false", StringComparer.Ordinal);
            undoRedo &= UndoRedoUx5B(editing, StudioNarrativeSectionUx5B.Cinematic);

            var staleDraft = editing.CreateDialogueProductionDraftUx5B(evidence.DialogueId.Value)!;
            var staleSource = !editing.PreviewDialogueProductionUx5B(staleDraft with
            {
                BaseRevision = staleDraft.BaseRevision.Next(),
            }).Succeeded;
            var approved = editing.CreateDialogueProductionDraftUx5B(evidence.DialogueId.Value)!;
            var approvedPreview = editing.PreviewDialogueProductionUx5B(approved);
            var tampered = editing.ConfigureDialogueProductionDraftUx5B(approved, "different", "memory",
                "insight", 12, "changed after approval", "Sous-titre", 52, 8_000, "changed");
            var staleApproval = approvedPreview.Succeeded &&
                                !editing.ApplyApprovedNarrativeUx5B(StudioNarrativeSectionUx5B.Dialogue, tampered).Succeeded;

            var saved = editing.Save().Succeeded && editing.SaveWorkspaceProfile().Succeeded && editing.SaveLoreUx5B();
            var expectedJson = ProjectJsonSerializer.Serialize(editing.Project!);
            reopened = new StudioSessionController();
            var persistence = saved && reopened.Open(root).Succeeded && reopened.Project is not null &&
                              ProjectJsonSerializer.Serialize(reopened.Project) == expectedJson &&
                              LoreIndexStore.Load(root).Succeeded;
            reopened.Close(); reopened = null;
            var final = editing.Project!;
            var neighbors = final.Blueprints.Select(item => item.Id).SequenceEqual(initialBlueprintIds) &&
                            final.Areas.Select(item => item.Id).SequenceEqual(initialAreaIds) &&
                            final.Dialogues.Select(item => item.Id).SequenceEqual(initialDialogueIds) &&
                            final.Quests.Select(item => item.Id).SequenceEqual(initialQuestIds);
            var succeeded = dialogueAuthoring && dialoguePresentation && dialogueExperience && dialogueProduction &&
                            dialogueSimulation && questFoundation && questAuthoring && questExperience && questQuality &&
                            questSimulation && localizationCatalog && accessibleTypography && loreIndex && loreGraph &&
                            cinematicStartup && cinematicTimeline && controlLocks && dryRun && staleSource &&
                            staleApproval && canonical && undoRedo && persistence && neighbors;
            editing.Close();
            if (Directory.Exists(root)) Directory.Delete(root, true);
            released = !Directory.Exists(root);
            succeeded &= released;
            return new StudioUx5BSmokeResult(succeeded, dialogueAuthoring, dialoguePresentation,
                dialogueExperience, dialogueProduction, dialogueSimulation, questFoundation, questAuthoring,
                questExperience, questQuality, questSimulation, localizationCatalog, accessibleTypography,
                loreIndex, loreGraph, cinematicStartup, cinematicTimeline, controlLocks, dryRun, staleSource,
                staleApproval, canonical, undoRedo, persistence, neighbors, released);
        }
        catch
        {
            return FailedSmokeUx5B();
        }
        finally
        {
            reopened?.Close();
            editing.Close();
            try { if (Directory.Exists(root)) Directory.Delete(root, true); released = !Directory.Exists(root); }
            catch { released = false; }
        }
    }

    private static bool UndoRedoUx5B(StudioSessionController controller, StudioNarrativeSectionUx5B section)
    {
        object? applied = section == StudioNarrativeSectionUx5B.Lore
            ? controller._loreHistoryUx5B!.State.Index
            : controller.Project;
        var undo = controller.UndoNarrativeUx5B(section);
        var redo = controller.RedoNarrativeUx5B(section);
        return undo.Succeeded && redo.Succeeded && (section == StudioNarrativeSectionUx5B.Lore
            ? Equals(applied, controller._loreHistoryUx5B!.State.Index)
            : ReferenceEquals(applied, controller.Project));
    }

    private static StudioUx5BSmokeResult FailedSmokeUx5B() => new(false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false);
}
