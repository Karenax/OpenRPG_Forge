using System.Text;
using Forge.LiveDirector.Authoring;
using Forge.ModuleFormat.LiveDirector;

namespace Forge.ModuleFormat.Tests.LiveDirector;

public sealed class WorkspaceLiveDirectorNarrativeStoreTests
{
    [Fact]
    public void Missing_profile_returns_canonical_default_without_writing()
    {
        var root = NewWorkspace();
        try
        {
            var loaded = WorkspaceLiveDirectorNarrativeStore.Load(root);
            Assert.True(loaded.Succeeded);
            Assert.True(loaded.FileMissing);
            Assert.Empty(LiveDirectorNarrativeValidator.Validate(loaded.Profile));
            Assert.False(File.Exists(Path.Combine(root, ".forge", "live-director",
                "narrative.json")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Save_is_atomic_utf8_without_bom_and_round_trips_canonical_json()
    {
        var root = NewWorkspace();
        try
        {
            var candidate = LiveDirectorNarrativeProfile.CreateDefault("test:ux9-k") with
            {
                ServerRevision = 1,
                RequiredAtRuntime = true
            };
            Assert.True(WorkspaceLiveDirectorNarrativeStore.Save(root, candidate).Succeeded);
            var loaded = WorkspaceLiveDirectorNarrativeStore.Load(root);
            Assert.True(loaded.Succeeded);
            Assert.False(loaded.FileMissing);
            var canonical = new LiveDirectorNarrativeStore();
            Assert.Equal(canonical.Serialize(candidate), canonical.Serialize(loaded.Profile));
            var directory = Path.Combine(root, ".forge", "live-director");
            var bytes = File.ReadAllBytes(Path.Combine(directory, "narrative.json"));
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
            var path = Path.Combine(root, ".forge", "live-director", "narrative.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{not-json", Encoding.UTF8);
            var loaded = WorkspaceLiveDirectorNarrativeStore.Load(root);
            Assert.False(loaded.Succeeded);
            Assert.Equal(WorkspaceLiveDirectorNarrativeStore.ReadFailedCode,
                Assert.Single(loaded.Diagnostics).Code);
            var saved = WorkspaceLiveDirectorNarrativeStore.Save(root,
                LiveDirectorNarrativeProfile.CreateDefault() with
                { EncounterActions = ["despawn"] });
            Assert.False(saved.Succeeded);
            Assert.Equal(WorkspaceLiveDirectorNarrativeStore.WriteFailedCode,
                Assert.Single(saved.Diagnostics).Code);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static string NewWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-live-narrative-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }
}
