using Forge.Domain.Areas;
using Godot;

namespace Forge.EngineAdapter.World;

public sealed record GodotSurfaceWetnessResult(
    float SurfaceWetness,
    int ShaderMaterials,
    int PbrMaterials,
    int FallbackMaterials,
    IReadOnlyList<string> Diagnostics,
    GodotSurfaceWetnessPbrSample? PbrSample);

public sealed record GodotSurfaceWetnessPbrSample(
    PbrMaterialState Dry,
    PbrMaterialState Projected);

public sealed class GodotSurfaceWetnessAdapter
{
    public const string LocalResourceMeta = "forge_surface_wetness_local";
    private const string WetnessUniform = "forge_surface_wetness";
    private const string DryAlbedoMeta = "forge_surface_wetness_dry_albedo";
    private const string DryRoughnessMeta = "forge_surface_wetness_dry_roughness";
    private const string DrySpecularMeta = "forge_surface_wetness_dry_specular";

    private readonly Dictionary<ulong, Mesh> _localizedMeshes = [];
    private readonly Dictionary<ulong, Material> _localizedMaterials = [];
    private readonly Dictionary<BaseMaterial3D, PbrMaterialState> _dryPbr = [];
    private readonly Dictionary<ulong, ShaderMaterial> _shaderMaterials = [];
    private readonly HashSet<ulong> _fallbackMaterials = [];
    private readonly List<string> _diagnostics = [];

    public GodotSurfaceWetnessResult BindAndApply(
        Node root,
        float surfaceWetness,
        SurfaceWetnessMaterialProfile coefficients)
    {
        ArgumentNullException.ThrowIfNull(root);
        _localizedMeshes.Clear();
        _localizedMaterials.Clear();
        _dryPbr.Clear();
        _shaderMaterials.Clear();
        _fallbackMaterials.Clear();
        _diagnostics.Clear();

        foreach (var candidate in root.FindChildren("*", "GeometryInstance3D", recursive: true, owned: false))
        {
            if (candidate is not GeometryInstance3D geometry) continue;
            geometry.MaterialOverride = LocalizeMaterial(geometry.MaterialOverride);
            geometry.MaterialOverlay = LocalizeMaterial(geometry.MaterialOverlay);
            if (geometry is MeshInstance3D meshInstance && meshInstance.Mesh is { } mesh)
            {
                for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
                {
                    var surfaceMaterial =
                        meshInstance.GetSurfaceOverrideMaterial(surface) ?? mesh.SurfaceGetMaterial(surface);
                    if (surfaceMaterial is not null)
                        meshInstance.SetSurfaceOverrideMaterial(surface, LocalizeMaterial(surfaceMaterial));
                }
            }
            else if (geometry is MultiMeshInstance3D multiMeshInstance &&
                     multiMeshInstance.Multimesh?.Mesh is { } multiMesh)
            {
                multiMeshInstance.Multimesh.Mesh = LocalizeMesh(multiMesh);
            }
        }

        return Apply(surfaceWetness, coefficients);
    }

    public GodotSurfaceWetnessResult Apply(
        float surfaceWetness,
        SurfaceWetnessMaterialProfile coefficients)
    {
        ArgumentNullException.ThrowIfNull(coefficients);
        if (!float.IsFinite(surfaceWetness) || surfaceWetness is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(surfaceWetness));

        GodotSurfaceWetnessPbrSample? sample = null;
        foreach (var pair in _dryPbr)
        {
            var projected = SurfaceWetnessMaterialProjector.Project(pair.Value, surfaceWetness, coefficients);
            sample ??= new(pair.Value, projected);
            pair.Key.AlbedoColor = new Color(
                projected.AlbedoR,
                projected.AlbedoG,
                projected.AlbedoB,
                projected.Alpha);
            pair.Key.Roughness = projected.Roughness;
            pair.Key.MetallicSpecular = projected.Specular;
        }
        foreach (var material in _shaderMaterials.Values)
        {
            material.SetShaderParameter(WetnessUniform, surfaceWetness);
            material.SetShaderParameter("forge_wetness_albedo_darkening", coefficients.AlbedoDarkening);
            material.SetShaderParameter("forge_wetness_roughness_reduction", coefficients.RoughnessReduction);
            material.SetShaderParameter("forge_wetness_specular_increase", coefficients.SpecularIncrease);
        }
        return new(surfaceWetness, _shaderMaterials.Count, _dryPbr.Count,
            _fallbackMaterials.Count, _diagnostics.ToArray(), sample);
    }

    private Mesh LocalizeMesh(Mesh source)
    {
        var sourceId = source.GetInstanceId();
        if (_localizedMeshes.TryGetValue(sourceId, out var cached)) return cached;
        var local = IsLocal(source)
            ? source
            : source.Duplicate(deep: true) as Mesh ??
              throw new InvalidOperationException("A governed PBR mesh could not be localized for weather projection.");
        local.SetMeta(LocalResourceMeta, true);
        _localizedMeshes[sourceId] = local;
        for (var surface = 0; surface < local.GetSurfaceCount(); surface++)
        {
            var material = local.SurfaceGetMaterial(surface);
            if (material is not null)
                local.SurfaceSetMaterial(surface, LocalizeMaterial(material));
        }
        return local;
    }

    private Material? LocalizeMaterial(Material? source)
    {
        if (source is null) return null;
        var sourceId = source.GetInstanceId();
        if (_localizedMaterials.TryGetValue(sourceId, out var cached)) return cached;
        var local = IsLocal(source)
            ? source
            : source.Duplicate(deep: true) as Material ??
              throw new InvalidOperationException("A governed PBR material could not be localized for weather projection.");
        local.SetMeta(LocalResourceMeta, true);
        _localizedMaterials[sourceId] = local;
        Register(local);
        return local;
    }

    private void Register(Material material)
    {
        var id = material.GetInstanceId();
        if (material is ShaderMaterial shader &&
            shader.Shader?.Code.Contains(WetnessUniform, StringComparison.Ordinal) == true)
        {
            _shaderMaterials[id] = shader;
            return;
        }
        if (material is BaseMaterial3D pbr)
        {
            PbrMaterialState dry;
            if (pbr.HasMeta(DryAlbedoMeta))
            {
                var color = pbr.GetMeta(DryAlbedoMeta).AsColor();
                dry = new(color.R, color.G, color.B, color.A,
                    pbr.GetMeta(DryRoughnessMeta).AsSingle(),
                    pbr.GetMeta(DrySpecularMeta).AsSingle());
            }
            else
            {
                var color = pbr.AlbedoColor;
                dry = new(color.R, color.G, color.B, color.A, pbr.Roughness, pbr.MetallicSpecular);
                pbr.SetMeta(DryAlbedoMeta, color);
                pbr.SetMeta(DryRoughnessMeta, pbr.Roughness);
                pbr.SetMeta(DrySpecularMeta, pbr.MetallicSpecular);
            }
            _dryPbr[pbr] = dry;
            return;
        }
        if (_fallbackMaterials.Add(id))
            _diagnostics.Add($"material={material.ResourceName} type={material.GetType().Name} fallback=unchanged");
    }

    private static bool IsLocal(Resource resource) =>
        resource.HasMeta(LocalResourceMeta) && resource.GetMeta(LocalResourceMeta).AsBool();
}
