namespace Forge.EngineAdapter.Assets;

public sealed record CryptCharacterVisualDescriptor(string AssetId, string AnimationProfile);

public static class CryptCharacterVisualResolver
{
    public const string KnightAssetId = "053511fd-e165-1951-9869-6224f82b747f";
    public const string MageAssetId = "220be95a-282c-cc52-aaf9-4fe811b32609";
    public const string RogueAssetId = "056fe96c-5c83-145d-98fd-0623b9350cf5";
    public const string BarbarianAssetId = "3bf93c4b-7df6-455c-be89-ba38d40e941c";
    public const string AnimationProfile = "kaykit-adventurer";

    public static CryptCharacterVisualDescriptor? Resolve(IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        if (tags.Contains("player", StringComparer.OrdinalIgnoreCase))
            return new CryptCharacterVisualDescriptor(KnightAssetId, AnimationProfile);
        if (tags.Contains("elyra", StringComparer.OrdinalIgnoreCase))
            return new CryptCharacterVisualDescriptor(MageAssetId, AnimationProfile);
        if (tags.Contains("sentinel", StringComparer.OrdinalIgnoreCase))
            return new CryptCharacterVisualDescriptor(RogueAssetId, AnimationProfile);
        if (tags.Contains("warden", StringComparer.OrdinalIgnoreCase))
            return new CryptCharacterVisualDescriptor(BarbarianAssetId, AnimationProfile);
        return null;
    }
}
