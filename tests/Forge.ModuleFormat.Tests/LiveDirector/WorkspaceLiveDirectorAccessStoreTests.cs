using System.Text;
using Forge.LiveDirector.Authoring;
using Forge.ModuleFormat.LiveDirector;

namespace Forge.ModuleFormat.Tests.LiveDirector;

public sealed class WorkspaceLiveDirectorAccessStoreTests
{
    [Fact]
    public void Missing_profile_returns_default_without_implicit_write()
    {
        var root = NewWorkspace();
        try
        {
            var loaded = WorkspaceLiveDirectorAccessStore.Load(root);
            Assert.True(loaded.Succeeded);
            Assert.True(loaded.FileMissing);
            Assert.Empty(LiveDirectorAccessValidator.Validate(loaded.Profile));
            Assert.False(File.Exists(Path.Combine(root, ".forge", "live-director",
                "access.json")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Save_is_atomic_utf8_without_bom_and_round_trips()
    {
        var root = NewWorkspace();
        try
        {
            var initial = LiveDirectorAccessProfile.CreateDefault("test:ux9-l");
            var candidate = initial with
            {
                ServerRevision = 1,
                RequiredAtRuntime = true,
                Players = initial.Players with { RefreshSeconds = 3 }
            };
            Assert.True(WorkspaceLiveDirectorAccessStore.Save(root, candidate).Succeeded);
            var loaded = WorkspaceLiveDirectorAccessStore.Load(root);
            Assert.True(loaded.Succeeded);
            Assert.False(loaded.FileMissing);
            var canonical = new LiveDirectorAccessStore();
            Assert.Equal(canonical.Serialize(candidate), canonical.Serialize(loaded.Profile));
            var directory = Path.Combine(root, ".forge", "live-director");
            var bytes = File.ReadAllBytes(Path.Combine(directory, "access.json"));
            Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp", SearchOption.TopDirectoryOnly));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Invalid_disk_and_invalid_save_are_reported()
    {
        var root = NewWorkspace();
        try
        {
            var path = Path.Combine(root, ".forge", "live-director", "access.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{not-json", Encoding.UTF8);
            var loaded = WorkspaceLiveDirectorAccessStore.Load(root);
            Assert.False(loaded.Succeeded);
            Assert.Equal(WorkspaceLiveDirectorAccessStore.ReadFailedCode,
                Assert.Single(loaded.Diagnostics).Code);
            var initial = LiveDirectorAccessProfile.CreateDefault();
            var saved = WorkspaceLiveDirectorAccessStore.Save(root, initial with
            {
                Authentication = initial.Authentication with { SessionLifetimeMinutes = 0 }
            });
            Assert.False(saved.Succeeded);
            Assert.Equal(WorkspaceLiveDirectorAccessStore.WriteFailedCode,
                Assert.Single(saved.Diagnostics).Code);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static string NewWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-live-access-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }
}
