using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Application.Sequences;

public sealed record SequenceEvaluation(SequencePlaybackState State, Guid? CameraAnchorId,
    IReadOnlyDictionary<Guid, TransformValue> Transforms, IReadOnlyList<string> AudioResources,
    IReadOnlyList<SequenceSignal> Signals, IReadOnlyList<DomainDiagnostic> Diagnostics);
