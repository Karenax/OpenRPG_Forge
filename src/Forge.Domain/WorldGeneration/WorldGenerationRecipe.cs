using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Forge.Domain.WorldGeneration;

public enum WorldGenerationStage
{
    Terrain = 0,
    Hydrology = 1,
    Biomes = 2,
    Roads = 3,
    Settlements = 4,
    Scatter = 5,
    Gameplay = 6,
    Navigation = 7,
    Validation = 8,
}

public enum WorldGenerationBackend
{
    Auto = 0,
    Terrain3D = 1,
    PcgGodot = 2,
    GodotNative = 3,
}

public sealed record WorldGenerationBounds(
    double WidthMeters,
    double DepthMeters,
    double MaxElevationMeters);

public sealed record TerrainGenerationRecipe(
    string Archetype,
    double Roughness,
    int ErosionPasses,
    bool AdaptToStructures);

public sealed record RiverGenerationRecipe(
    Guid Id,
    string Name,
    double WidthMeters,
    double DepthMeters,
    string FlowRule);

public sealed record BiomeGenerationRecipe(
    Guid Id,
    string BiomeKey,
    string RegionRule,
    double Density,
    IReadOnlyList<string> AssetTags);

public sealed record RoadGenerationRecipe(
    Guid Id,
    string Kind,
    string FromRule,
    string ToRule,
    double WidthMeters);

public sealed record SettlementGenerationRecipe(
    Guid Id,
    string Kind,
    string Style,
    string PlacementRule,
    int BuildingCount,
    bool Walled,
    IReadOnlyList<string> RequiredLandmarks);

public sealed record ScatterGenerationRecipe(
    Guid Id,
    string AssetTag,
    string RegionRule,
    double Density,
    double MinSpacingMeters);

public sealed record GameplayGenerationRecipe(
    IReadOnlyList<string> EncounterTables,
    IReadOnlyList<string> WildlifeTables,
    IReadOnlyList<string> LootTables,
    bool GenerateQuestAnchors);

public sealed record NavigationGenerationRecipe(
    bool BakeNavigation,
    bool ValidateReachability,
    double AgentRadiusMeters);

public sealed record WorldGenerationQualityProfile(
    int StreamingCellSizeMeters,
    int MaxObjectsPerCell,
    bool GenerateLod,
    bool RunAutomatedPlaytest);

public sealed record WorldGenerationValidationIssue(string Code, string Message, string Path);

public sealed record WorldGenerationRecipe
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public Guid AreaId { get; }
    public string Title { get; }
    public int Seed { get; }
    public WorldGenerationBounds Bounds { get; }
    public TerrainGenerationRecipe Terrain { get; }
    public IReadOnlyList<RiverGenerationRecipe> Rivers { get; }
    public IReadOnlyList<BiomeGenerationRecipe> Biomes { get; }
    public IReadOnlyList<RoadGenerationRecipe> Roads { get; }
    public IReadOnlyList<SettlementGenerationRecipe> Settlements { get; }
    public IReadOnlyList<ScatterGenerationRecipe> Scatter { get; }
    public GameplayGenerationRecipe Gameplay { get; }
    public NavigationGenerationRecipe Navigation { get; }
    public WorldGenerationQualityProfile Quality { get; }
    public IReadOnlyDictionary<WorldGenerationStage, WorldGenerationBackend> Backends { get; }
    public bool ApprovedForGeneration { get; }
    public string Provenance { get; }

    public WorldGenerationRecipe(
        Guid id,
        int schemaVersion,
        Guid areaId,
        string title,
        int seed,
        WorldGenerationBounds bounds,
        TerrainGenerationRecipe terrain,
        IReadOnlyList<RiverGenerationRecipe>? rivers,
        IReadOnlyList<BiomeGenerationRecipe>? biomes,
        IReadOnlyList<RoadGenerationRecipe>? roads,
        IReadOnlyList<SettlementGenerationRecipe>? settlements,
        IReadOnlyList<ScatterGenerationRecipe>? scatter,
        GameplayGenerationRecipe gameplay,
        NavigationGenerationRecipe navigation,
        WorldGenerationQualityProfile quality,
        IReadOnlyDictionary<WorldGenerationStage, WorldGenerationBackend>? backends,
        bool approvedForGeneration,
        string provenance)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        AreaId = areaId;
        Title = (title ?? string.Empty).Trim();
        Seed = seed;
        Bounds = bounds ?? throw new ArgumentNullException(nameof(bounds));
        Terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        Rivers = (rivers ?? []).OrderBy(item => item.Id).ToArray();
        Biomes = (biomes ?? []).Select(Normalize).OrderBy(item => item.Id).ToArray();
        Roads = (roads ?? []).OrderBy(item => item.Id).ToArray();
        Settlements = (settlements ?? []).Select(Normalize).OrderBy(item => item.Id).ToArray();
        Scatter = (scatter ?? []).OrderBy(item => item.Id).ToArray();
        Gameplay = Normalize(gameplay ?? throw new ArgumentNullException(nameof(gameplay)));
        Navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        Quality = quality ?? throw new ArgumentNullException(nameof(quality));
        Backends = new Dictionary<WorldGenerationStage, WorldGenerationBackend>(backends ??
            new Dictionary<WorldGenerationStage, WorldGenerationBackend>());
        ApprovedForGeneration = approvedForGeneration;
        Provenance = (provenance ?? string.Empty).Trim();
    }

    public WorldGenerationBackend BackendFor(WorldGenerationStage stage) =>
        Backends.TryGetValue(stage, out var backend) ? backend : WorldGenerationBackend.Auto;

    public WorldGenerationRecipe WithApproval(bool approved, string provenance) =>
        new(Id, SchemaVersion, AreaId, Title, Seed, Bounds, Terrain, Rivers, Biomes, Roads, Settlements, Scatter,
            Gameplay, Navigation, Quality, Backends, approved, provenance);

    public string Fingerprint()
    {
        var value = new StringBuilder()
            .Append(Id).Append('|').Append(SchemaVersion).Append('|').Append(AreaId).Append('|')
            .Append(Title).Append('|').Append(Seed).Append('|').Append(Provenance).Append('|')
            .Append(ApprovedForGeneration).Append('|')
            .Append(Number(Bounds.WidthMeters)).Append('|').Append(Number(Bounds.DepthMeters)).Append('|')
            .Append(Number(Bounds.MaxElevationMeters)).Append('|').Append(Terrain.Archetype).Append('|')
            .Append(Number(Terrain.Roughness)).Append('|').Append(Terrain.ErosionPasses).Append('|')
            .Append(Terrain.AdaptToStructures).Append('|');

        foreach (var backend in Backends.OrderBy(item => item.Key))
            value.Append("B|").Append(backend.Key).Append('|').Append(backend.Value).Append('|');
        foreach (var river in Rivers)
            value.Append("R|").Append(river.Id).Append('|').Append(river.Name).Append('|')
                .Append(Number(river.WidthMeters)).Append('|').Append(Number(river.DepthMeters)).Append('|')
                .Append(river.FlowRule).Append('|');
        foreach (var biome in Biomes)
            value.Append("M|").Append(biome.Id).Append('|').Append(biome.BiomeKey).Append('|')
                .Append(biome.RegionRule).Append('|').Append(Number(biome.Density)).Append('|')
                .Append(string.Join(',', biome.AssetTags)).Append('|');
        foreach (var road in Roads)
            value.Append("O|").Append(road.Id).Append('|').Append(road.Kind).Append('|')
                .Append(road.FromRule).Append('|').Append(road.ToRule).Append('|')
                .Append(Number(road.WidthMeters)).Append('|');
        foreach (var settlement in Settlements)
            value.Append("S|").Append(settlement.Id).Append('|').Append(settlement.Kind).Append('|')
                .Append(settlement.Style).Append('|').Append(settlement.PlacementRule).Append('|')
                .Append(settlement.BuildingCount).Append('|').Append(settlement.Walled).Append('|')
                .Append(string.Join(',', settlement.RequiredLandmarks)).Append('|');
        foreach (var item in Scatter)
            value.Append("C|").Append(item.Id).Append('|').Append(item.AssetTag).Append('|')
                .Append(item.RegionRule).Append('|').Append(Number(item.Density)).Append('|')
                .Append(Number(item.MinSpacingMeters)).Append('|');

        value.Append("G|").Append(string.Join(',', Gameplay.EncounterTables)).Append('|')
            .Append(string.Join(',', Gameplay.WildlifeTables)).Append('|')
            .Append(string.Join(',', Gameplay.LootTables)).Append('|').Append(Gameplay.GenerateQuestAnchors).Append('|')
            .Append("N|").Append(Navigation.BakeNavigation).Append('|').Append(Navigation.ValidateReachability)
            .Append('|').Append(Number(Navigation.AgentRadiusMeters)).Append('|')
            .Append("Q|").Append(Quality.StreamingCellSizeMeters).Append('|').Append(Quality.MaxObjectsPerCell)
            .Append('|').Append(Quality.GenerateLod).Append('|').Append(Quality.RunAutomatedPlaytest);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToString()))).ToLowerInvariant();
    }

    public static WorldGenerationRecipe CreateDefault(Guid areaId, string title, int seed = 1) => new(
        Guid.NewGuid(),
        CurrentSchemaVersion,
        areaId,
        title,
        seed,
        new WorldGenerationBounds(2048, 2048, 320),
        new TerrainGenerationRecipe("temperate-valley", 0.45, 2, true),
        [],
        [new BiomeGenerationRecipe(Guid.NewGuid(), "temperate-forest", "all", 0.55, ["tree", "rock", "ground-cover"])],
        [],
        [],
        [new ScatterGenerationRecipe(Guid.NewGuid(), "environment-props", "all", 0.2, 3)],
        new GameplayGenerationRecipe([], [], [], true),
        new NavigationGenerationRecipe(true, true, 0.5),
        new WorldGenerationQualityProfile(256, 2000, true, true),
        DefaultBackends(),
        false,
        "openrpg-default:world-generation-v1");

    public static IReadOnlyDictionary<WorldGenerationStage, WorldGenerationBackend> DefaultBackends() =>
        new Dictionary<WorldGenerationStage, WorldGenerationBackend>
        {
            [WorldGenerationStage.Terrain] = WorldGenerationBackend.Terrain3D,
            [WorldGenerationStage.Hydrology] = WorldGenerationBackend.Terrain3D,
            [WorldGenerationStage.Biomes] = WorldGenerationBackend.PcgGodot,
            [WorldGenerationStage.Roads] = WorldGenerationBackend.PcgGodot,
            [WorldGenerationStage.Settlements] = WorldGenerationBackend.PcgGodot,
            [WorldGenerationStage.Scatter] = WorldGenerationBackend.PcgGodot,
            [WorldGenerationStage.Gameplay] = WorldGenerationBackend.GodotNative,
            [WorldGenerationStage.Navigation] = WorldGenerationBackend.GodotNative,
            [WorldGenerationStage.Validation] = WorldGenerationBackend.GodotNative,
        };

    private static BiomeGenerationRecipe Normalize(BiomeGenerationRecipe value) =>
        value with { AssetTags = Normalize(value.AssetTags) };

    private static SettlementGenerationRecipe Normalize(SettlementGenerationRecipe value) =>
        value with { RequiredLandmarks = Normalize(value.RequiredLandmarks) };

    private static GameplayGenerationRecipe Normalize(GameplayGenerationRecipe value) =>
        value with
        {
            EncounterTables = Normalize(value.EncounterTables),
            WildlifeTables = Normalize(value.WildlifeTables),
            LootTables = Normalize(value.LootTables),
        };

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string>? values) =>
        (values ?? []).Select(item => (item ?? string.Empty).Trim()).Where(item => item.Length > 0)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}

public static class WorldGenerationRecipeValidator
{
    public static IReadOnlyList<WorldGenerationValidationIssue> Validate(WorldGenerationRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var issues = new List<WorldGenerationValidationIssue>();
        if (recipe.Id == Guid.Empty)
            issues.Add(new("world_generation.recipe_id_required", "Recipe ID cannot be empty.", "world-generation/id"));
        if (recipe.AreaId == Guid.Empty)
            issues.Add(new("world_generation.area_id_required", "Target area ID cannot be empty.", "world-generation/areaId"));
        if (recipe.SchemaVersion != WorldGenerationRecipe.CurrentSchemaVersion)
            issues.Add(new("world_generation.schema_unsupported", $"Recipe schema '{recipe.SchemaVersion}' is not supported.", "world-generation/schemaVersion"));
        if (string.IsNullOrWhiteSpace(recipe.Title))
            issues.Add(new("world_generation.title_required", "Recipe title is required.", "world-generation/title"));
        if (recipe.Bounds.WidthMeters <= 0 || recipe.Bounds.DepthMeters <= 0 || recipe.Bounds.MaxElevationMeters <= 0)
            issues.Add(new("world_generation.bounds_invalid", "World bounds must be positive.", "world-generation/bounds"));
        if (recipe.Terrain.Roughness is < 0 or > 1 || recipe.Terrain.ErosionPasses < 0)
            issues.Add(new("world_generation.terrain_invalid", "Terrain roughness must be between 0 and 1 and erosion passes cannot be negative.", "world-generation/terrain"));
        if (recipe.Navigation.AgentRadiusMeters <= 0)
            issues.Add(new("world_generation.navigation_invalid", "Navigation agent radius must be positive.", "world-generation/navigation"));
        if (recipe.Quality.StreamingCellSizeMeters <= 0 || recipe.Quality.MaxObjectsPerCell <= 0)
            issues.Add(new("world_generation.quality_invalid", "Streaming cell size and object budget must be positive.", "world-generation/quality"));

        ValidateUnique(recipe.Rivers.Select(item => item.Id), "river", issues);
        ValidateUnique(recipe.Biomes.Select(item => item.Id), "biome", issues);
        ValidateUnique(recipe.Roads.Select(item => item.Id), "road", issues);
        ValidateUnique(recipe.Settlements.Select(item => item.Id), "settlement", issues);
        ValidateUnique(recipe.Scatter.Select(item => item.Id), "scatter", issues);

        foreach (var river in recipe.Rivers.Where(item => item.Id == Guid.Empty || item.WidthMeters <= 0 || item.DepthMeters <= 0))
            issues.Add(new("world_generation.river_invalid", $"River '{river.Name}' requires an ID, positive width and positive depth.", $"world-generation/rivers/{river.Id}"));
        foreach (var biome in recipe.Biomes.Where(item => item.Id == Guid.Empty || item.Density is < 0 or > 1 || string.IsNullOrWhiteSpace(item.BiomeKey)))
            issues.Add(new("world_generation.biome_invalid", $"Biome '{biome.BiomeKey}' requires an ID, key and density between 0 and 1.", $"world-generation/biomes/{biome.Id}"));
        foreach (var road in recipe.Roads.Where(item => item.Id == Guid.Empty || item.WidthMeters <= 0))
            issues.Add(new("world_generation.road_invalid", $"Road '{road.Kind}' requires an ID and positive width.", $"world-generation/roads/{road.Id}"));
        foreach (var settlement in recipe.Settlements.Where(item => item.Id == Guid.Empty || item.BuildingCount < 0))
            issues.Add(new("world_generation.settlement_invalid", $"Settlement '{settlement.Kind}' requires an ID and non-negative building count.", $"world-generation/settlements/{settlement.Id}"));
        foreach (var item in recipe.Scatter.Where(item => item.Id == Guid.Empty || item.Density is < 0 or > 1 || item.MinSpacingMeters < 0))
            issues.Add(new("world_generation.scatter_invalid", $"Scatter rule '{item.AssetTag}' requires an ID, density between 0 and 1 and non-negative spacing.", $"world-generation/scatter/{item.Id}"));
        foreach (var backend in recipe.Backends)
        {
            if (!Enum.IsDefined(backend.Key) || !Enum.IsDefined(backend.Value))
                issues.Add(new("world_generation.backend_invalid", "Recipe contains an unknown generation stage or backend.", "world-generation/backends"));
        }
        return issues;
    }

    private static void ValidateUnique(
        IEnumerable<Guid> ids,
        string kind,
        ICollection<WorldGenerationValidationIssue> issues)
    {
        var materialized = ids.ToArray();
        if (materialized.Distinct().Count() != materialized.Length)
            issues.Add(new("world_generation.duplicate_id", $"Recipe contains duplicate {kind} IDs.", $"world-generation/{kind}s"));
    }
}
