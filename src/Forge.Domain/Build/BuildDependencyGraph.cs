namespace Forge.Domain.Build;

public sealed record BuildDependencyGraph
{
    public const int CurrentSchemaVersion = 1;

    public BuildResourceId Id { get; }

    public int SchemaVersion { get; }

    public string Provenance { get; }

    public IReadOnlyList<BuildDependencyNode> Nodes { get; }

    public BuildDependencyGraph(
        BuildResourceId id,
        int schemaVersion,
        string provenance,
        IReadOnlyList<BuildDependencyNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        ArgumentNullException.ThrowIfNull(nodes);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        var materialized = nodes.OrderBy(node => node.Path, StringComparer.Ordinal).ToArray();
        if (materialized.Select(node => node.Id).Distinct().Count() != materialized.Length)
        {
            throw new ArgumentException("Build resource IDs must be unique.", nameof(nodes));
        }

        if (materialized.Select(node => node.Path).Distinct(StringComparer.Ordinal).Count() != materialized.Length)
        {
            throw new ArgumentException("Build resource paths must be unique.", nameof(nodes));
        }

        var ids = materialized.Select(node => node.Id).ToHashSet();
        if (materialized.SelectMany(node => node.Dependencies).Any(dependency => !ids.Contains(dependency)))
        {
            throw new ArgumentException("Every build dependency must resolve to a graph node.", nameof(nodes));
        }

        EnsureAcyclic(materialized);
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance.Trim();
        Nodes = materialized;
    }

    public IReadOnlyList<BuildDependencyNode> AffectedBy(IReadOnlyList<string> changedPaths)
    {
        ArgumentNullException.ThrowIfNull(changedPaths);
        var changed = changedPaths
            .Select(path => path.Replace('\\', '/').Trim())
            .ToHashSet(StringComparer.Ordinal);
        var affected = Nodes.Where(node => changed.Contains(node.Path)).Select(node => node.Id).ToHashSet();
        var ordered = Nodes.Where(node => changed.Contains(node.Path)).Select(node => node.Id).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            var dependency = ordered[index];
            foreach (var dependent in Nodes
                         .Where(node => node.Dependencies.Contains(dependency) && affected.Add(node.Id))
                         .OrderBy(node => node.Path, StringComparer.Ordinal))
            {
                ordered.Add(dependent.Id);
            }
        }

        var order = ordered.Select((id, index) => (id, index)).ToDictionary(item => item.id, item => item.index);
        return Nodes.Where(node => affected.Contains(node.Id))
            .OrderBy(node => order[node.Id])
            .ThenBy(node => node.Path, StringComparer.Ordinal)
            .ToArray();
    }

    private static void EnsureAcyclic(IReadOnlyList<BuildDependencyNode> nodes)
    {
        var byId = nodes.ToDictionary(node => node.Id);
        var visiting = new HashSet<BuildResourceId>();
        var visited = new HashSet<BuildResourceId>();
        foreach (var node in nodes)
        {
            Visit(node);
        }

        void Visit(BuildDependencyNode node)
        {
            if (visited.Contains(node.Id))
            {
                return;
            }

            if (!visiting.Add(node.Id))
            {
                throw new ArgumentException("Build dependency graphs cannot contain cycles.", nameof(nodes));
            }

            foreach (var dependency in node.Dependencies)
            {
                Visit(byId[dependency]);
            }

            visiting.Remove(node.Id);
            visited.Add(node.Id);
        }
    }
}
