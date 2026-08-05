using Forge.Application.Commands;
using Forge.Domain.Gameplay;

namespace Forge.Application.Projects;

public sealed record SetGameplayControlProfileCommand(CommandId CommandId, ProjectRevision BaseRevision,
    ModuleGameplayControlProfile Profile, bool DryRun);
