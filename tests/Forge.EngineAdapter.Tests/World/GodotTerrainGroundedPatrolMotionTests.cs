using Forge.EngineAdapter.World;
using Godot;

namespace Forge.EngineAdapter.Tests.World;

public sealed class GodotTerrainGroundedPatrolMotionTests
{
    [Fact]
    public void AdvanceResamplesTerrainAtEveryIntermediatePatrolPosition()
    {
        static float HeightAt(float x, float z) => (x * .5f) + (z * .25f);

        var step = GodotTerrainGroundedPatrolMotion.Advance(
            new Vector3(0, 20, 0),
            new Vector3(10, -20, 0),
            2,
            1,
            HeightAt);

        Assert.False(step.Arrived);
        Assert.Equal(2, step.Position.X, 4);
        Assert.Equal(0, step.Position.Z, 4);
        Assert.Equal(1 + GodotTerrainGroundedPatrolMotion.GroundClearanceMeters,
            step.Position.Y, 4);
        Assert.Equal(Vector3.Right, step.Direction);
    }

    [Fact]
    public void AdvancePreservesThreeDimensionalRoutesWithoutAnExteriorTerrain()
    {
        var step = GodotTerrainGroundedPatrolMotion.Advance(
            Vector3.Zero,
            new Vector3(0, 3, 4),
            2.5,
            1,
            null);

        Assert.False(step.Arrived);
        Assert.Equal(new Vector3(0, 1.5f, 2), step.Position);
    }

    [Fact]
    public void GroundedVisualOffsetCentersTheModelAndPlacesItsLowestPointAtTheActorOrigin()
    {
        var minimum = new Vector3(-.5f, -1.8f, -1.25f);
        var maximum = new Vector3(1.5f, .2f, .75f);

        var offset = GodotTerrainGroundedPatrolMotion.GroundedVisualOffset(minimum, maximum);

        Assert.Equal(new Vector3(-.5f, 1.8f, .25f), offset);
        Assert.Equal(0, minimum.Y + offset.Y, 4);
    }
}
