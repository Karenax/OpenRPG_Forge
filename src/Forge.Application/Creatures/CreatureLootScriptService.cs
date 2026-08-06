using Forge.Domain.Combat;
using Forge.Domain.Creatures;

namespace Forge.Application.Creatures;

public sealed record CreatureLootHandle(Guid Id, int SchemaVersion);

public sealed record CreatureLootScriptSnapshot(
    CreatureLootHandle Handle,
    bool RequiredAtRuntime,
    IReadOnlyList<CreatureLootProfile.LootEntryPolicy> Entries,
    CreatureLootProfile.TheftProfile Theft,
    CreatureLootProfile.CreatureCorpseProfile Corpse);

/// <summary>
/// Read-only, type-safe projection for ForgeScript adapters. Persistent changes remain governed by
/// SetCreatureLootHandler; scripts receive stable handles and resolved outcomes, never project files.
/// </summary>
public sealed class CreatureLootScriptService
{
    private readonly CreatureLootProfile _profile;
    private readonly IReadOnlyList<LootDrop> _combatLoot;

    public CreatureLootScriptService(
        CreatureLootProfile profile,
        IReadOnlyList<LootDrop> combatLoot,
        IReadOnlySet<string> availableSkillIds)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(combatLoot);
        ArgumentNullException.ThrowIfNull(availableSkillIds);
        var itemIds = combatLoot.Select(item => item.ItemSlug).ToHashSet(StringComparer.Ordinal);
        var diagnostics = CreatureLootValidator.Validate(profile,
            availableItemSlugs: itemIds, availableSkillIds: availableSkillIds);
        if (diagnostics.Count > 0)
            throw new ArgumentException(string.Join("; ", diagnostics.Select(item => item.Code)), nameof(profile));
        _profile = profile;
        _combatLoot = combatLoot.ToArray();
    }

    public CreatureLootHandle Handle => new(_profile.Id, _profile.SchemaVersion);

    public CreatureLootScriptSnapshot Resolve(CreatureLootHandle handle)
    {
        EnsureHandle(handle);
        return new(handle, _profile.RequiredAtRuntime, _profile.Entries, _profile.Theft, _profile.Corpse);
    }

    public DeathLootResolution ResolveDeath(
        CreatureLootHandle handle,
        double roll,
        string killerId,
        string partyId,
        DateTimeOffset at)
    {
        EnsureHandle(handle);
        return CreatureLootRuntimeResolver.ResolveDeath(
            _profile, _combatLoot, roll, killerId, partyId, at, _profile.RequiredAtRuntime);
    }

    public TheftResolution ResolvePickpocket(
        CreatureLootHandle handle,
        Guid transactionId,
        double roll,
        string thiefId,
        string partyId,
        double skillScore,
        double perceptionScore,
        bool serverConsented,
        DateTimeOffset at,
        DateTimeOffset? previousAttemptAt)
    {
        EnsureHandle(handle);
        return CreatureLootRuntimeResolver.ResolvePickpocket(_profile, transactionId, roll, thiefId, partyId,
            skillScore, perceptionScore, serverConsented, at, previousAttemptAt);
    }

    private void EnsureHandle(CreatureLootHandle handle)
    {
        if (handle != Handle) throw new KeyNotFoundException("CREATURE_LOOT_HANDLE_INVALID");
    }
}
