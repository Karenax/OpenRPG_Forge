using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Localization;

namespace Forge.Application.Localization;

public sealed record SetLocalizationCatalogCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    LocalizationCatalog Catalog,
    bool DryRun);
