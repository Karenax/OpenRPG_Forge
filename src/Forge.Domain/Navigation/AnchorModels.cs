using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;

namespace Forge.Domain.Navigation;

public enum AnchorKind { Waypoint, Spawn, TransitionDestination }
public enum AnchorScope { Area, Module, World }
public enum PortalOrientationPolicy { Preserve, AnchorRotation, FixedYaw }
public enum PortalVelocityPolicy { Preserve, Reset, RedirectForward }

public sealed record AnchorDefinition(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Slug,
    LocalizedText Label,
    AnchorKind Kind,
    AnchorScope Scope,
    IReadOnlyList<string> Tags,
    BlueprintId? BlueprintId,
    string? AssetId,
    bool RequiredAtRuntime)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record SpawnAnchorProfile(
    string Profile,
    Vector3Value Clearance,
    int Priority,
    string Team,
    float OrientationDegrees,
    Guid? FallbackInstanceId,
    bool RequireNavigation);

public sealed record PortalAnchorDestination(
    AreaId TargetAreaId,
    Guid TargetAnchorInstanceId,
    PortalOrientationPolicy Orientation,
    float FixedYawDegrees,
    PortalVelocityPolicy Velocity,
    bool Preload);

public sealed record AnchorInstance(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    Guid DefinitionId,
    TransformValue Transform,
    int DisplayOrder,
    EntityPropertyOverrides Overrides,
    SpawnAnchorProfile? Spawn,
    PortalAnchorDestination? Portal)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record AnchorCatalog
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public AreaId AreaId { get; }
    public bool RequiredAtRuntime { get; }
    public IReadOnlyList<AnchorDefinition> Definitions { get; }
    public IReadOnlyList<AnchorInstance> Instances { get; }

    public AnchorCatalog(Guid id, int schemaVersion, string? provenance, AreaId areaId, bool requiredAtRuntime,
        IReadOnlyList<AnchorDefinition>? definitions, IReadOnlyList<AnchorInstance>? instances)
    {
        ArgumentNullException.ThrowIfNull(areaId);
        Id = id;
        SchemaVersion = schemaVersion == 0 ? CurrentSchemaVersion : schemaVersion;
        Provenance = string.IsNullOrWhiteSpace(provenance) ? "migration-default" : provenance.Trim();
        AreaId = areaId;
        RequiredAtRuntime = requiredAtRuntime;
        Definitions = (definitions ?? []).OrderBy(item => item.Slug, StringComparer.Ordinal)
            .ThenBy(item => item.Id).ToArray();
        Instances = (instances ?? []).OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id).ToArray();
    }

    public static AnchorCatalog CreateDefault(AreaId areaId) => new(StableId(areaId, "anchor-catalog"),
        CurrentSchemaVersion, "migration-default", areaId, false, [], []);

    public static AnchorCatalog CloneForArea(AnchorCatalog source, AreaId destinationAreaId)
    {
        var definitionMap = source.Definitions.Select((item, index) =>
                (item.Id, NewId: StableId(destinationAreaId, $"anchor-definition:{index}:{item.Slug}")))
            .ToDictionary(item => item.Id, item => item.NewId);
        var instanceMap = source.Instances.Select((item, index) =>
                (item.Id, NewId: StableId(destinationAreaId, $"anchor-instance:{index}:{item.Id}")))
            .ToDictionary(item => item.Id, item => item.NewId);
        var definitions = source.Definitions.Select(item => item with
        {
            Id = definitionMap[item.Id],
            Provenance = "area-clone",
        }).ToArray();
        var instances = source.Instances.Select(item => item with
        {
            Id = instanceMap[item.Id],
            DefinitionId = definitionMap[item.DefinitionId],
            Provenance = "area-clone",
            Spawn = item.Spawn is null ? null : item.Spawn with
            {
                FallbackInstanceId = item.Spawn.FallbackInstanceId is { } fallback && instanceMap.TryGetValue(fallback, out var mapped)
                    ? mapped
                    : item.Spawn.FallbackInstanceId,
            },
            Portal = item.Portal is null ? null : item.Portal with
            {
                TargetAreaId = item.Portal.TargetAreaId == source.AreaId ? destinationAreaId : item.Portal.TargetAreaId,
                TargetAnchorInstanceId = instanceMap.GetValueOrDefault(item.Portal.TargetAnchorInstanceId,
                    item.Portal.TargetAnchorInstanceId),
            },
        }).ToArray();
        return new(StableId(destinationAreaId, "anchor-catalog"), source.SchemaVersion, "area-clone",
            destinationAreaId, source.RequiredAtRuntime, definitions, instances);
    }

    internal static Guid StableId(AreaId areaId, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{areaId}:{discriminator}"));
        Span<byte> value = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(value);
        value[6] = (byte)((value[6] & 0x0f) | 0x40);
        value[8] = (byte)((value[8] & 0x3f) | 0x80);
        return new Guid(value);
    }
}
