using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Projects;

namespace Forge.Application.Creator;

public sealed record CreatorWorkspaceResource(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    CreatorContentKind Kind,
    string DisplayName,
    string RelativePath,
    string SearchText,
    IReadOnlyList<string> Tags)
{
    public const int CurrentSchemaVersion = 1;

    public static CreatorWorkspaceResource Create(CreatorContentKind kind, string relativePath, string searchText,
        IReadOnlyList<string>? tags = null, string provenance = "workspace-canonical", Guid? id = null)
    {
        if (kind is not (CreatorContentKind.Script or CreatorContentKind.Asset))
            throw new ArgumentOutOfRangeException(nameof(kind));
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(searchText);
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        return new CreatorWorkspaceResource(id ?? Stable(normalized), CurrentSchemaVersion, provenance, kind,
            Path.GetFileName(normalized), normalized, searchText, tags ?? []);
    }

    private static Guid Stable(string path)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:resource:{path.ToLowerInvariant()}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }
}

public sealed record CreatorWorkspaceResourceCatalog(
    IReadOnlyList<CreatorSearchDocument> Documents,
    IReadOnlyList<CreatorReference> References)
{
    public static CreatorWorkspaceResourceCatalog Build(
        ProjectDefinition project,
        IReadOnlyList<CreatorWorkspaceResource> resources)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(resources);
        if (resources.Select(resource => resource.Id).Distinct().Count() != resources.Count ||
            resources.Any(resource => resource.Id == Guid.Empty ||
                resource.SchemaVersion != CreatorWorkspaceResource.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(resource.Provenance)))
            throw new ArgumentException("Workspace resources require unique UUIDs, schema v1 and provenance.", nameof(resources));

        var documents = resources.Select(resource => new CreatorSearchDocument(
            resource.Kind, resource.Id.ToString(), resource.DisplayName, resource.RelativePath,
            $"{resource.RelativePath} {resource.SearchText}", [.. resource.Tags, resource.Kind.ToString().ToLowerInvariant()],
            resource.Provenance)).ToArray();
        var references = new List<CreatorReference>();
        foreach (var target in resources)
        {
            var usage = target.Kind == CreatorContentKind.Script
                ? CreatorReferenceUsageKind.ScriptPath
                : CreatorReferenceUsageKind.AssetPath;
            foreach (var value in ProjectValues(project).Where(value =>
                         value.Value.Contains(target.RelativePath, StringComparison.OrdinalIgnoreCase)))
                references.Add(new CreatorReference(target.Kind, target.Id.ToString(), usage, value.Kind, value.SourceId,
                    value.DisplayName, value.Path, value.Selection));
            foreach (var source in resources.Where(source => source.Id != target.Id &&
                         source.SearchText.Contains(target.RelativePath, StringComparison.OrdinalIgnoreCase)))
                references.Add(new CreatorReference(target.Kind, target.Id.ToString(), usage, source.Kind,
                    source.Id.ToString(), source.DisplayName, source.RelativePath));
        }
        return new CreatorWorkspaceResourceCatalog(documents,
            references.OrderBy(item => item.DocumentPath, StringComparer.Ordinal).ThenBy(item => item.TargetId).ToArray());
    }

    private static IEnumerable<ResourceValue> ProjectValues(ProjectDefinition project)
    {
        foreach (var area in project.Areas)
            foreach (var entity in area.Entities)
                foreach (var property in entity.Overrides.Properties)
                    yield return new ResourceValue(property.Value, CreatorContentKind.Entity, entity.Id.ToString(),
                        $"{area.DisplayName} · {property.Key}", $"areas/{area.Id}/entities/{entity.Id}/properties/{property.Key}",
                        new CreatorSelection(CreatorSelectionKind.Entity, entity.Id.ToString(), property.Key,
                            $"areas/{area.Id}/entities/{entity.Id}"));
        foreach (var dialogue in project.Dialogues)
            foreach (var node in dialogue.Nodes)
            {
                foreach (var action in node.OnEnterActions)
                    yield return new ResourceValue($"{action.Key} {action.Value}", CreatorContentKind.DialogueNode,
                        node.Id.ToString(), dialogue.DisplayName,
                        $"dialogues/{dialogue.Id}/nodes/{node.Id}/onEnterActions/{action.Id}",
                        new CreatorSelection(CreatorSelectionKind.Dialogue, dialogue.Id.ToString(), dialogue.DisplayName,
                            $"dialogues/{dialogue.Id}"));
                foreach (var choice in node.Choices)
                    foreach (var value in choice.Conditions.Select(item => $"{item.Key} {item.ExpectedValue}")
                                 .Concat(choice.Actions.Select(item => $"{item.Key} {item.Value}")))
                        yield return new ResourceValue(value, CreatorContentKind.DialogueChoice, choice.Id.ToString(),
                            dialogue.DisplayName, $"dialogues/{dialogue.Id}/nodes/{node.Id}/choices/{choice.Id}",
                            new CreatorSelection(CreatorSelectionKind.Dialogue, dialogue.Id.ToString(), dialogue.DisplayName,
                                $"dialogues/{dialogue.Id}"));
            }
        foreach (var quest in project.Quests)
            foreach (var transition in quest.Transitions)
                foreach (var value in transition.Conditions.Select(item => $"{item.Key} {item.ExpectedValue}")
                             .Concat(transition.Actions.Select(item => $"{item.Key} {item.Value}")))
                    yield return new ResourceValue(value, CreatorContentKind.QuestTransition, transition.Id.ToString(),
                        quest.DisplayName, $"quests/{quest.Id}/transitions/{transition.Id}",
                        new CreatorSelection(CreatorSelectionKind.Quest, quest.Id.ToString(), quest.DisplayName,
                            $"quests/{quest.Id}"));
    }

    private sealed record ResourceValue(string Value, CreatorContentKind Kind, string SourceId,
        string DisplayName, string Path, CreatorSelection Selection);
}
