using System.Text;
using Forge.ModuleFormat.Network;
using Forge.Network.Authoring;
using Forge.Network.Observability;

namespace Forge.ModuleFormat.Tests.Network;

public sealed class WorkspaceNetworkAssuranceStoreTests
{
    [Fact]
    public void Missing_document_returns_explicit_default_without_writing()
    {
        WithWorkspace(root =>
        {
            var loaded = WorkspaceNetworkAssuranceStore.Load(root);
            Assert.True(loaded.Succeeded);
            Assert.True(loaded.FileMissing);
            Assert.Empty(NetworkAssuranceValidator.Validate(loaded.Profile));
            Assert.False(File.Exists(Path.Combine(root, ".forge", "network", "assurance.json")));
        });
    }

    [Fact]
    public void Save_is_atomic_deterministic_and_round_trips_logs_and_anti_cheat()
    {
        WithWorkspace(root =>
        {
            var initial = NetworkAssuranceProfile.CreateDefault("test:ux9-o");
            var candidate = initial with
            {
                ServerRevision = 3,
                Logs = initial.Logs with
                {
                    MinimumSeverity = Forge.Domain.Quality.StructuredLogSeverity.Warning,
                    OutputFormat = StructuredLogOutputFormat.Json,
                    RotationFiles = 7
                },
                AntiCheat = initial.AntiCheat with
                {
                    MaximumUnitsPerSecond = 18,
                    MaximumIntentsPerSecond = 12,
                    AnomalyThreshold = 24
                }
            };
            var saved = WorkspaceNetworkAssuranceStore.Save(root, candidate);
            var loaded = WorkspaceNetworkAssuranceStore.Load(root);
            var path = Path.Combine(root, ".forge", "network", "assurance.json");
            var bytes = File.ReadAllBytes(path);
            var canonical = new NetworkAssuranceStore();

            Assert.True(saved.Succeeded);
            Assert.True(loaded.Succeeded);
            Assert.False(loaded.FileMissing);
            Assert.Equal(canonical.Serialize(candidate), canonical.Serialize(loaded.Profile));
            Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
            Assert.EndsWith("\n", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
            Assert.DoesNotContain("\r\n", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
            Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp"));
            Assert.True(WorkspaceNetworkAssuranceStore.Save(root, loaded.Profile).Succeeded);
            Assert.Equal(bytes, File.ReadAllBytes(path));
        });
    }

    [Fact]
    public void Invalid_document_and_candidate_return_stable_diagnostics()
    {
        WithWorkspace(root =>
        {
            var directory = Path.Combine(root, ".forge", "network");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "assurance.json");
            File.WriteAllText(path, "{\"schemaVersion\":999}", new UTF8Encoding(false));
            var loaded = WorkspaceNetworkAssuranceStore.Load(root);
            var invalid = NetworkAssuranceProfile.CreateDefault() with { SchemaVersion = 999 };
            var saved = WorkspaceNetworkAssuranceStore.Save(root, invalid);
            Assert.False(loaded.Succeeded);
            Assert.Equal(WorkspaceNetworkAssuranceStore.ReadFailedCode,
                Assert.Single(loaded.Diagnostics).Code);
            Assert.False(saved.Succeeded);
            Assert.Equal(WorkspaceNetworkAssuranceStore.WriteFailedCode,
                Assert.Single(saved.Diagnostics).Code);
            Assert.Equal("{\"schemaVersion\":999}", File.ReadAllText(path, Encoding.UTF8));
        });
    }

    private static void WithWorkspace(Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-network-assurance-store-{Guid.NewGuid():N}");
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
