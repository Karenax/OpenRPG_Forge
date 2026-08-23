using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Assets;
using Forge.Domain.Content;
using Forge.Domain.Localization;
using Forge.Domain.Projects;

namespace Forge.Application.Content;

public sealed record UnifiedContentBrowserQuery(
    string? Text = null,
    string? PackageNamespace = null,
    ContentResourceKind? Kind = null,
    string? LicenseSpdx = null,
    string? Status = null,
    int MaximumResults = 500);

public sealed record UnifiedContentBrowserItem(
    Guid Id,
    int SchemaVersion,
    string Address,
    IReadOnlyList<string> Aliases,
    string DisplayName,
    string PackageNamespace,
    ContentResourceKind Kind,
    string LicenseSpdx,
    string ContentSha256,
    string Provenance,
    string Status,
    string? SourcePath,
    string? PreviewPath,
    IReadOnlyList<string> References,
    IReadOnlyList<string> Actions,
    AssetCatalogItemId? AssetCatalogId = null);

public sealed class UnifiedContentBrowser
{
    private readonly IReadOnlyList<UnifiedContentBrowserItem> _items;

    public UnifiedContentBrowser(ProjectDefinition project, AssetCatalog? assetCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        var items = new List<UnifiedContentBrowserItem>();
        var packageResources = ContentResourceRegistry.Build(project.Module.Content.Packages);
        foreach (var resource in packageResources.Resources)
        {
            items.Add(new UnifiedContentBrowserItem(
                resource.Id, resource.SchemaVersion, resource.Address, resource.Aliases,
                resource.LogicalPath, resource.PackageNamespace, resource.Kind, resource.LicenseSpdx,
                resource.ContentSha256, resource.Provenance, resource.Required ? "required" : "optional",
                resource.RelativePath, PreviewFor(resource.Kind, resource.RelativePath), resource.References,
                ActionsFor(resource.Kind, canImport: true)));
        }

        foreach (var message in project.Module.Localization.Messages)
        {
            var address = $"{project.Module.Localization.PackageNamespace}:localization/{message.Key.ToLowerInvariant()}";
            items.Add(new UnifiedContentBrowserItem(
                StableGuid(address), LocalizationCatalog.CurrentSchemaVersion, address, [], message.Key,
                project.Module.Localization.PackageNamespace, ContentResourceKind.Localization,
                project.Module.Localization.LicenseSpdx, HashText(message.Fallback), message.Provenance,
                "translated", null, null, [], ["preview", "translate"]));
        }

        foreach (var asset in assetCatalog?.Items ?? [])
        {
            var kind = AssetKind(asset);
            var packageNamespace = NormalizeSegment(asset.Source);
            var path = NormalizePath(asset.SourceAssetId);
            var address = $"{packageNamespace}:{kind.ToString().ToLowerInvariant()}/{path}";
            items.Add(new UnifiedContentBrowserItem(
                asset.Id.Value, asset.SchemaVersion, address, [], asset.DisplayName, packageNamespace, kind,
                asset.LicenseSpdx, asset.ContentSha256, asset.Provenance,
                asset.Installed ? "installed" : asset.Availability.ToString().ToLowerInvariant(),
                asset.RelativeModelPath, asset.RelativePreviewPath, asset.InstalledPackIds,
                ActionsFor(kind, asset.CanPlanImport), asset.Id));
        }

        _items = items.GroupBy(item => item.Address, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(item => item.AssetCatalogId is null).First())
            .OrderBy(item => item.Address, StringComparer.Ordinal).ToArray();
    }

    public IReadOnlyList<UnifiedContentBrowserItem> Items => _items;

    public IReadOnlyList<string> PackageNamespaces => _items.Select(item => item.PackageNamespace)
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    public IReadOnlyList<ContentResourceKind> Kinds => _items.Select(item => item.Kind)
        .Distinct().Order().ToArray();

    public IReadOnlyList<string> Licenses => _items.Select(item => item.LicenseSpdx)
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    public IReadOnlyList<string> Statuses => _items.Select(item => item.Status)
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    public IReadOnlyList<UnifiedContentBrowserItem> Search(UnifiedContentBrowserQuery? query = null)
    {
        query ??= new UnifiedContentBrowserQuery();
        if (query.MaximumResults is < 1 or > 10_000)
            throw new ArgumentOutOfRangeException(nameof(query), "Maximum results must be between 1 and 10000.");
        var text = NormalizeSearch(query.Text);
        return _items
            .Where(item => string.IsNullOrEmpty(text) || NormalizeSearch(string.Join(' ', item.Address,
                string.Join(' ', item.Aliases), item.DisplayName, item.PackageNamespace, item.Kind,
                item.LicenseSpdx, item.Provenance, item.Status, item.SourcePath,
                string.Join(' ', item.References))).Contains(text, StringComparison.Ordinal))
            .Where(item => string.IsNullOrWhiteSpace(query.PackageNamespace) ||
                string.Equals(item.PackageNamespace, query.PackageNamespace.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(item => query.Kind is null || item.Kind == query.Kind)
            .Where(item => string.IsNullOrWhiteSpace(query.LicenseSpdx) ||
                string.Equals(item.LicenseSpdx, query.LicenseSpdx.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrWhiteSpace(query.Status) ||
                string.Equals(item.Status, query.Status.Trim(), StringComparison.OrdinalIgnoreCase))
            .Take(query.MaximumResults).ToArray();
    }

    private static IReadOnlyList<string> ActionsFor(ContentResourceKind kind, bool canImport)
    {
        var actions = new List<string> { "inspect-references" };
        if (kind is ContentResourceKind.Texture or ContentResourceKind.Model) actions.Add("preview");
        if (canImport) actions.Add("plan-import");
        return actions;
    }

    private static string? PreviewFor(ContentResourceKind kind, string path) =>
        kind is ContentResourceKind.Texture ? path : null;

    private static ContentResourceKind AssetKind(AssetCatalogItem asset)
    {
        var extension = Path.GetExtension(asset.RelativeModelPath ?? string.Empty).ToLowerInvariant();
        if (asset.Categories.Any(category =>
                category.Contains("texture", StringComparison.OrdinalIgnoreCase) ||
                category.Contains("material", StringComparison.OrdinalIgnoreCase)) ||
            extension is ".png" or ".jpg" or ".jpeg" or ".webp" or ".tif" or ".tiff" or ".exr" or ".ktx2")
            return ContentResourceKind.Texture;
        if (asset.Categories.Any(category => category.Contains("3d", StringComparison.OrdinalIgnoreCase)) ||
            extension is ".gltf" or ".glb" or ".obj" or ".fbx") return ContentResourceKind.Model;
        return ContentResourceKind.Asset;
    }

    private static string NormalizePath(string value) => string.Join('/', (value ?? string.Empty).Trim()
        .Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)
        .Select(NormalizeSegment)).Trim('/');

    private static string NormalizeSegment(string value)
    {
        var normalized = new string((value ?? string.Empty).Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-' ? character : '-')
            .ToArray()).Trim('-', '.');
        return string.IsNullOrEmpty(normalized) ? "resource" : normalized;
    }

    private static string NormalizeSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(character =>
                CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .Select(char.ToLowerInvariant).ToArray()).Normalize(NormalizationForm.FormC);
    }

    private static Guid StableGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:content-browser:{value}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }

    private static string HashText(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
