using Forge.Domain.Projects;

namespace Forge.Application.Creator;

public sealed record SafeRenamePreview(
    CreatorContentKind TargetKind,
    string TargetId,
    string PreviousDisplayName,
    string NewDisplayName,
    string? PreviousSlug,
    string? NewSlug,
    string DocumentPath,
    int PreservedReferenceCount)
{
    public static SafeRenamePreview? Create(
        ProjectDefinition project,
        CreatorContentKind targetKind,
        string targetId,
        string displayName,
        string? slug)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        var identity = targetId.Trim();
        var target = FindTarget(project, targetKind, identity);
        if (target is null)
        {
            return null;
        }

        var referenceCount = CreatorReferenceIndex.Build(project).Find(targetKind, identity).Count;
        return new SafeRenamePreview(
            targetKind,
            identity,
            target.Value.DisplayName,
            displayName.Trim(),
            target.Value.Slug,
            slug?.Trim(),
            target.Value.DocumentPath,
            referenceCount);
    }

    internal static (string DisplayName, string? Slug, string DocumentPath)? FindTarget(
        ProjectDefinition project,
        CreatorContentKind targetKind,
        string targetId) =>
        targetKind switch
        {
            CreatorContentKind.Project when project.Id.ToString() == targetId =>
                (project.Title, null, "project.json"),
            CreatorContentKind.Module when project.Module.Id.ToString() == targetId =>
                (project.Module.DisplayName, project.Module.Slug, "module"),
            CreatorContentKind.Area => project.Areas
                .Where(area => area.Id.ToString() == targetId)
                .Select(area => ((string DisplayName, string? Slug, string DocumentPath)?)(
                    area.DisplayName, area.Slug, $"areas/{area.Id}"))
                .FirstOrDefault(),
            CreatorContentKind.Blueprint => project.Blueprints
                .Where(blueprint => blueprint.Id.ToString() == targetId)
                .Select(blueprint => ((string DisplayName, string? Slug, string DocumentPath)?)(
                    blueprint.DisplayName, blueprint.Slug, $"blueprints/{blueprint.Id}"))
                .FirstOrDefault(),
            CreatorContentKind.Dialogue => project.Dialogues
                .Where(dialogue => dialogue.Id.ToString() == targetId)
                .Select(dialogue => ((string DisplayName, string? Slug, string DocumentPath)?)(
                    dialogue.DisplayName, dialogue.Slug, $"dialogues/{dialogue.Id}"))
                .FirstOrDefault(),
            CreatorContentKind.Quest => project.Quests
                .Where(quest => quest.Id.ToString() == targetId)
                .Select(quest => ((string DisplayName, string? Slug, string DocumentPath)?)(
                    quest.DisplayName, quest.Slug, $"quests/{quest.Id}"))
                .FirstOrDefault(),
            _ => null,
        };
}
