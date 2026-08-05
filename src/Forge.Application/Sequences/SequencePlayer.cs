using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Application.Sequences;

public static class SequencePlayer
{
    public const string AuthorityCode = "sequence.authority_required";
    public const string BranchBudgetCode = "sequence.branch_budget_exceeded";
    public const int MaximumBranchesPerAdvance = 8;

    public static SequenceEvaluation Advance(SequenceDefinition sequence, SequencePlaybackState state,
        int deltaMilliseconds, bool hasAuthority, Func<string, bool> conditionEvaluator)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(conditionEvaluator);
        if (deltaMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaMilliseconds));
        if (!state.Active || state.SequenceId != sequence.Id) return Evaluate(sequence, state, state.TimeMilliseconds,
            state.TimeMilliseconds, [], []);
        if (sequence.MultiplayerSynchronized && !hasAuthority)
        {
            return Evaluate(sequence, state, state.TimeMilliseconds, state.TimeMilliseconds, [],
                [new DomainDiagnostic(AuthorityCode, "A synchronized sequence requires server authority.",
                    $"sequences/{sequence.Id}")]);
        }

        var from = state.TimeMilliseconds;
        var to = Math.Min(sequence.DurationMilliseconds, checked(from + deltaMilliseconds));
        var visited = new HashSet<Guid>(state.VisitedBranches);
        var traversed = 0;
        while (true)
        {
            var branch = sequence.Branches.Where(item => !visited.Contains(item.Id) && item.AtMilliseconds >= from &&
                    item.AtMilliseconds <= to && conditionEvaluator(item.Condition))
                .OrderBy(item => item.AtMilliseconds).ThenByDescending(item => item.Priority).ThenBy(item => item.Id)
                .FirstOrDefault();
            if (branch is null) break;
            visited.Add(branch.Id);
            to = branch.TargetMilliseconds;
            if (++traversed >= MaximumBranchesPerAdvance)
            {
                var bounded = state with { VisitedBranches = visited };
                return Evaluate(sequence, bounded, from, to, [],
                    [new DomainDiagnostic(BranchBudgetCode, "Sequence branch budget was exceeded.",
                        $"sequences/{sequence.Id}/branches")]);
            }
        }

        var completed = to >= sequence.DurationMilliseconds;
        var next = new SequencePlaybackState(sequence.Id, to, !completed, completed, false, visited);
        return Evaluate(sequence, next, from, to, [], []);
    }

    public static SequenceEvaluation Skip(SequenceDefinition sequence, SequencePlaybackState state)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(state);
        if (!sequence.Skippable || !state.Active) return Evaluate(sequence, state, state.TimeMilliseconds,
            state.TimeMilliseconds, [], []);
        var next = state with
        {
            TimeMilliseconds = sequence.DurationMilliseconds,
            Active = false,
            Completed = true,
            Skipped = true,
        };
        return Evaluate(sequence, next, state.TimeMilliseconds, sequence.DurationMilliseconds, [], []);
    }

    private static SequenceEvaluation Evaluate(SequenceDefinition sequence, SequencePlaybackState state, int from,
        int to, IReadOnlyList<SequenceSignal> additionalSignals, IReadOnlyList<DomainDiagnostic> diagnostics)
    {
        Guid? camera = null;
        var transforms = new Dictionary<Guid, TransformValue>();
        var audio = new List<string>();
        var signals = new List<SequenceSignal>(additionalSignals);
        foreach (var track in sequence.Tracks)
        {
            var current = track.Keyframes.LastOrDefault(item => item.TimeMilliseconds <= to);
            switch (track.Kind)
            {
                case SequenceTrackKind.Camera when current is not null:
                    camera = track.TargetId;
                    break;
                case SequenceTrackKind.Transform when track.TargetId is not null:
                    var before = current;
                    var after = track.Keyframes.FirstOrDefault(item => item.TimeMilliseconds > to);
                    if (before?.Transform is not null)
                    {
                        transforms[track.TargetId.Value] = after?.Transform is null
                            ? before.Transform
                            : TransformInterpolator.Sample(before.Transform, after.Transform,
                                (to - before.TimeMilliseconds) /
                                (double)(after.TimeMilliseconds - before.TimeMilliseconds), after.Easing);
                    }
                    break;
                case SequenceTrackKind.Audio:
                    if (track.Keyframes.Any(item => item.TimeMilliseconds > from && item.TimeMilliseconds <= to))
                        audio.Add(track.ResourcePath);
                    break;
                case SequenceTrackKind.Signal:
                case SequenceTrackKind.State:
                    signals.AddRange(track.Keyframes.Where(item => item.TimeMilliseconds > from &&
                            item.TimeMilliseconds <= to)
                        .Select(item => new SequenceSignal(item.Id, item.TimeMilliseconds, item.Value)));
                    break;
            }
        }

        return new SequenceEvaluation(state, camera, transforms, audio.Distinct(StringComparer.Ordinal).ToArray(),
            signals.OrderBy(item => item.TimeMilliseconds).ThenBy(item => item.KeyframeId).ToArray(), diagnostics);
    }
}
