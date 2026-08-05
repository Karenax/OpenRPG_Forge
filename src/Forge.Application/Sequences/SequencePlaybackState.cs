namespace Forge.Application.Sequences;

public sealed record SequencePlaybackState(Guid SequenceId, int TimeMilliseconds, bool Active, bool Completed,
    bool Skipped, IReadOnlySet<Guid> VisitedBranches)
{
    public static SequencePlaybackState Start(Guid sequenceId) =>
        new(sequenceId, 0, true, false, false, new HashSet<Guid>());
}
