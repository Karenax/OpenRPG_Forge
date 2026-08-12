using Forge.Domain.Blueprints;

namespace Forge.Domain.Authoring;

public sealed record NamedPaletteCollection
{
    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Name { get; }
    public IReadOnlyList<BlueprintId> BlueprintIds { get; }
    public bool TaskScoped { get; }
    public string Provenance { get; }

    public NamedPaletteCollection(Guid id, int schemaVersion, string name,
        IReadOnlyList<BlueprintId> blueprintIds, bool taskScoped, string provenance)
    {
        if (id == Guid.Empty || schemaVersion != 1) throw new ArgumentOutOfRangeException(nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(blueprintIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var ids = blueprintIds.Distinct().Take(128).ToArray();
        if (ids.Length != blueprintIds.Count)
            throw new ArgumentException("Named palette blueprint IDs must be unique and bounded.", nameof(blueprintIds));
        Id = id;
        SchemaVersion = schemaVersion;
        Name = name.Trim();
        BlueprintIds = ids;
        TaskScoped = taskScoped;
        Provenance = provenance.Trim();
    }
}
