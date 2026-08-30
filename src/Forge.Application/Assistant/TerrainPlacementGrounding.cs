using Forge.Domain.Blueprints;

namespace Forge.Application.Assistant;

public static class TerrainPlacementGrounding
{
    public const float PlayerSpawnCapsuleCenterOffsetMeters = 1.05f;

    public static float AnchorHeight(BlueprintKind kind, float groundHeight) => kind switch
    {
        BlueprintKind.PlayerSpawn => groundHeight + PlayerSpawnCapsuleCenterOffsetMeters,
        _ => groundHeight,
    };
}
