using Forge.Scripting;

namespace Forge.Mcp.Scripts;

public sealed class ForgeScriptAgentService
{
    private readonly IForgeScriptRuntime _runtime;
    private readonly ForgeScriptCompilerService _compiler;

    public ForgeScriptAgentService(IForgeScriptRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
        var cacheRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge", "mcp-script-cache");
        _compiler = new ForgeScriptCompilerService(new ForgeScriptArtifactCache(runtime, cacheRoot),
            new ForgeScriptCompilerOptions(true, true, true, 4, 524_288,
                ["syntax", "diagnostics", "sandbox", "incremental"]));
    }

    public ForgeScriptApiDescription GetApi() => new(
        _runtime.VirtualMachineVersion,
        _runtime.ApiVersion,
        "EntityRef is an opaque 64-bit entity handle. Scripts expose void main().",
        ForgeScriptApiCatalog.Signatures);

    public ForgeScriptCompileResponse Compile(string source)
    {
        var result = _runtime.Compile(source);
        return new ForgeScriptCompileResponse(
            result.Success,
            result.Artifact?.Bytecode.Length ?? 0,
            result.Artifact?.VirtualMachineVersion ?? _runtime.VirtualMachineVersion,
            result.Artifact?.ApiVersion ?? _runtime.ApiVersion,
            result.Diagnostics.Select(Map).ToArray());
    }

    public ForgeScriptCompilationReport CompileBatch(IReadOnlyList<ForgeScriptCompilationUnit> units) =>
        _compiler.Compile(units);

    public IReadOnlyList<ForgeScriptConformanceResult> RunCompilerConformance() => _compiler.RunConformance(
    [
        new ForgeScriptConformanceCase("empty-main", "void main() {}", true, null),
        new ForgeScriptConformanceCase("unknown-api", "void main() { missing_api(); }", false, null),
    ]);

    public ForgeScriptSandboxResponse TestInSandbox(
        string source,
        ForgeScriptQuotas? quotas = null)
    {
        var compiled = _runtime.Compile(source);
        if (!compiled.Success || compiled.Artifact is null)
        {
            return new ForgeScriptSandboxResponse(
                false,
                "compile_failed",
                [],
                compiled.Diagnostics.Select(Map).ToArray());
        }

        var executed = _runtime.Execute(compiled.Artifact, quotas);
        return new ForgeScriptSandboxResponse(
            executed.Status == ForgeScriptExecutionStatus.Completed,
            executed.Status.ToString().ToLowerInvariant(),
            executed.Events.Select(item => new ForgeScriptEventResponse(item.Kind, item.Value)).ToArray(),
            executed.Diagnostics.Select(Map).ToArray());
    }

    private static AgentDiagnostic Map(ForgeScriptDiagnostic diagnostic) => new(
        diagnostic.Source,
        $"{diagnostic.Severity}: {diagnostic.Message} (line {diagnostic.Line}, column {diagnostic.Column})");
}

public sealed record ForgeScriptApiDescription(
    string VirtualMachineVersion,
    string ApiVersion,
    string EntityReferenceContract,
    IReadOnlyList<string> Signatures);

public sealed record ForgeScriptCompileResponse(
    bool Succeeded,
    int BytecodeSize,
    string VirtualMachineVersion,
    string ApiVersion,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record ForgeScriptSandboxResponse(
    bool Succeeded,
    string Status,
    IReadOnlyList<ForgeScriptEventResponse> Events,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record ForgeScriptEventResponse(string Kind, string Value);
