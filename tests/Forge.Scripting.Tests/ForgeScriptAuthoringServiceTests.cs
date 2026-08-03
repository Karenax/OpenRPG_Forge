using Forge.Scripting;

namespace Forge.Scripting.Tests;

public sealed class ForgeScriptAuthoringServiceTests
{
    [Fact]
    public void Editor_provides_completion_signature_docs_snippets_symbols_formatting_and_diagnostics()
    {
        var symbol = new ForgeScriptSymbol("ApplyEffect", "function", "void", "Applies a typed effect.", "void ApplyEffect(Entity target, EffectSpec effect)", ["rpg-kit", "server"]); var context = new ForgeScriptEditorContext([symbol], new Dictionary<string, string> { ["ifvalid"] = "if (entity.isValid) {\n}\n" }, new HashSet<string> { "if" });
        var completion = ForgeScriptAuthoringService.Analyze("App", 3, context); Assert.Equal("ApplyEffect", completion.Completions.Single().Label); Assert.Single(completion.DocumentSymbols);
        var signature = ForgeScriptAuthoringService.Analyze("ApplyEffect(", 12, context); Assert.Contains("Entity target", signature.SignatureHelp); Assert.EndsWith(Environment.NewLine, signature.FormattedSource);
        Assert.Contains(ForgeScriptAuthoringService.Analyze("if {", 4, context).Diagnostics, item => item.Message == "Unbalanced braces.");
    }

    [Fact]
    public void Visual_builder_round_trips_typed_ast_to_reviewable_source()
    {
        var target = ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Variable, "target", "Entity"); var effect = ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Variable, "burning", "EffectSpec"); var call = ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Call, "ApplyEffect", "void", target, effect); Assert.Equal("ApplyEffect(target, burning)", ForgeScriptAuthoringService.GenerateReviewedSource(call)); Assert.StartsWith("visual-builder:Call", call.SourceDescription);
    }

    [Fact]
    public void Deterministic_task_queue_orders_priority_checks_authority_cancels_and_completes()
    {
        var at = DateTimeOffset.UnixEpoch; var queue = new ScriptTaskQueue([], 0); var low = new ScriptTask(Guid.NewGuid(), "entity", 1, ScriptNetworkAuthority.Server, "move", new Dictionary<string, object>(), ScriptTaskStatus.Queued, at, null, null); var high = low with { Id = Guid.NewGuid(), Priority = 5, Action = "attack" };
        queue = ForgeScriptAuthoringService.Enqueue(queue, low, ScriptNetworkAuthority.Server).Queue; queue = ForgeScriptAuthoringService.Enqueue(queue, high, ScriptNetworkAuthority.Server).Queue; Assert.Equal(high.Id, queue.Tasks[0].Id);
        var rejected = ForgeScriptAuthoringService.Enqueue(queue, low with { Id = Guid.NewGuid(), Authority = ScriptNetworkAuthority.Local }, ScriptNetworkAuthority.Server); Assert.False(rejected.Accepted); Assert.Same(queue, rejected.Queue);
        var cancelled = ForgeScriptAuthoringService.Cancel(queue, low.Id, "entity", at.AddSeconds(1)); Assert.True(cancelled.Accepted); Assert.Equal(ScriptTaskStatus.Cancelled, cancelled.Queue.Tasks.Single(item => item.Id == low.Id).Status);
        var completed = ForgeScriptAuthoringService.Complete(queue, high.Id, "hit", at.AddSeconds(2)); Assert.True(completed.Accepted); Assert.Equal("hit", completed.Queue.Tasks.Single(item => item.Id == high.Id).CompletionValue);
    }
}
