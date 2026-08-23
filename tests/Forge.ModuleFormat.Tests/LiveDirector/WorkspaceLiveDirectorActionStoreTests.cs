using System.Text;
using Forge.LiveDirector.Authoring;
using Forge.ModuleFormat.LiveDirector;

namespace Forge.ModuleFormat.Tests.LiveDirector;

public sealed class WorkspaceLiveDirectorActionStoreTests
{
    [Fact]
    public void MissingDocumentReturnsExplicitDefaultWithoutWriting()
    {
        WithWorkspace(root =>
        {
            var loaded = WorkspaceLiveDirectorActionStore.Load(root);

            Assert.True(loaded.Succeeded);
            Assert.True(loaded.FileMissing);
            Assert.Empty(LiveDirectorActionValidator.Validate(loaded.Profile));
            Assert.False(File.Exists(Path.Combine(root, ".forge", "live-director",
                "actions.json")));
        });
    }

    [Fact]
    public void SaveIsAtomicDeterministicAndRoundTripsExactly()
    {
        WithWorkspace(root =>
        {
            var initial = LiveDirectorActionProfile.CreateDefault("test:ux9-g");
            var candidate = initial with
            {
                ServerRevision = 4,
                Possession = initial.Possession with { MaximumMinutes = 45 },
                RequiredAtRuntime = true
            };

            Assert.True(WorkspaceLiveDirectorActionStore.Save(root, candidate).Succeeded);
            var loaded = WorkspaceLiveDirectorActionStore.Load(root);
            var path = Path.Combine(root, ".forge", "live-director", "actions.json");
            var bytes = File.ReadAllBytes(path);

            Assert.True(loaded.Succeeded);
            Assert.False(loaded.FileMissing);
            var canonical = new LiveDirectorActionStore();
            Assert.Equal(canonical.Serialize(candidate), canonical.Serialize(loaded.Profile));
            Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
            Assert.EndsWith("\n", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
            Assert.DoesNotContain("\r\n", Encoding.UTF8.GetString(bytes),
                StringComparison.Ordinal);
            Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp"));
        });
    }

    [Fact]
    public void InvalidDocumentAndCandidateReturnStableDiagnostics()
    {
        WithWorkspace(root =>
        {
            var directory = Path.Combine(root, ".forge", "live-director");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "actions.json");
            File.WriteAllText(path, "{\"schemaVersion\":1}", new UTF8Encoding(false));

            var loaded = WorkspaceLiveDirectorActionStore.Load(root);
            var invalid = LiveDirectorActionProfile.CreateDefault() with { SchemaVersion = 999 };
            var saved = WorkspaceLiveDirectorActionStore.Save(root, invalid);

            Assert.False(loaded.Succeeded);
            Assert.Equal(WorkspaceLiveDirectorActionStore.ReadFailedCode,
                loaded.Diagnostics.Single().Code);
            Assert.Contains("LIVE_ACTION_PROFILE_INCOMPLETE", loaded.Diagnostics.Single().Message,
                StringComparison.Ordinal);
            Assert.False(saved.Succeeded);
            Assert.Equal(WorkspaceLiveDirectorActionStore.WriteFailedCode,
                saved.Diagnostics.Single().Code);
            Assert.Equal("{\"schemaVersion\":1}", File.ReadAllText(path, Encoding.UTF8));
        });
    }

    private static void WithWorkspace(Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-live-action-store-{Guid.NewGuid():N}");
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
