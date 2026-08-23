using Forge.Network.Operations;

namespace Forge.LiveDirector.Catalog;

public sealed class LiveCatalogService(AuthorizationService authorization)
{
    public sealed record Blueprint(Guid BlueprintId, string Name, string Kind, string Package, int Cost,
        IReadOnlySet<Guid> AllowedZoneIds, bool ServerAllowed, string? Thumbnail = null,
        string? RequiredCapability = null);
    public sealed record Query(string? Text, string? Kind, Guid ZoneId, int MaximumCost,
        string? Package = null);

    private readonly Dictionary<Guid, Blueprint> _blueprints = [];

    public void Replace(IEnumerable<Blueprint> blueprints)
    {
        var materialized = blueprints.ToArray();
        if (materialized.Any(x => x.BlueprintId == Guid.Empty || string.IsNullOrWhiteSpace(x.Name) ||
                                  string.IsNullOrWhiteSpace(x.Kind) || string.IsNullOrWhiteSpace(x.Package) ||
                                  x.Cost < 0 || x.AllowedZoneIds.Count == 0 ||
                                  x.AllowedZoneIds.Contains(Guid.Empty)) ||
            materialized.Select(x => x.BlueprintId).Distinct().Count() != materialized.Length)
            throw new ArgumentException("LIVE_CATALOG_INVALID", nameof(blueprints));
        _blueprints.Clear();
        foreach (var blueprint in materialized) _blueprints.Add(blueprint.BlueprintId, blueprint);
    }

    public IReadOnlyList<Blueprint> Search(AuthorizationSubject actor, Query query)
    {
        if (!authorization.Authorize(actor, NetworkCapability.ManageWorld, query.ZoneId).Allowed) return [];
        return _blueprints.Values
            .Where(x => x.ServerAllowed && x.Cost <= query.MaximumCost && x.AllowedZoneIds.Contains(query.ZoneId))
            .Where(x => string.IsNullOrWhiteSpace(query.Package) ||
                        string.Equals(x.Package, query.Package, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(query.Kind) || string.Equals(x.Kind, query.Kind, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(query.Text) || x.Name.Contains(query.Text, StringComparison.OrdinalIgnoreCase) || x.Package.Contains(query.Text, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(x.RequiredCapability) ||
                        authorization.Authorize(actor, NetworkCapability.ManageWorld, query.ZoneId,
                            x.RequiredCapability).Allowed)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.BlueprintId).ToArray();
    }

    public Blueprint? FindAllowed(AuthorizationSubject actor, Guid blueprintId, Guid zoneId) =>
        Search(actor, new(null, null, zoneId, int.MaxValue)).FirstOrDefault(x => x.BlueprintId == blueprintId);
}
