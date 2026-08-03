using Forge.Domain.Localization;

namespace Forge.Domain.Tests.Localization;

public sealed class LocalizationCatalogTests
{
    [Fact]
    public void ResolveUsesExactLanguageDefaultAndFallbackInOrder()
    {
        var catalog = new LocalizationCatalog(
            LocalizationCatalogId.From(Guid.Parse("d1000000-0000-4000-8000-000000000001")),
            "fr",
            ["fr", "en-US", "de"],
            [new LocalizationMessage("dialogue.hello", "Bonjour", new Dictionary<string, string>
            {
                ["fr"] = "Salut",
                ["en-US"] = "Hello",
            })]);

        Assert.Equal("Hello", catalog.Resolve("dialogue.hello", "en-US"));
        Assert.Equal("Salut", catalog.Resolve("dialogue.hello", "fr-CA"));
        Assert.Equal("Salut", catalog.Resolve("dialogue.hello", "de"));
    }

    [Fact]
    public void CatalogRejectsDuplicateKeysAndUndeclaredTranslationLocales()
    {
        var id = LocalizationCatalogId.From(Guid.Parse("d1000000-0000-4000-8000-000000000002"));
        Assert.Throws<ArgumentException>(() => new LocalizationCatalog(
            id, "fr", ["fr"],
            [new LocalizationMessage("same", "A"), new LocalizationMessage("same", "B")]));
        Assert.Throws<ArgumentException>(() => new LocalizationCatalog(
            id, "fr", ["fr"],
            [new LocalizationMessage("key", "A", new Dictionary<string, string> { ["en"] = "A" })]));
    }
}
