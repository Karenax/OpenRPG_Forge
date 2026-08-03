namespace Forge.Domain.Lifecycle;

public enum EventAuthorityScope { Local, Owner, Server }
public enum PlayerConnectionStage { Joining, Ready, Leaving, Disconnected }
public enum InventoryChangeKind { Acquired, Lost }
public enum LifeCycleStage { Downed, Dying, Dead, RespawnRequested, Respawned }
public enum EquipmentChangeKind { Equipped, Unequipped }

public sealed record EventBinding(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string ForgeScriptApiVersion,
    string EventName,
    string Handler,
    EventAuthorityScope Authority);

public abstract record ProjectGameplayEvent(
    Guid Id,
    string EventName,
    string ActorId,
    string Cause,
    EventAuthorityScope Authority,
    long ServerRevision,
    DateTimeOffset OccurredAt);

public sealed record PlayerConnectionEvent(
    Guid Id, string ActorId, PlayerConnectionStage Stage, string Cause,
    EventAuthorityScope Authority, long ServerRevision, DateTimeOffset OccurredAt)
    : ProjectGameplayEvent(Id, $"player.{Stage.ToString().ToLowerInvariant()}", ActorId, Cause, Authority, ServerRevision, OccurredAt);

public sealed record InventoryChangedEvent(
    Guid Id, string ActorId, string ItemId, InventoryChangeKind Change, int Quantity,
    string Cause, string SourceId, EventAuthorityScope Authority, long ServerRevision, DateTimeOffset OccurredAt)
    : ProjectGameplayEvent(Id, "inventory.changed", ActorId, Cause, Authority, ServerRevision, OccurredAt);

public sealed record ItemUsedEvent(
    Guid Id, string ActorId, string ItemId, string? AbilityId, string? TargetId, int ChargeCost,
    string Cause, EventAuthorityScope Authority, long ServerRevision, DateTimeOffset OccurredAt)
    : ProjectGameplayEvent(Id, AbilityId is null ? "item.used" : "ability.activated", ActorId, Cause, Authority, ServerRevision, OccurredAt);

public sealed record LifeCycleEvent(
    Guid Id, string ActorId, LifeCycleStage Stage, string? CheckpointId, string Cause,
    EventAuthorityScope Authority, long ServerRevision, DateTimeOffset OccurredAt)
    : ProjectGameplayEvent(Id, $"life.{Stage.ToString().ToLowerInvariant()}", ActorId, Cause, Authority, ServerRevision, OccurredAt);

public sealed record EquipmentChangedEvent(
    Guid Id, string ActorId, string ItemId, string Slot, EquipmentChangeKind Change,
    string Cause, EventAuthorityScope Authority, long ServerRevision, DateTimeOffset OccurredAt)
    : ProjectGameplayEvent(Id, "equipment.changed", ActorId, Cause, Authority, ServerRevision, OccurredAt);

public sealed record ProgressionChangedEvent(
    Guid Id, string ActorId, int PreviousLevel, int NewLevel, string Cause,
    EventAuthorityScope Authority, long ServerRevision, DateTimeOffset OccurredAt)
    : ProjectGameplayEvent(Id, "progression.changed", ActorId, Cause, Authority, ServerRevision, OccurredAt);

public sealed record EventTransactionResult(bool Accepted, string? DiagnosticCode, ProjectGameplayEvent? Event);
