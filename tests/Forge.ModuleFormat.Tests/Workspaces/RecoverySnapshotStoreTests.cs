using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Tests.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;

namespace Forge.ModuleFormat.Tests.Workspaces;

public sealed class RecoverySnapshotStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OpenRPGForgeTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Capture_and_restore_round_trip_without_touching_canonical_project()
    {
        Directory.CreateDirectory(_root);
        var canonical = Path.Combine(_root, ProjectSession.DefaultProjectFileName);
        File.WriteAllText(canonical, "canonical-sentinel");
        var project = TestProjectFactory.CreateValid();
        var store = new RecoverySnapshotStore();

        var snapshot = store.Capture(_root, project, 7, new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
        var restored = store.Restore(snapshot);

        Assert.Equal("canonical-sentinel", File.ReadAllText(canonical));
        Assert.Equal(ProjectJsonSerializer.Serialize(project), ProjectJsonSerializer.Serialize(restored));
        Assert.Single(store.List(_root));
    }

    [Fact]
    public void Retention_is_bounded_and_tampering_blocks_restore()
    {
        Directory.CreateDirectory(_root);
        var store = new RecoverySnapshotStore(retention: 2);
        var project = TestProjectFactory.CreateValid();
        for (var index = 0; index < 3; index++) store.Capture(_root, project, index, DateTimeOffset.UnixEpoch.AddMinutes(index));
        var snapshots = store.List(_root);
        Assert.Equal(2, snapshots.Count);
        File.AppendAllText(snapshots[0].SnapshotPath, "tampered");
        Assert.Throws<InvalidDataException>(() => store.Restore(snapshots[0]));
    }

    [Fact]
    public void Preview_candidate_and_quarantine_protect_canonical_project()
    {
        Directory.CreateDirectory(_root);
        var store = new RecoverySnapshotStore();
        var project = TestProjectFactory.CreateValid();
        var snapshot = store.Capture(_root, project, 3,
            new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero));
        var preview = store.PreviewRestore(snapshot, project);
        Assert.False(preview.Comparison.Changed);
        Assert.Equal(snapshot.Id, store.FindRecoveryCandidate(_root,
            new DateTimeOffset(2026, 8, 5, 11, 0, 0, TimeSpan.Zero))?.Id);

        File.AppendAllText(snapshot.SnapshotPath, "tampered");
        var inspection = Assert.Single(store.Inspect(_root, quarantineCorruptFiles: true));

        Assert.Equal(RecoverySnapshotStatus.Quarantined, inspection.Status);
        Assert.NotNull(inspection.QuarantinePath);
        Assert.True(File.Exists(inspection.QuarantinePath));
        Assert.False(File.Exists(snapshot.SnapshotPath));
        Assert.Empty(store.List(_root));
    }

    [Fact]
    public void Interrupted_session_and_selective_preview_are_explicit_and_clear_preserves_marker()
    {
        Directory.CreateDirectory(_root);
        var project = TestProjectFactory.CreateValid();
        var current = new ProjectDefinition(project.Id, "Unsaved title", project.Module, project.Blueprints,
            project.Areas, project.Dialogues, project.Quests);
        var sessions = new RecoverySessionStore();
        var first = sessions.Begin(_root, project.Id, DateTimeOffset.UnixEpoch, 1001, "test:r060");
        var second = sessions.Begin(_root, project.Id, DateTimeOffset.UnixEpoch.AddMinutes(1), 1002, "test:r060");
        var store = new RecoverySnapshotStore();
        var snapshot = store.Capture(_root, project, 4, DateTimeOffset.UnixEpoch.AddMinutes(2));
        var selection = new RecoveryRestoreSelection(Guid.NewGuid(), 1,
            RecoveryRestoreSection.ProjectAndModule, "test:r060");

        var preview = store.PreviewRestore(snapshot, current, selection);

        Assert.False(first.PreviousSessionWasUnclean);
        Assert.True(second.PreviousSessionWasUnclean);
        Assert.True(preview.Comparison.Changed);
        Assert.Equal(project.Title, preview.Project.Title);
        Assert.Equal([RecoveryRestoreSection.ProjectAndModule], preview.Comparison.SelectedSections);
        store.Clear(_root);
        Assert.Equal(second.Current.Id, sessions.Read(_root)!.Id);
        Assert.True(sessions.End(_root, second.Current.Id, DateTimeOffset.UnixEpoch.AddMinutes(3)));
        Assert.True(sessions.Read(_root)!.CleanShutdown);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
