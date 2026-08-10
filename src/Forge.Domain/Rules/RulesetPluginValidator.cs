using Forge.Domain.Validation;

namespace Forge.Domain.Rules;

public static class RulesetPluginValidator
{
    public const string CatalogIdentityCode = "rules.plugin.catalog_identity";
    public const string PackageDuplicateCode = "rules.plugin.package_duplicate";
    public const string ActiveMissingCode = "rules.plugin.active_missing";
    public const string RequiredDisabledCode = "rules.plugin.required_disabled";
    public const string RegistryMissingCode = "rules.plugin.registry_missing";

    public static IReadOnlyList<DomainDiagnostic> Validate(RulesetPluginCatalog catalog, string path)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var diagnostics = new List<DomainDiagnostic>();
        if (catalog.Id == Guid.Empty || catalog.SchemaVersion != RulesetPluginCatalog.CurrentSchemaVersion)
            diagnostics.Add(new(CatalogIdentityCode, "Ruleset plugin catalog requires a stable UUID and schema v1.",
                $"{path}/id"));
        if (catalog.RequiredAtRuntime && !catalog.Enabled)
            diagnostics.Add(new(RequiredDisabledCode, "A required ruleset plugin catalog cannot be disabled.",
                $"{path}/enabled"));
        foreach (var group in catalog.Packages.GroupBy(item => item.Id).Where(group => group.Count() > 1))
            diagnostics.Add(new(PackageDuplicateCode, $"Ruleset package UUID '{group.Key}' is duplicated.",
                $"{path}/packages/{group.Key}/id"));
        foreach (var group in catalog.Packages.GroupBy(item => item.Key, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
            diagnostics.Add(new(PackageDuplicateCode, $"Ruleset package key '{group.Key}' is duplicated.",
                $"{path}/packages/{group.First().Id}/key"));
        if (catalog.Enabled && catalog.Packages.All(item => item.Key != catalog.ActivePackageKey))
            diagnostics.Add(new(ActiveMissingCode,
                $"Active ruleset package '{catalog.ActivePackageKey}' does not exist.",
                $"{path}/activePackageKey"));
        foreach (var package in catalog.Packages)
        {
            if (package.Attributes.Count == 0 || package.Effects.Count == 0 || package.Abilities.Count == 0 ||
                package.Inventories.Count == 0 || package.ProgressionTracks.Count == 0 || package.UiPanels.Count == 0)
                diagnostics.Add(new(RegistryMissingCode,
                    $"Ruleset package '{package.Key}' must register attributes, effects, abilities, inventories, progression and UI.",
                    $"{path}/packages/{package.Id}"));
        }
        return diagnostics;
    }
}
