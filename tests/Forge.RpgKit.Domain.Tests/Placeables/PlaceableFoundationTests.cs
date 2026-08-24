using Forge.RpgKit.Application.Placeables;
using Forge.RpgKit.Domain.Placeables;

namespace Forge.RpgKit.Domain.Tests.Placeables;

public sealed class PlaceableFoundationTests
{
    [Fact]
    public void Creates_composable_interactable_blueprint_with_identity_visual_and_collision()
    {
        var definition = Interactable(); Assert.Empty(PlaceableRules.Validate(definition));
        var preview = PlaceableAuthoringService.Upsert(new(2, []), definition, 2, true); Assert.Empty(preview.State.Definitions);
        var applied = PlaceableAuthoringService.Upsert(new(2, []), definition, 2, false); Assert.Single(applied.State.Definitions); Assert.Equal(3, applied.State.Revision);
        Assert.Equal("Open", definition.Interaction!.Verbs[0].Label); Assert.True(definition.Collision.NavigationObstacle);
    }

    [Fact]
    public void Supports_optimized_static_props_and_rejects_unsafe_merge_collision()
    {
        var prop = Interactable() with { Interaction = null, StaticProp = new(true, true, true, true, false) };
        Assert.Empty(PlaceableRules.Validate(prop));
        var invalid = prop with { StaticProp = prop.StaticProp with { CollisionEnabled = true } };
        Assert.Contains(PlaceableRules.Validate(invalid), item => item.Code == "RPG_PLACEABLE_STATIC_MERGE_COLLISION");
    }

    [Fact]
    public void Applies_precise_parented_snapped_transform_with_surface_and_animation()
    {
        var definition = Interactable(); var state = new PlaceableProjectState(0, [definition]);
        var transform = new PlaceableTransform(new(1.24, 2.26, -0.24), new(0, 45, 0), new(1, 1, 1), new(0, 0.5, 0), 0.5, Guid.NewGuid(), true, "animation:lighthouse-rise");
        var result = PlaceableAuthoringService.Transform(state, definition.Id, transform, 0, false);
        Assert.Equal(new Vector3Value(1, 2.5, 0), result.Definition.Transform.Position); Assert.True(result.Definition.Transform.AlignToSurface); Assert.NotNull(result.Definition.Transform.ParentId);
        var nonFinite = definition with { Transform = transform with { Position = new(double.NaN, 0, 0) } };
        var selfParented = definition with { Transform = transform with { ParentId = definition.Id } };
        Assert.Contains(PlaceableRules.Validate(nonFinite), item => item.Code == "RPG_PLACEABLE_TRANSFORM_INVALID");
        Assert.Contains(PlaceableRules.Validate(selfParented), item => item.Code == "RPG_PLACEABLE_TRANSFORM_INVALID");
    }

    private static PlaceableDefinition Interactable() => new(Guid.NewGuid(), 1, "author:test", "lighthouse-console", [new("fr", "Console du phare", "Ouvrir")], new HashSet<string> { "lighthouse", "mechanism" }, "Module de démonstration", new(["asset:console"], ["asset:brass"], "asset:console-icon", "asset:console-portrait", ["vfx:glass-glow"]), new("box", new(1, 1.5, 0.5), true, true, "world"), new([new("open", "Open", "PlaceableUsed", "lighthouse-powered")], 2.5, 100, true, TimeSpan.FromSeconds(1), "server", "Ouvrir la console", new HashSet<string> { "lighthouse-powered" }), null, new(true, false, true, false, false, ["on_console_used"]), new(new(0, 0, 0), new(0, 0, 0), new(1, 1, 1), new(0, 0, 0), 0.25, null, true, null));
}
