using Forge.Domain.Assistant;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Assistant;

public static class LoreIndexProjectValidator
{
    public const string SourceUnresolvedCode = "assistant.lore.source_unresolved";

    public static IReadOnlyList<DomainDiagnostic> Validate(
        LoreIndexDefinition index,
        ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(project);
        var diagnostics = LoreIndexValidator.Validate(index).ToList();
        var catalog = LoreProjectSourceCatalog.Build(project);
        foreach (var entry in index.Entries)
        {
            foreach (var sourceId in entry.SourceIds)
            {
                if (!sourceId.StartsWith("manual:", StringComparison.Ordinal) && !catalog.TryGet(sourceId, out _))
                {
                    diagnostics.Add(new DomainDiagnostic(
                        SourceUnresolvedCode,
                        $"Lore source '{sourceId}' does not resolve in the current project.",
                        $"assistant/lore/entries/{entry.Id:D}/sourceIds"));
                }
            }
        }
        return diagnostics;
    }
}
