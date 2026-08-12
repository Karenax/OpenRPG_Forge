using System.Text.RegularExpressions;

namespace Forge.Scripting;

public enum ForgeScriptAstKind
{
    Literal,
    Variable,
    Call,
    Compare,
    Logical,
    Assignment,
    Sequence,
}

public enum ScriptTaskStatus
{
    Queued,
    Running,
    Completed,
    Cancelled,
    Failed,
}

public enum ScriptNetworkAuthority
{
    Local,
    Host,
    Server,
}

public sealed record ForgeScriptAst(
    ForgeScriptAstKind Kind,
    string Value,
    string TypeName,
    IReadOnlyList<ForgeScriptAst> Children,
    string SourceDescription);

public sealed record ForgeScriptSymbol(
    string Name,
    string Kind,
    string TypeName,
    string Documentation,
    string Signature,
    IReadOnlyList<string> Availability);

public sealed record ForgeScriptEditorContext(
    IReadOnlyList<ForgeScriptSymbol> Symbols,
    IReadOnlyDictionary<string, string> Snippets,
    IReadOnlySet<string> Keywords);

public sealed record ForgeScriptCompletion(
    string Label,
    string InsertText,
    string Detail,
    string Documentation);

public sealed record ForgeScriptSyntaxSpan(int Start, int Length, string Kind);

public sealed record ForgeScriptEditorAnalysis(
    IReadOnlyList<ForgeScriptCompletion> Completions,
    IReadOnlyList<ForgeScriptSymbol> DocumentSymbols,
    string FormattedSource,
    string? SignatureHelp,
    IReadOnlyList<ForgeScriptDiagnostic> Diagnostics,
    IReadOnlyList<ForgeScriptSyntaxSpan> SyntaxSpans);

public sealed record ForgeScriptWizardRequest(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string FunctionName,
    IReadOnlyList<ForgeScriptAst> Conditions,
    IReadOnlyList<ForgeScriptAst> Actions);

public sealed record ForgeScriptWizardResult(
    bool Succeeded,
    ForgeScriptAst? Ast,
    string ReviewedSource,
    IReadOnlyList<ForgeScriptDiagnostic> Diagnostics);

public sealed record ScriptTask(
    Guid Id,
    string OwnerId,
    int Priority,
    ScriptNetworkAuthority Authority,
    string Action,
    IReadOnlyDictionary<string, object> Arguments,
    ScriptTaskStatus Status,
    DateTimeOffset QueuedAt,
    DateTimeOffset? CompletedAt,
    string? CompletionValue);

public sealed record ScriptTaskQueue(IReadOnlyList<ScriptTask> Tasks, long Revision);

public sealed record ScriptTaskResult(
    bool Accepted,
    ScriptTaskQueue Queue,
    string Reason,
    string SemanticDiff);

public static partial class ForgeScriptAuthoringService
{
    public static ForgeScriptAst Build(
        ForgeScriptAstKind kind,
        string value,
        string typeName,
        params ForgeScriptAst[] children)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(typeName);
        ArgumentNullException.ThrowIfNull(children);
        return new(kind, value.Trim(), typeName.Trim(), children,
            $"visual-builder:{kind}:{value.Trim()}:{typeName.Trim()}");
    }

    public static string GenerateReviewedSource(ForgeScriptAst ast)
    {
        ArgumentNullException.ThrowIfNull(ast);
        return ast.Kind switch
        {
            ForgeScriptAstKind.Literal or ForgeScriptAstKind.Variable => ast.Value,
            ForgeScriptAstKind.Call => $"{ast.Value}({string.Join(", ", ast.Children.Select(GenerateReviewedSource))})",
            ForgeScriptAstKind.Compare =>
                $"({GenerateReviewedSource(ast.Children[0])} {ast.Value} {GenerateReviewedSource(ast.Children[1])})",
            ForgeScriptAstKind.Logical =>
                $"({string.Join($" {ast.Value} ", ast.Children.Select(GenerateReviewedSource))})",
            ForgeScriptAstKind.Assignment => $"{ast.Value} = {GenerateReviewedSource(ast.Children.Single())};",
            ForgeScriptAstKind.Sequence =>
                string.Join(Environment.NewLine, ast.Children.Select(GenerateReviewedSource)),
            _ => throw new ArgumentOutOfRangeException(nameof(ast)),
        };
    }

    public static ForgeScriptWizardResult BuildWizard(ForgeScriptWizardRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var diagnostics = new List<ForgeScriptDiagnostic>();
        if (request.Id == Guid.Empty)
            diagnostics.Add(Diagnostic("script.wizard.uuid_required", "A stable wizard UUID is required."));
        if (request.SchemaVersion != 1)
            diagnostics.Add(Diagnostic("script.wizard.schema_unsupported", "Script Wizard schema v1 is required."));
        if (string.IsNullOrWhiteSpace(request.Provenance))
            diagnostics.Add(Diagnostic("script.wizard.provenance_required", "Wizard provenance is required."));
        if (!IdentifierPattern().IsMatch(request.FunctionName ?? string.Empty))
            diagnostics.Add(Diagnostic("script.wizard.function_invalid", "A valid generated function name is required."));
        if (request.Conditions.Any(item => !string.Equals(item.TypeName, "bool", StringComparison.Ordinal)))
            diagnostics.Add(Diagnostic("script.wizard.condition_type_invalid", "Every condition must be typed bool."));
        if (request.Actions.Count == 0 || request.Actions.Any(item => item.Kind is not
                (ForgeScriptAstKind.Call or ForgeScriptAstKind.Assignment)))
            diagnostics.Add(Diagnostic("script.wizard.action_invalid", "At least one call or assignment action is required."));
        if (diagnostics.Count > 0) return new(false, null, string.Empty, diagnostics);

        var condition = request.Conditions.Count == 0
            ? "true"
            : string.Join(" && ", request.Conditions.Select(GenerateReviewedSource));
        var actions = request.Actions.Select(item =>
        {
            var source = GenerateReviewedSource(item);
            return source.EndsWith(';') ? source : source + ";";
        });
        var reviewed = $"bool {request.FunctionName}()\n{{\n" +
                       $"    if (!({condition})) return false;\n" +
                       string.Join("\n", actions.Select(item => $"    {item}")) +
                       "\n    return true;\n}";
        var ast = Build(ForgeScriptAstKind.Sequence, request.FunctionName!, "bool",
            [.. request.Conditions, .. request.Actions]);
        return new(true, ast, reviewed, []);
    }

    public static ForgeScriptEditorAnalysis Analyze(
        string source,
        int cursor,
        ForgeScriptEditorContext context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        if (cursor < 0 || cursor > source.Length) throw new ArgumentOutOfRangeException(nameof(cursor));

        var prefix = IdentifierPrefix(source, cursor);
        var completions = context.Symbols
            .Where(item => item.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(item => new ForgeScriptCompletion(item.Name,
                item.Kind == "function" ? item.Name + "(" : item.Name,
                item.Signature, item.Documentation))
            .Concat(context.Snippets
                .Where(item => item.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(item => new ForgeScriptCompletion(item.Key, item.Value, "snippet", item.Key)))
            .OrderBy(item => item.Label, StringComparer.Ordinal)
            .ToArray();
        var documentSymbols = context.Symbols
            .Where(item => Regex.IsMatch(source, $@"\b{Regex.Escape(item.Name)}\b", RegexOptions.CultureInvariant))
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .ToArray();
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.TrimEnd())
            .ToArray();
        var formatted = string.Join(Environment.NewLine, lines) + (source.Length == 0 ? "" : Environment.NewLine);
        var open = source.LastIndexOf('(', Math.Max(0, cursor - 1));
        var signature = open >= 0
            ? context.Symbols.FirstOrDefault(item =>
                source[..open].TrimEnd().EndsWith(item.Name, StringComparison.Ordinal))?.Signature
            : null;
        var diagnostics = new List<ForgeScriptDiagnostic>();
        if (source.Count(character => character == '{') != source.Count(character => character == '}'))
            diagnostics.Add(Diagnostic("script.editor.braces_unbalanced", "Unbalanced braces."));
        var syntaxSpans = SyntaxSpans(source, context).ToArray();
        return new(completions, documentSymbols, formatted, signature, diagnostics, syntaxSpans);
    }

    public static ScriptTaskResult Enqueue(
        ScriptTaskQueue queue,
        ScriptTask task,
        ScriptNetworkAuthority requiredAuthority,
        int maximumQueuedTasks = 256)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(task);
        if (task.Id == Guid.Empty || string.IsNullOrWhiteSpace(task.OwnerId) ||
            string.IsNullOrWhiteSpace(task.Action) || task.Priority is < 0 or > 10_000 ||
            task.Authority != requiredAuthority || task.Status != ScriptTaskStatus.Queued ||
            queue.Tasks.Any(item => item.Id == task.Id) ||
            queue.Tasks.Count(item => item.Status == ScriptTaskStatus.Queued) >= maximumQueuedTasks)
            return new(false, queue, "invalid-duplicate-full-or-unauthorized", "no-change");

        var tasks = queue.Tasks.Append(task)
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.QueuedAt)
            .ThenBy(item => item.Id)
            .ToArray();
        return new(true, new(tasks, checked(queue.Revision + 1)), "queued",
            $"enqueue-task:{task.Id}|priority:{task.Priority}|authority:{task.Authority}");
    }

    public static ScriptTaskResult StartNext(ScriptTaskQueue queue, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(queue);
        var task = queue.Tasks.FirstOrDefault(item => item.Status == ScriptTaskStatus.Queued);
        if (task is null) return new(false, queue, "queue-empty", "no-change");
        var tasks = queue.Tasks.ToArray();
        var index = Array.IndexOf(tasks, task);
        tasks[index] = task with { Status = ScriptTaskStatus.Running };
        return new(true, new(tasks, checked(queue.Revision + 1)), "started",
            $"start-task:{task.Id}|at:{at:O}");
    }

    public static ScriptTaskResult Cancel(
        ScriptTaskQueue queue,
        Guid taskId,
        string actorId,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(queue);
        var index = queue.Tasks.ToList().FindIndex(item => item.Id == taskId);
        if (index < 0 || queue.Tasks[index].OwnerId != actorId ||
            queue.Tasks[index].Status is ScriptTaskStatus.Completed or ScriptTaskStatus.Cancelled)
            return new(false, queue, "not-cancellable", "no-change");
        var tasks = queue.Tasks.ToArray();
        tasks[index] = tasks[index] with { Status = ScriptTaskStatus.Cancelled, CompletedAt = at };
        return new(true, new(tasks, checked(queue.Revision + 1)), "cancelled", $"cancel-task:{taskId}");
    }

    public static ScriptTaskResult Complete(
        ScriptTaskQueue queue,
        Guid taskId,
        string value,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(queue);
        var index = queue.Tasks.ToList().FindIndex(item => item.Id == taskId);
        if (index < 0 || queue.Tasks[index].Status != ScriptTaskStatus.Running)
            return new(false, queue, "not-running", "no-change");
        var tasks = queue.Tasks.ToArray();
        tasks[index] = tasks[index] with
        {
            Status = ScriptTaskStatus.Completed,
            CompletedAt = at,
            CompletionValue = value,
        };
        return new(true, new(tasks, checked(queue.Revision + 1)), "completed",
            $"complete-task:{taskId}|value:{value}");
    }

    private static IEnumerable<ForgeScriptSyntaxSpan> SyntaxSpans(
        string source,
        ForgeScriptEditorContext context)
    {
        foreach (var keyword in context.Keywords.Order(StringComparer.Ordinal))
        {
            foreach (Match match in Regex.Matches(source, $@"\b{Regex.Escape(keyword)}\b",
                         RegexOptions.CultureInvariant))
                yield return new(match.Index, match.Length, "keyword");
        }
        foreach (var symbol in context.Symbols.OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            foreach (Match match in Regex.Matches(source, $@"\b{Regex.Escape(symbol.Name)}\b",
                         RegexOptions.CultureInvariant))
                yield return new(match.Index, match.Length, symbol.Kind);
        }
    }

    private static string IdentifierPrefix(string source, int cursor)
    {
        var start = cursor;
        while (start > 0 && (char.IsLetterOrDigit(source[start - 1]) || source[start - 1] == '_')) start--;
        return source[start..cursor];
    }

    private static ForgeScriptDiagnostic Diagnostic(string code, string message) =>
        new(code, 1, 1, "error", message);

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();
}
