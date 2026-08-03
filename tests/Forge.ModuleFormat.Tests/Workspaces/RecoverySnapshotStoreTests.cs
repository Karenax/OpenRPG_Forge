using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Tests.Serialization;
using Forge.ModuleFormat.Workspaces;

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

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
