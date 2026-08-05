using Forge.Domain.Spatial;

namespace Forge.Application.Sequences;

public sealed record AnimationTweenState(Guid DefinitionId, int ElapsedMilliseconds, TransformValue? Transform,
    AnimationTweenValue Value, bool Active, bool Cancelled, bool Completed);
