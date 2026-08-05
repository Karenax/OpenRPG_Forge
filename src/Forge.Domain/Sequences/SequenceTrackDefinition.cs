namespace Forge.Domain.Sequences;

public sealed record SequenceTrackDefinition
{
    public Guid Id { get; }
    public string Name { get; }
    public SequenceTrackKind Kind { get; }
    public SequenceTargetKind TargetKind { get; }
    public Guid? TargetId { get; }
    public string ResourcePath { get; }
    public IReadOnlyList<SequenceKeyframe> Keyframes { get; }
    public string Provenance { get; }

    public SequenceTrackDefinition(Guid id, string name, SequenceTrackKind kind, SequenceTargetKind targetKind,
        Guid? targetId, string resourcePath, IReadOnlyList<SequenceKeyframe> keyframes, string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(resourcePath);
        ArgumentNullException.ThrowIfNull(keyframes);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var values = keyframes.OrderBy(item => item.TimeMilliseconds).ThenBy(item => item.Id).ToArray();
        if (id == Guid.Empty || targetId == Guid.Empty ||
            values.Select(item => item.Id).Distinct().Count() != values.Length ||
            kind is SequenceTrackKind.Camera or SequenceTrackKind.Transform && targetId is null ||
            kind == SequenceTrackKind.Audio && string.IsNullOrWhiteSpace(resourcePath) ||
            kind == SequenceTrackKind.Transform && values.Any(item => item.Transform is null) ||
            kind is SequenceTrackKind.Signal or SequenceTrackKind.State &&
            values.Any(item => string.IsNullOrWhiteSpace(item.Value)))
        {
            throw new ArgumentException("Sequence track is invalid.", nameof(keyframes));
        }

        Id = id;
        Name = name.Trim();
        Kind = kind;
        TargetKind = targetKind;
        TargetId = targetId;
        ResourcePath = resourcePath.Trim().Replace('\\', '/');
        Keyframes = values;
        Provenance = provenance.Trim();
    }
}
