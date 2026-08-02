namespace Forge.Domain.Lifecycle;

public interface IRuntimeLifecycleEventIdSource
{
    RuntimeLifecycleEventId Next();
}

public sealed class RandomRuntimeLifecycleEventIdSource : IRuntimeLifecycleEventIdSource
{
    public RuntimeLifecycleEventId Next() => RuntimeLifecycleEventId.From(Guid.NewGuid());
}
