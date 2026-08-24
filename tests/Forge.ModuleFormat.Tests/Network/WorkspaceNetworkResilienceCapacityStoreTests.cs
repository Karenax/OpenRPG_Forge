using System.Text;
using Forge.ModuleFormat.Network;
using Forge.Network.Authoring;

namespace Forge.ModuleFormat.Tests.Network;

public sealed class WorkspaceNetworkResilienceCapacityStoreTests
{
    [Fact]
    public void MissingDocumentReturnsExplicitDefaultWithoutWriting()
    {
        WithWorkspace(root =>
        {
            var loaded = WorkspaceNetworkResilienceCapacityStore.Load(root);

            Assert.True(loaded.Succeeded);
            Assert.True(loaded.FileMissing);
            Assert.Empty(NetworkResilienceCapacityValidator.Validate(loaded.Profile));
            Assert.False(File.Exists(Path.Combine(root, ".forge", "network",
                "resilience-capacity.json")));
        });
    }

    [Fact]
    public void SaveIsAtomicDeterministicAndRoundTripsExactly()
    {
        WithWorkspace(root =>
        {
            var initial = NetworkResilienceCapacityProfile.CreateDefault("test:ux9-e");
            var candidate = initial with
            {
                ServerRevision = 3,
                Backup = initial.Backup with { SnapshotIntervalMinutes = 10, RetentionDays = 45 },
                Capacity = initial.Capacity with
                {
                    MaximumPlayers = 72,
                    ReservedAdministratorSlots = 6,
                    ReservationSeconds = 45
                }
            };

            var saved = WorkspaceNetworkResilienceCapacityStore.Save(root, candidate);
            var loaded = WorkspaceNetworkResilienceCapacityStore.Load(root);
            var path = Path.Combine(root, ".forge", "network", "resilience-capacity.json");
            var bytes = File.ReadAllBytes(path);

            Assert.True(saved.Succeeded);
            Assert.True(loaded.Succeeded);
            Assert.False(loaded.FileMissing);
            var canonical = new NetworkResilienceCapacityStore();
            Assert.Equal(canonical.Serialize(candidate), canonical.Serialize(loaded.Profile));
            Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
            Assert.EndsWith("\n", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
            Assert.DoesNotContain("\r\n", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
            Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp"));

            Assert.True(WorkspaceNetworkResilienceCapacityStore.Save(root, loaded.Profile).Succeeded);
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
            var path = Path.Combine(directory, "resilience-capacity.json");
            File.WriteAllText(path, "{\"schemaVersion\":1}", new UTF8Encoding(false));

            var loaded = WorkspaceNetworkResilienceCapacityStore.Load(root);
            var invalid = NetworkResilienceCapacityProfile.CreateDefault() with { SchemaVersion = 999 };
            var saved = WorkspaceNetworkResilienceCapacityStore.Save(root, invalid);

            Assert.False(loaded.Succeeded);
            Assert.False(loaded.FileMissing);
            Assert.Equal(WorkspaceNetworkResilienceCapacityStore.ReadFailedCode,
                loaded.Diagnostics.Single().Code);
            Assert.Contains("NET_RESILIENCE_INCOMPLETE", loaded.Diagnostics.Single().Message,
                StringComparison.Ordinal);
            Assert.False(saved.Succeeded);
            Assert.Equal(WorkspaceNetworkResilienceCapacityStore.WriteFailedCode,
                saved.Diagnostics.Single().Code);
            Assert.Equal("{\"schemaVersion\":1}", File.ReadAllText(path, Encoding.UTF8));
        });
    }

    private static void WithWorkspace(Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-network-resilience-capacity-store-{Guid.NewGuid():N}");
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
