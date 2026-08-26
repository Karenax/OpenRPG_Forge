using Forge.Application.Areas;
using Forge.Domain.Areas;
using Forge.Domain.Audio;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Audio;

namespace Forge.Mcp.Projects;

public sealed partial class ForgeProjectAgentService
{
    public BuiltinAudioResourceCatalog GetBuiltinAudioResources()
    {
        var resources = ProceduralAmbientAudioService.SupportedResources.Select(resource =>
        {
            var clip = ProceduralAmbientAudioService.Create(resource);
            return new BuiltinAudioResource(
                1,
                resource,
                ProceduralAmbientAudioService.LicenseSpdx,
                clip.ContentSha256,
                clip.Pcm16Mono.LongLength,
                "audio/L16",
                ProceduralAmbientAudioService.PackageManifest);
        }).ToArray();
        return new BuiltinAudioResourceCatalog(1, resources);
    }

    public AudioEmitterPlanResult PlanAudioEmitter(AudioEmitterCreatePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != AudioEmitterCreatePlanRequest.CurrentSchemaVersion ||
                request.BaseRevision < 0 || string.IsNullOrWhiteSpace(request.Name) ||
                string.IsNullOrWhiteSpace(request.Provenance) || request.Conditions is null ||
                request.Conditions.Any(string.IsNullOrWhiteSpace) ||
                request.Conditions.Distinct(StringComparer.Ordinal).Count() != request.Conditions.Count ||
                !float.IsFinite(request.InnerRadius) || !float.IsFinite(request.OuterRadius) ||
                request.InnerRadius < 0 || request.OuterRadius <= request.InnerRadius ||
                !float.IsFinite(request.Volume) || request.Volume is < 0 or > 4 ||
                !Enum.IsDefined(request.LoopMode))
                return AudioEmitterFailure(request.BaseRevision, "audio_emitter_create.request_invalid",
                    "Audio emitter creation requires schema v1, identity, full transform, valid radii, volume, loop and unique conditions.");

            AreaId areaId;
            try
            {
                areaId = AreaId.Parse(request.AreaId);
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                return AudioEmitterFailure(request.BaseRevision, "audio_emitter_plan.area_invalid", exception.Message);
            }
            var area = _history.State.Project.Areas.SingleOrDefault(item => item.Id == areaId);
            if (area is null)
                return AudioEmitterFailure(request.BaseRevision, "audio_emitter_plan.area_missing",
                    "Area does not exist.");

            var source = new GovernedAudioSourceMetadata(
                request.ResourcePath.Trim(), request.LicenseSpdx.Trim(), request.ContentSha256.Trim(),
                request.MemoryBytes, request.MimeType.Trim(), request.PackageManifest.Trim());
            var governed = GovernedAudioSourceValidator.Validate(_session.RootDirectory, source);
            if (!governed.Succeeded)
                return new AudioEmitterPlanResult(false, null, request.BaseRevision, null, null,
                    governed.Diagnostics.Select(Map).ToArray(), false, false, []);

            var definitionId = Guid.NewGuid();
            var clip = new AudioClipDefinition(
                Guid.NewGuid(),
                source.ResourcePath,
                1,
                ["authored", "ambient"],
                source.ResourcePath.StartsWith("builtin://", StringComparison.Ordinal) ? "procedural" : "governed",
                "und",
                -18,
                request.PreviewResourcePath.Trim(),
                source.MimeType,
                source.MemoryBytes,
                source.LicenseSpdx,
                request.Provenance.Trim(),
                source.ContentSha256,
                request.Name.Trim(),
                [],
                0);
            var definition = new AudioEmitterDefinition(
                definitionId,
                AudioEmitterDefinition.CurrentSchemaVersion,
                request.Name.Trim(),
                new AudioClipSet(Guid.NewGuid(), AudioClipSet.CurrentSchemaVersion,
                    $"{request.Name.Trim()} clips", AudioClipSelectionMode.Ordered, [clip],
                    request.Provenance.Trim()),
                BuildLoop(request.LoopMode),
                new AudioEmitterSpatialProfile(request.InnerRadius, request.OuterRadius,
                    "inverse-square", 360, -12, 0, 0, true),
                null,
                0,
                string.Empty,
                string.Empty,
                true,
                request.Provenance.Trim(),
                source.PackageManifest,
                source.LicenseSpdx,
                source.ContentSha256);
            var control = new AudioEmitterControlProfile(
                AudioEmitterControlProfile.CurrentSchemaVersion,
                request.Volume,
                request.Muted,
                request.Conditions.Select(item => item.Trim()).ToArray());
            var instance = new AudioEmitterInstance(
                Guid.NewGuid(), definitionId, request.Transform, true, request.Provenance.Trim(), control);
            var current = area.Environment.Audio.Emitters;
            var catalog = new AudioEmitterCatalog(
                AudioEmitterCatalog.CurrentSchemaVersion,
                current.Definitions.Append(definition).ToArray(),
                current.Instances.Append(instance).ToArray());
            return PlanAudioEmitters(new AudioEmitterPlanRequest(
                AudioEmitterCreatePlanRequest.CurrentSchemaVersion,
                request.BaseRevision,
                request.AreaId,
                catalog));
        }
    }

    private static AudioLoopPolicy BuildLoop(AudioLoopMode mode) => mode switch
    {
        AudioLoopMode.OneShot => new(mode, 0, 0, 0, 0, string.Empty),
        AudioLoopMode.Loop => new(mode, 0, 0, 0, 0, string.Empty),
        AudioLoopMode.SeamlessRegion => new(mode, 0, 0, 1, .1f, string.Empty),
        AudioLoopMode.FiniteCount => new(mode, 1, 0, 0, 0, string.Empty),
        AudioLoopMode.EventDriven => new(mode, 0, 0, 0, 0, "audio.stop"),
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };
}
