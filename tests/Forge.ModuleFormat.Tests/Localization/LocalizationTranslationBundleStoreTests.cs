using Forge.Application.Localization;
using Forge.Domain.Localization;
using Forge.ModuleFormat.Localization;

namespace Forge.ModuleFormat.Tests.Localization;

public sealed class LocalizationTranslationBundleStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "openrpg-r017-localization", Guid.NewGuid().ToString("N"));

    [Fact]
    public void BundleStoreRoundTripsDeterministicallyWithoutBom()
    {
        var catalog = new LocalizationCatalog(
            LocalizationCatalogId.From(Guid.Parse("c0172000-0000-4000-8000-000000000001")), "fr", ["fr", "en"],
            [new LocalizationMessage("hello", "Bonjour", new Dictionary<string, string> { ["en"] = "Hello" })],
            packageNamespace: "crypt.localization", provenance: "test:r017");
        var bundle = LocalizationTranslationExchange.Export(catalog, "en");
        var first = Path.Combine(_root, "first.orfloc.json");
        var second = Path.Combine(_root, "second.orfloc.json");

        LocalizationTranslationBundleStore.Save(first, bundle);
        var restored = LocalizationTranslationBundleStore.Load(first);
        LocalizationTranslationBundleStore.Save(second, restored);

        Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
        Assert.False(File.ReadAllBytes(first).Take(3).SequenceEqual(new byte[] { 0xef, 0xbb, 0xbf }));
        Assert.Equal(catalog.ContentSha256, restored.CatalogContentSha256);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
