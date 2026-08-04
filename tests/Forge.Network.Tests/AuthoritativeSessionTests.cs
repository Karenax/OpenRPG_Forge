using Forge.Network.Sessions;
using Forge.Network.Authoring;
using Forge.Network.Configuration;
using Forge.Network.Discovery;

namespace Forge.Network.Tests;

public sealed class AuthoritativeSessionTests
{
    private static DedicatedServerConfiguration Configuration(int capacity = 4) => new(Guid.Parse("10000000-0000-4000-8000-000000000001"), 1, "Crypte", "127.0.0.1", 5121, capacity, DiscoveryMode.Lan, "crypt-v1", "1.0");
    private static SessionIdentity Identity(string name) => new(Guid.NewGuid(), name, SessionRole.Player);

    [Fact]
    public void Connect_rejects_incompatible_content_and_accepts_matching_client()
    {
        var session = new AuthoritativeSession(Configuration(), Guid.Parse("20000000-0000-4000-8000-000000000001"));
        Assert.Equal("NET_CONTENT_MISMATCH", session.Connect(Guid.NewGuid(), Identity("Elyra"), "tampered", "1.0").Code);
        var accepted = session.Connect(Guid.NewGuid(), Identity("Elyra"), "crypt-v1", "1.0");
        Assert.True(accepted.Accepted);
        Assert.Equal(1, accepted.Revision);
    }

    [Fact]
    public void Client_cannot_impose_unknown_or_stale_mutation()
    {
        var connection = Guid.NewGuid();
        var session = new AuthoritativeSession(Configuration());
        session.Connect(connection, Identity("Elyra"), "crypt-v1", "1.0");
        var forged = new ClientIntent(Guid.NewGuid(), connection, 1, "world.set-state", new Dictionary<string, string> { ["gold"] = "999999" });
        var decision = session.Apply(forged);
        Assert.False(decision.Accepted);
        Assert.Equal("NET_INTENT_FORBIDDEN", decision.Code);
        Assert.Contains("rejected", decision.AuditEntry);
        Assert.Equal(1, session.Snapshot.Revision);
    }

    [Fact]
    public void Lobby_character_and_ready_flow_is_revision_checked()
    {
        var connection = Guid.NewGuid();
        var session = new AuthoritativeSession(Configuration());
        session.Connect(connection, Identity("Elyra"), "crypt-v1", "1.0");
        var character = new CharacterDraft(Guid.NewGuid(), "Elyra", "human", "guardian", new Dictionary<string, int> { ["might"] = 12 });
        Assert.True(session.SelectCharacter(connection, character, 1).Accepted);
        var ready = new ClientIntent(Guid.NewGuid(), connection, 2, "lobby.ready", new Dictionary<string, string> { ["ready"] = "true" });
        Assert.True(session.Apply(ready).Accepted);
        Assert.True(session.Snapshot.Members.Single().Ready);
        Assert.Equal("NET_REVISION_STALE", session.Apply(ready with { IntentId = Guid.NewGuid(), ExpectedRevision = 2 }).Code);
    }

    [Fact]
    public void Invalid_configuration_has_localized_stable_diagnostics()
    {
        var diagnostics = NetworkValidation.Validate(Configuration() with { Port = 80, Capacity = 0 });
        Assert.Contains(diagnostics, x => x is { Code: "NET_PORT_INVALID", Path: "/network/port" });
        Assert.Contains(diagnostics, x => x is { Code: "NET_CAPACITY_INVALID", Path: "/network/capacity" });
    }

    [Fact]
    public void Configuration_round_trip_is_canonical_and_lossless()
    {
        var store = new NetworkConfigurationStore();
        var json = store.Serialize(Configuration());
        Assert.Equal(Configuration(), store.Deserialize(json));
        Assert.Equal(json, store.Serialize(store.Deserialize(json)));
        Assert.DoesNotContain("\r\n", json);
    }

    [Fact]
    public void Directory_supports_direct_and_deterministic_lan_discovery()
    {
        var directory = new InMemoryServerDirectory();
        directory.Publish(Configuration() with { Name = "Zeta" });
        directory.Publish(Configuration() with { ServerId = Guid.NewGuid(), Name = "Alpha", Port = 5122 });
        Assert.Equal("Alpha", directory.BrowseLan()[0].Name);
        var direct = directory.Resolve("127.0.0.1", 5121);
        Assert.NotNull(direct);
        Assert.Equal("crypt-v1", direct.ModuleFingerprint);
        Assert.Equal("1.0", direct.ProtocolVersion);
        Assert.Null(directory.Resolve("127.0.0.1", 9999));
    }

    [Fact]
    public void Authoring_preview_and_apply_use_same_validated_command()
    {
        var before = Configuration();
        var after = before with { Capacity = 12, Discovery = DiscoveryMode.Direct };
        var service = new NetworkAuthoringService();
        var preview = service.Execute(new(before, after, "author:karen", false));
        Assert.False(preview.Applied);
        Assert.Contains("capacity: 4 -> 12", preview.SemanticDiff);
        var applied = service.Execute(new(before, after, "author:karen", true));
        Assert.True(applied.Applied);
        Assert.Equal(after, applied.Configuration);
        Assert.Contains("actor=author:karen", applied.AuditEntry);
    }
}
