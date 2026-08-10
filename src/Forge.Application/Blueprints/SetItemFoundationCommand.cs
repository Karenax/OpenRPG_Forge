using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Items;

namespace Forge.Application.Blueprints;

public sealed record SetItemFoundationCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    BlueprintId BlueprintId,
    ItemFoundationProfile? Foundation,
    bool AcceptTypeMigration,
    bool DryRun);
