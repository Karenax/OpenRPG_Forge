using System.Text;
using Forge.LiveDirector.Authoring;
using Forge.ModuleFormat.LiveDirector;

namespace Forge.ModuleFormat.Tests.LiveDirector;

public sealed class WorkspaceLiveDirectorGroupStoreTests
{
    [Fact]
    public void Missing_profile_returns_canonical_default_without_writing()
    {
        var root = NewWorkspace();
        try
        {
            var loaded = WorkspaceLiveDirectorGroupStore.Load(root);
            Assert.True(loaded.Succeeded);
            Assert.True(loaded.FileMissing);
            Assert.Empty(LiveDirectorGroupValidator.Validate(loaded.Profile));
            Assert.False(File.Exists(Path.Combine(root, ".forge", "live-director", "group.json")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Save_is_atomic_utf8_without_bom_and_round_trips_canonical_json()
    {
        var root = NewWorkspace();
        try
        {
            var initial = LiveDirectorGroupProfile.CreateDefault("test:ux9-i");
            var candidate = initial with
            {
                ServerRevision = 1,
                SelectionCap = 96,
                RequiredAtRuntime = true
            };
            Assert.True(WorkspaceLiveDirectorGroupStore.Save(root, candidate).Succeeded);
            var loaded = WorkspaceLiveDirectorGroupStore.Load(root);
            Assert.True(loaded.Succeeded);
            Assert.False(loaded.FileMissing);
            var canonical = new LiveDirectorGroupStore();
            Assert.Equal(canonical.Serialize(candidate), canonical.Serialize(loaded.Profile));
            var bytes = File.ReadAllBytes(Path.Combine(root, ".forge", "live-director",
                "group.json"));
            Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
            Assert.Empty(Directory.GetFiles(Path.Combine(root, ".forge", "live-director"),
                "*.tmp", SearchOption.TopDirectoryOnly));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Invalid_disk_and_invalid_save_return_localized_store_diagnostics()
    {
        var root = NewWorkspace();
        try
        {
            var path = Path.Combine(root, ".forge", "live-director", "group.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{not-json", Encoding.UTF8);
            var loaded = WorkspaceLiveDirectorGroupStore.Load(root);
            Assert.False(loaded.Succeeded);
            Assert.Equal(WorkspaceLiveDirectorGroupStore.ReadFailedCode,
                Assert.Single(loaded.Diagnostics).Code);
            var saved = WorkspaceLiveDirectorGroupStore.Save(root,
                LiveDirectorGroupProfile.CreateDefault() with { SelectionCap = 0 });
            Assert.False(saved.Succeeded);
            Assert.Equal(WorkspaceLiveDirectorGroupStore.WriteFailedCode,
                Assert.Single(saved.Diagnostics).Code);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static string NewWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-live-group-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }
}
