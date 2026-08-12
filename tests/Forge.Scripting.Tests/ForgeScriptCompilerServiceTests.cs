using System.Text;

namespace Forge.Scripting.Tests;

public sealed class ForgeScriptCompilerServiceTests : IDisposable
{
    private readonly string _cache = Path.Combine(Path.GetTempPath(), "openrpg-forge-compiler-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Compiler_is_incremental_dependency_aware_stable_and_instrumented()
    {
        var runtime = new FakeRuntime();
        var service = new ForgeScriptCompilerService(new ForgeScriptArtifactCache(runtime, _cache),
            new ForgeScriptCompilerOptions(true, true, true, 4, 4096, ["incremental"]));
        var units = new[]
        {
            new ForgeScriptCompilationUnit("shared.as", "void helper() {}", []),
            new ForgeScriptCompilationUnit("main.as", "void main() {}", ["shared.as"]),
        };

        var first = service.Compile(units);
        var second = service.Compile(units);
        var changed = service.Compile([units[0] with { Source = "void helper() { say(\"changed\"); }" }, units[1]]);

        Assert.True(first.Success);
        Assert.Equal(2, first.Compiled);
        Assert.Equal(2, second.Reused);
        Assert.Equal(2, changed.Compiled);
        Assert.All(first.Entries, entry => Assert.True(entry.ElapsedMicroseconds >= 0));
        Assert.Equal(["main.as", "shared.as"], first.Entries.Select(entry => entry.Path).Order());
    }

    [Fact]
    public void Conformance_reports_expected_success_and_failure()
    {
        var service = new ForgeScriptCompilerService(new ForgeScriptArtifactCache(new FakeRuntime(), _cache),
            new ForgeScriptCompilerOptions(true, true, true, 1, 4096, ["syntax"]));
        var results = service.RunConformance(
        [
            new ForgeScriptConformanceCase("good", "void main() {}", true, null),
            new ForgeScriptConformanceCase("bad", "invalid", false, "parser"),
        ]);
        Assert.All(results, result => Assert.True(result.Passed));
    }

    [Fact]
    public void Independent_units_compile_within_parallelism_bound_and_keep_input_order()
    {
        var runtime = new ConcurrentRuntime();
        var service = new ForgeScriptCompilerService(new ForgeScriptArtifactCache(runtime, _cache),
            new ForgeScriptCompilerOptions(true, true, true, 4, 4096, ["parallel"]));
        var units = Enumerable.Range(0, 12).Select(index =>
            new ForgeScriptCompilationUnit($"script-{index:00}.as", $"void main() {{ int value = {index}; }}", []))
            .ToArray();

        var report = service.Compile(units);

        Assert.True(report.Success);
        // Parallel.ForEach may legitimately use a single worker on a constrained runner.
        // The compiler contract is the configured upper bound, not a guaranteed minimum.
        Assert.InRange(runtime.MaxConcurrent, 1, 4);
        Assert.Equal(units.Select(item => item.Path), report.Entries.Select(item => item.Path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_cache)) Directory.Delete(_cache, true);
    }

    private sealed class FakeRuntime : IForgeScriptRuntime
    {
        public string VirtualMachineVersion => "test-compiler";
        public string ApiVersion => "1.0";
        public ForgeScriptCompileResult Compile(string source) => source.Contains("invalid", StringComparison.Ordinal)
            ? new(false, null, [new ForgeScriptDiagnostic("parser", 4, 2, "error", "invalid source")])
            : new(true, new ForgeScriptArtifact(source, Encoding.UTF8.GetBytes(source), VirtualMachineVersion, ApiVersion), []);
        public ForgeScriptExecutionResult Execute(ForgeScriptArtifact artifact, ForgeScriptQuotas? quotas = null) =>
            new(ForgeScriptExecutionStatus.Completed, [], []);
    }

    private sealed class ConcurrentRuntime : IForgeScriptRuntime
    {
        private int _concurrent;
        private int _maxConcurrent;
        public int MaxConcurrent => _maxConcurrent;
        public string VirtualMachineVersion => "parallel-test";
        public string ApiVersion => "1.0.0";

        public ForgeScriptCompileResult Compile(string source)
        {
            var concurrent = Interlocked.Increment(ref _concurrent);
            int observed;
            do
            {
                observed = _maxConcurrent;
                if (observed >= concurrent) break;
            } while (Interlocked.CompareExchange(ref _maxConcurrent, concurrent, observed) != observed);
            Thread.Sleep(40);
            Interlocked.Decrement(ref _concurrent);
            return new(true, new ForgeScriptArtifact(source, Encoding.UTF8.GetBytes(source),
                VirtualMachineVersion, ApiVersion), []);
        }

        public ForgeScriptExecutionResult Execute(ForgeScriptArtifact artifact, ForgeScriptQuotas? quotas = null) =>
            new(ForgeScriptExecutionStatus.Completed, [], []);
    }
}
