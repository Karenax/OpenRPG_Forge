using Forge.Domain.Authoring;

namespace Forge.Domain.Tests.Authoring;

public sealed class EditorKeymapProfileTests
{
    [Theory]
    [InlineData(EditorKeymapPreset.Standard)]
    [InlineData(EditorKeymapPreset.Aurora)]
    [InlineData(EditorKeymapPreset.Blender)]
    public void BuiltInProfilesAreCompleteConflictFreeAndStable(EditorKeymapPreset preset)
    {
        var profile = EditorKeymapProfile.CreatePreset(preset);

        Assert.Empty(EditorKeymapProfileValidator.Validate(profile));
        Assert.Equal(EditorKeymapProfile.DefaultId, profile.Id);
        Assert.Equal(EditorKeymapCatalog.Commands.Count, profile.Bindings.Count);
        Assert.Equal(profile.Bindings.Count,
            profile.Bindings.Select(binding => binding.Gesture.CanonicalText).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void DuplicateGestureProducesStableLocalizedConflict()
    {
        var profile = EditorKeymapProfile.CreatePreset(EditorKeymapPreset.Standard);
        var duplicate = profile.Bindings
            .Select(binding => binding.CommandId == EditorKeymapCatalog.OpenSearch
                ? new EditorCommandBinding(binding.CommandId, binding.DisplayName, binding.Category,
                    profile.Bindings.Single(item => item.CommandId == EditorKeymapCatalog.SaveProject).Gesture)
                : binding)
            .ToArray();
        var changed = new EditorKeymapProfile(profile.Id, profile.SchemaVersion, EditorKeymapPreset.Custom,
            duplicate, "test:conflict");

        var diagnostic = Assert.Single(EditorKeymapProfileValidator.Validate(changed),
            item => item.Code == EditorKeymapProfileValidator.ConflictCode);

        Assert.Equal(".forge/editor-keymap/bindings", diagnostic.Path);
        Assert.Contains("Ctrl+S", diagnostic.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Ctrl+Shift+P", "P", true, false, true, false)]
    [InlineData("F6", "F6", false, false, false, false)]
    [InlineData("Delete", "DELETE", false, false, false, false)]
    [InlineData("Alt+Home", "HOME", false, true, false, false)]
    public void GestureParserNormalizesAuthorText(
        string source,
        string key,
        bool ctrl,
        bool alt,
        bool shift,
        bool meta)
    {
        Assert.True(EditorKeyGesture.TryParse(source, out var gesture));
        Assert.NotNull(gesture);
        Assert.True(gesture.Matches(key, ctrl, alt, shift, meta));
    }
}
