using System.Text;

namespace Forge.Scripting.Tests;

public sealed class ForgeScriptArtifactCacheTests : IDisposable
{
    private readonly string _cacheRoot = Path.Combine(
        Path.GetTempPath(),
        "openrpg-forge-script-cache-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void CompileMissThenHitUsesDeterministicVersionedKey()
    {
        var runtime = new CountingRuntime();
        var cache = new ForgeScriptArtifactCache(runtime, _cacheRoot);

        var first = cache.Compile("void main() { say(\"ember\"); }");
        var second = cache.Compile("void main() { say(\"ember\"); }");

        Assert.False(first.CacheHit);
        Assert.True(second.CacheHit);
        Assert.Equal(first.CacheKey, second.CacheKey);
        Assert.Equal(first.Metadata!.Id, second.Metadata!.Id);
        Assert.Equal(ForgeScriptCacheMetadata.CurrentSchemaVersion, second.Metadata.SchemaVersion);
        Assert.Equal(1, runtime.CompileCount);
        Assert.Equal(first.Compilation.Artifact!.Bytecode, second.Compilation.Artifact!.Bytecode);
    }

    [Fact]
    public void Ux0ISourceApiAndCompilerVersionsInvalidateIndependently()
    {
        var runtime = new CountingRuntime();
        var cache = new ForgeScriptArtifactCache(runtime, _cacheRoot);
        var first = cache.Compile("void main() {}");
        var changedSource = cache.Compile("void main() { say(\"changed\"); }");
        runtime.ApiVersion = "1.1.0";
        var changedApi = cache.Compile("void main() {}");
        runtime.CompilerVersion = "2.39.0";
        var changedCompiler = cache.Compile("void main() {}");

        Assert.Equal(4, runtime.CompileCount);
        Assert.Equal(4, new[] { first.CacheKey, changedSource.CacheKey, changedApi.CacheKey, changedCompiler.CacheKey }
            .Distinct().Count());
    }

    [Fact]
    public void CorruptBytecodeIsRejectedAndRebuilt()
    {
        var runtime = new CountingRuntime();
        var cache = new ForgeScriptArtifactCache(runtime, _cacheRoot);
        var first = cache.Compile("void main() {}");
        File.WriteAllBytes(Path.Combine(_cacheRoot, $"{first.CacheKey}.bin"), [0, 1, 2]);

        var rebuilt = cache.Compile("void main() {}");

        Assert.False(rebuilt.CacheHit);
        Assert.Equal(2, runtime.CompileCount);
        Assert.Equal(Encoding.UTF8.GetBytes("void main() {}"), rebuilt.Compilation.Artifact!.Bytecode);
    }

    [Fact]
    public void Independent_cache_roots_produce_identical_reproducible_artifacts()
    {
        var secondRoot = $"{_cacheRoot}-second";
        try
        {
            var first = new ForgeScriptArtifactCache(new CountingRuntime(), _cacheRoot)
                .Compile("void main() { say(\"deterministic\"); }");
            var second = new ForgeScriptArtifactCache(new CountingRuntime(), secondRoot)
                .Compile("void main() { say(\"deterministic\"); }");

            Assert.Equal(first.CacheKey, second.CacheKey);
            Assert.Equal(first.Metadata!.Id, second.Metadata!.Id);
            Assert.Equal(first.Metadata.BytecodeSha256, second.Metadata.BytecodeSha256);
            Assert.Equal(first.Compilation.Artifact!.Bytecode, second.Compilation.Artifact!.Bytecode);
        }
        finally
        {
            if (Directory.Exists(secondRoot)) Directory.Delete(secondRoot, true);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheRoot))
        {
            Directory.Delete(_cacheRoot, true);
        }
    }

    private sealed class CountingRuntime : IForgeScriptRuntime
    {
        public int CompileCount { get; private set; }

        public string CompilerVersion { get; set; } = "2.38.0";

        public string VirtualMachineVersion => CompilerVersion;

        public string ApiVersion { get; set; } = "1.0.0";

        public ForgeScriptCompileResult Compile(string source)
        {
            CompileCount++;
            return new ForgeScriptCompileResult(
                true,
                new ForgeScriptArtifact(source, Encoding.UTF8.GetBytes(source), VirtualMachineVersion, ApiVersion),
                []);
        }

        public ForgeScriptExecutionResult Execute(ForgeScriptArtifact artifact, ForgeScriptQuotas? quotas = null) =>
            new(ForgeScriptExecutionStatus.Completed, [], []);
    }
}
