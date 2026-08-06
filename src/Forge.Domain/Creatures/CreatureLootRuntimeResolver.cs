using Forge.Domain.Combat;

namespace Forge.Domain.Creatures;

public sealed record LootGrant(
    Guid PolicyId,
    string ItemSlug,
    int Quantity,
    string OwnerId,
    string InstanceKey,
    string Cause);

public sealed record CreatureCorpseSnapshot(
    bool Created,
    CreatureCorpseMode Mode,
    string? ContainerId,
    string? RagdollProfileId,
    DateTimeOffset? ExpiresAt,
    bool AllowResurrection,
    string OwnershipPolicy,
    CorpseCleanupMode CleanupMode,
    IReadOnlyList<LootGrant> Contents);

public sealed record DeathLootResolution(
    bool Succeeded,
    bool UsedLegacyFallback,
    IReadOnlyList<LootGrant> Grants,
    CreatureCorpseSnapshot Corpse,
    IReadOnlyList<CreatureLootDiagnostic> Diagnostics,
    string Audit);

public sealed record TheftResolution(
    bool Succeeded,
    bool Detected,
    LootGrant? Grant,
    string? CrimeId,
    DateTimeOffset? NextAttemptAt,
    IReadOnlyList<CreatureLootDiagnostic> Diagnostics,
    string Audit);

public static class CreatureLootRuntimeResolver
{
    public static DeathLootResolution ResolveDeath(
        CreatureLootProfile? profile,
        IReadOnlyList<LootDrop> combatLoot,
        double roll,
        string killerId,
        string partyId,
        DateTimeOffset at,
        bool required = false)
    {
        ArgumentNullException.ThrowIfNull(combatLoot);
        ValidateRollAndActors(roll, killerId, partyId);
        if (profile is null)
        {
            if (required)
            {
                var diagnostics = new[] { new CreatureLootDiagnostic("creature.loot.required_missing", "creatureLoot",
                    "The runtime requires a creature loot profile.") };
                return new(false, false, [], EmptyCorpse(), diagnostics, "loot:required-missing");
            }

            var legacy = combatLoot.Where(item => roll <= item.Chance).Select(item =>
                new LootGrant(Guid.Empty, item.ItemSlug, item.Quantity, killerId, "shared", "combat-legacy")).ToArray();
            return new(true, true, legacy, EmptyCorpse(), [], $"loot:legacy|grants:{legacy.Length}");
        }

        var items = combatLoot.Select(item => item.ItemSlug).ToHashSet(StringComparer.Ordinal);
        var diagnosticsForProfile = CreatureLootValidator.Validate(profile, availableItemSlugs: items);
        if (diagnosticsForProfile.Count > 0)
            return new(false, false, [], EmptyCorpse(), diagnosticsForProfile, "loot:invalid");

        var grants = profile.Entries.Where(item => item.DropOnDeath && roll <= item.Chance)
            .Select(item => Grant(item, Quantity(item, roll), killerId, partyId, "death"))
            .Take(profile.Corpse.MaximumItems > 0 ? profile.Corpse.MaximumItems : int.MaxValue)
            .ToArray();
        var corpse = CreateCorpse(profile.Corpse, grants, at);
        var direct = corpse.Created && profile.Corpse.Mode is CreatureCorpseMode.Container or CreatureCorpseMode.RagdollContainer
            ? [] : grants;
        return new(true, false, direct, corpse, [],
            $"loot:death|profile:{profile.Id:D}|grants:{grants.Length}|corpse:{corpse.Created}");
    }

    public static TheftResolution ResolvePickpocket(
        CreatureLootProfile profile,
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
        ArgumentNullException.ThrowIfNull(profile);
        ValidateRollAndActors(roll, thiefId, partyId);
        if (transactionId == Guid.Empty || !double.IsFinite(skillScore) || !double.IsFinite(perceptionScore))
            throw new ArgumentException("CREATURE_LOOT_THEFT_REQUEST_INVALID");
        var theft = profile.Theft;
        if (!theft.Enabled)
            return Failure("creature.loot.theft_disabled", "Pickpocket is disabled.", detected: false, null);
        if (theft.RequireServerConsent && !serverConsented)
            return Failure("creature.loot.theft_consent_required", "Server consent is required.", detected: false, null);
        var nextAttempt = previousAttemptAt?.AddSeconds(theft.CooldownSeconds);
        if (nextAttempt > at)
            return Failure("creature.loot.theft_cooldown", "Pickpocket is still on cooldown.", detected: false, nextAttempt);

        var detected = perceptionScore >= theft.PerceptionDifficulty;
        var eligible = profile.Entries.Where(item => item.Pickpocketable && roll <= item.Chance).ToArray();
        var success = skillScore >= theft.Difficulty && eligible.Length > 0;
        var grant = success ? Grant(eligible[0], Quantity(eligible[0], roll), thiefId, partyId, "pickpocket") : null;
        detected |= !success;
        var crime = detected ? theft.CrimeId : null;
        var diagnostics = success ? [] : new[] { new CreatureLootDiagnostic("creature.loot.theft_failed",
            "creatureLoot/theft", "The pickpocket check failed or no eligible item was rolled.") };
        return new(success, detected, grant, crime, at.AddSeconds(theft.CooldownSeconds), diagnostics,
            $"{theft.AuditTag}|tx:{transactionId:D}|success:{success}|detected:{detected}|crime:{crime ?? "none"}");

        TheftResolution Failure(string code, string message, bool detected, DateTimeOffset? retryAt) =>
            new(false, detected, null, null, retryAt, [new CreatureLootDiagnostic(code, "creatureLoot/theft", message)],
                $"{theft.AuditTag}|tx:{transactionId:D}|success:false|code:{code}");
    }

    private static LootGrant Grant(
        CreatureLootProfile.LootEntryPolicy policy,
        int quantity,
        string actorId,
        string partyId,
        string cause)
    {
        var owner = policy.Ownership switch
        {
            LootOwnershipMode.Killer => actorId,
            LootOwnershipMode.Party => partyId,
            LootOwnershipMode.FreeForAll => "*",
            LootOwnershipMode.FixedOwner => policy.FixedOwnerId!,
            LootOwnershipMode.ServerAssigned => "server",
            _ => throw new ArgumentOutOfRangeException(nameof(policy)),
        };
        var instance = policy.Instancing switch
        {
            LootInstancingMode.Shared => "shared",
            LootInstancingMode.PerPlayer => $"player:{actorId}",
            LootInstancingMode.PerParty => $"party:{partyId}",
            _ => throw new ArgumentOutOfRangeException(nameof(policy)),
        };
        return new(policy.Id, policy.ItemSlug, quantity, owner, instance, cause);
    }

    private static int Quantity(CreatureLootProfile.LootEntryPolicy item, double roll)
    {
        var span = item.MaximumQuantity - item.MinimumQuantity;
        return item.MinimumQuantity + (span == 0 ? 0 : Math.Min(span, (int)Math.Floor(roll * (span + 1))));
    }

    private static CreatureCorpseSnapshot CreateCorpse(
        CreatureLootProfile.CreatureCorpseProfile policy,
        IReadOnlyList<LootGrant> grants,
        DateTimeOffset at)
    {
        if (policy.Mode == CreatureCorpseMode.None) return EmptyCorpse();
        DateTimeOffset? expiry = policy.CleanupMode == CorpseCleanupMode.Persistent
            ? null
            : at.AddSeconds(policy.LifetimeSeconds);
        return new(true, policy.Mode, policy.ContainerId, policy.RagdollProfileId, expiry,
            policy.AllowResurrection, policy.OwnershipPolicy, policy.CleanupMode, grants);
    }

    private static CreatureCorpseSnapshot EmptyCorpse() =>
        new(false, CreatureCorpseMode.None, null, null, null, false, "none",
            CorpseCleanupMode.ImmediateWhenEmpty, []);

    private static void ValidateRollAndActors(double roll, string actorId, string partyId)
    {
        if (!double.IsFinite(roll) || roll is < 0 or > 1 ||
            string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(partyId))
            throw new ArgumentException("CREATURE_LOOT_RUNTIME_REQUEST_INVALID");
    }
}
