using Forge.Domain.Modules;

namespace Forge.Domain.WorldFeatures;

public sealed record WorldFeatureCatalog
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public IReadOnlyList<PlaceableWorldProfile> Placeables { get; }
    public IReadOnlyList<DoorWorldProfile> Doors { get; }
    public IReadOnlyList<TriggerWorldProfile> Triggers { get; }
    public IReadOnlyList<EncounterWorldProfile> Encounters { get; }
    public IReadOnlyList<VehicleRouteProfile> VehicleRoutes { get; }
    public IReadOnlyList<MerchantWorldProfile> Merchants { get; }
    public IReadOnlyList<CompanionWorldProfile> Companions { get; }
    public ScriptAutomationProfile Scripts { get; }

    public WorldFeatureCatalog(
        Guid id,
        int schemaVersion,
        string provenance,
        IReadOnlyList<PlaceableWorldProfile>? placeables = null,
        IReadOnlyList<DoorWorldProfile>? doors = null,
        IReadOnlyList<TriggerWorldProfile>? triggers = null,
        IReadOnlyList<EncounterWorldProfile>? encounters = null,
        IReadOnlyList<VehicleRouteProfile>? vehicleRoutes = null,
        IReadOnlyList<MerchantWorldProfile>? merchants = null,
        IReadOnlyList<CompanionWorldProfile>? companions = null,
        ScriptAutomationProfile? scripts = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("World-feature catalog ID is required.", nameof(id));
        if (schemaVersion != CurrentSchemaVersion)
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), "Unsupported world-feature catalog schema.");
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);

        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance.Trim();
        Placeables = Materialize(placeables, item => item.Id);
        Doors = Materialize(doors, item => item.Id);
        Triggers = Materialize(triggers, item => item.Id);
        Encounters = Materialize(encounters, item => item.Id);
        VehicleRoutes = Materialize(vehicleRoutes, item => item.Id);
        Merchants = Materialize(merchants, item => item.Id);
        Companions = Materialize(companions, item => item.Id);
        Scripts = scripts ?? ScriptAutomationProfile.CreateDefault(id, "migration:world-features-empty");
    }

    public static WorldFeatureCatalog CreateEmpty(ModuleId moduleId) =>
        new(moduleId.Value, CurrentSchemaVersion, "migration:world-features-empty");

    private static IReadOnlyList<T> Materialize<T>(IReadOnlyList<T>? source, Func<T, Guid> id)
    {
        var values = (source ?? []).ToArray();
        if (values.Select(id).Distinct().Count() != values.Length)
            throw new ArgumentException($"Duplicate {typeof(T).Name} IDs are not allowed.", nameof(source));
        return values.OrderBy(id).ToArray();
    }
}
