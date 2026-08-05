using Forge.Domain.RuntimeExtensions;
using Godot;

namespace Forge.Runtime;

public partial class RuntimeAreaEffectIndicator : Node3D
{
    private MeshInstance3D? _mesh;
    private AreaEffectIndicatorProfile? _profile;

    public void Configure(AreaEffectIndicatorProfile profile)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Name = "RuntimeAreaEffectIndicator";
        Visible = false;
    }

    public void ShowPreview(AreaEffectShape shape, Vector3 origin, Vector3 direction, float range, float radius,
        float width, bool valid)
    {
        if (_profile is null || !_profile.Shapes.Contains(shape)) return;
        _mesh?.QueueFree();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(valid ? _profile.ValidColor : _profile.InvalidColor),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            NoDepthTest = true,
        };
        Mesh geometry = shape switch
        {
            AreaEffectShape.Circle => new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = 0.035f, RadialSegments = 64 },
            AreaEffectShape.Cone => new CylinderMesh { TopRadius = 0, BottomRadius = MathF.Max(radius, range * 0.55f), Height = 0.035f, RadialSegments = 48 },
            AreaEffectShape.Rectangle or AreaEffectShape.Line => new BoxMesh { Size = new Vector3(MathF.Max(width, 0.2f), 0.035f, range) },
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };
        _mesh = new MeshInstance3D
        {
            Mesh = geometry,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_mesh);
        GlobalPosition = origin + new Vector3(0, 0.035f, 0);
        if (shape is AreaEffectShape.Rectangle or AreaEffectShape.Line)
        {
            var flat = new Vector3(direction.X, 0, direction.Z).Normalized();
            if (flat.LengthSquared() < 0.001f) flat = Vector3.Forward;
            LookAt(GlobalPosition + flat, Vector3.Up);
            _mesh.Position = new Vector3(0, 0, -range / 2);
        }
        Visible = true;
    }

    public void HidePreview() => Visible = false;
}
