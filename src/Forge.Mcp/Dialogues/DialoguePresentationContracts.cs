using Forge.Application.Commands;
using Forge.Domain.Dialogues;

namespace Forge.Mcp.Dialogues;

public sealed record DialoguePresentationResource(
    int SchemaVersion,
    long ProjectRevision,
    string? Filter,
    IReadOnlyList<DialoguePresentationResourceItem> Items);

public sealed record DialoguePresentationResourceItem(
    string DialogueId,
    string Slug,
    string DisplayName,
    string ProfileId,
    bool RequiredAtRuntime,
    bool ServerAuthoritativeInterjections,
    int AnimationCueCount,
    int VoiceCueCount,
    int CameraShotCount,
    int BookmarkCount,
    int InterjectionRuleCount);

public sealed record DialoguePresentationPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string DialogueId,
    DialoguePresentationProfile? Presentation);

public sealed record DialoguePresentationPlanResult(
    bool Succeeded,
    long BaseRevision,
    ProjectPatch? SemanticDiff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics);
