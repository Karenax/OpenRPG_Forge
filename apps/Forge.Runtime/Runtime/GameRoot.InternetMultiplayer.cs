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

    private void ReleaseLauncherNetworkSessionAfterStartupFailure()
    {
        if (_launcherNetworkSession is null ||
            !ReferenceEquals(_launcherNetworkSession, s_launcherNetworkSession)) return;
        _launcherNetworkSession.Dispose();
        _launcherNetworkSession = null;
        s_launcherNetworkSession = null;
    }
}
