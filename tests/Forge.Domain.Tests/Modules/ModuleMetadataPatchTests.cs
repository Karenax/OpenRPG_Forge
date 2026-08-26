using Forge.Domain.Modules;

namespace Forge.Domain.Tests.Modules;

public sealed class ModuleMetadataPatchTests
{
    [Fact]
    public void LocalizedFieldsAreTypedAndOmittedMetadataIsPreserved()
    {
        var moduleId = ModuleId.From(Guid.Parse("18f6b01a-2baa-48fd-96f0-f1f3e6af3101"));
        var source = new ProjectMetadata(
            ProjectMetadataId.From(moduleId.Value),
            "fr",
            [
                new("fr", "Nom source", "Résumé source", "Description source", "Crédits source",
                    "Installation", "Règles", "Contrôles", "Licences source", "Avertissements",
                    "Compatibilité source"),
                new("en", "Source name", "Source summary", "Source description", "Source credits",
                    licenses: "Source licenses", compatibility: "Source compatibility"),
            ],
            "Auteur source", "1.2.3", "MIT", "images/source.png", ["rpg"], "Aventure", "test");
        var french = source.Localizations.Single(item => item.Locale == "fr");
        var english = source.Localizations.Single(item => item.Locale == "en");
        var patch = new ModuleMetadataPatch([], Localizations:
        [
            new("fr",
                [
                    ProjectLocalizedMetadataPatchField.Name,
                    ProjectLocalizedMetadataPatchField.Summary,
                    ProjectLocalizedMetadataPatchField.Description,
                    ProjectLocalizedMetadataPatchField.Credits,
                    ProjectLocalizedMetadataPatchField.Licenses,
                    ProjectLocalizedMetadataPatchField.Compatibility,
                ],
                Name: "Nouveau nom",
                Summary: "Nouveau résumé",
                Description: "Nouvelle description",
                Credits: "Nouveaux crédits",
                Licenses: "CC0 pour les contenus listés",
                Compatibility: "Forge 1.x"),
        ]);

        var updated = ModuleMetadataPatch.Apply(source, patch);
        var localized = updated.Localizations.Single(item => item.Locale == "fr");

        Assert.Equal("Nouveau nom", localized.Name);
        Assert.Equal("Nouveau résumé", localized.Summary);
        Assert.Equal("Nouvelle description", localized.Description);
        Assert.Equal("Nouveaux crédits", localized.Credits);
        Assert.Equal("CC0 pour les contenus listés", localized.Licenses);
        Assert.Equal("Forge 1.x", localized.Compatibility);
        Assert.Equal(french.Installation, localized.Installation);
        Assert.Equal(french.Rules, localized.Rules);
        Assert.Equal(french.Controls, localized.Controls);
        Assert.Equal(french.Warnings, localized.Warnings);
        Assert.Same(english, updated.Localizations.Single(item => item.Locale == "en"));
        Assert.Equal(source.Author, updated.Author);
        Assert.Equal(source.Version, updated.Version);
        Assert.Equal(source.License, updated.License);
        Assert.Equal(source.ImagePath, updated.ImagePath);
        Assert.Equal(source.Tags, updated.Tags);
        Assert.Equal(source.Classification, updated.Classification);
        Assert.Equal(source.Provenance, updated.Provenance);
        Assert.Same(source.Foundation, updated.Foundation);
    }

    [Fact]
    public void SummaryOnlyPatchDoesNotOverwriteAnyOtherLocalizedField()
    {
        var moduleId = ModuleId.From(Guid.Parse("18f6b01a-2baa-48fd-96f0-f1f3e6af3102"));
        var source = ProjectMetadata.CreateDefault(moduleId, "Nom", "Description");
        var before = source.GetDefaultLocalization();

        var updated = ModuleMetadataPatch.Apply(source,
            new([], Localizations:
            [
                new("fr", [ProjectLocalizedMetadataPatchField.Summary], Summary: "Résumé ciblé"),
            ]));
        var after = updated.GetDefaultLocalization();

        Assert.Equal("Résumé ciblé", after.Summary);
        Assert.Equal(before.Name, after.Name);
        Assert.Equal(before.Description, after.Description);
        Assert.Equal(before.Credits, after.Credits);
        Assert.Equal(before.Licenses, after.Licenses);
        Assert.Equal(before.Compatibility, after.Compatibility);
    }
}
