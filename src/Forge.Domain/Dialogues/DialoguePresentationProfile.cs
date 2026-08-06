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

public sealed record DialoguePresentationProfile
{
    public Guid Id { get; init; }
    public int SchemaVersion { get; init; }
    public string Provenance { get; init; }
    public bool RequiredAtRuntime { get; init; }
    public bool ServerAuthoritativeInterjections { get; init; }
    public IReadOnlyList<DialogueAnimationCue> AnimationCues { get; init; }
    public IReadOnlyList<DialogueVoiceCue> VoiceCues { get; init; }
    public IReadOnlyList<DialogueCameraShot> CameraShots { get; init; }
    public IReadOnlyList<DialogueBookmark> Bookmarks { get; init; }
    public IReadOnlyList<DialogueInterjectionRule> InterjectionRules { get; init; }
    public IReadOnlyList<DialogueTimedChoiceRule> TimedChoices { get; init; }
    public IReadOnlyList<DialogueVariantSet> VariantSets { get; init; }

    public DialoguePresentationProfile(
        Guid id,
        int schemaVersion,
        string provenance,
        bool requiredAtRuntime,
        bool serverAuthoritativeInterjections,
        IReadOnlyList<DialogueAnimationCue> animationCues,
        IReadOnlyList<DialogueVoiceCue> voiceCues,
        IReadOnlyList<DialogueCameraShot> cameraShots,
        IReadOnlyList<DialogueBookmark> bookmarks,
        IReadOnlyList<DialogueInterjectionRule> interjectionRules,
        IReadOnlyList<DialogueTimedChoiceRule>? timedChoices = null,
        IReadOnlyList<DialogueVariantSet>? variantSets = null)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance;
        RequiredAtRuntime = requiredAtRuntime;
        ServerAuthoritativeInterjections = serverAuthoritativeInterjections;
        AnimationCues = animationCues?.ToArray() ?? [];
        VoiceCues = voiceCues?.ToArray() ?? [];
        CameraShots = cameraShots?.ToArray() ?? [];
        Bookmarks = bookmarks?.ToArray() ?? [];
        InterjectionRules = interjectionRules?.ToArray() ?? [];
        TimedChoices = timedChoices?.ToArray() ?? [];
        VariantSets = variantSets?.ToArray() ?? [];
    }
}

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

public enum DialogueRuntimeAuthorityScope
{
    Local,
    Server,
}

public enum DialogueTimedChoicePausePolicy
{
    Never,
    AccessibilityOnly,
    Always,
}

public sealed record DialogueTimedChoiceRule(
    Guid Id,
    DialogueNodeId NodeId,
    int DurationMilliseconds,
    DialogueChoiceId DefaultChoiceId,
    DialogueTimedChoicePausePolicy PausePolicy,
    DialogueRuntimeAuthorityScope AuthorityScope,
    long ServerRevision,
    string? TimeoutScriptId,
    string AuditActorId,
    string Provenance);

public enum DialogueVariantSeedScope
{
    Dialogue,
    Node,
    Session,
    ServerRevision,
}

public sealed record DialogueTextVariant(
    Guid Id,
    LocalizedText Text,
    int Weight,
    IReadOnlyList<DialogueCondition> Conditions,
    string Provenance);

public sealed record DialogueVariantSet(
    Guid Id,
    DialogueNodeId NodeId,
    string Key,
    DialogueVariantSeedScope SeedScope,
    int SeedOffset,
    int AntiRepeatWindow,
    DialogueRuntimeAuthorityScope AuthorityScope,
    bool RequiredAtRuntime,
    IReadOnlyList<DialogueTextVariant> Variants,
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
