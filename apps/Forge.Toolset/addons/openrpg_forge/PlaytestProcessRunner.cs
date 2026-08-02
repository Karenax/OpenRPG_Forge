#if TOOLS
using System.Diagnostics;
using System.ComponentModel;
using System.Text;
using Forge.ModuleFormat.Playtesting;

namespace Forge.Toolset.Editor;

internal static class PlaytestProcessRunner
{
    internal static async Task<PlaytestProcessResult> RunAsync(
        string executablePath,
        string runtimeProjectDirectory,
        PlaytestSnapshot snapshot,
        bool headless,
        bool runSmokeScenario)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = CreateStartInfo(
                    executablePath,
                    runtimeProjectDirectory,
                    snapshot,
                    headless,
                    runSmokeScenario),
            };

            if (!process.Start())
            {
                return new PlaytestProcessResult(-1, string.Empty, string.Empty, "The runtime process did not start.");
            }

            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            await Task.WhenAll(standardOutput, standardError);
            return new PlaytestProcessResult(
                process.ExitCode,
                standardOutput.Result,
                standardError.Result,
                LaunchError: null);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or IOException or UnauthorizedAccessException or Win32Exception)
        {
            return new PlaytestProcessResult(-1, string.Empty, string.Empty, exception.Message);
        }
    }

    private static ProcessStartInfo CreateStartInfo(
        string executablePath,
        string runtimeProjectDirectory,
        PlaytestSnapshot snapshot,
        bool headless,
        bool runSmokeScenario)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = runtimeProjectDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = headless,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        if (headless)
        {
            startInfo.ArgumentList.Add("--headless");
        }

        startInfo.ArgumentList.Add("--path");
        startInfo.ArgumentList.Add(runtimeProjectDirectory);
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("--project-snapshot");
        startInfo.ArgumentList.Add(snapshot.ProjectFilePath);
        startInfo.ArgumentList.Add("--spawn-id");
        startInfo.ArgumentList.Add(snapshot.SpawnEntityId.ToString());
        if (runSmokeScenario)
        {
            startInfo.ArgumentList.Add("--smoke-test");
        }

        return startInfo;
    }
}

internal sealed record PlaytestProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    string? LaunchError)
{
    internal bool Succeeded => ExitCode == 0 && LaunchError is null;
}
#endif
