using Forge.Domain.Quests;
using Forge.Mcp.Quests;

namespace Forge.Mcp.Projects;

internal static class ForgeProjectQuestResourceMapper
{
    internal static QuestAuthoringResource? MapAuthoring(QuestAuthoringProfile? profile)
    {
        if (profile is null) return null;
        return new QuestAuthoringResource(profile.Id.ToString("D"), profile.SchemaVersion, profile.Provenance,
            profile.Enabled, profile.RequiredAtRuntime,
            profile.Objectives.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id).Select(item =>
                new QuestAuthoringObjectiveResource(item.Id.ToString("D"), item.DisplayOrder,
                    item.Kind.ToString(), item.Label.Key, item.Label.Fallback, item.Target, item.TargetIds,
                    item.DestinationId, item.DurationSeconds, item.ChoiceIds, item.CustomRuleId, item.Optional))
                .ToArray(),
            new QuestAuthoringRewardResource(profile.Rewards.Id.ToString("D"), profile.Rewards.Progression,
                profile.Rewards.Items, profile.Rewards.Currencies, profile.Rewards.Reputation,
                profile.Rewards.Unlocks, profile.Rewards.ChoiceIds),
            new QuestAuthoringNotesResource(profile.Notes.Design, profile.Notes.Canon, profile.Notes.OwnerId,
                profile.Notes.Status.ToString(), profile.Notes.Risks, profile.Notes.Todos, profile.Notes.Sources,
                profile.Notes.ReviewHistory.Count),
            profile.Participants.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id).Select(item =>
                new QuestAuthoringParticipantResource(item.Id.ToString("D"), item.DisplayOrder, item.Role,
                    item.BindingKind.ToString(), item.BindingValue, item.PresenceConditions, item.Required)).ToArray(),
            new QuestPlotWizardResource(profile.Wizard.Id.ToString("D"), profile.Wizard.ZoneIds,
                profile.Wizard.ParticipantIds.Select(id => id.ToString("D")).ToArray(), profile.Wizard.PropIds,
                profile.Wizard.DialogueIds.Select(id => id.ToString()).ToArray(),
                profile.Wizard.ObjectiveIds.Select(id => id.ToString("D")).ToArray(),
                profile.Wizard.TransitionIds.Select(id => id.ToString()).ToArray(),
                profile.Wizard.GeneratedTestScenarios, profile.Wizard.GeneratesOpaqueCode));
    }

    internal static QuestExperienceProfileResource? MapExperience(QuestExperienceProfile? profile)
    {
        if (profile is null) return null;
        return new QuestExperienceProfileResource(profile.Id.ToString("D"), profile.SchemaVersion, profile.Provenance,
            profile.Enabled, profile.RequiredAtRuntime,
            profile.Resources.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id).Select(item =>
                new QuestExperienceResourceItem(item.Id.ToString("D"), item.DisplayOrder, item.Kind.ToString(),
                    item.ReferenceId, item.OwnerId, item.Required, item.Conditions)).ToArray(),
            profile.Steps.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id).Select(item =>
                new QuestExperienceStepResource(item.Id.ToString("D"), item.DisplayOrder, item.Label.Key,
                    item.Label.Fallback, item.TemplateId, item.TemplateVersion, item.Editable,
                    item.ObjectiveIds.Select(id => id.ToString("D")).ToArray(),
                    item.TransitionIds.Select(id => id.ToString()).ToArray())).ToArray(),
            new QuestExperienceVisibilityResource(profile.Visibility.Id.ToString("D"),
                profile.Visibility.Initial.ToString(), profile.Visibility.AllowManualTracking,
                profile.Visibility.RevealConditions.ToDictionary(item => item.Key.ToString(), item => item.Value,
                    StringComparer.Ordinal)),
            profile.History.OrderBy(item => item.At).ThenBy(item => item.Id).Select(item =>
                new QuestExperienceHistoryResource(item.Id.ToString("D"), item.FromState, item.ToState,
                    item.Cause, item.ActorId, item.At, item.PlayerSummary.Key, item.PlayerSummary.Fallback,
                    item.DebugContext)).ToArray(),
            profile.Markers.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id).Select(item =>
                new QuestExperienceMarkerResource(item.Id.ToString("D"), item.DisplayOrder,
                    item.ObjectiveId.ToString("D"), item.BindingKind.ToString(), item.BindingValue,
                    item.Visibility.ToString(), item.PrecisionRadius, item.Tracked, item.Label.Key,
                    item.Label.Fallback, item.FallbackPosition?.ToString())).ToArray());
    }

    internal static QuestQualityProfileResource? MapQuality(QuestQualityProfile? profile)
    {
        if (profile is null) return null;
        return new QuestQualityProfileResource(profile.Id.ToString("D"), profile.SchemaVersion, profile.Provenance,
            profile.Enabled, profile.RequiredAtRuntime,
            profile.TestScenarios.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id).Select(scenario =>
                new QuestQualityScenarioResource(scenario.Id.ToString("D"), scenario.DisplayOrder,
                    scenario.Kind.ToString(), scenario.Label.Key, scenario.Label.Fallback, scenario.Seed,
                    scenario.Fixtures.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id).Select(item =>
                        new QuestQualityFixtureResource(item.Id.ToString("D"), item.DisplayOrder,
                            item.Kind.ToString(), item.ReferenceId, item.Required)).ToArray(),
                    scenario.Steps.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id).Select(item =>
                        new QuestQualityStepResource(item.Id.ToString("D"), item.DisplayOrder,
                            item.Action.ToString(), item.Parameters)).ToArray(),
                    scenario.Edges.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id).Select(item =>
                        new QuestQualityEdgeResource(item.Id.ToString("D"), item.DisplayOrder,
                            item.FromStepId.ToString("D"), item.ToStepId.ToString("D"))).ToArray(),
                    scenario.Assertions.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id).Select(item =>
                        new QuestQualityAssertionResource(item.Id.ToString("D"), item.DisplayOrder,
                            item.Kind.ToString(), item.Path, item.Expected)).ToArray())).ToArray(),
            new QuestAiPlanResource(profile.AiPlan.Id.ToString("D"), profile.AiPlan.TransactionId.ToString("D"),
                profile.AiPlan.Synopsis.Key, profile.AiPlan.Synopsis.Fallback,
                profile.AiPlan.ProposedStateIds.Select(item => item.ToString()).ToArray(),
                profile.AiPlan.ParticipantIds.Select(item => item.ToString("D")).ToArray(),
                profile.AiPlan.Dependencies.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id).Select(item =>
                    new QuestAiDependencyResource(item.Id.ToString("D"), item.DisplayOrder,
                        item.Kind.ToString(), item.ReferenceId)).ToArray(),
                profile.AiPlan.ContextCitations, profile.AiPlan.StyleRules,
                profile.AiPlan.Lint.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id).Select(item =>
                    new QuestAiLintResource(item.Id.ToString("D"), item.DisplayOrder, item.Code, item.Path,
                        item.Message)).ToArray(), profile.AiPlan.SemanticDiff, profile.AiPlan.Authorization.ToString(),
                profile.AiPlan.RequestedBy, profile.AiPlan.ApprovedBy, profile.AiPlan.ApprovedAt,
                profile.AiPlan.Applied));
    }
}
