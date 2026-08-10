using Forge.Application.Commands;
using Forge.Domain.Assets;

namespace Forge.Mcp.Assets;

public sealed record AssetGovernanceQuery(
    string? AssetId = null,
    string? Origin = null,
    string? QuarantineStatus = null,
    int MaximumResults = 100);

public sealed record AssetGovernanceResource(
    int SchemaVersion,
    long Revision,
    int Total,
    IReadOnlyList<AssetGovernanceRecord> Records,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record AssetGovernancePlanRequest(
    int SchemaVersion,
    long BaseRevision,
    AssetGovernanceRecord Record);

public sealed record AssetGovernancePlanResult(
    bool Succeeded,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool DryRun);
