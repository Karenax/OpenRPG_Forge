using System.Globalization;

namespace Forge.RpgKit.Domain.Placeables;

public sealed record ContainerComponent(int CapacitySlots, double CapacityMass, string OwnershipPolicy, IReadOnlySet<string> AccessPermissions, bool Persistent, string LootPolicyId, string UiProfileId, bool Replicated);
public sealed record PlaceableVisualState(string Id, string AnimationId, string VisualOverrideId, bool Interactable);
public sealed record PlaceableStateTransition(string From, string To, string Trigger, TimeSpan Duration, string Condition);
public sealed record PlaceableStateMachine(string InitialState, IReadOnlyList<PlaceableVisualState> States, IReadOnlyList<PlaceableStateTransition> Transitions);
public sealed record DamageableComponent(double MaximumHealth, double CurrentHealth, double Armor, IReadOnlyDictionary<string, double> Resistances, IReadOnlyList<double> VisualStageThresholds, string DestructionEffectId, string DebrisDefinitionId, bool Repairable, double RepairCost);
public sealed record PlaceableRuntimeProfile(ContainerComponent? Container, PlaceableStateMachine StateMachine, DamageableComponent? Damageable, IReadOnlyList<string> LockReferences, IReadOnlyList<string> TrapReferences, IReadOnlyList<string> InventoryReferences, IReadOnlyList<string> PhysicsProfiles, int DrawCallBudget, int ScriptBudget);
public sealed record InteractionExecuted(Guid EventId, int SchemaVersion, string Provenance, Guid PlaceableId, string Verb, string ActorId, Vector3Value TargetPoint, string Result, DateTimeOffset OccurredAt);
public sealed record PlaceableStateResult(string PreviousState, string CurrentState, PlaceableStateTransition Transition, TimeSpan PreviewDuration);
public sealed record PlaceableDamageResult(DamageableComponent Damageable, string VisualStage, bool Destroyed, string? DebrisDefinitionId, string EventName);

public static class PlaceableInteractionService
{
    public static IReadOnlyList<PlaceableDiagnostic> Validate(PlaceableDefinition definition, PlaceableRuntimeProfile profile)
    {
        var diagnostics = PlaceableRules.Validate(definition).ToList(); var root = $"placeables/{definition.Id}";
        if (profile.Container is { } container && (container.CapacitySlots < 1 || container.CapacityMass < 0 || string.IsNullOrWhiteSpace(container.OwnershipPolicy) || string.IsNullOrWhiteSpace(container.LootPolicyId) || string.IsNullOrWhiteSpace(container.UiProfileId))) diagnostics.Add(new("RPG_PLACEABLE_CONTAINER_INVALID", $"{root}/container", "Container capacity, ownership, loot and UI are required."));
        if (profile.StateMachine.States.All(item => item.Id != profile.StateMachine.InitialState)) diagnostics.Add(new("RPG_PLACEABLE_STATE_INITIAL_MISSING", $"{root}/states", "Initial visual state does not exist."));
        foreach (var transition in profile.StateMachine.Transitions.Where(item => profile.StateMachine.States.All(state => state.Id != item.From) || profile.StateMachine.States.All(state => state.Id != item.To))) diagnostics.Add(new("RPG_PLACEABLE_STATE_TRANSITION_INVALID", $"{root}/states/{transition.Trigger}", "Transition references an unknown state."));
        if (profile.Damageable is { } damage && (damage.MaximumHealth <= 0 || damage.CurrentHealth is < 0 || damage.CurrentHealth > damage.MaximumHealth || damage.Armor < 0 || damage.VisualStageThresholds.Any(value => value is < 0 or > 1) || damage.RepairCost < 0)) diagnostics.Add(new("RPG_PLACEABLE_DAMAGEABLE_INVALID", $"{root}/damageable", "Health, armor, stages or repair are invalid."));
        if (definition.Collision.NavigationObstacle && !definition.Collision.Enabled) diagnostics.Add(new("RPG_PLACEABLE_NAVIGATION_COLLISION_MISSING", $"{root}/collision", "Navigation obstacle requires collision."));
        if (definition.Components.HasLock && profile.LockReferences.Count == 0) diagnostics.Add(new("RPG_PLACEABLE_LOCK_REFERENCE_MISSING", $"{root}/locks", "Lock component requires a definition."));
        if (definition.Components.HasTrap && profile.TrapReferences.Count == 0) diagnostics.Add(new("RPG_PLACEABLE_TRAP_REFERENCE_MISSING", $"{root}/traps", "Trap component requires a definition."));
        if (definition.Components.HasContainer && (profile.Container is null || profile.InventoryReferences.Count == 0)) diagnostics.Add(new("RPG_PLACEABLE_INVENTORY_REFERENCE_MISSING", $"{root}/inventory", "Container requires inventory references."));
        if (definition.Components.HasPhysics && profile.PhysicsProfiles.Count == 0) diagnostics.Add(new("RPG_PLACEABLE_PHYSICS_PROFILE_MISSING", $"{root}/physics", "Physics component requires a profile."));
        if (definition.Components.ScriptBindings.Count > profile.ScriptBudget || profile.ScriptBudget < 0 || profile.DrawCallBudget < definition.Visual.Meshes.Count) diagnostics.Add(new("RPG_PLACEABLE_BUDGET_EXCEEDED", root, "Script or draw-call budget is exceeded."));
        return diagnostics;
    }

    public static InteractionExecuted Execute(PlaceableDefinition definition, string verb, string actorId, Vector3Value targetPoint, string result, string provenance, DateTimeOffset at)
    {
        if (definition.Interaction is null || definition.Interaction.Verbs.All(item => item.Id != verb)) throw new InvalidOperationException("RPG_PLACEABLE_VERB_UNAVAILABLE");
        if (string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(result)) throw new InvalidOperationException("RPG_PLACEABLE_INTERACTION_CONTEXT_INVALID");
        return new(Guid.NewGuid(), 1, provenance, definition.Id, verb, actorId, targetPoint, result, at);
    }

    public static PlaceableStateResult Transition(PlaceableStateMachine machine, string currentState, string trigger, IReadOnlySet<string> conditions)
    {
        var transition = machine.Transitions.Where(item => item.From == currentState && item.Trigger == trigger && (string.IsNullOrWhiteSpace(item.Condition) || conditions.Contains(item.Condition))).OrderBy(item => item.To, StringComparer.Ordinal).FirstOrDefault() ?? throw new InvalidOperationException("RPG_PLACEABLE_TRANSITION_UNAVAILABLE");
        return new(currentState, transition.To, transition, transition.Duration);
    }

    public static PlaceableDamageResult ApplyDamage(DamageableComponent damageable, double rawDamage, string damageType)
    {
        if (rawDamage < 0) throw new InvalidOperationException("RPG_PLACEABLE_DAMAGE_INVALID");
        var resisted = rawDamage * (1 - Math.Clamp(damageable.Resistances.GetValueOrDefault(damageType), 0, 1)); var applied = Math.Max(0, resisted - damageable.Armor); var health = Math.Max(0, damageable.CurrentHealth - applied);
        var ratio = health / damageable.MaximumHealth; var stage = damageable.VisualStageThresholds.OrderByDescending(value => value).FirstOrDefault(value => ratio <= value);
        var updated = damageable with { CurrentHealth = health }; var destroyed = health <= 0;
        return new(updated, destroyed ? "destroyed" : $"damaged:{stage.ToString("0.##", CultureInfo.InvariantCulture)}", destroyed, destroyed ? damageable.DebrisDefinitionId : null, destroyed ? "PlaceableDestroyed" : "PlaceableDamaged");
    }

    public static DamageableComponent Repair(DamageableComponent damageable, double amount)
    {
        if (!damageable.Repairable || amount <= 0) throw new InvalidOperationException("RPG_PLACEABLE_REPAIR_UNAVAILABLE");
        return damageable with { CurrentHealth = Math.Min(damageable.MaximumHealth, damageable.CurrentHealth + amount) };
    }
}
