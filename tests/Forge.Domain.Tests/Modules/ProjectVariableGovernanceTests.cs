using Forge.Domain.Modules;

namespace Forge.Domain.Tests.Modules;

public sealed class ProjectVariableGovernanceTests
{
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
