namespace Forge.RpgKit.Domain.Placeables;

public sealed record Vector3Value(double X, double Y, double Z);
public sealed record LocalizedPlaceableText(string Locale, string Name, string Prompt);
public sealed record PlaceableVisualDefinition(IReadOnlyList<string> Meshes, IReadOnlyList<string> Materials, string IconAssetId, string PortraitAssetId, IReadOnlyList<string> VisualEffects);
public sealed record CollisionDefinition(string Shape, Vector3Value Size, bool Enabled, bool NavigationObstacle, string PhysicsLayer);
public sealed record InteractionVerb(string Id, string Label, string EventName, string Condition);
public sealed record InteractableComponent(IReadOnlyList<InteractionVerb> Verbs, double MaximumDistance, double MaximumAngleDegrees, bool RequiresFocus, TimeSpan Duration, string Authority, string Prompt, IReadOnlySet<string> Conditions);
public sealed record StaticPropComponent(bool Batched, bool Instanced, bool Occluder, bool MergeEligible, bool CollisionEnabled);
public sealed record PlaceableTransform(Vector3Value Position, Vector3Value RotationDegrees, Vector3Value Scale, Vector3Value Pivot, double SnapIncrement, Guid? ParentId, bool AlignToSurface, string? TransformAnimationId);
public sealed record PlaceableComponents(bool HasHealth, bool HasContainer, bool HasLock, bool HasTrap, bool HasPhysics, IReadOnlyList<string> ScriptBindings);
public sealed record PlaceableDefinition(Guid Id, int SchemaVersion, string Provenance, string Slug, IReadOnlyList<LocalizedPlaceableText> Texts, IReadOnlySet<string> Tags, string AuthorNotes, PlaceableVisualDefinition Visual, CollisionDefinition Collision, InteractableComponent? Interaction, StaticPropComponent? StaticProp, PlaceableComponents Components, PlaceableTransform Transform);
public sealed record PlaceableDiagnostic(string Code, string Path, string Message);

public static class PlaceableRules
{
    public static IReadOnlyList<PlaceableDiagnostic> Validate(PlaceableDefinition definition)
    {
        var diagnostics = new List<PlaceableDiagnostic>(); var root = $"placeables/{definition.Id}";
        if (definition.Id == Guid.Empty || definition.SchemaVersion != 1 || string.IsNullOrWhiteSpace(definition.Provenance) || string.IsNullOrWhiteSpace(definition.Slug)) diagnostics.Add(new("RPG_PLACEABLE_IDENTITY_INVALID", root, "Placeable identity is invalid."));
        if (definition.Texts.Count == 0 || definition.Texts.Any(text => string.IsNullOrWhiteSpace(text.Locale) || string.IsNullOrWhiteSpace(text.Name))) diagnostics.Add(new("RPG_PLACEABLE_LOCALIZATION_INVALID", $"{root}/texts", "Localized name is required."));
        if (definition.Visual.Meshes.Count == 0 || string.IsNullOrWhiteSpace(definition.Visual.IconAssetId)) diagnostics.Add(new("RPG_PLACEABLE_VISUAL_INVALID", $"{root}/visual", "Mesh and icon are required."));
        if (definition.Collision.Enabled && (definition.Collision.Size.X <= 0 || definition.Collision.Size.Y <= 0 || definition.Collision.Size.Z <= 0)) diagnostics.Add(new("RPG_PLACEABLE_COLLISION_INVALID", $"{root}/collision", "Enabled collision requires a positive size."));
        if (definition.Interaction is { } interaction && (interaction.Verbs.Count == 0 || interaction.MaximumDistance <= 0 || interaction.MaximumAngleDegrees is <= 0 or > 360 || string.IsNullOrWhiteSpace(interaction.Authority))) diagnostics.Add(new("RPG_PLACEABLE_INTERACTION_INVALID", $"{root}/interaction", "Interaction verbs, range, angle and authority are required."));
        if (definition.Interaction is null && definition.StaticProp is null) diagnostics.Add(new("RPG_PLACEABLE_ROLE_MISSING", $"{root}/components", "A placeable must be interactable or a static prop."));
        if (definition.StaticProp is { MergeEligible: true, CollisionEnabled: true }) diagnostics.Add(new("RPG_PLACEABLE_STATIC_MERGE_COLLISION", $"{root}/static", "Merged static decoration cannot keep per-instance collision."));
        var scale = definition.Transform.Scale; if (scale.X <= 0 || scale.Y <= 0 || scale.Z <= 0 || definition.Transform.SnapIncrement < 0) diagnostics.Add(new("RPG_PLACEABLE_TRANSFORM_INVALID", $"{root}/transform", "Scale and snap are invalid."));
        return diagnostics;
    }

    public static PlaceableTransform Snap(PlaceableTransform transform)
    {
        if (transform.SnapIncrement <= 0) return transform;
        double SnapValue(double value) => Math.Round(value / transform.SnapIncrement, MidpointRounding.AwayFromZero) * transform.SnapIncrement;
        return transform with { Position = new(SnapValue(transform.Position.X), SnapValue(transform.Position.Y), SnapValue(transform.Position.Z)) };
    }
}
