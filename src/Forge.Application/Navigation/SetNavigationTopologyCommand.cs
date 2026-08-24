using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.WorldFeatures;

namespace Forge.Application.Navigation;

public enum NavigationPortalMutationKind
{
    Upsert,
    Remove,
}

public sealed record AreaNavigationTopologyUpdate(AreaId AreaId, AreaNavigationDefinition Navigation);

public sealed record NavigationPortalMutation(
    NavigationPortalMutationKind Kind,
    AreaId SourceAreaId,
    PortalId PortalId,
    PortalLink? Portal)
{
    public static NavigationPortalMutation Upsert(AreaId sourceAreaId, PortalLink portal)
    {
        ArgumentNullException.ThrowIfNull(portal);
        return new(NavigationPortalMutationKind.Upsert, sourceAreaId, portal.Id, portal);
    }

    public static NavigationPortalMutation Remove(AreaId sourceAreaId, PortalId portalId) =>
        new(NavigationPortalMutationKind.Remove, sourceAreaId, portalId, null);
}

public sealed record SetNavigationTopologyCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    IReadOnlyList<AreaNavigationTopologyUpdate> NavigationUpdates,
    IReadOnlyList<NavigationPortalMutation> PortalMutations,
    WorldFeatureCatalog? WorldFeatures,
    bool DryRun);
