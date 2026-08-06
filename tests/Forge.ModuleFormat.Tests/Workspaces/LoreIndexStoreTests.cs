using Forge.Domain.Assistant;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Workspaces;

public sealed class LoreIndexStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "openrpg-forge-lore", Guid.NewGuid().ToString("N"));

    [Fact]
    public void MissingFileUsesSafeEmptyIndex()
    {
        var result = LoreIndexStore.Load(_root);

        Assert.True(result.Succeeded);
        Assert.True(result.FileMissing);
        Assert.Empty(result.Index.Entries);
    }

    [Fact]
    public void SaveAndLoadRoundTripCanonicalIndex()
    {
        var entry = new LoreEntryDefinition(
            Guid.Parse("b1230000-0000-4000-8000-000000000050"),
            1,
            LoreEntryKind.Terminology,
            "braise",
            "Braise",
            "Énergie mémorielle qui relie les sanctuaires.",
            ["manual:world-bible"],
            [],
            ["canon", "magic"],
            "test:g12-b");
        var index = new LoreIndexDefinition(
            Guid.Parse("b1230000-0000-4000-8000-000000000001"),
            1,
            true,
            9_000,
            [entry],
            "test:g12-b");

        var saved = LoreIndexStore.Save(_root, index);
        var loaded = LoreIndexStore.Load(_root);

        Assert.True(saved.Succeeded);
        Assert.True(loaded.Succeeded);
        Assert.False(loaded.FileMissing);
        Assert.Equal(index.Id, loaded.Index.Id);
        Assert.Equal(index.MaximumContextCharacters, loaded.Index.MaximumContextCharacters);
        Assert.Equal(index.Provenance, loaded.Index.Provenance);
        var loadedEntry = Assert.Single(loaded.Index.Entries);
        Assert.Equal(entry.Id, loadedEntry.Id);
        Assert.Equal(entry.Kind, loadedEntry.Kind);
        Assert.Equal(entry.Key, loadedEntry.Key);
        Assert.Equal(entry.Content, loadedEntry.Content);
        Assert.Equal(entry.SourceIds, loadedEntry.SourceIds);
        Assert.Equal(entry.Tags, loadedEntry.Tags);
        Assert.DoesNotContain('\uFEFF', File.ReadAllText(Path.Combine(_root, LoreIndexStore.RelativePath)));
    }

    [Fact]
    public void InvalidFileReturnsStableDiagnosticAndNoPartialState()
    {
        var path = Path.Combine(_root, LoreIndexStore.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{\"formatVersion\":\"99.0.0\",\"index\":null}");

        var loaded = LoreIndexStore.Load(_root);

        Assert.False(loaded.Succeeded);
        Assert.Equal(LoreIndexStore.ReadFailedCode, loaded.Diagnostics[0].Code);
        Assert.Empty(loaded.Index.Entries);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
