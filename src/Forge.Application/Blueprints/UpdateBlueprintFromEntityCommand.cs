using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;

namespace Forge.Application.Blueprints;

public enum BlueprintVersionBump
{
    Patch,
    Minor,
    Major,
}

public sealed record UpdateBlueprintFromEntityCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    BlueprintId BlueprintId,
    EntityId SourceEntityId,
    IReadOnlyList<string> PromotedOverrideKeys,
    BlueprintVersionBump VersionBump,
    IReadOnlyList<EntityId> ExcludedInstanceIds,
    bool DryRun,
    IReadOnlyList<string>? CategoryPaths = null);

public sealed record BlueprintInstanceMigrationImpact(
    EntityId EntityId,
    string DocumentPath,
    string VersionRequirement,
    string Action);

public sealed record BlueprintUpdatePlan(
    BlueprintId BlueprintId,
    EntityId SourceEntityId,
    string PreviousVersion,
    string TargetVersion,
    IReadOnlyList<Forge.Domain.Entities.EntityPropertyOverride> PromotedDefaults,
    IReadOnlyList<BlueprintInstanceMigrationImpact> Instances);
