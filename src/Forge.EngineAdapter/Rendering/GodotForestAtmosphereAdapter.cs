using Godot;

namespace Forge.EngineAdapter.Rendering;

public sealed record GodotForestAtmosphereResult(
    Node3D Root,
    int AnimatedEmitters,
    int Particles,
    string Provenance);

public static class GodotForestAtmosphereAdapter
{
    public static GodotForestAtmosphereResult Build()
    {
        var root = new Node3D { Name = "ForgeForestAtmosphere" };
        var fireflies = CreateEmitter(
            "ForestFireflies",
            72,
            8,
            new Vector3(0, 3.2f, 0),
            new Vector3(56, 2.8f, 56),
            new Vector3(.2f, .15f, .1f),
            Vector3.Zero,
            .08f,
            .34f,
            new Vector2(.075f, .075f),
            new Color(1f, .84f, .28f, .9f),
            emission: true);
        var leaves = CreateEmitter(
            "ForestFallingLeaves",
            96,
            12,
            new Vector3(0, 11, 0),
            new Vector3(60, 1.2f, 60),
            new Vector3(.45f, -.38f, .18f),
            new Vector3(.08f, -.12f, .03f),
            .18f,
            .62f,
            new Vector2(.12f, .20f),
            new Color(.48f, .66f, .20f, .76f),
            emission: false);
        var mist = CreateEmitter(
            "ForestGroundMist",
            36,
            15,
            new Vector3(0, .65f, 0),
            new Vector3(60, .22f, 60),
            new Vector3(.25f, .015f, .08f),
            Vector3.Zero,
            .05f,
            .22f,
            new Vector2(5.2f, 1.8f),
            new Color(.68f, .78f, .72f, .075f),
            emission: false);
        root.AddChild(fireflies);
        root.AddChild(leaves);
        root.AddChild(mist);
        return new(root, 3, fireflies.Amount + leaves.Amount + mist.Amount,
            "builtin:openrpg-forge:forest-atmosphere@1");
    }

    private static GpuParticles3D CreateEmitter(
        string name,
        int amount,
        float lifetime,
        Vector3 position,
        Vector3 extents,
        Vector3 direction,
        Vector3 gravity,
        float velocityMin,
        float velocityMax,
        Vector2 size,
        Color color,
        bool emission)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            EmissionEnabled = emission,
            Emission = emission ? new Color(color.R, color.G, color.B) : Colors.Black,
            EmissionEnergyMultiplier = emission ? 2.2f : 1,
        };
        var process = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = extents,
            Direction = direction.Normalized(),
            Spread = 180,
            Gravity = gravity,
            InitialVelocityMin = velocityMin,
            InitialVelocityMax = velocityMax,
            ScaleMin = .75f,
            ScaleMax = 1.25f,
        };
        return new GpuParticles3D
        {
            Name = name,
            Position = position,
            Amount = amount,
            Lifetime = lifetime,
            Preprocess = lifetime,
            Randomness = .85f,
            VisibilityAabb = new Aabb(new Vector3(-66, -18, -66), new Vector3(132, 36, 132)),
            ProcessMaterial = process,
            DrawPass1 = new QuadMesh { Size = size, Material = material },
            Emitting = true,
        };
    }
}
