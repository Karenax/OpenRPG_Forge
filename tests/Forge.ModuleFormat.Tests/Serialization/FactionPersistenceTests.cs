using System.Text.Json.Nodes;
using Forge.Domain.Factions;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class FactionPersistenceTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(), "openrpg-forge-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void AggregateJsonRoundTripPreservesAdvancedFactionProfileExactly()
    {
        var source = TestProjectFactory.CreateValid();
        var catalog = FactionPersistenceTestData.Complete();
        var project = new ProjectDefinition(source.Id, source.Title, source.Module.WithFactions(catalog),
            source.Blueprints, source.Areas, source.Dialogues, source.Quests);

        var json = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(json);

        Assert.Contains("\"formatVersion\": \"0.6.38\"", json, StringComparison.Ordinal);
        Assert.Equal(json, ProjectJsonSerializer.Serialize(restored));
        Assert.Equal("Ember Pilgrim", restored.Module.Factions.Advanced!.Memberships[1].CoverIdentity);
        Assert.Equal(FactionDiplomacyStatus.War,
            Assert.Single(Assert.Single(restored.Module.Factions.Advanced.DynamicFactions).Diplomacy).Status);
        var personal = Assert.Single(restored.Module.Factions.EffectivePersonalReputation);
        Assert.Equal(PersonalReputationSubjectKind.Player, personal.SubjectKind);
        Assert.Equal("earned-trust", personal.Cause);
        Assert.True(restored.Module.Factions.RequireFoundationRuntime);
        Assert.True(restored.Module.Factions.RequireReputationRuntime);
        Assert.Equal(ReputationScope.Player, restored.Module.Factions.Reputation!.Reaction.DefaultScope);
        Assert.Equal("saved-guild", Assert.Single(restored.Module.Factions.Reputation.Transactions).Cause);
        Assert.Equal("before", Assert.Single(restored.Module.Factions.Reputation.HeatmapHistory).Label);
    }

    [Fact]
    public void WorkspaceSaveCloseAndReloadPreservesCanonicalFactions()
    {
        var source = TestProjectFactory.CreateValid();
        var catalog = FactionPersistenceTestData.Complete();
        var project = new ProjectDefinition(source.Id, source.Title, source.Module.WithFactions(catalog),
            source.Blueprints, source.Areas, source.Dialogues, source.Quests);
        var workspace = Path.Combine(_temporaryRoot, "workspace");

        var created = ProjectSession.Create(workspace, project);
        var reopened = ProjectSession.Open(workspace);

        Assert.True(created.Succeeded);
        Assert.True(reopened.Succeeded);
        Assert.Equal(ProjectJsonSerializer.Serialize(project),
            ProjectJsonSerializer.Serialize(reopened.Session!.Project));
        Assert.Contains("\"formatVersion\": \"1.30.0\"",
            File.ReadAllText(reopened.Session.ProjectFilePath), StringComparison.Ordinal);
    }

    [Fact]
    public void PreAdvancedFactionDocumentWithoutCatalogMigratesToOptionalEmptyState()
    {
        var source = TestProjectFactory.CreateValid();
        var node = JsonNode.Parse(ProjectJsonSerializer.Serialize(source))!.AsObject();
        node["formatVersion"] = ProjectJsonSerializer.PreAdvancedFactionsFormatVersion;
        node["project"]!.AsObject()["module"]!.AsObject().Remove("factions");

        var restored = ProjectJsonSerializer.Deserialize(node.ToJsonString());

        Assert.Empty(restored.Module.Factions.Factions);
        Assert.Null(restored.Module.Factions.Advanced);
        Assert.False(restored.Module.Factions.RequireAdvancedRuntime);
        Assert.Empty(restored.Module.Factions.EffectivePersonalReputation);
        Assert.False(restored.Module.Factions.RequireFoundationRuntime);
        Assert.Null(restored.Module.Factions.Reputation);
        Assert.False(restored.Module.Factions.RequireReputationRuntime);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot)) Directory.Delete(_temporaryRoot, true);
    }
}
