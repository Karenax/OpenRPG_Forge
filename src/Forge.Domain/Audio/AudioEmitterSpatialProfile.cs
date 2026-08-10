namespace Forge.Domain.Audio;

public sealed record AudioEmitterSpatialProfile(
    float InnerRadius,
    float OuterRadius,
    string AttenuationCurve,
    float ConeAngleDegrees,
    float ConeAttenuationDb,
    float Occlusion,
    float HeightOffset,
    bool VirtualizeOutsideRange);
