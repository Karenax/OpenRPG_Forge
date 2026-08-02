using Forge.Domain.Build;

namespace Forge.ModuleFormat.Build;

public static class WorkspaceIncrementalBuildPlanner
{
    public static IncrementalBuildPlan Plan(
        BuildDependencyGraph? previous,
        BuildDependencyGraph current)
    {
        ArgumentNullException.ThrowIfNull(current);
        var currentBuildable = current.Nodes
            .Where(node => node.Kind != BuildResourceKind.ExternalPackage)
            .ToArray();
        if (previous is null)
        {
            return new IncrementalBuildPlan(
                currentBuildable.Select(node => node.Path).ToArray(),
                [],
                currentBuildable,
                []);
        }

        var previousByPath = previous.Nodes.ToDictionary(node => node.Path, StringComparer.Ordinal);
        var currentByPath = current.Nodes.ToDictionary(node => node.Path, StringComparer.Ordinal);
        var changed = current.Nodes
            .Where(node => node.Kind != BuildResourceKind.ModulePackage)
            .Where(node => !previousByPath.TryGetValue(node.Path, out var prior) || !Equivalent(prior, node))
            .Select(node => node.Path)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var removed = previous.Nodes
            .Where(node => !currentByPath.ContainsKey(node.Path))
            .Select(node => node.Path)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var affectedInOrder = current.AffectedBy(changed).ToList();
        var affectedPaths = affectedInOrder.Select(node => node.Path).ToHashSet(StringComparer.Ordinal);
        foreach (var priorAffectedPath in previous.AffectedBy(removed).Select(node => node.Path))
        {
            if (currentByPath.ContainsKey(priorAffectedPath))
            {
                if (affectedPaths.Add(priorAffectedPath))
                {
                    affectedInOrder.Add(currentByPath[priorAffectedPath]);
                }
            }
        }

        var rebuilt = affectedInOrder.Where(node => node.Kind != BuildResourceKind.ExternalPackage).ToArray();
        var reused = currentBuildable.Where(node => !affectedPaths.Contains(node.Path)).ToArray();
        return new IncrementalBuildPlan(changed, removed, rebuilt, reused);
    }

    private static bool Equivalent(BuildDependencyNode previous, BuildDependencyNode current) =>
        previous.Kind == current.Kind &&
        string.Equals(previous.ContentSha256, current.ContentSha256, StringComparison.Ordinal) &&
        string.Equals(previous.LicenseSpdx, current.LicenseSpdx, StringComparison.Ordinal) &&
        string.Equals(previous.Provenance, current.Provenance, StringComparison.Ordinal) &&
        previous.Dependencies.SequenceEqual(current.Dependencies);
}
