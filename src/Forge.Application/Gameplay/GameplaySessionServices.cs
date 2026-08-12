using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Gameplay;

namespace Forge.Application.Gameplay;

public sealed record SaveWriteContext(string ActorId, SaveAuthority Authority, bool CloudAvailable,
    bool PrivacyConsentGranted);

public sealed record RespawnRequest(string ActorId, string CheckpointWaypointId, int Currency,
    bool SafeSpawnAvailable, IReadOnlyList<string> PartyActorIds, bool LeaderApproved);

public sealed record RespawnResolution(bool Succeeded, string Code, string WaypointId, int RemainingCurrency,
    IReadOnlyList<string> RespawnActorIds, CorpsePolicy CorpsePolicy, double DelaySeconds,
    IReadOnlyList<string> Events);

public sealed record RuntimeShellSnapshot(string KitId, string SkinId, double Scale,
    IReadOnlyList<string> VisiblePanels, IReadOnlyList<string> InputActions, bool InputAgnostic);

public sealed record ReplayHeadlessReport(bool Succeeded, int FrameCount, ReplayDivergence? Divergence,
    string RecordingHash, string? MinimalPackageManifest);

public static class GameplaySessionServices
{
    public static SaveEnvelope WriteSave(PersistenceProfile profile, SaveWriteContext context,
        string ownerId, long revision, DateTimeOffset savedAt, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(context);
        if (GameplaySessionProfileValidator.Validate(GameplaySessionProfile.CreateDefault(
                Forge.Domain.Modules.ModuleId.From(profile.Id)) with
        {
            Persistence = profile,
        })
            .Any(item => item.Code == GameplaySessionProfileValidator.PersistenceCode))
            throw new InvalidOperationException("GAME_SAVE_PROFILE_INVALID");
        if (profile.Authority != context.Authority)
            throw new InvalidOperationException("GAME_SAVE_AUTHORITY_REQUIRED");
        if (profile.CloudAdapterEnabled && !context.CloudAvailable)
            throw new InvalidOperationException("GAME_SAVE_CLOUD_UNAVAILABLE");
        if (profile.ContainsPersonalData && !context.PrivacyConsentGranted)
            throw new InvalidOperationException("GAME_SAVE_PRIVACY_CONSENT_REQUIRED");
        if (string.IsNullOrWhiteSpace(ownerId) || revision < 0)
            throw new InvalidOperationException("GAME_SAVE_ENVELOPE_INVALID");
        return new(profile, ownerId.Trim(), revision, savedAt,
            new Dictionary<string, string>(values, StringComparer.Ordinal));
    }

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

    public static RespawnResolution Respawn(CheckpointProfile profile, RespawnRequest request,
        bool authorityGranted)
    {
        ValidateCheckpoint(profile);
        if (!profile.Enabled) return new(false, "GAME_RESPAWN_DISABLED", profile.WaypointId,
            request.Currency, [], profile.CorpsePolicy, 0, []);
        if (!authorityGranted) return new(false, "GAME_RESPAWN_AUTHORITY_REQUIRED", profile.WaypointId,
            request.Currency, [], profile.CorpsePolicy, 0, []);
        if (!string.Equals(request.CheckpointWaypointId, profile.WaypointId, StringComparison.Ordinal))
            return new(false, "GAME_RESPAWN_CHECKPOINT_MISMATCH", profile.WaypointId,
                request.Currency, [], profile.CorpsePolicy, 0, []);
        if (profile.RequireSafeSpawn && !request.SafeSpawnAvailable)
            return new(false, "GAME_RESPAWN_SAFE_SPAWN_REQUIRED", profile.WaypointId,
                request.Currency, [], profile.CorpsePolicy, 0, []);
        if (request.Currency < profile.CurrencyCost)
            return new(false, "GAME_RESPAWN_COST_UNAVAILABLE", profile.WaypointId,
                request.Currency, [], profile.CorpsePolicy, 0, []);
        if (profile.PartyPolicy == PartyRespawnPolicy.LeaderDecision && !request.LeaderApproved)
            return new(false, "GAME_RESPAWN_LEADER_APPROVAL_REQUIRED", profile.WaypointId,
                request.Currency, [], profile.CorpsePolicy, 0, []);
        var actors = profile.PartyPolicy == PartyRespawnPolicy.Individual
            ? new[] { request.ActorId }
            : request.PartyActorIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return new(true, "GAME_RESPAWN_READY", profile.WaypointId, request.Currency - profile.CurrencyCost,
            actors, profile.CorpsePolicy, profile.RespawnDelaySeconds,
            [profile.BeforeRespawnEvent, profile.AfterRespawnEvent]);
    }

    public static RuntimeShellProfile ValidateShell(RuntimeShellProfile profile)
    {
        Validate(profile.Id, profile.SchemaVersion, profile.Provenance);
        if (string.IsNullOrWhiteSpace(profile.SkinId) || profile.Scale is < 0.5 or > 3) throw new InvalidOperationException("GAME_SHELL_PROFILE_INVALID");
        return profile;
    }

    public static RuntimeShellSnapshot ResolveShell(RuntimeShellProfile profile)
    {
        ValidateShell(profile);
        var panels = new List<string>();
        if (profile.MapVisible) panels.Add("map");
        if (profile.JournalVisible) panels.Add("journal");
        if (profile.HudVisible) panels.Add("hud");
        return new(profile.KitId, profile.SkinId, profile.Scale, panels,
            profile.InputActions.Distinct(StringComparer.Ordinal).ToArray(), profile.InputAgnostic);
    }

    public static IReadOnlyList<ControlContext> PushControl(IReadOnlyList<ControlContext> stack, ControlContext context)
    {
        Validate(context.Id, context.SchemaVersion, context.Provenance);
        if (string.IsNullOrWhiteSpace(context.OwnerId)) throw new InvalidOperationException("GAME_CONTROL_OWNER_REQUIRED");
        return stack.Where(item => !string.Equals(item.OwnerId, context.OwnerId, StringComparison.Ordinal))
            .Append(context).OrderBy(item => item.Priority).ThenBy(item => item.OwnerId, StringComparer.Ordinal)
            .ToArray();
    }

    public static (IReadOnlyList<ControlContext> Stack, ControlContext? Restored) ReleaseOwner(IReadOnlyList<ControlContext> stack, string ownerId)
    {
        var released = stack.LastOrDefault(context => string.Equals(context.OwnerId, ownerId, StringComparison.Ordinal));
        var remaining = stack.Where(context => !string.Equals(context.OwnerId, ownerId, StringComparison.Ordinal))
            .ToArray();
        return (remaining, released?.RestoreOnRelease == false ? null : remaining.LastOrDefault());
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

    public static ReplayRecording Record(ReplayTestProfile profile, Guid recordingId,
        IEnumerable<ReplayFrame> frames, string provenance)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var ordered = frames.OrderBy(item => item.Tick).ToArray();
        if (!profile.Enabled || ordered.Length > profile.MaximumFrames ||
            ordered.Any(item => item.Tick < 0 || item.Seed < 0 || string.IsNullOrWhiteSpace(item.StateHash)) ||
            ordered.Select(item => item.Tick).Distinct().Count() != ordered.Length)
            throw new InvalidOperationException("GAME_REPLAY_RECORDING_INVALID");
        return new(recordingId, 1, provenance, ordered);
    }

    public static ReplayHeadlessReport ReplayHeadless(ReplayTestProfile profile, ReplayRecording recording,
        Func<ReplayFrame, string> stateProvider)
    {
        if (!profile.Enabled || !profile.HeadlessReplay)
            throw new InvalidOperationException("GAME_REPLAY_HEADLESS_DISABLED");
        var divergence = Replay(recording, stateProvider);
        var canonical = string.Join("\n", recording.Frames.OrderBy(item => item.Tick).Select(item =>
            $"{item.Tick}|{item.Input}|{item.Event}|{item.Seed}|{item.StateHash}"));
        var manifest = divergence is not null && profile.EmitMinimalPackage
            ? $"schema=1;recording={recording.Id:D};throughTick={divergence.Tick};frames=" +
              $"{divergence.MinimalReproduction.Frames.Count};privacy=" +
              $"{(profile.IncludePersonalData ? "included" : "redacted")};directory={profile.PackageDirectory}"
            : null;
        return new(divergence is null, recording.Frames.Count, divergence, HashState(canonical), manifest);
    }

    private static void Validate(Guid id, int schemaVersion, string provenance)
    {
        if (id == Guid.Empty) throw new InvalidOperationException("GAME_UUID_REQUIRED");
        if (schemaVersion != 1) throw new InvalidOperationException("GAME_SCHEMA_UNSUPPORTED");
        if (string.IsNullOrWhiteSpace(provenance)) throw new InvalidOperationException("GAME_PROVENANCE_REQUIRED");
    }
}
