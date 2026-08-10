using Forge.Scripting;

namespace Forge.Application.Content;

public sealed record ContentResourceScriptSnapshot(
    Guid Id,
    string Address,
    string Kind,
    string PackageNamespace,
    string LicenseSpdx,
    string ContentSha256,
    string Status,
    int ReferenceCount);

/// <summary>Read-only namespaced content boundary for ForgeScript; no file or engine object is exposed.</summary>
public sealed class ContentResourceScriptService
{
    private readonly IReadOnlyDictionary<Guid, UnifiedContentBrowserItem> _items;
    private readonly Dictionary<string, Guid> _identifiers = new(StringComparer.Ordinal);
    private readonly ForgeScriptHandleRegistry _handles = new();
    private readonly Dictionary<Guid, ForgeScriptOpaqueHandle> _registered = [];

    public ContentResourceScriptService(UnifiedContentBrowser browser)
    {
        ArgumentNullException.ThrowIfNull(browser);
        _items = browser.Items.ToDictionary(item => item.Id);
        foreach (var item in browser.Items)
        {
            _identifiers.TryAdd(item.Address, item.Id);
            foreach (var alias in item.Aliases) _identifiers.TryAdd(alias, item.Id);
        }
    }

    public ForgeScriptOpaqueHandle? Find(string addressOrAlias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addressOrAlias);
        if (!_identifiers.TryGetValue(Forge.Domain.Content.ContentResourceRegistry.NormalizeAddress(addressOrAlias), out var id))
            return null;
        if (_registered.TryGetValue(id, out var existing)) return existing;
        var handle = _handles.Register(ForgeScriptHandleKind.ContentResource, id);
        _registered.Add(id, handle);
        return handle;
    }

    public ContentResourceScriptSnapshot Read(ForgeScriptOpaqueHandle handle)
    {
        if (!_handles.TryResolve(handle, ForgeScriptHandleKind.ContentResource, out var id, out var diagnostic))
            throw new InvalidOperationException(diagnostic!.Code);
        var item = _items[id];
        return new(item.Id, item.Address, item.Kind.ToString(), item.PackageNamespace, item.LicenseSpdx,
            item.ContentSha256, item.Status, item.References.Count);
    }
}
