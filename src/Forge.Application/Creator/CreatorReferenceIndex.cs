using Forge.Domain.Projects;

namespace Forge.Application.Creator;

public enum CreatorReferenceUsageKind
{
    ModuleEntryArea,
    EntityBlueprint,
    PortalSourceEntity,
    PortalDestinationArea,
    PortalDestinationEntity,
    DialogueEntryNode,
    DialogueSpeakerBlueprint,
    DialogueChoiceTargetNode,
    QuestInitialState,
    QuestTransitionFromState,
    QuestTransitionToState,
}

public sealed record CreatorReference(
    CreatorContentKind TargetKind,
    string TargetId,
    CreatorReferenceUsageKind Usage,
    CreatorContentKind SourceKind,
    string SourceId,
    string DisplayName,
    string DocumentPath,
    CreatorSelection? SourceSelection = null);

public sealed class CreatorReferenceIndex
{
    private readonly IReadOnlyDictionary<(CreatorContentKind Kind, string Id), IReadOnlyList<CreatorReference>> _references;

    private CreatorReferenceIndex(
        IReadOnlyDictionary<(CreatorContentKind Kind, string Id), IReadOnlyList<CreatorReference>> references)
    {
        _references = references;
    }

    public static CreatorReferenceIndex Build(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var references = new List<CreatorReference>
        {
            new(
                CreatorContentKind.Area,
                project.Module.EntryAreaId.ToString(),
                CreatorReferenceUsageKind.ModuleEntryArea,
                CreatorContentKind.Module,
                project.Module.Id.ToString(),
                $"{project.Module.DisplayName} · zone d’entrée",
                "module/entryAreaId"),
        };

        var blueprintNames = project.Blueprints.ToDictionary(blueprint => blueprint.Id, blueprint => blueprint.DisplayName);
        foreach (var area in project.Areas)
        {
            var areaSelection = new CreatorSelection(
                CreatorSelectionKind.Area,
                area.Id.ToString(),
                area.DisplayName,
                $"areas/{area.Id}");
            foreach (var entity in area.Entities)
            {
                references.Add(new CreatorReference(
                    CreatorContentKind.Blueprint,
                    entity.BlueprintId.ToString(),
                    CreatorReferenceUsageKind.EntityBlueprint,
                    CreatorContentKind.Entity,
                    entity.Id.ToString(),
                    $"{area.DisplayName} · {blueprintNames.GetValueOrDefault(entity.BlueprintId, entity.BlueprintId.ToString())}",
                    $"areas/{area.Id}/entities/{entity.Id}/blueprintId",
                    new CreatorSelection(
                        CreatorSelectionKind.Entity,
                        entity.Id.ToString(),
                        blueprintNames.GetValueOrDefault(entity.BlueprintId, entity.Id.ToString()),
                        $"areas/{area.Id}/entities/{entity.Id}")));
            }

            foreach (var portal in area.Portals)
            {
                var sourcePath = $"areas/{area.Id}/portals/{portal.Id}";
                references.Add(new CreatorReference(
                    CreatorContentKind.Entity,
                    portal.SourceEntityId.ToString(),
                    CreatorReferenceUsageKind.PortalSourceEntity,
                    CreatorContentKind.Portal,
                    portal.Id.ToString(),
                    $"{area.DisplayName} · {portal.DisplayName}",
                    $"{sourcePath}/sourceEntityId",
                    areaSelection));
                references.Add(new CreatorReference(
                    CreatorContentKind.Area,
                    portal.DestinationAreaId.ToString(),
                    CreatorReferenceUsageKind.PortalDestinationArea,
                    CreatorContentKind.Portal,
                    portal.Id.ToString(),
                    $"{area.DisplayName} · {portal.DisplayName}",
                    $"{sourcePath}/destinationAreaId",
                    areaSelection));
                references.Add(new CreatorReference(
                    CreatorContentKind.Entity,
                    portal.DestinationEntityId.ToString(),
                    CreatorReferenceUsageKind.PortalDestinationEntity,
                    CreatorContentKind.Portal,
                    portal.Id.ToString(),
                    $"{area.DisplayName} · {portal.DisplayName}",
                    $"{sourcePath}/destinationEntityId",
                    areaSelection));
            }
        }

        foreach (var dialogue in project.Dialogues)
        {
            var dialogueSelection = new CreatorSelection(
                CreatorSelectionKind.Dialogue,
                dialogue.Id.ToString(),
                dialogue.DisplayName,
                $"dialogues/{dialogue.Id}");
            references.Add(new CreatorReference(
                CreatorContentKind.DialogueNode,
                dialogue.EntryNodeId.ToString(),
                CreatorReferenceUsageKind.DialogueEntryNode,
                CreatorContentKind.Dialogue,
                dialogue.Id.ToString(),
                $"{dialogue.DisplayName} · nœud d’entrée",
                $"dialogues/{dialogue.Id}/entryNodeId",
                dialogueSelection));
            foreach (var node in dialogue.Nodes)
            {
                if (node.SpeakerBlueprintId is not null)
                {
                    references.Add(new CreatorReference(
                        CreatorContentKind.Blueprint,
                        node.SpeakerBlueprintId.ToString(),
                        CreatorReferenceUsageKind.DialogueSpeakerBlueprint,
                        CreatorContentKind.DialogueNode,
                        node.Id.ToString(),
                        $"{dialogue.DisplayName} · nœud {node.Id.ToString()[..8]}",
                        $"dialogues/{dialogue.Id}/nodes/{node.Id}/speakerBlueprintId",
                        dialogueSelection));
                }

                foreach (var choice in node.Choices.Where(choice => choice.TargetNodeId is not null))
                {
                    references.Add(new CreatorReference(
                        CreatorContentKind.DialogueNode,
                        choice.TargetNodeId!.ToString(),
                        CreatorReferenceUsageKind.DialogueChoiceTargetNode,
                        CreatorContentKind.DialogueChoice,
                        choice.Id.ToString(),
                        $"{dialogue.DisplayName} · choix {choice.Text.Fallback}",
                        $"dialogues/{dialogue.Id}/nodes/{node.Id}/choices/{choice.Id}/targetNodeId",
                        dialogueSelection));
                }
            }
        }

        foreach (var quest in project.Quests)
        {
            var questSelection = new CreatorSelection(
                CreatorSelectionKind.Quest,
                quest.Id.ToString(),
                quest.DisplayName,
                $"quests/{quest.Id}");
            references.Add(new CreatorReference(
                CreatorContentKind.QuestState,
                quest.InitialStateId.ToString(),
                CreatorReferenceUsageKind.QuestInitialState,
                CreatorContentKind.Quest,
                quest.Id.ToString(),
                $"{quest.DisplayName} · état initial",
                $"quests/{quest.Id}/initialStateId",
                questSelection));
            foreach (var transition in quest.Transitions)
            {
                var path = $"quests/{quest.Id}/transitions/{transition.Id}";
                references.Add(new CreatorReference(
                    CreatorContentKind.QuestState,
                    transition.FromStateId.ToString(),
                    CreatorReferenceUsageKind.QuestTransitionFromState,
                    CreatorContentKind.QuestTransition,
                    transition.Id.ToString(),
                    $"{quest.DisplayName} · départ de transition",
                    $"{path}/fromStateId",
                    questSelection));
                references.Add(new CreatorReference(
                    CreatorContentKind.QuestState,
                    transition.ToStateId.ToString(),
                    CreatorReferenceUsageKind.QuestTransitionToState,
                    CreatorContentKind.QuestTransition,
                    transition.Id.ToString(),
                    $"{quest.DisplayName} · arrivée de transition",
                    $"{path}/toStateId",
                    questSelection));
            }
        }

        return new CreatorReferenceIndex(references
            .GroupBy(reference => (reference.TargetKind, reference.TargetId))
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<CreatorReference>)group
                    .OrderBy(reference => reference.DocumentPath, StringComparer.Ordinal)
                    .ThenBy(reference => reference.Usage)
                    .ToArray()));
    }

    public IReadOnlyList<CreatorReference> Find(CreatorContentKind kind, string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _references.TryGetValue((kind, id.Trim()), out var references) ? references : [];
    }
}
