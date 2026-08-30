namespace Forge.Mcp.Projects;

public sealed record TerrainPlacementCandidateRequest(
    int SchemaVersion,
    string AreaId,
    float FootprintWidth,
    float FootprintDepth,
    float PreferredX = 0,
    float PreferredZ = 0,
    float SearchRadiusMeters = 256,
    float StepMeters = 8,
    float MaximumSurfaceVariationMeters = .35f,
    string EnvironmentCategory = "vegetation",
    int MinimumNearbyEnvironmentInstances = 0,
    float EnvironmentRadiusMeters = 64,
    int MaximumFootprintEnvironmentInstances = 0,
    int Limit = 12,
    float MinimumEnvironmentDensityPerHectare = 0);

public sealed record TerrainPlacementCandidateResource(
    bool Succeeded,
    long ProjectRevision,
    string AreaId,
    string? SurfaceFingerprint,
    string? EnvironmentFingerprint,
    IReadOnlyList<Forge.Application.Assistant.TerrainPlacementCandidate> Candidates,
    IReadOnlyList<AgentDiagnostic> Diagnostics);
