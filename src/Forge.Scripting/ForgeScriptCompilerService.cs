using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Forge.Scripting;

public sealed record ForgeScriptCompilerOptions(
    bool Incremental,
    bool StableDiagnostics,
    bool PerformanceTelemetry,
    int MaxParallelism,
    int MaxSourceBytes,
    IReadOnlyList<string> ConformanceSuites);

public sealed record ForgeScriptCompilationUnit(
    string Path,
    string Source,
    IReadOnlyList<string> Dependencies);

public sealed record ForgeScriptCompilationEntry(
    string Path,
    bool Success,
    bool CacheHit,
    string Fingerprint,
    long ElapsedMicroseconds,
    int BytecodeBytes,
    IReadOnlyList<ForgeScriptDiagnostic> Diagnostics);

public sealed record ForgeScriptCompilationReport(
    bool Success,
    int Compiled,
    int Reused,
    long ElapsedMicroseconds,
    IReadOnlyList<ForgeScriptCompilationEntry> Entries);

public sealed record ForgeScriptConformanceCase(string Id, string Source, bool ExpectedSuccess, string? ExpectedDiagnosticCode);
public sealed record ForgeScriptConformanceResult(string Id, bool Passed, string Code);

public sealed class ForgeScriptCompilerService
{
    private readonly ForgeScriptArtifactCache _cache;
    private readonly ForgeScriptCompilerOptions _options;
    private readonly Dictionary<string, string> _fingerprints = new(StringComparer.Ordinal);

    public ForgeScriptCompilerService(ForgeScriptArtifactCache cache, ForgeScriptCompilerOptions options)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (options.MaxParallelism is < 1 or > 32 || options.MaxSourceBytes is < 1024 or > 4_194_304)
            throw new ArgumentOutOfRangeException(nameof(options));
    }

    public ForgeScriptCompilationReport Compile(IReadOnlyList<ForgeScriptCompilationUnit> units)
    {
        ArgumentNullException.ThrowIfNull(units);
        var started = Stopwatch.GetTimestamp();
        var ordered = TopologicalOrder(units);
        var currentHashes = ordered.ToDictionary(unit => unit.Path, unit => Hash(unit.Source), StringComparer.Ordinal);
        var entries = new List<ForgeScriptCompilationEntry>();
        foreach (var unit in ordered)
        {
            if (Encoding.UTF8.GetByteCount(unit.Source) > _options.MaxSourceBytes)
            {
                entries.Add(new ForgeScriptCompilationEntry(unit.Path, false, false, string.Empty, 0, 0,
                    [new ForgeScriptDiagnostic("SCRIPT-SOURCE-QUOTA", 0, 0, "error", "Source exceeds the configured byte quota.")]));
                continue;
            }

            var dependencyFingerprint = string.Join('|', unit.Dependencies.Order(StringComparer.Ordinal)
                .Select(path => $"{path}:{currentHashes.GetValueOrDefault(path, "missing")}"));
            var fingerprint = Hash($"{currentHashes[unit.Path]}\n{dependencyFingerprint}");
            var source = string.IsNullOrEmpty(dependencyFingerprint) ? unit.Source :
                $"{unit.Source}\n// forge-dependencies:{fingerprint}";
            var entryStarted = Stopwatch.GetTimestamp();
            var result = _cache.Compile(source);
            var diagnostics = _options.StableDiagnostics
                ? result.Compilation.Diagnostics.OrderBy(item => item.Line).ThenBy(item => item.Column)
                    .ThenBy(item => item.Source, StringComparer.Ordinal).ThenBy(item => item.Message, StringComparer.Ordinal).ToArray()
                : result.Compilation.Diagnostics;
            var unchanged = _options.Incremental && _fingerprints.TryGetValue(unit.Path, out var previous) && previous == fingerprint;
            _fingerprints[unit.Path] = fingerprint;
            entries.Add(new ForgeScriptCompilationEntry(unit.Path, result.Compilation.Success,
                result.CacheHit || unchanged, fingerprint,
                _options.PerformanceTelemetry ? ElapsedMicroseconds(entryStarted) : 0,
                result.Compilation.Artifact?.Bytecode.Length ?? 0, diagnostics));
        }

        return new ForgeScriptCompilationReport(entries.All(entry => entry.Success),
            entries.Count(entry => !entry.CacheHit), entries.Count(entry => entry.CacheHit),
            _options.PerformanceTelemetry ? ElapsedMicroseconds(started) : 0, entries);
    }

    public IReadOnlyList<ForgeScriptConformanceResult> RunConformance(IReadOnlyList<ForgeScriptConformanceCase> cases)
    {
        ArgumentNullException.ThrowIfNull(cases);
        return cases.OrderBy(item => item.Id, StringComparer.Ordinal).Select(test =>
        {
            var result = _cache.Compile(test.Source).Compilation;
            var diagnosticMatch = test.ExpectedDiagnosticCode is null ||
                result.Diagnostics.Any(item => item.Source == test.ExpectedDiagnosticCode);
            var passed = result.Success == test.ExpectedSuccess && diagnosticMatch;
            return new ForgeScriptConformanceResult(test.Id, passed,
                passed ? "compiler.conformance.ok" : "compiler.conformance.failed");
        }).ToArray();
    }

    private static IReadOnlyList<ForgeScriptCompilationUnit> TopologicalOrder(IReadOnlyList<ForgeScriptCompilationUnit> units)
    {
        var byPath = units.ToDictionary(unit => NormalizePath(unit.Path), unit => unit with { Path = NormalizePath(unit.Path) }, StringComparer.Ordinal);
        var result = new List<ForgeScriptCompilationUnit>();
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string path)
        {
            if (visited.Contains(path)) return;
            if (!byPath.TryGetValue(path, out var unit)) throw new InvalidOperationException($"compiler.dependency_missing:{path}");
            if (!visiting.Add(path)) throw new InvalidOperationException($"compiler.dependency_cycle:{path}");
            foreach (var dependency in unit.Dependencies.Select(NormalizePath).Order(StringComparer.Ordinal)) Visit(dependency);
            visiting.Remove(path);
            visited.Add(path);
            result.Add(unit with { Dependencies = unit.Dependencies.Select(NormalizePath).ToArray() });
        }
        foreach (var path in byPath.Keys.Order(StringComparer.Ordinal)) Visit(path);
        return result;
    }

    private static string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalized = path.Replace('\\', '/').Trim('/');
        if (Path.IsPathRooted(normalized) || normalized.Split('/').Any(segment => segment is "." or ".."))
            throw new ArgumentException("Compiler source paths must be safe relative paths.", nameof(path));
        return normalized;
    }

    private static long ElapsedMicroseconds(long started) =>
        (long)((Stopwatch.GetTimestamp() - started) * 1_000_000d / Stopwatch.Frequency);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
