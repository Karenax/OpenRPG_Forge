using System.Net;
using System.Net.Sockets;
using System.Text;
using Forge.Network.Scenarios;
using Forge.Network.Sessions;
using Forge.Network.Transport;

namespace Forge.Network.Tests;

public sealed class InternetTransportTests
{
    [Fact]
    public async Task SecureTransport_AuthenticatesEncryptsAndAppliesAuthoritativeGameplayState()
    {
        var result = await InternetTransportAcceptanceScenario.RunAsync();

        Assert.True(result.Passed);
        Assert.Equal("NET_AUTH_INVALID_CREDENTIALS", result.AuthenticationRejectionCode);
        Assert.StartsWith("Tls", result.TlsProtocol, StringComparison.Ordinal);
        Assert.Equal(70, result.TargetHealth);
        Assert.Equal(1, result.RemainingPotions);
        Assert.Equal("completed", result.QuestState);
        Assert.Equal(1, result.SaveRevision);
        Assert.Equal(64, result.CheckpointSha256.Length);
    }

    [Fact]
    public async Task SecureTransport_RejectsAnUnpinnedServerCertificateBeforeSendingCredentials()
    {
        var configuration = CreateConfiguration(ReserveLoopbackPort());
        var accountId = Guid.Parse("5f340000-0000-4000-8000-000000000051");
        const string token = "pinned-client-token-20260828";
        using var certificate = InternetTransportSecurity.CreateSelfSignedCertificate("pin-negative-test");
        var security = new InternetServerSecurityOptions(
            certificate,
            [new(accountId, InternetTransportSecurity.TokenSha256(token))]);
        await using var server = new TcpAuthoritativeServer(configuration, security: security);
        server.Start();

        var exception = await Assert.ThrowsAsync<InternetTransportException>(() =>
            TcpGameClient.ConnectAsync(new InternetClientOptions(
                "127.0.0.1",
                configuration.Port,
                "Elyra",
                configuration.ModuleFingerprint,
                AccountId: accountId,
                AuthenticationToken: token,
                RequireTls: true,
                PinnedServerCertificateSha256: new string('f', 64))));

        Assert.True(
            exception.Code == "NET_SERVER_CERTIFICATE_MISMATCH",
            $"client={exception.Code}:{exception.InnerException?.Message ?? exception.Message} " +
            $"server={server.LastTransportCode ?? "none"}");
        Assert.Equal(0, server.ConnectedPlayers);
    }

    [Fact]
    public void ClientOptions_RejectCredentialsOnPlaintextTransport()
    {
        var options = new InternetClientOptions(
            "127.0.0.1",
            5121,
            "Elyra",
            new string('a', 64),
            AuthenticationToken: "plaintext-token-must-not-leave-process");

        var exception = Assert.Throws<ArgumentException>(() =>
            TcpGameClient.ConnectAsync(options).GetAwaiter().GetResult());

        Assert.Contains("NET_AUTH_TOKEN_REQUIRES_TLS", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SessionInvite_RoundTripsPinnedTlsMaterialAndRejectsMalformedInput()
    {
        var expected = new InternetSessionInvite(
            InternetSessionInvite.CurrentSchemaVersion,
            "play.example.test",
            5121,
            new string('a', 64),
            "private-invitation-token-20260828");

        var encoded = expected.Encode();

        Assert.StartsWith(InternetSessionInvite.Prefix, encoded, StringComparison.Ordinal);
        Assert.True(InternetSessionInvite.TryDecode(encoded, out var decoded));
        Assert.Equal(expected, decoded);
        Assert.False(InternetSessionInvite.TryDecode(encoded[..^3] + "%%%", out _));
        Assert.False(InternetSessionInvite.TryDecode("openrpg2:not-base64", out _));
        var nullToken = InternetSessionInvite.Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(
                "{\"schemaVersion\":1,\"address\":\"127.0.0.1\",\"port\":5121," +
                $"\"certificateSha256\":\"{new string('a', 64)}\",\"authenticationToken\":null}}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.False(InternetSessionInvite.TryDecode(nullToken, out _));
    }

    [Fact]
    public void ServerSecurity_RejectsNullRegistryEntriesAsInvalidConfiguration()
    {
        using var certificate = InternetTransportSecurity.CreateSelfSignedCertificate("null-registry-test");

        var exception = Assert.Throws<ArgumentException>(() =>
            new InternetServerSecurityOptions(certificate, [null!]));

        Assert.Contains("NET_AUTH_ACCOUNT_REGISTRY_INVALID", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SecureServer_AcceptsWildcardInvitationClientsOutsideTheEngineSynchronizationContext()
    {
        var configuration = CreateConfiguration(ReserveLoopbackPort());
        const string token = "shared-private-invitation-token-20260828";
        using var certificate = InternetTransportSecurity.CreateSelfSignedCertificate("engine-context-test");
        var security = new InternetServerSecurityOptions(
            certificate,
            [new(Guid.Empty, InternetTransportSecurity.TokenSha256(token))]);
        await using var server = new TcpAuthoritativeServer(configuration, security: security);
        var previousContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(new NonPumpingSynchronizationContext());
            server.Start();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }

        await using var first = await ConnectSecureInvitationClientAsync(
            configuration,
            security,
            token,
            "Hote");
        await using var second = await ConnectSecureInvitationClientAsync(
            configuration,
            security,
            token,
            "Invite");

        Assert.True(first.PublishPose("area-entry", 1, 0, 1, 0));
        Assert.True(second.PublishPose("area-entry", 2, 0, 2, 0));
        var snapshot = await first.WaitForSnapshotAsync(
            item => item.Players.Count == 2,
            TimeSpan.FromSeconds(5));

        Assert.Equal(2, server.ConnectedPlayers);
        Assert.True(first.IsEncrypted && first.IsAuthenticated);
        Assert.True(second.IsEncrypted && second.IsAuthenticated);
        Assert.Equal(["Hote", "Invite"], snapshot.Players
            .Select(item => item.DisplayName)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray());
    }

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

    private static Task<TcpGameClient> ConnectSecureInvitationClientAsync(
        DedicatedServerConfiguration configuration,
        InternetServerSecurityOptions security,
        string token,
        string playerName) => TcpGameClient.ConnectAsync(new InternetClientOptions(
            "127.0.0.1",
            configuration.Port,
            playerName,
            configuration.ModuleFingerprint,
            AccountId: Guid.NewGuid(),
            AuthenticationToken: token,
            RequireTls: true,
            PinnedServerCertificateSha256: security.CertificateSha256));

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

    private sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state)
        {
            // Engine main-thread callbacks are intentionally not pumped during this test.
        }
    }
}
