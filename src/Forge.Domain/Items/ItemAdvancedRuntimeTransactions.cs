using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Forge.Domain.Items;

public sealed record ItemUniqueReservation(
    Guid Token,
    string RegistryKey,
    ItemUniqueScope Scope,
    string ScopeOwner,
    string ActorId,
    DateTimeOffset ReservedAt,
    DateTimeOffset? ExpiresAt,
    bool Committed);

public sealed record ItemUniqueRegistryState(long Revision, IReadOnlyList<ItemUniqueReservation> Reservations)
{
    public static ItemUniqueRegistryState Empty { get; } = new(0, []);
}

public sealed record ItemUniqueReservationResult(
    bool Accepted,
    string Code,
    ItemUniqueRegistryState State,
    ItemUniqueReservation? Reservation);

public static class ItemUniqueRegistryResolver
{
    public static ItemUniqueReservationResult Reserve(ItemAdvancedProfile profile, ItemUniqueRegistryState state,
        string actorId, string partyId, string seasonId, DateTimeOffset now, bool serverAuthorized)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(state);
        var unique = profile.Unique;
        if (!unique.Enabled)
            return new(true, "item.unique.not_required", state, null);
        if (profile.LootOwnership.RequireServerAuthority && !serverAuthorized)
            return new(false, "item.unique.server_authority_required", state, null);

        var active = state.Reservations.Where(item => item.Committed || item.ExpiresAt is null || item.ExpiresAt > now)
            .OrderBy(item => item.RegistryKey, StringComparer.Ordinal).ThenBy(item => item.Token).ToArray();
        var scopeOwner = unique.Scope switch
        {
            ItemUniqueScope.World => "world",
            ItemUniqueScope.Party => partyId,
            ItemUniqueScope.Player => actorId,
            ItemUniqueScope.Season => seasonId,
            _ => throw new ArgumentOutOfRangeException()
        };
        if (active.Any(item => string.Equals(item.RegistryKey, unique.RegistryKey, StringComparison.Ordinal) &&
                               item.Scope == unique.Scope &&
                               string.Equals(item.ScopeOwner, scopeOwner, StringComparison.Ordinal)))
            return new(false, "item.unique.already_reserved", new(state.Revision + 1, active), null);

        var token = StableToken(profile.Id, unique.RegistryKey, scopeOwner, actorId, state.Revision + 1);
        var reservation = new ItemUniqueReservation(token, unique.RegistryKey, unique.Scope, scopeOwner, actorId,
            now, unique.ReservationSeconds == 0 ? null : now.AddSeconds(unique.ReservationSeconds), false);
        return new(true, "item.unique.reserved", new(state.Revision + 1, [.. active, reservation]), reservation);
    }

    public static ItemUniqueRegistryState Commit(ItemUniqueRegistryState state, Guid token, bool serverAuthorized)
    {
        if (!serverAuthorized) throw new InvalidOperationException("item.unique.server_authority_required");
        if (state.Reservations.All(item => item.Token != token))
            throw new InvalidOperationException("item.unique.reservation_missing");
        return new(state.Revision + 1,
            state.Reservations.Select(item => item.Token == token ? item with { Committed = true, ExpiresAt = null } : item)
                .ToArray());
    }

    public static ItemUniqueRegistryState Release(ItemAdvancedProfile profile, ItemUniqueRegistryState state,
        Guid token, bool destroyed, bool serverAuthorized)
    {
        if (!serverAuthorized) throw new InvalidOperationException("item.unique.server_authority_required");
        if (destroyed && !profile.Unique.ReleaseOnDestroy) return state;
        return new(state.Revision + 1, state.Reservations.Where(item => item.Token != token).ToArray());
    }

    private static Guid StableToken(Guid profileId, string key, string owner, string actor, long revision)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{profileId:D}|{key}|{owner}|{actor}|{revision}"));
        return new Guid(bytes.AsSpan(0, 16));
    }
}

public sealed record ItemLootOwnershipContext(
    string ActorId,
    string OwnerActorId,
    string ActorPartyId,
    string OwnerPartyId,
    DateTimeOffset DroppedAt,
    DateTimeOffset Now,
    bool NeedGreedResolved,
    string? WinnerActorId,
    bool GameMasterAuthorized,
    bool ServerAuthorized);

public sealed record ItemLootOwnershipDecision(bool Allowed, string Code, DateTimeOffset? PublicAt);

public static class ItemLootOwnershipResolver
{
    public static ItemLootOwnershipDecision Resolve(ItemAdvancedProfile profile, ItemLootOwnershipContext context)
    {
        var policy = profile.LootOwnership;
        if (policy.RequireServerAuthority && !context.ServerAuthorized)
            return new(false, "item.loot.server_authority_required", null);
        var publicAt = policy.ExpirationSeconds == 0 ? context.DroppedAt :
            context.DroppedAt.AddSeconds(policy.ExpirationSeconds);
        if (policy.ExpirationSeconds == 0 || context.Now >= publicAt)
            return new(true, "item.loot.public", publicAt);

        var allowed = policy.Mode switch
        {
            ItemLootOwnershipMode.Public => true,
            ItemLootOwnershipMode.Party => string.Equals(context.ActorPartyId, context.OwnerPartyId, StringComparison.Ordinal),
            ItemLootOwnershipMode.Personal => string.Equals(context.ActorId, context.OwnerActorId, StringComparison.Ordinal),
            ItemLootOwnershipMode.NeedGreed => context.NeedGreedResolved &&
                                              string.Equals(context.ActorId, context.WinnerActorId, StringComparison.Ordinal),
            ItemLootOwnershipMode.GameMaster => context.GameMasterAuthorized,
            _ => false
        };
        if (!allowed && policy.TransferToPartyAfterExpiration && context.Now >= publicAt &&
            string.Equals(context.ActorPartyId, context.OwnerPartyId, StringComparison.Ordinal)) allowed = true;
        return new(allowed, allowed ? "item.loot.owner_allowed" : "item.loot.owner_denied", publicAt);
    }
}

public sealed record ItemCraftingContext(
    string StationTag,
    IReadOnlySet<string> Skills,
    int Quality,
    IReadOnlyDictionary<string, int> Inventory,
    bool ServerAuthorized);

public sealed record ItemCraftingDecision(
    bool Allowed,
    string Code,
    IReadOnlyDictionary<string, int> Consumed,
    IReadOnlyList<string> Byproducts,
    double DurationSeconds,
    string Provenance);

public static class ItemCraftingResolver
{
    public static ItemCraftingDecision Resolve(ItemAdvancedProfile profile, Guid recipeId,
        ItemCraftingContext context)
    {
        var recipe = profile.Recipes.SingleOrDefault(item => item.Id == recipeId)
            ?? throw new InvalidOperationException("item.recipe.missing");
        if (!context.ServerAuthorized)
            return Denied("item.recipe.server_authority_required", recipe);
        if (!string.Equals(recipe.StationTag, context.StationTag, StringComparison.Ordinal))
            return Denied("item.recipe.station_missing", recipe);
        if (!context.Skills.Contains(recipe.SkillId))
            return Denied("item.recipe.skill_missing", recipe);
        if (context.Quality < recipe.MinimumQuality)
            return Denied("item.recipe.quality_low", recipe);
        foreach (var ingredient in recipe.Ingredients)
            if (context.Inventory.GetValueOrDefault(ingredient.Query) < ingredient.Quantity)
                return Denied("item.recipe.ingredients_missing", recipe);
        var consumed = recipe.Ingredients.Where(item => item.Consume)
            .ToDictionary(item => item.Query, item => item.Quantity, StringComparer.Ordinal);
        return new(true, "item.recipe.ready", consumed, recipe.ByproductSlugs, recipe.DurationSeconds,
            recipe.Provenance);
    }

    private static ItemCraftingDecision Denied(string code, ItemRecipeDefinition recipe) =>
        new(false, code, new Dictionary<string, int>(), [], recipe.DurationSeconds, recipe.Provenance);
}

public sealed record ItemAmmoDecision(bool Fired, int Remaining, bool Recovered, string ProjectileAssetId,
    string? ElementalPayload);

public static class ItemAmmoResolver
{
    public static ItemAmmoDecision Fire(ItemAdvancedProfile profile, int currentStack, int seed, Guid shotId)
    {
        var ammo = profile.Ammo ?? throw new InvalidOperationException("item.ammo.missing");
        if (currentStack <= 0) return new(false, 0, false, ammo.ProjectileAssetId, ammo.ElementalPayload);
        var remaining = ammo.ConsumeOnUse ? currentStack - 1 : currentStack;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{profile.Id:D}|{shotId:D}|{seed}|ammo"));
        var roll = BinaryPrimitives.ReadUInt64BigEndian(hash) / (double)ulong.MaxValue;
        return new(true, remaining, ammo.ConsumeOnUse && roll <= ammo.RecoveryChance,
            ammo.ProjectileAssetId, ammo.ElementalPayload);
    }
}
