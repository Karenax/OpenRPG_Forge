using Forge.Scripting;

namespace Forge.Scripting.Tests;

public sealed class ForgeScriptAuthoringServiceTests
{
    [Fact]
    public void Editor_provides_highlighting_completion_signature_docs_snippets_symbols_formatting_and_diagnostics()
    {
        var context = Context();
        var completion = ForgeScriptAuthoringService.Analyze("App", 3, context);
        var signature = ForgeScriptAuthoringService.Analyze("ApplyEffect(", 12, context);
        var document = ForgeScriptAuthoringService.Analyze("void main() { ApplyEffect(target, burning); }", 45,
            context);
        var invalid = ForgeScriptAuthoringService.Analyze("if {", 4, context);

        Assert.Equal("ApplyEffect", completion.Completions.Single().Label);
        Assert.Contains("EntityRef target", signature.SignatureHelp, StringComparison.Ordinal);
        Assert.Contains(document.DocumentSymbols, item => item.Name == "ApplyEffect");
        Assert.Contains(document.SyntaxSpans, item => item.Kind == "keyword");
        Assert.EndsWith(Environment.NewLine, signature.FormattedSource, StringComparison.Ordinal);
        Assert.Contains(invalid.Diagnostics, item => item.Source == "script.editor.braces_unbalanced");
    }

    [Fact]
    public void Visual_builder_produces_typed_reviewable_wizard_source()
    {
        var target = ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Variable, "target", "EntityRef");
        var effect = ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Literal, "\"burning\"", "string");
        var call = ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Call, "ApplyEffect", "void",
            target, effect);
        var condition = ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Compare, ">", "bool",
            ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Variable, "health", "int"),
            ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Literal, "0", "int"));
        var result = ForgeScriptAuthoringService.BuildWizard(new(Guid.NewGuid(), 1, "test:wizard",
            "ApplyBurning", [condition], [call]));

        Assert.True(result.Succeeded);
        Assert.StartsWith("visual-builder:Sequence", result.Ast!.SourceDescription, StringComparison.Ordinal);
        Assert.Contains("bool ApplyBurning()", result.ReviewedSource, StringComparison.Ordinal);
        Assert.Contains("if (!((health > 0)))", result.ReviewedSource, StringComparison.Ordinal);
        Assert.Contains("ApplyEffect(target, \"burning\");", result.ReviewedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Wizard_rejects_untyped_conditions_and_missing_identity()
    {
        var invalidCondition = ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Literal, "1", "int");
        var action = ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Call, "say", "void",
            ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Literal, "\"hello\"", "string"));

        var result = ForgeScriptAuthoringService.BuildWizard(new(Guid.Empty, 2, "", "bad name",
            [invalidCondition], [action]));

        Assert.False(result.Succeeded);
        Assert.Equal(5, result.Diagnostics.Count);
        Assert.Contains(result.Diagnostics, item => item.Source == "script.wizard.condition_type_invalid");
    }

    [Fact]
    public void Deterministic_task_queue_orders_starts_completes_and_cancels_with_authority()
    {
        var at = DateTimeOffset.UnixEpoch;
        var queue = new ScriptTaskQueue([], 0);
        var low = Task(Guid.NewGuid(), 1, "move", at);
        var high = Task(Guid.NewGuid(), 5, "attack", at);

        queue = ForgeScriptAuthoringService.Enqueue(queue, low, ScriptNetworkAuthority.Server).Queue;
        queue = ForgeScriptAuthoringService.Enqueue(queue, high, ScriptNetworkAuthority.Server).Queue;
        Assert.Equal(high.Id, queue.Tasks[0].Id);

        var rejected = ForgeScriptAuthoringService.Enqueue(queue,
            low with { Id = Guid.NewGuid(), Authority = ScriptNetworkAuthority.Local },
            ScriptNetworkAuthority.Server);
        var duplicate = ForgeScriptAuthoringService.Enqueue(queue, low, ScriptNetworkAuthority.Server);
        Assert.False(rejected.Accepted);
        Assert.False(duplicate.Accepted);
        Assert.Same(queue, rejected.Queue);

        var started = ForgeScriptAuthoringService.StartNext(queue, at.AddSeconds(1));
        Assert.True(started.Accepted);
        Assert.Equal(ScriptTaskStatus.Running, started.Queue.Tasks.Single(item => item.Id == high.Id).Status);
        var completed = ForgeScriptAuthoringService.Complete(started.Queue, high.Id, "hit", at.AddSeconds(2));
        Assert.True(completed.Accepted);
        Assert.Equal("hit", completed.Queue.Tasks.Single(item => item.Id == high.Id).CompletionValue);
        var cancelled = ForgeScriptAuthoringService.Cancel(completed.Queue, low.Id, "entity", at.AddSeconds(3));
        Assert.True(cancelled.Accepted);
        Assert.Equal(ScriptTaskStatus.Cancelled,
            cancelled.Queue.Tasks.Single(item => item.Id == low.Id).Status);
    }

    [Fact]
    public void Queue_capacity_rejects_without_mutating_revision()
    {
        var queue = new ScriptTaskQueue([Task(Guid.NewGuid(), 1, "move", DateTimeOffset.UnixEpoch)], 7);
        var result = ForgeScriptAuthoringService.Enqueue(queue,
            Task(Guid.NewGuid(), 2, "attack", DateTimeOffset.UnixEpoch),
            ScriptNetworkAuthority.Server, maximumQueuedTasks: 1);

        Assert.False(result.Accepted);
        Assert.Same(queue, result.Queue);
        Assert.Equal(7, result.Queue.Revision);
    }

    private static ForgeScriptEditorContext Context()
    {
        var symbol = new ForgeScriptSymbol("ApplyEffect", "function", "void",
            "Applies a typed effect.",
            "void ApplyEffect(EntityRef target, EffectSpec effect)",
            ["rpg-kit", "server"]);
        return new([symbol], new Dictionary<string, string>
        {
            ["ifvalid"] = "if (entity.isValid) {\n}\n",
        }, new HashSet<string> { "if", "void" });
    }

    private static ScriptTask Task(Guid id, int priority, string action, DateTimeOffset at) =>
        new(id, "entity", priority, ScriptNetworkAuthority.Server, action,
            new Dictionary<string, object>(), ScriptTaskStatus.Queued, at, null, null);
}
