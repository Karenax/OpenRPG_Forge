using Forge.Domain.Creatures;

namespace Forge.Application.Creatures;

public sealed record CreatureProgressionHandle(Guid Id, int SchemaVersion);

public sealed record CreatureProgressionScriptSnapshot(
    CreatureProgressionHandle Handle,
    string RulesetId,
    int TotalLevel,
    IReadOnlyDictionary<string, int> TrackLevels,
    IReadOnlyDictionary<string, double> DispositionValues,
    IReadOnlyDictionary<string, int> SkillRanks,
    IReadOnlyDictionary<string, int> TalentRanks,
    IReadOnlyList<string> PreparedAbilityIds);

/// <summary>
/// Read-only, type-safe runtime projection for ForgeScript adapters. Persistent mutations remain governed by
/// SetCreatureProgressionHandler and never expose project files or engine nodes.
/// </summary>
public sealed class CreatureProgressionScriptService
{
    private readonly CreatureProgressionProfile _profile;
    private readonly CreatureProgressionRuntimeSnapshot _runtime;

    public CreatureProgressionScriptService(
        CreatureProgressionProfile profile,
        IReadOnlySet<string> availableAbilityIds)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _profile = profile;
        _runtime = CreatureProgressionRuntimeResolver.Resolve(profile, availableAbilityIds, required: true);
        if (!_runtime.Succeeded)
        {
            throw new ArgumentException(string.Join("; ", _runtime.Diagnostics.Select(item => item.Code)), nameof(profile));
        }
    }

    public CreatureProgressionHandle Handle => new(_profile.Id, _profile.SchemaVersion);

    public CreatureProgressionScriptSnapshot Resolve(CreatureProgressionHandle handle)
    {
        if (handle != Handle) throw new KeyNotFoundException("CREATURE_PROGRESSION_HANDLE_INVALID");
        return new CreatureProgressionScriptSnapshot(
            handle,
            _profile.RulesetId,
            _runtime.TotalLevel,
            _profile.Tracks.ToDictionary(item => item.TrackId, item => item.Level, StringComparer.Ordinal),
            _profile.DispositionAxes.ToDictionary(item => item.AxisId, item => item.Value, StringComparer.Ordinal),
            _runtime.SkillRanks,
            _profile.Talents.ToDictionary(item => item.TalentId, item => item.Rank, StringComparer.Ordinal),
            _runtime.PreparedAbilityIds);
    }

    public double ResolveSkillCheck(
        CreatureProgressionHandle handle,
        string skillId,
        double attributeModifier,
        double situationalModifier)
    {
        _ = Resolve(handle);
        return CreatureProgressionRuntimeResolver.ResolveSkillCheck(
            _runtime, skillId, attributeModifier, situationalModifier);
    }
}
