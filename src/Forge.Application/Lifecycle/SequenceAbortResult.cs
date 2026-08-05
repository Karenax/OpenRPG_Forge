namespace Forge.Application.Lifecycle;

public sealed record SequenceAbortResult(bool Accepted, string? DiagnosticCode, SequenceRuntimeState State,
    int ReleasedLocks, int RestoredControls, bool CameraRestored, bool NarrativeRolledBack, string EventHandler);
