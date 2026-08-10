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
    IReadOnlyList<DialogueGraphEdgeResource> Edges);

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
