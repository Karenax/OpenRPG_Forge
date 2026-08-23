using System.Text;
using Forge.ModuleFormat.Network;
using Forge.Network.Authoring;

namespace Forge.ModuleFormat.Tests.Network;

public sealed class WorkspaceNetworkFoundationStoreTests
{
    [Fact]
    public void MissingDocumentReturnsExplicitDefaultWithoutWriting()
    {
        WithWorkspace(root =>
        {
            var loaded = WorkspaceNetworkFoundationStore.Load(root);

            Assert.True(loaded.Succeeded);
            Assert.True(loaded.FileMissing);
            Assert.Empty(loaded.Diagnostics);
            Assert.Empty(NetworkFoundationValidator.Validate(loaded.Profile));
            Assert.False(File.Exists(Path.Combine(root, ".forge", "network", "foundation.json")));
        });
    }

    [Fact]
    public void SaveIsAtomicDeterministicAndRoundTripsExactly()
    {
        WithWorkspace(root =>
        {
            var initial = NetworkFoundationProfile.CreateDefault("test:ux9-a");
            var candidate = initial with
            {
                ServerRevision = 4,
                Server = initial.Server with { Capacity = 12 },
                Party = initial.Party with { MaximumMembers = 6 },
                Interest = initial.Interest with { MaximumDistance = 384 }
            };

            var saved = WorkspaceNetworkFoundationStore.Save(root, candidate);
            var loaded = WorkspaceNetworkFoundationStore.Load(root);
            var path = Path.Combine(root, ".forge", "network", "foundation.json");
            var bytes = File.ReadAllBytes(path);

            Assert.True(saved.Succeeded);
            Assert.True(loaded.Succeeded);
            Assert.False(loaded.FileMissing);
            var canonical = new NetworkFoundationStore();
            Assert.Equal(canonical.Serialize(candidate), canonical.Serialize(loaded.Profile));
            Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
            Assert.EndsWith("\n", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
            Assert.DoesNotContain("\r\n", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
            Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp"));

            Assert.True(WorkspaceNetworkFoundationStore.Save(root, loaded.Profile).Succeeded);
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
            var path = Path.Combine(directory, "foundation.json");
            File.WriteAllText(path, "{\"schemaVersion\":999}", new UTF8Encoding(false));

            var loaded = WorkspaceNetworkFoundationStore.Load(root);
            var invalid = NetworkFoundationProfile.CreateDefault() with { SchemaVersion = 999 };
            var saved = WorkspaceNetworkFoundationStore.Save(root, invalid);

            Assert.False(loaded.Succeeded);
            Assert.False(loaded.FileMissing);
            Assert.Equal(WorkspaceNetworkFoundationStore.ReadFailedCode, loaded.Diagnostics.Single().Code);
            Assert.False(saved.Succeeded);
            Assert.Equal(WorkspaceNetworkFoundationStore.WriteFailedCode, saved.Diagnostics.Single().Code);
            Assert.Equal("{\"schemaVersion\":999}", File.ReadAllText(path, Encoding.UTF8));
        });
    }

    private static void WithWorkspace(Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-network-foundation-store-{Guid.NewGuid():N}");
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
