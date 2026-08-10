using System.Text.RegularExpressions;
using Forge.Domain.Validation;

namespace Forge.Domain.Content;

public enum ContentResourceKind
{
    Localization,
    Text,
    Script,
    Texture,
    Model,
    Asset,
}

public sealed record ContentResourceDescriptor(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Address,
    string PackageNamespace,
    ContentResourceKind Kind,
    string LogicalPath,
    string RelativePath,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> References,
    string LicenseSpdx,
    string ContentSha256,
    long PlatformByteBudget,
    bool Required);

public sealed record ContentResourceRegistryResult(
    IReadOnlyList<ContentResourceDescriptor> Resources,
    IReadOnlyList<DomainDiagnostic> Diagnostics)
{
    public bool Valid => Diagnostics.Count == 0;
}

public sealed partial class ContentResourceRegistry
{
    private readonly IReadOnlyDictionary<string, ContentResourceDescriptor> _byAddressOrAlias;

    private ContentResourceRegistry(IReadOnlyDictionary<string, ContentResourceDescriptor> byAddressOrAlias) =>
        _byAddressOrAlias = byAddressOrAlias;

    public static ContentResourceRegistryResult Build(ContentPackageCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var diagnostics = new List<DomainDiagnostic>();
        var resources = new List<ContentResourceDescriptor>();
        foreach (var package in catalog.Packages)
        {
            foreach (var source in package.Sources)
            {
                Add(package, source.Id, source.SchemaVersion, source.Provenance, source.LogicalResourceId,
                    source.RelativePath, SourceKind(source.Kind), source.Aliases, source.References, source.LicenseSpdx,
                    source.ContentSha256, source.PlatformByteBudget, source.Required);
            }

            foreach (var asset in package.Assets)
            {
                Add(package, asset.Id, asset.SchemaVersion, asset.Provenance, asset.LogicalResourceId,
                    asset.RelativePath, AssetKind(asset.RelativePath), asset.Aliases, asset.References,
                    asset.LicenseSpdx, asset.ContentSha256, asset.PlatformByteBudget, asset.Required);
            }
        }

        var identifiers = new Dictionary<string, ContentResourceDescriptor>(StringComparer.Ordinal);
        foreach (var resource in resources)
        {
            Register(resource.Address, resource, $"module/content/resources/{resource.Id}/address");
            foreach (var alias in resource.Aliases)
                Register(alias, resource, $"module/content/resources/{resource.Id}/aliases/{alias}");
        }

        foreach (var resource in resources)
        {
            foreach (var reference in resource.References.Where(reference => !identifiers.ContainsKey(reference)))
            {
                diagnostics.Add(new DomainDiagnostic(
                    ContentPackageCatalogValidator.ResourceReferenceCode,
                    $"Resource reference '{reference}' cannot be resolved.",
                    $"module/content/resources/{resource.Id}/references/{reference}"));
            }
        }

        return new ContentResourceRegistryResult(
            resources.OrderBy(item => item.Address, StringComparer.Ordinal).ToArray(),
            diagnostics.OrderBy(item => item.Path, StringComparer.Ordinal).ThenBy(item => item.Code, StringComparer.Ordinal).ToArray());

        void Add(
            ContentPackageDefinition package,
            Guid id,
            int schemaVersion,
            string provenance,
            string logicalResourceId,
            string relativePath,
            ContentResourceKind kind,
            IReadOnlyList<string>? aliases,
            IReadOnlyList<string>? references,
            string licenseSpdx,
            string contentSha256,
            long platformByteBudget,
            bool required)
        {
            var logicalPath = NormalizePath(logicalResourceId);
            var address = $"{package.PackageNamespace}:{KindName(kind)}/{logicalPath}";
            var normalizedAliases = (aliases ?? []).Select(NormalizeAddress).Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToArray();
            var normalizedReferences = (references ?? []).Select(NormalizeAddress).Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToArray();
            if (!AddressPattern().IsMatch(address) || normalizedAliases.Any(alias => !AddressPattern().IsMatch(alias)) ||
                normalizedReferences.Any(reference => !AddressPattern().IsMatch(reference)))
            {
                diagnostics.Add(new DomainDiagnostic(
                    ContentPackageCatalogValidator.ResourceAddressCode,
                    "Resource addresses and aliases must use package:type/path lowercase URI syntax.",
                    $"module/content/resources/{id}/address"));
            }

            resources.Add(new ContentResourceDescriptor(id, schemaVersion, provenance, address,
                package.PackageNamespace, kind, logicalPath, ContentPackageDefinition.NormalizePath(relativePath),
                normalizedAliases, normalizedReferences, licenseSpdx, contentSha256, platformByteBudget, required));
        }

        void Register(string identifier, ContentResourceDescriptor resource, string path)
        {
            if (!identifiers.TryAdd(identifier, resource) && identifiers[identifier].Id != resource.Id)
            {
                diagnostics.Add(new DomainDiagnostic(
                    ContentPackageCatalogValidator.ResourceAliasCollisionCode,
                    $"Resource identifier '{identifier}' is already owned by another UUID.",
                    path));
            }
        }
    }

    public static ContentResourceRegistry Create(ContentResourceRegistryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Valid) throw new ArgumentException("Cannot create a resource registry from invalid descriptors.", nameof(result));
        return new ContentResourceRegistry(result.Resources.SelectMany(resource =>
                resource.Aliases.Prepend(resource.Address).Select(identifier => (identifier, resource)))
            .ToDictionary(item => item.identifier, item => item.resource, StringComparer.Ordinal));
    }

    public bool TryResolve(string addressOrAlias, out ContentResourceDescriptor? resource) =>
        _byAddressOrAlias.TryGetValue(NormalizeAddress(addressOrAlias), out resource);

    private static ContentResourceKind SourceKind(ContentSourceKind kind) => kind == ContentSourceKind.ForgeScript
        ? ContentResourceKind.Script
        : ContentResourceKind.Text;

    private static ContentResourceKind AssetKind(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" or ".tif" or ".tiff" or ".exr" or ".ktx2" => ContentResourceKind.Texture,
        ".gltf" or ".glb" or ".obj" or ".fbx" => ContentResourceKind.Model,
        _ => ContentResourceKind.Asset,
    };

    private static string KindName(ContentResourceKind kind) => kind.ToString().ToLowerInvariant();

    private static string NormalizePath(string value) => (value ?? string.Empty).Trim().Replace('\\', '/')
        .Trim('/').ToLowerInvariant();

    public static string NormalizeAddress(string value) => (value ?? string.Empty).Trim().Replace('\\', '/')
        .Trim('/').ToLowerInvariant();

    [GeneratedRegex("^[a-z][a-z0-9]*(?:\\.[a-z][a-z0-9]*)*:[a-z][a-z0-9-]*/[a-z0-9][a-z0-9._/-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex AddressPattern();
}
