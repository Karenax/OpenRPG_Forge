using Forge.Domain.Dialogues;

namespace Forge.Application.Dialogues;

public sealed record DialogueGraphHandle(DialogueId Id, int SchemaVersion);

public sealed record DialogueGraphScriptSnapshot(
    DialogueGraphHandle Handle,
    string Slug,
    string DisplayName,
    DialogueNodeId EntryNodeId,
    int NodeCount,
    int EdgeCount,
    int ReferenceEdgeCount,
    int LocalizationVariantCount,
    int ConditionCount,
    int ActionCount,
    IReadOnlyList<DialogueNodeRole> Roles);

/// <summary>Read-only typed boundary used by ForgeScript adapters.</summary>
public sealed class DialogueGraphScriptService
{
    private readonly DialogueDefinition _dialogue;

    public DialogueGraphScriptService(DialogueDefinition dialogue)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        var diagnostics = DialogueValidator.Validate(dialogue);
        if (diagnostics.Count > 0)
            throw new ArgumentException(string.Join(';', diagnostics.Select(item => item.Code)), nameof(dialogue));
        _dialogue = dialogue;
    }

    public DialogueGraphHandle Handle => new(_dialogue.Id, _dialogue.SchemaVersion);

    public DialogueGraphScriptSnapshot Read(DialogueGraphHandle handle)
    {
        Ensure(handle);
        return new(handle, _dialogue.Slug, _dialogue.DisplayName, _dialogue.EntryNodeId,
            _dialogue.Nodes.Count, _dialogue.Edges.Count,
            _dialogue.Edges.Count(item => item.Kind == DialogueEdgeKind.Reference),
            _dialogue.Nodes.Sum(item => item.Text.Variants.Count + item.Choices.Sum(choice => choice.Text.Variants.Count)),
            _dialogue.Nodes.Sum(item => item.Choices.Sum(choice => choice.Conditions.Count)),
            _dialogue.Nodes.Sum(item => item.OnEnterActions.Count + item.Choices.Sum(choice => choice.Actions.Count)),
            _dialogue.Nodes.Select(item => item.Role).Distinct().Order().ToArray());
    }

    public DialogueNode ReadNode(DialogueGraphHandle handle, DialogueNodeId nodeId)
    {
        Ensure(handle);
        return _dialogue.Nodes.SingleOrDefault(item => item.Id == nodeId)
            ?? throw new KeyNotFoundException("DIALOGUE_NODE_MISSING");
    }

    private void Ensure(DialogueGraphHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (handle.Id != _dialogue.Id || handle.SchemaVersion != _dialogue.SchemaVersion)
            throw new InvalidOperationException("DIALOGUE_GRAPH_HANDLE_STALE");
    }
}
