namespace Forge.Scripting;

public enum ForgeScriptAstKind { Literal, Variable, Call, Compare, Logical, Assignment, Sequence }
public enum ScriptTaskStatus { Queued, Running, Completed, Cancelled, Failed }
public enum ScriptNetworkAuthority { Local, Host, Server }
public sealed record ForgeScriptAst(ForgeScriptAstKind Kind, string Value, string TypeName, IReadOnlyList<ForgeScriptAst> Children, string SourceDescription);
public sealed record ForgeScriptSymbol(string Name, string Kind, string TypeName, string Documentation, string Signature, IReadOnlyList<string> Availability);
public sealed record ForgeScriptEditorContext(IReadOnlyList<ForgeScriptSymbol> Symbols, IReadOnlyDictionary<string, string> Snippets, IReadOnlySet<string> Keywords);
public sealed record ForgeScriptCompletion(string Label, string InsertText, string Detail, string Documentation);
public sealed record ForgeScriptEditorAnalysis(IReadOnlyList<ForgeScriptCompletion> Completions, IReadOnlyList<ForgeScriptSymbol> DocumentSymbols, string FormattedSource, string? SignatureHelp, IReadOnlyList<ForgeScriptDiagnostic> Diagnostics);
public sealed record ScriptTask(Guid Id, string OwnerId, int Priority, ScriptNetworkAuthority Authority, string Action, IReadOnlyDictionary<string, object> Arguments, ScriptTaskStatus Status, DateTimeOffset QueuedAt, DateTimeOffset? CompletedAt, string? CompletionValue);
public sealed record ScriptTaskQueue(IReadOnlyList<ScriptTask> Tasks, long Revision);
public sealed record ScriptTaskResult(bool Accepted, ScriptTaskQueue Queue, string Reason, string SemanticDiff);

public static class ForgeScriptAuthoringService
{
    public static ForgeScriptAst Build(ForgeScriptAstKind kind, string value, string typeName, params ForgeScriptAst[] children) => new(kind, value.Trim(), typeName.Trim(), children, $"visual-builder:{kind}:{value}:{typeName}");
    public static string GenerateReviewedSource(ForgeScriptAst ast) => ast.Kind switch
    {
        ForgeScriptAstKind.Literal or ForgeScriptAstKind.Variable => ast.Value,
        ForgeScriptAstKind.Call => $"{ast.Value}({string.Join(", ", ast.Children.Select(GenerateReviewedSource))})",
        ForgeScriptAstKind.Compare => $"({GenerateReviewedSource(ast.Children[0])} {ast.Value} {GenerateReviewedSource(ast.Children[1])})",
        ForgeScriptAstKind.Logical => $"({string.Join($" {ast.Value} ", ast.Children.Select(GenerateReviewedSource))})",
        ForgeScriptAstKind.Assignment => $"{ast.Value} = {GenerateReviewedSource(ast.Children.Single())};",
        ForgeScriptAstKind.Sequence => string.Join(Environment.NewLine, ast.Children.Select(GenerateReviewedSource)),
        _ => throw new ArgumentOutOfRangeException(nameof(ast))
    };

    public static ForgeScriptEditorAnalysis Analyze(string source, int cursor, ForgeScriptEditorContext context)
    {
        if (cursor < 0 || cursor > source.Length) throw new ArgumentOutOfRangeException(nameof(cursor)); var prefix = IdentifierPrefix(source, cursor);
        var completions = context.Symbols.Where(item => item.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Select(item => new ForgeScriptCompletion(item.Name, item.Kind == "function" ? item.Name + "(" : item.Name, item.Signature, item.Documentation)).Concat(context.Snippets.Where(item => item.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Select(item => new ForgeScriptCompletion(item.Key, item.Value, "snippet", item.Key))).OrderBy(item => item.Label, StringComparer.Ordinal).ToArray();
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(line => line.TrimEnd()).ToArray(); var formatted = string.Join(Environment.NewLine, lines) + (source.Length == 0 ? "" : Environment.NewLine); var open = source.LastIndexOf('(', Math.Max(0, cursor - 1)); var signature = open >= 0 ? context.Symbols.FirstOrDefault(item => source[..open].TrimEnd().EndsWith(item.Name, StringComparison.Ordinal))?.Signature : null;
        var diagnostics = new List<ForgeScriptDiagnostic>(); if (source.Count(character => character == '{') != source.Count(character => character == '}')) diagnostics.Add(new("editor", 1, 1, "error", "Unbalanced braces."));
        return new(completions, context.Symbols, formatted, signature, diagnostics);
    }

    public static ScriptTaskResult Enqueue(ScriptTaskQueue queue, ScriptTask task, ScriptNetworkAuthority requiredAuthority)
    {
        if (task.Id == Guid.Empty || string.IsNullOrWhiteSpace(task.OwnerId) || string.IsNullOrWhiteSpace(task.Action) || task.Priority < 0 || task.Authority != requiredAuthority || task.Status != ScriptTaskStatus.Queued) return new(false, queue, "invalid-or-unauthorized", "no-change");
        var tasks = queue.Tasks.Append(task).OrderByDescending(item => item.Priority).ThenBy(item => item.QueuedAt).ThenBy(item => item.Id).ToArray(); return new(true, new(tasks, checked(queue.Revision + 1)), "queued", $"enqueue-task:{task.Id}|priority:{task.Priority}|authority:{task.Authority}");
    }
    public static ScriptTaskResult Cancel(ScriptTaskQueue queue, Guid taskId, string actorId, DateTimeOffset at)
    {
        var index = queue.Tasks.ToList().FindIndex(item => item.Id == taskId); if (index < 0 || queue.Tasks[index].OwnerId != actorId || queue.Tasks[index].Status is ScriptTaskStatus.Completed or ScriptTaskStatus.Cancelled) return new(false, queue, "not-cancellable", "no-change"); var tasks = queue.Tasks.ToArray(); tasks[index] = tasks[index] with { Status = ScriptTaskStatus.Cancelled, CompletedAt = at }; return new(true, new(tasks, checked(queue.Revision + 1)), "cancelled", $"cancel-task:{taskId}");
    }
    public static ScriptTaskResult Complete(ScriptTaskQueue queue, Guid taskId, string value, DateTimeOffset at)
    {
        var index = queue.Tasks.ToList().FindIndex(item => item.Id == taskId); if (index < 0 || queue.Tasks[index].Status is ScriptTaskStatus.Completed or ScriptTaskStatus.Cancelled) return new(false, queue, "not-completable", "no-change"); var tasks = queue.Tasks.ToArray(); tasks[index] = tasks[index] with { Status = ScriptTaskStatus.Completed, CompletedAt = at, CompletionValue = value }; return new(true, new(tasks, checked(queue.Revision + 1)), "completed", $"complete-task:{taskId}|value:{value}");
    }
    private static string IdentifierPrefix(string source, int cursor) { var start = cursor; while (start > 0 && (char.IsLetterOrDigit(source[start - 1]) || source[start - 1] == '_')) start--; return source[start..cursor]; }
}
