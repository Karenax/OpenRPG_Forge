using Forge.Domain.Lifecycle;

namespace Forge.Application.Lifecycle;

public sealed class ProjectGameplayEventBus
{
    private readonly List<ProjectGameplayEvent> _journal = [];
    private readonly List<Func<ProjectGameplayEvent, string?>> _vetoes = [];

    public IReadOnlyList<ProjectGameplayEvent> Journal => _journal;

    public void RegisterVeto(Func<ProjectGameplayEvent, string?> veto) => _vetoes.Add(veto);

    public EventTransactionResult Publish(ProjectGameplayEvent @event)
    {
        Validate(@event);
        foreach (var veto in _vetoes)
        {
            var code = veto(@event);
            if (!string.IsNullOrWhiteSpace(code)) return new EventTransactionResult(false, code, null);
        }
        _journal.Add(@event);
        return new EventTransactionResult(true, null, @event);
    }

    public EventTransactionResult Publish(
        ProjectGameplayEvent @event,
        ProjectGameplayEventProfile? profile,
        bool required = false)
    {
        var resolution = ProjectGameplayEventProfileRuntime.Resolve(
            profile,
            required || profile?.RequiredAtRuntime == true);
        if (resolution.Diagnostics.Count > 0)
            return new EventTransactionResult(false, resolution.Diagnostics[0].Code, null);
        if (resolution.Handle is null)
            return Publish(@event);
        if (!resolution.Handle.Bindings.TryGetValue(@event.EventName, out var binding))
            return new EventTransactionResult(false,
                ProjectGameplayEventProfileValidator.BindingSetInvalidCode, null);
        if (binding.Authority != @event.Authority)
            return new EventTransactionResult(false,
                ProjectGameplayEventProfileValidator.BindingContractInvalidCode, null);
        return Publish(@event);
    }

    public static IReadOnlyList<LifeCycleEvent> CreateDeathPipeline(
        string actorId, string cause, long serverRevision, DateTimeOffset occurredAt, bool useDownedState, string? checkpointId)
    {
        var stages = useDownedState
            ? new[] { LifeCycleStage.Downed, LifeCycleStage.Dying, LifeCycleStage.Dead, LifeCycleStage.RespawnRequested, LifeCycleStage.Respawned }
            : new[] { LifeCycleStage.Dying, LifeCycleStage.Dead, LifeCycleStage.RespawnRequested, LifeCycleStage.Respawned };
        return stages.Select((stage, index) => new LifeCycleEvent(
            Guid.NewGuid(), actorId, stage, checkpointId, cause, EventAuthorityScope.Server,
            serverRevision + index, occurredAt.AddTicks(index))).ToArray();
    }

    public static IReadOnlyList<LifeCycleEvent> CreateDeathPipeline(
        ProjectGameplayEventProfile profile,
        string actorId,
        string cause,
        long serverRevision,
        DateTimeOffset occurredAt,
        string? checkpointId)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var stages = new List<LifeCycleStage>();
        if (profile.UseDownedState) stages.Add(LifeCycleStage.Downed);
        stages.Add(LifeCycleStage.Dying);
        stages.Add(LifeCycleStage.Dead);
        if (profile.AutoRespawn)
        {
            stages.Add(LifeCycleStage.RespawnRequested);
            stages.Add(LifeCycleStage.Respawned);
        }

        return stages.Select((stage, index) => new LifeCycleEvent(
            Guid.NewGuid(), actorId, stage, checkpointId, cause, EventAuthorityScope.Server,
            serverRevision + index, occurredAt.AddTicks(index))).ToArray();
    }

    public static EventBinding ValidateBinding(EventBinding binding)
    {
        if (binding.Id == Guid.Empty) throw new InvalidOperationException("PROJECT_EVENT_UUID_REQUIRED");
        if (binding.SchemaVersion != 1) throw new InvalidOperationException("PROJECT_EVENT_SCHEMA_UNSUPPORTED");
        if (string.IsNullOrWhiteSpace(binding.Provenance) || string.IsNullOrWhiteSpace(binding.ForgeScriptApiVersion) ||
            string.IsNullOrWhiteSpace(binding.EventName) || string.IsNullOrWhiteSpace(binding.Handler))
            throw new InvalidOperationException("PROJECT_EVENT_BINDING_INVALID");
        return binding;
    }

    private static void Validate(ProjectGameplayEvent @event)
    {
        if (@event.Id == Guid.Empty) throw new InvalidOperationException("PROJECT_EVENT_UUID_REQUIRED");
        if (string.IsNullOrWhiteSpace(@event.ActorId)) throw new InvalidOperationException("PROJECT_EVENT_ACTOR_REQUIRED");
        if (@event.ServerRevision < 0) throw new InvalidOperationException("PROJECT_EVENT_REVISION_INVALID");
        switch (@event)
        {
            case InventoryChangedEvent inventory when string.IsNullOrWhiteSpace(inventory.ItemId) || inventory.Quantity <= 0:
                throw new InvalidOperationException("PROJECT_INVENTORY_EVENT_INVALID");
            case ItemUsedEvent used when string.IsNullOrWhiteSpace(used.ItemId) || used.ChargeCost < 0:
                throw new InvalidOperationException("PROJECT_ITEM_USED_INVALID");
            case EquipmentChangedEvent equipment when string.IsNullOrWhiteSpace(equipment.ItemId) || string.IsNullOrWhiteSpace(equipment.Slot):
                throw new InvalidOperationException("PROJECT_EQUIPMENT_EVENT_INVALID");
            case ProgressionChangedEvent progression when progression.PreviousLevel < 0 || progression.NewLevel <= progression.PreviousLevel:
                throw new InvalidOperationException("PROJECT_PROGRESSION_EVENT_INVALID");
            case RestLifecycleEvent rest when rest.AreaId.Value == Guid.Empty || rest.PlannedDurationSeconds <= 0 ||
                                              string.IsNullOrWhiteSpace(rest.GroupId):
                throw new InvalidOperationException("PROJECT_REST_EVENT_INVALID");
        }
    }
}
