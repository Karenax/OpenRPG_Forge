using Forge.RpgKit.Domain.Scripting;
using Forge.Scripting;

namespace Forge.RpgKit.Application.Scripting;

public sealed record ForgeScriptWorldApiHandle(Guid SourceId);

public sealed record ForgeScriptWorldApiResolution(
    bool Available,
    bool OptionalFallback,
    ForgeScriptWorldApiHandle? Handle,
    string? DiagnosticCode);

public sealed record ForgeScriptWorldApiRuntime(
    ForgeScriptSourceDocument Document,
    InventoryState Inventory,
    IReadOnlyDictionary<string, QuestState> Quests,
    IReadOnlyDictionary<Guid, DialogueSession> Dialogues,
    FactionState Factions);

public sealed class ForgeScriptWorldApiRuntimeAdapter
{
    public const string RequiredMissingCode = "FORGESCRIPT_WORLD_API_REQUIRED_MISSING";

    public ForgeScriptWorldApiResolution Resolve(ForgeScriptSourceDocument? document, bool requiredAtRuntime)
    {
        if (document is null)
            return requiredAtRuntime
                ? new(false, false, null, RequiredMissingCode)
                : new(false, true, null, null);
        return new(true, false, new(document.Id), null);
    }

    public ForgeScriptWorldApiRuntime Build(
        ForgeScriptWorldApiHandle handle,
        ForgeScriptSourceDocument document,
        InventoryState inventory,
        IReadOnlyDictionary<string, QuestState> quests,
        FactionState factions)
    {
        Require(handle, document);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(quests);
        ArgumentNullException.ThrowIfNull(factions);
        return new(document, inventory, quests, new Dictionary<Guid, DialogueSession>(), factions);
    }

    public (ForgeScriptWorldApiRuntime Runtime, InventoryResult Result) Transfer(
        ForgeScriptWorldApiHandle handle,
        ForgeScriptWorldApiRuntime runtime,
        InventoryTransaction transaction)
    {
        Require(handle, runtime.Document);
        var result = ScriptWorldService.Transfer(runtime.Inventory, transaction);
        return result.Committed
            ? (runtime with { Inventory = result.State }, result)
            : (runtime, result);
    }

    public ForgeScriptWorldApiRuntime TransitionQuest(
        ForgeScriptWorldApiHandle handle,
        ForgeScriptWorldApiRuntime runtime,
        string questId,
        QuestTransition transition)
    {
        Require(handle, runtime.Document);
        if (!runtime.Quests.TryGetValue(questId, out var quest))
            throw new InvalidOperationException("FORGESCRIPT_QUEST_HANDLE_STALE");
        var quests = runtime.Quests.ToDictionary(StringComparer.Ordinal);
        quests[questId] = ScriptWorldService.TransitionQuest(quest, transition);
        return runtime with { Quests = quests };
    }

    public (ForgeScriptWorldApiRuntime Runtime, DialogueSession Session) StartDialogue(
        ForgeScriptWorldApiHandle handle,
        ForgeScriptWorldApiRuntime runtime,
        DialogueRequest request)
    {
        Require(handle, runtime.Document);
        var session = ScriptWorldService.StartDialogue(request);
        var sessions = runtime.Dialogues.ToDictionary();
        if (!sessions.TryAdd(session.Id, session))
            throw new InvalidOperationException("FORGESCRIPT_DIALOGUE_SESSION_DUPLICATE");
        return (runtime with { Dialogues = sessions }, session);
    }

    public ForgeScriptWorldApiRuntime CompleteDialogue(
        ForgeScriptWorldApiHandle handle,
        ForgeScriptWorldApiRuntime runtime,
        Guid sessionId,
        string outcome)
    {
        Require(handle, runtime.Document);
        if (!runtime.Dialogues.TryGetValue(sessionId, out var session))
            throw new InvalidOperationException("FORGESCRIPT_DIALOGUE_HANDLE_STALE");
        var sessions = runtime.Dialogues.ToDictionary();
        sessions[sessionId] = ScriptWorldService.CompleteDialogue(session, outcome);
        return runtime with { Dialogues = sessions };
    }

    public ForgeScriptWorldApiRuntime ApplyFaction(
        ForgeScriptWorldApiHandle handle,
        ForgeScriptWorldApiRuntime runtime,
        FactionTransaction transaction)
    {
        Require(handle, runtime.Document);
        return runtime with { Factions = ScriptWorldService.ApplyFaction(runtime.Factions, transaction) };
    }

    public PerceptionResult Perceive(
        ForgeScriptWorldApiHandle handle,
        ForgeScriptWorldApiRuntime runtime,
        PerceptionQuery query)
    {
        Require(handle, runtime.Document);
        return ScriptWorldService.Perceive(query);
    }

    public ForgeScriptExecutionResult Execute(
        ForgeScriptWorldApiHandle handle,
        ForgeScriptWorldApiRuntime runtime,
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

    private static void Require(ForgeScriptWorldApiHandle handle, ForgeScriptSourceDocument document)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(document);
        if (handle.SourceId == Guid.Empty || handle.SourceId != document.Id)
            throw new InvalidOperationException("FORGESCRIPT_WORLD_API_HANDLE_STALE");
    }
}
