using System.Net;
using System.Net.Sockets;
using Forge.Network.Sessions;
using Forge.Network.Transport;

namespace Forge.Network.Scenarios;

public sealed record InternetTransportAcceptanceResult(
    bool Passed,
    Guid SessionId,
    long Revision,
    int Players,
    string RejectionCode);

public static class InternetTransportAcceptanceScenario
{
    public static async Task<InternetTransportAcceptanceResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        var fingerprint = new string('a', 64);
        var configuration = new DedicatedServerConfiguration(
            Guid.Parse("5f340000-0000-4000-8000-000000000100"),
            1,
            "OpenRPG Forge Internet transport smoke",
            "127.0.0.1",
            ReserveLoopbackPort(),
            8,
            DiscoveryMode.Direct,
            fingerprint,
            InternetTransportProtocol.GameProtocolVersion);
        await using var server = new TcpAuthoritativeServer(
            configuration,
            Guid.Parse("5f340000-0000-4000-8000-000000000101"));
        server.Start();
        await using var first = await TcpGameClient.ConnectAsync(
            new InternetClientOptions("127.0.0.1", configuration.Port, "Elyra", fingerprint),
            cancellationToken);
        await using var second = await TcpGameClient.ConnectAsync(
            new InternetClientOptions("127.0.0.1", configuration.Port, "Borin", fingerprint),
            cancellationToken);
        first.PublishPose("entry-area", 1, 0, 2, .25);
        second.PublishPose("entry-area", 3, 0, 4, .75);
        var snapshot = await first.WaitForSnapshotAsync(
            item => item.Players.Count == 2,
            TimeSpan.FromSeconds(5),
            cancellationToken);

        var rejectionCode = "none";
        try
        {
            await using var incompatible = await TcpGameClient.ConnectAsync(
                new InternetClientOptions(
                    "127.0.0.1",
                    configuration.Port,
                    "Incompatible",
                    "different-module"),
                cancellationToken);
        }
        catch (InternetTransportException exception)
        {
            rejectionCode = exception.Code;
        }

        return new InternetTransportAcceptanceResult(
            snapshot.Players.Count == 2 && rejectionCode == "NET_CONTENT_MISMATCH",
            snapshot.SessionId,
            snapshot.Revision,
            snapshot.Players.Count,
            rejectionCode);
    }

    private static int ReserveLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
