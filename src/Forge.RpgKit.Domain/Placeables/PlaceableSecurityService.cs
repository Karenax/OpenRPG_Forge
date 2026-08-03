using Forge.RpgKit.Domain.Items;

namespace Forge.RpgKit.Domain.Placeables;

public sealed record PlaceableProtectionPolicy(IReadOnlySet<string> ProtectedActions, IReadOnlySet<string> RequiredPermissions, string Feedback, bool AllowGameMasterOverride);
public sealed record ProtectionDecision(bool Allowed, string Code, string Feedback, string AuditEntry);
public enum LockState { Unlocked, Locked, Jammed, Broken }
public sealed record LockComponent(LockState State, IReadOnlySet<string> Credentials, string SkillId, double Difficulty, string? CodeHash, bool Hackable, bool Relockable, bool CanJam, IReadOnlySet<string> Permissions);
public sealed record LockAttempt(string ActorId, IReadOnlySet<string> Credentials, IReadOnlySet<string> Permissions, string? CodeHash, double SkillResult, bool Hacking, bool GameMasterOverride);
public sealed record LockResult(LockComponent Lock, bool Succeeded, string Method, string EventName);
public sealed record TrapComponent(Guid Id, string TriggerId, string PayloadId, double DetectionDifficulty, double DisarmDifficulty, TimeSpan ResetDelay, string OwnerId, string VisualEffectId, string FriendlyPolicy, bool Armed, bool OneShot);
public sealed record TrapResult(TrapComponent Trap, bool Succeeded, string EventName, string Detail);
public enum LootSpawnTiming { Initialize, Open, Reset }
public sealed record LootSpawner(Guid LootTableId, LootSpawnTiming Timing, int Seed, bool PersistGeneratedState, IReadOnlyList<LootResult> SavedResults, int Generation);
public sealed record LootSpawnResult(LootSpawner Spawner, IReadOnlyList<LootResult> Results, bool ReusedSavedState, string EventName);
public sealed record ContainerStateEvent(Guid EventId, int SchemaVersion, string Provenance, string Name, Guid PlaceableId, string ActorId, string Result, string PreviousState, string CurrentState, string Authority, DateTimeOffset OccurredAt);

public static class PlaceableSecurityService
{
    public static ProtectionDecision Authorize(PlaceableProtectionPolicy policy, string action, IReadOnlySet<string> permissions, bool gameMasterOverride, string actorId, string reason)
    {
        if (!policy.ProtectedActions.Contains(action)) return new(true, "RPG_PLACEABLE_ACTION_ALLOWED", "", $"allowed:{action}:{actorId}");
        if (policy.RequiredPermissions.All(permissions.Contains)) return new(true, "RPG_PLACEABLE_PERMISSION_ALLOWED", "", $"permission:{action}:{actorId}");
        if (gameMasterOverride && policy.AllowGameMasterOverride) return new(true, "RPG_PLACEABLE_GM_OVERRIDE", policy.Feedback, $"gm-override:{action}:{actorId}:{reason}");
        return new(false, "RPG_PLACEABLE_PROTECTED", policy.Feedback, $"blocked:{action}:{actorId}");
    }

    public static LockResult AttemptUnlock(LockComponent component, LockAttempt attempt)
    {
        if (component.State == LockState.Unlocked) return new(component, true, "already-unlocked", "LockUnchanged");
        if (component.State == LockState.Broken) return new(component, false, "broken", "LockAttemptFailed");
        var authorized = component.Permissions.All(attempt.Permissions.Contains);
        var credential = component.Credentials.Count > 0 && component.Credentials.Overlaps(attempt.Credentials);
        var code = component.CodeHash is not null && component.CodeHash == attempt.CodeHash;
        var skill = !string.IsNullOrWhiteSpace(component.SkillId) && attempt.SkillResult >= component.Difficulty;
        var hack = attempt.Hacking && component.Hackable && attempt.SkillResult >= component.Difficulty;
        var succeeded = attempt.GameMasterOverride || authorized || credential || code || skill || hack;
        var method = attempt.GameMasterOverride ? "gm" : credential ? "credential" : code ? "code" : hack ? "hacking" : skill ? "skill" : authorized ? "permission" : "failed";
        return new(succeeded ? component with { State = LockState.Unlocked } : component, succeeded, method, succeeded ? "LockOpened" : "LockAttemptFailed");
    }

    public static LockResult Relock(LockComponent component, bool jam, string actorId)
    {
        if (!component.Relockable || component.State != LockState.Unlocked) return new(component, false, "unavailable", "LockAttemptFailed");
        if (jam && !component.CanJam) return new(component, false, "jam-disabled", "LockAttemptFailed");
        return new(component with { State = jam ? LockState.Jammed : LockState.Locked }, true, jam ? $"jam:{actorId}" : $"relock:{actorId}", jam ? "LockJammed" : "LockClosed");
    }

    public static TrapResult Detect(TrapComponent trap, double perception, string actorFaction)
    {
        if (!trap.Armed) return new(trap, true, "TrapInactive", "not-armed");
        if (trap.FriendlyPolicy == actorFaction) return new(trap, true, "TrapFriendlyIgnored", "friendly");
        return new(trap, perception >= trap.DetectionDifficulty, perception >= trap.DetectionDifficulty ? "TrapDetected" : "TrapHidden", $"difficulty:{trap.DetectionDifficulty}");
    }

    public static TrapResult Disarm(TrapComponent trap, double skill)
    {
        if (!trap.Armed) return new(trap, true, "TrapAlreadyDisarmed", "inactive");
        var succeeded = skill >= trap.DisarmDifficulty; return new(succeeded ? trap with { Armed = false } : trap, succeeded, succeeded ? "TrapDisarmed" : "TrapDisarmFailed", $"payload:{trap.PayloadId}");
    }

    public static TrapResult Trigger(TrapComponent trap, string actorFaction)
    {
        if (!trap.Armed || trap.FriendlyPolicy == actorFaction) return new(trap, false, "TrapIgnored", "inactive-or-friendly");
        return new(trap.OneShot ? trap with { Armed = false } : trap, true, "TrapTriggered", $"{trap.TriggerId}:{trap.PayloadId}:{trap.VisualEffectId}");
    }

    public static TrapComponent Reset(TrapComponent trap) => trap with { Armed = true };

    public static LootSpawnResult Spawn(LootSpawner spawner, LootTable table, LootSpawnTiming timing, IReadOnlySet<string> conditions)
    {
        if (spawner.Timing != timing) return new(spawner, [], false, "LootSpawnSkipped");
        if (spawner.PersistGeneratedState && spawner.SavedResults.Count > 0) return new(spawner, spawner.SavedResults, true, "LootSpawnRestored");
        var results = ItemGameplayService.Roll(table, spawner.Seed + spawner.Generation, conditions); var updated = spawner with { SavedResults = spawner.PersistGeneratedState ? results : [], Generation = spawner.Generation + 1 };
        return new(updated, results, false, "LootSpawned");
    }

    public static ContainerStateEvent ContainerEvent(bool opened, Guid placeableId, string actorId, string result, string previousState, string authority, string provenance, DateTimeOffset at)
        => new(Guid.NewGuid(), 1, provenance, opened ? "ContainerOpened" : "ContainerClosed", placeableId, actorId, result, previousState, opened ? "open" : "closed", authority, at);
}
