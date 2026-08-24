using Forge.Domain.Factions;
using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Domain.Placeables;

namespace Forge.RpgKit.Application.Placeables;

public sealed record PlaceableFactionHandle(Guid PlaceableId, Guid OwnershipId);

public sealed record PlaceableFactionResolution(
    bool Available,
    bool OptionalFallback,
    PlaceableFactionHandle? Handle,
    string? DiagnosticCode);

public sealed record PlaceableFactionRuntime(
    Guid PrimaryFactionId,
    IReadOnlySet<Guid> MembershipFactionIds,
    bool InheritHostility,
    bool UnauthorizedActionsAreCrime,
    bool MembersOnlyAccess,
    bool BlockFriendlyTargeting);

public sealed class PlaceableFactionRuntimeAdapter
{
    public PlaceableFactionResolution Resolve(PlaceableWorldProfile placeable, FactionCatalog factions)
    {
        ArgumentNullException.ThrowIfNull(placeable);
        ArgumentNullException.ThrowIfNull(factions);
        if (placeable.FactionOwnership is null)
        {
            return placeable.FactionOwnershipRequiredAtRuntime
                ? new(false, false, null, "WORLD_PLACEABLE_FACTION_OWNERSHIP_REQUIRED_MISSING")
                : new(false, true, null, null);
        }

        var diagnostic = PlaceableFactionOwnershipProfileValidator.Validate(
                placeable.FactionOwnership,
                factions.Factions.Select(item => item.Id).ToHashSet(),
                $"module/worldFeatures/placeables/{placeable.Id}/factionOwnership")
            .FirstOrDefault();
        return diagnostic is null
            ? new(true, false, new(placeable.Id, placeable.FactionOwnership.Id), null)
            : new(false, false, null, diagnostic.Code.ToUpperInvariant().Replace('.', '_'));
    }

    public PlaceableFactionRuntime Build(
        PlaceableFactionHandle handle,
        PlaceableWorldProfile placeable,
        FactionCatalog factions)
    {
        var ownership = Require(handle, placeable);
        var diagnostic = PlaceableFactionOwnershipProfileValidator.Validate(
                ownership,
                factions.Factions.Select(item => item.Id).ToHashSet(),
                $"module/worldFeatures/placeables/{placeable.Id}/factionOwnership")
            .FirstOrDefault();
        if (diagnostic is not null)
        {
            throw new InvalidOperationException(diagnostic.Code.ToUpperInvariant().Replace('.', '_'));
        }
        return new(
            ownership.PrimaryFactionId,
            ownership.MembershipFactionIds.ToHashSet(),
            ownership.InheritHostility,
            ownership.UnauthorizedActionsAreCrime,
            ownership.MembersOnlyAccess,
            ownership.BlockFriendlyTargeting);
    }

    public PlaceableFactionDecision Evaluate(
        PlaceableFactionHandle handle,
        PlaceableWorldProfile placeable,
        FactionCatalog factions,
        PlaceableFactionActor actor,
        string action)
    {
        Require(handle, placeable);
        return PlaceableFactionService.Evaluate(placeable.FactionOwnership!, factions, actor, action);
    }

    private static PlaceableFactionOwnershipProfile Require(
        PlaceableFactionHandle handle,
        PlaceableWorldProfile placeable)
    {
        if (handle.PlaceableId != placeable.Id || placeable.FactionOwnership is null ||
            handle.OwnershipId != placeable.FactionOwnership.Id)
        {
            throw new InvalidOperationException("WORLD_PLACEABLE_FACTION_OWNERSHIP_HANDLE_STALE");
        }
        return placeable.FactionOwnership;
    }
}
