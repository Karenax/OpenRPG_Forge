using Forge.Domain.Areas;
using Forge.EngineAdapter.World;

namespace Forge.EngineAdapter.Tests.World;

public sealed class GodotTerrainMaterialAdapterTests
{
    [Fact]
    public void DescriptorExposesProceduralPbrContractForCanonicalTerrain()
    {
        var world = AreaWorldProfile.CreateDefault(
            AreaId.From(Guid.Parse("a1190000-0000-4000-8000-000000000092")),
            AreaKind.Exterior,
            new AreaDimensions(96, 80));

        var descriptor = GodotTerrainMaterialAdapter.Describe(world.Terrain);

        Assert.Equal(GodotTerrainMaterialAdapter.ProceduralPbrMode, descriptor.Mode);
        Assert.Equal(3, descriptor.LayerCount);
        Assert.True(descriptor.UsesVertexBlend);
        Assert.True(descriptor.UsesMacroVariation);
        Assert.True(descriptor.PhysicallyBased);
    }

    [Fact]
    public void ShaderContractDrivesPbrChannelsAndWorldSpaceVariation()
    {
        var shader = GodotTerrainMaterialAdapter.ProceduralPbrShaderCode;

        Assert.Contains("MODEL_MATRIX", shader, StringComparison.Ordinal);
        Assert.Contains("COLOR.rgb", shader, StringComparison.Ordinal);
        Assert.Contains("ROUGHNESS", shader, StringComparison.Ordinal);
        Assert.Contains("METALLIC", shader, StringComparison.Ordinal);
        Assert.Contains("AO_LIGHT_AFFECT", shader, StringComparison.Ordinal);
        Assert.Contains("surface_texture", shader, StringComparison.Ordinal);
        Assert.Contains("use_surface_texture", shader, StringComparison.Ordinal);
        Assert.Contains("secondary_surface_texture", shader, StringComparison.Ordinal);
        Assert.Contains("use_secondary_surface_texture", shader, StringComparison.Ordinal);

        var roadShader = GodotTerrainMaterialAdapter.ProceduralRoadShaderCode;
        Assert.Contains("worn_center", roadShader, StringComparison.Ordinal);
        Assert.Contains("ROUGHNESS", roadShader, StringComparison.Ordinal);
        Assert.Contains("world_position", roadShader, StringComparison.Ordinal);
        Assert.Contains("surface_texture", roadShader, StringComparison.Ordinal);
        Assert.Contains("use_surface_texture", roadShader, StringComparison.Ordinal);
        Assert.DoesNotContain("render_mode unshaded", roadShader, StringComparison.Ordinal);
    }
}
