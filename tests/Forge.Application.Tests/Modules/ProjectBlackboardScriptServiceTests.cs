using Forge.Application.Modules;
using Forge.Domain.Modules;

namespace Forge.Application.Tests.Modules;

public sealed class ProjectBlackboardScriptServiceTests
{
    [Fact]
    public void Handles_mutate_typed_values_atomically_and_filter_debug_view()
    {
        var publicValue = Variable("world.threat", ProjectVariableValueKind.Integer,
            ProjectVariableScope.Party, "1", ProjectVariableReplicationMode.Party, true);
        var secret = Variable("world.secret", ProjectVariableValueKind.String,
            ProjectVariableScope.Save, "hidden", ProjectVariableReplicationMode.None, false);
        var service = new ProjectBlackboardScriptService([publicValue, secret]);
        var handle = Assert.Single(service.Query("threat"));

        var rejected = service.Set(handle, "many");
        var applied = service.Set(handle, "004");

        Assert.False(rejected.Succeeded);
        Assert.Equal("1", rejected.Before!.Value);
        Assert.True(applied.Succeeded);
        Assert.Equal("4", applied.After!.Value);
        var debug = service.DebugSnapshot();
        Assert.Single(debug.Variables);
        Assert.Equal(1, debug.HiddenVariableCount);
    }

    [Fact]
    public void Replication_delta_obeys_owner_party_client_and_server_policies()
    {
        var owner = Variable("player.hp", ProjectVariableValueKind.Integer, ProjectVariableScope.Player, "10",
            ProjectVariableReplicationMode.Owner, true);
        var party = Variable("party.quest", ProjectVariableValueKind.Boolean, ProjectVariableScope.Party, "false",
            ProjectVariableReplicationMode.Party, true);
        var all = Variable("world.time", ProjectVariableValueKind.Number, ProjectVariableScope.Save, "12",
            ProjectVariableReplicationMode.AllClients, true);
        var server = Variable("server.seed", ProjectVariableValueKind.Integer, ProjectVariableScope.Server, "42",
            ProjectVariableReplicationMode.ServerAuthority, false);
        var service = new ProjectBlackboardScriptService([owner, party, all, server]);

        Assert.Equal(3, service.ReplicationDelta(BlackboardReplicationAudience.Owner).Values.Count);
        Assert.Equal(2, service.ReplicationDelta(BlackboardReplicationAudience.Party).Values.Count);
        Assert.Single(service.ReplicationDelta(BlackboardReplicationAudience.Client).Values);
        Assert.Single(service.ReplicationDelta(BlackboardReplicationAudience.Server).Values);
    }

    [Fact]
    public void Runtime_compatibility_is_limited_to_schema_less_legacy_modules()
    {
        var legacy = ProjectBlackboardScriptService.ForRuntime([]);
        var declared = ProjectBlackboardScriptService.ForRuntime([
            Variable("world.ready", ProjectVariableValueKind.Boolean, ProjectVariableScope.Save, "false",
                ProjectVariableReplicationMode.Party, true),
        ]);

        Assert.True(legacy.UsesLegacyCompatibility);
        Assert.True(legacy.TrySet("legacy.quest.stage", "2", out var legacyDiagnostic));
        Assert.Null(legacyDiagnostic);
        Assert.False(declared.UsesLegacyCompatibility);
        Assert.False(declared.TrySet("legacy.quest.stage", "2", out var schemaDiagnostic));
        Assert.Contains("not declared", schemaDiagnostic, StringComparison.Ordinal);
        Assert.True(declared.TrySet("world.ready", "true", out var declaredDiagnostic));
        Assert.Null(declaredDiagnostic);
    }

    private static ProjectVariableDefinition Variable(string key, ProjectVariableValueKind kind,
        ProjectVariableScope scope, string value, ProjectVariableReplicationMode replication,
        bool debugVisible) => new(ProjectVariableId.From(Guid.NewGuid()), key, kind, scope, value, string.Empty,
        "test", 1, replication, debugVisible, false);
}
