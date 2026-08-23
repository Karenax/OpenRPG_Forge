using System.Text;
using Forge.ModuleFormat.Network;
using Forge.Network.Authoring;

namespace Forge.ModuleFormat.Tests.Network;

public sealed class WorkspaceNetworkWorldAdministrationStoreTests
{
    [Fact]
    public void MissingDocumentReturnsExplicitDefaultWithoutWriting()
    {
        WithWorkspace(root =>
        {
            var loaded = WorkspaceNetworkWorldAdministrationStore.Load(root);

            Assert.True(loaded.Succeeded);
            Assert.True(loaded.FileMissing);
            Assert.Empty(NetworkWorldAdministrationValidator.Validate(loaded.Profile));
            Assert.False(File.Exists(Path.Combine(root, ".forge", "network", "world-administration.json")));
        });
    }

    [Fact]
    public void SaveIsAtomicDeterministicAndRoundTripsExactly()
    {
        WithWorkspace(root =>
        {
            var initial = NetworkWorldAdministrationProfile.CreateDefault("test:ux9-c");
            var candidate = initial with
            {
                ServerRevision = 3,
                Instances = initial.Instances with { MaximumInstances = 24 },
                Authentication = initial.Authentication with { SessionLifetimeMinutes = 180 },
                Moderation = initial.Moderation with { PrivacyRetentionDays = 120 }
            };

            var saved = WorkspaceNetworkWorldAdministrationStore.Save(root, candidate);
            var loaded = WorkspaceNetworkWorldAdministrationStore.Load(root);
            var path = Path.Combine(root, ".forge", "network", "world-administration.json");
            var bytes = File.ReadAllBytes(path);

            Assert.True(saved.Succeeded);
            Assert.True(loaded.Succeeded);
            Assert.False(loaded.FileMissing);
            var canonical = new NetworkWorldAdministrationStore();
            Assert.Equal(canonical.Serialize(candidate), canonical.Serialize(loaded.Profile));
            Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
            Assert.EndsWith("\n", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
            Assert.DoesNotContain("\r\n", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
            Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp"));

            Assert.True(WorkspaceNetworkWorldAdministrationStore.Save(root, loaded.Profile).Succeeded);
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
            var path = Path.Combine(directory, "world-administration.json");
            File.WriteAllText(path, "{\"schemaVersion\":1}", new UTF8Encoding(false));

            var loaded = WorkspaceNetworkWorldAdministrationStore.Load(root);
            var invalid = NetworkWorldAdministrationProfile.CreateDefault() with { SchemaVersion = 999 };
            var saved = WorkspaceNetworkWorldAdministrationStore.Save(root, invalid);

            Assert.False(loaded.Succeeded);
            Assert.False(loaded.FileMissing);
            Assert.Equal(WorkspaceNetworkWorldAdministrationStore.ReadFailedCode,
                loaded.Diagnostics.Single().Code);
            Assert.Contains("NET_WORLD_ADMIN_INCOMPLETE", loaded.Diagnostics.Single().Message,
                StringComparison.Ordinal);
            Assert.False(saved.Succeeded);
            Assert.Equal(WorkspaceNetworkWorldAdministrationStore.WriteFailedCode,
                saved.Diagnostics.Single().Code);
            Assert.Equal("{\"schemaVersion\":1}", File.ReadAllText(path, Encoding.UTF8));
        });
    }

    private static void WithWorkspace(Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-network-world-administration-store-{Guid.NewGuid():N}");
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
