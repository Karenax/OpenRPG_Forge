using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;

namespace Forge.Application.Blueprints;

public sealed record SetCreatureProgressionCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    BlueprintId BlueprintId,
    CreatureProgressionProfile? Progression,
    bool DryRun);
