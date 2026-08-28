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
    string RejectionCode,
    string AuthenticationRejectionCode,
    string TlsProtocol,
    int TargetHealth,
    int RemainingPotions,
    string QuestState,
    long SaveRevision,
    string CheckpointSha256);

public static class InternetTransportAcceptanceScenario
{
    public static async Task<InternetTransportAcceptanceResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        var fingerprint = new string('a', 64);
        var firstAccount = Guid.Parse("5f340000-0000-4000-8000-000000000111");
        var secondAccount = Guid.Parse("5f340000-0000-4000-8000-000000000112");
        var thirdAccount = Guid.Parse("5f340000-0000-4000-8000-000000000113");
        const string firstToken = "elyra-secure-token-20260828";
        const string secondToken = "borin-secure-token-20260828";
        const string thirdToken = "mismatch-secure-token-20260828";
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
        using var certificate = InternetTransportSecurity.CreateSelfSignedCertificate("openrpg-loopback-smoke");
        var security = new InternetServerSecurityOptions(
            certificate,
            [
                new(firstAccount, InternetTransportSecurity.TokenSha256(firstToken)),
                new(secondAccount, InternetTransportSecurity.TokenSha256(secondToken)),
                new(thirdAccount, InternetTransportSecurity.TokenSha256(thirdToken)),
            ]);
        await using var server = new TcpAuthoritativeServer(
            configuration,
            Guid.Parse("5f340000-0000-4000-8000-000000000101"),
            security);
        server.Start();
        await using var first = await TcpGameClient.ConnectAsync(
            new InternetClientOptions(
                "127.0.0.1", configuration.Port, "Elyra", fingerprint,
                ConnectionId: Guid.Parse("5f340000-0000-4000-8000-000000000121"),
                AccountId: firstAccount,
                AuthenticationToken: firstToken,
                RequireTls: true,
                PinnedServerCertificateSha256: security.CertificateSha256),
            cancellationToken);
        await using var second = await TcpGameClient.ConnectAsync(
            new InternetClientOptions(
                "127.0.0.1", configuration.Port, "Borin", fingerprint,
                ConnectionId: Guid.Parse("5f340000-0000-4000-8000-000000000122"),
                AccountId: secondAccount,
                AuthenticationToken: secondToken,
                RequireTls: true,
                PinnedServerCertificateSha256: security.CertificateSha256),
            cancellationToken);
        first.PublishPose("entry-area", 1, 0, 2, .25);
        second.PublishPose("entry-area", 3, 0, 4, .75);
        var snapshot = await first.WaitForSnapshotAsync(
            item => item.Players.Count == 2,
            TimeSpan.FromSeconds(5),
            cancellationToken);

        _ = await first.WaitForGameplaySnapshotAsync(
            item => item.Combatants.Count == 2,
            TimeSpan.FromSeconds(5),
            cancellationToken);
        await first.PublishGameplayIntentAsync(
            "quest.advance",
            new Dictionary<string, string> { ["action"] = "begin" },
            cancellationToken);
        _ = await first.WaitForGameplaySnapshotAsync(
            item => item.EventCode == "NET_QUEST_ADVANCE_APPLIED" &&
                    item.PrivateState.Quests["network-trial"] == "active",
            TimeSpan.FromSeconds(5),
            cancellationToken);
        await first.PublishGameplayIntentAsync(
            "combat.attack",
            new Dictionary<string, string> { ["targetConnectionId"] = second.ConnectionId.ToString("D") },
            cancellationToken);
        var attacked = await second.WaitForGameplaySnapshotAsync(
            item => item.EventCode == "NET_COMBAT_ATTACK_APPLIED" &&
                    item.Combatants.Single(state => state.ConnectionId == second.ConnectionId).Health == 70,
            TimeSpan.FromSeconds(5),
            cancellationToken);
        await second.PublishGameplayIntentAsync(
            "inventory.use",
            new Dictionary<string, string> { ["item"] = "healing-potion" },
            cancellationToken);
        var healed = await second.WaitForGameplaySnapshotAsync(
            item => item.EventCode == "NET_INVENTORY_USE_APPLIED" &&
                    item.PrivateState.Inventory["healing-potion"] == 1,
            TimeSpan.FromSeconds(5),
            cancellationToken);
        _ = await first.WaitForGameplaySnapshotAsync(
            item => item.Revision >= healed.Revision,
            TimeSpan.FromSeconds(5),
            cancellationToken);
        await first.PublishGameplayIntentAsync(
            "quest.advance",
            new Dictionary<string, string> { ["action"] = "claim" },
            cancellationToken);
        var quest = await first.WaitForGameplaySnapshotAsync(
            item => item.EventCode == "NET_QUEST_ADVANCE_APPLIED" &&
                    item.PrivateState.Quests["network-trial"] == "completed",
            TimeSpan.FromSeconds(5),
            cancellationToken);
        await first.PublishGameplayIntentAsync(
            "save.checkpoint",
            new Dictionary<string, string>(),
            cancellationToken);
        var checkpoint = await first.WaitForGameplaySnapshotAsync(
            item => item.EventCode == "NET_SAVE_CHECKPOINT_APPLIED" && item.PrivateState.SaveRevision == 1,
            TimeSpan.FromSeconds(5),
            cancellationToken);

        var authenticationRejectionCode = "none";
        try
        {
            await using var unauthenticated = await TcpGameClient.ConnectAsync(
                new InternetClientOptions(
                    "127.0.0.1", configuration.Port, "Intrus", fingerprint,
                    AccountId: thirdAccount,
                    AuthenticationToken: "incorrect-secure-token-20260828",
                    RequireTls: true,
                    PinnedServerCertificateSha256: security.CertificateSha256),
                cancellationToken);
        }
        catch (InternetTransportException exception)
        {
            authenticationRejectionCode = exception.Code;
        }

        var rejectionCode = "none";
        try
        {
            await using var incompatible = await TcpGameClient.ConnectAsync(
                new InternetClientOptions(
                    "127.0.0.1",
                    configuration.Port,
                    "Incompatible",
                    "different-module",
                    AccountId: thirdAccount,
                    AuthenticationToken: thirdToken,
                    RequireTls: true,
                    PinnedServerCertificateSha256: security.CertificateSha256),
                cancellationToken);
        }
        catch (InternetTransportException exception)
        {
            rejectionCode = exception.Code;
        }

        return new InternetTransportAcceptanceResult(
            snapshot.Players.Count == 2 && rejectionCode == "NET_CONTENT_MISMATCH" &&
            authenticationRejectionCode == "NET_AUTH_INVALID_CREDENTIALS" &&
            first.IsEncrypted && first.IsAuthenticated && second.IsEncrypted && second.IsAuthenticated &&
            attacked.Combatants.Single(item => item.ConnectionId == second.ConnectionId).Health == 70 &&
            healed.PrivateState.Inventory["healing-potion"] == 1 &&
            quest.PrivateState.Quests["network-trial"] == "completed" &&
            checkpoint.PrivateState.SaveRevision == 1 && checkpoint.PrivateState.CheckpointSha256 is { Length: 64 },
            snapshot.SessionId,
            snapshot.Revision,
            snapshot.Players.Count,
            rejectionCode,
            authenticationRejectionCode,
            first.TlsProtocol ?? "none",
            attacked.Combatants.Single(item => item.ConnectionId == second.ConnectionId).Health,
            healed.PrivateState.Inventory["healing-potion"],
            quest.PrivateState.Quests["network-trial"],
            checkpoint.PrivateState.SaveRevision,
            checkpoint.PrivateState.CheckpointSha256 ?? "none");
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
