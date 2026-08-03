using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Combat;

namespace Forge.Application.Blueprints;

public sealed record SetBlueprintCombatCommand(CommandId CommandId, ProjectRevision BaseRevision, BlueprintId BlueprintId, CombatProfile Combat, bool DryRun);
