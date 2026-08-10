using Forge.Domain.Localization;
using Forge.Scripting;

namespace Forge.Application.Localization;

public sealed record LocalizationScriptSnapshot(
    string Key,
    string Locale,
    string Text,
    string Provenance,
    string PackageNamespace,
    string ContentSha256);

/// <summary>Read-only localization boundary for ForgeScript; changes use SetLocalizationCatalogCommand.</summary>
public sealed class LocalizationScriptService
{
    private readonly LocalizationCatalog _catalog;
    private readonly ForgeScriptHandleRegistry _handles = new();
    private readonly Dictionary<string, ForgeScriptOpaqueHandle> _registered = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, string> _keys = [];

    public LocalizationScriptService(LocalizationCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    public ForgeScriptOpaqueHandle? Find(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var normalized = key.Trim();
        if (!_catalog.Messages.Any(message => message.Key == normalized)) return null;
        if (_registered.TryGetValue(normalized, out var handle)) return handle;
        var id = StableGuid(normalized);
        handle = _handles.Register(ForgeScriptHandleKind.LocalizationMessage, id);
        _registered.Add(normalized, handle);
        _keys.Add(id, normalized);
        return handle;
    }

    public LocalizationScriptSnapshot Read(
        ForgeScriptOpaqueHandle handle,
        string locale,
        IReadOnlyDictionary<string, object?>? arguments = null)
    {
        if (!_handles.TryResolve(handle, ForgeScriptHandleKind.LocalizationMessage, out var id, out var diagnostic))
            throw new InvalidOperationException(diagnostic!.Code);
        var key = _keys[id];
        var message = _catalog.Messages.Single(item => item.Key == key);
        return new(key, LocalizationCatalog.NormalizeLocale(locale),
            _catalog.Resolve(key, locale, arguments ?? new Dictionary<string, object?>()), message.Provenance,
            _catalog.PackageNamespace, _catalog.ContentSha256);
    }

    private Guid StableGuid(string key)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            $"openrpg-forge:localization:{_catalog.Id}:{key}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }
}
