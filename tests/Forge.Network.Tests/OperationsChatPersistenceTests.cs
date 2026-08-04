using Forge.Network.Chat;
using Forge.Network.Operations;
using Forge.Network.Persistence;

namespace Forge.Network.Tests;

public sealed class OperationsChatPersistenceTests
{
    private static AuthorizationSubject Subject(NetworkRole role, params Guid[] zones) => new(Guid.NewGuid(), role, zones.ToHashSet());

    [Fact]
    public void Roles_and_zone_scope_are_enforced()
    {
        var zone = Guid.NewGuid();
        var service = new AuthorizationService();
        Assert.True(service.Authorize(Subject(NetworkRole.Player), NetworkCapability.Chat).Allowed);
        Assert.Equal("NET_AUTH_CAPABILITY_DENIED", service.Authorize(Subject(NetworkRole.Player), NetworkCapability.ManageWorld).Code);
        var moderator = Subject(NetworkRole.Moderator, zone);
        Assert.True(service.Authorize(moderator, NetworkCapability.ModerateChat, zone).Allowed);
        Assert.Equal("NET_AUTH_SCOPE_DENIED", service.Authorize(moderator, NetworkCapability.ModerateChat, Guid.NewGuid()).Code);
    }

    [Fact]
    public void Remote_admin_supports_preview_revision_and_replay_protection()
    {
        var admin = Subject(NetworkRole.Administrator);
        var command = new AdministrationCommand(Guid.NewGuid(), admin.AccountId, "world.save", null, null, 0, new Dictionary<string, string>());
        var service = new AdministrationService(new AuthorizationService());
        Assert.Equal("NET_ADMIN_PREVIEW", service.Execute(admin, command, false).Code);
        Assert.True(service.Execute(admin, command, true).Applied);
        Assert.Equal("NET_ADMIN_REVISION_STALE", service.Execute(admin, command, true).Code);
        var player = Subject(NetworkRole.Player);
        Assert.Equal("NET_AUTH_CAPABILITY_DENIED", service.Execute(player, command with { CommandId = Guid.NewGuid(), ActorAccountId = player.AccountId, ExpectedRevision = 1 }, true).Code);
    }

    [Fact]
    public void Moderation_mute_expires_and_is_audited()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero));
        var service = new ModerationService(new AuthorizationService(), clock);
        var moderator = Subject(NetworkRole.Moderator);
        var target = Guid.NewGuid();
        Assert.True(service.Apply(moderator, ModerationActionKind.Mute, target, "spam", TimeSpan.FromMinutes(5)).Applied);
        Assert.True(service.IsRestricted(target, ModerationActionKind.Mute));
        clock.Advance(TimeSpan.FromMinutes(6));
        Assert.False(service.IsRestricted(target, ModerationActionKind.Mute));
        Assert.Single(service.AuditFor(target));
    }

    [Fact]
    public void Chat_validates_scope_visibility_and_moderation()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var auth = new AuthorizationService();
        var moderation = new ModerationService(auth, clock);
        var chat = new ChatService(auth, moderation, clock);
        var player = Subject(NetworkRole.Player);
        var party = Guid.NewGuid();
        Assert.True(chat.Send(player, Guid.NewGuid(), ChatChannelKind.Party, party, "  En avant ! ").Accepted);
        Assert.Equal("NET_CHAT_SCOPE_REQUIRED", chat.Send(player, null, ChatChannelKind.Party, null, "test").Code);
        var moderator = Subject(NetworkRole.Moderator);
        moderation.Apply(moderator, ModerationActionKind.Mute, player.AccountId, "spam", TimeSpan.FromMinutes(5));
        Assert.Equal("NET_CHAT_MUTED", chat.Send(player, null, ChatChannelKind.Global, null, "blocked").Code);
        Assert.Single(chat.Since(0, message => message.ScopeId == party));
    }

    [Fact]
    public void World_persistence_is_transactional_canonical_and_hash_verified()
    {
        var store = new InMemoryWorldSnapshotStore(TimeProvider.System);
        var actor = Guid.NewGuid();
        var saved = store.Save(0, 4, "{\r\n  \"revision\": 4\r\n}", actor);
        Assert.DoesNotContain("\r\n", saved.Payload);
        Assert.Equal(64, saved.Sha256.Length);
        Assert.Equal(saved, store.LoadLatest());
        Assert.Throws<InvalidOperationException>(() => store.Save(0, 5, "{}", actor));
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
