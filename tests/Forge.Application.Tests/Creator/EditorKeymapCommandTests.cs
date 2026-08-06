using Forge.Application.Commands;
using Forge.Application.Creator;
using Forge.Application.Tests.Entities;
using Forge.Domain.Authoring;

namespace Forge.Application.Tests.Creator;

public sealed class EditorKeymapCommandTests
{
    private readonly SetEditorKeymapHandler _handler =
        new(new FixedClock(DateTimeOffset.Parse("2026-08-06T12:00:00Z")));

    [Fact]
    public void PreviewApplyUndoAndRedoUseCanonicalCommand()
    {
        var history = new EditorKeymapCommandHistory();
        var changed = EditorKeymapPlanner.UsePreset(history.State.Profile, EditorKeymapPreset.Aurora, "author:test");

        var preview = _handler.Handle(history.State, Command(history.State.Revision, changed, true));
        var applied = _handler.Handle(history.State, Command(history.State.Revision, changed, false));
        history.Record(applied);

        Assert.True(preview.Succeeded);
        Assert.False(preview.Audit.Applied);
        Assert.Equal(".forge/editor-keymap", preview.Patch?.Path);
        Assert.Equal(EditorKeymapPreset.Aurora, history.State.Profile.Preset);
        Assert.True(history.Undo());
        Assert.Equal(EditorKeymapPreset.Standard, history.State.Profile.Preset);
        Assert.True(history.Redo());
        Assert.Equal(EditorKeymapPreset.Aurora, history.State.Profile.Preset);
    }

    [Fact]
    public void StaleOrConflictingConfigurationIsRefusedBeforePersistence()
    {
        var state = new EditorKeymapState();
        var save = state.Profile.Bindings.Single(binding => binding.CommandId == EditorKeymapCatalog.SaveProject).Gesture;
        var invalid = EditorKeymapPlanner.Rebind(state.Profile, EditorKeymapCatalog.OpenSearch, save, "author:test");

        var stale = _handler.Handle(state, Command(new EditorKeymapRevision(4), state.Profile, true));
        var conflict = _handler.Handle(state, Command(state.Revision, invalid, true));

        Assert.Equal(SetEditorKeymapHandler.RevisionConflictCode, Assert.Single(stale.Diagnostics).Code);
        Assert.Contains(conflict.Diagnostics, diagnostic => diagnostic.Code == EditorKeymapProfileValidator.ConflictCode);
    }

    [Fact]
    public void PresetsCustomBindingAndSearchRemainDeterministic()
    {
        var standard = EditorKeymapProfile.CreatePreset(EditorKeymapPreset.Standard);
        var blender = EditorKeymapPlanner.UsePreset(standard, EditorKeymapPreset.Blender, "author:test");
        var custom = EditorKeymapPlanner.Rebind(blender, EditorKeymapCatalog.OpenSearch,
            new EditorKeyGesture("K", ctrl: true, shift: true), "author:test");

        var matches = EditorKeymapPlanner.Search(custom, "recherche");

        Assert.Equal(EditorKeymapPreset.Custom, custom.Preset);
        Assert.Equal("Ctrl+Shift+K", custom.Bindings.Single(binding =>
            binding.CommandId == EditorKeymapCatalog.OpenSearch).Gesture.CanonicalText);
        Assert.Equal(EditorKeymapCatalog.OpenSearch, Assert.Single(matches).CommandId);
    }

    private static SetEditorKeymapCommand Command(
        EditorKeymapRevision revision,
        EditorKeymapProfile profile,
        bool dryRun) =>
        new(CommandId.From(Guid.NewGuid()), revision, profile, dryRun);
}
