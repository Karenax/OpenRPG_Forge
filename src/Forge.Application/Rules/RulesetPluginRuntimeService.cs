using Forge.Domain.Rules;

namespace Forge.Application.Rules;

public sealed record RulesetPluginRegistration(
    Guid PackageId,
    string Key,
    string Version,
    IReadOnlyList<string> Attributes,
    IReadOnlyList<string> Effects,
    IReadOnlyList<string> Abilities,
    IReadOnlyList<string> Inventories,
    IReadOnlyList<string> ProgressionTracks,
    IReadOnlyList<string> UiPanels);

public sealed record RulesetPluginResolution(bool Succeeded, bool ExtensionPresent,
    RulesetPluginRegistration? Registration, string? DiagnosticCode, string? Diagnostic);

public static class RulesetPluginRuntimeService
{
    public const string RequiredMissingCode = "rules.plugin.required_missing";
    public const string ContentMissingCode = "rules.plugin.content_missing";

    public static RulesetPluginResolution Resolve(RulesetPluginCatalog catalog,
        IReadOnlyCollection<string> availableContentPackages)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(availableContentPackages);
        if (!catalog.Enabled)
            return catalog.RequiredAtRuntime
                ? new(false, false, null, RequiredMissingCode, "Required ruleset plugin catalog is disabled.")
                : new(true, false, null, null, null);

        var package = catalog.Packages.SingleOrDefault(item => item.Key == catalog.ActivePackageKey);
        if (package is null)
            return catalog.RequiredAtRuntime
                ? new(false, false, null, RequiredMissingCode,
                    $"Required ruleset package '{catalog.ActivePackageKey}' is unavailable.")
                : new(true, false, null, null, null);
        if (!availableContentPackages.Contains(package.ContentPackageId, StringComparer.Ordinal))
            return catalog.RequiredAtRuntime
                ? new(false, false, null, ContentMissingCode,
                    $"Required content package '{package.ContentPackageId}' is unavailable.")
                : new(true, false, null, null, null);

        return new(true, true, new RulesetPluginRegistration(package.Id, package.Key, package.Version,
            package.Attributes, package.Effects, package.Abilities, package.Inventories, package.ProgressionTracks,
            package.UiPanels), null, null);
    }
}
