using Forge.Application.WorldGeneration;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.WorldGeneration;

public sealed class WorldGenerationBuildingEntranceInspectorTests
{
    [Fact]
    public void AcceptsClosedClickableDoorAndAlignedThresholdStair()
    {
        var area = Area(Door(
            new Vector3Value(-.5f, .85f, 0),
            QuaternionValue.Identity,
            interactive: true));

        var result = WorldGenerationBuildingEntranceInspector.Inspect(area);

        Assert.True(result.Passed, string.Join(" | ", result.Diagnostics.Select(item => item.Message)));
        Assert.Equal(1, result.RequiredGroupCount);
        Assert.Equal(1, result.ValidEntranceCount);
    }

    [Fact]
    public void RejectsDoorPivotAtOpeningCenterAndHalfMeterStairOffset()
    {
        var area = Area(Door(
            new Vector3Value(0, .85f, 0),
            QuaternionValue.Identity,
            interactive: true), stairX: .5f);

        var result = WorldGenerationBuildingEntranceInspector.Inspect(area);

        Assert.False(result.Passed);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationBuildingEntranceInspector.DoorAlignmentInvalidCode);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationBuildingEntranceInspector.StairAlignmentInvalidCode);
    }

    [Fact]
    public void RejectsDecorativeOpenOrMisalignedDoorAndMissingStairRole()
    {
        var door = Door(
            new Vector3Value(0, 0, 0),
            new QuaternionValue(0, .70710677f, 0, .70710677f),
            interactive: false,
            extraTags: ["open-door", "nonblocking"]);
        var area = Area(door, stairTags: ["entrance", "nonblocking"]);

        var result = WorldGenerationBuildingEntranceInspector.Inspect(area);

        Assert.False(result.Passed);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationBuildingEntranceInspector.RoleMissingCode);
    }

    private static AreaDefinition Area(
        EntityInstance door,
        IReadOnlyList<string>? stairTags = null,
        float stairX = 0)
    {
        var group = "assistant-node:aaaaaaaa-0000-4000-8000-000000000001";
        var frame = Entity(1, new Vector3Value(0, 0, 0), QuaternionValue.Identity,
            [group, "assistant-generated", "medieval-house", "village-building", "entrance", "doorway"]);
        var stair = Entity(2, new Vector3Value(stairX, -.12f, 1.1f), QuaternionValue.Identity,
            [group, "assistant-generated", "medieval-house", "village-building", .. stairTags ?? ["entrance", "entrance-stairs", "nonblocking"]]);
        var fillers = Enumerable.Range(3, 4).Select(index => Entity(index,
            new Vector3Value(index, 0, -2), QuaternionValue.Identity,
            [group, "assistant-generated", "medieval-house", "village-building", index == 3 ? "foundation" : "wall"]));
        return new AreaDefinition(AreaId.From(Guid.NewGuid()), "entrance-test", "Entrance test",
            [frame, stair, door, .. fillers]);
    }

    private static EntityInstance Door(
        Vector3Value position,
        QuaternionValue rotation,
        bool interactive,
        IReadOnlyList<string>? extraTags = null)
    {
        var tags = new List<string>
        {
            "assistant-node:aaaaaaaa-0000-4000-8000-000000000001",
            "assistant-generated",
            "medieval-house",
            "village-building",
            "entrance",
            "interactive-door",
        };
        tags.AddRange(extraTags ?? []);
        var overrides = interactive
            ? EntityClickTriggerSettings.Merge(EntityPropertyOverrides.Empty,
                new EntityClickTriggerSettings(true, EntityClickTriggerAction.ToggleDoor, null, null))
            : EntityPropertyOverrides.Empty;
        return Entity(10, position, rotation, tags, overrides);
    }

    private static EntityInstance Entity(
        int suffix,
        Vector3Value position,
        QuaternionValue rotation,
        IReadOnlyList<string> tags,
        EntityPropertyOverrides? overrides = null) => new(
        EntityId.From(Guid.Parse($"aaaaaaaa-0000-4000-8000-{suffix:000000000000}")),
        BlueprintId.From(Guid.Parse("bbbbbbbb-0000-4000-8000-000000000001")),
        new TransformValue(position, rotation, Vector3Value.One),
        tags,
        overrides);
}
