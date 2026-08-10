using Forge.Application.Creator;
using Forge.Domain.Authoring;

namespace Forge.Application.Tests.Creator;

public sealed class EditorWorkspaceProfileCommandHistoryTests
{
    [Fact]
    public void CommandSupportsDryRunApplyUndoAndRedoWithoutChangingProfileIdentity()
    {
        var initial = EditorWorkspaceProfile.CreateDefault(EditorRole.Builder);
        var shell = new EditorShellConfiguration(
            Guid.Parse("35000000-0000-4000-8000-000000000001"), 1, "test",
            new EditorShellLayout(Guid.Parse("35000000-0000-4000-8000-000000000002"), 1, "test", 280, 360, 180),
            EditorExplorerGrouping.Tag, EditorViewportMode.Collisions,
            new EditorPaletteFilter(tags: ["dungeon"]));
        var candidate = EditorWorkspaceProfilePlanner.ConfigureShell(initial, shell);
        var history = new EditorWorkspaceProfileCommandHistory(initial);
        var command = new UpdateEditorWorkspaceProfileCommand(
            Guid.Parse("35000000-0000-4000-8000-000000000003"), 1, "test", candidate);

        var preview = history.Execute(command with { DryRun = true });
        Assert.True(preview.Succeeded);
        Assert.False(preview.Applied);
        Assert.Equal(EditorViewportMode.Editing, history.Current.Shell.ViewportMode);

        var applied = history.Execute(command);
        Assert.True(applied.Applied);
        Assert.Equal(initial.Id, applied.Profile.Id);
        Assert.Equal(EditorViewportMode.Collisions, history.Current.Shell.ViewportMode);
        Assert.True(history.Undo().Succeeded);
        Assert.Equal(EditorViewportMode.Editing, history.Current.Shell.ViewportMode);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(EditorExplorerGrouping.Tag, history.Current.Shell.ExplorerGrouping);
    }

    [Fact]
    public void CommandRejectsInvalidEnvelopeAndProfileIdentityWithLocalizedDiagnostics()
    {
        var initial = EditorWorkspaceProfile.CreateDefault(EditorRole.Builder);
        var other = EditorWorkspaceProfile.CreateDefault(EditorRole.Writer);
        var history = new EditorWorkspaceProfileCommandHistory(initial);

        Assert.Equal(EditorWorkspaceProfileCommandHistory.InvalidIdCode,
            Assert.Single(history.Execute(new UpdateEditorWorkspaceProfileCommand(
                Guid.Empty, 1, "test", initial)).Diagnostics).Code);
        Assert.Equal(EditorWorkspaceProfileCommandHistory.UnsupportedSchemaCode,
            Assert.Single(history.Execute(new UpdateEditorWorkspaceProfileCommand(
                Guid.NewGuid(), 99, "test", initial)).Diagnostics).Code);
        Assert.Equal(EditorWorkspaceProfileCommandHistory.MissingProvenanceCode,
            Assert.Single(history.Execute(new UpdateEditorWorkspaceProfileCommand(
                Guid.NewGuid(), 1, " ", initial)).Diagnostics).Code);
        Assert.Equal(EditorWorkspaceProfileCommandHistory.ProfileIdentityCode,
            Assert.Single(history.Execute(new UpdateEditorWorkspaceProfileCommand(
                Guid.NewGuid(), 1, "test", other)).Diagnostics).Code);
        Assert.Same(initial, history.Current);
    }
}
