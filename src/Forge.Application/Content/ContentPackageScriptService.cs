using Forge.Domain.Content;
using Forge.Scripting;

namespace Forge.Application.Content;

public sealed record ContentPackageScriptSnapshot(
    Guid Id,
    int SchemaVersion,
    string PackageNamespace,
    string Version,
    int Priority,
    int DependencyCount,
    int SourceCount,
    int AssetCount,
    int SealedResourceCount);

/// <summary>Read-only ForgeScript boundary; mutation remains a validated project command.</summary>
public sealed class ContentPackageScriptService
{
    private readonly ContentPackageCatalog _catalog;
    private readonly ForgeScriptHandleRegistry _handles = new();
    private readonly Dictionary<Guid, ForgeScriptOpaqueHandle> _registered = [];

    public ContentPackageScriptService(ContentPackageCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var diagnostics = ContentPackageCatalogValidator.Validate(catalog);
        if (diagnostics.Count > 0)
            throw new ArgumentException(string.Join(';', diagnostics.Select(item => item.Code)), nameof(catalog));
        _catalog = catalog;
    }

    public ForgeScriptOpaqueHandle? Find(string packageNamespace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageNamespace);
        var package = _catalog.Packages.SingleOrDefault(item =>
            string.Equals(item.PackageNamespace, packageNamespace.Trim(), StringComparison.Ordinal));
        if (package is null) return null;
        if (_registered.TryGetValue(package.Id, out var existing)) return existing;
        var handle = _handles.Register(ForgeScriptHandleKind.ContentPackage, package.Id);
        _registered.Add(package.Id, handle);
        return handle;
    }

    public ContentPackageScriptSnapshot Read(ForgeScriptOpaqueHandle handle)
    {
        if (!_handles.TryResolve(handle, ForgeScriptHandleKind.ContentPackage, out var id, out var diagnostic))
            throw new InvalidOperationException(diagnostic!.Code);
        var package = _catalog.Packages.Single(item => item.Id == id);
        return new(package.Id, package.SchemaVersion, package.PackageNamespace, package.Version, package.Priority,
            package.Dependencies.Count, package.Sources.Count, package.Assets.Count, package.SealedResources.Count);
    }
}
