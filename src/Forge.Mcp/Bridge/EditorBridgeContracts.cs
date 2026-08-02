namespace Forge.Mcp.Bridge;

public sealed record EditorBridgeDiscovery(
    int ProtocolVersion,
    string Transport,
    string PipeName,
    string SessionToken,
    string ProjectId,
    int ProcessId,
    DateTimeOffset ExpiresAtUtc,
    string DiscoveryPath);

public sealed record EditorBridgeRequest(string SessionToken, string Operation);

public sealed record EditorBridgeResponse(
    bool Succeeded,
    object? Result,
    IReadOnlyList<AgentDiagnostic> Diagnostics);
