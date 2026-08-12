namespace Forge.Mcp.Dialogues;

public sealed record DialogueGraphResource(
    int SchemaVersion,
    long ProjectRevision,
    string? Filter,
    IReadOnlyList<DialogueGraphResourceItem> Items);

public sealed record DialogueGraphResourceItem(
    string DialogueId,
    int DocumentSchemaVersion,
    string Provenance,
    string Slug,
    string DisplayName,
    string EntryNodeId,
    IReadOnlyDictionary<string, string> Metadata,
    int ParticipantCount,
    int EntryPointCount,
    IReadOnlyList<DialogueGraphNodeResource> Nodes,
    IReadOnlyList<DialogueGraphEdgeResource> Edges,
    DialogueAuthoringResource? Authoring = null,
    DialogueExperienceResource? Experience = null,
    DialogueProductionResource? Production = null);

public sealed record DialogueGraphNodeResource(
    string NodeId,
    string Role,
    string ParticipantId,
    string? SpeakerBlueprintId,
    string? SubgraphKey,
    string LocalizationKey,
    string FallbackText,
    int LocalizationVariantCount,
    int OnEnterActionCount,
    int ChoiceCount,
    int IncomingEdgeCount);

public sealed record DialogueGraphEdgeResource(
    string ChoiceId,
    string SourceNodeId,
    string TargetNodeId,
    string Kind,
    int Order,
    int Priority,
    string ConditionCombination,
    int ConditionCount,
    int ActionCount);

public sealed record DialogueAuthoringResource(
    string AuthoringId,
    int SchemaVersion,
    string Provenance,
    bool Enabled,
    bool RequiredAtRuntime,
    IReadOnlyList<DialogueParticipantBindingResource> Participants,
    IReadOnlyList<DialogueNodeAuthoringResource> Nodes);

public sealed record DialogueParticipantBindingResource(
    string BindingId,
    string Role,
    string Kind,
    string Value,
    string? FallbackRole);

public sealed record DialogueNodeAuthoringResource(
    string AuthoringNodeId,
    string NodeId,
    string SpeakerRole,
    string EvaluationMode,
    IReadOnlyList<string> Tokens,
    IReadOnlyList<string> ChoiceWeights,
    IReadOnlyList<string> WizardSources,
    string Intent,
    string Emotion,
    string Source,
    string EditorialStatus,
    IReadOnlyList<string> Todos,
    string AuthorMention);

public sealed record DialogueExperienceResource(
    string ExperienceId,
    int SchemaVersion,
    string Provenance,
    bool Enabled,
    bool RequiredAtRuntime,
    int CompletedActionCount,
    int AbortedActionCount,
    bool RollbackOnAbort,
    bool CreateResumeState,
    string SearchPattern,
    bool SafeRegex,
    IReadOnlyList<string> SearchFilters,
    IReadOnlyList<string> ReviewBadges,
    bool ReviewMode,
    IReadOnlyList<string> MemorySlots,
    IReadOnlyList<string> SkillChecks);

public sealed record DialogueProductionResource(
    string ProductionId,
    int SchemaVersion,
    string Provenance,
    bool Enabled,
    bool RequiredAtRuntime,
    IReadOnlyList<string> Subgraphs,
    IReadOnlyList<string> Consequences,
    IReadOnlyList<string> Coverage,
    IReadOnlyList<string> Collaboration,
    IReadOnlyList<string> AiPlans);
