using Godot;

namespace Forge.Runtime;

public partial class GameRoot
{
    private static LauncherRuntimeNetworkSession? s_launcherNetworkSession;
    private LauncherRuntimeNetworkSession? _launcherNetworkSession;
    private RuntimeInternetMultiplayer? _runtimeInternetMultiplayer;

    static GameRoot()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            s_launcherNetworkSession?.Dispose();
            s_launcherNetworkSession = null;
        };
    }

    private async Task AcquireLauncherNetworkSessionAsync(
        LauncherRuntimeContext? context,
        string moduleFingerprint,
        string moduleTitle)
    {
        if (context is null || context.Settings.SessionMode == Forge.ModuleFormat.Launcher.LauncherSessionMode.Solo)
        {
            _launcherNetworkSession = null;
            return;
        }

        if (s_launcherNetworkSession is not null)
        {
            if (!string.Equals(s_launcherNetworkSession.ModuleFingerprint, moduleFingerprint, StringComparison.Ordinal) ||
                !string.Equals(
                    s_launcherNetworkSession.RequestedSessionMode,
                    context.RequestedSessionMode,
                    StringComparison.Ordinal))
                throw new InvalidOperationException("A different launcher network session is already active.");
            _launcherNetworkSession = s_launcherNetworkSession;
            return;
        }

        s_launcherNetworkSession = await LauncherRuntimeBootstrap.CreateNetworkSessionAsync(
            context,
            moduleFingerprint,
            moduleTitle);
        _launcherNetworkSession = s_launcherNetworkSession;
    }

    private void ConfigureLauncherInternetReplication()
    {
        if (_launcherNetworkSession is null || _player is null) return;
        _runtimeInternetMultiplayer = new RuntimeInternetMultiplayer();
        _runtimeInternetMultiplayer.Configure(
            _launcherNetworkSession.Client,
            _player,
            () => _activeArea?.Id.ToString());
        AddChild(_runtimeInternetMultiplayer);
    }

    private int LauncherConnectedPlayerCount() => _launcherNetworkSession?.ConnectedPlayers ?? 1;

    private async Task RunSecureInternetRuntimeSmokeTestAsync()
    {
        var session = _launcherNetworkSession
            ?? throw new InvalidOperationException("The secure Internet runtime smoke requires a launcher session.");
        var client = session.Client;
        AssertSmoke(client.IsEncrypted && client.IsAuthenticated &&
                    client.ServerCertificateSha256 is { Length: 64 },
            "The launcher runtime session is not authenticated over pinned TLS.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        _ = await client.WaitForSnapshotAsync(
            snapshot => snapshot.Players.Count == 2,
            TimeSpan.FromSeconds(55),
            timeout.Token);
        _ = await client.WaitForGameplaySnapshotAsync(
            snapshot => snapshot.Combatants.Count == 2,
            TimeSpan.FromSeconds(55),
            timeout.Token);

        if (session.IsHost)
        {
            await client.PublishGameplayIntentAsync(
                "quest.advance",
                new Dictionary<string, string> { ["action"] = "begin" },
                timeout.Token);
            _ = await client.WaitForGameplaySnapshotAsync(
                snapshot => snapshot.EventCode == "NET_QUEST_ADVANCE_APPLIED" &&
                            snapshot.PrivateState.Quests["network-trial"] == "active",
                TimeSpan.FromSeconds(10),
                timeout.Token);
            var target = client.LatestGameplaySnapshot!.Combatants.Single(item =>
                item.ConnectionId != client.ConnectionId);
            await client.PublishGameplayIntentAsync(
                "combat.attack",
                new Dictionary<string, string>
                {
                    ["targetConnectionId"] = target.ConnectionId.ToString("D"),
                },
                timeout.Token);
            _ = await client.WaitForGameplaySnapshotAsync(
                snapshot => snapshot.EventCode == "NET_COMBAT_ATTACK_APPLIED" &&
                            snapshot.Combatants.Single(item => item.ConnectionId == target.ConnectionId).Health == 70,
                TimeSpan.FromSeconds(10),
                timeout.Token);
            _ = await client.WaitForGameplaySnapshotAsync(
                snapshot => snapshot.EventCode == "NET_INVENTORY_USE_APPLIED",
                TimeSpan.FromSeconds(10),
                timeout.Token);
            await client.PublishGameplayIntentAsync(
                "quest.advance",
                new Dictionary<string, string> { ["action"] = "claim" },
                timeout.Token);
            _ = await client.WaitForGameplaySnapshotAsync(
                snapshot => snapshot.EventCode == "NET_QUEST_ADVANCE_APPLIED" &&
                            snapshot.PrivateState.Quests["network-trial"] == "completed",
                TimeSpan.FromSeconds(10),
                timeout.Token);
            await client.PublishGameplayIntentAsync(
                "save.checkpoint",
                new Dictionary<string, string>(),
                timeout.Token);
            var checkpoint = await client.WaitForGameplaySnapshotAsync(
                snapshot => snapshot.EventCode == "NET_SAVE_CHECKPOINT_APPLIED" &&
                            snapshot.PrivateState.SaveRevision == 1 &&
                            snapshot.PrivateState.CheckpointSha256 is { Length: 64 },
                TimeSpan.FromSeconds(10),
                timeout.Token);
            GD.Print(
                $"OPENRPG_FORGE_RUNTIME_SECURE_INTERNET_HOST_PASS tls={client.TlsProtocol} players=2 " +
                $"combat=70 inventory=healed quest=completed save={checkpoint.PrivateState.SaveRevision} " +
                $"checkpoint={checkpoint.PrivateState.CheckpointSha256}");
        }
        else
        {
            _ = await client.WaitForGameplaySnapshotAsync(
                snapshot => snapshot.Combatants.Single(item => item.ConnectionId == client.ConnectionId).Health == 70,
                TimeSpan.FromSeconds(20),
                timeout.Token);
            await client.PublishGameplayIntentAsync(
                "inventory.use",
                new Dictionary<string, string> { ["item"] = "healing-potion" },
                timeout.Token);
            var healed = await client.WaitForGameplaySnapshotAsync(
                snapshot => snapshot.EventCode == "NET_INVENTORY_USE_APPLIED" &&
                            snapshot.PrivateState.Inventory["healing-potion"] == 1 &&
                            snapshot.Combatants.Single(item => item.ConnectionId == client.ConnectionId).Health == 95,
                TimeSpan.FromSeconds(10),
                timeout.Token);
            _ = await client.WaitForGameplaySnapshotAsync(
                snapshot => snapshot.EventCode == "NET_SAVE_CHECKPOINT_APPLIED",
                TimeSpan.FromSeconds(20),
                timeout.Token);
            GD.Print(
                $"OPENRPG_FORGE_RUNTIME_SECURE_INTERNET_JOIN_PASS tls={client.TlsProtocol} players=2 " +
                $"health=95 inventory={healed.PrivateState.Inventory["healing-potion"]} authority=server");
        }

        GetTree().Quit(0);
    }

    private void ReleaseLauncherNetworkSessionAfterStartupFailure()
    {
        if (_launcherNetworkSession is null ||
            !ReferenceEquals(_launcherNetworkSession, s_launcherNetworkSession)) return;
        _runtimeInternetMultiplayer?.Stop();
        _launcherNetworkSession.Dispose();
        _launcherNetworkSession = null;
        s_launcherNetworkSession = null;
    }
}
