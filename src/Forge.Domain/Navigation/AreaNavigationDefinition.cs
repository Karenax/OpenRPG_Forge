using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Areas;

namespace Forge.Domain.Navigation;

public sealed record AreaNavigationDefinition
{
    public const int CurrentSchemaVersion = 1;

    public AreaNavigationDefinitionId Id { get; }

    public int SchemaVersion { get; }

    public float CellSize { get; }

    public IReadOnlyList<NavigationAgentProfile> AgentProfiles { get; }

    public IReadOnlyList<OffMeshNavigationLink> OffMeshLinks { get; }

    public string Provenance { get; }

    public AnchorCatalog Anchors { get; }

    public WaypointAuthoringProfile Waypoints { get; }

    public AreaNavigationDefinition(
        AreaNavigationDefinitionId id,
        float cellSize,
        IReadOnlyList<NavigationAgentProfile> agentProfiles,
        IReadOnlyList<OffMeshNavigationLink>? offMeshLinks,
        string provenance,
        int schemaVersion = CurrentSchemaVersion,
        AnchorCatalog? anchors = null,
        WaypointAuthoringProfile? waypoints = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(agentProfiles);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion),
                $"Area navigation schema '{schemaVersion}' is not supported.");
        }

        if (!float.IsFinite(cellSize) || cellSize is < 0.25f or > 4f)
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize), "Navigation cell size must be between 0.25 and 4 metres.");
        }

        var profiles = agentProfiles.OrderBy(profile => profile.Radius).ThenBy(profile => profile.Name).ToArray();
        if (profiles.Length == 0)
        {
            throw new ArgumentException("Area navigation requires at least one agent profile.", nameof(agentProfiles));
        }

        if (profiles.Select(profile => profile.Id).Distinct().Count() != profiles.Length ||
            profiles.Select(profile => profile.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != profiles.Length)
        {
            throw new ArgumentException("Navigation agent profile IDs and names must be unique.", nameof(agentProfiles));
        }

        var links = (offMeshLinks ?? []).OrderBy(link => link.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        if (links.Select(link => link.Id).Distinct().Count() != links.Length)
        {
            throw new ArgumentException("Off-mesh navigation link IDs must be unique.", nameof(offMeshLinks));
        }

        Id = id;
        SchemaVersion = schemaVersion;
        CellSize = cellSize;
        AgentProfiles = profiles;
        OffMeshLinks = links;
        Provenance = provenance.Trim();
        Anchors = anchors ?? AnchorCatalog.CreateDefault(AreaId.From(id.Value));
        Waypoints = waypoints ?? WaypointAuthoringProfile.CreateDefault(Anchors.AreaId);
    }

    public static AreaNavigationDefinition CreateDefault(AreaId areaId)
    {
        ArgumentNullException.ThrowIfNull(areaId);
        return new AreaNavigationDefinition(
            AreaNavigationDefinitionId.From(StableGuid(areaId, "navigation")),
            1f,
            [
                Agent(areaId, "small", "Petit", 0.3f, 1.4f, true, true, false),
                Agent(areaId, "medium", "Moyen", 0.5f, 1.8f, true, true, false),
                Agent(areaId, "large", "Grand", 0.9f, 2.4f, false, true, false),
                Agent(areaId, "flying", "Volant", 0.4f, 1f, false, true, true),
            ],
            [],
            "migration-default",
            anchors: AnchorCatalog.CreateDefault(areaId),
            waypoints: WaypointAuthoringProfile.CreateDefault(areaId));
    }

    public static AreaNavigationDefinition CloneForArea(AreaNavigationDefinition source, AreaId destinationAreaId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destinationAreaId);
        return new AreaNavigationDefinition(
            AreaNavigationDefinitionId.From(StableGuid(destinationAreaId, "navigation")),
            source.CellSize,
            source.AgentProfiles.Select((profile, index) => new NavigationAgentProfile(
                NavigationAgentProfileId.From(StableGuid(destinationAreaId, $"agent:{index}:{profile.Name}")),
                profile.Name,
                profile.Radius,
                profile.Height,
                profile.CanOpenDoors,
                profile.CanUseOffMeshLinks,
                profile.CanFly,
                "area-clone")).ToArray(),
            source.OffMeshLinks.Select((link, index) => new OffMeshNavigationLink(
                OffMeshNavigationLinkId.From(StableGuid(destinationAreaId, $"link:{index}:{link.Name}")),
                link.Name,
                link.Kind,
                link.Start,
                link.End,
                link.Bidirectional,
                link.MaximumAgentRadius,
                "area-clone")).ToArray(),
            "area-clone",
            anchors: AnchorCatalog.CloneForArea(source.Anchors, destinationAreaId),
            waypoints: WaypointAuthoringProfile.CloneForArea(source.Waypoints, destinationAreaId));
    }

    public AreaNavigationDefinition WithAnchors(AnchorCatalog anchors) => new(
        Id, CellSize, AgentProfiles, OffMeshLinks, Provenance, SchemaVersion, anchors, Waypoints);

    public AreaNavigationDefinition WithWaypoints(WaypointAuthoringProfile waypoints) => new(
        Id, CellSize, AgentProfiles, OffMeshLinks, Provenance, SchemaVersion, Anchors, waypoints);

    private static NavigationAgentProfile Agent(
        AreaId areaId,
        string key,
        string name,
        float radius,
        float height,
        bool doors,
        bool links,
        bool flight) => new(
            NavigationAgentProfileId.From(StableGuid(areaId, $"agent:{key}")),
            name,
            radius,
            height,
            doors,
            links,
            flight,
            "migration-default");

    private static Guid StableGuid(AreaId areaId, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{areaId}:{discriminator}"));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0f) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3f) | 0x80);
        return new Guid(guidBytes);
    }
}
