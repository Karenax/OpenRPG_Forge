namespace Forge.Domain.Navigation;

public sealed record NavigationEdge(
    NavigationCell From,
    NavigationCell To,
    OffMeshNavigationLinkId LinkId);
