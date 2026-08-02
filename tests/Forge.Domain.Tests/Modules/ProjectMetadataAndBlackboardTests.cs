using Forge.Domain.Modules;

namespace Forge.Domain.Tests.Modules;

public sealed class ProjectMetadataAndBlackboardTests
{
    [Fact]
    public void Metadata_RequiresAUniqueDefaultLocalizationAndNormalizesTags()
    {
        var metadata = CreateMetadata();

        Assert.Equal("fr", metadata.DefaultLocale);
        Assert.Equal("Les Îles", metadata.GetDefaultLocalization().Name);
        Assert.Equal(["Aventure", "coop"], metadata.Tags);
        Assert.Throws<ArgumentException>(() => new ProjectMetadata(
            metadata.Id,
            "de",
            metadata.Localizations,
            metadata.Author,
            metadata.Version,
            metadata.License,
            metadata.ImagePath,
            metadata.Tags,
            metadata.Classification,
            metadata.Provenance));
    }

    [Fact]
    public void VariableDefinition_NormalizesTypedDefaultsAndRejectsInvalidValues()
    {
        var boolean = Variable("world.doorOpen", ProjectVariableValueKind.Boolean, "TRUE");
        var number = Variable("world.threat", ProjectVariableValueKind.Number, "2.5000");

        Assert.Equal("true", boolean.DefaultValue);
        Assert.Equal("2.5", number.DefaultValue);
        Assert.Throws<ArgumentException>(() =>
            Variable("world.invalid", ProjectVariableValueKind.Integer, "many"));
    }

    [Fact]
    public void Blackboard_EnforcesDeclaredTypesAndCanFilterSnapshotsByScope()
    {
        var saved = Variable("world.threat", ProjectVariableValueKind.Integer, "1", ProjectVariableScope.Save);
        var session = Variable("session.wave", ProjectVariableValueKind.Integer, "0", ProjectVariableScope.Session);
        var blackboard = new ProjectBlackboard([saved, session]);

        Assert.True(blackboard.TrySet("world.threat", "4", out var acceptedError));
        Assert.False(blackboard.TrySet("world.threat", "high", out var typeError));
        Assert.False(blackboard.TrySet("unknown", "value", out var missingError));
        Assert.Null(acceptedError);
        Assert.NotNull(typeError);
        Assert.NotNull(missingError);
        Assert.Equal("4", blackboard.Snapshot(ProjectVariableScope.Save)["world.threat"]);
        Assert.DoesNotContain("session.wave", blackboard.Snapshot(ProjectVariableScope.Save));
    }

    [Fact]
    public void Module_DefaultsMetadataForLegacyDocumentsAndRejectsDuplicateVariableKeys()
    {
        var moduleId = ModuleId.From(Guid.Parse("c1000000-0000-4000-8000-000000000001"));
        var areaId = Forge.Domain.Areas.AreaId.From(Guid.Parse("c2000000-0000-4000-8000-000000000001"));
        var legacy = new ModuleDefinition(moduleId, "legacy", "Legacy", "Description", areaId);
        var duplicate = Variable("world.flag", ProjectVariableValueKind.Boolean, "false");

        Assert.Equal(moduleId.Value, legacy.Metadata.Id.Value);
        Assert.Equal("migration-default", legacy.Metadata.Provenance);
        Assert.Empty(legacy.Variables);
        Assert.Throws<ArgumentException>(() => new ModuleDefinition(
            moduleId,
            "invalid",
            "Invalid",
            string.Empty,
            areaId,
            CreateMetadata(),
            [duplicate, duplicate with { }]));
    }

    private static ProjectMetadata CreateMetadata() => new(
        ProjectMetadataId.From(Guid.Parse("c3000000-0000-4000-8000-000000000001")),
        "fr",
        [
            new ProjectLocalizedMetadata("fr", "Les Îles", "Aventure maritime", "Un archipel.", "Équipe Forge"),
            new ProjectLocalizedMetadata("en", "The Isles", "Sea adventure", "An archipelago.", "Forge Team"),
        ],
        "Atelier Forge",
        "1.2.0",
        "CC0-1.0",
        "assets/cover.png",
        ["coop", "Aventure", "coop"],
        "Tout public",
        "creator-command");

    private static ProjectVariableDefinition Variable(
        string key,
        ProjectVariableValueKind kind,
        string value,
        ProjectVariableScope scope = ProjectVariableScope.Save) =>
        new(
            ProjectVariableId.From(Guid.NewGuid()),
            key,
            kind,
            scope,
            value,
            "Variable de test",
            "test");
}
