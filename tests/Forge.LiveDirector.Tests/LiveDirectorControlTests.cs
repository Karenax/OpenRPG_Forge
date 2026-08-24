using Forge.LiveDirector.Control;
using Forge.LiveDirector.Economy;
using Forge.LiveDirector.World;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Tests;

public sealed class LiveDirectorControlTests
{
    private static readonly Guid ActorId = Guid.Parse("c0000000-0000-4000-8000-000000000001");
    private static readonly Guid PlayerId = Guid.Parse("c0000000-0000-4000-8000-000000000002");
    private static readonly Guid ZoneId = Guid.Parse("c0000000-0000-4000-8000-000000000003");
    private static readonly Guid TargetId = Guid.Parse("c0000000-0000-4000-8000-000000000004");

    [Fact]
    public void Inspect_returns_permissioned_read_only_components_stats_variables_scripts_owner_and_history()
    {
        var (service, _, gm, _) = Create();
        service.SetMetadata(TargetId, new(
            new Dictionary<string, string> { ["combat"] = "enabled" },
            new Dictionary<string, decimal> { ["health"] = 42 },
            new Dictionary<string, string> { ["quest.stage"] = "2" },
            ["ember_guardian.forge"], PlayerId, ["spawned", "healed"]));

        var result = service.Execute(gm, Request("director.inspect", 0, TimeSpan.Zero, new Dictionary<string, string>()), true);

        Assert.False(result.Applied);
        Assert.Equal("LIVE_INSPECTION_READY", result.Code);
        Assert.Equal(42, result.Inspection!.Stats["health"]);
        Assert.Equal("ember_guardian.forge", Assert.Single(result.Inspection.Scripts));
        Assert.Equal(PlayerId, result.Inspection.OwnerAccountId);
    }

    [Fact]
    public void Possession_requires_protected_player_consent_return_point_and_expires()
    {
        var (service, _, gm, advance) = Create(playerTarget: true);
        Assert.Equal("LIVE_POSSESSION_PLAYER_PROTECTED", service.Execute(gm, Request("director.possess", 0, TimeSpan.FromMinutes(5), new Dictionary<string, string> { ["returnPoint"] = "crypt-entry" }), true).Code);
        var request = Request("director.possess", 0, TimeSpan.FromMinutes(5), new Dictionary<string, string> { ["returnPoint"] = "crypt-entry", ["protectedPlayerConsent"] = "granted", ["inputContext"] = "gm" });
        Assert.Equal("LIVE_CONTROL_PREVIEW", service.Execute(gm, request, false).Code);
        var applied = service.Execute(gm, request, true);

        Assert.True(applied.Applied);
        Assert.Contains("possessed-by", applied.Target!.State);
        advance(TimeSpan.FromMinutes(6));
        Assert.Equal("LIVE_POSSESSION_RETURNED:crypt-entry", service.ReleaseExpiredPossession(ActorId));
        Assert.Equal("active", service.Execute(gm,
            Request("director.inspect", 2, TimeSpan.Zero,
                new Dictionary<string, string>()), true).Target!.State);
    }

    [Fact]
    public void Possession_can_be_previewed_and_released_to_the_preserved_identity()
    {
        var (service, chooser, gm, _) = Create();
        var request = Request("director.possess", 0, TimeSpan.FromMinutes(5),
            new Dictionary<string, string>
            {
                ["returnPoint"] = "crypt-entry",
                ["inputContext"] = "director"
            });

        Assert.True(service.Execute(gm, request, true).Applied);
        Assert.Equal("LIVE_POSSESSION_RELEASE_PREVIEW",
            service.ReleasePossession(gm, false).Code);
        var released = service.ReleasePossession(gm, true);

        Assert.Equal("LIVE_POSSESSION_RETURNED", released.Code);
        Assert.Equal("active", chooser.Find(TargetId)!.State);
        Assert.Equal("Gardien", chooser.Find(TargetId)!.Name);
    }

    [Fact]
    public void Impersonation_and_protection_require_disclosure_scope_duration_and_indicator()
    {
        var (service, _, gm, _) = Create();
        Assert.Equal("LIVE_IMPERSONATION_DISCLOSURE_REQUIRED", service.Execute(gm, Request("director.impersonate", 0, TimeSpan.Zero, new Dictionary<string, string> { ["mode"] = "chat" }), true).Code);
        var impersonated = service.Execute(gm, Request("director.impersonate", 0, TimeSpan.Zero, new Dictionary<string, string> { ["mode"] = "dialogue", ["disclosure"] = "visible" }), true);
        Assert.Contains("impersonated:dialogue", impersonated.Target!.State);

        Assert.Equal("LIVE_PROTECTION_INDICATOR_REQUIRED", service.Execute(gm, Request("director.protect", 1, TimeSpan.FromMinutes(10), new Dictionary<string, string> { ["scope"] = "all" }), true).Code);
        var protectedResult = service.Execute(gm, Request("director.protect", 1, TimeSpan.FromMinutes(10), new Dictionary<string, string> { ["scope"] = "all", ["indicator"] = "amber-shield" }), true);
        Assert.Contains("indicator:amber-shield", protectedResult.Target!.State);
        Assert.NotNull(protectedResult.ExpiresAt);
    }

    [Fact]
    public void Economy_adjustment_is_previewable_capped_non_negative_and_audited()
    {
        var authorization = new AuthorizationService();
        var service = new LiveEconomyService(authorization, 1_000, 10_000);
        var gm = new AuthorizationSubject(ActorId, NetworkRole.Administrator, new HashSet<Guid>());
        var command = new LiveEconomyService.Request(Guid.NewGuid(), PlayerId, 500, "live reward", 0);

        Assert.Equal(500, service.Adjust(gm, command, false).Balance);
        Assert.Equal(0, service.Balance(PlayerId));
        var applied = service.Adjust(gm, command, true);
        Assert.Equal(500, applied.Balance);
        Assert.Contains("reason=live reward", applied.AuditEntry);
        Assert.Equal("LIVE_ECONOMY_ADJUSTMENT_CAP", service.Adjust(gm, new(Guid.NewGuid(), PlayerId, 1_001, "too much", 1), true).Code);
        Assert.Equal("LIVE_ECONOMY_WALLET_RANGE", service.Adjust(gm, new(Guid.NewGuid(), PlayerId, -501, "too low", 1), true).Code);
    }

    private static (LiveDirectorControlService Service, LiveWorldChooser Chooser, AuthorizationSubject Gm, Action<TimeSpan> Advance) Create(bool playerTarget = false)
    {
        var now = new DateTimeOffset(2026, 8, 4, 13, 0, 0, TimeSpan.Zero);
        var authorization = new AuthorizationService();
        var chooser = new LiveWorldChooser(authorization);
        chooser.Upsert(new(TargetId, ZoneId, "creature", "Gardien", "ember", playerTarget ? PlayerId : null, "active", []));
        return (new(chooser, authorization, () => now), chooser, new(ActorId, NetworkRole.Administrator, new HashSet<Guid> { ZoneId }), duration => now = now.Add(duration));
    }

    private static LiveDirectorControlService.Request Request(string kind, long revision, TimeSpan duration, IReadOnlyDictionary<string, string> arguments) =>
        new(Guid.NewGuid(), kind, TargetId, revision, duration, arguments);
}
