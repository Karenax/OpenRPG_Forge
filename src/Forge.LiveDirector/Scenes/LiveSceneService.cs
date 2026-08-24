using Forge.LiveDirector.Audit;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Scenes;

public sealed class LiveSceneService(AuthorizationService authorization, LiveAuditJournal audit)
{
    public sealed record Preset(Guid SceneId, string Name, IReadOnlyList<string> Spawns,
        string? Dialogue, string? Music, string? Weather, IReadOnlyList<string> Triggers,
        IReadOnlyList<string> RollbackPlan);

    public sealed record Result(bool Applied, string Code, long Revision, string Diff,
        int AdaptedFacets, bool Restored, string AuditEntry);

    private readonly Dictionary<Guid, Preset> _presets = [];
    private readonly Stack<Preset> _active = [];
    private readonly HashSet<Guid> _processed = [];
    private long _revision;

    public int ActiveSceneCount => _active.Count;

    public void Save(Preset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        if (preset.SceneId == Guid.Empty || string.IsNullOrWhiteSpace(preset.Name) ||
            preset.RollbackPlan is null || preset.RollbackPlan.Count == 0 ||
            preset.Spawns is null || preset.Triggers is null)
            throw new ArgumentException("LIVE_SCENE_INVALID", nameof(preset));
        _presets[preset.SceneId] = preset;
    }

    public Result Run(AuthorizationSubject actor, Guid sceneId, Guid commandId, string reason,
        long expectedRevision, bool apply)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!authorization.Authorize(actor, NetworkCapability.ManageWorld).Allowed)
            return Reject("LIVE_SCENE_PERMISSION_DENIED", commandId);
        if (!_presets.TryGetValue(sceneId, out var preset))
            return Reject("LIVE_SCENE_UNKNOWN", commandId);
        if (commandId == Guid.Empty || string.IsNullOrWhiteSpace(reason))
            return Reject("LIVE_SCENE_COMMAND_INVALID", commandId);
        if (expectedRevision != _revision)
            return Reject("LIVE_SCENE_REVISION_STALE", commandId);
        if (_processed.Contains(commandId))
            return Reject("LIVE_SCENE_COMMAND_REPLAYED", commandId);
        var facets = CountFacets(preset);
        var diff = Describe(preset, "scene.run");
        if (!apply)
            return new(false, "LIVE_SCENE_PREVIEW", _revision, diff, facets, false,
                $"previewed:scene={sceneId}:actor={actor.AccountId}");
        _active.Push(preset);
        _processed.Add(commandId);
        _revision++;
        audit.Append(actor.AccountId, "scene.run", [sceneId], "prepared", "active", reason,
            commandId);
        return new(true, "LIVE_SCENE_APPLIED", _revision, diff, facets, false,
            $"applied:scene={sceneId}:actor={actor.AccountId}:revision={_revision}");
    }

    public Result Rollback(AuthorizationSubject actor, Guid commandId, string reason,
        long expectedRevision, bool apply)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!authorization.Authorize(actor, NetworkCapability.ManageWorld).Allowed)
            return Reject("LIVE_SCENE_PERMISSION_DENIED", commandId);
        if (_active.Count == 0) return Reject("LIVE_SCENE_ROLLBACK_EMPTY", commandId);
        if (commandId == Guid.Empty || string.IsNullOrWhiteSpace(reason))
            return Reject("LIVE_SCENE_COMMAND_INVALID", commandId);
        if (expectedRevision != _revision)
            return Reject("LIVE_SCENE_REVISION_STALE", commandId);
        if (_processed.Contains(commandId))
            return Reject("LIVE_SCENE_COMMAND_REPLAYED", commandId);
        var preset = _active.Peek();
        var diff = Describe(preset, "scene.rollback");
        if (!apply)
            return new(false, "LIVE_SCENE_ROLLBACK_PREVIEW", _revision, diff,
                CountFacets(preset), false,
                $"previewed:scene.rollback={preset.SceneId}:actor={actor.AccountId}");
        _active.Pop();
        _processed.Add(commandId);
        _revision++;
        audit.Append(actor.AccountId, "scene.rollback", [preset.SceneId], "active", "prepared",
            reason, commandId);
        return new(true, "LIVE_SCENE_ROLLED_BACK", _revision, diff, CountFacets(preset), true,
            $"compensated:scene={preset.SceneId}:actor={actor.AccountId}:revision={_revision}");
    }

    private Result Reject(string code, Guid commandId) => new(false, code, _revision,
        "no changes", 0, false, $"rejected:{code}:command={commandId}");

    private static int CountFacets(Preset preset) => new[]
    {
        preset.Spawns.Count > 0,
        !string.IsNullOrWhiteSpace(preset.Dialogue),
        !string.IsNullOrWhiteSpace(preset.Music),
        !string.IsNullOrWhiteSpace(preset.Weather),
        preset.Triggers.Count > 0
    }.Count(value => value);

    private static string Describe(Preset preset, string operation) =>
        $"{operation}:{preset.Name}:spawns={preset.Spawns.Count}:dialogue={preset.Dialogue}:" +
        $"music={preset.Music}:weather={preset.Weather}:triggers={preset.Triggers.Count}:" +
        $"rollback={preset.RollbackPlan.Count}:adapters={CountFacets(preset)}";
}
