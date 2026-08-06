using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Dialogues;

namespace Forge.Application.Dialogues;

public sealed record SetDialoguePresentationCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    DialogueId DialogueId,
    DialoguePresentationProfile? Presentation,
    bool DryRun);
