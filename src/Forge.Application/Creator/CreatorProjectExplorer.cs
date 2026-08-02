using Forge.Domain.Blueprints;
using Forge.Domain.Projects;

namespace Forge.Application.Creator;

public static class CreatorProjectExplorer
{
    public static CreatorExplorerNode Build(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var blueprintNames = project.Blueprints.ToDictionary(
            blueprint => blueprint.Id,
            blueprint => blueprint.DisplayName);
        var areaNodes = project.Areas
            .OrderBy(area => area.DisplayName, StringComparer.Ordinal)
            .Select(area => new CreatorExplorerNode(
                $"area:{area.Id}",
                CreatorExplorerNodeKind.Area,
                area.DisplayName,
                $"areas/{area.Id}",
                area.Id.ToString(),
                area.Entities.Select(entity => new CreatorExplorerNode(
                    $"entity:{entity.Id}",
                    CreatorExplorerNodeKind.Entity,
                    blueprintNames.TryGetValue(entity.BlueprintId, out var name)
                        ? name
                        : $"Blueprint manquant · {entity.BlueprintId}",
                    $"areas/{area.Id}/entities/{entity.Id}",
                    entity.Id.ToString())).ToArray()))
            .ToArray();

        var blueprintKindNodes = project.Blueprints
            .GroupBy(blueprint => blueprint.Kind)
            .OrderBy(group => FormatBlueprintKind(group.Key), StringComparer.Ordinal)
            .Select(group => Folder(
                $"folder:blueprints:{group.Key}",
                FormatBlueprintKind(group.Key),
                $"blueprints/kinds/{group.Key}",
                group.OrderBy(blueprint => blueprint.DisplayName, StringComparer.Ordinal)
                    .Select(blueprint => new CreatorExplorerNode(
                        $"blueprint:{blueprint.Id}",
                        CreatorExplorerNodeKind.Blueprint,
                        blueprint.DisplayName,
                        $"blueprints/{blueprint.Id}",
                        blueprint.Id.ToString()))
                    .ToArray()))
            .ToArray();

        var dialogueNodes = project.Dialogues
            .OrderBy(dialogue => dialogue.DisplayName, StringComparer.Ordinal)
            .Select(dialogue => new CreatorExplorerNode(
                $"dialogue:{dialogue.Id}",
                CreatorExplorerNodeKind.Dialogue,
                dialogue.DisplayName,
                $"dialogues/{dialogue.Id}",
                dialogue.Id.ToString()))
            .ToArray();
        var questNodes = project.Quests
            .OrderBy(quest => quest.DisplayName, StringComparer.Ordinal)
            .Select(quest => new CreatorExplorerNode(
                $"quest:{quest.Id}",
                CreatorExplorerNodeKind.Quest,
                quest.DisplayName,
                $"quests/{quest.Id}",
                quest.Id.ToString()))
            .ToArray();

        return new CreatorExplorerNode(
            $"project:{project.Id}",
            CreatorExplorerNodeKind.Project,
            project.Title,
            "project.json",
            project.Id.ToString(),
            [
                Folder("folder:areas", $"Zones · {project.Areas.Count}", "areas", areaNodes),
                Folder("folder:blueprints", $"Blueprints · {project.Blueprints.Count}", "blueprints", blueprintKindNodes),
                Folder("folder:dialogues", $"Dialogues · {project.Dialogues.Count}", "dialogues", dialogueNodes),
                Folder("folder:quests", $"Quêtes · {project.Quests.Count}", "quests", questNodes),
            ]);
    }

    private static CreatorExplorerNode Folder(
        string key,
        string displayName,
        string path,
        IReadOnlyList<CreatorExplorerNode> children) =>
        new(key, CreatorExplorerNodeKind.Folder, displayName, path, children: children);

    private static string FormatBlueprintKind(BlueprintKind kind) => kind switch
    {
        BlueprintKind.PlayerSpawn => "Points de départ",
        BlueprintKind.Placeable => "Décors",
        BlueprintKind.Door => "Portes",
        BlueprintKind.Creature => "Créatures",
        _ => kind.ToString(),
    };
}
