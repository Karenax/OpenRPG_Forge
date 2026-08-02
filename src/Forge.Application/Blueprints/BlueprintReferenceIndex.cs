using Forge.Domain.Blueprints;
using Forge.Domain.Projects;

namespace Forge.Application.Blueprints;

public sealed class BlueprintReferenceIndex
{
    private readonly IReadOnlyDictionary<BlueprintId, IReadOnlyList<BlueprintReference>> _references;

    private BlueprintReferenceIndex(IReadOnlyDictionary<BlueprintId, IReadOnlyList<BlueprintReference>> references)
    {
        _references = references;
    }

    public static BlueprintReferenceIndex Build(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var references = new List<BlueprintReference>();
        foreach (var area in project.Areas)
        {
            foreach (var entity in area.Entities)
            {
                references.Add(new BlueprintReference(
                    entity.BlueprintId,
                    BlueprintReferenceKind.EntityInstance,
                    entity.Id.ToString(),
                    $"{area.DisplayName} · entité {entity.Id.ToString()[..8]}",
                    $"areas/{area.Id}/entities/{entity.Id}/blueprintId"));
            }
        }

        foreach (var dialogue in project.Dialogues)
        {
            foreach (var node in dialogue.Nodes.Where(node => node.SpeakerBlueprintId is not null))
            {
                references.Add(new BlueprintReference(
                    node.SpeakerBlueprintId!,
                    BlueprintReferenceKind.DialogueSpeaker,
                    node.Id.ToString(),
                    $"{dialogue.DisplayName} · nœud {node.Id.ToString()[..8]}",
                    $"dialogues/{dialogue.Id}/nodes/{node.Id}/speakerBlueprintId"));
            }
        }

        return new BlueprintReferenceIndex(references
            .GroupBy(reference => reference.BlueprintId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<BlueprintReference>)group
                    .OrderBy(reference => reference.DocumentPath, StringComparer.Ordinal)
                    .ToArray()));
    }

    public IReadOnlyList<BlueprintReference> Find(BlueprintId blueprintId) =>
        _references.TryGetValue(blueprintId, out var references) ? references : [];
}
