using Forge.EngineAdapter.Assets;

namespace Forge.EngineAdapter.Tests.Assets;

public sealed class GodotAssetAnimationControllerTests
{
    [Theory]
    [InlineData("idle_no_loop", "idle_no")]
    [InlineData("walk_carry_loop", "walk_carry")]
    [InlineData("idle_loop", "idle")]
    [InlineData("jog_fwd_loop", "jog_fwd")]
    public void Resolves_loop_suffix_consumed_by_the_godot_gltf_importer(
        string requested,
        string imported)
    {
        Assert.Equal(imported,
            GodotAssetAnimationController.ResolveClip(["hit_knockback", imported], requested));
    }
}
