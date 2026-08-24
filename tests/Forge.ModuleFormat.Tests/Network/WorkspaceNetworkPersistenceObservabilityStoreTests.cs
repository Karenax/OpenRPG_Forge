using System.Text;
using Forge.ModuleFormat.Network;
using Forge.Network.Authoring;

namespace Forge.ModuleFormat.Tests.Network;

public sealed class WorkspaceNetworkPersistenceObservabilityStoreTests
{
    [Fact]
    public void MissingDocumentReturnsExplicitDefaultWithoutWriting()
    {
        WithWorkspace(root =>
        {
            var loaded = WorkspaceNetworkPersistenceObservabilityStore.Load(root);

            Assert.True(loaded.Succeeded);
            Assert.True(loaded.FileMissing);
            Assert.Empty(NetworkPersistenceObservabilityValidator.Validate(loaded.Profile));
            Assert.False(File.Exists(Path.Combine(root, ".forge", "network",
                "persistence-observability.json")));
        });
    }

    [Fact]
    public void SaveIsAtomicDeterministicAndRoundTripsExactly()
    {
        WithWorkspace(root =>
        {
            var initial = NetworkPersistenceObservabilityProfile.CreateDefault("test:ux9-d");
            var candidate = initial with
            {
                ServerRevision = 3,
                Persistence = initial.Persistence with { BackupRetentionDays = 60 },
                Profiler = initial.Profiler with { MaximumCapturedSpans = 250_000 },
                Reconnection = initial.Reconnection with { GracePeriodSeconds = 180 }
            };

            var saved = WorkspaceNetworkPersistenceObservabilityStore.Save(root, candidate);
            var loaded = WorkspaceNetworkPersistenceObservabilityStore.Load(root);
            var path = Path.Combine(root, ".forge", "network", "persistence-observability.json");
            var bytes = File.ReadAllBytes(path);

            Assert.True(saved.Succeeded);
            Assert.True(loaded.Succeeded);
            Assert.False(loaded.FileMissing);
            var canonical = new NetworkPersistenceObservabilityStore();
            Assert.Equal(canonical.Serialize(candidate), canonical.Serialize(loaded.Profile));
            Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
            Assert.EndsWith("\n", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
            Assert.DoesNotContain("\r\n", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
            Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp"));

            Assert.True(WorkspaceNetworkPersistenceObservabilityStore.Save(root, loaded.Profile).Succeeded);
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
            var path = Path.Combine(directory, "persistence-observability.json");
            File.WriteAllText(path, "{\"schemaVersion\":1}", new UTF8Encoding(false));

            var loaded = WorkspaceNetworkPersistenceObservabilityStore.Load(root);
            var invalid = NetworkPersistenceObservabilityProfile.CreateDefault() with { SchemaVersion = 999 };
            var saved = WorkspaceNetworkPersistenceObservabilityStore.Save(root, invalid);

            Assert.False(loaded.Succeeded);
            Assert.False(loaded.FileMissing);
            Assert.Equal(WorkspaceNetworkPersistenceObservabilityStore.ReadFailedCode,
                loaded.Diagnostics.Single().Code);
            Assert.Contains("NET_PERSIST_OBSERVE_INCOMPLETE", loaded.Diagnostics.Single().Message,
                StringComparison.Ordinal);
            Assert.False(saved.Succeeded);
            Assert.Equal(WorkspaceNetworkPersistenceObservabilityStore.WriteFailedCode,
                saved.Diagnostics.Single().Code);
            Assert.Equal("{\"schemaVersion\":1}", File.ReadAllText(path, Encoding.UTF8));
        });
    }

    private static void WithWorkspace(Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-network-persistence-observability-store-{Guid.NewGuid():N}");
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
