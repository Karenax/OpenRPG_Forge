using System.Net;
using System.Net.Sockets;
using Forge.Network.Sessions;
using Forge.Network.Transport;

namespace Forge.Network.Tests;

public sealed class InternetTransportTests
{
    [Fact]
    public async Task TwoTcpClients_JoinAndReceiveAuthoritativePlayerSnapshots()
    {
        var configuration = CreateConfiguration(ReserveLoopbackPort());
        await using var server = new TcpAuthoritativeServer(
            configuration,
            Guid.Parse("5f340000-0000-4000-8000-000000000001"));
        server.Start();

        await using var first = await TcpGameClient.ConnectAsync(new InternetClientOptions(
            "127.0.0.1",
            configuration.Port,
            "Elyra",
            configuration.ModuleFingerprint,
            configuration.ProtocolVersion,
            Guid.Parse("5f340000-0000-4000-8000-000000000011"),
            Guid.Parse("5f340000-0000-4000-8000-000000000021")));
        await using var second = await TcpGameClient.ConnectAsync(new InternetClientOptions(
            "localhost",
            configuration.Port,
            "Borin",
            configuration.ModuleFingerprint,
            configuration.ProtocolVersion,
            Guid.Parse("5f340000-0000-4000-8000-000000000012"),
            Guid.Parse("5f340000-0000-4000-8000-000000000022")));

        Assert.True(first.PublishPose("area-entry", 1, 2, 3, .5));
        Assert.True(second.PublishPose("area-entry", 4, 5, 6, 1.5));
        var snapshot = await first.WaitForSnapshotAsync(item => item.Players.Count == 2, TimeSpan.FromSeconds(5));

        Assert.Equal(server.SessionId, first.SessionId);
        Assert.Equal(2, server.ConnectedPlayers);
        Assert.Collection(
            snapshot.Players.OrderBy(item => item.DisplayName, StringComparer.Ordinal),
            player =>
            {
                Assert.Equal("Borin", player.DisplayName);
                Assert.Equal(4, player.X);
                Assert.Equal("area-entry", player.AreaId);
            },
            player =>
            {
                Assert.Equal("Elyra", player.DisplayName);
                Assert.Equal(1, player.X);
                Assert.Equal("area-entry", player.AreaId);
            });
    }

    [Fact]
    public async Task TcpServer_RejectsMismatchedContentBeforePublishingThePlayer()
    {
        var configuration = CreateConfiguration(ReserveLoopbackPort());
        await using var server = new TcpAuthoritativeServer(configuration);
        server.Start();

        var exception = await Assert.ThrowsAsync<InternetTransportException>(() =>
            TcpGameClient.ConnectAsync(new InternetClientOptions(
                "127.0.0.1",
                configuration.Port,
                "Intrus",
                "different-module",
                configuration.ProtocolVersion)));

        Assert.Equal("NET_CONTENT_MISMATCH", exception.Code);
        Assert.Equal(0, server.ConnectedPlayers);
    }

    [Fact]
    public async Task TcpServer_BroadcastsDisconnectAndRejectsImplausiblePoseStep()
    {
        var configuration = CreateConfiguration(ReserveLoopbackPort());
        await using var server = new TcpAuthoritativeServer(configuration);
        server.Start();
        var first = await TcpGameClient.ConnectAsync(new InternetClientOptions(
            "127.0.0.1", configuration.Port, "Elyra", configuration.ModuleFingerprint,
            ConnectionId: Guid.Parse("5f340000-0000-4000-8000-000000000031"),
            AccountId: Guid.Parse("5f340000-0000-4000-8000-000000000041")));
        await using var second = await TcpGameClient.ConnectAsync(new InternetClientOptions(
            "127.0.0.1", configuration.Port, "Borin", configuration.ModuleFingerprint,
            ConnectionId: Guid.Parse("5f340000-0000-4000-8000-000000000032"),
            AccountId: Guid.Parse("5f340000-0000-4000-8000-000000000042")));
        Assert.True(first.PublishPose("area-entry", .1, 0, 0, 0));
        Assert.True(second.PublishPose("area-entry", 2, 0, 0, 0));
        await Task.Delay(250);
        Assert.True(server.ConnectedPlayers == 2,
            $"connected={server.ConnectedPlayers} server={server.LastTransportCode ?? "none"} " +
            $"first={first.LastServerCode ?? "none"}/{first.SentPacketCount} " +
            $"second={second.LastServerCode ?? "none"}/{second.SentPacketCount}");
        Assert.True(first.SentPacketCount == 1 && second.SentPacketCount == 1,
            $"sent first={first.SentPacketCount} second={second.SentPacketCount}");
        _ = await second.WaitForSnapshotAsync(item => item.Players.Count == 2, TimeSpan.FromSeconds(5));

        Assert.True(first.PublishPose("area-entry", 500, 0, 0, 0));
        await WaitUntilAsync(() => first.LastServerCode == "NET_POSE_STEP_REJECTED", TimeSpan.FromSeconds(5));
        await first.DisposeAsync();
        var snapshot = await second.WaitForSnapshotAsync(item => item.Players.Count == 1, TimeSpan.FromSeconds(5));

        Assert.Single(snapshot.Players);
        Assert.Equal("Borin", snapshot.Players[0].DisplayName);
    }

    private static DedicatedServerConfiguration CreateConfiguration(int port) => new(
        Guid.Parse("5f340000-0000-4000-8000-000000000000"),
        1,
        "Internet transport test",
        "127.0.0.1",
        port,
        8,
        DiscoveryMode.Direct,
        new string('a', 64),
        InternetTransportProtocol.GameProtocolVersion);

    private static int ReserveLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        while (!predicate()) await Task.Delay(10, cancellation.Token);
    }
}
