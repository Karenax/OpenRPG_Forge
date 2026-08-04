using Forge.Network.Operations;

namespace Forge.LiveDirector.Scripting;

public sealed class LiveScriptService(AuthorizationService authorization, Func<string, bool> signatureVerifier)
{
    public sealed record Command(string Name, Func<IReadOnlyDictionary<string, string>, IReadOnlyDictionary<string, string>> Execute);
    public sealed record Request(Guid CommandId, string Kind, string Source, string? Signature, int InstructionBudget, long ExpectedRevision, IReadOnlyDictionary<string, string> Arguments);
    public sealed record Result(bool Applied, string Code, long Revision, IReadOnlyDictionary<string, string> Output, string AuditEntry);

    private readonly Dictionary<string, Command> _commands = new(StringComparer.Ordinal);
    private readonly HashSet<Guid> _processed = [];
    private long _revision;

    public void Register(Command command) => _commands.Add(command.Name, command);

    public Result Run(AuthorizationSubject actor, Request request, bool apply)
    {
        if (!authorization.Authorize(actor, NetworkCapability.ManageWorld).Allowed) return Reject("LIVE_SCRIPT_PERMISSION_DENIED", request);
        if (request.ExpectedRevision != _revision) return Reject("LIVE_SCRIPT_REVISION_STALE", request);
        if (_processed.Contains(request.CommandId)) return Reject("LIVE_SCRIPT_COMMAND_REPLAYED", request);
        if (request.InstructionBudget is < 1 or > 10_000) return Reject("LIVE_SCRIPT_BUDGET_INVALID", request);
        if (request.Source.Contains("shell", StringComparison.OrdinalIgnoreCase) || request.Source.Contains("process", StringComparison.OrdinalIgnoreCase) || request.Source.Contains("System.IO", StringComparison.Ordinal))
            return Reject("LIVE_SCRIPT_FORBIDDEN_API", request);
        Command? command = null;
        if (request.Kind == "registered-command" && !_commands.TryGetValue(request.Source, out command)) return Reject("LIVE_SCRIPT_COMMAND_UNKNOWN", request);
        if (request.Kind == "signed-snippet" && (request.Signature is null || !signatureVerifier($"{request.Source}:{request.Signature}"))) return Reject("LIVE_SCRIPT_SIGNATURE_INVALID", request);
        if (request.Kind is not ("registered-command" or "signed-snippet")) return Reject("LIVE_SCRIPT_KIND_INVALID", request);
        var output = request.Kind == "registered-command" ? command!.Execute(request.Arguments) : new Dictionary<string, string> { ["sandbox"] = "validated", ["instructions"] = request.InstructionBudget.ToString() };
        if (!apply) return new(false, "LIVE_SCRIPT_PREVIEW", _revision, output, $"previewed:script={request.Source}:actor={actor.AccountId}");
        _processed.Add(request.CommandId);
        _revision++;
        return new(true, "LIVE_SCRIPT_APPLIED", _revision, output, $"applied:script={request.Source}:actor={actor.AccountId}:revision={_revision}");
    }

    private Result Reject(string code, Request request) => new(false, code, _revision, new Dictionary<string, string>(), $"rejected:{code}:command={request.CommandId}");
}
