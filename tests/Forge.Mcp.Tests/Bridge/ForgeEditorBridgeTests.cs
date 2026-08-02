using System.Text.Json;
using Forge.Mcp.Bridge;

namespace Forge.Mcp.Tests.Bridge;

public sealed class ForgeEditorBridgeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public async Task BridgeDiscoversAuthenticatesAndReconnectsWithoutNetworkBinding()
    {
        using var workspace = new TestWorkspace();
        var discoveryDirectory = Path.Combine(workspace.RootDirectory, ".bridge");
        var server = new ForgeEditorBridgeServer(workspace.Project, discoveryDirectory);
        var discoveryPath = server.Discovery.DiscoveryPath;
        await server.StartAsync();

        try
        {
            Assert.True(File.Exists(discoveryPath));
            Assert.Equal("named-pipe-current-user", server.Discovery.Transport);
            Assert.DoesNotContain("tcp", server.Discovery.Transport, StringComparison.OrdinalIgnoreCase);
            var client = ForgeEditorBridgeClient.FromDiscoveryFile(discoveryPath);

            var summary = await client.QueryAsync("project.summary");
            var revision = await client.QueryAsync("project.revision");

            Assert.True(summary.Succeeded);
            Assert.True(revision.Succeeded);

            var invalidDiscovery = server.Discovery with
            {
                SessionToken = "invalid-token",
                DiscoveryPath = Path.Combine(discoveryDirectory, "invalid.json"),
            };
            File.WriteAllText(invalidDiscovery.DiscoveryPath, JsonSerializer.Serialize(invalidDiscovery, JsonOptions));
            var invalidClient = ForgeEditorBridgeClient.FromDiscoveryFile(invalidDiscovery.DiscoveryPath);
            var denied = await invalidClient.QueryAsync("project.summary");

            Assert.False(denied.Succeeded);
            Assert.Equal("bridge.unauthorized", Assert.Single(denied.Diagnostics).Code);
        }
        finally
        {
            await server.DisposeAsync();
        }

        Assert.False(File.Exists(discoveryPath));
    }
}
