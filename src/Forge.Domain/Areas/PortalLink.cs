using Forge.Domain.Entities;

namespace Forge.Domain.Areas;

public sealed record PortalLink
{
    public PortalId Id { get; }

    public string DisplayName { get; }

    public EntityId SourceEntityId { get; }

    public AreaId DestinationAreaId { get; }

    public EntityId DestinationEntityId { get; }

    public bool Bidirectional { get; }

    public bool PreloadDestination { get; }

    public PortalLink(
        PortalId id,
        string displayName,
        EntityId sourceEntityId,
        AreaId destinationAreaId,
        EntityId destinationEntityId,
        bool bidirectional,
        bool preloadDestination)
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
    }
}
