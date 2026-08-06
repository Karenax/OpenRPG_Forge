using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Quests;

namespace Forge.Application.Quests;

public sealed record SetQuestOperationsCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    QuestId QuestId,
    QuestOperationsProfile? Operations,
    bool DryRun);
