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
    IReadOnlyList<AgentDiagnostic> NarrativeDiagnostics);

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
