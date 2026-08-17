using Forge.Studio.Core.Navigation;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorldViewport
{
    private Node3D? _ux2NavigationOverlay;

    internal int NavigationOverlayPrimitiveCountUx2NavB { get; private set; }

    internal void ShowNavigationOverlayUx2NavB(StudioNavigationOverlaySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ClearNavigationOverlayUx2NavB();
        if (snapshot.Markers.Count == 0 && snapshot.Segments.Count == 0 && snapshot.Polygons.Count == 0)
        {
            return;
        }
        var material = new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true,
            NoDepthTest = true,
        };
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);
        var primitiveCount = 0;
        foreach (var polygon in snapshot.Polygons)
        {
            var color = OverlayColorUx2NavB(polygon.Semantic, polygon.Selected, polygon.Broken);
            for (var index = 0; index < polygon.Points.Count; index++)
            {
                AddNavigationLineUx2NavB(mesh, polygon.Points[index],
                    polygon.Points[(index + 1) % polygon.Points.Count], color);
                primitiveCount++;
            }
        }
        foreach (var segment in snapshot.Segments)
        {
            var color = OverlayColorUx2NavB(segment.Semantic, segment.Selected, segment.Broken);
            AddNavigationLineUx2NavB(mesh, segment.From, segment.To, color);
            primitiveCount++;
            if (segment.Directed)
            {
                AddDirectionTickUx2NavB(mesh, segment.From, segment.To, color);
                primitiveCount += 2;
            }
        }
        foreach (var marker in snapshot.Markers)
        {
            AddNavigationMarkerUx2NavB(mesh, marker.Position,
                OverlayColorUx2NavB(marker.Semantic, marker.Selected, marker.Broken), marker.Selected ? .6f : .38f);
            primitiveCount += 4;
        }
        mesh.SurfaceEnd();
        var instance = new MeshInstance3D
        {
            Name = "Ux2NavigationAuthoringOverlay",
            Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _world.AddChild(instance);
        _ux2NavigationOverlay = instance;
        NavigationOverlayPrimitiveCountUx2NavB = primitiveCount;
    }

    internal void ClearNavigationOverlayUx2NavB()
    {
        NavigationOverlayPrimitiveCountUx2NavB = 0;
        if (_ux2NavigationOverlay is null || !GodotObject.IsInstanceValid(_ux2NavigationOverlay))
        {
            _ux2NavigationOverlay = null;
            return;
        }
        _ux2NavigationOverlay.GetParent()?.RemoveChild(_ux2NavigationOverlay);
        _ux2NavigationOverlay.Free();
        _ux2NavigationOverlay = null;
    }

    private static void AddNavigationLineUx2NavB(ImmediateMesh mesh, StudioNavigationPoint from,
        StudioNavigationPoint to, Color color)
    {
        mesh.SurfaceSetColor(color);
        mesh.SurfaceAddVertex(ToGodotUx2NavB(from));
        mesh.SurfaceSetColor(color);
        mesh.SurfaceAddVertex(ToGodotUx2NavB(to));
    }

    private static void AddNavigationMarkerUx2NavB(ImmediateMesh mesh, StudioNavigationPoint point, Color color,
        float radius)
    {
        var center = ToGodotUx2NavB(point);
        AddNavigationLineUx2NavB(mesh, FromGodotUx2NavB(center + new Vector3(-radius, 0, 0)),
            FromGodotUx2NavB(center + new Vector3(radius, 0, 0)), color);
        AddNavigationLineUx2NavB(mesh, FromGodotUx2NavB(center + new Vector3(0, 0, -radius)),
            FromGodotUx2NavB(center + new Vector3(0, 0, radius)), color);
        AddNavigationLineUx2NavB(mesh, FromGodotUx2NavB(center),
            FromGodotUx2NavB(center + new Vector3(0, radius * 1.8f, 0)), color);
        AddNavigationLineUx2NavB(mesh, FromGodotUx2NavB(center + new Vector3(-radius * .45f, radius * 1.35f, 0)),
            FromGodotUx2NavB(center + new Vector3(radius * .45f, radius * 1.35f, 0)), color);
    }

    private static void AddDirectionTickUx2NavB(ImmediateMesh mesh, StudioNavigationPoint from,
        StudioNavigationPoint to, Color color)
    {
        var start = ToGodotUx2NavB(from);
        var end = ToGodotUx2NavB(to);
        var direction = end - start;
        if (direction.LengthSquared() < .0001f)
        {
            return;
        }
        direction = direction.Normalized();
        var side = new Vector3(-direction.Z, 0, direction.X) * .24f;
        var basePoint = end - direction * .52f;
        AddNavigationLineUx2NavB(mesh, FromGodotUx2NavB(end), FromGodotUx2NavB(basePoint + side), color);
        AddNavigationLineUx2NavB(mesh, FromGodotUx2NavB(end), FromGodotUx2NavB(basePoint - side), color);
    }

    private static Color OverlayColorUx2NavB(StudioNavigationOverlaySemantic semantic, bool selected, bool broken)
    {
        if (broken)
        {
            return new Color(1f, .28f, .22f, .95f);
        }
        if (selected)
        {
            return new Color(1f, .77f, .22f, 1f);
        }
        return semantic switch
        {
            StudioNavigationOverlaySemantic.Surface => new Color(.18f, .9f, .62f, .55f),
            StudioNavigationOverlaySemantic.OffMeshLink => new Color(.31f, .7f, 1f, .95f),
            StudioNavigationOverlaySemantic.Anchor => new Color(.92f, .44f, 1f, .95f),
            StudioNavigationOverlaySemantic.Transition => new Color(1f, .56f, .22f, .95f),
            _ => new Color(.82f, .9f, 1f, .82f),
        };
    }

    private static Vector3 ToGodotUx2NavB(StudioNavigationPoint point) =>
        new((float)point.X, (float)point.Y, (float)point.Z);

    private static StudioNavigationPoint FromGodotUx2NavB(Vector3 point) => new(point.X, point.Y, point.Z);
}
