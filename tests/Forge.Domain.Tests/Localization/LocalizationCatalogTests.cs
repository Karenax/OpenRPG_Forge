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

    [Fact]
    public void ResolveFormatsIcuPluralGenderAndPreservesDeterministicGovernance()
    {
        var catalog = new LocalizationCatalog(
            LocalizationCatalogId.From(Guid.Parse("d1000000-0000-4000-8000-000000000010")),
            "fr", ["fr", "en"],
            [new LocalizationMessage("loot.summary",
                "{gender, select, female {Elle} male {Il} other {Iel}} trouve {count, plural, =0 {aucun objet} one {# objet} other {# objets}}.",
                new Dictionary<string, string>
                {
                    ["en"] = "{gender, select, female {She} male {He} other {They}} finds {count, plural, =0 {no item} one {# item} other {# items}}.",
                })],
            packageNamespace: "crypt.localization", licenseSpdx: "CC0-1.0", provenance: "test:r017",
            platformByteBudget: 4096);

        var first = catalog.Resolve("loot.summary", "en-US", new Dictionary<string, object?>
        {
            ["gender"] = "female",
            ["count"] = 2,
        });
        var second = new LocalizationCatalog(catalog.Id, catalog.DefaultLocale, catalog.Locales, catalog.Messages,
            catalog.SchemaVersion, catalog.PackageNamespace, catalog.LicenseSpdx, catalog.Provenance,
            catalog.PlatformByteBudget);

        Assert.Equal("She finds 2 items.", first);
        Assert.Equal(catalog.ContentSha256, second.ContentSha256);
        Assert.Empty(LocalizationCatalogValidator.Validate(catalog, catalog.Id.Value));
    }

    [Fact]
    public void ValidatorRejectsInvalidIcuGovernanceAndPlatformBudget()
    {
        var catalog = new LocalizationCatalog(
            LocalizationCatalogId.From(Guid.Parse("d1000000-0000-4000-8000-000000000011")),
            "fr", ["fr"], [new LocalizationMessage("bad", "{count, plural, one {objet}}")],
            packageNamespace: "Bad Namespace", licenseSpdx: "UNLICENSED", provenance: "",
            platformByteBudget: 1);

        var diagnostics = LocalizationCatalogValidator.Validate(catalog,
            Guid.Parse("d1000000-0000-4000-8000-000000000012"));

        Assert.Contains(diagnostics, item => item.Code == LocalizationCatalogValidator.IdentityCode);
        Assert.Contains(diagnostics, item => item.Code == LocalizationCatalogValidator.GovernanceCode);
        Assert.Contains(diagnostics, item => item.Code == LocalizationCatalogValidator.MessageFormatCode);
        Assert.Contains(diagnostics, item => item.Code == LocalizationCatalogValidator.BudgetExceededCode);
    }
}
