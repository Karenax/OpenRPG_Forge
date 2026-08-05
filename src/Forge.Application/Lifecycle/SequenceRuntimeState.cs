using Forge.Domain.Gameplay;

namespace Forge.Application.Lifecycle;

public sealed record SequenceRuntimeState(string CameraId, IReadOnlyList<ControlContext> ControlStack,
    IReadOnlySet<string> TemporarilyInvulnerableActors, IReadOnlySet<string> LockIds, long NarrativeRevision,
    string? NarrativeTransactionId, bool SequenceActive);
