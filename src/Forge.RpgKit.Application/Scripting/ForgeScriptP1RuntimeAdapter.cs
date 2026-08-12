using Forge.RpgKit.Domain.Scripting;
using Forge.Scripting;

namespace Forge.RpgKit.Application.Scripting;

public sealed record ForgeScriptP1Handle(Guid SourceId);

public sealed record ForgeScriptP1Resolution(
    bool Available,
    bool OptionalFallback,
    ForgeScriptP1Handle? Handle,
    string? DiagnosticCode);

public sealed record ForgeScriptP1Runtime(
    ForgeScriptSourceDocument Document,
    ForgeScriptEditorContext Editor,
    ScriptTaskQueue Tasks,
    ScriptCombatState Combat,
    IReadOnlyList<ActiveEffect> Effects);

public sealed class ForgeScriptP1RuntimeAdapter
{
    public const string RequiredMissingCode = "FORGESCRIPT_P1_REQUIRED_MISSING";

    public ForgeScriptP1Resolution Resolve(ForgeScriptSourceDocument? document, bool requiredAtRuntime)
    {
        if (document is null)
            return requiredAtRuntime
                ? new(false, false, null, RequiredMissingCode)
                : new(false, true, null, null);
        return new(true, false, new(document.Id), null);
    }

    public ForgeScriptP1Runtime Build(
        ForgeScriptP1Handle handle,
        ForgeScriptSourceDocument document,
        ForgeScriptEditorContext editor,
        ScriptCombatState combat)
    {
        Require(handle, document);
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(combat);
        var contracts = document.EventBindings
            .GroupBy(item => item.EventName, StringComparer.Ordinal)
            .Select(group => new ForgeScriptEventContract(group.First().Id, 1, group.First().Provenance,
                group.Key, group.First().PayloadType, document.RequiredAtRuntime))
            .ToArray();
        var diagnostics = ForgeScriptSourceValidator.Validate(document, contracts);
        if (diagnostics.Any(item => item.Severity == "error"))
            throw new InvalidOperationException($"FORGESCRIPT_P1_DOCUMENT_INVALID:{diagnostics[0].Code}");
        return new(document, editor, new([], 0), combat, []);
    }

    public ForgeScriptEditorAnalysis Analyze(
        ForgeScriptP1Handle handle,
        ForgeScriptP1Runtime runtime,
        int cursor)
    {
        Require(handle, runtime.Document);
        return ForgeScriptAuthoringService.Analyze(runtime.Document.Source, cursor, runtime.Editor);
    }

    public ForgeScriptWizardResult BuildWizard(
        ForgeScriptP1Handle handle,
        ForgeScriptP1Runtime runtime,
        ForgeScriptWizardRequest request)
    {
        Require(handle, runtime.Document);
        return ForgeScriptAuthoringService.BuildWizard(request);
    }

    public ForgeScriptP1Runtime Enqueue(
        ForgeScriptP1Handle handle,
        ForgeScriptP1Runtime runtime,
        ScriptTask task,
        ScriptNetworkAuthority authority)
    {
        Require(handle, runtime.Document);
        var result = ForgeScriptAuthoringService.Enqueue(runtime.Tasks, task, authority);
        if (!result.Accepted) throw new InvalidOperationException($"FORGESCRIPT_TASK_REJECTED:{result.Reason}");
        return runtime with { Tasks = result.Queue };
    }

    public ForgeScriptP1Runtime StartNext(
        ForgeScriptP1Handle handle,
        ForgeScriptP1Runtime runtime,
        DateTimeOffset at)
    {
        Require(handle, runtime.Document);
        var result = ForgeScriptAuthoringService.StartNext(runtime.Tasks, at);
        if (!result.Accepted) throw new InvalidOperationException($"FORGESCRIPT_TASK_REJECTED:{result.Reason}");
        return runtime with { Tasks = result.Queue };
    }

    public ForgeScriptP1Runtime Complete(
        ForgeScriptP1Handle handle,
        ForgeScriptP1Runtime runtime,
        Guid taskId,
        string value,
        DateTimeOffset at)
    {
        Require(handle, runtime.Document);
        var result = ForgeScriptAuthoringService.Complete(runtime.Tasks, taskId, value, at);
        if (!result.Accepted) throw new InvalidOperationException($"FORGESCRIPT_TASK_REJECTED:{result.Reason}");
        return runtime with { Tasks = result.Queue };
    }

    public (ForgeScriptP1Runtime Runtime, ScriptCombatResult Result) Attack(
        ForgeScriptP1Handle handle,
        ForgeScriptP1Runtime runtime,
        AttackContext context,
        DamageSpec damage)
    {
        Require(handle, runtime.Document);
        var result = ScriptGameplayService.Attack(runtime.Combat, context, damage);
        if (!result.Committed) return (runtime, result);
        return (runtime with { Combat = result.State }, result);
    }

    public (ForgeScriptP1Runtime Runtime, EffectApplyResult Result) ApplyEffect(
        ForgeScriptP1Handle handle,
        ForgeScriptP1Runtime runtime,
        EffectDefinition definition,
        EffectSpec spec)
    {
        Require(handle, runtime.Document);
        var result = ScriptGameplayService.ApplyEffect(runtime.Effects, definition, spec);
        return (runtime with { Effects = result.Effects }, result);
    }

    public ForgeScriptExecutionResult Execute(
        ForgeScriptP1Handle handle,
        ForgeScriptP1Runtime runtime,
        IForgeScriptRuntime scriptRuntime,
        ForgeScriptQuotas? quotas = null)
    {
        Require(handle, runtime.Document);
        ArgumentNullException.ThrowIfNull(scriptRuntime);
        var compiled = scriptRuntime.Compile(runtime.Document.Source);
        if (!compiled.Success || compiled.Artifact is null)
            return new(ForgeScriptExecutionStatus.Failed, [], compiled.Diagnostics, null);
        return scriptRuntime.Execute(compiled.Artifact, quotas);
    }

    private static void Require(ForgeScriptP1Handle handle, ForgeScriptSourceDocument document)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(document);
        if (handle.SourceId == Guid.Empty || handle.SourceId != document.Id)
            throw new InvalidOperationException("FORGESCRIPT_P1_HANDLE_STALE");
    }
}
