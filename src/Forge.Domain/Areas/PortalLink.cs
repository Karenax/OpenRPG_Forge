using Forge.Domain.Entities;
using Forge.Domain.Navigation;

namespace Forge.Domain.Areas;

public sealed record PortalLink
{
    public const int CurrentSchemaVersion = 1;

    public PortalId Id { get; }

    public string DisplayName { get; }

    public EntityId SourceEntityId { get; }

    public AreaId DestinationAreaId { get; }

    public EntityId DestinationEntityId { get; }

    public bool Bidirectional { get; }

    public bool PreloadDestination { get; }

    public int SchemaVersion { get; }

    public string Provenance { get; }

    public PortalOrientationPolicy Orientation { get; }

    public float FixedYawDegrees { get; }

    public PortalVelocityPolicy Velocity { get; }

    public string BeforeTransitionScript { get; }

    public string AfterTransitionScript { get; }

    public bool HooksRequiredAtRuntime { get; }

    public PortalLink(
        PortalId id,
        string displayName,
        EntityId sourceEntityId,
        AreaId destinationAreaId,
        EntityId destinationEntityId,
        bool bidirectional,
        bool preloadDestination,
        int schemaVersion = CurrentSchemaVersion,
        string? provenance = null,
        PortalOrientationPolicy orientation = PortalOrientationPolicy.AnchorRotation,
        float fixedYawDegrees = 0,
        PortalVelocityPolicy velocity = PortalVelocityPolicy.Reset,
        string? beforeTransitionScript = null,
        string? afterTransitionScript = null,
        bool hooksRequiredAtRuntime = false)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(sourceEntityId);
        ArgumentNullException.ThrowIfNull(destinationAreaId);
        ArgumentNullException.ThrowIfNull(destinationEntityId);

        Id = id;
        DisplayName = displayName.Trim();
        SourceEntityId = sourceEntityId;
        DestinationAreaId = destinationAreaId;
        DestinationEntityId = destinationEntityId;
        Bidirectional = bidirectional;
        PreloadDestination = preloadDestination;
        SchemaVersion = schemaVersion == 0 ? CurrentSchemaVersion : schemaVersion;
        Provenance = string.IsNullOrWhiteSpace(provenance) ? "migration-default" : provenance.Trim();
        Orientation = orientation;
        FixedYawDegrees = fixedYawDegrees;
        Velocity = velocity;
        BeforeTransitionScript = beforeTransitionScript?.Trim() ?? string.Empty;
        AfterTransitionScript = afterTransitionScript?.Trim() ?? string.Empty;
        HooksRequiredAtRuntime = hooksRequiredAtRuntime;
    }
}
