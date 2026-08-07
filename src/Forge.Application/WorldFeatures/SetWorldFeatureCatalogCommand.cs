using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Application.WorldFeatures;

public sealed record SetWorldFeatureCatalogCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    WorldFeatureCatalog Catalog,
    bool DryRun);
