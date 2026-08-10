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
        Assert.Equal("AngelScript", api.Language);
        Assert.Equal("2.38", api.DialectVersion);
        Assert.Equal(".as", api.CanonicalSourceExtension);
        Assert.Contains("source + apiVersion + compilerVersion", api.BytecodeCacheKeyContract, StringComparison.Ordinal);
        Assert.Contains("void Handler(const Payload &in e)", api.EntryContracts);
        Assert.Contains(api.Signatures, signature => signature.StartsWith("void say(const string &in", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("AudioEmitterRef audio_emitter", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("void audio_set_active(AudioEmitterRef", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("MovementTaskRef move_to", StringComparison.Ordinal));
        Assert.Equal(6, api.Capabilities.Count);
        Assert.Contains("1.0.0", api.SupportedVersions);
        Assert.Contains(api.Deprecations, item => item.Symbol == "variable");
        Assert.Contains(api.Migrations, item => item.From == "1.0");
        Assert.Contains("filesystem", api.ForbiddenCapabilities);
        Assert.Contains("regex=256/4096", api.SandboxContract, StringComparison.Ordinal);
        Assert.Equal("scripts/forge-script-permissions.json", api.ModulePermissionPath);
        Assert.Equal(1, api.ModulePermissionSchemaVersion);
        Assert.Contains("deny-default", api.ModulePermissionContract, StringComparison.Ordinal);
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
    public void Compiler_and_sandbox_reject_forbidden_capabilities_with_stable_paths()
    {
        const string source = "void main() { file_open(\"secret\"); }";

        var compilation = _agent.Compile(source);
        var sandbox = _agent.TestInSandbox(source);

        Assert.False(compilation.Succeeded);
        Assert.Equal("script.sandbox.capability_forbidden", Assert.Single(compilation.Diagnostics).Code);
        Assert.StartsWith("script/source:1:", compilation.Diagnostics[0].Path, StringComparison.Ordinal);
        Assert.False(sandbox.Succeeded);
        Assert.Equal("capability_forbidden", sandbox.Status);
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
