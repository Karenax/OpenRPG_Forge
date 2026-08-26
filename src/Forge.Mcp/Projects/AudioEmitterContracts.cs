using Forge.Application.Commands;
using Forge.Domain.Audio;
using Forge.Domain.Spatial;

namespace Forge.Mcp.Projects;

public sealed record AudioEmitterResource(
    int SchemaVersion,
    long Revision,
    string AreaId,
    AudioEmitterCatalog Catalog);

public sealed record AudioEmitterPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string AreaId,
    AudioEmitterCatalog Catalog);

public sealed record AudioEmitterCreatePlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string AreaId,
    string Name,
    string ResourcePath,
    TransformValue Transform,
    float InnerRadius,
    float OuterRadius,
    float Volume,
    AudioLoopMode LoopMode,
    IReadOnlyList<string> Conditions,
    bool Muted,
    string LicenseSpdx,
    string ContentSha256,
    long MemoryBytes,
    string MimeType,
    string PreviewResourcePath,
    string Provenance,
    string PackageManifest)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record BuiltinAudioResourceCatalog(
    int SchemaVersion,
    IReadOnlyList<BuiltinAudioResource> Resources);

public sealed record BuiltinAudioResource(
    int SchemaVersion,
    string ResourcePath,
    string LicenseSpdx,
    string ContentSha256,
    long MemoryBytes,
    string MimeType,
    string PackageManifest);

public sealed record AudioEmitterPlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<AudioEmitterAuditEntry> Audit);

public sealed record AudioEmitterAuditEntry(
    string PlanId,
    string Action,
    long Revision,
    DateTimeOffset OccurredAt,
    bool Succeeded);
