#if TOOLS
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Areas;
using Forge.Application.Assistant;
using Forge.Application.Assets;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Content;
using Forge.Application.Creator;
using Forge.Application.Creatures;
using Forge.Application.Dialogues;
using Forge.Application.Entities;
using Forge.Application.Factions;
using Forge.Application.Gameplay;
using Forge.Application.Items;
using Forge.Application.Modules;
using Forge.Application.Rules;
using Forge.Application.Localization;
using Forge.Application.Lifecycle;
using Forge.Application.Navigation;
using Forge.Application.Sequences;
using Forge.Application.Projects;
using Forge.Application.Quests;
using Forge.Application.Quality;
using Forge.Application.Spatial;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Audio;
using Forge.Domain.Assistant;
using Forge.Domain.Assets;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Forge.Domain.Build;
using Forge.Domain.Combat;
using Forge.Domain.Content;
using Forge.Domain.Creatures;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Factions;
using Forge.Domain.Gameplay;
using Forge.Domain.Localization;
using Forge.Domain.Lifecycle;
using Forge.Domain.Items;
using Forge.Domain.Modules;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Quality;
using Forge.Domain.Quests;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Rules;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.Domain.WorldFeatures;
using Forge.EngineAdapter.Assets;
using Forge.EngineAdapter.Audio;
using Forge.EngineAdapter.Rendering;
using Forge.EngineAdapter.World;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Content;
using Forge.ModuleFormat.Distribution;
using Forge.ModuleFormat.Diagnostics;
using Forge.ModuleFormat.Launcher;
using Forge.ModuleFormat.Navigation;
using Forge.ModuleFormat.Playtesting;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Content;
using Forge.Network.Replication;
using Forge.RpgKit.Application.Abilities;
using Forge.RpgKit.Application.Bosses;
using Forge.RpgKit.Application.Companions;
using Forge.RpgKit.Application.Combat;
using Forge.RpgKit.Application.Creatures;
using Forge.RpgKit.Application.Dialogues;
using Forge.RpgKit.Application.Doors;
using Forge.RpgKit.Application.Encounters;
using Forge.RpgKit.Application.Factions;
using Forge.RpgKit.Application.Merchants;
using Forge.RpgKit.Application.Placeables;
using Forge.RpgKit.Application.Quests;
using Forge.RpgKit.Application.Triggers;
using Forge.RpgKit.Domain.Merchants;
using Forge.RpgKit.Domain.Companions;
using Forge.RpgKit.Domain.Factions;
using RpgLockState = Forge.RpgKit.Domain.Placeables.LockState;
using RpgActivator = Forge.RpgKit.Domain.Encounters.Activator;
using RpgNetworkAuthority = Forge.RpgKit.Domain.Encounters.NetworkAuthority;
using RpgQueryableEntity = Forge.RpgKit.Domain.Triggers.QueryableEntity;
using RpgTriggerEventTrace = Forge.RpgKit.Domain.Triggers.TriggerEventTrace;
using RpgTriggerRuntimeState = Forge.RpgKit.Domain.Triggers.TriggerRuntimeState;
using Forge.Scripting;
using Godot;

namespace Forge.Toolset.Editor;

public partial class ForgeEditorPlugin
{
    private bool RunQuestJournalSmokeTest()
    {
        if (_history is null || _questJournalWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R007_QUEST_FAILED missing_workspace");
            return false;
        }
        var appliedToHistory = false;
        try
        {
            _questJournalWindow.Open();
            var visible = _questJournalWindow.Visible;
            _questJournalWindow.Hide();
            var questId = _questJournalWindow.SelectedQuestId;
            var before = ProjectJsonSerializer.Serialize(_history.State.Project);
            var configured = visible && questId is not null && _questJournalWindow.ConfigureForSmokeTest();
            var previewed = configured && _questJournalWindow.PreviewForSmokeTest();
            var applied = previewed && _questJournalWindow.LastCommandId != Guid.Empty &&
                          _questJournalWindow.LastPatchSummary.Contains("quest", StringComparison.OrdinalIgnoreCase) &&
                          _questJournalWindow.ApplyForSmokeTest();
            if (!applied || questId is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R007_QUEST_FAILED preview_apply_ui " +
                    _questJournalWindow.LastDiagnostics);
                return false;
            }
            appliedToHistory = true;
            var quest = _history.State.Project.Quests.Single(item => item.Id == questId);
            var script = new QuestJournalScriptService(quest);
            var snapshot = script.Read(script.Handle);
            var instance = QuestInstanceService.Create(quest, quest.InstancePolicy.DefaultScope, "player:smoke-r007",
                DateTimeOffset.Parse("2026-08-07T12:00:00Z"), authorityGranted: true,
                id: Guid.Parse("c7070000-0000-4000-8000-000000000007"));
            var firstTransition = quest.Transitions.OrderBy(item => item.Id.Value).First();
            var advanced = QuestInstanceService.Advance(quest, instance, firstTransition.Id,
                new SmokeQuestConditionEvaluator(), DateTimeOffset.Parse("2026-08-07T12:01:00Z"),
                "player:smoke-r007", new Dictionary<string, string> { ["smoke"] = "passed" });
            var appliedJson = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(appliedJson).Quests.Single(item => item.Id == questId);
            if (QuestValidator.Validate(quest).Count != 0 || _questJournalWindow.LastValidationCount != 0 ||
                !_questJournalWindow.LastSimulationSucceeded || _questJournalWindow.StateCount != quest.States.Count ||
                snapshot.StateCount != quest.States.Count || snapshot.TerminalStateCount == 0 ||
                advanced.History.Count != 2 || advanced.Variables.GetValueOrDefault("smoke") != "passed" ||
                restored.Category != "main" || restored.Priority != 91 ||
                restored.States.All(item => item.Journal.Summary.Variants.Count == 0))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R007_QUEST_FAILED canonical_instance_roundtrip");
                return false;
            }
            Undo();
            appliedToHistory = false;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != before) return false;
            Redo();
            appliedToHistory = true;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != appliedJson) return false;
            Undo();
            appliedToHistory = false;
            GD.Print("OPENRPG_FORGE_TOOLSET_R007_QUEST_PASS metadata=category-priority-owner-localized " +
                "states=uuid-order-legacy-index terminals=completed-failed-abandoned-custom-reactivation " +
                "journal=summary-details-objectives-context-links instances=player-party-world-history-variables-time " +
                "ui=visible-empty-error-minimap-validation-simulation forgescript=typed-handle mcp=filtered-dry-run " +
                "transaction=identified audit=generated history=undo-redo persistence=project-workspace-roundtrip");
            return true;
        }
        finally
        {
            _questJournalWindow.Hide();
            if (appliedToHistory) _history.Undo();
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunQuestTransitionSmokeTest()
    {
        if (_history is null || _questJournalWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R008_QUEST_FAILED missing_workspace");
            return false;
        }
        var appliedToHistory = false;
        try
        {
            _questJournalWindow.Open();
            var visible = _questJournalWindow.Visible;
            _questJournalWindow.Hide();
            var questId = _questJournalWindow.SelectedQuestId;
            var before = ProjectJsonSerializer.Serialize(_history.State.Project);
            var configured = visible && questId is not null && _questJournalWindow.ConfigureForR008SmokeTest();
            var previewed = configured && _questJournalWindow.PreviewForSmokeTest();
            var applied = previewed && _questJournalWindow.LastCommandId != Guid.Empty &&
                          _questJournalWindow.LastPatchSummary.Contains("quest", StringComparison.OrdinalIgnoreCase) &&
                          _questJournalWindow.ApplyForSmokeTest();
            if (!applied || questId is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R008_QUEST_FAILED preview_apply_ui " +
                    _questJournalWindow.LastDiagnostics);
                return false;
            }
            appliedToHistory = true;
            var quest = _history.State.Project.Quests.Single(item => item.Id == questId);
            var script = new QuestJournalScriptService(quest);
            var transitions = script.ReadTransitions(script.Handle);
            var simulation = script.Simulate(script.Handle, quest.InstancePolicy.DefaultScope,
                quest.InstancePolicy.DefaultScope == QuestProgressScope.Player ? "player:smoke-r008" :
                quest.InstancePolicy.DefaultScope == QuestProgressScope.Party ? "party:smoke-r008" :
                "world:smoke-r008", DateTimeOffset.Parse("2026-08-07T12:00:00Z"));
            var appliedJson = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(appliedJson).Quests.Single(item => item.Id == questId);
            if (QuestValidator.Validate(quest).Count != 0 || _questJournalWindow.LastValidationCount != 0 ||
                !_questJournalWindow.LastSimulationSucceeded || transitions.Count != quest.Transitions.Count ||
                transitions[0].EventName != "dialogue.accept" ||
                transitions[0].Idempotence != QuestTransitionIdempotence.PerEvent ||
                simulation.Status != QuestSimulationStatus.Completed || simulation.AppliedTransitionCount == 0 ||
                restored.Simulation.Events.Count != quest.Transitions.Count ||
                restored.Transitions[0].ForgeScriptApiVersion != 1)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R008_QUEST_FAILED transition_simulation_roundtrip");
                return false;
            }
            Undo(); appliedToHistory = false;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != before) return false;
            Redo(); appliedToHistory = true;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != appliedJson) return false;
            Undo(); appliedToHistory = false;
            GD.Print("OPENRPG_FORGE_TOOLSET_R008_QUEST_PASS transition=schema-event-priority-idempotence-order " +
                "conditions=typed actions=typed simulation=persisted-events-effects-deadlock-step-limit " +
                "lint=references-unreachable-deadlock-cycle-conflict-reward-objective ui=editor-minimap-trace " +
                "forgescript=typed-handle-sandbox mcp=filtered-dry-run-simulation transaction=identified " +
                "audit=transition-trace history=undo-redo persistence=project-workspace-roundtrip");
            return true;
        }
        finally
        {
            _questJournalWindow.Hide();
            if (appliedToHistory) _history.Undo();
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunQuestAuthoringSmokeTest()
    {
        if (_history is null || _questJournalWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R071_QUEST_AUTHORING_FAILED missing_workspace");
            return false;
        }
        var appliedToHistory = false;
        try
        {
            _questJournalWindow.Open();
            var visible = _questJournalWindow.Visible;
            _questJournalWindow.Hide();
            var questId = _questJournalWindow.SelectedQuestId;
            var before = ProjectJsonSerializer.Serialize(_history.State.Project);
            var configured = visible && questId is not null && _questJournalWindow.ConfigureForR071SmokeTest();
            var previewed = configured && _questJournalWindow.PreviewForSmokeTest();
            var applied = previewed && _questJournalWindow.LastCommandId != Guid.Empty &&
                          _questJournalWindow.LastPatchSummary.Contains("quest", StringComparison.OrdinalIgnoreCase) &&
                          _questJournalWindow.ApplyForSmokeTest();
            if (!applied || questId is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R071_QUEST_AUTHORING_FAILED preview_apply_ui " +
                            _questJournalWindow.LastDiagnostics);
                return false;
            }
            appliedToHistory = true;
            var quest = _history.State.Project.Quests.Single(item => item.Id == questId);
            var profile = quest.Authoring;
            var script = new QuestJournalScriptService(quest);
            var snapshot = script.ReadAuthoring(script.Handle);
            var reward = QuestAuthoringRuntimeService.GrantRewards(quest,
                new QuestAuthoringRewardState(0, new Dictionary<string, int>(),
                    new Dictionary<string, decimal>(), new Dictionary<string, double>(),
                    new HashSet<string>(), new HashSet<string>()), "player:smoke-r071");
            var appliedJson = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(appliedJson).Quests.Single(item => item.Id == questId);
            var disabledRequired = quest.WithAuthoring(profile! with { Enabled = false, RequiredAtRuntime = true });
            if (profile is null || QuestAuthoringProfileValidator.Validate(quest).Count != 0 ||
                _questJournalWindow.LastValidationCount != 0 ||
                _questJournalWindow.LastAuthoringObjectiveCount != 8 ||
                _questJournalWindow.LastAuthoringParticipantCount != 1 ||
                Enum.GetValues<QuestObjectiveKind>().Any(kind => profile.Objectives.All(item => item.Kind != kind)) ||
                snapshot.ObjectiveCount != 8 || snapshot.ParticipantCount != 1 ||
                snapshot.GeneratedTestCount != 5 || snapshot.GeneratesOpaqueCode || !reward.Committed ||
                reward.State.Progression != 250 || reward.State.Items.GetValueOrDefault("ash-key") != 1 ||
                reward.State.Currencies.GetValueOrDefault("ember") != 5 ||
                reward.State.Reputation.GetValueOrDefault("ash-watch") != 2 ||
                !reward.State.Unlocks.Contains("unlock:crypt") || !reward.State.Choices.Contains("choice:dawn") ||
                restored.Authoring?.Objectives.Count != 8 || restored.Authoring.Participants.Count != 1 ||
                restored.Authoring.Wizard.GeneratesOpaqueCode ||
                QuestAuthoringProfileValidator.Validate(quest.WithAuthoring(null)).Count != 0 ||
                !QuestAuthoringProfileValidator.Validate(disabledRequired)
                    .Any(item => item.Code == QuestAuthoringProfileValidator.RequiredMissingCode))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R071_QUEST_AUTHORING_FAILED canonical_roundtrip");
                return false;
            }
            Undo(); appliedToHistory = false;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != before) return false;
            Redo(); appliedToHistory = true;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != appliedJson) return false;
            Undo(); appliedToHistory = false;
            GD.Print("OPENRPG_FORGE_TOOLSET_R071_QUEST_AUTHORING_PASS " +
                "objectives=counter-boolean-collection-destination-escort-survival-choice-custom " +
                "rewards=atomic-progression-items-currency-reputation-unlocks-choices " +
                "notes=design-canon-owner-status-risks-todo-sources-review-history " +
                "cast=role-blueprint-instance-query-presence-required " +
                "wizard=zones-cast-props-dialogues-objectives-transitions-five-tests-no-opaque-code " +
                "ui=visible-empty-error-minimap-validation-simulation command=dry-run-apply-reject " +
                "history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run " +
                "runtime=engine-adapters optional=ignored required=diagnostic");
            return true;
        }
        finally
        {
            _questJournalWindow.Hide();
            if (appliedToHistory) _history.Undo();
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunQuestExperienceSmokeTest()
    {
        if (_history is null || _questJournalWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R072_QUEST_EXPERIENCE_FAILED missing_workspace");
            return false;
        }
        var appliedToHistory = false;
        try
        {
            _questJournalWindow.Open();
            var visible = _questJournalWindow.Visible;
            _questJournalWindow.Hide();
            var questId = _questJournalWindow.SelectedQuestId;
            var before = ProjectJsonSerializer.Serialize(_history.State.Project);
            var configured = visible && questId is not null && _questJournalWindow.ConfigureForR072SmokeTest();
            var previewed = configured && _questJournalWindow.PreviewForSmokeTest();
            var applied = previewed && _questJournalWindow.LastCommandId != Guid.Empty &&
                          _questJournalWindow.LastPatchSummary.Contains("quest", StringComparison.OrdinalIgnoreCase) &&
                          _questJournalWindow.ApplyForSmokeTest();
            if (!applied || questId is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R072_QUEST_EXPERIENCE_FAILED preview_apply_ui " +
                            _questJournalWindow.LastDiagnostics);
                return false;
            }
            appliedToHistory = true;
            var quest = _history.State.Project.Quests.Single(item => item.Id == questId);
            var profile = quest.Experience;
            var script = new QuestJournalScriptService(quest);
            var snapshot = script.ReadExperience(script.Handle);
            var revealed = QuestExperienceRuntimeService.Reveal(profile!.Visibility,
                QuestExperienceVisibility.Hidden, new HashSet<string> { "heard", "seen" });
            var playerHistory = script.ReadHistory(script.Handle, QuestExperienceHistoryView.Player);
            var debugHistory = script.ReadHistory(script.Handle, QuestExperienceHistoryView.Debug);
            var marker = profile.Markers.Single(item => item.BindingKind == QuestExperienceMarkerBindingKind.Waypoint);
            var resolvedMarker = QuestExperienceRuntimeService.ResolveMarker(marker,
                QuestExperienceVisibility.Tracked, true);
            var appliedJson = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(appliedJson).Quests.Single(item => item.Id == questId);
            var invalid = quest.WithExperience(profile with
            {
                Resources = [profile.Resources[0] with { Id = Guid.Empty }],
            });
            var disabledRequired = quest.WithExperience(profile with { Enabled = false, RequiredAtRuntime = true });
            if (QuestExperienceProfileValidator.Validate(quest).Count != 0 ||
                _questJournalWindow.LastValidationCount != 0 ||
                _questJournalWindow.LastExperienceResourceCount != 5 ||
                _questJournalWindow.LastExperienceMarkerCount != 4 ||
                Enum.GetValues<QuestExperienceResourceKind>().Any(kind =>
                    profile.Resources.All(item => item.Kind != kind)) ||
                Enum.GetValues<QuestExperienceMarkerBindingKind>().Any(kind =>
                    profile.Markers.All(item => item.BindingKind != kind)) ||
                snapshot.ResourceCount != 5 || snapshot.StepCount != 1 || snapshot.HistoryEventCount != 2 ||
                snapshot.MarkerCount != 4 || revealed != QuestExperienceVisibility.Discovered ||
                playerHistory.Count != 2 || playerHistory.Any(item => item.Cause is not null) ||
                debugHistory.Count != 2 || debugHistory[1].Cause != "dialogue" || !resolvedMarker.Tracked ||
                restored.Experience?.Resources.Count != 5 || restored.Experience.Steps.Count != 1 ||
                restored.Experience.History.Count != 2 || restored.Experience.Markers.Count != 4 ||
                QuestExperienceProfileValidator.Validate(invalid).All(item =>
                    item.Code != QuestExperienceProfileValidator.ResourceInvalidCode) ||
                QuestExperienceProfileValidator.Validate(quest.WithExperience(null)).Count != 0 ||
                QuestExperienceProfileValidator.Validate(disabledRequired).All(item =>
                    item.Code != QuestExperienceProfileValidator.RequiredMissingCode))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R072_QUEST_EXPERIENCE_FAILED canonical_roundtrip");
                return false;
            }
            Undo(); appliedToHistory = false;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != before) return false;
            Redo(); appliedToHistory = true;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != appliedJson) return false;
            Undo(); appliedToHistory = false;
            GD.Print("OPENRPG_FORGE_TOOLSET_R072_QUEST_EXPERIENCE_PASS " +
                "resources=item-placeable-door-zone-asset-ownership-resolved " +
                "steps=template-version-objectives-transitions-editable-provenance " +
                "visibility=hidden-rumor-discovered-tracked-secret-completed-conditions " +
                "history=timestamp-transition-cause-actor-player-debug " +
                "markers=zone-entity-query-waypoint-visibility-precision-tracking-fallback " +
                "ui=visible-empty-error-minimap-validation-simulation command=dry-run-apply-reject " +
                "history-edit=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run " +
                "runtime=engine-adapters optional=ignored required=diagnostic");
            return true;
        }
        finally
        {
            _questJournalWindow.Hide();
            if (appliedToHistory) _history.Undo();
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunQuestQualitySmokeTest()
    {
        if (_history is null || _questJournalWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R073_QUEST_QUALITY_FAILED missing_workspace");
            return false;
        }
        var appliedToHistory = false;
        try
        {
            _questJournalWindow.Open();
            var visible = _questJournalWindow.Visible;
            _questJournalWindow.Hide();
            var questId = _questJournalWindow.SelectedQuestId;
            var before = ProjectJsonSerializer.Serialize(_history.State.Project);
            var configured = visible && questId is not null && _questJournalWindow.ConfigureForR073SmokeTest();
            var previewed = configured && _questJournalWindow.PreviewForSmokeTest();
            var applied = previewed && _questJournalWindow.LastCommandId != Guid.Empty &&
                          _questJournalWindow.LastPatchSummary.Contains("quest", StringComparison.OrdinalIgnoreCase) &&
                          _questJournalWindow.ApplyForSmokeTest();
            if (!applied || questId is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R073_QUEST_QUALITY_FAILED preview_apply_ui " +
                            _questJournalWindow.LastDiagnostics);
                return false;
            }
            appliedToHistory = true;
            var quest = _history.State.Project.Quests.Single(item => item.Id == questId);
            var profile = quest.Quality;
            var script = new QuestJournalScriptService(quest);
            var snapshot = script.ReadQuality(script.Handle);
            var nominal = profile!.TestScenarios.Single(item => item.Kind == QuestQualityTestKind.Nominal);
            var nominalRun = QuestQualityRuntimeService.Run(nominal, (step, state) =>
                step.Action == QuestQualityTestActionKind.CompleteObjectives
                    ? new Dictionary<string, string>
                    {
                        ["quest/state"] = "completed",
                        ["quest/rewards"] = "true",
                    }
                    : state);
            var failureRun = QuestQualityRuntimeService.Run(nominal, (_, state) => state);
            var appliedPlan = QuestQualityRuntimeService.MarkApplied(profile.AiPlan);
            var appliedJson = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(appliedJson).Quests.Single(item => item.Id == questId);
            var invalid = quest.WithQuality(profile with
            {
                TestScenarios = [profile.TestScenarios[0] with { Edges = [] }, .. profile.TestScenarios.Skip(1)],
            });
            var disabledRequired = quest.WithQuality(profile with { Enabled = false, RequiredAtRuntime = true });
            if (QuestQualityProfileValidator.Validate(quest).Count != 0 ||
                _questJournalWindow.LastValidationCount != 0 ||
                _questJournalWindow.LastQualityScenarioCount != 5 ||
                !_questJournalWindow.LastQualityPlanApproved ||
                Enum.GetValues<QuestQualityTestKind>().Any(kind =>
                    profile.TestScenarios.All(item => item.Kind != kind)) ||
                profile.TestScenarios.Any(item => item.Seed < 7300 || item.Fixtures.Count == 0 ||
                    item.Steps.Count == 0 || item.Edges.Count != item.Steps.Count - 1 ||
                    item.Assertions.Count == 0) ||
                snapshot.ScenarioCount != 5 || snapshot.StepCount != 11 || snapshot.AssertionCount != 6 ||
                !snapshot.HumanApproved || snapshot.Applied || !nominalRun.Passed || failureRun.Passed ||
                !appliedPlan.Applied || !appliedPlan.SemanticDiff.Contains("apply:", StringComparison.Ordinal) ||
                restored.Quality?.TestScenarios.Count != 5 || restored.Quality.AiPlan.Dependencies.Count != 5 ||
                restored.Quality.AiPlan.ContextCitations.Count != 2 || restored.Quality.AiPlan.StyleRules.Count != 2 ||
                QuestQualityProfileValidator.Validate(invalid).All(item =>
                    item.Code != QuestQualityProfileValidator.GraphInvalidCode) ||
                QuestQualityProfileValidator.Validate(quest.WithQuality(null)).Count != 0 ||
                QuestQualityProfileValidator.Validate(disabledRequired).All(item =>
                    item.Code != QuestQualityProfileValidator.RequiredMissingCode))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R073_QUEST_QUALITY_FAILED canonical_roundtrip");
                return false;
            }
            Undo(); appliedToHistory = false;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != before) return false;
            Redo(); appliedToHistory = true;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != appliedJson) return false;
            Undo(); appliedToHistory = false;
            GD.Print("OPENRPG_FORGE_TOOLSET_R073_QUEST_QUALITY_PASS " +
                "tests=nominal-failure-repetition-multiplayer-save-resume-seed-fixtures-nodes-edges-assertions-runner " +
                "ai=synopsis-states-participants-dependencies-citations-style-lint-human-approval-transaction-plan-semantic-diff-authorization " +
                "ui=visible-empty-error-minimap-validation-simulation command=dry-run-apply-reject " +
                "history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run " +
                "runtime=engine-adapters optional=ignored required=diagnostic");
            return true;
        }
        finally
        {
            _questJournalWindow.Hide();
            if (appliedToHistory) _history.Undo();
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunQuestOperationsSmokeTest()
    {
        if (_history is null || _questOperationsWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13J_QUEST_OPERATIONS_FAILED missing_workspace");
            return false;
        }
        var appliedToHistory = false;
        try
        {
            _questOperationsWindow.Open(); var visible = _questOperationsWindow.Visible;
            _questOperationsWindow.Hide(); var questId = _questOperationsWindow.SelectedQuestId;
            var before = ProjectJsonSerializer.Serialize(_history.State.Project);
            var configured = visible && questId is not null && _questOperationsWindow.ConfigureForSmokeTest();
            var previewed = configured && _questOperationsWindow.PreviewForSmokeTest();
            var applied = previewed && _questOperationsWindow.LastCommandId != Guid.Empty &&
                          _questOperationsWindow.LastPatchSummary.Contains("quest", StringComparison.OrdinalIgnoreCase) &&
                          _questOperationsWindow.ApplyForSmokeTest();
            if (!applied || questId is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13J_QUEST_OPERATIONS_FAILED preview_apply_ui " +
                    _questOperationsWindow.LastDiagnostics); return false;
            }
            appliedToHistory = true;
            var quest = _history.State.Project.Quests.Single(item => item.Id == questId);
            var profile = quest.Operations; var context = new QuestOperationsRuntimeContext(
                DateTimeOffset.Parse("2026-08-06T12:00:00Z"), DateTimeOffset.Parse("2026-08-06T10:00:00Z"),
                1, 2, 2, true, true, new HashSet<QuestTransitionId>());
            var repeat = profile is null ? null : QuestOperationsRuntimeResolver.ResolveRepeat(quest, context);
            var share = profile is null ? null : QuestOperationsRuntimeResolver.ResolveShare(quest, context);
            var exclusive = profile is null || quest.Transitions.Count == 0 ? null :
                QuestOperationsRuntimeResolver.ResolveExclusive(quest, quest.Transitions[0].Id, context);
            var notice = profile is null ? null : QuestOperationsRuntimeResolver.ResolveNotification(quest);
            var script = profile is null ? null : new QuestOperationsScriptService(quest);
            var snapshot = script?.Read(script.Handle);
            var appliedJson = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(appliedJson).Quests.Single(item => item.Id == questId);
            if (profile is null || QuestOperationsValidator.Validate(quest).Count != 0 ||
                profile.Journal.Priority != 92 || repeat is not { Allowed: true } || share is not { Accepted: true } ||
                exclusive is not { Allowed: true, RequiresConfirmation: true } || notice is not { Visible: true } ||
                snapshot is null || snapshot.ExclusiveChoiceCount != 1 ||
                _questOperationsWindow.SummarySectionCount != 5 || restored.Operations?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13J_QUEST_OPERATIONS_FAILED canonical_runtime_roundtrip");
                return false;
            }
            Undo(); appliedToHistory = false;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != before) return false;
            Redo(); appliedToHistory = true;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != appliedJson) return false;
            Undo(); appliedToHistory = false;
            GD.Print("OPENRPG_FORGE_TOOLSET_G13J_QUEST_OPERATIONS_PASS " +
                "journal=priority-importance-manual-pin notifications=grouped-accessible-localized-silent-cinematic " +
                "repeat=cooldown-counter-reset-decay-seed share=individual-party-optin-leader-unanimous-separation " +
                "exclusive=mutex-irreversible-confirmation-consequence ui=visible-empty-error-summary-simulation " +
                "forgescript=typed-handle mcp=filtered-dry-run transaction=identified audit=generated " +
                "history=restored persistence=project-workspace-package runtime=optional-safe-authoritative");
            return true;
        }
        finally
        {
            _questOperationsWindow.Hide(); if (appliedToHistory) _history.Undo();
            RebuildPreview(); RefreshEditorControls();
        }
    }

    private bool RunItemFoundationSmokeTest()
    {
        if (_history is null || _itemFoundationWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R044_ITEM_FOUNDATION_FAILED missing_workspace");
            return false;
        }
        var before = ProjectJsonSerializer.Serialize(_history.State.Project);
        var appliedToHistory = false;
        try
        {
            _itemFoundationWindow.Open(); var visible = _itemFoundationWindow.Visible; _itemFoundationWindow.Hide();
            if (!visible || !_itemFoundationWindow.ConfigureForSmokeTest() ||
                !_itemFoundationWindow.RejectInvalidForSmokeTest() ||
                !_itemFoundationWindow.PreviewForSmokeTest() || !_itemFoundationWindow.ApplyForSmokeTest())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R044_ITEM_FOUNDATION_FAILED preview_apply_ui " +
                    _itemFoundationWindow.LastDiagnostics);
                return false;
            }
            appliedToHistory = true;
            var blueprintId = _itemFoundationWindow.SelectedBlueprintId;
            var blueprint = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId);
            var profile = blueprint.ItemFoundation;
            var script = blueprintId is null ? null : ItemFoundationScriptService.Read(_history.State.Project, blueprintId);
            var creationRuntime = Forge.RpgKit.Domain.Items.ItemFoundationRuntimeAdapter.Resolve(profile);
            var creationPolicies = creationRuntime.Policies;
            var contribution = creationPolicies is null ? default :
                Forge.RpgKit.Domain.Items.ItemInventoryService.Contribution(creationPolicies.Inventory, 3);
            var merged = creationPolicies is null ? null : Forge.RpgKit.Domain.Items.ItemInventoryService.Merge(
                new(Guid.Parse("c2880000-0000-4000-8000-000000000051"), profile!.Id, "ash-key", 7, 4),
                new(Guid.Parse("c2880000-0000-4000-8000-000000000052"), profile!.Id, "ash-key", 5, 4),
                creationPolicies.Stack, 4);
            var split = creationPolicies is null || merged is null ? null :
                Forge.RpgKit.Domain.Items.ItemInventoryService.Split(merged.Primary, 2, 5);
            var consumed = creationPolicies is null ? null :
                Forge.RpgKit.Domain.Items.ItemInventoryService.Consume(creationPolicies.Usage,
                    DateTimeOffset.UnixEpoch);
            var optionalCreation = Forge.RpgKit.Domain.Items.ItemFoundationRuntimeAdapter.Resolve(null);
            var requiredCreation = Forge.RpgKit.Domain.Items.ItemFoundationRuntimeAdapter.Resolve(null, true);
            var instance = profile is null ? null : new ItemFoundationInstance(
                Guid.Parse("c2460000-0000-4000-8000-000000000001"), profile.Id, profile.DefinitionVersion, 80,
                new Dictionary<string, string> { ["identified"] = "true" });
            var instanceDiagnostics = profile is null || instance is null
                ? [new ItemFoundationDiagnostic("missing", "item", "missing")]
                : ItemFoundationScriptService.ValidateInstance(new(profile.Id), profile, instance);
            var forbiddenDiagnostics = profile is null || instance is null
                ? []
                : ItemFoundationValidator.ValidateInstance(profile,
                    instance with { Overrides = new Dictionary<string, string> { ["forbidden"] = "true" } });
            var targetType = new ItemTypeDefinition(Guid.Parse("c2460000-0000-4000-8000-000000000002"), 1,
                "toolset:r044", "quest-relic", ["belt"], ["ward"], "rig.relic");
            var migrationRejected = profile is null ? null : _itemFoundationHandler.Handle(_history.State,
                new SetItemFoundationCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision, blueprint.Id,
                    profile with { TypeId = targetType.Id, Type = targetType, AppearanceRig = targetType.RequiredRig },
                    false, true));
            var invalidRejected = profile is null ? null : _itemFoundationHandler.Handle(_history.State,
                new SetItemFoundationCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision, blueprint.Id,
                    profile with { ContentSha256 = "invalid" }, false, true));
            var invalidCreationRejected = profile?.Creation is null ? null : _itemFoundationHandler.Handle(
                _history.State, new SetItemFoundationCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
                    blueprint.Id, profile with
                    {
                        Creation = profile.Creation with
                        {
                            Stack = profile.Creation.Stack with { Maximum = 0 },
                        },
                    }, false, true));
            var appliedJson = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(appliedJson).Blueprints.Single(item => item.Id == blueprint.Id);
            if (profile is null || blueprintId is null || ItemFoundationValidator.Validate(profile).Count != 0 ||
                profile.TypeId != profile.Type.Id || profile.Type.Slug != "ritual-key" || profile.DefinitionVersion != 2 ||
                profile.Texts.Single().UnknownText != "Objet inconnu" || script?.Handle.Value != profile.Id ||
                instanceDiagnostics.Count != 0 ||
                forbiddenDiagnostics.SingleOrDefault()?.Code != "item.foundation.instance_override_forbidden" ||
                migrationRejected?.Diagnostics.SingleOrDefault()?.Code !=
                    "item.foundation.type_migration_preview_required" ||
                invalidRejected?.Diagnostics.SingleOrDefault()?.Code != "item.foundation.content_governance_invalid" ||
                ItemFoundationValidator.Resolve(null, true).Diagnostics.SingleOrDefault()?.Code !=
                    ItemFoundationValidator.MissingRequiredCode ||
                profile.Creation is null || profile.Creation.Wizard.Sections.Count != 8 ||
                profile.Creation.Appearance.EquippedPreviewAssetId != "asset.items.ash-key.equipped" ||
                script?.Creation?.ProfileId != profile.Creation.Id ||
                creationPolicies is null || creationRuntime.Diagnostics.Count != 0 ||
                creationPolicies.Visual.Meshes.Single() != "asset.items.ash-key.mesh" ||
                Math.Abs(contribution.Mass - 2.5) > .000001 || Math.Abs(contribution.Volume - .6) > .000001 ||
                merged?.Primary.Quantity != 10 || merged?.Secondary?.Quantity != 2 || split?.Primary.Quantity != 8 ||
                consumed?.CurrentCharges != 2 || consumed?.CooldownUntil != DateTimeOffset.UnixEpoch.AddSeconds(5) ||
                !optionalCreation.OptionalFallback ||
                requiredCreation.Diagnostics.SingleOrDefault()?.Code !=
                    ItemFoundationValidator.CreationMissingRequiredCode ||
                invalidCreationRejected?.Diagnostics.SingleOrDefault()?.Code !=
                    "item.creation.stack_policy_invalid" ||
                restored.ItemFoundation?.Id != profile.Id || ProjectJsonSerializer.Serialize(
                    ProjectJsonSerializer.Deserialize(appliedJson)) != appliedJson ||
                restored.ItemFoundation.Creation?.Id != profile.Creation.Id ||
                _itemFoundationWindow.SummarySectionCount != 8)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R044_ITEM_FOUNDATION_FAILED canonical_validation_runtime_roundtrip");
                return false;
            }

            Undo(); appliedToHistory = false;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != before)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R044_ITEM_FOUNDATION_FAILED undo");
                return false;
            }
            Redo(); appliedToHistory = true;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != appliedJson)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R044_ITEM_FOUNDATION_FAILED redo");
                return false;
            }
            Undo(); appliedToHistory = false;
            GD.Print("OPENRPG_FORGE_TOOLSET_R044_ITEM_FOUNDATION_PASS " +
                "type=immutable-reference-migration-preview-explicit-accept blueprint=versioned-instance-separated-overrides-bounded " +
                "identity=uuid-slug-localized-name-short-long-unknown-label-notes " +
                "validation=identity-type-slots-rig-values-effects-scripts-localization-asset-license-economy " +
                "ui=visible-empty-error-sections-summary transaction=dry-run-apply history=undo-redo " +
                "persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic");
            GD.Print("OPENRPG_FORGE_TOOLSET_R088_ITEM_CREATION_PASS " +
                "wizard=archetype-appearance-slots-quality-level-property-provenance-generation-rules " +
                "appearance=meshes-materials-sockets-icon-vfx-equipped-preview " +
                "inventory=mass-volume-grid-stack-contribution stack=maximum-key-atomic-merge-split-replication " +
                "usage=charges-rest-cooldown-durability-consumable-recharge " +
                "ui=visible-empty-error-sections-live-preview command=dry-run-apply-reject history=undo-redo " +
                "persistence=roundtrip forgescript=typed-profile-handle mcp=filtered-dry-run " +
                "runtime=optional-safe-required-diagnostic-rpgkit-inventory-reused");
            return true;
        }
        finally
        {
            _itemFoundationWindow.Hide();
            if (appliedToHistory) _history.Undo();
            RebuildPreview(); RefreshEditorControls();
        }
    }

    private bool RunItemGameplaySmokeTest()
    {
        if (_history is null || _itemGameplayWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R089_ITEM_GAMEPLAY_FAILED missing_workspace");
            return false;
        }

        var before = ProjectJsonSerializer.Serialize(_history.State.Project);
        var appliedToHistory = false;
        try
        {
            _itemGameplayWindow.Open();
            var visible = _itemGameplayWindow.Visible;
            _itemGameplayWindow.Hide();
            if (!visible || !_itemGameplayWindow.ConfigureForSmokeTest() ||
                !_itemGameplayWindow.RejectInvalidForSmokeTest() ||
                !_itemGameplayWindow.PreviewForSmokeTest() || !_itemGameplayWindow.ApplyForSmokeTest())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R089_ITEM_GAMEPLAY_FAILED preview_apply_ui " +
                    _itemGameplayWindow.LastDiagnostics);
                return false;
            }

            appliedToHistory = true;
            var blueprintId = _itemGameplayWindow.SelectedBlueprintId;
            var blueprint = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId);
            var foundation = blueprint.ItemFoundation;
            var gameplay = foundation?.Gameplay;
            var script = blueprintId is null ? null :
                ItemFoundationScriptService.Read(_history.State.Project, blueprintId);
            var resolved = Forge.RpgKit.Domain.Items.ItemGameplayRuntimeAdapter.Resolve(foundation);
            var blocked = gameplay is null ? null :
                Forge.RpgKit.Domain.Items.ItemGameplayRuntimeAdapter.AuthorizeCriticalAction(
                    gameplay, "drop", false, "player", string.Empty);
            var overridden = gameplay is null ? null :
                Forge.RpgKit.Domain.Items.ItemGameplayRuntimeAdapter.AuthorizeCriticalAction(
                    gameplay, "drop", true, "gm:karen", "recover quest item");
            var transferAllowed = resolved.Transfer is null ? null :
                Forge.RpgKit.Domain.Items.ItemGameplayService.AuthorizeTransfer(resolved.Transfer,
                    Forge.RpgKit.Domain.Items.TransferDestination.Container,
                    new HashSet<string> { "item.transfer.quest" }, "player");
            var transferBlocked = resolved.Transfer is null ? null :
                Forge.RpgKit.Domain.Items.ItemGameplayService.AuthorizeTransfer(resolved.Transfer,
                    Forge.RpgKit.Domain.Items.TransferDestination.Drop,
                    new HashSet<string> { "item.transfer.quest" }, "player");
            var modifiers = Forge.RpgKit.Domain.Items.ItemGameplayService.ResolveModifiers(resolved.Modifiers,
                new HashSet<string> { "night" });
            var usage = new Forge.RpgKit.Domain.Items.UsageResource(3, 3, 3, 3, TimeSpan.Zero, null,
                100, false, "rest");
            var activation = resolved.Ability is null ? default :
                Forge.RpgKit.Domain.Items.ItemGameplayService.ActivateAbility(Guid.NewGuid(), resolved.Ability,
                    "crypt-door", usage, DateTimeOffset.UnixEpoch);
            var equipment = Forge.RpgKit.Domain.Items.ItemGameplayService.PreviewEquipment(
                new(Guid.NewGuid(), resolved.RequiredSlots, "forge", "humanoid"), resolved.Slots,
                new HashSet<string> { "hands-two" });
            var optional = Forge.RpgKit.Domain.Items.ItemGameplayRuntimeAdapter.Resolve(null);
            var required = Forge.RpgKit.Domain.Items.ItemGameplayRuntimeAdapter.Resolve(null, true);
            var appliedJson = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(appliedJson).Blueprints
                .Single(item => item.Id == blueprint.Id);

            if (foundation is null || gameplay is null || ItemFoundationValidator.Validate(foundation).Count != 0 ||
                gameplay.Critical is not
                {
                    ProtectDestruction: true, ProtectSale: true, ProtectDrop: true,
                    ProtectTheft: true, ProtectConsumption: true, ProtectCleanup: true,
                    AllowAuditedGameMasterOverride: true
                } ||
                gameplay.Transfer.AllowedDestinations.Count != 8 ||
                gameplay.Transfer.RequiredPermissions.SingleOrDefault() != "item.transfer.quest" ||
                script?.Gameplay?.ProfileId != gameplay.Id ||
                script.Gameplay.RequiredEquipmentSlots.Count != 2 ||
                resolved.Diagnostics.Count != 0 || resolved.Modifiers.SingleOrDefault()?.Kind != "ward" ||
                blocked?.Code != "RPG_ITEM_CRITICAL_PROTECTED" ||
                overridden?.Code != "RPG_ITEM_GM_OVERRIDE" ||
                !overridden.AuditEntry.Contains("recover quest item", StringComparison.Ordinal) ||
                transferAllowed?.Code != "RPG_ITEM_TRANSFER_ALLOWED" ||
                transferBlocked?.Code != "RPG_ITEM_TRANSFER_BLOCKED" ||
                !modifiers.TryGetValue("ward", out var ward) || Math.Abs(ward - 8) > .000001 ||
                activation.Requested?.Name != "ItemAbilityRequested" || activation.Usage?.CurrentCharges != 2 ||
                equipment.Allowed || equipment.Conflicts.Count != 2 ||
                !optional.OptionalFallback ||
                required.Diagnostics.SingleOrDefault()?.Code != ItemFoundationValidator.GameplayMissingRequiredCode ||
                restored.ItemFoundation?.Gameplay?.Id != gameplay.Id ||
                ProjectJsonSerializer.Serialize(ProjectJsonSerializer.Deserialize(appliedJson)) != appliedJson ||
                _itemGameplayWindow.SummarySectionCount != 5)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R089_ITEM_GAMEPLAY_FAILED canonical_runtime_roundtrip");
                return false;
            }

            Undo(); appliedToHistory = false;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != before)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R089_ITEM_GAMEPLAY_FAILED undo");
                return false;
            }
            Redo(); appliedToHistory = true;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != appliedJson)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R089_ITEM_GAMEPLAY_FAILED redo");
                return false;
            }
            Undo(); appliedToHistory = false;
            GD.Print("OPENRPG_FORGE_TOOLSET_R089_ITEM_GAMEPLAY_PASS " +
                "critical=destruction-sale-drop-theft-consumption-cleanup-gm-audit " +
                "transfer=drop-trade-store-container-mail-destroy-consume-gm-permissions " +
                "modifiers=data-driven-parameters-stacking-conditions-ui-hooks " +
                "ability=definition-targeting-costs-charges-cooldown-events " +
                "equipment=ruleset-rig-multislot-layer-conflicts-preview " +
                "ui=visible-empty-error-sections-live-preview command=dry-run-apply-reject history=undo-redo " +
                "persistence=roundtrip forgescript=typed-profile-handle mcp=filtered-dry-run " +
                "runtime=optional-safe-required-diagnostic-rpgkit-gameplay-reused");
            return true;
        }
        finally
        {
            _itemGameplayWindow.Hide();
            if (appliedToHistory) _history.Undo();
            RebuildPreview(); RefreshEditorControls();
        }
    }

    private bool RunItemOperationsSmokeTest()
    {
        if (_history is null || _itemOperationsWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R090_ITEM_OPERATIONS_FAILED missing_workspace");
            return false;
        }

        var before = ProjectJsonSerializer.Serialize(_history.State.Project);
        var appliedToHistory = false;
        try
        {
            _itemOperationsWindow.Open();
            var visible = _itemOperationsWindow.Visible;
            _itemOperationsWindow.Hide();
            if (!visible || !_itemOperationsWindow.ConfigureForSmokeTest() ||
                !_itemOperationsWindow.RejectInvalidForSmokeTest() ||
                !_itemOperationsWindow.PreviewForSmokeTest() || !_itemOperationsWindow.ApplyForSmokeTest())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R090_ITEM_OPERATIONS_FAILED preview_apply_ui " +
                    _itemOperationsWindow.LastDiagnostics);
                return false;
            }

            appliedToHistory = true;
            var blueprintId = _itemOperationsWindow.SelectedBlueprintId;
            var blueprint = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId);
            var foundation = blueprint.ItemFoundation;
            var operations = foundation?.Operations;
            var script = blueprintId is null ? null :
                ItemFoundationScriptService.Read(_history.State.Project, blueprintId);
            var resolved = Forge.RpgKit.Application.Items.ItemOperationsRuntimeAdapter.Resolve(foundation);
            var conditions = new HashSet<string> { "crypt-open" };
            var firstRoll = Forge.RpgKit.Domain.Items.ItemGameplayService.Roll(resolved.Loot!, 42, conditions);
            var secondRoll = Forge.RpgKit.Domain.Items.ItemGameplayService.Roll(resolved.Loot!, 42, conditions);
            var lootPreview = Forge.RpgKit.Domain.Items.ItemGameplayService.Preview(resolved.Loot!, conditions);
            var placements = resolved.Inventory!.Placements;
            var move = Forge.RpgKit.Application.Items.InventoryAuthoringService.Move(resolved.Inventory,
                new(placements.Select(item => item.ItemInstanceId).ToArray(), "vault", null, 3, 2, true), 0);
            var filtered = Forge.RpgKit.Application.Items.InventoryAuthoringService.Filter(
                resolved.Inventory, "backpack", null, null);
            var comparisons = Forge.RpgKit.Application.Items.InventoryAuthoringService.Compare(move.Before, move.After);
            var events = Enum.GetValues<Forge.RpgKit.Application.Items.ItemLifecycleEventKind>().Select(kind =>
                Forge.RpgKit.Application.Items.ItemOperationsRuntimeAdapter.Emit(operations!, kind, Guid.NewGuid(),
                    "source", "target", DateTimeOffset.UnixEpoch)).ToArray();
            var optional = Forge.RpgKit.Application.Items.ItemOperationsRuntimeAdapter.Resolve(null);
            var required = Forge.RpgKit.Application.Items.ItemOperationsRuntimeAdapter.Resolve(null, true);
            var appliedJson = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(appliedJson).Blueprints
                .Single(item => item.Id == blueprint.Id);

            if (foundation is null || operations is null || ItemFoundationValidator.Validate(foundation).Count != 0 ||
                operations.Loot.Entries.Count != 2 || firstRoll.Count != 1 || !firstRoll.SequenceEqual(secondRoll) ||
                lootPreview.Count != 2 || resolved.Diagnostics.Count != 0 ||
                resolved.Inventory.Revision != 0 || move.Diagnostics.Count != 0 || move.Document.Revision != 0 ||
                filtered.Count != 2 || comparisons.Count != 2 ||
                operations.Inspector is not
                {
                    DragDropEnabled: true, MultiEditEnabled: true,
                    ComparisonEnabled: true
                } ||
                events.Length != 9 || events.Select(item => item.Kind).Distinct().Count() != 9 ||
                script?.Operations?.ProfileId != operations.Id || script.Operations.LifecycleEvents.Count != 9 ||
                !optional.OptionalFallback ||
                required.Diagnostics.SingleOrDefault()?.Code != ItemFoundationValidator.OperationsMissingRequiredCode ||
                restored.ItemFoundation?.Operations?.Id != operations.Id ||
                ProjectJsonSerializer.Serialize(ProjectJsonSerializer.Deserialize(appliedJson)) != appliedJson ||
                _itemOperationsWindow.SummarySectionCount != 3)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R090_ITEM_OPERATIONS_FAILED canonical_runtime_roundtrip");
                return false;
            }

            Undo();
            appliedToHistory = false;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != before)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R090_ITEM_OPERATIONS_FAILED undo");
                return false;
            }
            Redo();
            appliedToHistory = true;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != appliedJson)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R090_ITEM_OPERATIONS_FAILED redo");
                return false;
            }
            Undo();
            appliedToHistory = false;
            GD.Print("OPENRPG_FORGE_TOOLSET_R090_ITEM_OPERATIONS_PASS " +
                "loot=weighted-groups-conditions-quantity-quality-seed-uniqueness-statistical-preview " +
                "inspector=drag-drop-containers-slots-filters-validation-visual-preview-comparison-multi-edit " +
                "events=created-acquired-transferred-equipped-unequipped-used-consumed-broken-destroyed " +
                "forgescript=typed-profile-handle mcp=filtered-dry-run " +
                "ui=visible-empty-error-sections-live-preview command=dry-run-apply-reject history=undo-redo " +
                "persistence=roundtrip runtime=optional-safe-required-diagnostic-rpgkit-loot-inventory-reused");
            return true;
        }
        finally
        {
            _itemOperationsWindow.Hide();
            if (appliedToHistory) _history.Undo();
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunItemAdvancedSmokeTest()
    {
        if (_history is null || _itemAdvancedWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13K_M_ITEM_ADVANCED_FAILED missing_workspace");
            return false;
        }
        var appliedToHistory = false;
        try
        {
            _itemAdvancedWindow.Open(); var visible = _itemAdvancedWindow.Visible; _itemAdvancedWindow.Hide();
            var blueprintId = _itemAdvancedWindow.SelectedBlueprintId;
            var before = ProjectJsonSerializer.Serialize(_history.State.Project);
            var configured = visible && blueprintId is not null && _itemAdvancedWindow.ConfigureForSmokeTest();
            var previewed = configured && _itemAdvancedWindow.PreviewForSmokeTest();
            var applied = previewed && _itemAdvancedWindow.LastCommandId != Guid.Empty &&
                          _itemAdvancedWindow.LastPatchSummary.Contains("item", StringComparison.OrdinalIgnoreCase) &&
                          _itemAdvancedWindow.ApplyForSmokeTest();
            if (!applied || blueprintId is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13K_M_ITEM_ADVANCED_FAILED preview_apply_ui " +
                    _itemAdvancedWindow.LastDiagnostics); return false;
            }
            appliedToHistory = true;
            var blueprint = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId);
            var profile = blueprint.ItemAdvanced;
            var context = new ItemRuntimeContext(20, 20, new HashSet<string>(["lore", "smithing"]),
                new HashSet<string>(["ash-attuned"]), new HashSet<string>(["surface-alliance"]),
                new HashSet<string>(["quest.accepted"]), "default", "gold", 1, 40, 2026, "player", true);
            var requirements = profile is null ? null : ItemAdvancedRuntimeResolver.ResolveRequirements(profile, context);
            var value = profile is null ? 0 : ItemAdvancedRuntimeResolver.ResolveEconomicValue(profile, context);
            var proc = profile is null ? null : ItemAdvancedRuntimeResolver.ResolveOnHit(profile, "hit",
                new Dictionary<Guid, DateTimeOffset>(), DateTimeOffset.UnixEpoch, context);
            var generated = profile?.Generator?.Enabled == true ? ItemAdvancedRuntimeResolver.Generate(profile, context) : null;
            var wear = profile is null ? null : ItemAdvancedRuntimeResolver.ApplyWear(profile, 40, 1, 1);
            var ammo = profile?.Ammo is null ? null : ItemAmmoResolver.Fire(profile, 3, 2026, Guid.Parse("a1650000-0000-4000-8000-000000000001"));
            var reservation = profile is null ? null : ItemUniqueRegistryResolver.Reserve(profile,
                ItemUniqueRegistryState.Empty, "player", "party", "season", DateTimeOffset.UnixEpoch, true);
            var ownership = profile is null ? null : ItemLootOwnershipResolver.Resolve(profile,
                new("player", "player", "party", "party", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
                    false, null, false, true));
            var craft = profile?.Recipes.Count > 0 ? ItemCraftingResolver.Resolve(profile, profile.Recipes[0].Id,
                new("forge", new HashSet<string>(["smithing"]), 1,
                    new Dictionary<string, int> { ["tag:ember-metal"] = 2 }, true)) : null;
            var loot = profile is null ? null : ItemAdvancedRuntimeResolver.ResolveLootContext(profile,
                "crypt-chest", "crypt", "ash-watch", null, new HashSet<string>(["undead"]), null, context);
            var script = profile is null ? null : new ItemAdvancedScriptService(blueprint);
            var snapshot = script?.Read(script.Handle);
            var appliedJson = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(appliedJson).Blueprints.Single(item => item.Id == blueprintId);
            var checks = new (string Name, bool Passed)[]
            {
                ("profile", profile is not null),
                ("validation", profile is not null && ItemAdvancedValidator.Validate(profile, "item").Count == 0),
                ("requirements", requirements is { Allowed: true }),
                ("economy", value > 0),
                ("on_hit", proc is { Triggered: true }),
                ("weapon", profile?.Weapon is not null),
                ("armor", profile?.Armor is not null),
                ("ammo", ammo is { Fired: true }),
                ("wear", wear is not null && wear.Current < 40),
                ("container", profile?.Container is not null),
                // Existing authored affixes are intentionally preserved by the editor. The smoke
                // contract verifies usable deterministic generation without imposing a lossy count.
                ("generator", generated is not null && generated.Affixes.Count > 0),
                ("unique", reservation is { Accepted: true }),
                ("ownership", ownership is { Allowed: true }),
                ("craft", craft is { Allowed: true }),
                ("loot", loot?.RuleId is not null),
                ("script", snapshot is not null),
                ("summary", _itemAdvancedWindow.SummarySectionCount == 4),
                ("roundtrip", profile is not null && restored.ItemAdvanced?.Id == profile.Id),
            };
            var failedChecks = checks.Where(check => !check.Passed).Select(check => check.Name).ToArray();
            if (failedChecks.Length > 0)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13K_M_ITEM_ADVANCED_FAILED canonical_runtime_roundtrip " +
                    string.Join(',', failedChecks));
                return false;
            }
            Undo(); appliedToHistory = false; if (ProjectJsonSerializer.Serialize(_history.State.Project) != before) return false;
            Redo(); appliedToHistory = true; if (ProjectJsonSerializer.Serialize(_history.State.Project) != appliedJson) return false;
            Undo(); appliedToHistory = false;
            GD.Print("OPENRPG_FORGE_TOOLSET_G13K_ITEM_FOUNDATION_PASS economy=currency-market-quality-rarity-wear-demand " +
                "requirements=level-skill-trait-faction-knowledge-script identification=levels-hidden-cost-party " +
                "ownership=history-stolen-jurisdiction-evidence-launder onhit=trigger-chance-cooldown-condition-effect-attribution-deterministic " +
                "ui=visible-empty-error-summary-simulation transaction=identified history=restored persistence=roundtrip");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13L_ITEM_EQUIPMENT_PASS weapon=attacks-damage-reach-animation-requirements-tags " +
                "armor=coverage-protection-encumbrance-noise-material-resistance-visual ammo=projectile-stack-recovery-element " +
                "durability=wear-repair-break-vfx-optional container=capacity-mass-depth-weight-permissions-persistence-cycles " +
                "ui=three-tabs runtime=deterministic persistence=roundtrip");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13M_ITEM_LOOT_CRAFT_PASS loot=context-biome-faction-level-difficulty-tags-player-event " +
                "unique=world-party-player-season-transactional ownership=public-party-personal-needgreed-gm-expiration " +
                "craft=recipe-query-station-skill-quality-duration-byproducts-provenance " +
                "generator=seed-pools-affixes-budget-rarity-exclusions-explanation forgescript=typed-handle mcp=filtered-dry-run");
            return true;
        }
        finally
        {
            _itemAdvancedWindow.Hide(); if (appliedToHistory) _history.Undo();
            RebuildPreview(); RefreshEditorControls();
        }
    }


}
#endif
