using Forge.RpgKit.Application.Scripting;
using Forge.RpgKit.Domain.Scripting;
using Forge.Scripting;

namespace Forge.RpgKit.Domain.Tests.Scripting;

public sealed class ForgeScriptP1RuntimeAdapterTests : IDisposable
{
    private readonly string _workspace = Path.Combine(Path.GetTempPath(), "openrpg-forge-r097",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Optional_missing_is_safe_required_missing_is_diagnostic_and_stale_handles_are_rejected()
    {
        var adapter = new ForgeScriptP1RuntimeAdapter();
        var document = Document();
        var resolution = adapter.Resolve(document, true);

        Assert.True(adapter.Resolve(null, false).OptionalFallback);
        Assert.Equal(ForgeScriptP1RuntimeAdapter.RequiredMissingCode,
            adapter.Resolve(null, true).DiagnosticCode);
        Assert.True(resolution.Available);
        Assert.Throws<InvalidOperationException>(() => adapter.Build(
            new(Guid.NewGuid()), document, Editor(), Combat()));
    }

    [Fact]
    public void Editor_and_wizard_use_the_same_canonical_source_handle()
    {
        var (adapter, handle, runtime) = Runtime();
        var analysis = adapter.Analyze(handle, runtime, runtime.Document.Source.IndexOf("say", StringComparison.Ordinal) + 3);
        var condition = ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Compare, ">", "bool",
            ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Variable, "health", "int"),
            ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Literal, "0", "int"));
        var action = ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Call, "say", "void",
            ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Literal, "\"alive\"", "string"));
        var wizard = adapter.BuildWizard(handle, runtime, new(Guid.NewGuid(), 1, "test:r097",
            "NotifyAlive", [condition], [action]));

        Assert.Contains(analysis.Completions, item => item.Label == "say");
        Assert.Contains(analysis.SyntaxSpans, item => item.Kind == "keyword");
        Assert.True(wizard.Succeeded);
        Assert.Contains("bool NotifyAlive()", wizard.ReviewedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Deferred_actions_combat_and_effects_share_authoritative_immutable_runtime_state()
    {
        var (adapter, handle, runtime) = Runtime();
        var at = DateTimeOffset.UnixEpoch;
        var task = new ScriptTask(Guid.NewGuid(), "hero", 50, ScriptNetworkAuthority.Server,
            "attack", new Dictionary<string, object> { ["target"] = "warden" },
            ScriptTaskStatus.Queued, at, null, null);
        runtime = adapter.Enqueue(handle, runtime, task, ScriptNetworkAuthority.Server);
        runtime = adapter.StartNext(handle, runtime, at.AddSeconds(1));
        runtime = adapter.Complete(handle, runtime, task.Id, "hit", at.AddSeconds(2));
        var attack = adapter.Attack(handle, runtime,
            new(Guid.NewGuid(), "hero", "warden", "ability:slash", 1, 0, 97, "server", "r097"),
            new(12, "slashing", new HashSet<string> { "melee" }, false, 0.5));
        var definition = new EffectDefinition("effect:burn", 1, "test:r097", "damage-over-time",
            EffectStacking.RefreshDuration, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2), 2,
            new HashSet<string> { "fire" }, true);
        var applied = adapter.ApplyEffect(handle, attack.Runtime, definition,
            new(Guid.NewGuid(), definition.Id, "hero", "warden", 1, null, at));

        Assert.Equal(ScriptTaskStatus.Completed,
            applied.Runtime.Tasks.Tasks.Single().Status);
        Assert.True(attack.Result.Committed);
        Assert.Equal(89, applied.Runtime.Combat.Health["warden"]);
        Assert.Single(applied.Runtime.Effects);
        Assert.Contains("source:hero", applied.Result.Events);
    }

    [Fact]
    public void Source_persistence_command_history_and_sandbox_execution_round_trip()
    {
        var (adapter, handle, runtime) = Runtime();
        var history = new ForgeScriptSourceHistory(runtime.Document, new NativeForgeScriptRuntime(), []);
        var changed = runtime.Document with { Source = "void main() { say(\"changed-r097\"); }" };
        var applied = history.Apply(new(Guid.NewGuid(), 0, changed, "r097-authoring"));
        var undone = history.Undo();
        var redone = history.Redo();
        var store = new ForgeScriptSourceStore(_workspace);
        store.Save(redone.Document);
        var restored = store.Load(redone.Document.RelativePath);
        var resolved = adapter.Resolve(restored, true).Handle!;
        var restoredRuntime = adapter.Build(resolved, restored, Editor(), Combat());
        var executed = adapter.Execute(resolved, restoredRuntime, new NativeForgeScriptRuntime(),
            new ForgeScriptQuotas(20_000, 1000, 32, 8));

        Assert.True(applied.Succeeded);
        Assert.Equal(runtime.Document.Source, undone.Document.Source);
        Assert.Equal(changed.Source, redone.Document.Source);
        Assert.Equal(changed.Id, restored.Id);
        Assert.Equal(changed.Source, restored.Source);
        Assert.Equal(changed.ApiVersion, restored.ApiVersion);
        Assert.Equal(changed.Dependencies, restored.Dependencies);
        Assert.Equal(changed.EventBindings, restored.EventBindings);
        Assert.Equal(ForgeScriptExecutionStatus.Completed, executed.Status);
        Assert.Contains(executed.Events, item => item.Kind == "say" && item.Value == "changed-r097");
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspace)) Directory.Delete(_workspace, true);
    }

    private static (ForgeScriptP1RuntimeAdapter Adapter, ForgeScriptP1Handle Handle,
        ForgeScriptP1Runtime Runtime) Runtime()
    {
        var adapter = new ForgeScriptP1RuntimeAdapter();
        var document = Document();
        var handle = adapter.Resolve(document, true).Handle!;
        return (adapter, handle, adapter.Build(handle, document, Editor(), Combat()));
    }

    private static ForgeScriptSourceDocument Document() =>
        new(Guid.Parse("c7970000-0000-4000-8000-000000000001"), 1, "test:r097",
            "scripts/r097.as", "void main() { say(\"r097\"); }", "1.0.0",
            ForgeScriptSourceDistributionPolicy.SourceAndBytecode, true, [], []);

    private static ForgeScriptEditorContext Editor() =>
        new([new("say", "function", "void", "Publie une replique.",
                "void say(const string &in text)", ["server", "client"])],
            new Dictionary<string, string> { ["main"] = "void main() {\n}\n" },
            new HashSet<string> { "void", "if", "return" });

    private static ScriptCombatState Combat() =>
        new(new Dictionary<string, double> { ["warden"] = 100 }, [], 0,
            new Dictionary<string, double> { ["warden"] = 2 });
}
