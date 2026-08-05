using System.Security.Cryptography;
using Forge.Domain.Areas;

namespace Forge.Application.Areas;

public sealed record WeatherRuntimeSnapshot(
    int SchemaVersion,
    Guid StateId,
    int Revision,
    long ServerRevision,
    double ElapsedSeconds)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record WeatherRuntimeTransition(
    Guid PreviousStateId,
    Guid CurrentStateId,
    float BlendSeconds,
    string AudioCue,
    string GameplayEffect,
    bool Replicate,
    bool Persist);

public sealed record WeatherAdvanceResult(
    WeatherRuntimeSnapshot State,
    WeatherRuntimeTransition? Transition,
    bool AuthorityDenied);

public static class WeatherRuntimeStateMachine
{
    public static WeatherRuntimeSnapshot CreateInitial(WeatherProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new(WeatherRuntimeSnapshot.CurrentSchemaVersion, profile.InitialStateId, 0, 0, 0);
    }

    public static WeatherAdvanceResult Advance(
        WeatherProfile profile,
        WeatherRuntimeSnapshot state,
        double deltaSeconds,
        bool hasAuthority)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(state);
        if (state.SchemaVersion != WeatherRuntimeSnapshot.CurrentSchemaVersion ||
            deltaSeconds < 0 || !double.IsFinite(deltaSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        }

        var current = profile.States.SingleOrDefault(candidate => candidate.Id == state.StateId)
            ?? throw new ArgumentException("Weather runtime state references an unknown profile state.", nameof(state));
        if (profile.ServerAuthoritative && !hasAuthority)
        {
            return new(state, null, AuthorityDenied: true);
        }

        var elapsed = state.ElapsedSeconds + deltaSeconds;
        var targetDuration = DeterministicDuration(profile.DeterministicSeed, state.Revision, current);
        if (elapsed < targetDuration)
        {
            return new(state with { ElapsedSeconds = elapsed }, null, AuthorityDenied: false);
        }

        var candidates = profile.States.Where(candidate => candidate.Id != current.Id).ToArray();
        if (candidates.Length == 0)
        {
            return new(state with { ElapsedSeconds = elapsed % Math.Max(1, targetDuration) }, null, false);
        }

        var index = DeterministicIndex(profile.DeterministicSeed, state.Revision, current.Id, candidates.Length);
        var next = candidates[index];
        var updated = new WeatherRuntimeSnapshot(
            WeatherRuntimeSnapshot.CurrentSchemaVersion,
            next.Id,
            state.Revision + 1,
            state.ServerRevision + 1,
            0);
        return new(
            updated,
            new(
                current.Id,
                next.Id,
                next.TransitionSeconds,
                next.AudioCue,
                next.GameplayEffect,
                profile.ReplicateState,
                profile.PersistState),
            AuthorityDenied: false);
    }

    public static WeatherStateProfile Resolve(WeatherProfile profile, WeatherRuntimeSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(state);
        return profile.States.Single(candidate => candidate.Id == state.StateId);
    }

    private static int DeterministicIndex(int seed, int revision, Guid stateId, int count)
    {
        Span<byte> input = stackalloc byte[24];
        BitConverter.TryWriteBytes(input[..4], seed);
        BitConverter.TryWriteBytes(input.Slice(4, 4), revision);
        stateId.TryWriteBytes(input[8..]);
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(input, digest);
        return (int)(BitConverter.ToUInt32(digest) % count);
    }

    private static double DeterministicDuration(int seed, int revision, WeatherStateProfile state)
    {
        if (state.MaximumDurationSeconds <= state.MinimumDurationSeconds)
        {
            return state.MaximumDurationSeconds;
        }

        Span<byte> input = stackalloc byte[24];
        BitConverter.TryWriteBytes(input[..4], seed);
        BitConverter.TryWriteBytes(input.Slice(4, 4), revision);
        state.Id.TryWriteBytes(input[8..]);
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(input, digest);
        var ratio = BitConverter.ToUInt32(digest) / (double)uint.MaxValue;
        return state.MinimumDurationSeconds +
            ((state.MaximumDurationSeconds - state.MinimumDurationSeconds) * ratio);
    }
}
