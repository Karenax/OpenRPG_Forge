using Forge.Application.Localization;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class LocalizationPersistenceTests
{
    [Fact]
    public void SerializerRoundTripsCanonicalLocalizationCatalog()
    {
        var source = TestProjectFactory.CreateValid();
        var catalog = LocalizationCatalogExtractor.Extract(source).Catalog;
        var module = new ModuleDefinition(
            source.Module.Id, source.Module.Slug, source.Module.DisplayName, source.Module.Description,
            source.Module.EntryAreaId, source.Module.Metadata, source.Module.Variables,
            source.Module.Distribution, catalog);
        var project = new ProjectDefinition(
            source.Id, source.Title, module, source.Blueprints, source.Areas, source.Dialogues, source.Quests);

        var json = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(json);

        Assert.Equal("0.6.21", ProjectJsonSerializer.CurrentFormatVersion);
        Assert.Equal(catalog.Messages.Count, restored.Module.Localization.Messages.Count);
        Assert.Equal(catalog.Id, restored.Module.Localization.Id);
        Assert.Contains(restored.Module.Localization.Messages, message => message.Key == "dialogue.warden.warning");
    }
}
