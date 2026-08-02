namespace Forge.Domain.Lifecycle;

public enum RuntimeLifecycleCause
{
    Startup,
    Playtest,
    SaveResume,
    PortalTransition,
    ScriptedTransition,
    ServerTransfer,
}
