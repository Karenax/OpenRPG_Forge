using System.Text;
using Forge.LiveDirector.Authoring;
using Forge.ModuleFormat.LiveDirector;

namespace Forge.ModuleFormat.Tests.LiveDirector;

public sealed class WorkspaceLiveDirectorAuthorityStoreTests
{
    [Fact]
    public void MissingDocumentReturnsExplicitDefaultWithoutWriting()
    {
        WithWorkspace(root =>
        {
            var loaded = WorkspaceLiveDirectorAuthorityStore.Load(root);
            Assert.True(loaded.Succeeded);
            Assert.True(loaded.FileMissing);
            Assert.Empty(LiveDirectorAuthorityValidator.Validate(loaded.Profile));
            Assert.False(File.Exists(Path.Combine(root, ".forge", "live-director",
                "authority.json")));
        });
    }

    [Fact]
    public void SaveIsAtomicDeterministicAndRoundTripsExactly()
    {
        WithWorkspace(root =>
        {
            var initial = LiveDirectorAuthorityProfile.CreateDefault("test:ux9-h");
            var candidate = initial with
            {
                ServerRevision = 3,
                MaximumCurrencyAdjustment = 50_000,
                MaximumExperienceAdjustment = 250_000,
                RequiredAtRuntime = true
            };
            Assert.True(WorkspaceLiveDirectorAuthorityStore.Save(root, candidate).Succeeded);
            var loaded = WorkspaceLiveDirectorAuthorityStore.Load(root);
            var path = Path.Combine(root, ".forge", "live-director", "authority.json");
            var bytes = File.ReadAllBytes(path);
            var canonical = new LiveDirectorAuthorityStore();
            Assert.True(loaded.Succeeded);
            Assert.False(loaded.FileMissing);
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
            var path = Path.Combine(directory, "authority.json");
            File.WriteAllText(path, "{\"schemaVersion\":1}", new UTF8Encoding(false));
            var loaded = WorkspaceLiveDirectorAuthorityStore.Load(root);
            var saved = WorkspaceLiveDirectorAuthorityStore.Save(root,
                LiveDirectorAuthorityProfile.CreateDefault() with { SchemaVersion = 999 });
            Assert.False(loaded.Succeeded);
            Assert.Equal(WorkspaceLiveDirectorAuthorityStore.ReadFailedCode,
                loaded.Diagnostics.Single().Code);
            Assert.Contains("LIVE_AUTHORITY_UUID_INVALID", loaded.Diagnostics.Single().Message,
                StringComparison.Ordinal);
            Assert.False(saved.Succeeded);
            Assert.Equal(WorkspaceLiveDirectorAuthorityStore.WriteFailedCode,
                saved.Diagnostics.Single().Code);
            Assert.Equal("{\"schemaVersion\":1}", File.ReadAllText(path, Encoding.UTF8));
        });
    }

    private static void WithWorkspace(Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-live-authority-store-{Guid.NewGuid():N}");
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
