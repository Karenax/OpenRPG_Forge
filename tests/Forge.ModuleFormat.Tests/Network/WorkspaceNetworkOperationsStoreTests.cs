using System.Text;
using Forge.ModuleFormat.Network;
using Forge.Network.Authoring;

namespace Forge.ModuleFormat.Tests.Network;

public sealed class WorkspaceNetworkOperationsStoreTests
{
    [Fact]
    public void MissingDocumentReturnsExplicitDefaultWithoutWriting()
    {
        WithWorkspace(root =>
        {
            var loaded = WorkspaceNetworkOperationsStore.Load(root);

            Assert.True(loaded.Succeeded);
            Assert.True(loaded.FileMissing);
            Assert.Empty(NetworkOperationsValidator.Validate(loaded.Profile));
            Assert.False(File.Exists(Path.Combine(root, ".forge", "network", "operations.json")));
        });
    }

    [Fact]
    public void SaveIsAtomicDeterministicAndRoundTripsExactly()
    {
        WithWorkspace(root =>
        {
            var initial = NetworkOperationsProfile.CreateDefault("test:ux9-b");
            var candidate = initial with
            {
                ServerRevision = 7,
                Content = initial.Content with { ChunkBytes = 32 * 1024 },
                Chat = initial.Chat with { LocalRadius = 48 },
                Events = initial.Events with { MaximumPendingEvents = 32 }
            };

            var saved = WorkspaceNetworkOperationsStore.Save(root, candidate);
            var loaded = WorkspaceNetworkOperationsStore.Load(root);
            var path = Path.Combine(root, ".forge", "network", "operations.json");
            var bytes = File.ReadAllBytes(path);

            Assert.True(saved.Succeeded);
            Assert.True(loaded.Succeeded);
            Assert.False(loaded.FileMissing);
            var canonical = new NetworkOperationsStore();
            Assert.Equal(canonical.Serialize(candidate), canonical.Serialize(loaded.Profile));
            Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
            Assert.EndsWith("\n", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
            Assert.DoesNotContain("\r\n", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
            Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp"));

            Assert.True(WorkspaceNetworkOperationsStore.Save(root, loaded.Profile).Succeeded);
            Assert.Equal(bytes, File.ReadAllBytes(path));
        });
    }

    [Fact]
    public void InvalidDocumentAndInvalidCandidateReturnStableDiagnostics()
    {
        WithWorkspace(root =>
        {
            var directory = Path.Combine(root, ".forge", "network");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "operations.json");
            File.WriteAllText(path, "{\"schemaVersion\":1}", new UTF8Encoding(false));

            var loaded = WorkspaceNetworkOperationsStore.Load(root);
            var invalid = NetworkOperationsProfile.CreateDefault() with { SchemaVersion = 999 };
            var saved = WorkspaceNetworkOperationsStore.Save(root, invalid);

            Assert.False(loaded.Succeeded);
            Assert.False(loaded.FileMissing);
            Assert.Equal(WorkspaceNetworkOperationsStore.ReadFailedCode, loaded.Diagnostics.Single().Code);
            Assert.Contains("NET_OPERATIONS_INCOMPLETE", loaded.Diagnostics.Single().Message, StringComparison.Ordinal);
            Assert.False(saved.Succeeded);
            Assert.Equal(WorkspaceNetworkOperationsStore.WriteFailedCode, saved.Diagnostics.Single().Code);
            Assert.Equal("{\"schemaVersion\":1}", File.ReadAllText(path, Encoding.UTF8));
        });
    }

    private static void WithWorkspace(Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-network-operations-store-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            action(root);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
