using Forge.Domain.Areas;

namespace Forge.Domain.Lifecycle;

public sealed record RestLifecycleEvent(
    Guid Id,
    string ActorId,
    string GroupId,
    AreaId AreaId,
    RestLifecycleStage Stage,
    int PlannedDurationSeconds,
    string Cause,
    EventAuthorityScope Authority,
    long ServerRevision,
    DateTimeOffset OccurredAt)
    : ProjectGameplayEvent(Id, $"rest.{Stage.ToString().ToLowerInvariant()}", ActorId, Cause, Authority,
        ServerRevision, OccurredAt);
