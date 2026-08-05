using Forge.Domain.Areas;
using Forge.Domain.RuntimePolicies;

namespace Forge.Application.Lifecycle;

public sealed record RestSession(Guid Id, string ActorId, string GroupId, AreaId AreaId,
    PartyRestPolicy PartyPolicy, DateTimeOffset StartedAt, DateTimeOffset CompletesAt, int CooldownSeconds,
    bool Active);
