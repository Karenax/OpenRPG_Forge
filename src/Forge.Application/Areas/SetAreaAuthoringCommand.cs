using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
namespace Forge.Application.Areas;

public sealed record SetAreaAuthoringCommand(CommandId CommandId, ProjectRevision BaseRevision, AreaId AreaId, AreaAuthoringProfile Authoring, bool DryRun);
