using Forge.Application.Commands;
using Forge.Domain.Quests;

namespace Forge.Mcp.Quests;

public sealed record QuestJournalResource(
    int SchemaVersion,
    long ProjectRevision,
    string? Filter,
    IReadOnlyList<QuestJournalResourceItem> Items);

public sealed record QuestJournalResourceItem(
    string QuestId,
    int DocumentSchemaVersion,
    string Provenance,
    string Slug,
    string TitleKey,
    string Title,
    string DescriptionKey,
    string Category,
    int Priority,
    string Owner,
    bool RequiredAtRuntime,
    string DefaultScope,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<QuestJournalStateResource> States,
    IReadOnlyList<QuestTransitionResource> Transitions,
    int SimulationEventCount,
    int MaximumSimulationSteps,
    IReadOnlyList<AgentDiagnostic> NarrativeDiagnostics,
    QuestAuthoringResource? Authoring,
    QuestExperienceProfileResource? Experience,
    QuestQualityProfileResource? Quality);

public sealed record QuestQualityProfileResource(
    string ProfileId, int DocumentSchemaVersion, string Provenance, bool Enabled, bool RequiredAtRuntime,
    IReadOnlyList<QuestQualityScenarioResource> TestScenarios, QuestAiPlanResource AiPlan);

public sealed record QuestQualityScenarioResource(
    string ScenarioId, int DisplayOrder, string Kind, string LabelKey, string Label, int Seed,
    IReadOnlyList<QuestQualityFixtureResource> Fixtures, IReadOnlyList<QuestQualityStepResource> Steps,
    IReadOnlyList<QuestQualityEdgeResource> Edges, IReadOnlyList<QuestQualityAssertionResource> Assertions);

public sealed record QuestQualityFixtureResource(
    string FixtureId, int DisplayOrder, string Kind, string ReferenceId, bool Required);

public sealed record QuestQualityStepResource(
    string StepId, int DisplayOrder, string Action, IReadOnlyDictionary<string, string> Parameters);

public sealed record QuestQualityEdgeResource(
    string EdgeId, int DisplayOrder, string FromStepId, string ToStepId);

public sealed record QuestQualityAssertionResource(
    string AssertionId, int DisplayOrder, string Kind, string Path, string Expected);

public sealed record QuestAiPlanResource(
    string PlanId, string TransactionId, string SynopsisKey, string Synopsis,
    IReadOnlyList<string> ProposedStateIds, IReadOnlyList<string> ParticipantIds,
    IReadOnlyList<QuestAiDependencyResource> Dependencies, IReadOnlyList<string> ContextCitations,
    IReadOnlyList<string> StyleRules, IReadOnlyList<QuestAiLintResource> Lint, string SemanticDiff,
    string Authorization, string RequestedBy, string ApprovedBy, DateTimeOffset? ApprovedAt, bool Applied);

public sealed record QuestAiDependencyResource(
    string DependencyId, int DisplayOrder, string Kind, string ReferenceId);

public sealed record QuestAiLintResource(
    string LintId, int DisplayOrder, string Code, string Path, string Message);

public sealed record QuestExperienceProfileResource(
    string ProfileId,
    int DocumentSchemaVersion,
    string Provenance,
    bool Enabled,
    bool RequiredAtRuntime,
    IReadOnlyList<QuestExperienceResourceItem> Resources,
    IReadOnlyList<QuestExperienceStepResource> Steps,
    QuestExperienceVisibilityResource Visibility,
    IReadOnlyList<QuestExperienceHistoryResource> History,
    IReadOnlyList<QuestExperienceMarkerResource> Markers);

public sealed record QuestExperienceResourceItem(
    string ResourceId, int DisplayOrder, string Kind, string ReferenceId, string OwnerId,
    bool Required, IReadOnlyList<string> Conditions);

public sealed record QuestExperienceStepResource(
    string StepId, int DisplayOrder, string LabelKey, string Label, string TemplateId, int TemplateVersion,
    bool Editable, IReadOnlyList<string> ObjectiveIds, IReadOnlyList<string> TransitionIds);

public sealed record QuestExperienceVisibilityResource(
    string PolicyId, string Initial, bool AllowManualTracking,
    IReadOnlyDictionary<string, IReadOnlyList<string>> RevealConditions);

public sealed record QuestExperienceHistoryResource(
    string EventId, string FromState, string ToState, string Cause, string ActorId, DateTimeOffset At,
    string PlayerSummaryKey, string PlayerSummary, IReadOnlyDictionary<string, string> DebugContext);

public sealed record QuestExperienceMarkerResource(
    string MarkerId, int DisplayOrder, string ObjectiveId, string BindingKind, string BindingValue,
    string Visibility, double PrecisionRadius, bool Tracked, string LabelKey, string Label,
    string? FallbackPosition);

public sealed record QuestAuthoringResource(
    string ProfileId,
    int DocumentSchemaVersion,
    string Provenance,
    bool Enabled,
    bool RequiredAtRuntime,
    IReadOnlyList<QuestAuthoringObjectiveResource> Objectives,
    QuestAuthoringRewardResource Rewards,
    QuestAuthoringNotesResource Notes,
    IReadOnlyList<QuestAuthoringParticipantResource> Participants,
    QuestPlotWizardResource Wizard);

public sealed record QuestAuthoringObjectiveResource(
    string ObjectiveId,
    int DisplayOrder,
    string Kind,
    string LabelKey,
    string Label,
    double Target,
    IReadOnlyList<string> TargetIds,
    string? DestinationId,
    double DurationSeconds,
    IReadOnlyList<string> ChoiceIds,
    string? CustomRuleId,
    bool Optional);

public sealed record QuestAuthoringRewardResource(
    string RewardId,
    double Progression,
    IReadOnlyDictionary<string, int> Items,
    IReadOnlyDictionary<string, decimal> Currencies,
    IReadOnlyDictionary<string, double> Reputation,
    IReadOnlyList<string> Unlocks,
    IReadOnlyList<string> ChoiceIds);

public sealed record QuestAuthoringNotesResource(
    string Design,
    string Canon,
    string OwnerId,
    string Status,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> Todos,
    IReadOnlyList<string> Sources,
    int ReviewCount);

public sealed record QuestAuthoringParticipantResource(
    string ParticipantId,
    int DisplayOrder,
    string Role,
    string BindingKind,
    string BindingValue,
    IReadOnlyList<string> PresenceConditions,
    bool Required);

public sealed record QuestPlotWizardResource(
    string WizardId,
    IReadOnlyList<string> ZoneIds,
    IReadOnlyList<string> ParticipantIds,
    IReadOnlyList<string> PropIds,
    IReadOnlyList<string> DialogueIds,
    IReadOnlyList<string> ObjectiveIds,
    IReadOnlyList<string> TransitionIds,
    IReadOnlyList<string> GeneratedTestScenarios,
    bool GeneratesOpaqueCode);

public sealed record QuestJournalStateResource(
    string StateId,
    int DocumentSchemaVersion,
    string Provenance,
    string Slug,
    string DisplayName,
    int DisplayOrder,
    int? LegacyJournalIndex,
    string TerminalKind,
    string Reactivation,
    string SummaryKey,
    string Summary,
    string DetailsKey,
    int ObjectiveCount,
    int ContextLinkCount);

public sealed record QuestTransitionResource(
    string TransitionId,
    int DocumentSchemaVersion,
    string Provenance,
    string FromStateId,
    string ToStateId,
    int DisplayOrder,
    string EventKind,
    string EventName,
    int Priority,
    string Idempotence,
    string IdempotenceKey,
    int ForgeScriptApiVersion,
    int ConditionCount,
    int ActionCount);

public sealed record QuestJournalPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string QuestId,
    QuestDefinition Quest);

public sealed record QuestJournalPlanResult(
    bool Succeeded,
    long BaseRevision,
    ProjectPatch? SemanticDiff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record QuestSimulationRequest(
    int SchemaVersion,
    string QuestId,
    string Scope,
    string ScopeOwnerId,
    DateTimeOffset StartedAt,
    bool AuthorityGranted = true);

public sealed record QuestSimulationResource(
    bool Succeeded,
    string QuestId,
    string? ScenarioId,
    string? Status,
    string? FinalStateId,
    int StepCount,
    int AppliedTransitionCount,
    IReadOnlyDictionary<string, string> Variables,
    IReadOnlyList<QuestSimulationStepResource> Steps,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record QuestSimulationStepResource(
    string StepId,
    string EventId,
    string EventName,
    string BeforeStateId,
    string AfterStateId,
    string? TransitionId,
    int EffectCount,
    IReadOnlyList<AgentDiagnostic> Diagnostics);
