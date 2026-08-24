using System.Text.Json.Nodes;
using Forge.Domain.Authoring;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Workspaces;

public sealed class EditorKeymapProfileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "openrpg-editor-keymap", Guid.NewGuid().ToString("N"));

    public EditorKeymapProfileStoreTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void MissingKeymapReturnsStandardDefaultWithoutWriting()
    {
        var loaded = EditorKeymapProfileStore.Load(_root);

        Assert.True(loaded.Succeeded);
        Assert.True(loaded.FileMissing);
        Assert.Equal(EditorKeymapPreset.Standard, loaded.Profile.Preset);
        Assert.False(File.Exists(Path.Combine(_root, ".forge", "editor-keymap.json")));
    }

    [Fact]
    public void SaveReloadAndSaveAgainAreExactAndDeterministic()
    {
        var profile = EditorKeymapProfile.CreatePreset(EditorKeymapPreset.Aurora, "author:g13-a");
        var firstSave = EditorKeymapProfileStore.Save(_root, profile);
        var path = Path.Combine(_root, ".forge", "editor-keymap.json");
        var first = File.ReadAllText(path);
        var loaded = EditorKeymapProfileStore.Load(_root);
        var secondSave = EditorKeymapProfileStore.Save(_root, loaded.Profile);
        var second = File.ReadAllText(path);

        Assert.True(firstSave.Succeeded);
        Assert.True(loaded.Succeeded);
        Assert.False(loaded.FileMissing);
        Assert.True(secondSave.Succeeded);
        Assert.Equal(profile.Id, loaded.Profile.Id);
        Assert.Equal(profile.Preset, loaded.Profile.Preset);
        Assert.Equal(profile.Provenance, loaded.Profile.Provenance);
        Assert.Equal(profile.Bindings, loaded.Profile.Bindings);
        Assert.Equal(first, second);
    }

    [Fact]
    public void InvalidKeymapReturnsStableDiagnosticAndSafeFallback()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".forge"));
        File.WriteAllText(Path.Combine(_root, ".forge", "editor-keymap.json"),
            "{\"formatVersion\":\"1.0.0\",\"profile\":{\"schemaVersion\":1}}");

        var loaded = EditorKeymapProfileStore.Load(_root);

        Assert.False(loaded.Succeeded);
        Assert.Equal(EditorKeymapProfileStore.ReadFailedCode, Assert.Single(loaded.Diagnostics).Code);
        Assert.Equal(EditorKeymapPreset.Standard, loaded.Profile.Preset);
    }

    [Fact]
    public void ThirteenCommandProfileMigratesCutBindingWithoutLosingExistingShortcuts()
    {
        var original = EditorKeymapProfile.CreatePreset(EditorKeymapPreset.Aurora, "author:g13-a-legacy");
        Assert.True(EditorKeymapProfileStore.Save(_root, original).Succeeded);
        var path = Path.Combine(_root, ".forge", "editor-keymap.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var bindings = document["profile"]!["bindings"]!.AsArray();
        var cut = bindings.Single(node =>
            node!["commandId"]!.GetValue<string>() == EditorKeymapCatalog.CutSelection);
        bindings.Remove(cut);
        File.WriteAllText(path, document.ToJsonString());

        var loaded = EditorKeymapProfileStore.Load(_root);

        Assert.True(loaded.Succeeded);
        Assert.False(loaded.FileMissing);
        Assert.Equal(EditorKeymapCatalog.Commands.Count, loaded.Profile.Bindings.Count);
        Assert.Equal("Ctrl+X", loaded.Profile.Bindings.Single(binding =>
            binding.CommandId == EditorKeymapCatalog.CutSelection).Gesture.CanonicalText);
        Assert.All(original.Bindings.Where(binding => binding.CommandId != EditorKeymapCatalog.CutSelection),
            binding => Assert.Contains(binding, loaded.Profile.Bindings));
        Assert.Equal("author:g13-a-legacy", loaded.Profile.Provenance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
