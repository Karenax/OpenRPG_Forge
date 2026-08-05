namespace Forge.Domain.Sequences;

public sealed record SequenceDefinition
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Slug { get; }
    public string DisplayName { get; }
    public int DurationMilliseconds { get; }
    public bool Skippable { get; }
    public bool MultiplayerSynchronized { get; }
    public IReadOnlyList<SequenceTrackDefinition> Tracks { get; }
    public IReadOnlyList<SequenceBranchDefinition> Branches { get; }
    public string Provenance { get; }

    public SequenceDefinition(Guid id, int schemaVersion, string slug, string displayName, int durationMilliseconds,
        bool skippable, bool multiplayerSynchronized, IReadOnlyList<SequenceTrackDefinition> tracks,
        IReadOnlyList<SequenceBranchDefinition> branches, string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(tracks);
        ArgumentNullException.ThrowIfNull(branches);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var orderedTracks = tracks.OrderBy(item => item.Id).ToArray();
        var orderedBranches = branches.OrderBy(item => item.AtMilliseconds).ThenByDescending(item => item.Priority)
            .ThenBy(item => item.Id).ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || durationMilliseconds is < 1 or > 86_400_000 ||
            orderedTracks.Select(item => item.Id).Distinct().Count() != orderedTracks.Length ||
            orderedBranches.Select(item => item.Id).Distinct().Count() != orderedBranches.Length ||
            orderedTracks.SelectMany(item => item.Keyframes).Any(item => item.TimeMilliseconds > durationMilliseconds) ||
            orderedBranches.Any(item => item.AtMilliseconds > durationMilliseconds ||
                item.TargetMilliseconds > durationMilliseconds))
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Sequence definition is invalid.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Slug = slug.Trim();
        DisplayName = displayName.Trim();
        DurationMilliseconds = durationMilliseconds;
        Skippable = skippable;
        MultiplayerSynchronized = multiplayerSynchronized;
        Tracks = orderedTracks;
        Branches = orderedBranches;
        Provenance = provenance.Trim();
    }
}
