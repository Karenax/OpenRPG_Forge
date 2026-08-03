using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Gameplay;

namespace Forge.Application.Gameplay;

public static class GameplaySessionServices
{
    public static SaveEnvelope ResolveSave(SaveEnvelope current, SaveEnvelope incoming)
    {
        Validate(incoming.Profile.Id, incoming.Profile.SchemaVersion, incoming.Profile.Provenance);
        if (current.Profile.Scope != incoming.Profile.Scope || current.OwnerId != incoming.OwnerId)
            throw new InvalidOperationException("GAME_SAVE_SCOPE_MISMATCH");
        if (incoming.Revision <= current.Revision)
            return incoming.Profile.ConflictPolicy switch
            {
                SaveConflictPolicy.Reject => throw new InvalidOperationException("GAME_SAVE_REVISION_CONFLICT"),
                SaveConflictPolicy.ServerWins when current.Profile.Authority == SaveAuthority.Server => current,
                _ => incoming.SavedAt > current.SavedAt ? incoming : current
            };
        return incoming;
    }

    public static CheckpointProfile ValidateCheckpoint(CheckpointProfile profile)
    {
        Validate(profile.Id, profile.SchemaVersion, profile.Provenance);
        if (string.IsNullOrWhiteSpace(profile.WaypointId)) throw new InvalidOperationException("GAME_CHECKPOINT_WAYPOINT_REQUIRED");
        if (profile.CurrencyCost < 0 || profile.SafeRadius < 0) throw new InvalidOperationException("GAME_CHECKPOINT_RANGE_INVALID");
        return profile;
    }

    public static RuntimeShellProfile ValidateShell(RuntimeShellProfile profile)
    {
        Validate(profile.Id, profile.SchemaVersion, profile.Provenance);
        if (string.IsNullOrWhiteSpace(profile.SkinId) || profile.Scale is < 0.5 or > 3) throw new InvalidOperationException("GAME_SHELL_PROFILE_INVALID");
        return profile;
    }

    public static IReadOnlyList<ControlContext> PushControl(IReadOnlyList<ControlContext> stack, ControlContext context)
    {
        Validate(context.Id, context.SchemaVersion, context.Provenance);
        if (string.IsNullOrWhiteSpace(context.OwnerId)) throw new InvalidOperationException("GAME_CONTROL_OWNER_REQUIRED");
        return stack.Append(context).ToArray();
    }

    public static (IReadOnlyList<ControlContext> Stack, ControlContext? Restored) ReleaseOwner(IReadOnlyList<ControlContext> stack, string ownerId)
    {
        var remaining = stack.Where(context => !string.Equals(context.OwnerId, ownerId, StringComparison.Ordinal)).ToArray();
        return (remaining, remaining.LastOrDefault());
    }

    public static string HashState(string canonicalState) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalState)));

    public static ReplayDivergence? Replay(ReplayRecording recording, Func<ReplayFrame, string> stateProvider)
    {
        Validate(recording.Id, recording.SchemaVersion, recording.Provenance);
        foreach (var frame in recording.Frames.OrderBy(frame => frame.Tick))
        {
            var actual = HashState(stateProvider(frame));
            if (!string.Equals(actual, frame.StateHash, StringComparison.Ordinal))
                return new ReplayDivergence(frame.Tick, frame.StateHash, actual, recording with { Frames = recording.Frames.Where(item => item.Tick <= frame.Tick).ToArray() });
        }
        return null;
    }

    private static void Validate(Guid id, int schemaVersion, string provenance)
    {
        if (id == Guid.Empty) throw new InvalidOperationException("GAME_UUID_REQUIRED");
        if (schemaVersion != 1) throw new InvalidOperationException("GAME_SCHEMA_UNSUPPORTED");
        if (string.IsNullOrWhiteSpace(provenance)) throw new InvalidOperationException("GAME_PROVENANCE_REQUIRED");
    }
}
