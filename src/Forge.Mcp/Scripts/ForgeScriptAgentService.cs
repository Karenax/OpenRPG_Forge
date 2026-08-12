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
        "EntityRef, AreaRef, AssetId, EffectHandle, task/result references, audio, clock, timer, custom-event, hot-reload and debug-session references are opaque typed 64-bit generational handles validated on every call. Scripts expose void main().",
        ForgeScriptApiCatalog.Signatures,
        ForgeScriptSourceDocument.Language,
        ForgeScriptSourceDocument.DialectVersion,
        ".as",
        "sha256(source + apiVersion + compilerVersion)",
        ["void main()", "void Handler(const Payload &in e)"],
        ForgeScriptCapabilityManifest.Capabilities,
        ForgeScriptCapabilityManifest.ExplicitlyForbiddenCapabilities,
        ForgeScriptCapabilityManifest.Summary(),
        ForgeScriptApiCompatibility.SupportedVersions,
        ForgeScriptApiCompatibility.Deprecations,
        ForgeScriptApiCompatibility.Migrations,
        ForgeScriptModulePermissionPolicy.RelativePath,
        ForgeScriptModulePermissionPolicy.CurrentSchemaVersion,
        "deny-default;server-enforced;signed-production;module-kit-event capability intersection")
    {
        QualityTools =
        [
            "unit-tests:testworld-fixtures-mocks-assertions-ci",
            "profiler:cpu-instructions-allocations-hotspots-call-graph-alerts",
            "generated-docs:examples-availability-mcp-resources",
            "static-analysis:heartbeat-query-null-deprecated-authority",
            "script-state:registered-components-migration-validation-no-vm-pointers",
        ],
        GeneratedReference = ForgeScriptQualityService.GenerateDocumentation(
            ForgeScriptApiCatalog.Registrations, "1.0"),
    };

    public ForgeScriptCompileResponse Compile(string source)
    {
        var policyDiagnostics = ForgeScriptCapabilityManifest.ValidateSource(source);
        if (policyDiagnostics.Count > 0)
            return new(false, 0, _runtime.VirtualMachineVersion, _runtime.ApiVersion,
                policyDiagnostics.Select(Map).ToArray());
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
        var policyDiagnostics = ForgeScriptCapabilityManifest.ValidateSource(source);
        if (policyDiagnostics.Count > 0)
            return new(false, "capability_forbidden", [], policyDiagnostics.Select(Map).ToArray());
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

    private static AgentDiagnostic Map(ForgeScriptSourceDiagnostic diagnostic) => new(
        diagnostic.Code, diagnostic.Message, diagnostic.Path, diagnostic.SubjectId?.ToString("D"),
        Severity: diagnostic.Severity, SubjectUuid: diagnostic.SubjectId?.ToString("D"),
        Source: "ForgeScript capability manifest", Provenance: "forge:r013");
}

public sealed record ForgeScriptApiDescription(
    string VirtualMachineVersion,
    string ApiVersion,
    string EntityReferenceContract,
    IReadOnlyList<string> Signatures,
    string Language,
    string DialectVersion,
    string CanonicalSourceExtension,
    string BytecodeCacheKeyContract,
    IReadOnlyList<string> EntryContracts,
    IReadOnlyList<ForgeScriptCapability> Capabilities,
    IReadOnlyList<string> ForbiddenCapabilities,
    string SandboxContract,
    IReadOnlyList<string> SupportedVersions,
    IReadOnlyList<ForgeScriptApiDeprecation> Deprecations,
    IReadOnlyList<ForgeScriptApiMigration> Migrations,
    string ModulePermissionPath,
    int ModulePermissionSchemaVersion,
    string ModulePermissionContract)
{
    public IReadOnlyList<string> QualityTools { get; init; } = [];
    public GeneratedApiDocument? GeneratedReference { get; init; }
}

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
