using System.Collections.Concurrent;
using System.Diagnostics;
using Forge.Domain.Areas;

namespace Forge.Application.Areas;

public sealed class AreaStreamingCoordinator
{
    private readonly IAreaChunkLoader _loader;
    private readonly object _cacheLock = new();
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
    private long _cacheBytes;
    private long _accessSequence;

    public AreaStreamingCoordinator(IAreaChunkLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        _loader = loader;
    }

    public async Task<AreaStreamingResult> LoadAsync(AreaLoadingProfile profile,
        IProgress<AreaStreamingProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();
        var seen = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        var results = new ConcurrentDictionary<string, AreaChunkPayload>(StringComparer.Ordinal);
        var inFlight = new ConcurrentDictionary<string, Task<AreaChunkPayload>>(StringComparer.Ordinal);
        using var concurrency = new SemaphoreSlim(profile.MaximumConcurrentLoads, profile.MaximumConcurrentLoads);
        var total = 0;
        var completed = 0;
        var hits = 0;
        long loadedBytes = 0;

        async Task LoadTreeAsync(string resource)
        {
            if (!seen.TryAdd(resource, 0))
            {
                return;
            }

            Interlocked.Increment(ref total);
            cancellationToken.ThrowIfCancellationRequested();
            var fromCache = TryGetCached(resource, out var cached);
            AreaChunkPayload payload;
            if (fromCache)
            {
                payload = cached!;
                Interlocked.Increment(ref hits);
            }
            else
            {
                var task = inFlight.GetOrAdd(resource, path => LoadCoreAsync(path, concurrency, cancellationToken));
                try
                {
                    payload = await task.ConfigureAwait(false);
                }
                finally
                {
                    inFlight.TryRemove(resource, out _);
                }
                ValidatePayload(resource, payload);
                AddToCache(payload, profile.CacheBudgetMegabytes * 1024L * 1024L);
                Interlocked.Add(ref loadedBytes, payload.SizeBytes);
            }

            results[resource] = payload;
            var done = Interlocked.Increment(ref completed);
            progress?.Report(new AreaStreamingProgress(done, Volatile.Read(ref total), resource,
                Volatile.Read(ref hits), Interlocked.Read(ref loadedBytes)));
            if (profile.PrefetchDependencies)
            {
                await Task.WhenAll(payload.Dependencies.Select(LoadTreeAsync)).ConfigureAwait(false);
            }
        }

        await Task.WhenAll(profile.RequiredResources.Select(LoadTreeAsync)).ConfigureAwait(false);
        stopwatch.Stop();
        var ordered = results.Values.OrderBy(item => item.ResourcePath, StringComparer.Ordinal).ToArray();
        return new AreaStreamingResult(ordered, ordered.Length - hits, hits, loadedBytes, stopwatch.Elapsed,
            stopwatch.ElapsedMilliseconds <= profile.TargetLoadMilliseconds);
    }

    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _cache.Clear();
            _cacheBytes = 0;
        }
    }

    private async Task<AreaChunkPayload> LoadCoreAsync(string resource, SemaphoreSlim concurrency,
        CancellationToken cancellationToken)
    {
        await concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _loader.LoadAsync(resource, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            concurrency.Release();
        }
    }

    private bool TryGetCached(string resource, out AreaChunkPayload? payload)
    {
        lock (_cacheLock)
        {
            if (!_cache.TryGetValue(resource, out var entry))
            {
                payload = null;
                return false;
            }

            entry.LastAccess = ++_accessSequence;
            payload = entry.Payload;
            return true;
        }
    }

    private void AddToCache(AreaChunkPayload payload, long budgetBytes)
    {
        lock (_cacheLock)
        {
            if (_cache.Remove(payload.ResourcePath, out var previous))
            {
                _cacheBytes -= previous.Payload.SizeBytes;
            }

            _cache[payload.ResourcePath] = new CacheEntry(payload, ++_accessSequence);
            _cacheBytes += payload.SizeBytes;
            while (_cacheBytes > budgetBytes && _cache.Count > 1)
            {
                var oldest = _cache.MinBy(item => item.Value.LastAccess);
                _cache.Remove(oldest.Key);
                _cacheBytes -= oldest.Value.Payload.SizeBytes;
            }
        }
    }

    private static void ValidatePayload(string requestedResource, AreaChunkPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (!string.Equals(requestedResource, payload.ResourcePath, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Area chunk loader returned '{payload.ResourcePath}' for '{requestedResource}'.");
        }

        foreach (var dependency in payload.Dependencies)
        {
            _ = WeatherStateProfile.NormalizeOptionalResource(dependency);
        }
    }

    private sealed class CacheEntry(AreaChunkPayload payload, long lastAccess)
    {
        public AreaChunkPayload Payload { get; } = payload;
        public long LastAccess { get; set; } = lastAccess;
    }
}
