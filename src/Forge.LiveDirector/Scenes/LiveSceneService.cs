using Forge.LiveDirector.Audit;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Scenes;

public sealed class LiveSceneService(AuthorizationService authorization, LiveAuditJournal audit)
{
    public sealed record Preset(Guid SceneId, string Name, IReadOnlyList<string> Spawns, string? Dialogue, string? Music, string? Weather, IReadOnlyList<string> Triggers, IReadOnlyList<string> RollbackPlan);
    public sealed record Result(bool Applied, string Code, long Revision, string Diff);
    private readonly Dictionary<Guid, Preset> _presets = [];
    private long _revision;

    public void Save(Preset preset)
    {
        if (preset.SceneId == Guid.Empty || string.IsNullOrWhiteSpace(preset.Name) || preset.RollbackPlan.Count == 0) throw new ArgumentException("LIVE_SCENE_INVALID", nameof(preset));
        _presets[preset.SceneId] = preset;
    }

    public Result Run(AuthorizationSubject actor, Guid sceneId, Guid commandId, string reason, long expectedRevision, bool apply)
    {
        if (!authorization.Authorize(actor, NetworkCapability.ManageWorld).Allowed) return new(false, "LIVE_SCENE_PERMISSION_DENIED", _revision, "no changes");
        if (!_presets.TryGetValue(sceneId, out var preset)) return new(false, "LIVE_SCENE_UNKNOWN", _revision, "no changes");
        if (expectedRevision != _revision) return new(false, "LIVE_SCENE_REVISION_STALE", _revision, "no changes");
        var diff = $"scene.run:{preset.Name}:spawns={preset.Spawns.Count}:dialogue={preset.Dialogue}:music={preset.Music}:weather={preset.Weather}:triggers={preset.Triggers.Count}:rollback={preset.RollbackPlan.Count}";
        if (!apply) return new(false, "LIVE_SCENE_PREVIEW", _revision, diff);
        _revision++;
        audit.Append(actor.AccountId, "scene.run", [sceneId], "prepared", "active", reason, commandId);
        return new(true, "LIVE_SCENE_APPLIED", _revision, diff);
    }
}
