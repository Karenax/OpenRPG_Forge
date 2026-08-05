namespace Forge.Domain.Sequences;

public sealed record SequenceBranchDefinition
{
    public Guid Id { get; }
    public int AtMilliseconds { get; }
    public string Condition { get; }
    public int TargetMilliseconds { get; }
    public int Priority { get; }

    public SequenceBranchDefinition(Guid id, int atMilliseconds, string condition, int targetMilliseconds,
        int priority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(condition);
        if (id == Guid.Empty || atMilliseconds < 0 || targetMilliseconds < 0 || priority is < -10_000 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Sequence branch is invalid.");
        }

        Id = id;
        AtMilliseconds = atMilliseconds;
        Condition = condition.Trim();
        TargetMilliseconds = targetMilliseconds;
        Priority = priority;
    }
}
