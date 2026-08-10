using System.Security.Cryptography;
using System.Text;
using Forge.Application.Creator;
using Forge.Domain.Projects;

namespace Forge.Application.Assistant;

public sealed record AgentReferenceGraphNode(
    Guid Id,
    CreatorContentKind Kind,
    string DisplayName,
    string DocumentPath);

public sealed record AgentReferenceGraphEdge(
    Guid Id,
    Guid SourceId,
    Guid TargetId,
    CreatorReferenceUsageKind Usage,
    string DocumentPath);

public sealed record AgentReferenceImpact(
    IReadOnlyList<AgentReferenceGraphNode> Nodes,
    IReadOnlyList<AgentReferenceGraphEdge> Edges,
    IReadOnlyList<Guid> ImpactedObjectIds);

public static class AgentReferenceGraphBuilder
{
    public static AgentReferenceImpact Build(
        ProjectDefinition project,
        Guid? impactTargetId = null,
        int maximumDepth = 4,
        int maximumNodes = 500)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (maximumDepth is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(maximumDepth));
        if (maximumNodes is < 10 or > 1_000) throw new ArgumentOutOfRangeException(nameof(maximumNodes));

        var nodes = EnumerateNodes(project)
            .DistinctBy(item => item.Id)
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.DocumentPath, StringComparer.Ordinal)
            .Take(maximumNodes)
            .ToArray();
        var nodeIds = nodes.Select(item => item.Id).ToHashSet();
        var edges = CreatorReferenceIndex.Build(project).References
            .Select(ToEdge)
            .Where(item => item is not null && nodeIds.Contains(item.SourceId) && nodeIds.Contains(item.TargetId))
            .Select(item => item!)
            .OrderBy(item => item.DocumentPath, StringComparer.Ordinal)
            .ThenBy(item => item.Usage)
            .ToArray();
        var impacted = impactTargetId is { } target && nodeIds.Contains(target)
            ? FindReverseImpact(target, edges, maximumDepth, maximumNodes)
            : [];
        return new AgentReferenceImpact(nodes, edges, impacted);
    }

    private static IEnumerable<AgentReferenceGraphNode> EnumerateNodes(ProjectDefinition project)
    {
        yield return Node(project.Id.Value, CreatorContentKind.Project, project.Title, "project");
        yield return Node(project.Module.Id.Value, CreatorContentKind.Module, project.Module.DisplayName, "module");
        foreach (var blueprint in project.Blueprints)
            yield return Node(blueprint.Id.Value, CreatorContentKind.Blueprint, blueprint.DisplayName, $"blueprints/{blueprint.Id}");
        foreach (var area in project.Areas)
        {
            yield return Node(area.Id.Value, CreatorContentKind.Area, area.DisplayName, $"areas/{area.Id}");
            foreach (var entity in area.Entities)
                yield return Node(entity.Id.Value, CreatorContentKind.Entity, entity.Id.ToString(), $"areas/{area.Id}/entities/{entity.Id}");
            foreach (var portal in area.Portals)
                yield return Node(portal.Id.Value, CreatorContentKind.Portal, portal.DisplayName, $"areas/{area.Id}/portals/{portal.Id}");
        }
        foreach (var dialogue in project.Dialogues)
        {
            yield return Node(dialogue.Id.Value, CreatorContentKind.Dialogue, dialogue.DisplayName, $"dialogues/{dialogue.Id}");
            foreach (var node in dialogue.Nodes)
            {
                yield return Node(node.Id.Value, CreatorContentKind.DialogueNode, node.Text.Fallback, $"dialogues/{dialogue.Id}/nodes/{node.Id}");
                foreach (var choice in node.Choices)
                    yield return Node(choice.Id.Value, CreatorContentKind.DialogueChoice, choice.Text.Fallback, $"dialogues/{dialogue.Id}/nodes/{node.Id}/choices/{choice.Id}");
            }
        }
        foreach (var quest in project.Quests)
        {
            yield return Node(quest.Id.Value, CreatorContentKind.Quest, quest.DisplayName, $"quests/{quest.Id}");
            foreach (var state in quest.States)
                yield return Node(state.Id.Value, CreatorContentKind.QuestState, state.DisplayName, $"quests/{quest.Id}/states/{state.Id}");
            foreach (var transition in quest.Transitions)
                yield return Node(transition.Id.Value, CreatorContentKind.QuestTransition,
                    $"{quest.DisplayName} transition {transition.Id.ToString()[..8]}",
                    $"quests/{quest.Id}/transitions/{transition.Id}");
        }
    }

    private static AgentReferenceGraphNode Node(Guid id, CreatorContentKind kind, string displayName, string path) =>
        new(id, kind, string.IsNullOrWhiteSpace(displayName) ? id.ToString("D") : displayName, path);

    private static AgentReferenceGraphEdge? ToEdge(CreatorReference reference)
    {
        if (!Guid.TryParse(reference.SourceId, out var sourceId) || !Guid.TryParse(reference.TargetId, out var targetId))
            return null;
        return new AgentReferenceGraphEdge(
            StableGuid($"{sourceId:D}|{targetId:D}|{reference.Usage}|{reference.DocumentPath}"),
            sourceId,
            targetId,
            reference.Usage,
            reference.DocumentPath);
    }

    private static IReadOnlyList<Guid> FindReverseImpact(
        Guid target,
        IReadOnlyList<AgentReferenceGraphEdge> edges,
        int maximumDepth,
        int maximumNodes)
    {
        var seen = new HashSet<Guid> { target };
        var frontier = new Queue<(Guid Id, int Depth)>();
        frontier.Enqueue((target, 0));
        while (frontier.Count > 0 && seen.Count < maximumNodes)
        {
            var current = frontier.Dequeue();
            if (current.Depth >= maximumDepth) continue;
            foreach (var source in edges.Where(edge => edge.TargetId == current.Id)
                         .Select(edge => edge.SourceId).Distinct().Order())
            {
                if (!seen.Add(source)) continue;
                frontier.Enqueue((source, current.Depth + 1));
                if (seen.Count >= maximumNodes) break;
            }
        }
        return seen.Where(id => id != target).Order().ToArray();
    }

    public static Guid StableGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[6] = (byte)((guidBytes[6] & 0x0f) | 0x40);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3f) | 0x80);
        return new Guid(guidBytes);
    }
}
