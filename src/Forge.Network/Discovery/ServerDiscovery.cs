using Forge.Network.Sessions;

namespace Forge.Network.Discovery;

public sealed record ServerEndpoint(Guid ServerId, string Name, string Address, int Port, int Players, int Capacity, string ProtocolVersion, string ModuleFingerprint);

public interface IServerDirectory
{
    IReadOnlyList<ServerEndpoint> BrowseLan();
    ServerEndpoint? Resolve(string address, int port);
}

public sealed class InMemoryServerDirectory : IServerDirectory
{
    private readonly Dictionary<(string Address, int Port), ServerEndpoint> _servers = [];

    public void Publish(DedicatedServerConfiguration configuration, int players = 0)
    {
        var endpoint = new ServerEndpoint(configuration.ServerId, configuration.Name, configuration.BindAddress, configuration.Port, players, configuration.Capacity, configuration.ProtocolVersion, configuration.ModuleFingerprint);
        _servers[(configuration.BindAddress, configuration.Port)] = endpoint;
    }

    public IReadOnlyList<ServerEndpoint> BrowseLan() => _servers.Values.OrderBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.ServerId).ToArray();
    public ServerEndpoint? Resolve(string address, int port) => _servers.GetValueOrDefault((address, port));
}
