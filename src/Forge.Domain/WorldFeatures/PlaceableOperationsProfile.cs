namespace Forge.Domain.WorldFeatures;

public enum PlaceableAuthoredLockState
{
    Unlocked,
    Locked,
    Jammed,
    Broken,
}

public sealed record PlaceableOperationsProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    PlaceableContainerProfile? Container,
    PlaceableVisualStateMachineProfile? StateMachine,
    PlaceableDamageableProfile? Damageable,
    PlaceableProtectionProfile? Protection,
    PlaceableLockProfile? Lock);

public sealed record PlaceableContainerProfile(
    Guid Id,
    int CapacitySlots,
    double CapacityMass,
    string OwnershipPolicy,
    IReadOnlyList<string> AccessPermissions,
    bool Persistent,
    string LootPolicyId,
    string UiProfileId,
    bool Replicated,
    string InventoryId);

public sealed record PlaceableVisualStateProfile(
    Guid Id,
    string StateId,
    string AnimationId,
    string VisualOverrideId,
    bool Interactable);

public sealed record PlaceableVisualTransitionProfile(
    Guid Id,
    string FromStateId,
    string ToStateId,
    string Trigger,
    double DurationSeconds,
    string? Condition);

public sealed record PlaceableVisualStateMachineProfile(
    Guid Id,
    string InitialStateId,
    IReadOnlyList<PlaceableVisualStateProfile> States,
    IReadOnlyList<PlaceableVisualTransitionProfile> Transitions);

public sealed record PlaceableDamageableProfile(
    Guid Id,
    double MaximumHealth,
    double CurrentHealth,
    double Armor,
    IReadOnlyDictionary<string, double> Resistances,
    IReadOnlyList<double> VisualStageThresholds,
    string DestructionEffectId,
    string DebrisDefinitionId,
    bool Repairable,
    double RepairCost);

public sealed record PlaceableProtectionProfile(
    Guid Id,
    IReadOnlyList<string> ProtectedActions,
    IReadOnlyList<string> RequiredPermissions,
    string Feedback,
    bool AllowGameMasterOverride,
    string AuditBinding);

public sealed record PlaceableLockProfile(
    Guid Id,
    PlaceableAuthoredLockState InitialState,
    IReadOnlyList<string> CredentialIds,
    string? SkillId,
    double Difficulty,
    string? CodeHash,
    bool Hackable,
    bool Relockable,
    bool CanJam,
    IReadOnlyList<string> RequiredPermissions);
