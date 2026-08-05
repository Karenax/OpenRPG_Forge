using Forge.Mcp.Scripts;
using Forge.Scripting;

namespace Forge.Mcp.Tests.Scripts;

public sealed class ForgeScriptAgentServiceTests
{
    private readonly ForgeScriptAgentService _agent = new(new NativeForgeScriptRuntime());

    [Fact]
    public void ApiAndCompilerExposeVersionedStructuredResults()
    {
        var api = _agent.GetApi();
        var result = _agent.Compile("void main() { say(\"hello\"); }");

        Assert.Equal("2.38.0", api.VirtualMachineVersion);
        Assert.Equal("1.0.0", api.ApiVersion);
        Assert.Contains(api.Signatures, signature => signature.StartsWith("void say(const string &in", StringComparison.Ordinal));
        Assert.True(result.Succeeded);
        Assert.True(result.BytecodeSize > 0);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void CompilerDiagnosticsAndSandboxEventsRemainStructured()
    {
        var invalid = _agent.Compile("void main( { say(\"broken\"); }");
        var sandbox = _agent.TestInSandbox("void main() { say(\"hello\"); spawn(\"spirit\"); }");

        Assert.False(invalid.Succeeded);
        Assert.NotEmpty(invalid.Diagnostics);
        Assert.True(sandbox.Succeeded);
        Assert.Contains(sandbox.Events, item => item.Kind == "say" && item.Value == "hello");
        Assert.Contains(sandbox.Events, item => item.Kind == "spawn");
    }

    [Fact]
    public void BatchCompilerAndConformanceAreAvailableWithoutShellAccess()
    {
        var first = _agent.CompileBatch(
        [
            new ForgeScriptCompilationUnit("shared.as", "void main() {}", []),
            new ForgeScriptCompilationUnit("main.as", "void main() {}", ["shared.as"]),
        ]);
        var second = _agent.CompileBatch(
        [
            new ForgeScriptCompilationUnit("shared.as", "void main() {}", []),
            new ForgeScriptCompilationUnit("main.as", "void main() {}", ["shared.as"]),
        ]);
        var conformance = _agent.RunCompilerConformance();

        Assert.True(first.Success);
        Assert.Equal(2, second.Reused);
        Assert.All(conformance, result => Assert.True(result.Passed));
    }
}
