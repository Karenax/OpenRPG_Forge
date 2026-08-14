using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using Forge.ModuleFormat.Launcher;

namespace Forge.GameLauncher;

public partial class App
{
    private async void OnStartup(object sender, StartupEventArgs eventArgs)
    {
        var root = AppContext.BaseDirectory;
        try
        {
            _ = GameLauncherManifestStore.LoadAndVerifyDistribution(root);
            var manifest = GameLauncherManifestStore.Load(root, verifyHashes: false);
            var verification = GameLauncherManifestStore.Verify(root, manifest, verifyHashes: false);
            var settingsPath = GameLauncherSettingsStore.GetDefaultPath(manifest);
            var settings = GameLauncherSettingsStore.LoadOrDefault(settingsPath, manifest);
            if (eventArgs.Args.Contains("--verify", StringComparer.Ordinal))
            {
                Shutdown(0);
                return;
            }

            var captureArgument = eventArgs.Args.FirstOrDefault(argument =>
                argument.StartsWith("--capture=", StringComparison.Ordinal));
            var pageArgument = eventArgs.Args.FirstOrDefault(argument =>
                argument.StartsWith("--page=", StringComparison.Ordinal));
            var window = new LauncherWindow(verification, settings, settingsPath);
            MainWindow = window;
            if (captureArgument is not null)
            {
                var capturePath = Path.GetFullPath(captureArgument["--capture=".Length..]);
                var page = pageArgument?["--page=".Length..] ?? "home";
                await window.CapturePreviewAsync(capturePath, page);
                Shutdown(0);
                return;
            }

            ShutdownMode = ShutdownMode.OnMainWindowClose;
            window.Show();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or
                                           UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            WriteFailure(root, exception);
            if (!eventArgs.Args.Contains("--verify", StringComparer.Ordinal))
            {
                MessageBox.Show(
                    "Le lanceur ne peut pas démarrer.\n\n" + exception.Message +
                    "\n\nConsultez launcher-error.log à côté de l'application.",
                    "Lanceur OpenRPG Forge",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            Shutdown(1);
        }
    }

    private static void WriteFailure(string root, Exception exception)
    {
        try
        {
            var text = new StringBuilder()
                .AppendLine("OpenRPG Forge game launcher failure")
                .AppendLine(DateTimeOffset.UtcNow.ToString("O"))
                .AppendLine(exception.ToString())
                .ToString();
            File.WriteAllText(Path.Combine(root, "launcher-error.log"), text, new UTF8Encoding(false));
        }
        catch
        {
            // The initial validation or launch failure remains the actionable result on read-only media.
        }
    }
}
