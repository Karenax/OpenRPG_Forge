using Forge.Domain.Entities;

namespace Forge.ModuleFormat.Playtesting;

public sealed record PlaytestSnapshot(
    string DirectoryPath,
    string ProjectFilePath,
    EntityId SpawnEntityId);
