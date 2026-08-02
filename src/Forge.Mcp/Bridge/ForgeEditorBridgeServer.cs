using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Mcp.Projects;

namespace Forge.Mcp.Bridge;

public sealed class ForgeEditorBridgeServer : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
    };

    private readonly ForgeProjectAgentService _project;
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _serverTask;

    public EditorBridgeDiscovery Discovery { get; }

    public ForgeEditorBridgeServer(
        ForgeProjectAgentService project,
        string discoveryDirectory,
        TimeSpan? sessionLifetime = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(discoveryDirectory);
        _project = project;
        var summary = project.GetSummary();
        var sessionId = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var fullDirectory = Path.GetFullPath(discoveryDirectory);
        Discovery = new EditorBridgeDiscovery(
            1,
            "named-pipe-current-user",
            $"openrpg-forge-{sessionId}",
            token,
            summary.ProjectId,
            Environment.ProcessId,
            DateTimeOffset.UtcNow.Add(sessionLifetime ?? TimeSpan.FromHours(8)),
            Path.Combine(fullDirectory, $"{summary.ProjectId}.json"));
    }

    public Task StartAsync()
    {
        if (_serverTask is not null)
        {
            throw new InvalidOperationException("The editor bridge is already running.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Discovery.DiscoveryPath)!);
        WriteDiscoveryAtomically();
        _serverTask = RunServerAsync(_shutdown.Token);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _shutdown.CancelAsync();
        if (_serverTask is not null)
        {
            try
            {
                await _serverTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (File.Exists(Discovery.DiscoveryPath))
        {
            File.Delete(Discovery.DiscoveryPath);
        }

        _shutdown.Dispose();
    }

    private async Task RunServerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                Discovery.PipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.WaitForConnectionAsync(cancellationToken);
            await HandleConnectionAsync(pipe, cancellationToken);
        }
    }

    private async Task HandleConnectionAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true,
        };
        var line = await reader.ReadLineAsync(cancellationToken);
        EditorBridgeResponse response;
        try
        {
            var request = JsonSerializer.Deserialize<EditorBridgeRequest>(line ?? string.Empty, JsonOptions)
                ?? throw new JsonException("The bridge request is empty.");
            response = Authorize(request)
                ? Execute(request)
                : Failure("bridge.unauthorized", "The editor bridge session token is invalid or expired.");
        }
        catch (JsonException exception)
        {
            response = Failure("bridge.request_invalid", exception.Message);
        }

        await writer.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions).AsMemory(), cancellationToken);
    }

    private bool Authorize(EditorBridgeRequest request)
    {
        if (DateTimeOffset.UtcNow >= Discovery.ExpiresAtUtc)
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(Discovery.SessionToken);
        var actual = Encoding.UTF8.GetBytes(request.SessionToken ?? string.Empty);
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private EditorBridgeResponse Execute(EditorBridgeRequest request) => request.Operation switch
    {
        "project.summary" => new EditorBridgeResponse(true, _project.GetSummary(), []),
        "project.revision" => new EditorBridgeResponse(true, _project.GetCurrentRevision(), []),
        "project.diagnostics" => new EditorBridgeResponse(true, _project.GetDiagnostics(), []),
        _ => Failure("bridge.operation_unknown", $"Bridge operation '{request.Operation}' is not available."),
    };

    private void WriteDiscoveryAtomically()
    {
        var path = Discovery.DiscoveryPath;
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(Discovery, JsonOptions), new UTF8Encoding(false));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static EditorBridgeResponse Failure(string code, string message) =>
        new(false, null, [new AgentDiagnostic(code, message)]);
}
