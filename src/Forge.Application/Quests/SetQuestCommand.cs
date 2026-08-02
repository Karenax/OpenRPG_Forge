using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Quests;

namespace Forge.Application.Quests;

public sealed record SetQuestCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    QuestDefinition Quest,
    bool DryRun);
