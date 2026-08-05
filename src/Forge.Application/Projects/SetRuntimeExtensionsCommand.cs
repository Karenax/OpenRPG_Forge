using Forge.Application.Commands;
using Forge.Domain.RuntimeExtensions;

namespace Forge.Application.Projects;

public sealed record SetRuntimeExtensionsCommand(CommandId CommandId, ProjectRevision BaseRevision,
    ModuleRuntimeExtensionProfile Profile, bool DryRun);
