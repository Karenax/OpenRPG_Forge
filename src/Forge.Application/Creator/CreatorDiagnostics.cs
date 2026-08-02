using Forge.Domain.Projects;
using Forge.Domain.Entities;
using Forge.Domain.Validation;

namespace Forge.Application.Creator;

public static class CreatorDiagnostics
{
    public static IReadOnlyList<CreatorDiagnostic> FromProject(
        ProjectDefinition project,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        return ProjectDiagnosticCatalog.Build(project)
            .Select(diagnostic => CreatorDiagnostic.FromActionable(
                diagnostic,
                correlationId,
                ResolveTarget(project, diagnostic.DocumentPath)))
            .ToArray();
    }

    private static CreatorSelection ResolveTarget(ProjectDefinition project, string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 4 &&
            parts[0] == "areas" &&
            Guid.TryParse(parts[1], out var areaGuid) &&
            parts[2] == "entities")
        {
            var area = project.Areas.FirstOrDefault(candidate => candidate.Id.Value == areaGuid);
            EntityInstance? entity = null;
            if (area is not null && int.TryParse(parts[3], out var entityIndex) &&
                entityIndex >= 0 && entityIndex < area.Entities.Count)
            {
                entity = area.Entities[entityIndex];
            }
            else if (area is not null && Guid.TryParse(parts[3], out var entityGuid))
            {
                entity = area.Entities.FirstOrDefault(candidate => candidate.Id.Value == entityGuid);
            }

            if (entity is not null)
            {
                return new CreatorSelection(
                    CreatorSelectionKind.Entity,
                    entity.Id.ToString(),
                    $"Entité · {entity.Id.ToString()[..8]}",
                    $"areas/{area!.Id}/entities/{entity.Id}");
            }
        }

        if (parts.Length >= 2 && parts[0] == "areas" && Guid.TryParse(parts[1], out var areaTargetGuid))
        {
            var area = project.Areas.FirstOrDefault(candidate => candidate.Id.Value == areaTargetGuid);
            if (area is not null)
            {
                return new CreatorSelection(
                    CreatorSelectionKind.Area,
                    area.Id.ToString(),
                    area.DisplayName,
                    $"areas/{area.Id}");
            }
        }

        if (parts.Length >= 2 && parts[0] == "dialogues" && Guid.TryParse(parts[1], out var dialogueGuid))
        {
            var dialogue = project.Dialogues.FirstOrDefault(candidate => candidate.Id.Value == dialogueGuid);
            if (dialogue is not null)
            {
                return new CreatorSelection(
                    CreatorSelectionKind.Dialogue,
                    dialogue.Id.ToString(),
                    dialogue.DisplayName,
                    $"dialogues/{dialogue.Id}");
            }
        }

        if (parts.Length >= 2 && parts[0] == "quests" && Guid.TryParse(parts[1], out var questGuid))
        {
            var quest = project.Quests.FirstOrDefault(candidate => candidate.Id.Value == questGuid);
            if (quest is not null)
            {
                return new CreatorSelection(
                    CreatorSelectionKind.Quest,
                    quest.Id.ToString(),
                    quest.DisplayName,
                    $"quests/{quest.Id}");
            }
        }

        return new CreatorSelection(
            CreatorSelectionKind.Project,
            project.Id.ToString(),
            project.Title,
            "project.json");
    }
}
