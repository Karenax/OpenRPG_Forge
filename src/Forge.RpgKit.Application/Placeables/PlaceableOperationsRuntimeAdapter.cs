using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Domain.Placeables;

namespace Forge.RpgKit.Application.Placeables;

public sealed record PlaceableOperationsHandle(Guid PlaceableId, Guid OperationsId);

public sealed record PlaceableOperationsResolution(
    bool Available,
    bool OptionalFallback,
    PlaceableOperationsHandle? Handle,
    string? DiagnosticCode);

public sealed record PlaceableOperationsRuntime(
    PlaceableDefinition Definition,
    PlaceableRuntimeProfile Profile,
    PlaceableProtectionPolicy? Protection,
    LockComponent? Lock,
    string? ProtectionAuditBinding);

public sealed class PlaceableOperationsRuntimeAdapter
{
    public PlaceableOperationsResolution Resolve(PlaceableWorldProfile placeable)
    {
        if (placeable.Operations is null)
        {
            return placeable.OperationsRequiredAtRuntime
                ? new(false, false, null, "WORLD_PLACEABLE_OPERATIONS_REQUIRED_MISSING")
                : new(false, true, null, null);
        }

        return new(true, false, new(placeable.Id, placeable.Operations.Id), null);
    }

    public PlaceableOperationsRuntime Build(PlaceableOperationsHandle handle, PlaceableWorldProfile placeable)
    {
        var operations = Require(handle, placeable);
        var foundation = placeable.Foundation ??
            throw new InvalidOperationException("WORLD_PLACEABLE_FOUNDATION_REQUIRED_MISSING");
        var definition = ToDefinition(placeable, foundation);
        var validation = foundation.Validation;
        var profile = new PlaceableRuntimeProfile(
            operations.Container is null ? null : new ContainerComponent(
                operations.Container.CapacitySlots,
                operations.Container.CapacityMass,
                operations.Container.OwnershipPolicy,
                operations.Container.AccessPermissions.ToHashSet(StringComparer.Ordinal),
                operations.Container.Persistent,
                operations.Container.LootPolicyId,
                operations.Container.UiProfileId,
                operations.Container.Replicated),
            operations.StateMachine is null
                ? new PlaceableStateMachine("idle", [new("idle", "idle", "default", true)], [])
                : new PlaceableStateMachine(
                    operations.StateMachine.InitialStateId,
                    operations.StateMachine.States.Select(item => new PlaceableVisualState(
                        item.StateId, item.AnimationId, item.VisualOverrideId, item.Interactable)).ToArray(),
                    operations.StateMachine.Transitions.Select(item => new PlaceableStateTransition(
                        item.FromStateId, item.ToStateId, item.Trigger,
                        TimeSpan.FromSeconds(item.DurationSeconds), item.Condition ?? string.Empty)).ToArray()),
            operations.Damageable is null ? null : new DamageableComponent(
                operations.Damageable.MaximumHealth,
                operations.Damageable.CurrentHealth,
                operations.Damageable.Armor,
                operations.Damageable.Resistances,
                operations.Damageable.VisualStageThresholds,
                operations.Damageable.DestructionEffectId,
                operations.Damageable.DebrisDefinitionId,
                operations.Damageable.Repairable,
                operations.Damageable.RepairCost),
            validation?.LockIds ?? [],
            validation?.TrapIds ?? [],
            operations.Container is null ? validation?.InventoryIds ?? [] : [operations.Container.InventoryId],
            validation?.PhysicsProfileIds ?? [],
            validation?.DrawCallBudget ?? Math.Max(1, foundation.Visual.MeshIds.Count),
            validation?.ScriptBudget ?? foundation.Components.ScriptBindings.Count);
        var protection = operations.Protection is null ? null : new PlaceableProtectionPolicy(
            operations.Protection.ProtectedActions.ToHashSet(StringComparer.Ordinal),
            operations.Protection.RequiredPermissions.ToHashSet(StringComparer.Ordinal),
            operations.Protection.Feedback,
            operations.Protection.AllowGameMasterOverride);
        var lockComponent = operations.Lock is null ? null : new LockComponent(
            MapLockState(operations.Lock.InitialState),
            operations.Lock.CredentialIds.ToHashSet(StringComparer.Ordinal),
            operations.Lock.SkillId ?? string.Empty,
            operations.Lock.Difficulty,
            operations.Lock.CodeHash,
            operations.Lock.Hackable,
            operations.Lock.Relockable,
            operations.Lock.CanJam,
            operations.Lock.RequiredPermissions.ToHashSet(StringComparer.Ordinal));
        return new(definition, profile, protection, lockComponent, operations.Protection?.AuditBinding);
    }

    public IReadOnlyList<PlaceableDiagnostic> Validate(PlaceableOperationsHandle handle, PlaceableWorldProfile placeable)
    {
        var runtime = Build(handle, placeable);
        return PlaceableInteractionService.Validate(runtime.Definition, runtime.Profile);
    }

    public PlaceableStateResult PreviewTransition(
        PlaceableOperationsHandle handle,
        PlaceableWorldProfile placeable,
        string currentState,
        string trigger,
        IReadOnlySet<string> conditions)
        => PlaceableInteractionService.Transition(Build(handle, placeable).Profile.StateMachine,
            currentState, trigger, conditions);

    public PlaceableDamageResult Damage(
        PlaceableOperationsHandle handle,
        PlaceableWorldProfile placeable,
        double rawDamage,
        string damageType)
        => PlaceableInteractionService.ApplyDamage(
            Build(handle, placeable).Profile.Damageable ??
            throw new InvalidOperationException("WORLD_PLACEABLE_DAMAGEABLE_MISSING"), rawDamage, damageType);

    public DamageableComponent Repair(
        PlaceableOperationsHandle handle,
        PlaceableWorldProfile placeable,
        DamageableComponent damageable,
        double amount)
    {
        Require(handle, placeable);
        return PlaceableInteractionService.Repair(damageable, amount);
    }

    public ProtectionDecision Authorize(
        PlaceableOperationsHandle handle,
        PlaceableWorldProfile placeable,
        string action,
        IReadOnlySet<string> permissions,
        bool gameMasterOverride,
        string actorId,
        string reason)
        => PlaceableSecurityService.Authorize(
            Build(handle, placeable).Protection ??
            throw new InvalidOperationException("WORLD_PLACEABLE_PROTECTION_MISSING"),
            action, permissions, gameMasterOverride, actorId, reason);

    public LockResult Unlock(
        PlaceableOperationsHandle handle,
        PlaceableWorldProfile placeable,
        LockAttempt attempt)
        => PlaceableSecurityService.AttemptUnlock(
            Build(handle, placeable).Lock ??
            throw new InvalidOperationException("WORLD_PLACEABLE_LOCK_MISSING"), attempt);

    public LockResult Relock(
        PlaceableOperationsHandle handle,
        PlaceableWorldProfile placeable,
        LockComponent component,
        bool jam,
        string actorId)
    {
        Require(handle, placeable);
        return PlaceableSecurityService.Relock(component, jam, actorId);
    }

    private static PlaceableOperationsProfile Require(
        PlaceableOperationsHandle handle,
        PlaceableWorldProfile placeable)
    {
        if (handle.PlaceableId != placeable.Id || placeable.Operations is null ||
            handle.OperationsId != placeable.Operations.Id)
            throw new InvalidOperationException("WORLD_PLACEABLE_OPERATIONS_HANDLE_STALE");
        return placeable.Operations;
    }

    private static PlaceableDefinition ToDefinition(
        PlaceableWorldProfile placeable,
        PlaceableWorldProfile.FoundationPolicy foundation)
        => new(
            placeable.Id,
            foundation.SchemaVersion,
            foundation.Provenance,
            foundation.Slug,
            foundation.Texts.Select(item => new LocalizedPlaceableText(item.Locale, item.Name, item.Prompt)).ToArray(),
            foundation.Tags.ToHashSet(StringComparer.Ordinal),
            foundation.AuthorNotes,
            new PlaceableVisualDefinition(foundation.Visual.MeshIds, foundation.Visual.MaterialIds,
                foundation.Visual.IconAssetId, foundation.Visual.PortraitAssetId, foundation.Visual.VisualEffectIds),
            new CollisionDefinition(foundation.Collision.Shape, Vector(foundation.Collision.Size),
                foundation.Collision.Enabled, foundation.Collision.NavigationObstacle, foundation.Collision.PhysicsLayer),
            foundation.Interaction is null ? null : new InteractableComponent(
                foundation.Interaction.Verbs.Select(item => new InteractionVerb(item.LabelKey, item.LabelKey,
                    item.EventName, item.Condition)).ToArray(), foundation.Interaction.MaximumDistance,
                foundation.Interaction.MaximumAngleDegrees, foundation.Interaction.RequiresFocus,
                TimeSpan.FromSeconds(foundation.Interaction.DurationSeconds), foundation.Interaction.Authority,
                foundation.Interaction.PromptKey, foundation.Interaction.Conditions.ToHashSet(StringComparer.Ordinal)),
            foundation.StaticProp is null ? null : new StaticPropComponent(
                foundation.StaticProp.Batched, foundation.StaticProp.Instanced, foundation.StaticProp.Occluder,
                foundation.StaticProp.MergeEligible, foundation.StaticProp.CollisionEnabled),
            new PlaceableComponents(foundation.Components.HasHealth, foundation.Components.HasContainer,
                foundation.Components.HasLock, foundation.Components.HasTrap, foundation.Components.HasPhysics,
                foundation.Components.ScriptBindings.Values.ToArray()),
            new PlaceableTransform(Vector(foundation.Transform.Position), Vector(foundation.Transform.RotationDegrees),
                Vector(foundation.Transform.Scale), Vector(foundation.Transform.Pivot), foundation.Transform.SnapIncrement,
                foundation.Transform.ParentId, foundation.Transform.AlignToSurface,
                foundation.Transform.TransformAnimationId));

    private static Vector3Value Vector(Forge.Domain.Spatial.Vector3Value value)
        => new(value.X, value.Y, value.Z);

    private static LockState MapLockState(PlaceableAuthoredLockState state) => state switch
    {
        PlaceableAuthoredLockState.Unlocked => LockState.Unlocked,
        PlaceableAuthoredLockState.Locked => LockState.Locked,
        PlaceableAuthoredLockState.Jammed => LockState.Jammed,
        PlaceableAuthoredLockState.Broken => LockState.Broken,
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };
}
