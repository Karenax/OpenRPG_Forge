using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Application.Creator;

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
        var generic = CreatorReferenceIndex.Build(project);
        var references = project.Blueprints
            .SelectMany(blueprint => generic.Find(CreatorContentKind.Blueprint, blueprint.Id.ToString())
                .Where(reference => reference.Usage is
                    CreatorReferenceUsageKind.EntityBlueprint or
                    CreatorReferenceUsageKind.DialogueSpeakerBlueprint)
                .Select(reference => new BlueprintReference(
                    blueprint.Id,
                    reference.Usage == CreatorReferenceUsageKind.EntityBlueprint
                        ? BlueprintReferenceKind.EntityInstance
                        : BlueprintReferenceKind.DialogueSpeaker,
                    reference.SourceId,
                    reference.DisplayName,
                    reference.DocumentPath)))
            .ToArray();

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
