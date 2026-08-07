using Forge.Domain.Blueprints;

namespace Forge.Domain.WorldFeatures;

public sealed record CompanionWorldProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    BlueprintId BlueprintId,
    CompanionWorldProfile.DialoguePermissionPolicy Dialogue,
    CompanionWorldProfile.FormationPolicy Formation,
    CompanionWorldProfile.ProgressionPolicy Progression,
    CompanionWorldProfile.RelationshipPolicy Relationship,
    CompanionWorldProfile.BanterPolicy Banter,
    CompanionWorldProfile.QuestPolicy? PersonalQuest,
    CompanionWorldProfile.TrapPolicy Traps,
    CompanionWorldProfile.AbilityPolicy Abilities,
    CompanionWorldProfile.DirectControlPolicy DirectControl,
    CompanionWorldProfile.PartySplitPolicy PartySplit)
{
    public enum ProgressionMode { Automatic, PlayerDirected, AuthoredMilestones, Fixed }

    public sealed record DialoguePermissionPolicy(
        Guid DialogueId,
        string OwnerBehavior,
        string PartyBehavior,
        string OutsiderBehavior,
        string GameMasterBehavior,
        string HostileBehavior);

    public sealed record FormationPolicy(
        string FormationId,
        int Slot,
        double Spacing,
        bool AdaptToObstacles,
        bool IndoorMode,
        bool NetworkSynchronized);

    public sealed record ProgressionPolicy(
        ProgressionMode Mode,
        int CurrentLevel,
        int MaximumLevel,
        bool PreviewEnabled,
        bool AllowRespec,
        IReadOnlyList<int> Milestones);

    public sealed record RelationshipPolicy(
        double Affinity,
        double Trust,
        double Fear,
        double Loyalty,
        IReadOnlyList<string> Tags,
        IReadOnlyList<string> History);

    public sealed record BanterPolicy(
        IReadOnlyList<BlueprintId> RequiredParticipants,
        int Priority,
        double CooldownSeconds,
        IReadOnlyList<string> Locations,
        bool Interruptible,
        Guid DialogueId);

    public sealed record QuestPolicy(
        Guid QuestId,
        string AvailabilityCondition,
        string DepartureState,
        double MinimumLoyalty,
        string PartyConsequence);

    public sealed record TrapPolicy(
        bool Detect,
        bool Disarm,
        int MaximumRisk,
        string RequiredSense,
        string SkillId,
        bool RequirePlayerCommand);

    public sealed record AbilityPolicy(
        IReadOnlyList<string> Allowed,
        IReadOnlyList<string> Reserved,
        double HealthThreshold,
        string TargetRule,
        bool AllowPlayerOverrides);

    public sealed record DirectControlPolicy(
        bool Enabled,
        bool AllowPossession,
        bool AllowCharacterSwitch,
        bool ReturnToAi,
        string MultiplayerPermission);

    public sealed record PartySplitPolicy(
        bool FollowOwner,
        bool AllowRegroupRequest,
        string InstanceSelection,
        bool SynchronizeQuests,
        double MaximumSeparationSeconds);
}
