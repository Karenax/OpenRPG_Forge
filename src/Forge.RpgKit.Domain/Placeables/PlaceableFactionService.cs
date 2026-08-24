using Forge.Domain.Factions;
using Forge.Domain.WorldFeatures;

namespace Forge.RpgKit.Domain.Placeables;

public sealed record PlaceableFactionActor(
    Guid? PrimaryFactionId,
    IReadOnlySet<Guid> FactionIds);

public sealed record PlaceableFactionDecision(
    bool IsMember,
    bool AccessAllowed,
    bool Hostile,
    bool Crime,
    bool TargetAllowed,
    double RelationshipScore,
    string Disposition,
    string Code);

public static class PlaceableFactionService
{
    private static readonly IReadOnlySet<string> CrimeActions = new HashSet<string>(
        ["destroy", "damage", "move", "use", "open", "lock", "unlock", "steal"],
        StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> TargetingActions = new HashSet<string>(
        ["destroy", "damage", "attack", "target"],
        StringComparer.Ordinal);

    public static PlaceableFactionDecision Evaluate(
        PlaceableFactionOwnershipProfile ownership,
        FactionCatalog factions,
        PlaceableFactionActor actor,
        string action)
    {
        ArgumentNullException.ThrowIfNull(ownership);
        ArgumentNullException.ThrowIfNull(factions);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        var factionIds = factions.Factions.Select(item => item.Id).ToHashSet();
        var diagnostics = PlaceableFactionOwnershipProfileValidator.Validate(
            ownership,
            factionIds,
            "placeable/factionOwnership");
        if (diagnostics.Count > 0)
        {
            throw new InvalidOperationException(diagnostics[0].Code.ToUpperInvariant().Replace('.', '_'));
        }

        var normalizedAction = action.Trim().ToLowerInvariant();
        var isMember = actor.FactionIds.Overlaps(ownership.MembershipFactionIds);
        var relationshipScore = ResolveRelationshipScore(ownership, factions, actor, isMember);
        var disposition = isMember
            ? "member"
            : actor.PrimaryFactionId is { } actorFactionId && factionIds.Contains(actorFactionId)
                ? FactionValidator.ResolveBand(factions, actorFactionId, ownership.PrimaryFactionId,
                    relationshipScore)
                : "unaffiliated";
        var hostile = ownership.InheritHostility && !isMember && relationshipScore < 0;
        var accessAllowed = !ownership.MembersOnlyAccess || isMember;
        var crime = ownership.UnauthorizedActionsAreCrime && !isMember && CrimeActions.Contains(normalizedAction);
        var friendly = isMember || relationshipScore > 0;
        var targetAllowed = !ownership.BlockFriendlyTargeting ||
                            !TargetingActions.Contains(normalizedAction) ||
                            !friendly;
        var code = !accessAllowed
            ? "RPG_PLACEABLE_FACTION_ACCESS_DENIED"
            : !targetAllowed
                ? "RPG_PLACEABLE_FACTION_FRIENDLY_TARGET_BLOCKED"
                : hostile
                    ? "RPG_PLACEABLE_FACTION_HOSTILE"
                    : "RPG_PLACEABLE_FACTION_ALLOWED";
        return new(
            isMember,
            accessAllowed,
            hostile,
            crime,
            targetAllowed,
            relationshipScore,
            disposition,
            code);
    }

    private static double ResolveRelationshipScore(
        PlaceableFactionOwnershipProfile ownership,
        FactionCatalog factions,
        PlaceableFactionActor actor,
        bool isMember)
    {
        if (isMember)
        {
            return 100;
        }
        if (actor.PrimaryFactionId is not { } actorFactionId || actorFactionId == Guid.Empty)
        {
            return 0;
        }
        if (actorFactionId == ownership.PrimaryFactionId)
        {
            return 100;
        }
        return FactionRuntimeResolver.EffectiveRelationship(
            factions,
            actorFactionId,
            ownership.PrimaryFactionId)?.Score ?? 0;
    }
}
