using Forge.Domain.Dialogues;

namespace Forge.Application.Gameplay;

public sealed record RuntimeDialogueResolution(
    DialogueDefinition Dialogue,
    DialogueNodeId EntryNodeId);
