using Forge.Domain.Validation;

namespace Forge.Domain.WorldFeatures;

public sealed record PlaceableFactionOwnershipProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    Guid PrimaryFactionId,
    IReadOnlyList<Guid> MembershipFactionIds,
    bool InheritHostility,
    bool UnauthorizedActionsAreCrime,
    bool MembersOnlyAccess,
    bool BlockFriendlyTargeting)
{
    public const int CurrentSchemaVersion = 1;
}

public static class PlaceableFactionOwnershipProfileValidator
{
    public static IReadOnlyList<DomainDiagnostic> Validate(
        PlaceableFactionOwnershipProfile profile,
        IReadOnlySet<Guid> availableFactionIds,
        string root)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(availableFactionIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        var diagnostics = new List<DomainDiagnostic>();
        if (profile.Id == Guid.Empty ||
            profile.SchemaVersion != PlaceableFactionOwnershipProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(profile.Provenance))
        {
            diagnostics.Add(new(
                "world.placeable.faction_ownership_identity_invalid",
                "Faction ownership requires a stable identity, supported schema and provenance.",
                root));
        }

        if (profile.PrimaryFactionId == Guid.Empty ||
            profile.MembershipFactionIds.Count == 0 ||
            profile.MembershipFactionIds.Any(id => id == Guid.Empty) ||
            profile.MembershipFactionIds.Distinct().Count() != profile.MembershipFactionIds.Count ||
            !profile.MembershipFactionIds.Contains(profile.PrimaryFactionId))
        {
            diagnostics.Add(new(
                "world.placeable.faction_memberships_invalid",
                "Faction ownership requires a primary faction included in unique memberships.",
                $"{root}/memberships"));
        }

        foreach (var factionId in profile.MembershipFactionIds
                     .Append(profile.PrimaryFactionId)
                     .Distinct()
                     .Where(id => id != Guid.Empty && !availableFactionIds.Contains(id)))
        {
            diagnostics.Add(new(
                "world.placeable.faction_reference_missing",
                "Placeable faction membership references an unknown faction.",
                $"{root}/memberships/{factionId}"));
        }

        return diagnostics;
    }
}
