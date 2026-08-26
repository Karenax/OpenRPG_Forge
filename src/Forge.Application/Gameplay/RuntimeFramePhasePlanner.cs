namespace Forge.Application.Gameplay;

public enum RuntimeFramePhase
{
    Process,
    PhysicsProcess,
}

public sealed record RuntimeFramePhasePlan(
    bool ProcessInputAndUi,
    bool MutateInterpolatedCamera,
    bool AdvanceCameraCinematics);

public static class RuntimeFramePhasePlanner
{
    public static RuntimeFramePhasePlan Plan(RuntimeFramePhase phase) => phase switch
    {
        RuntimeFramePhase.Process => new(true, false, false),
        RuntimeFramePhase.PhysicsProcess => new(false, true, true),
        _ => throw new ArgumentOutOfRangeException(nameof(phase)),
    };
}
