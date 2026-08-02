using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Quests;

namespace Forge.ModuleFormat.Serialization;

internal sealed record WorkspaceProjectDocument(string FormatVersion, WorkspaceProjectData Project);

internal sealed record WorkspaceProjectData(
    ProjectId Id,
    string Title,
    ModuleDefinition Module,
    IReadOnlyList<BlueprintDefinition> Blueprints,
    IReadOnlyList<AreaDocumentReference> Areas,
    IReadOnlyList<DialogueDefinition>? Dialogues = null,
    IReadOnlyList<QuestDefinition>? Quests = null);

internal sealed record AreaDocumentReference(AreaId Id, string Slug, string DocumentPath);

internal sealed record WorkspaceProjectBundle(
    ProjectDefinition Project,
    string ProjectJson,
    IReadOnlyDictionary<string, string> AreaDocuments);
