using Forge.Network.Operations;

namespace Forge.LiveDirector.Scripting;

public sealed class LiveScriptService(AuthorizationService authorization, Func<string, bool> signatureVerifier)
{
    public sealed record Command(string Name, Func<IReadOnlyDictionary<string, string>, IReadOnlyDictionary<string, string>> Execute);
    public sealed record Request(Guid CommandId, string Kind, string Source, string? Signature, int InstructionBudget, long ExpectedRevision, IReadOnlyDictionary<string, string> Arguments);
    public sealed record Result(bool Applied, string Code, long Revision,
        IReadOnlyDictionary<string, string> Output, string ServerAdapter, string AuditEntry);

    private readonly Dictionary<string, Command> _commands = new(StringComparer.Ordinal);
    private readonly HashSet<Guid> _processed = [];
    private long _revision;

    public void Register(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ArgumentException("LIVE_SCRIPT_COMMAND_INVALID", nameof(command));
        _commands.Add(command.Name, command);
    }

    public Result Run(AuthorizationSubject actor, Request request, bool apply)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        if (!authorization.Authorize(actor, NetworkCapability.ManageWorld).Allowed) return Reject("LIVE_SCRIPT_PERMISSION_DENIED", request);
        if (request.ExpectedRevision != _revision) return Reject("LIVE_SCRIPT_REVISION_STALE", request);
        if (_processed.Contains(request.CommandId)) return Reject("LIVE_SCRIPT_COMMAND_REPLAYED", request);
        if (request.InstructionBudget is < 1 or > 10_000) return Reject("LIVE_SCRIPT_BUDGET_INVALID", request);
        if (string.IsNullOrWhiteSpace(request.Source)) return Reject("LIVE_SCRIPT_SOURCE_INVALID", request);
        if (Forbidden(request.Source))
            return Reject("LIVE_SCRIPT_FORBIDDEN_API", request);
        Command? command = null;
        if (request.Kind == "registered-command" && !_commands.TryGetValue(request.Source, out command)) return Reject("LIVE_SCRIPT_COMMAND_UNKNOWN", request);
        if (request.Kind == "signed-snippet" && (request.Signature is null || !signatureVerifier($"{request.Source}:{request.Signature}"))) return Reject("LIVE_SCRIPT_SIGNATURE_INVALID", request);
        if (request.Kind is not ("registered-command" or "signed-snippet")) return Reject("LIVE_SCRIPT_KIND_INVALID", request);
        var output = request.Kind == "registered-command" ? command!.Execute(request.Arguments) : new Dictionary<string, string> { ["sandbox"] = "validated", ["instructions"] = request.InstructionBudget.ToString() };
        var adapter = $"forgescript:server:{request.Kind}:budget={request.InstructionBudget}";
        if (!apply) return new(false, "LIVE_SCRIPT_PREVIEW", _revision, output, adapter,
            $"previewed:script={request.Source}:actor={actor.AccountId}");
        _processed.Add(request.CommandId);
        _revision++;
        return new(true, "LIVE_SCRIPT_APPLIED", _revision, output, adapter,
            $"applied:script={request.Source}:actor={actor.AccountId}:revision={_revision}");
    }

    private static bool Forbidden(string source) => new[]
    {
        "shell", "process", "system.io", "filesystem", "network", "socket", "thread",
        "native", "dllimport"
    }.Any(value => source.Contains(value, StringComparison.OrdinalIgnoreCase));

    private Result Reject(string code, Request request) => new(false, code, _revision,
        new Dictionary<string, string>(), "forgescript:server:rejected",
        $"rejected:{code}:command={request.CommandId}");
}
