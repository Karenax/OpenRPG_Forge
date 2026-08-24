using Forge.Domain.Modules;

namespace Forge.Domain.Tests.Modules;

public sealed class ProjectVariableGovernanceTests
{
    [Fact]
    public void Ux0IHumanProjectVariablesCoverAllFiveScopesWithTypedDefaults()
    {
        var variables = new[]
        {
            new ProjectVariableDefinition(ProjectVariableId.From(Guid.NewGuid()), "session.preview",
                ProjectVariableValueKind.String, ProjectVariableScope.Session, "ready", "Aperçu", "studio:ux0-i"),
            new ProjectVariableDefinition(ProjectVariableId.From(Guid.NewGuid()), "world.chapter",
                ProjectVariableValueKind.Integer, ProjectVariableScope.Save, "01", "Chapitre", "studio:ux0-i",
                replication: ProjectVariableReplicationMode.AllClients),
            new ProjectVariableDefinition(ProjectVariableId.From(Guid.NewGuid()), "player.reputation",
                ProjectVariableValueKind.Number, ProjectVariableScope.Player, "2.5000", "Réputation", "studio:ux0-i",
                replication: ProjectVariableReplicationMode.Owner),
            new ProjectVariableDefinition(ProjectVariableId.From(Guid.NewGuid()), "party.ready",
                ProjectVariableValueKind.Boolean, ProjectVariableScope.Party, "TRUE", "Groupe prêt", "studio:ux0-i",
                replication: ProjectVariableReplicationMode.Party),
            new ProjectVariableDefinition(ProjectVariableId.From(Guid.NewGuid()), "server.phase",
                ProjectVariableValueKind.String, ProjectVariableScope.Server, "lobby", "Phase serveur", "studio:ux0-i",
                replication: ProjectVariableReplicationMode.ServerAuthority, requiredAtRuntime: true),
        };

        Assert.Empty(ProjectVariableValidator.Validate(variables));
        Assert.Equal(Enum.GetValues<ProjectVariableScope>(), variables.Select(item => item.Scope));
        Assert.Equal("1", variables[1].DefaultValue);
        Assert.Equal("2.5", variables[2].DefaultValue);
        Assert.Equal("true", variables[3].DefaultValue);
        Assert.All(variables, item => Assert.NotEqual(Guid.Empty, item.Id.Value));
        Assert.All(variables, item => Assert.Equal(ProjectVariableDefinition.CurrentSchemaVersion, item.SchemaVersion));
        Assert.All(variables, item => Assert.Equal("studio:ux0-i", item.Provenance));
    }

    [Fact]
    public void Variable_preserves_replication_debug_and_runtime_policies()
    {
        var variable = Variable("server.phase", ProjectVariableScope.Server,
            ProjectVariableReplicationMode.ServerAuthority, debugVisible: false, required: true);

        Assert.Equal(ProjectVariableReplicationMode.ServerAuthority, variable.Replication);
        Assert.False(variable.DebugVisible);
        Assert.True(variable.RequiredAtRuntime);
        Assert.Empty(ProjectVariableValidator.Validate([variable]));
    }

    [Fact]
    public void Validator_localizes_invalid_scope_replication_and_duplicates()
    {
        var id = ProjectVariableId.From(Guid.NewGuid());
        var transient = new ProjectVariableDefinition(id, "session.required", ProjectVariableValueKind.String,
            ProjectVariableScope.Session, "ready", string.Empty, "test", 1,
            ProjectVariableReplicationMode.ServerAuthority, true, true);
        var duplicate = new ProjectVariableDefinition(id, transient.Key, ProjectVariableValueKind.String,
            ProjectVariableScope.Session, "ready", string.Empty, "test");

        var diagnostics = ProjectVariableValidator.Validate([transient, duplicate]);

        Assert.Contains(diagnostics, item => item.Code == ProjectVariableValidator.RequiredSessionCode &&
            item.Path.Contains(id.ToString(), StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == ProjectVariableValidator.ServerReplicationCode);
        Assert.Contains(diagnostics, item => item.Code == ProjectVariableValidator.DuplicateIdCode);
        Assert.Contains(diagnostics, item => item.Code == ProjectVariableValidator.DuplicateKeyCode);
    }

    private static ProjectVariableDefinition Variable(string key, ProjectVariableScope scope,
        ProjectVariableReplicationMode replication, bool debugVisible, bool required) => new(
        ProjectVariableId.From(Guid.NewGuid()), key, ProjectVariableValueKind.String, scope, "ready",
        string.Empty, "test", 1, replication, debugVisible, required);
}
