#if TOOLS
using Forge.Application.Navigation;
using Forge.Domain.Navigation;
using Godot;

namespace Forge.Toolset.Editor;

public partial class WaypointRoutePreview : Control
{
    private IReadOnlyList<WaypointProfile> _waypoints = [];
    private IReadOnlyList<PatrolRoute> _routes = [];
    private IReadOnlyList<GenericLocator> _locators = [];
    private IReadOnlySet<Guid> _navProjected = new HashSet<Guid>();
    private RouteVisualizationProfile? _visualization;

    public WaypointRoutePreview()
    {
        CustomMinimumSize = new Vector2(0, 300);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void SetData(IReadOnlyList<WaypointProfile> waypoints, IReadOnlyList<PatrolRoute> routes,
        IReadOnlyList<GenericLocator>? locators = null, RouteVisualizationProfile? visualization = null,
        IReadOnlySet<Guid>? navProjected = null)
    {
        _waypoints = waypoints.ToArray();
        _routes = routes.ToArray();
        _locators = (locators ?? []).ToArray();
        _visualization = visualization;
        _navProjected = navProjected ?? new HashSet<Guid>();
        QueueRedraw();
    }

    public override void _Draw()
    {
        var bounds = new Rect2(Vector2.Zero, Size);
        DrawRect(bounds, new Color("0d0a14"));
        DrawGrid(bounds);
        if (_waypoints.Count == 0 && _locators.Count == 0)
        {
            DrawString(ThemeDB.FallbackFont, bounds.GetCenter() - new Vector2(110, 0),
                "Aucun waypoint dans le brouillon", HorizontalAlignment.Left, -1, 14,
                new Color("a899b9"));
            return;
        }

        var positions = _waypoints.ToDictionary(item => item.Id, item => Project(item.Position.X,
            item.Position.Z, bounds));
        var knownIds = _waypoints.Select(item => item.Id).Concat(_locators.Select(item => item.Id)).ToHashSet();
        foreach (var route in _routes)
        {
            var segments = WaypointRouteService.AnalyzeRoute(route, _navProjected, knownIds,
                _visualization?.CostPerMeter ?? 1);
            foreach (var segment in segments)
            {
                var from = route.Points.SingleOrDefault(item => item.Id == segment.FromId);
                var to = route.Points.SingleOrDefault(item => item.Id == segment.ToId);
                if (from is null || to is null) continue;
                var a = Project(from.Position.X, from.Position.Z, bounds);
                var b = Project(to.Position.X, to.Position.Z, bounds);
                var color = segment.BrokenLink && (_visualization?.ShowBrokenLinks ?? true)
                    ? new Color("ff6b6b")
                    : segment.NavProjected && (_visualization?.ShowNavigationProjection ?? true)
                        ? new Color("55d6be") : new Color("8b5cf6");
                if (_visualization?.ShowSegments ?? true) DrawLine(a, b, color, 4, true);
                var middle = a.Lerp(b, .5f);
                if (_visualization?.ShowDirection ?? true) DrawArrow(middle, b - a, color);
                var details = new List<string>();
                if (_visualization?.ShowCosts ?? true) details.Add($"{segment.Cost:0.0}c");
                if (_visualization?.ShowTiming ?? true) details.Add($"{segment.TravelSeconds:0.0}s");
                if ((_visualization?.ShowActions ?? true) && !string.IsNullOrWhiteSpace(segment.Action))
                    details.Add(segment.Action);
                if (details.Count > 0 && segment.Direction >= 0) DrawString(ThemeDB.FallbackFont,
                    middle + new Vector2(7, -7),
                    string.Join(" · ", details), HorizontalAlignment.Left, -1, 11,
                    new Color("e4d5f7"));
            }
        }

        for (var index = 0; index < _waypoints.Count; index++)
        {
            var waypoint = _waypoints[index];
            var point = positions[waypoint.Id];
            var color = Color.FromHtml(waypoint.Appearance.Color);
            var radius = (float)Math.Clamp(waypoint.Appearance.ScreenSize / 4, 5, 18);
            DrawCircle(point, radius + 3, new Color("0d0a14"));
            DrawCircle(point, radius, color);
            if (waypoint.Marker is not null) DrawArc(point, radius + 7, 0, Mathf.Tau, 24,
                new Color("83d6ae"), 2, true);
            var label = LabelPosition(bounds, index);
            DrawLine(point, label - new Vector2(4, 4), new Color(color, .45f), 1, true);
            DrawString(ThemeDB.FallbackFont, label, waypoint.Name,
                HorizontalAlignment.Left, -1, 13, new Color("f3e8ff"));
        }
        for (var index = 0; index < _locators.Count; index++)
        {
            var locator = _locators[index];
            var point = Project(locator.Position.X, locator.Position.Z, bounds);
            var color = locator.Purpose switch
            {
                LocatorPurpose.Audio => new Color("58a6ff"),
                LocatorPurpose.Vfx => new Color("ff9858"),
                LocatorPurpose.Spawn => new Color("83d6ae"),
                LocatorPurpose.Objective => new Color("ffd166"),
                _ => new Color("c6a0f6"),
            };
            DrawColoredPolygon([point + new Vector2(0, -8), point + new Vector2(8, 0),
                point + new Vector2(0, 8), point + new Vector2(-8, 0)], color);
            var label = LabelPosition(bounds, _waypoints.Count + index);
            DrawLine(point, label - new Vector2(4, 4), new Color(color, .45f), 1, true);
            DrawString(ThemeDB.FallbackFont, label,
                $"{locator.Name} · {locator.Purpose}", HorizontalAlignment.Left, -1, 12,
                new Color("f3e8ff"));
        }
    }

    private void DrawArrow(Vector2 position, Vector2 delta, Color color)
    {
        if (delta.LengthSquared() < .001f) return;
        var direction = delta.Normalized();
        var normal = new Vector2(-direction.Y, direction.X);
        DrawColoredPolygon([position + direction * 7, position - direction * 5 + normal * 4,
            position - direction * 5 - normal * 4], color);
    }

    private static Vector2 LabelPosition(Rect2 bounds, int index) =>
        new(bounds.Size.X * .55f, 38 + index * 22);

    private static Vector2 Project(float x, float z, Rect2 bounds)
    {
        const float extent = 16;
        var usable = bounds.Grow(-28);
        return new Vector2(usable.Position.X + (x + extent) / (extent * 2) * usable.Size.X,
            usable.Position.Y + (z + extent) / (extent * 2) * usable.Size.Y);
    }

    private void DrawGrid(Rect2 bounds)
    {
        var usable = bounds.Grow(-28);
        for (var index = 0; index <= 8; index++)
        {
            var ratio = index / 8f;
            var x = usable.Position.X + usable.Size.X * ratio;
            var y = usable.Position.Y + usable.Size.Y * ratio;
            DrawLine(new Vector2(x, usable.Position.Y), new Vector2(x, usable.End.Y),
                new Color(0.25f, 0.2f, 0.32f, .55f), 1);
            DrawLine(new Vector2(usable.Position.X, y), new Vector2(usable.End.X, y),
                new Color(0.25f, 0.2f, 0.32f, .55f), 1);
        }
    }
}
#endif
