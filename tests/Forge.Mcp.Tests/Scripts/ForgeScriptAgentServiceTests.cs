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
        Assert.Contains("void say(const string &in)", api.Signatures);
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
}
