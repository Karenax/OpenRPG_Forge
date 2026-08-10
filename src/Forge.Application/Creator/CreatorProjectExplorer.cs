using Forge.Domain.Blueprints;
using Forge.Domain.Authoring;
using Forge.Domain.Projects;

namespace Forge.Application.Creator;

public static class CreatorProjectExplorer
{
    public static CreatorExplorerNode Build(ProjectDefinition project, EditorExplorerGrouping grouping)
    {
        ArgumentNullException.ThrowIfNull(project);
        var canonical = Build(project);
        var folders = canonical.Children.ToDictionary(node => node.Key, StringComparer.Ordinal);
        var children = grouping switch
        {
            EditorExplorerGrouping.Folder => canonical.Children,
            EditorExplorerGrouping.Area => [folders["folder:areas"]],
            EditorExplorerGrouping.Type => BuildTypeView(folders),
            EditorExplorerGrouping.Tag => BuildTagView(project),
            EditorExplorerGrouping.Dependency => BuildDependencyView(project),
            EditorExplorerGrouping.ValidationStatus => BuildValidationView(project),
            _ => canonical.Children,
        };
        return new CreatorExplorerNode(
            $"project:{project.Id}", CreatorExplorerNodeKind.Project,
            $"{project.Title} · {FormatGrouping(grouping)}", "project.json", project.Id.ToString(), children);
    }

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

    private static IReadOnlyList<CreatorExplorerNode> BuildTypeView(
        IReadOnlyDictionary<string, CreatorExplorerNode> folders) =>
        folders["folder:blueprints"].Children
            .Concat([folders["folder:dialogues"], folders["folder:quests"]])
            .ToArray();

    private static IReadOnlyList<CreatorExplorerNode> BuildTagView(ProjectDefinition project)
    {
        var names = project.Blueprints.ToDictionary(item => item.Id, item => item.DisplayName);
        var tagged = project.Areas.SelectMany(area => area.Entities.SelectMany(entity => entity.Tags.Select(tag =>
            (Tag: tag.Trim().ToLowerInvariant(), Area: area, Entity: entity))))
            .Where(item => item.Tag.Length > 0)
            .GroupBy(item => item.Tag, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => Folder(
                $"tag:{group.Key}", $"#{group.Key} · {group.Count()}", $"virtual/tags/{group.Key}",
                group.OrderBy(item => names.GetValueOrDefault(item.Entity.BlueprintId), StringComparer.Ordinal)
                    .ThenBy(item => item.Entity.Id.Value)
                    .Select(item => new CreatorExplorerNode(
                        $"tag:{group.Key}:entity:{item.Entity.Id}", CreatorExplorerNodeKind.Entity,
                        names.GetValueOrDefault(item.Entity.BlueprintId, item.Entity.Id.ToString()[..8]),
                        $"areas/{item.Area.Id}/entities/{item.Entity.Id}", item.Entity.Id.ToString()))
                    .ToArray()))
            .ToArray();
        return tagged.Length == 0
            ? [Folder("tag:empty", "Aucun tag", "virtual/tags/empty", [])]
            : tagged;
    }

    private static IReadOnlyList<CreatorExplorerNode> BuildDependencyView(ProjectDefinition project)
    {
        var byManifest = project.Blueprints.ToDictionary(item => item.Authoring.ManifestPath, StringComparer.Ordinal);
        return project.Blueprints.OrderBy(item => item.DisplayName, StringComparer.Ordinal).Select(blueprint =>
            new CreatorExplorerNode(
                $"dependency:blueprint:{blueprint.Id}", CreatorExplorerNodeKind.Blueprint,
                $"{blueprint.DisplayName} · {blueprint.Authoring.Dependencies.Count}",
                $"blueprints/{blueprint.Id}", blueprint.Id.ToString(),
                blueprint.Authoring.Dependencies.Select((dependency, index) =>
                {
                    if (byManifest.TryGetValue(dependency, out var target))
                        return new CreatorExplorerNode(
                            $"dependency:{blueprint.Id}:blueprint:{target.Id}", CreatorExplorerNodeKind.Blueprint,
                            target.DisplayName, $"blueprints/{target.Id}", target.Id.ToString());
                    return Folder($"dependency:{blueprint.Id}:resource:{index}", dependency,
                        $"virtual/dependencies/{blueprint.Id}/{index}", []);
                }).ToArray())).ToArray();
    }

    private static IReadOnlyList<CreatorExplorerNode> BuildValidationView(ProjectDefinition project)
    {
        var issues = CreatorDiagnostics.FromProject(project, "explorer:validation");
        if (issues.Count == 0)
            return [Folder("validation:valid", "✓ Projet valide", "virtual/validation/valid", [])];
        return issues.GroupBy(item => item.Severity).OrderBy(group => group.Key).Select(group =>
            Folder($"validation:{group.Key}", $"{group.Key} · {group.Count()}",
                $"virtual/validation/{group.Key}",
                group.Select(item => DiagnosticNode(item)).ToArray())).ToArray();
    }

    private static CreatorExplorerNode DiagnosticNode(CreatorDiagnostic diagnostic)
    {
        var target = diagnostic.Target;
        var kind = target?.Kind switch
        {
            CreatorSelectionKind.Area => CreatorExplorerNodeKind.Area,
            CreatorSelectionKind.Entity => CreatorExplorerNodeKind.Entity,
            CreatorSelectionKind.Blueprint => CreatorExplorerNodeKind.Blueprint,
            CreatorSelectionKind.Dialogue => CreatorExplorerNodeKind.Dialogue,
            CreatorSelectionKind.Quest => CreatorExplorerNodeKind.Quest,
            _ => CreatorExplorerNodeKind.Project,
        };
        return new CreatorExplorerNode(
            $"validation:diagnostic:{diagnostic.Id}", kind,
            $"{diagnostic.Code} · {diagnostic.Message}", diagnostic.DocumentPath,
            target?.Id ?? diagnostic.SubjectUuid?.ToString() ?? diagnostic.Id);
    }

    private static string FormatGrouping(EditorExplorerGrouping grouping) => grouping switch
    {
        EditorExplorerGrouping.Folder => "par dossier",
        EditorExplorerGrouping.Area => "par zone",
        EditorExplorerGrouping.Tag => "par tag",
        EditorExplorerGrouping.Dependency => "par dépendance",
        EditorExplorerGrouping.ValidationStatus => "par validation",
        _ => "par type",
    };
}
