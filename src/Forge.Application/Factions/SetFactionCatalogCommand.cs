using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Factions;

namespace Forge.Application.Factions;

public sealed record SetFactionCatalogCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    FactionCatalog Catalog,
    bool DryRun);
