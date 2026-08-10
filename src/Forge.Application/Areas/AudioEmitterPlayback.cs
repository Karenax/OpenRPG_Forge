using Forge.Domain.Audio;
using Forge.Domain.Spatial;

namespace Forge.Application.Areas;

public sealed record AudioEmitterPlayback(
    AudioEmitterHandle Handle,
    Guid DefinitionId,
    Guid ClipId,
    string ResourcePath,
    TransformValue Transform,
    AudioLoopPolicy Loop,
    AudioEmitterSpatialProfile Spatial,
    Guid? MixBusId,
    float StartDelaySeconds,
    bool Autoplay,
    string ActivatedScript,
    string CompletedScript);
