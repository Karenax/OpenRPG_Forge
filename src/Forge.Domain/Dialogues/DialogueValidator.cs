using Forge.Domain.Validation;

namespace Forge.Domain.Dialogues;

public static class DialogueValidator
{
    public const string MissingEntryNodeCode = "dialogue.reference.entry_node_missing";
    public const string MissingTargetNodeCode = "dialogue.reference.target_node_missing";
    public const string UnreachableNodeCode = "dialogue.graph.node_unreachable";

    public static IReadOnlyList<DomainDiagnostic> Validate(DialogueDefinition dialogue)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        var diagnostics = new List<DomainDiagnostic>();
        var nodes = dialogue.Nodes.ToDictionary(node => node.Id);
        if (!nodes.ContainsKey(dialogue.EntryNodeId))
        {
            diagnostics.Add(new DomainDiagnostic(
                MissingEntryNodeCode,
                $"Dialogue '{dialogue.Id}' references missing entry node '{dialogue.EntryNodeId}'.",
                $"dialogues/{dialogue.Id}/entryNodeId"));
            return diagnostics;
        }

        foreach (var node in dialogue.Nodes)
        {
            foreach (var choice in node.Choices.Where(choice =>
                         choice.TargetNodeId is not null && !nodes.ContainsKey(choice.TargetNodeId)))
            {
                diagnostics.Add(new DomainDiagnostic(
                    MissingTargetNodeCode,
                    $"Choice '{choice.Id}' references missing target node '{choice.TargetNodeId}'.",
                    $"dialogues/{dialogue.Id}/nodes/{node.Id}/choices/{choice.Id}/targetNodeId"));
            }
        }

        var reachable = new HashSet<DialogueNodeId>();
        var pending = new Queue<DialogueNodeId>();
        pending.Enqueue(dialogue.EntryNodeId);
        while (pending.TryDequeue(out var nodeId) && reachable.Add(nodeId))
        {
            foreach (var target in nodes[nodeId].Choices
                         .Select(choice => choice.TargetNodeId)
                         .Where(target => target is not null && nodes.ContainsKey(target)))
            {
                pending.Enqueue(target!);
            }
        }

        foreach (var node in dialogue.Nodes.Where(node => !reachable.Contains(node.Id)))
        {
            diagnostics.Add(new DomainDiagnostic(
                UnreachableNodeCode,
                $"Dialogue node '{node.Id}' cannot be reached from the entry node.",
                $"dialogues/{dialogue.Id}/nodes/{node.Id}"));
        }

        diagnostics.AddRange(DialoguePresentationValidator.Validate(dialogue));
        return diagnostics;
    }
}
