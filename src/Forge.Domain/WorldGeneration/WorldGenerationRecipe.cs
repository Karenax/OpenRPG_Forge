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

public static class WorldGenerationBackendPolicy
{
    public static bool Supports(WorldGenerationBackend backend, WorldGenerationStage stage) =>
        backend switch
        {
            WorldGenerationBackend.Auto => true,
            WorldGenerationBackend.Terrain3D =>
                stage is WorldGenerationStage.Terrain or WorldGenerationStage.Hydrology,
            WorldGenerationBackend.PcgGodot =>
                stage is WorldGenerationStage.Biomes or WorldGenerationStage.Roads or
                    WorldGenerationStage.Settlements or WorldGenerationStage.Scatter,
            WorldGenerationBackend.GodotNative => Enum.IsDefined(stage),
            _ => false,
        };
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

public enum WorldGenerationVisualTier
{
    TechnicalPreview = 0,
    Production = 1,
}

public sealed record WorldGenerationArtDirectionProfile(
    WorldGenerationVisualTier VisualTier,
    Guid? SourceAreaId,
    bool InheritEnvironment,
    int MinimumVegetationStrata)
{
    public static WorldGenerationArtDirectionProfile ProductionDefault { get; } =
        new(WorldGenerationVisualTier.Production, null, true, 3);
}

public sealed record WorldGenerationRegenerationPolicy(
    bool PreserveManualEdits,
    IReadOnlyList<string> ProtectedNodePaths);

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
    public WorldGenerationRegenerationPolicy Regeneration { get; }
    public WorldGenerationArtDirectionProfile ArtDirection { get; }

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
        string provenance,
        WorldGenerationRegenerationPolicy? regeneration = null,
        WorldGenerationArtDirectionProfile? artDirection = null)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        AreaId = areaId;
        Title = (title ?? string.Empty).Trim();
        Seed = seed;
        Bounds = bounds ?? throw new ArgumentNullException(nameof(bounds));
        Terrain = Normalize(terrain ?? throw new ArgumentNullException(nameof(terrain)));
        Rivers = (rivers ?? []).Select(Normalize).OrderBy(item => item.Id).ToArray();
        Biomes = (biomes ?? []).Select(Normalize).OrderBy(item => item.Id).ToArray();
        Roads = (roads ?? []).Select(Normalize).OrderBy(item => item.Id).ToArray();
        Settlements = (settlements ?? []).Select(Normalize).OrderBy(item => item.Id).ToArray();
        Scatter = (scatter ?? []).Select(Normalize).OrderBy(item => item.Id).ToArray();
        Gameplay = Normalize(gameplay ?? throw new ArgumentNullException(nameof(gameplay)));
        Navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        Quality = quality ?? throw new ArgumentNullException(nameof(quality));
        Backends = new Dictionary<WorldGenerationStage, WorldGenerationBackend>(backends ??
            new Dictionary<WorldGenerationStage, WorldGenerationBackend>());
        ApprovedForGeneration = approvedForGeneration;
        Provenance = (provenance ?? string.Empty).Trim();
        Regeneration = Normalize(regeneration ?? new(true, []));
        ArtDirection = artDirection ?? WorldGenerationArtDirectionProfile.ProductionDefault;
    }

    public WorldGenerationBackend BackendFor(WorldGenerationStage stage) =>
        Backends.TryGetValue(stage, out var backend) ? backend : WorldGenerationBackend.Auto;

    public WorldGenerationRecipe WithApproval(bool approved, string provenance) =>
        new(Id, SchemaVersion, AreaId, Title, Seed, Bounds, Terrain, Rivers, Biomes, Roads, Settlements, Scatter,
            Gameplay, Navigation, Quality, Backends, approved, provenance, Regeneration, ArtDirection);

    public string Fingerprint()
    {
        var value = new StringBuilder();
        AppendFingerprint(value, "world-generation-recipe-v1");
        AppendFingerprint(value, Id.ToString("D"));
        AppendFingerprint(value, SchemaVersion.ToString(CultureInfo.InvariantCulture));
        AppendFingerprint(value, AreaId.ToString("D"));
        AppendFingerprint(value, Title);
        AppendFingerprint(value, Seed.ToString(CultureInfo.InvariantCulture));
        AppendFingerprint(value, Provenance);
        AppendFingerprint(value, ApprovedForGeneration.ToString(CultureInfo.InvariantCulture));
        AppendFingerprint(value, Number(Bounds.WidthMeters));
        AppendFingerprint(value, Number(Bounds.DepthMeters));
        AppendFingerprint(value, Number(Bounds.MaxElevationMeters));
        AppendFingerprint(value, Terrain.Archetype);
        AppendFingerprint(value, Number(Terrain.Roughness));
        AppendFingerprint(value, Terrain.ErosionPasses.ToString(CultureInfo.InvariantCulture));
        AppendFingerprint(value, Terrain.AdaptToStructures.ToString(CultureInfo.InvariantCulture));
        AppendFingerprint(value, Backends.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var backend in Backends.OrderBy(item => item.Key))
        {
            AppendFingerprint(value, "backend");
            AppendFingerprint(value, backend.Key.ToString());
            AppendFingerprint(value, backend.Value.ToString());
        }
        AppendFingerprint(value, Rivers.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var river in Rivers)
        {
            AppendFingerprint(value, "river");
            AppendFingerprint(value, river.Id.ToString("D"));
            AppendFingerprint(value, river.Name);
            AppendFingerprint(value, Number(river.WidthMeters));
            AppendFingerprint(value, Number(river.DepthMeters));
            AppendFingerprint(value, river.FlowRule);
        }
        AppendFingerprint(value, Biomes.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var biome in Biomes)
        {
            AppendFingerprint(value, "biome");
            AppendFingerprint(value, biome.Id.ToString("D"));
            AppendFingerprint(value, biome.BiomeKey);
            AppendFingerprint(value, biome.RegionRule);
            AppendFingerprint(value, Number(biome.Density));
            AppendFingerprint(value, biome.AssetTags.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var tag in biome.AssetTags) AppendFingerprint(value, tag);
        }
        AppendFingerprint(value, Roads.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var road in Roads)
        {
            AppendFingerprint(value, "road");
            AppendFingerprint(value, road.Id.ToString("D"));
            AppendFingerprint(value, road.Kind);
            AppendFingerprint(value, road.FromRule);
            AppendFingerprint(value, road.ToRule);
            AppendFingerprint(value, Number(road.WidthMeters));
        }
        AppendFingerprint(value, Settlements.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var settlement in Settlements)
        {
            AppendFingerprint(value, "settlement");
            AppendFingerprint(value, settlement.Id.ToString("D"));
            AppendFingerprint(value, settlement.Kind);
            AppendFingerprint(value, settlement.Style);
            AppendFingerprint(value, settlement.PlacementRule);
            AppendFingerprint(value, settlement.BuildingCount.ToString(CultureInfo.InvariantCulture));
            AppendFingerprint(value, settlement.Walled.ToString(CultureInfo.InvariantCulture));
            AppendFingerprint(value, settlement.RequiredLandmarks.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var landmark in settlement.RequiredLandmarks) AppendFingerprint(value, landmark);
        }
        AppendFingerprint(value, Scatter.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var item in Scatter)
        {
            AppendFingerprint(value, "scatter");
            AppendFingerprint(value, item.Id.ToString("D"));
            AppendFingerprint(value, item.AssetTag);
            AppendFingerprint(value, item.RegionRule);
            AppendFingerprint(value, Number(item.Density));
            AppendFingerprint(value, Number(item.MinSpacingMeters));
        }

        AppendFingerprint(value, Gameplay.EncounterTables.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var table in Gameplay.EncounterTables) AppendFingerprint(value, table);
        AppendFingerprint(value, Gameplay.WildlifeTables.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var table in Gameplay.WildlifeTables) AppendFingerprint(value, table);
        AppendFingerprint(value, Gameplay.LootTables.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var table in Gameplay.LootTables) AppendFingerprint(value, table);
        AppendFingerprint(value, Gameplay.GenerateQuestAnchors.ToString(CultureInfo.InvariantCulture));
        AppendFingerprint(value, Navigation.BakeNavigation.ToString(CultureInfo.InvariantCulture));
        AppendFingerprint(value, Navigation.ValidateReachability.ToString(CultureInfo.InvariantCulture));
        AppendFingerprint(value, Number(Navigation.AgentRadiusMeters));
        AppendFingerprint(value, Quality.StreamingCellSizeMeters.ToString(CultureInfo.InvariantCulture));
        AppendFingerprint(value, Quality.MaxObjectsPerCell.ToString(CultureInfo.InvariantCulture));
        AppendFingerprint(value, Quality.GenerateLod.ToString(CultureInfo.InvariantCulture));
        AppendFingerprint(value, Quality.RunAutomatedPlaytest.ToString(CultureInfo.InvariantCulture));
        AppendFingerprint(value, ArtDirection.VisualTier.ToString());
        AppendFingerprint(value, ArtDirection.SourceAreaId?.ToString("D") ?? string.Empty);
        AppendFingerprint(value, ArtDirection.InheritEnvironment.ToString(CultureInfo.InvariantCulture));
        AppendFingerprint(value, ArtDirection.MinimumVegetationStrata.ToString(CultureInfo.InvariantCulture));
        AppendFingerprint(value, Regeneration.PreserveManualEdits.ToString(CultureInfo.InvariantCulture));
        AppendFingerprint(value, Regeneration.ProtectedNodePaths.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var path in Regeneration.ProtectedNodePaths) AppendFingerprint(value, path);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToString()))).ToLowerInvariant();
    }

    public static WorldGenerationRecipe CreateDefault(Guid areaId, string title, int seed = 1)
    {
        var normalizedTitle = (title ?? string.Empty).Trim();
        var identityBuilder = new StringBuilder();
        AppendFingerprint(identityBuilder, areaId.ToString("D"));
        AppendFingerprint(identityBuilder, normalizedTitle);
        AppendFingerprint(identityBuilder, seed.ToString(CultureInfo.InvariantCulture));
        var identity = identityBuilder.ToString();
        return new(
            StableGuid(identity, "recipe"),
            CurrentSchemaVersion,
            areaId,
            normalizedTitle,
            seed,
            new WorldGenerationBounds(2048, 2048, 320),
            new TerrainGenerationRecipe("temperate-valley", 0.45, 2, true),
            [],
            [new BiomeGenerationRecipe(StableGuid(identity, "biome:temperate-forest"),
                "temperate-forest", "all", 0.55, ["tree", "rock", "ground-cover"])],
            [],
            [],
            [new ScatterGenerationRecipe(StableGuid(identity, "scatter:environment-props"),
                "environment-props", "all", 0.2, 3)],
            new GameplayGenerationRecipe([], [], [], true),
            new NavigationGenerationRecipe(true, true, 0.5),
            new WorldGenerationQualityProfile(256, 2000, true, true),
            DefaultBackends(),
            false,
            "openrpg-default:world-generation-v1",
            new(true, []),
            WorldGenerationArtDirectionProfile.ProductionDefault);
    }

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

    private static TerrainGenerationRecipe Normalize(TerrainGenerationRecipe value) =>
        value with { Archetype = (value.Archetype ?? string.Empty).Trim() };

    private static RiverGenerationRecipe Normalize(RiverGenerationRecipe value) =>
        value with
        {
            Name = (value.Name ?? string.Empty).Trim(),
            FlowRule = (value.FlowRule ?? string.Empty).Trim(),
        };

    private static BiomeGenerationRecipe Normalize(BiomeGenerationRecipe value) =>
        value with
        {
            BiomeKey = (value.BiomeKey ?? string.Empty).Trim(),
            RegionRule = (value.RegionRule ?? string.Empty).Trim(),
            AssetTags = Normalize(value.AssetTags),
        };

    private static RoadGenerationRecipe Normalize(RoadGenerationRecipe value) =>
        value with
        {
            Kind = (value.Kind ?? string.Empty).Trim(),
            FromRule = (value.FromRule ?? string.Empty).Trim(),
            ToRule = (value.ToRule ?? string.Empty).Trim(),
        };

    private static SettlementGenerationRecipe Normalize(SettlementGenerationRecipe value) =>
        value with
        {
            Kind = (value.Kind ?? string.Empty).Trim(),
            Style = (value.Style ?? string.Empty).Trim(),
            PlacementRule = (value.PlacementRule ?? string.Empty).Trim(),
            RequiredLandmarks = Normalize(value.RequiredLandmarks),
        };

    private static ScatterGenerationRecipe Normalize(ScatterGenerationRecipe value) =>
        value with
        {
            AssetTag = (value.AssetTag ?? string.Empty).Trim(),
            RegionRule = (value.RegionRule ?? string.Empty).Trim(),
        };

    private static GameplayGenerationRecipe Normalize(GameplayGenerationRecipe value) =>
        value with
        {
            EncounterTables = Normalize(value.EncounterTables),
            WildlifeTables = Normalize(value.WildlifeTables),
            LootTables = Normalize(value.LootTables),
        };

    private static WorldGenerationRegenerationPolicy Normalize(WorldGenerationRegenerationPolicy value) =>
        value with { ProtectedNodePaths = Normalize(value.ProtectedNodePaths) };

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string>? values) =>
        (values ?? []).Select(item => (item ?? string.Empty).Trim()).Where(item => item.Length > 0)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static void AppendFingerprint(StringBuilder target, string value) =>
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value).Append(';');

    private static Guid StableGuid(string identity, string discriminator)
    {
        var canonical = new StringBuilder();
        AppendFingerprint(canonical, "openrpg-world-generation");
        AppendFingerprint(canonical, identity);
        AppendFingerprint(canonical, discriminator);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0f) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3f) | 0x80);
        return new(guidBytes);
    }
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
        if (recipe.Title.Length > 160 || recipe.Provenance.Length is 0 or > 512)
            issues.Add(new("world_generation.text_invalid", "Recipe title or provenance exceeds its bounded contract.", "world-generation"));
        if (!FinitePositive(recipe.Bounds.WidthMeters, 32_768) ||
            !FinitePositive(recipe.Bounds.DepthMeters, 32_768) ||
            !FinitePositive(recipe.Bounds.MaxElevationMeters, 8_192))
            issues.Add(new("world_generation.bounds_invalid", "World bounds must be finite, positive and within supported limits.", "world-generation/bounds"));
        if (!double.IsFinite(recipe.Terrain.Roughness) || recipe.Terrain.Roughness is < 0 or > 1 ||
            recipe.Terrain.ErosionPasses is < 0 or > 64 || InvalidToken(recipe.Terrain.Archetype, 64))
            issues.Add(new("world_generation.terrain_invalid", "Terrain roughness must be between 0 and 1 and erosion passes cannot be negative.", "world-generation/terrain"));
        if (!FinitePositive(recipe.Navigation.AgentRadiusMeters, 128))
            issues.Add(new("world_generation.navigation_invalid", "Navigation agent radius must be positive.", "world-generation/navigation"));
        if (recipe.Quality.StreamingCellSizeMeters is <= 0 or > 8_192 ||
            recipe.Quality.MaxObjectsPerCell is <= 0 or > 1_000_000)
            issues.Add(new("world_generation.quality_invalid", "Streaming cell size and object budget must be positive.", "world-generation/quality"));
        if (!Enum.IsDefined(recipe.ArtDirection.VisualTier) ||
            recipe.ArtDirection.SourceAreaId == Guid.Empty ||
            recipe.ArtDirection.MinimumVegetationStrata is < 1 or > 8)
            issues.Add(new("world_generation.art_direction_invalid",
                "Art direction requires a supported visual tier, a non-empty optional source area and between 1 and 8 vegetation strata.",
                "world-generation/artDirection"));
        if (recipe.ArtDirection.SourceAreaId == recipe.AreaId)
            issues.Add(new("world_generation.art_direction_recursive",
                "The generated area cannot be its own art-direction source.",
                "world-generation/artDirection/sourceAreaId"));
        if (recipe.Rivers.Count > 128 || recipe.Biomes.Count > 128 || recipe.Roads.Count > 512 ||
            recipe.Settlements.Count > 256 || recipe.Scatter.Count > 512)
            issues.Add(new("world_generation.collection_limit",
                "World-generation collection limits were exceeded.", "world-generation"));

        ValidateUnique(recipe.Rivers.Select(item => item.Id), "river", issues);
        ValidateUnique(recipe.Biomes.Select(item => item.Id), "biome", issues);
        ValidateUnique(recipe.Roads.Select(item => item.Id), "road", issues);
        ValidateUnique(recipe.Settlements.Select(item => item.Id), "settlement", issues);
        ValidateUnique(recipe.Scatter.Select(item => item.Id), "scatter", issues);

        foreach (var river in recipe.Rivers.Where(item => item.Id == Guid.Empty ||
                     !FinitePositive(item.WidthMeters, 4_096) || !FinitePositive(item.DepthMeters, 4_096) ||
                     InvalidToken(item.Name, 120) || InvalidToken(item.FlowRule, 256)))
            issues.Add(new("world_generation.river_invalid", $"River '{river.Name}' requires an ID, positive width and positive depth.", $"world-generation/rivers/{river.Id}"));
        foreach (var biome in recipe.Biomes.Where(item => item.Id == Guid.Empty || !double.IsFinite(item.Density) ||
                     item.Density is < 0 or > 1 || InvalidToken(item.BiomeKey, 120) ||
                     InvalidToken(item.RegionRule, 256) || item.AssetTags.Count > 64 ||
                     item.AssetTags.Any(tag => InvalidToken(tag, 120))))
            issues.Add(new("world_generation.biome_invalid", $"Biome '{biome.BiomeKey}' requires an ID, key and density between 0 and 1.", $"world-generation/biomes/{biome.Id}"));
        foreach (var road in recipe.Roads.Where(item => item.Id == Guid.Empty ||
                     !FinitePositive(item.WidthMeters, 1_024) || InvalidToken(item.Kind, 120) ||
                     InvalidToken(item.FromRule, 256) || InvalidToken(item.ToRule, 256)))
            issues.Add(new("world_generation.road_invalid", $"Road '{road.Kind}' requires an ID and positive width.", $"world-generation/roads/{road.Id}"));
        foreach (var settlement in recipe.Settlements.Where(item => item.Id == Guid.Empty ||
                     item.BuildingCount is < 0 or > 100_000 || InvalidToken(item.Kind, 120) ||
                     InvalidToken(item.Style, 120) || InvalidToken(item.PlacementRule, 256) ||
                     item.RequiredLandmarks.Count > 128 ||
                     item.RequiredLandmarks.Any(landmark => InvalidToken(landmark, 120))))
            issues.Add(new("world_generation.settlement_invalid", $"Settlement '{settlement.Kind}' requires an ID and non-negative building count.", $"world-generation/settlements/{settlement.Id}"));
        foreach (var item in recipe.Scatter.Where(item => item.Id == Guid.Empty ||
                     !double.IsFinite(item.Density) || item.Density is < 0 or > 1 ||
                     !double.IsFinite(item.MinSpacingMeters) || item.MinSpacingMeters is < 0 or > 4_096 ||
                     InvalidToken(item.AssetTag, 120) || InvalidToken(item.RegionRule, 256)))
            issues.Add(new("world_generation.scatter_invalid", $"Scatter rule '{item.AssetTag}' requires an ID, density between 0 and 1 and non-negative spacing.", $"world-generation/scatter/{item.Id}"));
        if (recipe.Gameplay.EncounterTables.Count > 512 || recipe.Gameplay.WildlifeTables.Count > 512 ||
            recipe.Gameplay.LootTables.Count > 512 ||
            recipe.Gameplay.EncounterTables.Concat(recipe.Gameplay.WildlifeTables)
                .Concat(recipe.Gameplay.LootTables).Any(value => InvalidToken(value, 160)))
            issues.Add(new("world_generation.gameplay_invalid",
                "Gameplay generation tables exceed their bounded contract.", "world-generation/gameplay"));
        if (recipe.Regeneration.ProtectedNodePaths.Count > 128 ||
            recipe.Regeneration.ProtectedNodePaths.Any(path => !IsSafeRelativeNodePath(path)))
            issues.Add(new("world_generation.protected_path_invalid",
                "Protected generated-node paths must be bounded relative node paths without traversal.",
                "world-generation/regeneration/protectedNodePaths"));
        foreach (var backend in recipe.Backends)
        {
            if (!Enum.IsDefined(backend.Key) || !Enum.IsDefined(backend.Value))
                issues.Add(new("world_generation.backend_invalid", "Recipe contains an unknown generation stage or backend.", "world-generation/backends"));
            else if (!WorldGenerationBackendPolicy.Supports(backend.Value, backend.Key))
                issues.Add(new("world_generation.backend_stage_invalid",
                    $"Backend '{backend.Value}' cannot execute stage '{backend.Key}'.",
                    $"world-generation/backends/{backend.Key}"));
        }
        return issues;
    }

    private static bool FinitePositive(double value, double maximum) =>
        double.IsFinite(value) && value > 0 && value <= maximum;

    private static bool InvalidToken(string? value, int maximumLength) =>
        string.IsNullOrWhiteSpace(value) || value.Length > maximumLength || value.IndexOf('\0') >= 0;

    private static bool IsSafeRelativeNodePath(string value)
    {
        if (InvalidToken(value, 256) || value.StartsWith('/') || value.StartsWith('\\') || value.Contains(':'))
            return false;
        var segments = value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 && segments.All(segment => segment is not "." and not ".." &&
            segment.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.'));
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
