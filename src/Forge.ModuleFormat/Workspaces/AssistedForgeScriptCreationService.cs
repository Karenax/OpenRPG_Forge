using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Assistant;
using Forge.Domain.Assistant;
using Forge.Domain.Projects;
using Forge.Scripting;

namespace Forge.ModuleFormat.Workspaces;

public sealed record AssistedForgeScriptBrief(
    Guid ScriptId,
    string RelativePath,
    string EventBinding,
    string Intent,
    string Message,
    string VariableKey,
    string VariableValue,
    IReadOnlyList<string> ContextCitations,
    IReadOnlyList<string> StyleRules,
    string AuthorId,
    int Seed,
    string? SourceOverride = null);

public sealed record AssistedForgeScriptTestEvidence(
    string Id,
    bool Passed,
    string Detail);

public sealed record AssistedForgeScriptProposal(
    bool Succeeded,
    Guid ScriptId,
    int SchemaVersion,
    string RelativePath,
    string EventBinding,
    string Source,
    string ForgeScriptApiVersion,
    string VirtualMachineVersion,
    IReadOnlyList<ForgeScriptDiagnostic> Diagnostics,
    IReadOnlyList<AssistedForgeScriptTestEvidence> Tests,
    IReadOnlyList<string> Citations,
    IReadOnlyList<string> StyleRules,
    string BaseContentSha256,
    string SourceSha256,
    string SemanticDiff,
    string PromptSha256,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCostUsd,
    string ProviderKey,
    string Provenance,
    bool ApprovedForApplication)
{
    public bool CanApply => Succeeded && Diagnostics.All(item => !string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase)) &&
                            Tests.Count >= 3 && Tests.All(item => item.Passed);

    public AssistedForgeScriptProposal WithApproval(bool approved, string provenance) => this with
    {
        ApprovedForApplication = approved,
        Provenance = string.IsNullOrWhiteSpace(provenance) ? Provenance : provenance.Trim(),
    };
}

public static class AssistedForgeScriptCreationService
{
    public const string EmptyContentSha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    public static async Task<AssistedForgeScriptProposal> ProposeAsync(
        AssistedForgeScriptBrief brief,
        string workspaceRoot,
        ProjectDefinition project,
        LoreIndexDefinition lore,
        IAssistantProvider provider,
        AssistantProviderDefinition providerDefinition,
        IForgeScriptRuntime runtime,
        bool networkAuthorized,
        bool projectContentAuthorized,
        CancellationToken cancellationToken = default)
    {
        ValidateBrief(brief);
        var lorePacket = LoreContextAssembler.Assemble(lore, project, brief.Intent, 8);
        var api = ForgeScriptApiCatalog.Registrations.Select(item => item.Signature).ToArray();
        const string system = "Write one bounded ForgeScript main function. Use only the supplied API. Never access files, network, processes, threads or native code.";
        var user = JsonSerializer.Serialize(new
        {
            brief.EventBinding,
            brief.Intent,
            brief.Message,
            brief.VariableKey,
            brief.VariableValue,
            brief.StyleRules,
            brief.ContextCitations,
            api,
            lore = projectContentAuthorized ? lorePacket.Content : "[project content withheld]",
        });
        const string schema = "{\"type\":\"object\",\"required\":[\"source\",\"tests\"],\"properties\":{\"source\":{\"type\":\"string\"},\"tests\":{\"type\":\"array\"}}}";
        var hash = Sha256(string.Join('\n', system, user, schema));
        var response = await provider.GenerateAsync(new AssistantProviderRequest(
            brief.ScriptId,
            Guid.NewGuid(),
            new AssistantPromptRenderResult("forgescript-creation", "1.0.0", system, user, schema, hash),
            AssistantPermissionLevel.Propose,
            Math.Min(2048, providerDefinition.MaximumOutputTokens),
            networkAuthorized,
            projectContentAuthorized), cancellationToken).ConfigureAwait(false);
        return Generate(brief, workspaceRoot, runtime, response.ProviderKey, response.EstimatedCost,
            response.Provenance, hash, response.InputTokens, response.OutputTokens);
    }

    public static AssistedForgeScriptProposal Generate(
        AssistedForgeScriptBrief brief,
        string workspaceRoot,
        IForgeScriptRuntime runtime,
        string providerKey = "offline-ci",
        decimal estimatedCostUsd = 0,
        string provenance = "assistant:deterministic",
        string promptSha256 = "offline",
        int inputTokens = 0,
        int outputTokens = 0)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ValidateBrief(brief);
        var relativePath = NormalizeRelativePath(brief.RelativePath);
        var absolutePath = ResolvePath(workspaceRoot, relativePath);
        var previous = File.Exists(absolutePath) ? File.ReadAllText(absolutePath, Encoding.UTF8) : string.Empty;
        var source = string.IsNullOrWhiteSpace(brief.SourceOverride) ? BuildSource(brief) : NormalizeSource(brief.SourceOverride);
        var compilation = runtime.Compile(source);
        var tests = new List<AssistedForgeScriptTestEvidence>
        {
            new("compile", compilation.Success && compilation.Artifact is not null,
                compilation.Success ? $"bytecode:{compilation.Artifact?.Bytecode.Length ?? 0}" : "compile-failed"),
            new("api-version", compilation.Artifact?.ApiVersion == runtime.ApiVersion,
                $"expected:{runtime.ApiVersion}|actual:{compilation.Artifact?.ApiVersion ?? "none"}"),
        };
        if (compilation.Success && compilation.Artifact is not null)
        {
            var execution = runtime.Execute(compilation.Artifact, new ForgeScriptQuotas(25_000, 250, 16, 8));
            tests.Add(new("sandbox", execution.Status == ForgeScriptExecutionStatus.Completed &&
                                     execution.Events.Any(item => item.Kind == "say"),
                $"status:{execution.Status}|events:{execution.Events.Count}"));
        }
        else
        {
            tests.Add(new("sandbox", false, "not-executed"));
        }
        var sourceHash = Sha256(source);
        var baseHash = Sha256(previous);
        var operation = previous.Length == 0 ? "add-script" : "replace-script";
        return new(
            compilation.Success && tests.All(item => item.Passed),
            brief.ScriptId,
            1,
            relativePath,
            brief.EventBinding.Trim(),
            source,
            runtime.ApiVersion,
            runtime.VirtualMachineVersion,
            compilation.Diagnostics,
            tests,
            brief.ContextCitations.Select(item => item.Trim()).ToArray(),
            brief.StyleRules.Select(item => item.Trim()).ToArray(),
            baseHash,
            sourceHash,
            $"{operation}:{relativePath}|event:{brief.EventBinding.Trim()}|api:{runtime.ApiVersion}|tests:{tests.Count}|source:{sourceHash[..12]}",
            promptSha256,
            inputTokens,
            outputTokens,
            estimatedCostUsd,
            providerKey,
            provenance,
            false);
    }

    public static string NormalizeRelativePath(string value)
    {
        var normalized = value.Trim().Replace('\\', '/');
        var supportedExtension = normalized.EndsWith(".forge", StringComparison.OrdinalIgnoreCase) ||
                                 normalized.EndsWith(".as", StringComparison.OrdinalIgnoreCase);
        if (Path.IsPathRooted(normalized) || normalized.Contains(':', StringComparison.Ordinal) ||
            normalized.Split('/').Any(segment => segment is "" or "." or "..") ||
            !normalized.StartsWith("scripts/", StringComparison.Ordinal) ||
            !supportedExtension)
            throw new ArgumentException(
                "ForgeScript path must be a confined scripts/*.forge legacy path or scripts/*.as canonical path.",
                nameof(value));
        return normalized;
    }

    public static string ResolvePath(string workspaceRoot, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var root = Path.GetFullPath(workspaceRoot);
        var fullPath = Path.GetFullPath(Path.Combine(root, NormalizeRelativePath(relativePath)
            .Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("ForgeScript path escapes the workspace.", nameof(relativePath));
        return fullPath;
    }

    public static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string BuildSource(AssistedForgeScriptBrief brief)
    {
        var message = Escape(brief.Message.Trim());
        var key = Escape(brief.VariableKey.Trim());
        var value = Escape(brief.VariableValue.Trim());
        return NormalizeSource($$"""
            void main()
            {
                say("{{message}}");
                variable("{{key}}", "{{value}}");
            }
            """);
    }

    private static string NormalizeSource(string source) =>
        source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim() + "\n";

    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\r", "", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal);

    private static void ValidateBrief(AssistedForgeScriptBrief brief)
    {
        if (brief.ScriptId == Guid.Empty || string.IsNullOrWhiteSpace(brief.EventBinding) ||
            string.IsNullOrWhiteSpace(brief.Intent) || string.IsNullOrWhiteSpace(brief.Message) ||
            string.IsNullOrWhiteSpace(brief.VariableKey) || string.IsNullOrWhiteSpace(brief.VariableValue) ||
            string.IsNullOrWhiteSpace(brief.AuthorId) || brief.ContextCitations.Count == 0 ||
            brief.StyleRules.Count == 0 || brief.ContextCitations.Count > 16 || brief.StyleRules.Count > 16)
            throw new ArgumentException("Assisted ForgeScript identity, binding, intent, source data, citations and style are required.");
        NormalizeRelativePath(brief.RelativePath);
    }
}
