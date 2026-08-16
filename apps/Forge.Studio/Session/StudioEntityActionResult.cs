using Forge.Domain.Entities;

namespace Forge.Studio.Session;

internal sealed record StudioEntityActionResult(
    bool Succeeded,
    string Message,
    EntityId? EntityId = null);
