using Forge.Domain.Areas;
using Forge.Domain.Validation;

namespace Forge.Domain.Navigation;

public static class WaypointAuthoringValidator
{
    public const string IdentityInvalidCode = "waypoint.authoring.identity_invalid";
    public const string AreaMismatchCode = "waypoint.authoring.area_mismatch";
    public const string RequiredContentCode = "waypoint.authoring.required_missing";
    public const string AppearanceInvalidCode = "waypoint.appearance.invalid";
    public const string MarkerInvalidCode = "waypoint.marker.invalid";
    public const string AnchorMissingCode = "waypoint.anchor.missing";
    public const string RouteInvalidCode = "waypoint.route.invalid";
    public const string RouteReferenceCode = "waypoint.route.reference_invalid";
    public const string ArrivalInvalidCode = "waypoint.arrival.invalid";
    public const string LocatorInvalidCode = "waypoint.locator.invalid";
    public const string CollectionInvalidCode = "waypoint.collection.invalid";
    public const string VisualizationInvalidCode = "waypoint.route_visualization.invalid";
    public const string BudgetExceededCode = "waypoint.authoring.budget_exceeded";

    public static IReadOnlyList<DomainDiagnostic> Validate(AreaDefinition area)
    {
        ArgumentNullException.ThrowIfNull(area);
        var profile = area.Navigation.Waypoints;
        var root = $"areas/{area.Id}/navigation/waypoints";
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.Id == Guid.Empty || profile.SchemaVersion != WaypointAuthoringProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(profile.Provenance))
            diagnostics.Add(new(IdentityInvalidCode,
                "Waypoint authoring requires a stable UUID, schema v1 and provenance.", root));
        if (profile.AreaId != area.Id)
            diagnostics.Add(new(AreaMismatchCode,
                "Waypoint authoring area identity must match its owning area.", $"{root}/areaId"));
        if (profile.RequiredAtRuntime && profile.Waypoints.Count == 0)
            diagnostics.Add(new(RequiredContentCode,
                "Required waypoint authoring must contain at least one waypoint.", root));
        if (profile.Waypoints.Count > 256 || profile.PatrolRoutes.Count > 128 ||
            profile.PatrolRoutes.Sum(item => item.Points.Count) > 2048 ||
            profile.Locators.Count > 512 || profile.Collections.Count > 128)
            diagnostics.Add(new(BudgetExceededCode,
                "Waypoint authoring exceeds the 256 waypoint, 128 route, 2048 patrol-point, " +
                "512 locator or 128 collection budget.", root));

        var waypointIds = profile.Waypoints.Select(item => item.Id).ToArray();
        if (waypointIds.Any(id => id == Guid.Empty) || waypointIds.Distinct().Count() != waypointIds.Length ||
            profile.Waypoints.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            profile.Waypoints.Count)
            diagnostics.Add(new(IdentityInvalidCode,
                "Waypoint UUIDs and names must be non-empty and unique.", $"{root}/items"));
        var anchorIds = area.Navigation.Anchors.Instances.Select(item => item.Id).ToHashSet();
        for (var index = 0; index < profile.Waypoints.Count; index++)
        {
            var waypoint = profile.Waypoints[index];
            var path = $"{root}/items/{index}";
            if (waypoint.SchemaVersion != 1 || string.IsNullOrWhiteSpace(waypoint.Provenance) ||
                string.IsNullOrWhiteSpace(waypoint.Name) ||
                string.IsNullOrWhiteSpace(waypoint.ForgeScriptApiVersion) || !Inside(area, waypoint.Position))
                diagnostics.Add(new(IdentityInvalidCode,
                    "Waypoint requires schema v1, provenance, name, finite in-area position and script API.", path));
            if (string.IsNullOrWhiteSpace(waypoint.Appearance.Icon) ||
                string.IsNullOrWhiteSpace(waypoint.Appearance.Layer) ||
                !ValidColor(waypoint.Appearance.Color) ||
                !double.IsFinite(waypoint.Appearance.ScreenSize) || waypoint.Appearance.ScreenSize is < 8 or > 96)
                diagnostics.Add(new(AppearanceInvalidCode,
                    "Waypoint appearance requires icon, layer, #RRGGBB[A] color and 8–96 px size.",
                    $"{path}/appearance"));
            if (waypoint.Marker is { } marker &&
                (string.IsNullOrWhiteSpace(marker.Text) || string.IsNullOrWhiteSpace(marker.Icon) ||
                 string.IsNullOrWhiteSpace(marker.Category) || marker.Floor is < -64 or > 64 ||
                 !double.IsFinite(marker.Precision) || marker.Precision is < 0 or > 100 ||
                 marker.VisibilityPolicy == MarkerVisibilityPolicy.HiddenUntilRevealed &&
                 (string.IsNullOrWhiteSpace(marker.RevealEvent) ||
                  string.IsNullOrWhiteSpace(marker.RevealAnimation))))
                diagnostics.Add(new(MarkerInvalidCode,
                    "Map marker requires text, icon, category, valid floor/precision and reveal metadata.",
                    $"{path}/marker"));
            if (waypoint.AnchorInstanceId is { } anchorId && !anchorIds.Contains(anchorId))
                diagnostics.Add(new(AnchorMissingCode,
                    $"Waypoint anchor instance '{anchorId:D}' does not exist.",
                    $"{path}/anchorInstanceId"));
            ValidateArrival(waypoint.Arrival, $"{path}/arrival", diagnostics);
        }

        var routeIds = profile.PatrolRoutes.Select(item => item.Id).ToArray();
        if (routeIds.Any(id => id == Guid.Empty) || routeIds.Distinct().Count() != routeIds.Length)
            diagnostics.Add(new(RouteInvalidCode, "Patrol route UUIDs must be non-empty and unique.",
                $"{root}/patrolRoutes"));
        for (var index = 0; index < profile.PatrolRoutes.Count; index++)
            ValidateRoute(area, profile.PatrolRoutes[index], $"{root}/patrolRoutes/{index}", diagnostics);
        ValidateLocators(area, profile, root, diagnostics);
        ValidateVisualization(profile.RouteVisualization, $"{root}/routeVisualization", diagnostics);
        return diagnostics;
    }

    private static void ValidateLocators(AreaDefinition area, WaypointAuthoringProfile profile, string root,
        ICollection<DomainDiagnostic> diagnostics)
    {
        var locatorIds = profile.Locators.Select(item => item.Id).ToArray();
        if (locatorIds.Any(id => id == Guid.Empty) || locatorIds.Distinct().Count() != locatorIds.Length ||
            profile.Locators.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            profile.Locators.Count)
            diagnostics.Add(new(LocatorInvalidCode,
                "Locator UUIDs and names must be non-empty and unique.", $"{root}/locators"));
        for (var index = 0; index < profile.Locators.Count; index++)
        {
            var locator = profile.Locators[index];
            var path = $"{root}/locators/{index}";
            if (locator.SchemaVersion != 1 || string.IsNullOrWhiteSpace(locator.Provenance) ||
                string.IsNullOrWhiteSpace(locator.Name) || !Inside(area, locator.Position) ||
                locator.Tags is null || locator.Tags.Any(string.IsNullOrWhiteSpace) ||
                locator.ExplicitOverrides?.Any(item => string.IsNullOrWhiteSpace(item.Key)) == true)
                diagnostics.Add(new(LocatorInvalidCode,
                    "Locator requires identity, bounded position, typed purpose, tags and explicit overrides.", path));
            ValidateArrival(locator.Arrival, $"{path}/arrival", diagnostics);
        }

        var collectionIds = profile.Collections.Select(item => item.Id).ToArray();
        if (collectionIds.Any(id => id == Guid.Empty) ||
            collectionIds.Distinct().Count() != collectionIds.Length ||
            profile.Collections.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            profile.Collections.Count)
            diagnostics.Add(new(CollectionInvalidCode,
                "Collection UUIDs and names must be non-empty and unique.", $"{root}/collections"));
        for (var index = 0; index < profile.Collections.Count; index++)
        {
            var collection = profile.Collections[index];
            var path = $"{root}/collections/{index}";
            var ids = collection.LocatorIds ?? [];
            if (collection.SchemaVersion != 1 || string.IsNullOrWhiteSpace(collection.Provenance) ||
                string.IsNullOrWhiteSpace(collection.Name) ||
                string.IsNullOrWhiteSpace(collection.Query) && (collection.RequiredTags?.Count ?? 0) == 0 &&
                ids.Count == 0 || ids.Any(id => id == Guid.Empty || !locatorIds.Contains(id)) ||
                ids.Distinct().Count() != ids.Count ||
                collection.RequiredTags?.Any(string.IsNullOrWhiteSpace) == true ||
                collection.Selection == LocatorSelectionMode.Nearest && collection.Origin is null)
                diagnostics.Add(new(CollectionInvalidCode,
                    "Collection requires identity, a query/tag/member filter, resolved unique locators and " +
                    "an origin for nearest selection.", path));
        }
    }

    private static void ValidateArrival(ArrivalBehavior? arrival, string path,
        ICollection<DomainDiagnostic> diagnostics)
    {
        if (arrival is null) return;
        if (!double.IsFinite(arrival.DirectionDegrees) || arrival.DirectionDegrees is < -360 or > 360 ||
            !double.IsFinite(arrival.WaitSeconds) || arrival.WaitSeconds is < 0 or > 3600 ||
            arrival.Facing == ArrivalFacingMode.Target && string.IsNullOrWhiteSpace(arrival.TargetId) ||
            arrival.Facing == ArrivalFacingMode.Direction && !string.IsNullOrWhiteSpace(arrival.TargetId) ||
            !ValidAction(arrival.Animation) || !ValidAction(arrival.Interaction) ||
            !ValidAction(arrival.Callback))
            diagnostics.Add(new(ArrivalInvalidCode,
                "Arrival behavior requires bounded facing/wait and safe animation, interaction and callback names.", path));
    }

    private static void ValidateVisualization(RouteVisualizationProfile visualization, string path,
        ICollection<DomainDiagnostic> diagnostics)
    {
        if (visualization.Id == Guid.Empty || visualization.SchemaVersion != 1 ||
            string.IsNullOrWhiteSpace(visualization.Provenance) ||
            !double.IsFinite(visualization.CostPerMeter) || visualization.CostPerMeter is <= 0 or > 10_000 ||
            !(visualization.ShowSegments || visualization.ShowDirection || visualization.ShowCosts ||
              visualization.ShowActions || visualization.ShowBrokenLinks ||
              visualization.ShowNavigationProjection || visualization.ShowTiming))
            diagnostics.Add(new(VisualizationInvalidCode,
                "Route visualization requires identity, a positive cost scale and at least one visible layer.", path));
    }

    private static void ValidateRoute(AreaDefinition area, PatrolRoute route, string path,
        ICollection<DomainDiagnostic> diagnostics)
    {
        var pointIds = route.Points.Select(item => item.Id).ToArray();
        if (route.SchemaVersion != 1 || string.IsNullOrWhiteSpace(route.Provenance) ||
            route.Points.Count < 2 || pointIds.Any(id => id == Guid.Empty) ||
            pointIds.Distinct().Count() != pointIds.Length || route.Points.Any(point =>
                !Inside(area, point.Position) || !double.IsFinite(point.WaitSeconds) ||
                point.WaitSeconds is < 0 or > 3600 || !double.IsFinite(point.FacingDegrees) ||
                point.FacingDegrees is < -360 or > 360 || !double.IsFinite(point.Speed) ||
                point.Speed is <= 0 or > 100 || !double.IsFinite(point.Weight) || point.Weight < 0) ||
            route.Traversal == PatrolTraversalPolicy.Weighted && route.Points.Sum(item => item.Weight) <= 0 ||
            route.Traversal == PatrolTraversalPolicy.Scripted && string.IsNullOrWhiteSpace(route.ScriptHandler))
            diagnostics.Add(new(RouteInvalidCode,
                "Patrol route requires schema/provenance, two bounded points and a valid traversal policy.", path));
        var edgeIds = route.Edges.Select(item => item.Id).ToArray();
        var expectedOrders = Enumerable.Range(0, route.Edges.Count).ToArray();
        if (route.Edges.Count != route.Points.Count - 1 || edgeIds.Any(id => id == Guid.Empty) ||
            edgeIds.Distinct().Count() != edgeIds.Length ||
            !route.Edges.Select(item => item.DisplayOrder).SequenceEqual(expectedOrders) ||
            route.Edges.Any(edge => !pointIds.Contains(edge.FromPointId) || !pointIds.Contains(edge.ToPointId) ||
                edge.FromPointId == edge.ToPointId || !double.IsFinite(edge.Weight) || edge.Weight < 0))
            diagnostics.Add(new(RouteReferenceCode,
                "Patrol edges require stable UUIDs, deterministic order and resolved point references.",
                $"{path}/edges"));
    }

    private static bool Inside(AreaDefinition area, Forge.Domain.Spatial.Vector3Value position) =>
        Math.Abs(position.X) <= area.Dimensions.Width / 2f &&
        Math.Abs(position.Z) <= area.Dimensions.Depth / 2f && Math.Abs(position.Y) <= 4096;

    private static bool ValidColor(string value) => value is { Length: 7 or 9 } && value[0] == '#' &&
        value.AsSpan(1).ToArray().All(char.IsAsciiHexDigit);

    private static bool ValidAction(string? value) => string.IsNullOrWhiteSpace(value) ||
        value.Length <= 128 && value.All(character => char.IsLetterOrDigit(character) ||
            character is '.' or '_' or '-' or ':');
}
