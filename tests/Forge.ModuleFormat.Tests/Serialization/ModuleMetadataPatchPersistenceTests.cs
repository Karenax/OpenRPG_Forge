using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class ModuleMetadataPatchPersistenceTests
{
    [Fact]
    public void LocalizedMetadataPatchRoundTripsWithoutAFormatMigration()
    {
        var source = TestProjectFactory.CreateValid();
        var locale = source.Module.Metadata.DefaultLocale;
        var metadata = ModuleMetadataPatch.Apply(source.Module.Metadata,
            new([ModuleMetadataPatchField.License], License: "CC-BY-4.0", Localizations:
            [
                new(locale,
                    [ProjectLocalizedMetadataPatchField.Summary,
                        ProjectLocalizedMetadataPatchField.Licenses,
                        ProjectLocalizedMetadataPatchField.Compatibility],
                    Summary: "Résumé persistant",
                    Licenses: "Crédits et licences détaillés",
                    Compatibility: "Forge 1.x"),
            ]));
        var module = source.Module;
        var updatedModule = new ModuleDefinition(module.Id, module.Slug, module.DisplayName, module.Description,
            module.EntryAreaId, metadata, module.Variables, module.Distribution, module.Localization,
            module.Content, module.ArtisticStyle, module.RuntimePolicies, module.Factions, module.WorldFeatures);
        var project = new ProjectDefinition(source.Id, source.Title, updatedModule, source.Blueprints,
            source.Areas, source.Dialogues, source.Quests);

        var restored = ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(project));
        var localized = restored.Module.Metadata.GetDefaultLocalization();

        Assert.Equal("CC-BY-4.0", restored.Module.Metadata.License);
        Assert.Equal("Résumé persistant", localized.Summary);
        Assert.Equal("Crédits et licences détaillés", localized.Licenses);
        Assert.Equal("Forge 1.x", localized.Compatibility);
        Assert.Equal("0.6.38", ProjectJsonSerializer.CurrentFormatVersion);
    }
}
