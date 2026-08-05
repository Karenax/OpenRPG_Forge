namespace Forge.Domain.RuntimePolicies;

[Flags]
public enum SequenceAbortRestoreFlags
{
    None = 0,
    Camera = 1,
    PlayerControl = 2,
    TemporaryInvulnerability = 4,
    Locks = 8,
    NarrativeTransaction = 16,
    All = Camera | PlayerControl | TemporaryInvulnerability | Locks | NarrativeTransaction,
}
