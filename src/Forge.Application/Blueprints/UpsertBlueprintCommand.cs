using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;

namespace Forge.Application.Blueprints;

public sealed record UpsertBlueprintCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    BlueprintId BlueprintId,
    string Slug,
    string DisplayName,
    BlueprintKind Kind,
    bool DryRun);
