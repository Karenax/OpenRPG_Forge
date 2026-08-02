namespace Forge.Scripting.Tests;

public sealed class NativeForgeScriptRuntimeTests
{
    private readonly NativeForgeScriptRuntime _runtime = new();

    [Fact]
    public void NativeHost_ReportsPinnedVmAndApiVersions()
    {
        Assert.Equal("2.38.0", _runtime.VirtualMachineVersion);
        Assert.Equal("1.0.0", _runtime.ApiVersion);
    }

    [Fact]
    public void CompileAndExecute_LoadsBytecodeAndEmitsMinimalApiEvents()
    {
        const string source = """
            void main()
            {
                EntityRef hero = 42;
                int current = health(hero);
                variable("warden.phase", "two");
                say("La braise se lève.");
                cast_ability(hero, "ash-nova");
                EntityRef spirit = spawn("ash-spirit");
                quest("crypt-of-ashes");
                unlock(spirit);
            }
            """;

        var compilation = _runtime.Compile(source);
        var artifact = Assert.IsType<ForgeScriptArtifact>(compilation.Artifact);
        var result = _runtime.Execute(artifact);

        Assert.True(compilation.Success);
        Assert.NotEmpty(artifact.Bytecode);
        Assert.Equal(ForgeScriptExecutionStatus.Completed, result.Status);
        Assert.Equal(
            ["health", "variable", "say", "cast", "spawn", "quest", "unlock"],
            result.Events.Select(scriptEvent => scriptEvent.Kind));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Compile_ReturnsStructuredDiagnosticsForInvalidSource()
    {
        var result = _runtime.Compile("void main() { missing_api(); }");

        Assert.False(result.Success);
        Assert.Null(result.Artifact);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Severity == "error" && diagnostic.Message.Contains("missing_api", StringComparison.Ordinal));
    }

    [Fact]
    public void Execute_StopsAtInstructionQuotaAndHostContinues()
    {
        var endless = Compile("void main() { while (true) { say(\"loop\"); } }");

        var failed = _runtime.Execute(
            endless,
            new ForgeScriptQuotas(instructionLimit: 8, timeoutMilliseconds: 1000));
        var healthy = _runtime.Execute(Compile("void main() { say(\"healthy\"); }"));

        Assert.Equal(ForgeScriptExecutionStatus.QuotaExceeded, failed.Status);
        Assert.Contains(failed.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("instruction_limit", StringComparison.Ordinal));
        Assert.Equal(ForgeScriptExecutionStatus.Completed, healthy.Status);
        Assert.Equal("healthy", Assert.Single(healthy.Events).Value);
    }

    [Fact]
    public void Execute_EnforcesRecursionAndSpawnCaps()
    {
        var recursive = Compile("void dive() { dive(); } void main() { dive(); }");
        var spawning = Compile("void main() { spawn(\"a\"); spawn(\"b\"); }");

        var recursionResult = _runtime.Execute(
            recursive,
            new ForgeScriptQuotas(recursionLimit: 4));
        var spawnResult = _runtime.Execute(
            spawning,
            new ForgeScriptQuotas(spawnCap: 1));

        Assert.NotEqual(ForgeScriptExecutionStatus.Completed, recursionResult.Status);
        Assert.Equal(ForgeScriptExecutionStatus.QuotaExceeded, spawnResult.Status);
        Assert.Contains(spawnResult.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("spawn_cap", StringComparison.Ordinal));
    }

    [Fact]
    public void Execute_EnforcesWallClockTimeout()
    {
        var endless = Compile("void main() { while (true) { int value = 1; } }");

        var result = _runtime.Execute(
            endless,
            new ForgeScriptQuotas(instructionLimit: int.MaxValue, timeoutMilliseconds: 1));

        Assert.Equal(ForgeScriptExecutionStatus.QuotaExceeded, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("timeout", StringComparison.Ordinal));
    }

    [Fact]
    public void Execute_RejectsIncompatibleVersionMetadata()
    {
        var artifact = Compile("void main() {}");
        var incompatible = artifact with { ApiVersion = "99.0.0" };

        Assert.Throws<InvalidOperationException>(() => _runtime.Execute(incompatible));
    }

    private ForgeScriptArtifact Compile(string source)
    {
        var result = _runtime.Compile(source);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        return Assert.IsType<ForgeScriptArtifact>(result.Artifact);
    }
}
