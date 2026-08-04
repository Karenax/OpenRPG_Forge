using Forge.LiveDirector.Authentication;
using Forge.LiveDirector.Catalog;
using Forge.LiveDirector.Client;
using Forge.LiveDirector.World;
using Forge.Network.Identity;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Tests;

public sealed class LiveDirectorFoundationTests
{
    private static readonly Guid AccountIdPlaceholder = Guid.Parse("a0000000-0000-4000-8000-000000000001");
    private static readonly Guid SessionId = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    private static readonly Guid ZoneId = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    private static readonly Guid BlueprintId = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    [Fact]
    public void Authentication_requires_gm_role_supports_second_factor_expiry_and_revocation()
    {
        var now = new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);
        var accounts = new AccountService();
        var account = accounts.Register("director", "correct-horse-battery");
        var service = new DirectorAuthenticationService(accounts, () => now, () => SessionId, TimeSpan.FromMinutes(30));

        Assert.Equal("LIVE_GM_ROLE_REQUIRED", service.Authenticate(new("director", "correct-horse-battery", null)).Code);
        service.GrantRole(account.AccountId, NetworkRole.Administrator);
        Assert.Equal("LIVE_GM_SECOND_FACTOR_INVALID", service.Authenticate(new("director", "correct-horse-battery", "bad"), x => x == "246810").Code);
        var session = service.Authenticate(new("director", "correct-horse-battery", "246810"), x => x == "246810");

        Assert.True(session.Authenticated);
        Assert.True(service.IsActive(session));
        Assert.Equal(SessionId, session.SessionId);
        Assert.Contains("LIVE_DIRECTOR_CLIENT_CONNECTED", new LiveDirectorClient().Connect(AccountIdPlaceholder, session).AuditEntry);
        now = now.AddMinutes(31);
        Assert.False(service.IsActive(session));
        Assert.Contains("LIVE_SESSION_REVOKED", service.Revoke(session));
        Assert.False(service.IsActive(session));
    }

    [Fact]
    public void Catalog_filters_server_policy_zone_cost_and_permissions()
    {
        var authorization = new AuthorizationService();
        var catalog = new LiveCatalogService(authorization);
        catalog.Replace([
            new(BlueprintId, "Gardien des braises", "creature", "crypt", 10, new HashSet<Guid> { ZoneId }, true),
            new(Guid.NewGuid(), "Interdit", "creature", "crypt", 1, new HashSet<Guid> { ZoneId }, false)
        ]);

        var gm = Subject(NetworkRole.Administrator);
        var player = Subject(NetworkRole.Player);
        Assert.Single(catalog.Search(gm, new("gardien", "creature", ZoneId, 10)));
        Assert.Empty(catalog.Search(gm, new(null, null, ZoneId, 5)));
        Assert.Empty(catalog.Search(player, new(null, null, ZoneId, 100)));
    }

    [Fact]
    public void Spawn_is_previewable_atomic_bounded_and_visible_in_chooser()
    {
        var authorization = new AuthorizationService();
        var catalog = new LiveCatalogService(authorization);
        var chooser = new LiveWorldChooser(authorization);
        catalog.Replace([new(BlueprintId, "Gardien des braises", "creature", "crypt", 10, new HashSet<Guid> { ZoneId }, true)]);
        var next = 100;
        var spawn = new LiveSpawnService(catalog, chooser, () => Guid.Parse($"a0000000-0000-4000-8000-{next++:000000000000}"));
        var gm = Subject(NetworkRole.Administrator);
        var commandId = Guid.Parse("a0000000-0000-4000-8000-000000000005");
        var request = new LiveSpawnService.Request(commandId, BlueprintId, ZoneId, 3, "formation", 0);

        var preview = spawn.Execute(gm, request, false);
        Assert.Equal("LIVE_SPAWN_PREVIEW", preview.Code);
        Assert.Empty(chooser.Search(gm, new(null, null, null, null, null)));
        var applied = spawn.Execute(gm, request, true);

        Assert.True(applied.Applied);
        Assert.Equal(3, applied.ObjectIds.Count);
        Assert.Equal(3, chooser.Search(gm, new(ZoneId, "creature", "braises", null, "spawned")).Count);
        Assert.Equal("LIVE_SPAWN_COMMAND_REPLAYED", spawn.Execute(gm, request with { ExpectedRevision = 1 }, true).Code);
        Assert.Equal("LIVE_SPAWN_REQUEST_INVALID", spawn.Execute(gm, request with { CommandId = Guid.NewGuid(), Count = 65, ExpectedRevision = 1 }, true).Code);
    }

    private static AuthorizationSubject Subject(NetworkRole role) => new(AccountIdPlaceholder, role, new HashSet<Guid> { ZoneId });
}
