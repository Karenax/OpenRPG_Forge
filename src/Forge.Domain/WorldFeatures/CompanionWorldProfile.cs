using Forge.Domain.Blueprints;
using Forge.Domain.Areas;

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
    CompanionWorldProfile.PartySplitPolicy PartySplit,
    CompanionWorldProfile.ValidationPolicy? Validation = null,
    bool ValidationRequiredAtRuntime = false,
    CompanionWorldProfile.FoundationProfile? Foundation = null,
    bool FoundationRequiredAtRuntime = false,
    CompanionWorldProfile.ControlProfile? Control = null,
    bool ControlRequiredAtRuntime = false,
    CompanionWorldProfile.ExperienceProfile? Experience = null,
    bool ExperienceRequiredAtRuntime = false)
{
    public enum ProgressionMode { Automatic, PlayerDirected, AuthoredMilestones, Fixed }
    public enum CompanionPersistence { Session, Area, Campaign, Permanent }

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

    public sealed record ValidationPolicy(
        Guid Id,
        int SchemaVersion,
        string Provenance,
        RecruitmentValidation Recruitment,
        OwnershipValidation Ownership,
        IReadOnlyList<string> CommandIds,
        EquipmentValidation Equipment,
        DeathValidation Death,
        IReadOnlyList<DialogueContextValidation> DialogueContexts,
        CrossZoneNavigationValidation CrossZoneNavigation);

    public sealed record RecruitmentValidation(
        bool AllowDialogue,
        bool AllowScript,
        string ScriptId,
        string CurrencyId,
        decimal Cost,
        double MinimumReputation);

    public sealed record OwnershipValidation(
        string Kind,
        string Authority,
        bool TransferAllowed);

    public sealed record EquipmentValidation(
        string InventoryId,
        string EquipmentPolicyId,
        IReadOnlyList<string> Slots);

    public sealed record DeathValidation(
        string PolicyId,
        string Mode,
        double RecoverySeconds);

    public sealed record DialogueContextValidation(
        string ContextId,
        Guid DialogueId,
        string Binding);

    public sealed record CrossZoneNavigationValidation(
        bool Enabled,
        IReadOnlyList<AreaId> AllowedAreaIds,
        bool FollowPortals,
        bool SafeFallback,
        string FallbackAnchorTag,
        double TimeoutSeconds);

    public sealed record FoundationProfile(
        Guid Id,
        int SchemaVersion,
        string Provenance,
        RecruitableCreatureProfile Recruitable,
        PreRecruitmentDialogueProfile PreRecruitment,
        OwnerDialogueProfile OwnerDialogue,
        RecruitDismissProfile Membership,
        PartyPolicyProfile Party);

    public sealed record RecruitableCreatureProfile(
        Guid Id,
        int SchemaVersion,
        string Provenance,
        bool Enabled,
        IReadOnlyList<string> Conditions,
        decimal Cost,
        string CurrencyId,
        double MinimumReputation,
        IReadOnlyList<string> ConsequenceIds,
        bool AllowDialogue,
        bool AllowScript,
        string ScriptId);

    public sealed record PreRecruitmentDialogueProfile(
        Guid Id,
        int SchemaVersion,
        string Provenance,
        Guid DialogueId,
        string ContextId,
        IReadOnlyList<string> Conditions,
        string OfferNodeId,
        string RefusalNodeId,
        IReadOnlyList<string> ConsequenceIds);

    public sealed record OwnerDialogueProfile(
        Guid Id,
        int SchemaVersion,
        string Provenance,
        Guid DialogueId,
        string ContextId,
        IReadOnlyList<string> CommandIds,
        bool ExposeRelationship,
        bool ExposeEquipment,
        bool ExposeProgression,
        string DepartureActionId);

    public sealed record RecruitDismissProfile(
        Guid Id,
        int SchemaVersion,
        string Provenance,
        bool EnforcePartyCapacity,
        bool TransferOwnership,
        bool TransferInventory,
        bool PreserveDismissPosition,
        CompanionPersistence Persistence,
        string InventoryId,
        string DismissAnchorTag,
        bool Atomic);

    public sealed record PartyPolicyProfile(
        Guid Id,
        int SchemaVersion,
        string Provenance,
        int CompanionSlots,
        int SummonSlots,
        int PetSlots,
        int TemporaryAllySlots,
        int ServerActorLimit,
        IReadOnlyDictionary<string, int> RoleLimits);

    public sealed record ControlProfile(
        Guid Id,
        int SchemaVersion,
        string Provenance,
        OwnershipProfile Ownership,
        MovementCommandsProfile Movement,
        TacticalCommandsProfile Tactical,
        AiControlProfile Ai,
        EquipmentControlProfile Equipment);

    public sealed record OwnershipProfile(
        Guid Id,
        int SchemaVersion,
        string Provenance,
        string Kind,
        IReadOnlyList<string> ControllerIds,
        bool TransferAllowed,
        IReadOnlyList<string> AllowedTargetKinds,
        long Revision);

    public sealed record MovementCommandsProfile(
        Guid Id,
        int SchemaVersion,
        string Provenance,
        IReadOnlyList<string> CommandKinds,
        string DefaultFormationId,
        string WaypointQuery,
        int MinimumPriority,
        int MaximumPriority);

    public sealed record TacticalCommandsProfile(
        Guid Id,
        int SchemaVersion,
        string Provenance,
        IReadOnlyList<string> CommandKinds,
        IReadOnlyList<string> Stances,
        IReadOnlyList<string> AbilityIds,
        int MinimumPriority,
        int MaximumPriority);

    public sealed record AiControlProfile(
        Guid Id,
        int SchemaVersion,
        string Provenance,
        double Autonomy,
        double RiskTolerance,
        double ResourceUsage,
        bool AllowFriendlyFire,
        string Role,
        double FollowDistance,
        IReadOnlyList<string> AllowedAbilityTags,
        Guid PlanId,
        Guid TransactionId,
        string SemanticDiff,
        string AuthorizationLevel);

    public sealed record EquipmentControlProfile(
        Guid Id,
        int SchemaVersion,
        string Provenance,
        string Permission,
        IReadOnlyList<Guid> LockedItemIds,
        IReadOnlyDictionary<string, Guid> SuggestedLoadout,
        bool AllowCompare,
        string InventoryOwnerId);

    public sealed record ExperienceProfile(
        Guid Id,
        int SchemaVersion,
        string Provenance,
        DeathExperienceProfile Death,
        CohesionExperienceProfile Cohesion,
        PartyHudProfile Hud);

    public sealed record DeathExperienceProfile(
        Guid Id,
        int SchemaVersion,
        string Provenance,
        string Mode,
        double DownedDurationSeconds,
        string? ResurrectionResourceId,
        string? ReplacementCreatureId,
        string? MemorialId,
        bool DropInventory);

    public sealed record CohesionExperienceProfile(
        Guid Id,
        int SchemaVersion,
        string Provenance,
        double NavigateThreshold,
        double PortalThreshold,
        double TeleportThreshold,
        bool FollowPortals,
        bool AllowSafeTeleport,
        IReadOnlyList<string> SafeAnchorTags);

    public sealed record PartyHudProfile(
        Guid Id,
        int SchemaVersion,
        string Provenance,
        bool Portraits,
        bool Health,
        bool Resources,
        bool Statuses,
        bool QuickCommands,
        bool Reorder,
        bool Keyboard,
        bool Gamepad,
        bool ScreenReaderLabels,
        bool HighContrast,
        IReadOnlyList<string> VisibleResourceIds);
}
