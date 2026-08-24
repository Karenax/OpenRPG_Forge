namespace Forge.Domain.WorldFeatures;

public enum PlaceableDamageEventsAuthority
{
    None,
    Direct,
    Composition,
    DualCanonical,
    Divergent,
}

public sealed record PlaceableDamageEventsCanonicalResolution(
    bool Available,
    PlaceableDamageEventsProfile? Profile,
    PlaceableDamageEventsAuthority Authority,
    string Provenance,
    string? DiagnosticCode);

public static class PlaceableDamageEventsResolver
{
    public const string DivergenceDiagnosticCode = "WORLD_PLACEABLE_DAMAGE_EVENTS_AUTHORITY_DIVERGED";
    public const string CatalogDivergenceDiagnosticCode = "world.placeable.damage_events_authority_diverged";

    public static PlaceableDamageEventsCanonicalResolution Resolve(PlaceableWorldProfile placeable)
    {
        ArgumentNullException.ThrowIfNull(placeable);
        var direct = placeable.DamageEvents;
        var composition = placeable.CompositionEvents?.DamageEvents;
        if (direct is not null && composition is not null)
        {
            return direct == composition
                ? new(true, direct, PlaceableDamageEventsAuthority.DualCanonical, placeable.Provenance, null)
                : new(false, null, PlaceableDamageEventsAuthority.Divergent, placeable.Provenance,
                    DivergenceDiagnosticCode);
        }

        if (direct is not null)
            return new(true, direct, PlaceableDamageEventsAuthority.Direct, placeable.Provenance, null);
        if (composition is not null)
            return new(true, composition, PlaceableDamageEventsAuthority.Composition,
                placeable.CompositionEvents!.Provenance, null);
        return new(false, null, PlaceableDamageEventsAuthority.None, placeable.Provenance, null);
    }
}
