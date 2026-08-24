using System.Text;
using Forge.LiveDirector.Authoring;
using Forge.ModuleFormat.LiveDirector;

namespace Forge.ModuleFormat.Tests.LiveDirector;

public sealed class WorkspaceLiveDirectorStoreTests
{
    [Fact]
    public void MissingDocumentReturnsExplicitDefaultWithoutWriting()
    {
        WithWorkspace(root =>
        {
            var loaded = WorkspaceLiveDirectorStore.Load(root);

            Assert.True(loaded.Succeeded);
            Assert.True(loaded.FileMissing);
            Assert.Empty(LiveDirectorWorkspaceValidator.Validate(loaded.Profile));
            Assert.False(File.Exists(Path.Combine(root, ".forge", "live-director",
                "workspace.json")));
        });
    }

    [Fact]
    public void SaveIsAtomicDeterministicAndRoundTripsExactly()
    {
        WithWorkspace(root =>
        {
            var initial = LiveDirectorWorkspaceProfile.CreateDefault("test:ux9-f");
            var candidate = initial with
            {
                ServerRevision = 4,
                Client = initial.Client with { SessionMinutes = 45 },
                Spawn = initial.Spawn with { MaximumInstances = 96 }
            };

            var saved = WorkspaceLiveDirectorStore.Save(root, candidate);
            var loaded = WorkspaceLiveDirectorStore.Load(root);
            var path = Path.Combine(root, ".forge", "live-director", "workspace.json");
            var bytes = File.ReadAllBytes(path);

            Assert.True(saved.Succeeded);
            Assert.True(loaded.Succeeded);
            Assert.False(loaded.FileMissing);
            var canonical = new LiveDirectorWorkspaceStore();
            Assert.Equal(canonical.Serialize(candidate), canonical.Serialize(loaded.Profile));
            Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
            Assert.EndsWith("\n", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
            Assert.DoesNotContain("\r\n", Encoding.UTF8.GetString(bytes),
                StringComparison.Ordinal);
            Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp"));

            Assert.True(WorkspaceLiveDirectorStore.Save(root, loaded.Profile).Succeeded);
            Assert.Equal(bytes, File.ReadAllBytes(path));
        });
    }

    [Fact]
    public void InvalidDocumentAndCandidateReturnStableDiagnostics()
    {
        WithWorkspace(root =>
        {
            var directory = Path.Combine(root, ".forge", "live-director");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "workspace.json");
            File.WriteAllText(path, "{\"schemaVersion\":1}", new UTF8Encoding(false));

            var loaded = WorkspaceLiveDirectorStore.Load(root);
            var invalid = LiveDirectorWorkspaceProfile.CreateDefault() with
            {
                SchemaVersion = 999
            };
            var saved = WorkspaceLiveDirectorStore.Save(root, invalid);

            Assert.False(loaded.Succeeded);
            Assert.False(loaded.FileMissing);
            Assert.Equal(WorkspaceLiveDirectorStore.ReadFailedCode,
                loaded.Diagnostics.Single().Code);
            Assert.Contains("LIVE_WORKSPACE_INCOMPLETE", loaded.Diagnostics.Single().Message,
                StringComparison.Ordinal);
            Assert.False(saved.Succeeded);
            Assert.Equal(WorkspaceLiveDirectorStore.WriteFailedCode,
                saved.Diagnostics.Single().Code);
            Assert.Equal("{\"schemaVersion\":1}", File.ReadAllText(path, Encoding.UTF8));
        });
    }

    private static void WithWorkspace(Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-live-director-store-{Guid.NewGuid():N}");
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
