using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.Areas;

public sealed class AreaFoundationProfileTests
{
    private static readonly AreaId AreaId = AreaId.From(Guid.Parse("a0280000-0000-4000-8000-000000000001"));
    private static readonly BlueprintId BlueprintId = BlueprintId.From(Guid.Parse("a0280000-0000-4000-8000-000000000002"));
    private static readonly EntityId FirstEntityId = EntityId.From(Guid.Parse("a0280000-0000-4000-8000-000000000003"));
    private static readonly EntityId SecondEntityId = EntityId.From(Guid.Parse("a0280000-0000-4000-8000-000000000004"));

    [Fact]
    public void Legacy_default_is_stable_and_valid()
    {
        var first = AreaFoundationProfile.CreateDefault(AreaId, AreaKind.Exterior);
        var second = AreaFoundationProfile.CreateDefault(AreaId, AreaKind.Exterior);
        var area = new AreaDefinition(AreaId, "r028", "R028", [], AreaKind.Exterior);

        Assert.Equal(first, second);
        Assert.Equal("heightmap", first.Creation.TerrainProfile);
        Assert.Empty(AreaFoundationValidator.Validate(area, first));
    }

    [Fact]
    public void Modular_kit_validates_rotation_mirror_variants_groups_brushes_and_preview()
    {
        var area = Area();
        var firstDefinition = Definition(Guid.Parse("a0280000-0000-4000-8000-000000000010"), "floor-a", "door",
            ModularTileRotationMode.Orthogonal, allowMirrorX: true);
        var secondDefinition = Definition(Guid.Parse("a0280000-0000-4000-8000-000000000020"), "floor-b", "frame",
            ModularTileRotationMode.Free, allowMirrorX: false);
        var groupId = Guid.Parse("a0280000-0000-4000-8000-000000000030");
        var firstPlacement = new ModularTilePlacement(Guid.Parse("a0280000-0000-4000-8000-000000000031"),
            firstDefinition.Id, FirstEntityId, 0, 0, 90, ModularTileMirrorMode.X, "damaged", groupId);
        var secondPlacement = new ModularTilePlacement(Guid.Parse("a0280000-0000-4000-8000-000000000032"),
            secondDefinition.Id, SecondEntityId, 1, 0, 12.5f, ModularTileMirrorMode.None, string.Empty, groupId);
        var kit = new ModularTileKitProfile(Guid.Parse("a0280000-0000-4000-8000-000000000040"), 1, "test", 2,
            [firstDefinition, secondDefinition],
            [new(Guid.Parse("a0280000-0000-4000-8000-000000000041"), "door", "frame", true)],
            [firstPlacement, secondPlacement],
            [new(groupId, "Room", [firstPlacement.Id, secondPlacement.Id])],
            [new(Guid.Parse("a0280000-0000-4000-8000-000000000042"), "Floor brush",
                [firstDefinition.Id, secondDefinition.Id], 7)]);
        var profile = new AreaFoundationProfile(Guid.Parse("a0280000-0000-4000-8000-000000000050"), 1,
            "test", AreaCreationSettings.CreateDefault(AreaKind.Interior), kit,
            AreaLifecycleBindingProfile.Optional,
            [new(Guid.Parse("a0280000-0000-4000-8000-000000000051"), "Room", "gameplay",
                new(-1, 0, -1), new(1, 2, 1), "test")],
            [new(Guid.Parse("a0280000-0000-4000-8000-000000000052"), BlueprintId.Value, "blueprint",
                "blueprints/floor.json", true)]);

        Assert.Empty(AreaFoundationValidator.Validate(area, profile));
        Assert.Single(profile.Volumes);
        Assert.Single(profile.Dependencies);
        var preview = ModularTileConnectionPreviewService.Preview(kit, firstPlacement.Id);
        Assert.Equal(1, preview.CompatibleSocketCount);
        Assert.Equal(0, preview.OpenSocketCount);
    }

    [Fact]
    public void Invalid_rotation_and_unsafe_required_lifecycle_binding_are_localized()
    {
        var area = Area();
        var definition = Definition(Guid.Parse("a0280000-0000-4000-8000-000000000060"), "floor", "door",
            ModularTileRotationMode.Orthogonal, allowMirrorX: false);
        var placement = new ModularTilePlacement(Guid.Parse("a0280000-0000-4000-8000-000000000061"),
            definition.Id, FirstEntityId, 0, 0, 13, ModularTileMirrorMode.X, "missing", null);
        var kit = new ModularTileKitProfile(Guid.Parse("a0280000-0000-4000-8000-000000000062"), 1, "test", 1,
            [definition], [], [placement], [], []);
        var lifecycle = new AreaLifecycleBindingProfile(
            new(Guid.Parse("a0280000-0000-4000-8000-000000000063"), "area.entered", "../escape.csx",
                "1.0.0", true, "test"), null);
        var profile = new AreaFoundationProfile(Guid.Parse("a0280000-0000-4000-8000-000000000064"), 1,
            "test", AreaCreationSettings.CreateDefault(AreaKind.Interior), kit, lifecycle, [], []);

        var diagnostics = AreaFoundationValidator.Validate(area, profile);

        Assert.Contains(diagnostics, item => item.Code == AreaFoundationValidator.TileCode &&
                                             item.Path.Contains(placement.Id.ToString("D"), StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == AreaFoundationValidator.LifecycleCode &&
                                             item.Path.EndsWith("lifecycle/entered", StringComparison.Ordinal));
    }

    private static AreaDefinition Area() => new(AreaId, "r028", "R028",
        [new(FirstEntityId, BlueprintId, TransformValue.Identity),
         new(SecondEntityId, BlueprintId, TransformValue.Identity)]);

    private static ModularTileDefinition Definition(Guid id, string key, string socketType,
        ModularTileRotationMode rotationMode, bool allowMirrorX)
    {
        var socket = new ModularTileSocket(Derived(id, 0x70),
            "edge", socketType, TransformValue.Identity, ModularTileSocketDirection.East);
        var variant = new ModularTileVariant(Derived(id, 0x71),
            "damaged", "damaged", BlueprintId);
        return new(id, key, BlueprintId, rotationMode, allowMirrorX, false, [socket], [variant]);
    }

    private static Guid Derived(Guid source, byte discriminator)
    {
        var input = source.ToByteArray().Concat([discriminator]).ToArray();
        return new Guid(System.Security.Cryptography.SHA256.HashData(input)[..16]);
    }
}
