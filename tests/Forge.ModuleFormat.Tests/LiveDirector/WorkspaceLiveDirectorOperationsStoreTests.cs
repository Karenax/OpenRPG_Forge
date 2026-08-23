using System.Text;
using Forge.LiveDirector.Authoring;
using Forge.ModuleFormat.LiveDirector;

namespace Forge.ModuleFormat.Tests.LiveDirector;

public sealed class WorkspaceLiveDirectorOperationsStoreTests
{
    [Fact]
    public void Missing_profile_returns_canonical_default_without_writing()
    {
        var root = NewWorkspace();
        try
        {
            var loaded = WorkspaceLiveDirectorOperationsStore.Load(root);
            Assert.True(loaded.Succeeded);
            Assert.True(loaded.FileMissing);
            Assert.Empty(LiveDirectorOperationsValidator.Validate(loaded.Profile));
            Assert.False(File.Exists(Path.Combine(root, ".forge", "live-director",
                "operations.json")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Save_is_atomic_utf8_without_bom_and_round_trips_canonical_json()
    {
        var root = NewWorkspace();
        try
        {
            var candidate = LiveDirectorOperationsProfile.CreateDefault("test:ux9-j") with
            {
                ServerRevision = 1,
                RequiredAtRuntime = true
            };
            Assert.True(WorkspaceLiveDirectorOperationsStore.Save(root, candidate).Succeeded);
            var loaded = WorkspaceLiveDirectorOperationsStore.Load(root);
            Assert.True(loaded.Succeeded);
            Assert.False(loaded.FileMissing);
            var canonical = new LiveDirectorOperationsStore();
            Assert.Equal(canonical.Serialize(candidate), canonical.Serialize(loaded.Profile));
            var directory = Path.Combine(root, ".forge", "live-director");
            var bytes = File.ReadAllBytes(Path.Combine(directory, "operations.json"));
            Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp", SearchOption.TopDirectoryOnly));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Invalid_disk_and_invalid_save_return_localized_store_diagnostics()
    {
        var root = NewWorkspace();
        try
        {
            var path = Path.Combine(root, ".forge", "live-director", "operations.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{not-json", Encoding.UTF8);
            var loaded = WorkspaceLiveDirectorOperationsStore.Load(root);
            Assert.False(loaded.Succeeded);
            Assert.Equal(WorkspaceLiveDirectorOperationsStore.ReadFailedCode,
                Assert.Single(loaded.Diagnostics).Code);
            var saved = WorkspaceLiveDirectorOperationsStore.Save(root,
                LiveDirectorOperationsProfile.CreateDefault() with { VisualFocus = false });
            Assert.False(saved.Succeeded);
            Assert.Equal(WorkspaceLiveDirectorOperationsStore.WriteFailedCode,
                Assert.Single(saved.Diagnostics).Code);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static string NewWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-live-operations-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }
}
