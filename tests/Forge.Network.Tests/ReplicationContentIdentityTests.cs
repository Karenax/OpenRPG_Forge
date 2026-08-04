using Forge.Network.Content;
using Forge.Network.Identity;
using Forge.Network.Replication;

namespace Forge.Network.Tests;

public sealed class ReplicationContentIdentityTests
{
    [Fact]
    public void Replication_delta_is_revisioned_and_server_authoritative()
    {
        var service = new ReplicationService();
        var entity = new ReplicatedEntity(Guid.NewGuid(), Guid.NewGuid(), new(1, 2, 3), 0, new Dictionary<string, string> { ["hp"] = "10" });
        Assert.Throws<UnauthorizedAccessException>(() => service.Upsert(Guid.NewGuid(), entity, false));
        Assert.Equal(1, service.Upsert(Guid.NewGuid(), entity, true));
        var scope = new ObserverScope(Guid.NewGuid(), entity.ZoneInstanceId, new(0, 0, 0), 10, new HashSet<Guid>());
        var delta = service.CreateDelta(scope, 0);
        Assert.Single(delta.Upserts);
        Assert.Equal(1, delta.ToRevision);
        Assert.Empty(service.CreateDelta(scope, 1).Upserts);
    }

    [Fact]
    public void Interest_management_filters_other_zone_and_distant_entities()
    {
        var service = new ReplicationService();
        var zone = Guid.NewGuid();
        var nearby = new ReplicatedEntity(Guid.NewGuid(), zone, new(2, 0, 0), 0, new Dictionary<string, string>());
        var distant = nearby with { EntityId = Guid.NewGuid(), Position = new(100, 0, 0) };
        var otherZone = nearby with { EntityId = Guid.NewGuid(), ZoneInstanceId = Guid.NewGuid() };
        service.Upsert(Guid.NewGuid(), nearby, true);
        service.Upsert(Guid.NewGuid(), distant, true);
        service.Upsert(Guid.NewGuid(), otherZone, true);
        var scope = new ObserverScope(Guid.NewGuid(), zone, new(0, 0, 0), 10, new HashSet<Guid>());
        Assert.Equal(nearby.EntityId, Assert.Single(service.CreateDelta(scope, 0).Upserts).EntityId);
        var explicitScope = scope with { ExplicitEntityIds = new HashSet<Guid> { distant.EntityId } };
        Assert.Contains(service.CreateDelta(explicitScope, 0).Upserts, x => x.EntityId == distant.EntityId);
    }

    [Fact]
    public void Content_manifest_is_deterministic_and_detects_tampering()
    {
        var hash = new string('a', 64);
        var resources = new[] { new ContentResource("areas/crypt.json", 42, hash) };
        var server = ContentManifestService.Create("crypt", "1.0.0", "1", resources);
        var reordered = ContentManifestService.Create("crypt", "1.0.0", "1", resources.Reverse());
        Assert.Equal(server.Fingerprint, reordered.Fingerprint);
        Assert.True(ContentManifestService.Compare(server, reordered).Compatible);
        var tampered = ContentManifestService.Create("crypt", "1.0.0", "1", new[] { resources[0] with { Sha256 = new string('b', 64) } });
        Assert.Equal("NET_CONTENT_SYNC_REQUIRED", ContentManifestService.Compare(server, tampered).Code);
    }

    [Fact]
    public void Version_lock_rejects_protocol_and_module_mismatch()
    {
        var resource = new[] { new ContentResource("project.json", 10, new string('c', 64)) };
        var server = ContentManifestService.Create("crypt", "1.0.0", "1", resource);
        Assert.Equal("NET_PROTOCOL_MISMATCH", ContentManifestService.Compare(server, ContentManifestService.Create("crypt", "1.0.0", "2", resource)).Code);
        Assert.Equal("NET_MODULE_VERSION_MISMATCH", ContentManifestService.Compare(server, ContentManifestService.Create("crypt", "1.1.0", "1", resource)).Code);
    }

    [Fact]
    public void Account_authentication_hashes_secret_and_has_constant_result_shape()
    {
        var service = new AccountService();
        var account = service.Register("Elyra.Player", "a-strong-password");
        Assert.False(account.PasswordHash.SequenceEqual(System.Text.Encoding.UTF8.GetBytes("a-strong-password")));
        Assert.True(service.Authenticate("elyra.player", "a-strong-password").Authenticated);
        var wrong = service.Authenticate("elyra.player", "wrong-password");
        var unknown = service.Authenticate("unknown.player", "wrong-password");
        Assert.Equal("NET_AUTH_INVALID_CREDENTIALS", wrong.Code);
        Assert.Equal(wrong.Code, unknown.Code);
    }
}
