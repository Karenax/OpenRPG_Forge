using System.Text;
using Forge.ModuleFormat.Build;

namespace Forge.ModuleFormat.Tests.Build;

public sealed class DeterministicBuildTaskSchedulerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRPGForgeTests", "r036-build-scheduler", Guid.NewGuid().ToString("N"));

    [Fact]
    public void SchedulerExecutesIndependentTasksInParallelButReturnsStableOrder()
    {
        using var started = new CountdownEvent(4);
        var taskIds = Enumerable.Range(0, 8).Select(index => $"task-{7 - index}").ToArray();

        var batch = DeterministicBuildTaskScheduler.Execute(taskIds, 4, taskId =>
        {
            if (started.CurrentCount > 0)
            {
                started.Signal();
                Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            }
            return taskId.ToUpperInvariant();
        });
        var sequential = DeterministicBuildTaskScheduler.Execute(taskIds, 1, taskId => taskId.ToUpperInvariant());

        Assert.Equal(taskIds.Order(StringComparer.Ordinal).Select(item => item.ToUpperInvariant()), batch.Results);
        Assert.Equal(4, batch.Metrics.PeakParallelism);
        Assert.Equal(4, batch.Metrics.MaximumParallelism);
        Assert.Equal(8, batch.Metrics.TaskCount);
        Assert.Equal(batch.Metrics.OrderFingerprint, sequential.Metrics.OrderFingerprint);
        Assert.Equal(batch.Results, sequential.Results);
    }

    [Fact]
    public void WorkspaceMaterializationIsConfinedAndByteStableAcrossParallelismLevels()
    {
        Directory.CreateDirectory(_root);
        var paths = Enumerable.Range(0, 6).Select(index => Path.Combine(_root, $"file-{index}.txt")).ToArray();
        foreach (var path in paths.Reverse())
        {
            File.WriteAllText(path, Path.GetFileName(path), new UTF8Encoding(false));
        }

        var parallel = WorkspaceFileMaterializer.Materialize(_root, paths.Reverse(), 4);
        var sequential = WorkspaceFileMaterializer.Materialize(_root, paths, 1);

        Assert.Equal(paths.Select(Path.GetFileName).Order(StringComparer.Ordinal),
            parallel.Results.Select(item => item.RelativePath));
        Assert.Equal(sequential.Results.Select(item => item.Content), parallel.Results.Select(item => item.Content),
            ByteArrayComparer.Instance);
        Assert.Equal(sequential.Metrics.OrderFingerprint, parallel.Metrics.OrderFingerprint);
        Assert.Throws<InvalidOperationException>(() => WorkspaceFileMaterializer.Materialize(
            _root, [Path.Combine(Path.GetDirectoryName(_root)!, "outside.txt")], 1));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public static readonly ByteArrayComparer Instance = new();
        public bool Equals(byte[]? left, byte[]? right) =>
            left is not null && right is not null && left.SequenceEqual(right);
        public int GetHashCode(byte[] value) => value.Length;
    }
}
