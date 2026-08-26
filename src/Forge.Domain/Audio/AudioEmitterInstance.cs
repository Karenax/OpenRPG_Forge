using Forge.Domain.Spatial;

namespace Forge.Domain.Audio;

public sealed record AudioEmitterInstance(
    Guid Id,
    Guid DefinitionId,
    TransformValue Transform,
    bool Enabled,
    string Provenance,
    AudioEmitterControlProfile? Control = null)
{
    public AudioEmitterControlProfile EffectiveControl => Control ?? AudioEmitterControlProfile.Default;
}
