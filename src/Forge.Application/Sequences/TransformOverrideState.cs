using Forge.Domain.Spatial;

namespace Forge.Application.Sequences;

public sealed record TransformOverrideState(Guid ComponentId, int ElapsedMilliseconds, TransformValue Transform,
    bool Active, bool Cancelled, bool Completed);
