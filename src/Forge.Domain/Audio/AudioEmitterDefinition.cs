namespace Forge.Domain.Audio;

public sealed record AudioEmitterDefinition(
    Guid Id,
    int SchemaVersion,
    string Name,
    AudioClipSet ClipSet,
    AudioLoopPolicy Loop,
    AudioEmitterSpatialProfile Spatial,
    Guid? MixBusId,
    float StartDelaySeconds,
    string ActivatedScript,
    string CompletedScript,
    bool Required,
    string Provenance,
    string PackageManifest,
    string LicenseSpdx,
    string ContentSha256)
{
    public const int CurrentSchemaVersion = 1;
}
