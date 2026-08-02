namespace Forge.Domain.Dialogues;

public sealed record DialogueDefinition
{
    public DialogueId Id { get; }

    public string Slug { get; }

    public string DisplayName { get; }

    public DialogueNodeId EntryNodeId { get; }

    public IReadOnlyList<DialogueNode> Nodes { get; }

    public DialogueDefinition(
        DialogueId id,
        string slug,
        string displayName,
        DialogueNodeId entryNodeId,
        IReadOnlyList<DialogueNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(entryNodeId);
        ArgumentNullException.ThrowIfNull(nodes);
        var materializedNodes = nodes.ToArray();
        if (materializedNodes.Select(node => node.Id).Distinct().Count() != materializedNodes.Length)
        {
            throw new ArgumentException("A dialogue cannot contain duplicate node IDs.", nameof(nodes));
        }

        var choiceIds = materializedNodes.SelectMany(node => node.Choices).Select(choice => choice.Id).ToArray();
        if (choiceIds.Distinct().Count() != choiceIds.Length)
        {
            throw new ArgumentException("Choice IDs must be unique inside a dialogue.", nameof(nodes));
        }

        Id = id;
        Slug = slug.Trim();
        DisplayName = displayName.Trim();
        EntryNodeId = entryNodeId;
        Nodes = materializedNodes;
    }
}
