using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;

namespace Forge.Domain.Areas;

public enum ModularTileRotationMode
{
    Orthogonal,
    Octagonal,
    Free,
}

public enum ModularTileMirrorMode
{
    None,
    X,
    Z,
}

public enum ModularTileSocketDirection
{
    North,
    East,
    South,
    West,
    Up,
    Down,
    Omnidirectional,
}

public sealed record AreaCreationSettings(
    string TemplateKey,
    string KitKey,
    string TerrainProfile,
    string LightingProfile,
    string NavigationProfile,
    string StreamingProfile,
    string Biome,
    int Seed,
    int RuntimeBudgetMegabytes)
{
    public static AreaCreationSettings CreateDefault(AreaKind kind) => new(
        "blank",
        "forge-generic",
        kind == AreaKind.Exterior ? "heightmap" : "flat",
        kind == AreaKind.Exterior ? "daylight" : "interior-neutral",
        "walk-medium",
        "single-area",
        kind == AreaKind.Exterior ? "temperate" : "interior",
        2026,
        512);
}

public sealed record AreaDocumentDependency(
    Guid Id,
    Guid TargetId,
    string Kind,
    string DocumentPath,
    bool Required);

public sealed record AreaVolumeDefinition(
    Guid Id,
    string Name,
    string Kind,
    Vector3Value Minimum,
    Vector3Value Maximum,
    string Provenance);

public sealed record ModularTileSocket(
    Guid Id,
    string Name,
    string Type,
    TransformValue LocalTransform,
    ModularTileSocketDirection Direction);

public sealed record ModularTileVariant(
    Guid Id,
    string Key,
    string SemanticState,
    BlueprintId BlueprintId);

public sealed record ModularTileDefinition(
    Guid Id,
    string Key,
    BlueprintId BlueprintId,
    ModularTileRotationMode RotationMode,
    bool AllowMirrorX,
    bool AllowMirrorZ,
    IReadOnlyList<ModularTileSocket> Sockets,
    IReadOnlyList<ModularTileVariant> Variants);

public sealed record ModularTileConnectionRule(
    Guid Id,
    string FirstSocketType,
    string SecondSocketType,
    bool Bidirectional);

public sealed record ModularTilePlacement(
    Guid Id,
    Guid DefinitionId,
    EntityId EntityId,
    int GridX,
    int GridZ,
    float RotationDegrees,
    ModularTileMirrorMode Mirror,
    string VariantKey,
    Guid? GroupId);

public sealed record ModularTileGroup(Guid Id, string Name, IReadOnlyList<Guid> PlacementIds);

public sealed record ModularTileBrush(Guid Id, string Name, IReadOnlyList<Guid> DefinitionIds, int SeedOffset);

public sealed record ModularTileKitProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public float GridSizeMeters { get; }
    public IReadOnlyList<ModularTileDefinition> Definitions { get; }
    public IReadOnlyList<ModularTileConnectionRule> ConnectionRules { get; }
    public IReadOnlyList<ModularTilePlacement> Placements { get; }
    public IReadOnlyList<ModularTileGroup> Groups { get; }
    public IReadOnlyList<ModularTileBrush> Brushes { get; }
    public AreaTileEditingProfile? Editing { get; }

    public ModularTileKitProfile(
        Guid id,
        int schemaVersion,
        string provenance,
        float gridSizeMeters,
        IReadOnlyList<ModularTileDefinition>? definitions,
        IReadOnlyList<ModularTileConnectionRule>? connectionRules,
        IReadOnlyList<ModularTilePlacement>? placements,
        IReadOnlyList<ModularTileGroup>? groups,
        IReadOnlyList<ModularTileBrush>? brushes,
        AreaTileEditingProfile? editing = null)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance?.Trim() ?? string.Empty;
        GridSizeMeters = gridSizeMeters;
        Definitions = (definitions ?? []).OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
        ConnectionRules = (connectionRules ?? []).OrderBy(item => item.FirstSocketType, StringComparer.Ordinal)
            .ThenBy(item => item.SecondSocketType, StringComparer.Ordinal).ToArray();
        Placements = (placements ?? []).OrderBy(item => item.GridZ).ThenBy(item => item.GridX)
            .ThenBy(item => item.Id).ToArray();
        Groups = (groups ?? []).OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
        Brushes = (brushes ?? []).OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
        Editing = editing;
    }
}

public sealed record AreaLifecycleScriptBinding(
    Guid Id,
    string EventName,
    string ScriptPath,
    string ForgeScriptApiVersion,
    bool Required,
    string Provenance);

public sealed record AreaLifecycleBindingProfile(
    AreaLifecycleScriptBinding? Entered,
    AreaLifecycleScriptBinding? Exited)
{
    public static AreaLifecycleBindingProfile Optional => new(null, null);
}

public sealed record AreaFoundationProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public AreaCreationSettings Creation { get; }
    public ModularTileKitProfile TileKit { get; }
    public AreaLifecycleBindingProfile Lifecycle { get; }
    public IReadOnlyList<AreaVolumeDefinition> Volumes { get; }
    public IReadOnlyList<AreaDocumentDependency> Dependencies { get; }
    public bool RequireTileEditingRuntimeMetadata { get; }

    public AreaFoundationProfile(
        Guid id,
        int schemaVersion,
        string provenance,
        AreaCreationSettings creation,
        ModularTileKitProfile tileKit,
        AreaLifecycleBindingProfile lifecycle,
        IReadOnlyList<AreaVolumeDefinition>? volumes,
        IReadOnlyList<AreaDocumentDependency>? dependencies,
        bool requireTileEditingRuntimeMetadata = false)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance?.Trim() ?? string.Empty;
        Creation = creation ?? throw new ArgumentNullException(nameof(creation));
        TileKit = tileKit ?? throw new ArgumentNullException(nameof(tileKit));
        Lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        Volumes = (volumes ?? []).OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
        Dependencies = (dependencies ?? []).OrderBy(item => item.DocumentPath, StringComparer.Ordinal).ToArray();
        RequireTileEditingRuntimeMetadata = requireTileEditingRuntimeMetadata;
    }

    public static AreaFoundationProfile CreateDefault(AreaId areaId, AreaKind kind = AreaKind.Interior)
    {
        ArgumentNullException.ThrowIfNull(areaId);
        return new(
            StableGuid(areaId, "foundation"),
            CurrentSchemaVersion,
            "migration-default",
            AreaCreationSettings.CreateDefault(kind),
            new ModularTileKitProfile(
                StableGuid(areaId, "tile-kit"),
                ModularTileKitProfile.CurrentSchemaVersion,
                "migration-default",
                1f,
                [],
                [],
                [],
                [],
                []),
            AreaLifecycleBindingProfile.Optional,
            [],
            []);
    }

    internal static Guid StableGuid(AreaId areaId, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{areaId}:{discriminator}"));
        return new Guid(bytes[..16]);
    }
}
