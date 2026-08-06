using Forge.Domain.Blueprints;
using Forge.Domain.Sequences;
using Forge.Domain.Validation;

namespace Forge.Domain.Dialogues;

public enum DialogueGesture
{
    None,
    Idle,
    Greet,
    Explain,
    Threaten,
    Agree,
    Disagree,
    Point,
}

public enum DialogueAnimationInterruptionPolicy
{
    Complete,
    BlendOut,
    InterruptImmediately,
}

public enum DialogueCameraShotMode
{
    Automatic,
    Anchor,
    Disabled,
}

public enum DialogueBookmarkScope
{
    Personal,
    Shared,
}

public enum DialogueInterjectionConflictPolicy
{
    HighestPriority,
    FirstAvailable,
    SuppressAll,
}

public sealed record DialoguePresentationProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool RequiredAtRuntime,
    bool ServerAuthoritativeInterjections,
    IReadOnlyList<DialogueAnimationCue> AnimationCues,
    IReadOnlyList<DialogueVoiceCue> VoiceCues,
    IReadOnlyList<DialogueCameraShot> CameraShots,
    IReadOnlyList<DialogueBookmark> Bookmarks,
    IReadOnlyList<DialogueInterjectionRule> InterjectionRules);

public sealed record DialogueAnimationCue(
    Guid Id,
    DialogueNodeId NodeId,
    BlueprintId ParticipantBlueprintId,
    string AnimationId,
    int StartMilliseconds,
    int DurationMilliseconds,
    int BlendMilliseconds,
    DialogueGesture Gesture,
    BlueprintId? LookAtBlueprintId,
    DialogueAnimationInterruptionPolicy InterruptionPolicy,
    string FallbackAnimationId,
    string Provenance);

public sealed record DialogueVoiceCue(
    Guid Id,
    DialogueNodeId NodeId,
    BlueprintId ParticipantBlueprintId,
    string Locale,
    string? Resource,
    string Take,
    LocalizedText Subtitle,
    string LipSyncProfileId,
    string Emotion,
    int DurationMilliseconds,
    string? LicenseSpdx,
    string? ContentSha256,
    bool AllowSubtitleFallback,
    string Provenance);

public sealed record DialogueCameraShot(
    Guid Id,
    DialogueNodeId NodeId,
    DialogueCameraShotMode Mode,
    Guid? CameraAnchorId,
    BlueprintId? FocusBlueprintId,
    CameraFraming Framing,
    float FieldOfViewDegrees,
    int DurationMilliseconds,
    int BlendMilliseconds,
    bool CinematicBars,
    bool AllowPlayerOverride,
    bool ReduceMotionFallback,
    float SafeAreaMargin,
    int Priority,
    string Provenance);

public sealed record DialogueBookmark(
    Guid Id,
    DialogueNodeId NodeId,
    string Name,
    DialogueBookmarkScope Scope,
    string? ReviewComment,
    string AuthorId,
    string DeepLink,
    string Provenance);

public sealed record DialogueInterjectionRule(
    Guid Id,
    DialogueNodeId TriggerNodeId,
    DialogueNodeId InterjectionNodeId,
    BlueprintId CompanionBlueprintId,
    IReadOnlyList<string> PersonalityTags,
    bool RequirePresent,
    double MinimumRelation,
    int Priority,
    int CooldownSeconds,
    string ConflictGroup,
    DialogueInterjectionConflictPolicy ConflictPolicy,
    string Provenance);

public sealed record DialogueCompanionState(
    BlueprintId BlueprintId,
    bool Present,
    double Relation,
    IReadOnlySet<string> PersonalityTags);

public sealed record DialoguePresentationRuntimeContext(
    string Locale,
    bool RequirePresentation,
    bool PlayerControlsCamera,
    bool ReduceMotion,
    bool IsServer,
    DateTimeOffset Now,
    IReadOnlyList<DialogueCompanionState> Companions,
    IReadOnlyDictionary<Guid, DateTimeOffset> LastInterjections);

public sealed record DialoguePresentationResolution(
    IReadOnlyList<DialogueAnimationCue> AnimationCues,
    DialogueVoiceCue? VoiceCue,
    bool UsesSubtitleFallback,
    DialogueCameraShot? CameraShot,
    DialogueInterjectionRule? Interjection,
    IReadOnlyList<DomainDiagnostic> Diagnostics,
    bool OptionalFallback);
