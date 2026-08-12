using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.Areas;

public sealed class AreaTileEditingProfileTests
{
    private static readonly AreaId AreaId = AreaId.From(Guid.Parse("a0530000-0000-4000-8000-000000000001"));
    private static readonly BlueprintId BlueprintId = BlueprintId.From(Guid.Parse("a0530000-0000-4000-8000-000000000002"));
    private static readonly EntityId FirstEntityId = EntityId.From(Guid.Parse("a0530000-0000-4000-8000-000000000003"));
    private static readonly EntityId SecondEntityId = EntityId.From(Guid.Parse("a0530000-0000-4000-8000-000000000004"));

    [Fact]
    public void R053ProfileValidatesGroupsToolsAutoFitPropertiesResizeAndRuntimeFallback()
    {
        var (area, foundation, firstPlacement, _) = Fixture(manualLock: true);

        Assert.Empty(AreaFoundationValidator.Validate(area, foundation));
        var editing = foundation.TileKit.Editing!;
        Assert.Single(editing.Groups);
        Assert.Single(editing.Properties);
        Assert.True(editing.Groups[0].ConvertToEditableInstances);

        var optional = AreaTileEditingRuntimeResolver.Resolve(area.Id, null, required: false);
        var required = AreaTileEditingRuntimeResolver.Resolve(area.Id, null, required: true);
        var resolved = AreaTileEditingRuntimeResolver.Resolve(area.Id, editing, required: true);
        Assert.Empty(optional.Diagnostics);
        Assert.Equal(AreaTileEditingRuntimeResolver.RequiredMissingCode, Assert.Single(required.Diagnostics).Code);
        Assert.Equal(firstPlacement.Id, Assert.Single(editing.AutoFit.ManualLockedPlacementIds));
        Assert.Equal(1, resolved.Handle!.GroupCount);
    }

    [Fact]
    public void R053AutoFitIsStableReportsAmbiguityAndHonoursManualLock()
    {
        var (_, unlocked, firstPlacement, _) = Fixture(manualLock: false);
        var first = AreaTileAutoFitService.Preview(unlocked.TileKit, firstPlacement.Id);
        var second = AreaTileAutoFitService.Preview(unlocked.TileKit, firstPlacement.Id);

        Assert.Equal(first.SelectedDefinitionId, second.SelectedDefinitionId);
        Assert.True(first.Ambiguous);
        Assert.Equal(2, first.CompatibleDefinitionIds.Count);
        Assert.Contains(AreaTileAutoFitService.AmbiguousCode, first.Diagnostics);

        var (_, locked, lockedPlacement, _) = Fixture(manualLock: true);
        var manual = AreaTileAutoFitService.Preview(locked.TileKit, lockedPlacement.Id);
        Assert.True(manual.ManualLock);
        Assert.Equal(lockedPlacement.DefinitionId, manual.SelectedDefinitionId);
        Assert.Single(manual.CompatibleDefinitionIds);
    }

    [Fact]
    public void R053InvalidReferencesAndTilePropertiesReturnStableLocalizedDiagnostics()
    {
        var (area, foundation, _, _) = Fixture(manualLock: false);
        var editing = foundation.TileKit.Editing!;
        var invalid = new AreaTileEditingProfile(editing.Id, editing.SchemaVersion, editing.Provenance,
            editing.Groups, editing.Tools with { ReplacementDefinitionId = Guid.NewGuid() },
            editing.AutoFit with { ManualLockedPlacementIds = [Guid.NewGuid()] },
            [editing.Properties[0] with { SemanticState = string.Empty, Light = new(true, string.Empty, -1, -1) }],
            editing.Resize with { Anchor = "Outside", LossRepositions = [new(Guid.NewGuid(), 0, 0)] });
        var invalidKit = new ModularTileKitProfile(foundation.TileKit.Id, 1, "test", 1,
            foundation.TileKit.Definitions, foundation.TileKit.ConnectionRules, foundation.TileKit.Placements,
            foundation.TileKit.Groups, foundation.TileKit.Brushes, invalid);

        var diagnostics = AreaTileEditingValidator.Validate(area, invalidKit, invalid);

        Assert.Contains(diagnostics, item => item.Code == AreaTileEditingValidator.ToolCode &&
            item.Path.EndsWith("tools", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == AreaTileEditingValidator.ReferenceCode &&
            item.Path.Contains("manualLockedPlacementIds", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == AreaTileEditingValidator.PropertyCode &&
            item.Path.Contains(editing.Properties[0].Id.ToString("D"), StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == AreaTileEditingValidator.ResizeCode &&
            item.Path.EndsWith("resize", StringComparison.Ordinal));
    }

    private static (AreaDefinition Area, AreaFoundationProfile Foundation, ModularTilePlacement First,
        ModularTilePlacement Second) Fixture(bool manualLock)
    {
        var firstDefinition = Definition(Guid.Parse("a0530000-0000-4000-8000-000000000010"), "room-a",
            ModularTileSocketDirection.East);
        var alternateDefinition = Definition(Guid.Parse("a0530000-0000-4000-8000-000000000011"), "room-b",
            ModularTileSocketDirection.East);
        var neighbourDefinition = Definition(Guid.Parse("a0530000-0000-4000-8000-000000000012"), "corridor",
            ModularTileSocketDirection.West);
        var groupId = Guid.Parse("a0530000-0000-4000-8000-000000000020");
        var firstPlacement = new ModularTilePlacement(Guid.Parse("a0530000-0000-4000-8000-000000000021"),
            firstDefinition.Id, FirstEntityId, 0, 0, 0, ModularTileMirrorMode.None, string.Empty, groupId);
        var secondPlacement = new ModularTilePlacement(Guid.Parse("a0530000-0000-4000-8000-000000000022"),
            neighbourDefinition.Id, SecondEntityId, 1, 0, 0, ModularTileMirrorMode.None, string.Empty, groupId);
        var group = new ModularTileGroup(groupId, "Room prefab", [firstPlacement.Id, secondPlacement.Id]);
        var editing = new AreaTileEditingProfile(Guid.Parse("a0530000-0000-4000-8000-000000000030"), 1,
            "test:r053", [new(Guid.Parse("a0530000-0000-4000-8000-000000000031"), groupId,
                [new("width", "int", "2")], new(2, 1, 3),
                [new(Guid.Parse("a0530000-0000-4000-8000-000000000032"), "door", "edge", 1, 0,
                    ModularTileSocketDirection.East)],
                [new("default", [firstDefinition.Id, neighbourDefinition.Id])], true)],
            new(AreaTileEditOperationKind.Replace, alternateDefinition.Id, "crypt-remap", firstDefinition.Id,
                "room-a", true),
            new(2026, manualLock ? [firstPlacement.Id] : [], true),
            [new(Guid.Parse("a0530000-0000-4000-8000-000000000033"), firstPlacement.Id, "lit",
                "animations/torch-loop", true, new Dictionary<string, string> { ["floor"] = "stone-wet" },
                new(true, "#ff9858", 2, 8), true, true, ["door"],
                new Dictionary<string, string> { ["state.open"] = "false" })],
            new(1, 1, 512, 512, "Center", true, [new(secondPlacement.Id, 2, 0)]));
        var kit = new ModularTileKitProfile(Guid.Parse("a0530000-0000-4000-8000-000000000040"), 1,
            "test:r053", 1, [firstDefinition, alternateDefinition, neighbourDefinition], [],
            [firstPlacement, secondPlacement], [group], [], editing);
        var foundation = new AreaFoundationProfile(Guid.Parse("a0530000-0000-4000-8000-000000000050"), 1,
            "test:r053", AreaCreationSettings.CreateDefault(AreaKind.Interior), kit,
            AreaLifecycleBindingProfile.Optional, [], [], true);
        var area = new AreaDefinition(AreaId, "r053", "R053",
            [new(FirstEntityId, BlueprintId, TransformValue.Identity),
             new(SecondEntityId, BlueprintId, TransformValue.Identity)], foundation: foundation);
        return (area, foundation, firstPlacement, secondPlacement);
    }

    private static ModularTileDefinition Definition(Guid id, string key, ModularTileSocketDirection direction) =>
        new(id, key, BlueprintId, ModularTileRotationMode.Orthogonal, true, true,
            [new(Derived(id), "edge", "edge", TransformValue.Identity, direction)], []);

    private static Guid Derived(Guid source) =>
        new(System.Security.Cryptography.SHA256.HashData(source.ToByteArray())[..16]);
}
