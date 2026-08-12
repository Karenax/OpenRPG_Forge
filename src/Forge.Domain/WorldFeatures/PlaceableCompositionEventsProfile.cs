using Forge.Domain.Blueprints;

namespace Forge.Domain.WorldFeatures;

public sealed record PlaceableCompositionEventsProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    PlaceableDamageEventsProfile DamageEvents,
    IReadOnlyList<PlaceableAttachmentSocketProfile> Sockets,
    IReadOnlyList<PlaceableAuthoredAttachmentProfile> Attachments,
    PlaceableMultiVerbProfile MultiVerb);

public sealed record PlaceableDamageEventsProfile(
    Guid Id,
    string ForgeScriptApiVersion,
    string DamagedBinding,
    string DestroyedBinding,
    bool IncludeSource,
    bool IncludeAmount,
    bool IncludeDamageType,
    bool IncludePreviousHealth,
    bool IncludeCurrentHealth,
    bool IncludeContext);

public sealed record PlaceableAttachmentSocketProfile(
    Guid Id,
    string SocketId,
    string Kind,
    PlaceableWorldProfile.TransformPolicy LocalTransform,
    IReadOnlyList<string> AllowedAttachmentTags,
    bool SnapEnabled,
    int MaximumAttachments);

public sealed record PlaceableAuthoredAttachmentProfile(
    Guid Id,
    string SocketId,
    BlueprintId ChildBlueprintId,
    IReadOnlyList<string> Tags,
    PlaceableWorldProfile.TransformPolicy Transform,
    string? PrefabId);

public sealed record PlaceableMultiVerbProfile(
    Guid Id,
    IReadOnlyList<PlaceableContextualVerbProfile> Verbs);

public sealed record PlaceableContextualVerbProfile(
    Guid Id,
    string VerbId,
    string LabelKey,
    int BasePriority,
    IReadOnlyList<string> RequiredContext,
    IReadOnlyList<string> ForbiddenContext,
    string EventBinding);
