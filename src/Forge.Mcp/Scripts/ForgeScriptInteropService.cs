using Forge.Scripting;

namespace Forge.Mcp.Scripts;

public sealed record ScriptSourceDocument(string Path, string Source, long Revision, string Provenance);
public sealed record ScriptSourceTransaction(Guid TransactionId, long BaseRevision, string Path, string Source, string Provenance);
public sealed record ScriptInteropResult(bool Succeeded, long Revision, ScriptSourceDocument? Document, string SemanticDiff, IReadOnlyList<AgentDiagnostic> Diagnostics);
public sealed record ScriptBindingPreview(string EntityId, string Event, string ScriptPath, long Revision, bool Resolved, string SemanticDiff);
public sealed record ScriptInteropTestResult(bool Succeeded, string ScriptPath, string Status, IReadOnlyList<ForgeScriptEventResponse> Events, IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed class ForgeScriptInteropService
{
    private readonly ForgeScriptAgentService _agent;
    private readonly Dictionary<string, ScriptSourceDocument> _sources = new(StringComparer.Ordinal);
    private long _revision;

    public ForgeScriptInteropService(IForgeScriptRuntime runtime) => _agent = new(runtime);
    public long Revision => _revision;
    public ForgeScriptApiDescription GetApi() => _agent.GetApi();
    public IReadOnlyList<ScriptSourceDocument> GetSources() => _sources.Values.OrderBy(item => item.Path, StringComparer.Ordinal).ToArray();

    public ScriptInteropResult CreateOrUpdateSource(ScriptSourceTransaction transaction)
    {
        if (transaction.TransactionId == Guid.Empty || transaction.BaseRevision != _revision || string.IsNullOrWhiteSpace(transaction.Path) || !transaction.Path.EndsWith(".as", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(transaction.Provenance)) return new(false, _revision, null, "no-change", [new("script.transaction_invalid", "Transaction, revision, .as path and provenance are required.")]);
        var before = _sources.GetValueOrDefault(transaction.Path); var compile = _agent.Compile(transaction.Source); if (!compile.Succeeded) return new(false, _revision, before, "no-change", compile.Diagnostics); _revision = checked(_revision + 1); var document = new ScriptSourceDocument(transaction.Path, transaction.Source, _revision, transaction.Provenance); _sources[transaction.Path] = document; return new(true, _revision, document, $"source:{transaction.Path}|revision:{_revision}|before:{before?.Revision ?? 0}", []);
    }

    public ForgeScriptCompileResponse Compile(string path) => _sources.TryGetValue(path, out var document) ? _agent.Compile(document.Source) : new(false, 0, "unknown", "unknown", [new("script.source_missing", path)]);
    public IReadOnlyList<AgentDiagnostic> GetDiagnostics(string path) => Compile(path).Diagnostics;
    public ScriptInteropTestResult RunTests(string path, ForgeScriptQuotas? quotas = null)
    {
        if (!_sources.TryGetValue(path, out var document)) return new(false, path, "source_missing", [], [new("script.source_missing", path)]); var response = _agent.TestInSandbox(document.Source, quotas); return new(response.Succeeded, path, response.Status, response.Events, response.Diagnostics);
    }
    public ScriptBindingPreview PreviewBinding(string entityId, string @event, string scriptPath)
    {
        var resolved = !string.IsNullOrWhiteSpace(entityId) && !string.IsNullOrWhiteSpace(@event) && _sources.ContainsKey(scriptPath); return new(entityId, @event, scriptPath, _revision, resolved, resolved ? $"bind:{entityId}.{@event}->{scriptPath}" : "no-change");
    }
}
