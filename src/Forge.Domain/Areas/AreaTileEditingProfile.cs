using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Domain.Areas;

public enum AreaTileEditOperationKind
{
    Erase,
    Replace,
    RemapKit,
    Fill,
    SelectByType,
}

public sealed record EditableTileGroupParameter(string Key, string Type, string Value);

public sealed record EditableTileGroupFootprint(int WidthCells, int DepthCells, float HeightMeters);

public sealed record EditableTileGroupSocket(
    Guid Id,
    string Name,
    string Type,
    int GridX,
    int GridZ,
    ModularTileSocketDirection Direction);

public sealed record EditableTileGroupVariant(string Key, IReadOnlyList<Guid> DefinitionIds);

public sealed record EditableTileGroupProfile(
    Guid Id,
    Guid GroupId,
    IReadOnlyList<EditableTileGroupParameter> Parameters,
    EditableTileGroupFootprint Footprint,
    IReadOnlyList<EditableTileGroupSocket> Sockets,
    IReadOnlyList<EditableTileGroupVariant> Variants,
    bool ConvertToEditableInstances);

public sealed record AreaTileToolProfile(
    AreaTileEditOperationKind Operation,
    Guid? ReplacementDefinitionId,
    string? ReplacementKitKey,
    Guid? FillDefinitionId,
    string SelectionType,
    bool PreserveAttachedObjects);

public sealed record AreaTileAutoFitProfile(
    int Seed,
    IReadOnlyList<Guid> ManualLockedPlacementIds,
    bool ReportAmbiguities);

public sealed record TileInstanceLightProfile(
    bool Enabled,
    string Color,
    float Intensity,
    float RangeMeters);

public sealed record TileInstancePropertyProfile(
    Guid Id,
    Guid PlacementId,
    string SemanticState,
    string Animation,
    bool LoopAnimation,
    IReadOnlyDictionary<string, string> MaterialOverrides,
    TileInstanceLightProfile Light,
    bool CollisionEnabled,
    bool NavigationEnabled,
    IReadOnlyList<string> SocketOverrides,
    IReadOnlyDictionary<string, string> Overrides);

public sealed record TileLossReposition(Guid PlacementId, int GridX, int GridZ);

public sealed record AreaTileResizeProfile(
    int MinimumWidth,
    int MinimumDepth,
    int MaximumWidth,
    int MaximumDepth,
    string Anchor,
    bool MigrateInstances,
    IReadOnlyList<TileLossReposition> LossRepositions);

public sealed record AreaTileEditingProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public IReadOnlyList<EditableTileGroupProfile> Groups { get; }
    public AreaTileToolProfile Tools { get; }
    public AreaTileAutoFitProfile AutoFit { get; }
    public IReadOnlyList<TileInstancePropertyProfile> Properties { get; }
    public AreaTileResizeProfile Resize { get; }

    public AreaTileEditingProfile(
        Guid id,
        int schemaVersion,
        string provenance,
        IReadOnlyList<EditableTileGroupProfile>? groups,
        AreaTileToolProfile tools,
        AreaTileAutoFitProfile autoFit,
        IReadOnlyList<TileInstancePropertyProfile>? properties,
        AreaTileResizeProfile resize)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance?.Trim() ?? string.Empty;
        Groups = (groups ?? []).OrderBy(item => item.Id).ToArray();
        Tools = tools ?? throw new ArgumentNullException(nameof(tools));
        AutoFit = autoFit ?? throw new ArgumentNullException(nameof(autoFit));
        Properties = (properties ?? []).OrderBy(item => item.PlacementId).ToArray();
        Resize = resize ?? throw new ArgumentNullException(nameof(resize));
    }

    public static AreaTileEditingProfile CreateDefault(AreaId areaId) => new(
        AreaFoundationProfile.StableGuid(areaId, "tile-editing"),
        CurrentSchemaVersion,
        "migration-default",
        [],
        new(AreaTileEditOperationKind.SelectByType, null, null, null, string.Empty, true),
        new(2026, [], true),
        [],
        new(1, 1, 4096, 4096, "Center", true, []));
}

public static class AreaTileEditingValidator
{
    public const string IdentityCode = "area.tile_editing.identity_invalid";
    public const string SchemaCode = "area.tile_editing.schema_unsupported";
    public const string GroupCode = "area.tile_editing.group_invalid";
    public const string ReferenceCode = "area.tile_editing.reference_missing";
    public const string ToolCode = "area.tile_editing.tool_invalid";
    public const string PropertyCode = "area.tile_editing.property_invalid";
    public const string ResizeCode = "area.tile_editing.resize_invalid";

    public static IReadOnlyList<DomainDiagnostic> Validate(
        AreaDefinition area,
        ModularTileKitProfile kit,
        AreaTileEditingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(kit);
        ArgumentNullException.ThrowIfNull(profile);
        var root = $"areas/{area.Id}/foundation/tileKit/editing";
        var diagnostics = new List<DomainDiagnostic>();

        if (profile.Id == Guid.Empty || string.IsNullOrWhiteSpace(profile.Provenance))
        {
            diagnostics.Add(new(IdentityCode, "Tile editing UUID and provenance are required.", root));
        }

        if (profile.SchemaVersion != AreaTileEditingProfile.CurrentSchemaVersion)
        {
            diagnostics.Add(new(SchemaCode, "Tile editing schema version is unsupported.", root));
        }

        var placementIds = kit.Placements.Select(item => item.Id).ToHashSet();
        var definitionIds = kit.Definitions.Select(item => item.Id).ToHashSet();
        var groupIds = kit.Groups.Select(item => item.Id).ToHashSet();
        var childIds = profile.Groups.Select(item => item.Id)
            .Concat(profile.Groups.SelectMany(item => item.Sockets).Select(item => item.Id))
            .Concat(profile.Properties.Select(item => item.Id)).ToArray();
        if (childIds.Any(item => item == Guid.Empty) || childIds.Distinct().Count() != childIds.Length)
        {
            diagnostics.Add(new(IdentityCode, "Tile editing child UUIDs must be non-empty and unique.", root));
        }

        foreach (var group in profile.Groups)
        {
            var path = $"{root}/groups/{group.Id:D}";
            if (!groupIds.Contains(group.GroupId) || group.Footprint.WidthCells <= 0 ||
                group.Footprint.DepthCells <= 0 || !float.IsFinite(group.Footprint.HeightMeters) ||
                group.Footprint.HeightMeters <= 0 ||
                group.Parameters.Any(item => string.IsNullOrWhiteSpace(item.Key) ||
                    item.Type is not ("string" or "int" or "float" or "bool")) ||
                group.Parameters.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() !=
                group.Parameters.Count ||
                group.Sockets.Any(item => string.IsNullOrWhiteSpace(item.Name) ||
                    string.IsNullOrWhiteSpace(item.Type)) ||
                group.Variants.Any(item => string.IsNullOrWhiteSpace(item.Key) ||
                    item.DefinitionIds.Count == 0 || item.DefinitionIds.Any(id => !definitionIds.Contains(id))))
            {
                diagnostics.Add(new(GroupCode,
                    "Editable groups require a source group, parameters, footprint, sockets and resolved variants.",
                    path));
            }
        }

        if (profile.Tools.ReplacementDefinitionId is { } replacement && !definitionIds.Contains(replacement) ||
            profile.Tools.FillDefinitionId is { } fill && !definitionIds.Contains(fill) ||
            profile.Tools.Operation == AreaTileEditOperationKind.RemapKit &&
            string.IsNullOrWhiteSpace(profile.Tools.ReplacementKitKey))
        {
            diagnostics.Add(new(ToolCode, "Tile tools must reference valid definitions and a replacement kit.",
                $"{root}/tools"));
        }

        if (profile.AutoFit.ManualLockedPlacementIds.Distinct().Count() !=
                profile.AutoFit.ManualLockedPlacementIds.Count ||
            profile.AutoFit.ManualLockedPlacementIds.Any(id => !placementIds.Contains(id)))
        {
            diagnostics.Add(new(ReferenceCode, "Auto-fit locks must reference unique tile placements.",
                $"{root}/autoFit/manualLockedPlacementIds"));
        }

        foreach (var property in profile.Properties)
        {
            var path = $"{root}/properties/{property.Id:D}";
            if (!placementIds.Contains(property.PlacementId) || string.IsNullOrWhiteSpace(property.SemanticState) ||
                !float.IsFinite(property.Light.Intensity) || property.Light.Intensity < 0 ||
                !float.IsFinite(property.Light.RangeMeters) || property.Light.RangeMeters < 0 ||
                property.Light.Enabled && string.IsNullOrWhiteSpace(property.Light.Color) ||
                property.MaterialOverrides.Keys.Any(string.IsNullOrWhiteSpace) ||
                property.SocketOverrides.Any(string.IsNullOrWhiteSpace) ||
                property.Overrides.Keys.Any(string.IsNullOrWhiteSpace))
            {
                diagnostics.Add(new(PropertyCode,
                    "Tile state, animation, materials, light, collision, navigation, sockets and overrides must be valid.",
                    path));
            }
        }

        if (profile.Resize.MinimumWidth <= 0 || profile.Resize.MinimumDepth <= 0 ||
            profile.Resize.MaximumWidth < profile.Resize.MinimumWidth ||
            profile.Resize.MaximumDepth < profile.Resize.MinimumDepth ||
            profile.Resize.MaximumWidth > 65536 || profile.Resize.MaximumDepth > 65536 ||
            profile.Resize.Anchor is not ("Center" or "NorthWest" or "NorthEast" or "SouthWest" or "SouthEast") ||
            profile.Resize.LossRepositions.Select(item => item.PlacementId).Distinct().Count() !=
            profile.Resize.LossRepositions.Count ||
            profile.Resize.LossRepositions.Any(item => !placementIds.Contains(item.PlacementId)))
        {
            diagnostics.Add(new(ResizeCode,
                "Resize limits, anchor and loss reposition mappings must be bounded and reference placements.",
                $"{root}/resize"));
        }

        return diagnostics.OrderBy(item => item.Path, StringComparer.Ordinal)
            .ThenBy(item => item.Code, StringComparer.Ordinal).ToArray();
    }
}

public sealed record AreaTileAutoFitPreview(
    Guid PlacementId,
    Guid CurrentDefinitionId,
    Guid SelectedDefinitionId,
    IReadOnlyList<Guid> CompatibleDefinitionIds,
    bool ManualLock,
    bool Ambiguous,
    IReadOnlyList<string> Diagnostics);

public static class AreaTileAutoFitService
{
    public const string PlacementMissingCode = "area.tile.autofit_placement_missing";
    public const string NoCandidateCode = "area.tile.autofit_no_candidate";
    public const string AmbiguousCode = "area.tile.autofit_ambiguous";

    public static AreaTileAutoFitPreview Preview(ModularTileKitProfile kit, Guid placementId)
    {
        ArgumentNullException.ThrowIfNull(kit);
        var placement = kit.Placements.SingleOrDefault(item => item.Id == placementId);
        if (placement is null)
        {
            return new(placementId, Guid.Empty, Guid.Empty, [], false, false, [PlacementMissingCode]);
        }

        var editing = kit.Editing ?? new AreaTileEditingProfile(Guid.Empty, 1, "runtime-default", [],
            new(AreaTileEditOperationKind.SelectByType, null, null, null, string.Empty, true),
            new(0, [], true), [], new(1, 1, 4096, 4096, "Center", true, []));
        var locked = editing.AutoFit.ManualLockedPlacementIds.Contains(placement.Id);
        if (locked)
        {
            return new(placement.Id, placement.DefinitionId, placement.DefinitionId, [placement.DefinitionId],
                true, false, []);
        }

        var neighbours = kit.Placements.Where(item => item.Id != placement.Id &&
                Math.Abs(item.GridX - placement.GridX) + Math.Abs(item.GridZ - placement.GridZ) == 1)
            .ToArray();
        var candidates = kit.Definitions.Where(candidate => neighbours.All(neighbour =>
                HasCompatibleSocket(kit, candidate, placement, neighbour)))
            .OrderBy(item => item.Id).Select(item => item.Id).ToArray();
        if (candidates.Length == 0)
        {
            return new(placement.Id, placement.DefinitionId, placement.DefinitionId, [], false, false,
                [NoCandidateCode]);
        }

        var selected = candidates[StableIndex(editing.AutoFit.Seed, placement.Id, candidates.Length)];
        var ambiguous = candidates.Length > 1;
        var diagnostics = ambiguous && editing.AutoFit.ReportAmbiguities ? new[] { AmbiguousCode } : [];
        return new(placement.Id, placement.DefinitionId, selected, candidates, false, ambiguous, diagnostics);
    }

    private static bool HasCompatibleSocket(
        ModularTileKitProfile kit,
        ModularTileDefinition candidate,
        ModularTilePlacement placement,
        ModularTilePlacement neighbour)
    {
        var neighbourDefinition = kit.Definitions.SingleOrDefault(item => item.Id == neighbour.DefinitionId);
        if (neighbourDefinition is null) return false;
        var direction = Direction(neighbour.GridX - placement.GridX, neighbour.GridZ - placement.GridZ);
        var opposite = Opposite(direction);
        var sourceSockets = candidate.Sockets.Where(item => item.Direction is ModularTileSocketDirection.Omnidirectional ||
            item.Direction == direction).ToArray();
        var targetSockets = neighbourDefinition.Sockets.Where(item =>
            item.Direction is ModularTileSocketDirection.Omnidirectional || item.Direction == opposite).ToArray();
        return sourceSockets.Any(source => targetSockets.Any(target => Compatible(kit, source.Type, target.Type)));
    }

    private static bool Compatible(ModularTileKitProfile kit, string first, string second) =>
        string.Equals(first, second, StringComparison.Ordinal) ||
        kit.ConnectionRules.Any(rule =>
            string.Equals(rule.FirstSocketType, first, StringComparison.Ordinal) &&
            string.Equals(rule.SecondSocketType, second, StringComparison.Ordinal) ||
            rule.Bidirectional && string.Equals(rule.FirstSocketType, second, StringComparison.Ordinal) &&
            string.Equals(rule.SecondSocketType, first, StringComparison.Ordinal));

    private static ModularTileSocketDirection Direction(int dx, int dz) => (dx, dz) switch
    {
        (1, 0) => ModularTileSocketDirection.East,
        (-1, 0) => ModularTileSocketDirection.West,
        (0, 1) => ModularTileSocketDirection.South,
        _ => ModularTileSocketDirection.North,
    };

    private static ModularTileSocketDirection Opposite(ModularTileSocketDirection direction) => direction switch
    {
        ModularTileSocketDirection.North => ModularTileSocketDirection.South,
        ModularTileSocketDirection.East => ModularTileSocketDirection.West,
        ModularTileSocketDirection.South => ModularTileSocketDirection.North,
        ModularTileSocketDirection.West => ModularTileSocketDirection.East,
        _ => direction,
    };

    private static int StableIndex(int seed, Guid placementId, int count)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}:{placementId:D}"));
        return (int)(BitConverter.ToUInt32(digest, 0) % (uint)count);
    }
}

public sealed record AreaTileEditingRuntimeHandle(
    Guid ProfileId,
    int GroupCount,
    int PropertyCount,
    int ManualLockCount,
    bool ResizeMigrationEnabled);

public static class AreaTileEditingRuntimeResolver
{
    public const string RequiredMissingCode = "runtime.area_tile_editing.required_missing";

    public static (AreaTileEditingRuntimeHandle? Handle, IReadOnlyList<DomainDiagnostic> Diagnostics) Resolve(
        AreaId areaId,
        AreaTileEditingProfile? profile,
        bool required)
    {
        if (profile is null)
        {
            return required
                ? (null, [new(RequiredMissingCode, "Required tile editing metadata is missing.",
                    $"areas/{areaId}/foundation/tileKit/editing")])
                : (null, []);
        }

        return (new(profile.Id, profile.Groups.Count, profile.Properties.Count,
            profile.AutoFit.ManualLockedPlacementIds.Count, profile.Resize.MigrateInstances), []);
    }
}
