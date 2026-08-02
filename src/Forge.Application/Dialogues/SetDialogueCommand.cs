using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Dialogues;

namespace Forge.Application.Dialogues;

public sealed record SetDialogueCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    DialogueDefinition Dialogue,
    bool DryRun);
