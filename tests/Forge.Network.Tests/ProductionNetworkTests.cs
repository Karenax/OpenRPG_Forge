using Forge.Domain.Modules;
using Forge.Domain.RuntimeExtensions;
using Forge.Network.Production;
using Forge.Network.Testing;

namespace Forge.Network.Tests;

public sealed class ProductionNetworkTests
{
    [Fact]
    public void Server_negotiates_protocol_and_applies_atomic_reload_once()
    {
        var profile = CreateNetwork();
        var service = new ServerProductionService(profile);
        var protocol = service.Negotiate("windows", 1, 3, ["metrics", "shell"], signedExtension: true);
        var request = new ServerProductionService.ReloadRequest(Guid.NewGuid(), "crypt-eu", 32, 4, 0,
            new string('a', 64));
        var preview = service.Reload(request, false);
        var applied = service.Reload(request, true);

        Assert.True(protocol.Compatible);
        Assert.Equal(["metrics"], protocol.GrantedCapabilities);
        Assert.Equal("NET_PRODUCTION_PREVIEW", preview.Code);
        Assert.True(applied.Applied);
        Assert.True(applied.RollbackAvailable);
    }

    [Fact]
    public void Server_rejects_invalid_stale_and_replayed_reload_commands()
    {
        var service = new ServerProductionService(CreateNetwork());
        var invalid = service.Reload(new ServerProductionService.ReloadRequest(Guid.NewGuid(),
            "crypt-eu", 32, 4, 0, "not-a-sha256"), true);
        var stale = service.Reload(new ServerProductionService.ReloadRequest(Guid.NewGuid(),
            "crypt-eu", 32, 4, 1, new string('a', 64)), true);
        var commandId = Guid.NewGuid();
        var request = new ServerProductionService.ReloadRequest(commandId, "crypt-eu", 32, 4, 0,
            new string('b', 64));
        var applied = service.Reload(request, true);
        var replayed = service.Reload(request with { ExpectedRevision = applied.Revision }, true);

        Assert.Equal("NET_PRODUCTION_FINGERPRINT_INVALID", invalid.Code);
        Assert.Equal("NET_PRODUCTION_REVISION_STALE", stale.Code);
        Assert.True(applied.Applied);
        Assert.Equal("NET_PRODUCTION_COMMAND_REPLAYED", replayed.Code);
        Assert.False(replayed.Applied);
    }

    [Fact]
    public void Protocol_negotiation_filters_capabilities_and_blocks_unsigned_extensions()
    {
        var service = new ServerProductionService(CreateNetwork());
        var signed = service.Negotiate("windows", 1, 3, ["shell", "metrics"],
            signedExtension: true);
        var unsigned = service.Negotiate("windows", 1, 3, ["metrics"],
            signedExtension: false);
        var unsupported = service.Negotiate("plan9", 1, 3, ["metrics"],
            signedExtension: true);

        Assert.True(signed.Compatible);
        Assert.Equal(2, signed.NegotiatedVersion);
        Assert.True(signed.ExtensionAllowed);
        Assert.Equal(["metrics"], signed.GrantedCapabilities);
        Assert.True(unsigned.Compatible);
        Assert.False(unsigned.ExtensionAllowed);
        Assert.Empty(unsigned.GrantedCapabilities);
        Assert.False(unsupported.Compatible);
        Assert.Equal(0, unsupported.NegotiatedVersion);
    }

    [Fact]
    public void Soak_report_checks_duration_memory_handles_database_latency_and_players()
    {
        var source = ProductionAutomationProfile.CreateDefault(Guid.Parse("44000000-0000-4000-8000-000000000001"));
        var policy = source.SoakTests with
        {
            Enabled = true,
            DurationMinutes = 120,
            VirtualPlayers = 16,
            MaximumMemoryGrowthMegabytes = 64,
            MaximumHandleGrowth = 50,
            MaximumP95LatencyMilliseconds = 200,
        };
        var passed = DeterministicSoakTestService.Evaluate(policy,
        [
            new(0, 512, 1_000, 80, true, 16),
            new(60, 530, 1_010, 120, true, 16),
            new(120, 540, 1_020, 140, true, 16),
        ]);
        var failed = DeterministicSoakTestService.Evaluate(policy,
        [
            new(0, 512, 1_000, 80, true, 16),
            new(120, 700, 1_200, 350, false, 12),
        ]);

        Assert.True(passed.Passed);
        Assert.Equal("SOAK_PASS", passed.Code);
        Assert.False(failed.Passed);
        Assert.Equal(5, failed.Diagnostics.Count);
    }

    private static NetworkProductionProfile CreateNetwork()
    {
        var source = NetworkProductionProfile.CreateDefault(Guid.Parse("44000000-0000-4000-8000-000000000010"));
        return new NetworkProductionProfile(source.Id, 1, source.Voice with { Enabled = true },
            [new NetworkProductionProfile.ShardDefinition(Guid.Parse("44000000-0000-4000-8000-000000000011"),
                "crypt-eu", [Guid.Parse("44000000-0000-4000-8000-000000000012")], 32, "eu-west")],
            source.HotReload with { Enabled = true },
            source.ServerExtensions with { Enabled = true, AllowedCapabilities = ["metrics"] },
            source.Protocol with { MaximumVersion = 2 }, "test");
    }
}
