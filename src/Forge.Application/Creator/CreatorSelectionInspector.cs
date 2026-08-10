using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;

namespace Forge.Application.Creator;

public sealed record CreatorCommonProperty(
    string Key,
    string DisplayName,
    string? Value,
    bool Mixed,
    bool Editable);

public sealed record CreatorSelectionInspection(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    IReadOnlyList<CreatorSelection> Selection,
    IReadOnlyList<CreatorCommonProperty> CommonProperties,
    IReadOnlyList<string> Diagnostics);

public static class CreatorSelectionInspector
{
    public static CreatorSelectionInspection Inspect(
        ProjectDefinition project,
        IReadOnlyList<CreatorSelection> selection,
        string provenance)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var entities = selection.Where(item => item.Kind == CreatorSelectionKind.Entity)
            .Select(item => TryFindEntity(project, item.Id)).Where(item => item is not null).Select(item => item!).ToArray();
        var diagnostics = selection.Count == 0
            ? new[] { "selection.empty" }
            : entities.Length != selection.Count
                ? new[] { "selection.reference_unresolved" }
                : [];
        var properties = entities.Length == 0 ? [] : new[]
        {
            Common("blueprintId", "Blueprint", entities.Select(item => item.BlueprintId.ToString()).ToArray(), false),
            Common("position.x", "Position X", entities.Select(item => item.Transform.Position.X.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).ToArray(), true),
            Common("position.y", "Position Y", entities.Select(item => item.Transform.Position.Y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).ToArray(), true),
            Common("position.z", "Position Z", entities.Select(item => item.Transform.Position.Z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).ToArray(), true),
            Common("scale.x", "Échelle X", entities.Select(item => item.Transform.Scale.X.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).ToArray(), true),
            Common("scale.y", "Échelle Y", entities.Select(item => item.Transform.Scale.Y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).ToArray(), true),
            Common("scale.z", "Échelle Z", entities.Select(item => item.Transform.Scale.Z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).ToArray(), true),
            new CreatorCommonProperty("tags", "Tags communs",
                string.Join(", ", entities.Select(item => item.Tags.AsEnumerable()).Aggregate((left, right) =>
                    left.Intersect(right, StringComparer.OrdinalIgnoreCase)).Order(StringComparer.OrdinalIgnoreCase)), false, false),
        };
        return new CreatorSelectionInspection(
            StableId(project.Id, selection), 1, provenance.Trim(), selection.ToArray(), properties, diagnostics);
    }

    private static CreatorCommonProperty Common(
        string key,
        string displayName,
        IReadOnlyList<string> values,
        bool editable)
    {
        var distinct = values.Distinct(StringComparer.Ordinal).ToArray();
        return new CreatorCommonProperty(key, displayName, distinct.Length == 1 ? distinct[0] : null,
            distinct.Length > 1, editable);
    }

    private static EntityInstance? TryFindEntity(ProjectDefinition project, string id) =>
        Guid.TryParse(id, out var value)
            ? project.Areas.SelectMany(area => area.Entities).FirstOrDefault(item => item.Id.Value == value)
            : null;

    private static Guid StableId(ProjectId projectId, IReadOnlyList<CreatorSelection> selection) => new(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            $"{projectId}:{string.Join('|', selection.Select(item => $"{item.Kind}:{item.Id}"))}"))[..16]);
}
