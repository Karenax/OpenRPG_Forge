using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Quests;

namespace Forge.ModuleFormat.Serialization;

internal sealed record ProjectDocument(string FormatVersion, ProjectData Project);

internal sealed record ProjectData(
    ProjectId Id,
    string Title,
    ModuleDefinition Module,
    IReadOnlyList<BlueprintDefinition> Blueprints,
    IReadOnlyList<AreaDefinition> Areas,
    IReadOnlyList<DialogueDefinition>? Dialogues = null,
    IReadOnlyList<QuestDefinition>? Quests = null)
{
    internal static ProjectData FromDomain(ProjectDefinition project) =>
        new(
            project.Id,
            project.Title,
            project.Module,
            project.Blueprints,
            project.Areas,
            project.Dialogues,
            project.Quests);

    internal ProjectDefinition ToDomain() =>
        new(Id, Title, Module, Blueprints, Areas, Dialogues ?? [], Quests ?? []);
}

internal sealed record LegacyProjectDocument(string FormatVersion, LegacyProjectData Project);

internal sealed record LegacyProjectData(
    ProjectId Id,
    string Title,
    IReadOnlyList<BlueprintDefinition> Blueprints,
    IReadOnlyList<AreaDefinition> Areas)
{
    internal ProjectDefinition ToDomain() => new(Id, Title, Blueprints, Areas);
}
