using System.Diagnostics;
using System.Text;

namespace Forge.Mcp.Testing;

public sealed class ScenarioTestAgent
{
    public async Task<ScenarioTestResult> RunAsync(
        ScenarioTestRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ArtifactLogPath);
        var startedAt = DateTimeOffset.UtcNow;
        var output = new StringBuilder();
        var diagnostics = new List<AgentDiagnostic>();
        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            WorkingDirectory = string.IsNullOrWhiteSpace(request.WorkingDirectory)
                ? Environment.CurrentDirectory
                : Path.GetFullPath(request.WorkingDirectory),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, eventArgs) => AppendLine(output, eventArgs.Data);
        process.ErrorDataReceived += (_, eventArgs) => AppendLine(output, eventArgs.Data);
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("The scenario process did not start.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(request.Timeout);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
                diagnostics.Add(new AgentDiagnostic(
                    "scenario.timeout",
                    $"Scenario exceeded its {request.Timeout.TotalSeconds:0.###} second timeout."));
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            diagnostics.Add(new AgentDiagnostic("scenario.launch_failed", exception.Message));
        }

        var content = output.ToString();
        var missingMarkers = request.ExpectedMarkers
            .Where(marker => !content.Contains(marker, StringComparison.Ordinal))
            .ToArray();
        diagnostics.AddRange(missingMarkers.Select(marker => new AgentDiagnostic(
            "scenario.marker_missing",
            $"Expected marker '{marker}' was not emitted.")));
        diagnostics.AddRange(content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith("ERROR:", StringComparison.Ordinal) ||
                           line.Contains("OPENRPG_FORGE_", StringComparison.Ordinal) &&
                           (line.Contains("_FAILED", StringComparison.Ordinal) ||
                            line.Contains("_INVALID", StringComparison.Ordinal)))
            .Select(line => new AgentDiagnostic("scenario.runtime_error", line)));

        var artifactPath = Path.GetFullPath(request.ArtifactLogPath);
        Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
        await File.WriteAllTextAsync(artifactPath, content, new UTF8Encoding(false), cancellationToken);
        int? exitCode = process.HasExited ? process.ExitCode : null;
        if (exitCode is not 0 && diagnostics.All(item => item.Code != "scenario.launch_failed"))
        {
            diagnostics.Add(new AgentDiagnostic("scenario.exit_code", $"Scenario exited with code {exitCode}."));
        }

        return new ScenarioTestResult(
            diagnostics.Count == 0,
            exitCode,
            DateTimeOffset.UtcNow - startedAt,
            request.ExpectedMarkers.Except(missingMarkers, StringComparer.Ordinal).ToArray(),
            missingMarkers,
            diagnostics,
            artifactPath);
    }

    private static void AppendLine(StringBuilder output, string? value)
    {
        if (value is null)
        {
            return;
        }

        lock (output)
        {
            output.AppendLine(value);
        }
    }
}

public sealed record ScenarioTestRequest(
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyList<string> ExpectedMarkers,
    TimeSpan Timeout,
    string ArtifactLogPath);

public sealed record ScenarioTestResult(
    bool Succeeded,
    int? ExitCode,
    TimeSpan Duration,
    IReadOnlyList<string> FoundMarkers,
    IReadOnlyList<string> MissingMarkers,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    string ArtifactLogPath);
