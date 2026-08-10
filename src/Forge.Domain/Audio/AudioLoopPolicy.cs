namespace Forge.Domain.Audio;

public sealed record AudioLoopPolicy(
    AudioLoopMode Mode,
    int RepeatCount,
    float RegionStartSeconds,
    float RegionEndSeconds,
    float CrossfadeSeconds,
    string StopEvent);
