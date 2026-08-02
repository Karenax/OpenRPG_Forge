using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.Dialogues;
using Forge.Domain.Quests;

namespace Forge.Domain.Validation;

public static class ProjectValidator
{
    public const string MissingBlueprintCode = "project.reference.blueprint_missing";
    public const string MissingEntryAreaCode = "project.reference.entry_area_missing";
    public const string MissingDialogueSpeakerBlueprintCode = "project.reference.dialogue_speaker_blueprint_missing";
    public const string DuplicatePortalCode = "area.portal.id_duplicate";
    public const string MissingPortalSourceCode = "area.portal.source_missing";
    public const string MissingPortalDestinationAreaCode = "area.portal.destination_area_missing";
    public const string MissingPortalDestinationEntityCode = "area.portal.destination_entity_missing";

    public static IReadOnlyList<DomainDiagnostic> Validate(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var diagnostics = new List<DomainDiagnostic>();
        var blueprintIds = project.Blueprints.Select(blueprint => blueprint.Id).ToHashSet();
        var areaIds = project.Areas.Select(area => area.Id).ToHashSet();
        var portalIds = new HashSet<PortalId>();

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

            for (var index = 0; index < area.Portals.Count; index++)
            {
                var portal = area.Portals[index];
                var path = $"areas/{area.Id}/portals/{index}";
                if (!portalIds.Add(portal.Id))
                {
                    diagnostics.Add(new DomainDiagnostic(
                        DuplicatePortalCode,
                        $"Portal '{portal.Id}' is declared more than once in the project.",
                        $"{path}/id"));
                }

                if (!area.Entities.Any(entity => entity.Id == portal.SourceEntityId))
                {
                    diagnostics.Add(new DomainDiagnostic(
                        MissingPortalSourceCode,
                        $"Portal '{portal.Id}' references missing source entity '{portal.SourceEntityId}'.",
                        $"{path}/sourceEntityId"));
                }

                var destinationArea = project.Areas.FirstOrDefault(candidate =>
                    candidate.Id == portal.DestinationAreaId);
                if (destinationArea is null)
                {
                    diagnostics.Add(new DomainDiagnostic(
                        MissingPortalDestinationAreaCode,
                        $"Portal '{portal.Id}' references missing destination area '{portal.DestinationAreaId}'.",
                        $"{path}/destinationAreaId"));
                }
                else if (!destinationArea.Entities.Any(entity => entity.Id == portal.DestinationEntityId))
                {
                    diagnostics.Add(new DomainDiagnostic(
                        MissingPortalDestinationEntityCode,
                        $"Portal '{portal.Id}' references missing destination entity '{portal.DestinationEntityId}'.",
                        $"{path}/destinationEntityId"));
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
