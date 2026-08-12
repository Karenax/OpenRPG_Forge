using Forge.Domain.Blueprints;

namespace Forge.Domain.Authoring;

public sealed record EditorDisplayFilter
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }

    public int SchemaVersion { get; }

    public string Provenance { get; }

    public IReadOnlyList<BlueprintKind> VisibleKinds { get; }

    public IReadOnlyList<string> RequiredTags { get; }

    public string Author { get; }

    public string State { get; }

    public bool IsolateSelection { get; }

    public bool GhostHidden { get; }

    public IReadOnlyList<Guid> VisibleLayerIds { get; }

    public IReadOnlyList<EditorVolumeKind> VisibleVolumeKinds { get; }

    public EditorDisplayFilter(
        IReadOnlyList<BlueprintKind>? visibleKinds = null,
        IReadOnlyList<string>? requiredTags = null,
        string author = "",
        string state = "",
        bool isolateSelection = false,
        bool ghostHidden = false,
        IReadOnlyList<Guid>? visibleLayerIds = null,
        IReadOnlyList<EditorVolumeKind>? visibleVolumeKinds = null,
        Guid id = default,
        int schemaVersion = CurrentSchemaVersion,
        string provenance = "openrpg-forge-default")
    {
        if (schemaVersion != CurrentSchemaVersion) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var layers = (visibleLayerIds ?? []).Distinct().Order().ToArray();
        if (layers.Any(candidate => candidate == Guid.Empty))
            throw new ArgumentException("Display layer UUIDs must be non-empty.", nameof(visibleLayerIds));
        Id = id == Guid.Empty ? Guid.Parse("59000000-0000-4000-8000-000000000008") : id;
        SchemaVersion = schemaVersion;
        Provenance = provenance.Trim();
        VisibleKinds = (visibleKinds ?? Enum.GetValues<BlueprintKind>()).Distinct().Order().ToArray();
        RequiredTags = (requiredTags ?? []).Select(tag => tag.Trim().ToLowerInvariant())
            .Where(tag => tag.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Author = author.Trim();
        State = state.Trim();
        IsolateSelection = isolateSelection;
        GhostHidden = ghostHidden;
        VisibleLayerIds = layers;
        VisibleVolumeKinds = (visibleVolumeKinds ?? Enum.GetValues<EditorVolumeKind>()).Distinct().Order().ToArray();
    }

    public static EditorDisplayFilter ShowAll { get; } = new();
}
