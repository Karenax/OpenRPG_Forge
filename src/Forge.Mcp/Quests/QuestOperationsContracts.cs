using Forge.Application.Commands;
using Forge.Domain.Quests;

namespace Forge.Mcp.Quests;

public sealed record QuestOperationsResource(
    int SchemaVersion,
    long ProjectRevision,
    string? Filter,
    IReadOnlyList<QuestOperationsResourceItem> Items);

public sealed record QuestOperationsResourceItem(
    string QuestId,
    string QuestSlug,
    string DisplayName,
    string ProfileId,
    int Priority,
    int NarrativeImportance,
    bool Repeatable,
    string ShareMode,
    int ExclusiveChoiceCount,
    string NotificationMode,
    bool RequiredAtRuntime);

public sealed record QuestOperationsPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string QuestId,
    QuestOperationsProfile Profile);

public sealed record QuestOperationsPlanResult(
    bool Succeeded,
    long BaseRevision,
    ProjectPatch? SemanticDiff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics);
