using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Forge.Mcp.Bridge;

public sealed class ForgeEditorBridgeClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
    };

    private readonly EditorBridgeDiscovery _discovery;

    private ForgeEditorBridgeClient(EditorBridgeDiscovery discovery)
    {
        _discovery = discovery;
    }

    public static ForgeEditorBridgeClient FromDiscoveryFile(string discoveryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(discoveryPath);
        var discovery = JsonSerializer.Deserialize<EditorBridgeDiscovery>(
            File.ReadAllText(discoveryPath),
            JsonOptions) ?? throw new JsonException("The editor bridge discovery file is empty.");
        if (discovery.ProtocolVersion != 1 || discovery.Transport != "named-pipe-current-user")
        {
            throw new NotSupportedException("The editor bridge discovery protocol or transport is unsupported.");
        }

        return new ForgeEditorBridgeClient(discovery);
    }

    public async Task<EditorBridgeResponse> QueryAsync(
        string operation,
        CancellationToken cancellationToken = default)
    {
        await using var pipe = new NamedPipeClientStream(
            ".",
            _discovery.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(TimeSpan.FromSeconds(5), cancellationToken);
        using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true,
        };
        var request = new EditorBridgeRequest(_discovery.SessionToken, operation);
        await writer.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions).AsMemory(), cancellationToken);
        var response = await reader.ReadLineAsync(cancellationToken);
        return JsonSerializer.Deserialize<EditorBridgeResponse>(response ?? string.Empty, JsonOptions)
            ?? throw new JsonException("The editor bridge returned an empty response.");
    }
}
