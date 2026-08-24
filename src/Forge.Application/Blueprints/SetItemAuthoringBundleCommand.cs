using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Items;

namespace Forge.Application.Blueprints;

public sealed record SetItemAuthoringBundleCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    BlueprintId BlueprintId,
    string Slug,
    string DisplayName,
    ItemFoundationProfile Foundation,
    ItemAdvancedProfile Advanced,
    bool AcceptTypeMigration,
    bool DryRun);
