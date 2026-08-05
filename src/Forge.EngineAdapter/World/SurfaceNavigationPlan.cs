using Forge.Domain.Navigation;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.EngineAdapter.World;

public sealed record SurfaceNavigationTriangle(int A, int B, int C);

public sealed record SurfaceNavigationLinkPlan(
    OffMeshNavigationLinkId Id,
    string Name,
    Vector3Value Start,
    Vector3Value End,
    bool Bidirectional);

public sealed record SurfaceNavigationPlan(
    bool Enabled,
    NavigationAgentProfile? Agent,
    IReadOnlyList<Vector3Value> Vertices,
    IReadOnlyList<SurfaceNavigationTriangle> Triangles,
    IReadOnlyList<SurfaceNavigationLinkPlan> Links,
    int BlockedTriangles,
    IReadOnlyList<DomainDiagnostic> Diagnostics);
