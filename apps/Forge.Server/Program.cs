using Forge.Network.Configuration;
using Forge.Network.Sessions;
using Forge.Network.Scenarios;
using Forge.LiveDirector.Scenarios;

if (args.Contains("--live-director-smoke-test", StringComparer.Ordinal))
{
    var result = LiveDirectorScenario.Run();
    Console.WriteLine($"OPENRPG_FORGE_LIVE_DIRECTOR_NETWORK_PASS digest={result.NetworkDigest}");
    Console.WriteLine($"OPENRPG_FORGE_LIVE_DIRECTOR_AUTHORITY_PASS gm={result.SpawnCode} player={result.RejectedPlayerCode}");
    Console.WriteLine($"OPENRPG_FORGE_LIVE_DIRECTOR_AUDIT_PASS entries={result.AuditEntries} hash={result.AuditHash}");
    Console.WriteLine($"OPENRPG_FORGE_LIVE_DIRECTOR={(result.Passed ? "PASS" : "FAILED")}");
    Environment.ExitCode = result.Passed ? 0 : 5;
    return;
}

if (args.Contains("--network-beta-smoke-test", StringComparer.Ordinal))
{
    var result = NetworkBetaScenario.Run();
    Console.WriteLine($"OPENRPG_FORGE_NETWORK_TWO_CLIENTS_PASS revision={result.ServerRevision} digest={result.SharedDigest}");
    Console.WriteLine($"OPENRPG_FORGE_NETWORK_RECONNECT_PASS code={result.ReconnectionCode} clientA={result.ClientARevision} clientB={result.ClientBRevision}");
    Console.WriteLine($"OPENRPG_FORGE_NETWORK_AUTHORITY_PASS forged={result.ForgedMutationCode}");
    Console.WriteLine($"OPENRPG_FORGE_NETWORK_BETA={(result.Passed ? "PASS" : "FAILED")}");
    Environment.ExitCode = result.Passed ? 0 : 4;
    return;
}

if (args.Contains("--self-test", StringComparer.Ordinal))
{
    var config = new DedicatedServerConfiguration(Guid.Parse("10000000-0000-4000-8000-000000000001"), 1, "OpenRPG Forge Network Beta", "127.0.0.1", 5121, 8, DiscoveryMode.Direct, "self-test", "1.0");
    var session = new AuthoritativeSession(config, Guid.Parse("20000000-0000-4000-8000-000000000001"));
    Console.WriteLine($"OPENRPG_FORGE_SERVER_READY server={config.ServerId} session={session.SessionId}");
    Console.WriteLine("OPENRPG_FORGE_SERVER_HEALTH status=healthy revision=0 players=0");
    return;
}

var configArgument = args.FirstOrDefault(value => value.StartsWith("--config=", StringComparison.Ordinal));
if (configArgument is null)
{
    Console.Error.WriteLine("NET_CONFIG_ARGUMENT_REQUIRED: use --config=<path>");
    Environment.ExitCode = 2;
    return;
}

try
{
    var path = Path.GetFullPath(configArgument["--config=".Length..]);
    var configuration = new NetworkConfigurationStore().Deserialize(await File.ReadAllTextAsync(path));
    var session = new AuthoritativeSession(configuration);
    Console.WriteLine($"OPENRPG_FORGE_SERVER_READY server={configuration.ServerId} session={session.SessionId}");
    Console.WriteLine($"OPENRPG_FORGE_SERVER_HEALTH status=healthy revision={session.Snapshot.Revision} players={session.Snapshot.Members.Count}");
}
catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"OPENRPG_FORGE_SERVER_INVALID {exception.Message}");
    Environment.ExitCode = 3;
}
