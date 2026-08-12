using Forge.Application.Areas;
using Forge.Domain.Areas;

namespace Forge.Application.Tests.Areas;

public sealed class AreaStreamingCoordinatorTests
{
    [Fact]
    public async Task PrefetchesDependenciesReportsProgressAndReusesCache()
    {
        var loader = new RecordingLoader(new Dictionary<string, AreaChunkPayload>(StringComparer.Ordinal)
        {
            ["areas/forest/terrain.bin"] = Payload("areas/forest/terrain.bin", 12, ["assets/ground.glb"]),
            ["areas/forest/entities.bin"] = Payload("areas/forest/entities.bin", 8, ["assets/ground.glb"]),
            ["assets/ground.glb"] = Payload("assets/ground.glb", 32, []),
        });
        var coordinator = new AreaStreamingCoordinator(loader);
        var profile = Profile(["areas/forest/terrain.bin", "areas/forest/entities.bin"]);
        var updates = new List<AreaStreamingProgress>();

        var first = await coordinator.LoadAsync(profile, new InlineProgress(updates.Add));
        var second = await coordinator.LoadAsync(profile);

        Assert.Equal(3, first.Resources.Count);
        Assert.Equal(3, first.LoadedResources);
        Assert.Equal(52, first.LoadedBytes);
        Assert.Equal(3, loader.Calls.Count);
        Assert.Equal(3, second.CacheHits);
        Assert.Equal(0, second.LoadedResources);
        Assert.Equal(3, loader.Calls.Count);
        Assert.Equal(1, updates[^1].Fraction);
        Assert.True(first.TargetMet);
    }

    [Fact]
    public async Task PropagatesCancellationToActiveLoad()
    {
        var loader = new BlockingLoader();
        var coordinator = new AreaStreamingCoordinator(loader);
        using var cancellation = new CancellationTokenSource();
        var load = coordinator.LoadAsync(Profile(["areas/slow.bin"]), cancellationToken: cancellation.Token);

        await loader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => load);
        Assert.True(loader.CancellationObserved);
    }

    [Fact]
    public async Task DisabledAsyncAndCancellationRunSequentiallyAndIgnoreCallerCancellation()
    {
        var loader = new ConcurrentLoader();
        var coordinator = new AreaStreamingCoordinator(loader);
        var profile = new AreaLoadingProfile(Guid.NewGuid(), 1, false, false, false,
            64, 8, 1, 1000, ["areas/a.bin", "areas/b.bin"], "test:r116");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await coordinator.LoadAsync(profile, cancellationToken: cancellation.Token);

        Assert.Equal(2, result.LoadedResources);
        Assert.Equal(1, loader.MaximumConcurrency);
    }

    [Fact]
    public void BenchmarkGateReturnsStableLocalizedDiagnosticWhenTargetIsMissed()
    {
        var profile = Profile(["areas/slow.bin"]);
        var result = new AreaStreamingResult([], 1, 0, 16, TimeSpan.FromMilliseconds(1200), false);

        var evaluation = AreaStreamingBenchmarkGate.Evaluate(profile, result);

        Assert.False(evaluation.Passed);
        var diagnostic = Assert.Single(evaluation.Diagnostics);
        Assert.Equal(AreaStreamingBenchmarkGate.TargetExceededCode, diagnostic.Code);
        Assert.Equal("area.world.loading.targetLoadMilliseconds", diagnostic.Path);
        Assert.Equal(profile.Id, diagnostic.ProfileId);
    }

    private static AreaLoadingProfile Profile(IReadOnlyList<string> resources) => new(Guid.NewGuid(), 1,
        true, true, true, 64, 2, 1, 1000, resources, "test");

    private static AreaChunkPayload Payload(string path, int bytes, IReadOnlyList<string> dependencies) =>
        new(path, new byte[bytes], dependencies);

    private sealed class RecordingLoader(IReadOnlyDictionary<string, AreaChunkPayload> payloads) : IAreaChunkLoader
    {
        public List<string> Calls { get; } = [];

        public Task<AreaChunkPayload> LoadAsync(string resourcePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (Calls) Calls.Add(resourcePath);
            return Task.FromResult(payloads[resourcePath]);
        }
    }

    private sealed class BlockingLoader : IAreaChunkLoader
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool CancellationObserved { get; private set; }

        public async Task<AreaChunkPayload> LoadAsync(string resourcePath, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("Unreachable.");
            }
            catch (OperationCanceledException)
            {
                CancellationObserved = true;
                throw;
            }
        }
    }

    private sealed class ConcurrentLoader : IAreaChunkLoader
    {
        private int _active;
        private int _maximumConcurrency;

        public int MaximumConcurrency => Volatile.Read(ref _maximumConcurrency);

        public async Task<AreaChunkPayload> LoadAsync(string resourcePath, CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref _active);
            while (true)
            {
                var observed = Volatile.Read(ref _maximumConcurrency);
                if (active <= observed ||
                    Interlocked.CompareExchange(ref _maximumConcurrency, active, observed) == observed)
                {
                    break;
                }
            }

            try
            {
                await Task.Delay(20, cancellationToken);
                return Payload(resourcePath, 8, []);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }
    }

    private sealed class InlineProgress(Action<AreaStreamingProgress> report) : IProgress<AreaStreamingProgress>
    {
        public void Report(AreaStreamingProgress value) => report(value);
    }
}
