using Forge.Application.Commands;

namespace Forge.Mcp.Dialogues;

public sealed record DialoguePlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string DialogueId,
    string Slug,
    string DisplayName,
    string EntryNodeId,
    IReadOnlyList<DialogueNodeInput> Nodes,
    DialogueAuthoringInput? Authoring = null,
    DialogueExperienceInput? Experience = null,
    DialogueProductionInput? Production = null);

public sealed record DialogueNodeInput(
    string NodeId,
    string? SpeakerBlueprintId,
    string LocalizationKey,
    string FallbackText,
    IReadOnlyList<DialogueActionInput>? OnEnterActions = null,
    IReadOnlyList<DialogueChoiceInput>? Choices = null,
    string? Role = null,
    string? ParticipantId = null,
    string? SubgraphKey = null,
    IReadOnlyList<DialogueLocalizedTextVariantInput>? Variants = null);

public sealed record DialogueChoiceInput(
    string ChoiceId,
    string LocalizationKey,
    string FallbackText,
    string? TargetNodeId = null,
    IReadOnlyList<DialogueConditionInput>? Conditions = null,
    IReadOnlyList<DialogueActionInput>? Actions = null,
    string? EdgeKind = null,
    int Priority = 0,
    string? ConditionCombination = null,
    IReadOnlyList<DialogueLocalizedTextVariantInput>? Variants = null);

public sealed record DialogueLocalizedTextVariantInput(
    string Locale,
    string Gender,
    string PluralCategory,
    string Text);

public sealed record DialogueConditionInput(
    string Kind,
    string Key,
    string ExpectedValue,
    bool Negated = false,
    string? EventBinding = null,
    int ForgeScriptApiVersion = 1);

public sealed record DialogueActionInput(string Kind, string Key, string Value, bool Required = true);

public sealed record DialogueAuthoringInput(
    string AuthoringId,
    bool Enabled,
    bool RequiredAtRuntime,
    IReadOnlyList<DialogueParticipantBindingInput> Participants,
    IReadOnlyList<DialogueNodeAuthoringInput> Nodes);

public sealed record DialogueParticipantBindingInput(
    string BindingId,
    string Role,
    string Kind,
    string Value,
    string? FallbackRole = null);

public sealed record DialogueNodeAuthoringInput(
    string AuthoringNodeId,
    string NodeId,
    string SpeakerRole,
    string EvaluationMode,
    IReadOnlyList<DialogueTokenInput> Tokens,
    IReadOnlyList<DialogueChoiceEvaluationInput> ChoiceEvaluations,
    IReadOnlyList<DialogueWizardEntryInput> WizardEntries,
    DialogueEditorialNotesInput Notes);

public sealed record DialogueTokenInput(
    string TokenId,
    string Name,
    string Type,
    bool Required,
    string? DefaultValue = null);

public sealed record DialogueChoiceEvaluationInput(
    string EvaluationId,
    string ChoiceId,
    double Weight);

public sealed record DialogueWizardEntryInput(
    string EntryId,
    string Kind,
    string TargetId,
    string SourceDescription);

public sealed record DialogueEditorialNotesInput(
    string NotesId,
    string Intent,
    string Emotion,
    string Source,
    string Status,
    IReadOnlyList<string> Todos,
    string AuthorMention);

public sealed record DialogueExperienceInput(
    string ExperienceId,
    bool Enabled,
    bool RequiredAtRuntime,
    DialogueLifecycleInput Lifecycle,
    DialogueSearchInput Search,
    DialogueReviewInput Review,
    IReadOnlyList<DialogueMemorySlotInput> MemorySlots,
    IReadOnlyList<DialogueSkillCheckInput> SkillChecks);

public sealed record DialogueLifecycleInput(
    string LifecycleId,
    IReadOnlyList<DialogueActionInput> CompletedActions,
    IReadOnlyList<DialogueActionInput> AbortedActions,
    bool RollbackOnAbort,
    bool CreateResumeState);

public sealed record DialogueSearchInput(
    string SearchId,
    string Pattern,
    bool UseRegex,
    bool IgnoreCase,
    IReadOnlyList<string> SpeakerRoles,
    IReadOnlyList<string> ConditionKinds,
    IReadOnlyList<string> ActionKinds,
    string ReplacementPreview);

public sealed record DialogueReviewInput(
    string ReviewId,
    IReadOnlyList<string> Badges,
    IReadOnlyList<string> SpeakerRoles,
    bool ReviewMode);

public sealed record DialogueMemorySlotInput(
    string SlotId,
    string Scope,
    string ScopeId,
    string Name,
    string Type,
    string Duration,
    string? DefaultValue = null);

public sealed record DialogueSkillCheckInput(
    string CheckId,
    string NodeId,
    string SkillId,
    double Difficulty,
    string? OpponentId,
    bool Advantage,
    string Visibility,
    string SuccessNodeId,
    string FailureNodeId);

public sealed record DialogueProductionInput(
    string ProductionId,
    bool Enabled,
    bool RequiredAtRuntime,
    IReadOnlyList<DialogueSubgraphInput> Subgraphs,
    IReadOnlyList<DialogueSubgraphInvocationInput> Invocations,
    IReadOnlyList<DialogueConsequenceInput> Consequences,
    DialogueCoverageInput Coverage,
    DialogueCollaborationInput Collaboration,
    IReadOnlyList<DialogueAiPlanInput> AiPlans);

public sealed record DialogueSubgraphInput(
    string ContractId,
    string Key,
    int Version,
    IReadOnlyList<DialogueSubgraphParameterInput> Parameters,
    IReadOnlyList<string> RequiredVariables,
    IReadOnlyList<DialogueSubgraphTestInput> Tests);

public sealed record DialogueSubgraphParameterInput(
    string ParameterId,
    string Name,
    string Type,
    string Direction,
    bool Required,
    string? DefaultValue = null);

public sealed record DialogueSubgraphTestInput(
    string TestId,
    string Name,
    IReadOnlyDictionary<string, string> Inputs,
    IReadOnlyDictionary<string, string> ExpectedOutputs);

public sealed record DialogueSubgraphInvocationInput(
    string InvocationId,
    string NodeId,
    string ContractId,
    IReadOnlyDictionary<string, string> InputBindings,
    IReadOnlyDictionary<string, string> OutputBindings);

public sealed record DialogueConsequenceInput(
    string ConsequenceId,
    string NodeId,
    string Kind,
    string TargetId,
    string Operation,
    string Value,
    string Explanation);

public sealed record DialogueCoverageInput(
    string CoverageId,
    int MaximumDepth,
    IReadOnlyDictionary<string, string> RequiredStates,
    IReadOnlyList<DialogueCoverageEvidenceInput> Evidence);

public sealed record DialogueCoverageEvidenceInput(
    string EvidenceId,
    string Name,
    string Kind,
    IReadOnlyList<string> VisitedNodeIds,
    IReadOnlyDictionary<string, string> State);

public sealed record DialogueCollaborationInput(
    string CollaborationId,
    IReadOnlyList<DialogueNodeRevisionInput> Revisions,
    IReadOnlyList<DialogueNodeLockInput> Locks,
    IReadOnlyList<DialogueNodeCommentInput> Comments,
    IReadOnlyList<DialogueSemanticConflictInput> Conflicts);

public sealed record DialogueNodeRevisionInput(
    string RevisionId,
    string NodeId,
    long Revision,
    string AuthorId,
    string CanonicalJson,
    DateTimeOffset At);

public sealed record DialogueNodeLockInput(
    string LockId,
    string NodeId,
    string OwnerId,
    DateTimeOffset ExpiresAt);

public sealed record DialogueNodeCommentInput(
    string CommentId,
    string NodeId,
    string AuthorId,
    string Text,
    bool Resolved,
    DateTimeOffset At);

public sealed record DialogueSemanticConflictInput(
    string ConflictId,
    string NodeId,
    string Field,
    string BaseValue,
    string OursValue,
    string TheirsValue,
    string Resolution,
    string? ManualValue,
    bool Resolved);

public sealed record DialogueAiPlanInput(
    string AiId,
    string TransactionId,
    string PlanId,
    string Authorization,
    string Synopsis,
    IReadOnlyList<string> RequiredBeats,
    IReadOnlyList<string> StyleRules,
    IReadOnlyList<string> ContextCitations,
    string SemanticDiff,
    IReadOnlyList<string> Lint,
    bool PreviewOnly,
    bool Applied);

public sealed record DialoguePlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<PlanAuditEntry> Audit);

public sealed record PlanAuditEntry(
    string PlanId,
    string Action,
    long Revision,
    DateTimeOffset Timestamp,
    bool Succeeded);
