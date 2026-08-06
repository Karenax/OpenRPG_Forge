using Forge.Domain.Creatures;

namespace Forge.Application.Creatures;

public sealed record CreatureRoleBehaviorHandle(Guid Id, int SchemaVersion);

public sealed record CreatureRoleBehaviorScriptSnapshot(
    CreatureRoleBehaviorHandle Handle,
    IReadOnlyDictionary<string, string> LoreFields,
    IReadOnlyList<string> RoleTags,
    string? ActiveIdleSetId,
    string? ActiveAnimationId,
    string? WaypointId,
    string? ConversationId,
    bool AutoLevelLocked,
    int AutoLevelChangeCount);

/// <summary>
/// Read-only type-safe projection for ForgeScript adapters. Persistent mutations remain governed by
/// SetCreatureRoleBehaviorHandler and never expose project files or engine nodes.
/// </summary>
public sealed class CreatureRoleBehaviorScriptService
{
    private readonly CreatureRoleBehaviorProfile _profile;
    private readonly CreatureProgressionProfile? _progression;

    public CreatureRoleBehaviorScriptService(
        CreatureRoleBehaviorProfile profile,
        CreatureProgressionProfile? progression)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var validation = CreatureRoleBehaviorValidator.Validate(profile, progression);
        if (validation.Count > 0)
            throw new ArgumentException(string.Join("; ", validation.Select(item => item.Code)), nameof(profile));
        _profile = profile;
        _progression = progression;
    }

    public CreatureRoleBehaviorHandle Handle => new(_profile.Id, _profile.SchemaVersion);

    public CreatureRoleBehaviorScriptSnapshot Resolve(
        CreatureRoleBehaviorHandle handle,
        int minuteOfDay,
        bool interrupted)
    {
        if (handle != Handle) throw new KeyNotFoundException("CREATURE_ROLE_BEHAVIOR_HANDLE_INVALID");
        var snapshot = CreatureRoleBehaviorRuntimeResolver.Resolve(_profile, _progression, minuteOfDay, interrupted,
            required: true);
        if (!snapshot.Succeeded) throw new InvalidOperationException("CREATURE_ROLE_BEHAVIOR_RESOLVE_INVALID");
        return new(handle, snapshot.LoreFields, snapshot.RoleTags, snapshot.ActiveIdleSetId,
            snapshot.ActiveAnimationId, snapshot.WaypointId, snapshot.ConversationId,
            snapshot.AutoLevel?.ManualLocked ?? true, snapshot.AutoLevel?.Changes.Count ?? 0);
    }
}
