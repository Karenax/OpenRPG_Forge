namespace Forge.Application.Sequences;

public sealed record SequenceSignal(Guid KeyframeId, int TimeMilliseconds, string Name);
