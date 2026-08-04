using Forge.Network.Configuration;
using Forge.Network.Sessions;

namespace Forge.Network.Authoring;

public sealed record NetworkConfigurationCommand(DedicatedServerConfiguration Before, DedicatedServerConfiguration After, string ActorId, bool Apply);
public sealed record NetworkAuthoringResult(bool Applied, DedicatedServerConfiguration Configuration, IReadOnlyList<NetworkDiagnostic> Diagnostics, string SemanticDiff, string AuditEntry);

public sealed class NetworkAuthoringService
{
    private readonly NetworkConfigurationStore _store = new();

    public NetworkAuthoringResult Execute(NetworkConfigurationCommand command)
    {
        var diagnostics = NetworkValidation.Validate(command.After);
        var diff = BuildDiff(command.Before, command.After);
        var canApply = diagnostics.Count == 0 && !string.IsNullOrWhiteSpace(command.ActorId);
        if (!canApply)
        {
            if (string.IsNullOrWhiteSpace(command.ActorId))
                diagnostics = diagnostics.Append(new("NET_AUTHOR_ID_REQUIRED", "/audit/actorId", "An author identity is required.")).ToArray();
            return new(false, command.Before, diagnostics, diff, "rejected:network.configure");
        }

        if (!command.Apply) return new(false, command.Before, diagnostics, diff, $"previewed:network.configure:actor={command.ActorId}");
        _ = _store.Serialize(command.After);
        return new(true, command.After, diagnostics, diff, $"applied:network.configure:actor={command.ActorId}:server={command.After.ServerId}");
    }

    private static string BuildDiff(DedicatedServerConfiguration before, DedicatedServerConfiguration after)
    {
        var changes = new List<string>();
        if (before.Name != after.Name) changes.Add($"name: {before.Name} -> {after.Name}");
        if (before.Port != after.Port) changes.Add($"port: {before.Port} -> {after.Port}");
        if (before.Capacity != after.Capacity) changes.Add($"capacity: {before.Capacity} -> {after.Capacity}");
        if (before.Discovery != after.Discovery) changes.Add($"discovery: {before.Discovery} -> {after.Discovery}");
        return changes.Count == 0 ? "no changes" : string.Join("; ", changes);
    }
}
