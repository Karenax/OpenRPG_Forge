using System.Text.Json.Nodes;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class ProjectFoundationPersistenceTests
{
    [Fact]
    public void ProjectFoundationRoundTripsExactlyWithPlayerStartAndIdentity()
    {
        var project = WithFoundation(TestProjectFactory.CreateValid());

        var serialized = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(serialized);

        Assert.Equal(serialized, ProjectJsonSerializer.Serialize(restored));
        var profile = Assert.IsType<ProjectFoundationProfile>(restored.Module.Metadata.Foundation);
        Assert.Equal(project.Id, profile.Identity.ProjectId);
        Assert.Equal("tests.crypt", profile.Identity.PackageNamespace);
        Assert.Equal(ProjectNetworkMode.DedicatedServer, profile.NetworkMode);
        Assert.Equal(restored.Areas[0].Id, Assert.Single(profile.PlayerStarts).AreaId);
    }

    [Fact]
    public void LegacyProjectWithoutFoundationRemainsSupportedAndOptionalAtRuntime()
    {
        var project = WithFoundation(TestProjectFactory.CreateValid());
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(project))!.AsObject();
        document["project"]!["module"]!["metadata"]!.AsObject().Remove("foundation");

        var restored = ProjectJsonSerializer.Deserialize(document.ToJsonString());
        var selection = PlayerStartProfileResolver.Resolve(restored.Module.Metadata.Foundation,
            new("legacy", "campaign", "adventurer", new HashSet<string>()));

        Assert.Null(restored.Module.Metadata.Foundation);
        Assert.True(selection.Succeeded);
        Assert.Null(selection.Profile);
    }

    internal static ProjectDefinition WithFoundation(ProjectDefinition source)
    {
        var blueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("d0240000-0000-4000-8000-000000000041")),
            "player-spawn", "Player spawn", BlueprintKind.PlayerSpawn);
        var entity = new EntityInstance(
            EntityId.From(Guid.Parse("d0240000-0000-4000-8000-000000000042")),
            blueprint.Id, TransformValue.Identity, ["player"]);
        var area = source.Areas[0].WithEntities([.. source.Areas[0].Entities, entity]);
        var foundation = ProjectFoundationProfile.CreateRpgDefault(
            Guid.Parse("d0240000-0000-4000-8000-000000000043"), source.Id, source.Module.Slug,
            area.Id, entity.Id, "tests.crypt") with
        {
            NetworkMode = ProjectNetworkMode.DedicatedServer,
        };
        var metadata = source.Module.Metadata;
        metadata = new ProjectMetadata(metadata.Id, metadata.DefaultLocale, metadata.Localizations,
            metadata.Author, metadata.Version, metadata.License, metadata.ImagePath, metadata.Tags,
            metadata.Classification, "r024-persistence", metadata.SchemaVersion, foundation);
        var module = new ModuleDefinition(source.Module.Id, source.Module.Slug, source.Module.DisplayName,
            source.Module.Description, area.Id, metadata, source.Module.Variables, source.Module.Distribution,
            source.Module.Localization, source.Module.Content, source.Module.ArtisticStyle, source.Module.RuntimePolicies,
            source.Module.Factions, source.Module.WorldFeatures);
        return new ProjectDefinition(source.Id, source.Title, module, [.. source.Blueprints, blueprint], [area],
            source.Dialogues, source.Quests);
    }
}
