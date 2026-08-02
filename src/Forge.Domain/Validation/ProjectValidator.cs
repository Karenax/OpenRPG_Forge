using Forge.Domain.Projects;
using Forge.Domain.Dialogues;
using Forge.Domain.Quests;

namespace Forge.Domain.Validation;

public static class ProjectValidator
{
    public const string MissingBlueprintCode = "project.reference.blueprint_missing";
    public const string MissingEntryAreaCode = "project.reference.entry_area_missing";
    public const string MissingDialogueSpeakerBlueprintCode = "project.reference.dialogue_speaker_blueprint_missing";

    public static IReadOnlyList<DomainDiagnostic> Validate(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var diagnostics = new List<DomainDiagnostic>();
        var blueprintIds = project.Blueprints.Select(blueprint => blueprint.Id).ToHashSet();
        var areaIds = project.Areas.Select(area => area.Id).ToHashSet();

        if (!areaIds.Contains(project.Module.EntryAreaId))
        {
            diagnostics.Add(new DomainDiagnostic(
                MissingEntryAreaCode,
                $"Module '{project.Module.Id}' references missing entry area '{project.Module.EntryAreaId}'.",
                "module/entryAreaId"));
        }

        foreach (var area in project.Areas)
        {
            for (var index = 0; index < area.Entities.Count; index++)
            {
                var entity = area.Entities[index];
                if (!blueprintIds.Contains(entity.BlueprintId))
                {
                    diagnostics.Add(new DomainDiagnostic(
                        MissingBlueprintCode,
                        $"Entity '{entity.Id}' references missing blueprint '{entity.BlueprintId}'.",
                        $"areas/{area.Id}/entities/{index}/blueprintId"));
                }
            }
        }

        foreach (var dialogue in project.Dialogues)
        {
            diagnostics.AddRange(DialogueValidator.Validate(dialogue));
            foreach (var node in dialogue.Nodes.Where(node =>
                         node.SpeakerBlueprintId is not null && !blueprintIds.Contains(node.SpeakerBlueprintId)))
            {
                diagnostics.Add(new DomainDiagnostic(
                    MissingDialogueSpeakerBlueprintCode,
                    $"Dialogue node '{node.Id}' references missing speaker blueprint '{node.SpeakerBlueprintId}'.",
                    $"dialogues/{dialogue.Id}/nodes/{node.Id}/speakerBlueprintId"));
            }
        }

        foreach (var quest in project.Quests)
        {
            diagnostics.AddRange(QuestValidator.Validate(quest));
        }

        return diagnostics;
    }
}
