namespace Forge.Domain.Blueprints;

public sealed record BlueprintCreditEntry(
    string Author,
    string LicenseSpdx,
    string PermissionEvidence,
    IReadOnlyList<string> Sources,
    IReadOnlyList<BlueprintId> BlueprintIds,
    bool IncludesAiGeneratedContent);

public static class BlueprintCreditCatalog
{
    public static IReadOnlyList<BlueprintCreditEntry> Aggregate(IEnumerable<BlueprintDefinition> blueprints)
    {
        ArgumentNullException.ThrowIfNull(blueprints);
        return blueprints
            .GroupBy(blueprint => new
            {
                blueprint.Provenance.Author,
                blueprint.Provenance.LicenseSpdx,
                blueprint.Provenance.PermissionEvidence,
            })
            .OrderBy(group => group.Key.Author, StringComparer.Ordinal)
            .ThenBy(group => group.Key.LicenseSpdx, StringComparer.Ordinal)
            .ThenBy(group => group.Key.PermissionEvidence, StringComparer.Ordinal)
            .Select(group => new BlueprintCreditEntry(
                group.Key.Author,
                group.Key.LicenseSpdx,
                group.Key.PermissionEvidence,
                group.Select(item => item.Provenance.Source).Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal).ToArray(),
                group.Select(item => item.Id).Distinct().OrderBy(item => item.Value).ToArray(),
                group.Any(item => item.Provenance.IsAiGenerated)))
            .ToArray();
    }
}
