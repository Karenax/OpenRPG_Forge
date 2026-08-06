using Forge.Domain.Creatures;

namespace Forge.Application.Creatures;

public sealed record CreatureEventHandle(Guid Id, int SchemaVersion);

public sealed record CreatureEventScriptSnapshot(
    CreatureEventHandle Handle,
    string Provenance,
    bool RequiredAtRuntime,
    string? CombatBoundary,
    int BindingCount,
    bool HeartbeatEnabled,
    int HeartbeatIntervalMilliseconds,
    int HeartbeatBudgetMilliseconds,
    bool SuspendOutsideInterest,
    IReadOnlyList<string> CustomEventNames);

/// <summary>
/// Read-only ForgeScript surface. Canonical mutation remains owned by SetCreatureEventsHandler.
/// </summary>
public sealed class CreatureEventScriptService
{
    private readonly CreatureEventProfile _profile;
    private CreatureEventRuntimeState _runtimeState = CreatureEventRuntimeState.Empty;

    public CreatureEventScriptService(CreatureEventProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = CreatureEventValidator.Validate(profile);
        if (diagnostics.Count > 0)
            throw new ArgumentException(string.Join(';', diagnostics.Select(item => item.Code)), nameof(profile));
        _profile = profile;
    }

    public CreatureEventHandle Handle => new(_profile.Id, _profile.SchemaVersion);

    public CreatureEventScriptSnapshot Read(CreatureEventHandle handle)
    {
        EnsureHandle(handle);
        return new(handle, _profile.Provenance, _profile.RequiredAtRuntime,
            _profile.CombatBoundary?.Mode.ToString(), CreatureEventValidator.EnumerateBindings(_profile).Count,
            _profile.Heartbeat?.Enabled == true, _profile.Heartbeat?.IntervalMilliseconds ?? 0,
            _profile.Heartbeat?.BudgetMilliseconds ?? 0,
            _profile.Heartbeat?.SuspendOutsideInterest == true,
            _profile.CustomEvents.Select(item => item.QualifiedName).ToArray());
    }

    public CreatureEventAdvanceResult AdvanceHeartbeat(
        CreatureEventHandle handle, long deltaMilliseconds, bool insideInterest, string creatureId)
    {
        EnsureHandle(handle);
        var result = CreatureEventRuntimeResolver.AdvanceHeartbeat(_profile, _runtimeState, deltaMilliseconds,
            insideInterest, creatureId, _profile.RequiredAtRuntime);
        _runtimeState = result.State;
        return result;
    }

    public CreatureEventDispatch PublishCustom(
        CreatureEventHandle handle, Guid schemaId, IReadOnlyDictionary<string, string> payload)
    {
        EnsureHandle(handle);
        return CreatureEventRuntimeResolver.Custom(_profile, schemaId, payload);
    }

    private void EnsureHandle(CreatureEventHandle handle)
    {
        if (handle.Id != _profile.Id || handle.SchemaVersion != _profile.SchemaVersion)
            throw new KeyNotFoundException("CREATURE_EVENT_HANDLE_INVALID");
    }
}
