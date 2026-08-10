namespace Forge.Scripting;

public sealed record ForgeScriptSourceCommand(
    Guid TransactionId,
    long BaseRevision,
    ForgeScriptSourceDocument Document,
    string Reason);

public sealed record ForgeScriptSourceMutationResult(
    bool Succeeded,
    long Revision,
    ForgeScriptSourceDocument Document,
    string SemanticDiff,
    IReadOnlyList<ForgeScriptSourceDiagnostic> Diagnostics);

public sealed class ForgeScriptSourceHistory
{
    private readonly IForgeScriptRuntime _runtime;
    private readonly IReadOnlyList<ForgeScriptEventContract> _eventContracts;
    private readonly Stack<ForgeScriptSourceDocument> _undo = [];
    private readonly Stack<ForgeScriptSourceDocument> _redo = [];

    public ForgeScriptSourceHistory(ForgeScriptSourceDocument initial, IForgeScriptRuntime runtime,
        IReadOnlyList<ForgeScriptEventContract> eventContracts)
    {
        Document = initial ?? throw new ArgumentNullException(nameof(initial));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _eventContracts = eventContracts ?? throw new ArgumentNullException(nameof(eventContracts));
    }

    public ForgeScriptSourceDocument Document { get; private set; }
    public long Revision { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public ForgeScriptSourceMutationResult Preview(ForgeScriptSourceCommand command) => Validate(command, mutate: false);

    public ForgeScriptSourceMutationResult Apply(ForgeScriptSourceCommand command) => Validate(command, mutate: true);

    public ForgeScriptSourceMutationResult Undo()
    {
        if (_undo.Count == 0) return Failure("script.history.undo_empty", "No source mutation can be undone.");
        _redo.Push(Document);
        Document = _undo.Pop();
        Revision = checked(Revision + 1);
        return Success("undo-source");
    }

    public ForgeScriptSourceMutationResult Redo()
    {
        if (_redo.Count == 0) return Failure("script.history.redo_empty", "No source mutation can be redone.");
        _undo.Push(Document);
        Document = _redo.Pop();
        Revision = checked(Revision + 1);
        return Success("redo-source");
    }

    private ForgeScriptSourceMutationResult Validate(ForgeScriptSourceCommand command, bool mutate)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.TransactionId == Guid.Empty || command.BaseRevision != Revision ||
            string.IsNullOrWhiteSpace(command.Reason))
            return Failure("script.transaction.invalid", "Transaction identity, current revision and reason are required.");
        var diagnostics = ForgeScriptSourceValidator.Validate(command.Document, _eventContracts);
        if (diagnostics.Any(item => item.Severity == "error"))
            return new(false, Revision, Document, "no-change", diagnostics);
        var compilation = _runtime.Compile(command.Document.Source);
        if (!compilation.Success)
        {
            var compileDiagnostics = compilation.Diagnostics.Select(item => new ForgeScriptSourceDiagnostic(
                "script.compile.failed", command.Document.RelativePath, command.Document.Id, item.Severity,
                $"{item.Message} (line {item.Line}, column {item.Column})")).ToArray();
            return new(false, Revision, Document, "no-change", compileDiagnostics);
        }
        var semanticDiff = $"source:{Document.RelativePath}->{command.Document.RelativePath}|" +
                           $"bindings:{Document.EventBindings.Count}->{command.Document.EventBindings.Count}|" +
                           $"bytes:{System.Text.Encoding.UTF8.GetByteCount(Document.Source)}->" +
                           System.Text.Encoding.UTF8.GetByteCount(command.Document.Source);
        if (!mutate) return new(true, Revision, command.Document, semanticDiff, []);
        _undo.Push(Document);
        _redo.Clear();
        Document = command.Document;
        Revision = checked(Revision + 1);
        return new(true, Revision, Document, semanticDiff, []);
    }

    private ForgeScriptSourceMutationResult Success(string diff) => new(true, Revision, Document, diff, []);

    private ForgeScriptSourceMutationResult Failure(string code, string message) => new(false, Revision, Document,
        "no-change", [new(code, "script/history", Document.Id, "error", message)]);
}
