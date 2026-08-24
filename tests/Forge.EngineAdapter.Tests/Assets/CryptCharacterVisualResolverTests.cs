using Forge.EngineAdapter.Assets;

namespace Forge.EngineAdapter.Tests.Assets;

public sealed class CryptCharacterVisualResolverTests
{
    [Theory]
    [InlineData("player", CryptCharacterVisualResolver.KnightAssetId)]
    [InlineData("ELYRA", CryptCharacterVisualResolver.MageAssetId)]
    [InlineData("sentinel", CryptCharacterVisualResolver.RogueAssetId)]
    [InlineData("warden", CryptCharacterVisualResolver.BarbarianAssetId)]
    public void ResolveMapsCryptRolesToTheSharedGovernedModels(string tag, string expectedAssetId)
    {
        var resolved = CryptCharacterVisualResolver.Resolve([tag]);

        Assert.NotNull(resolved);
        Assert.Equal(expectedAssetId, resolved.AssetId);
        Assert.Equal(CryptCharacterVisualResolver.AnimationProfile, resolved.AnimationProfile);
    }

    [Fact]
    public void ResolveDoesNotMapUnrelatedEntities()
    {
        Assert.Null(CryptCharacterVisualResolver.Resolve(["floor", "architecture"]));
    }
}
