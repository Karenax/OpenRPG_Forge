namespace Forge.Studio.Session;

internal sealed record StudioUx6HChoice(
    string Key,
    string Label,
    string Detail);

internal sealed record StudioUx6HQuestChoice(
    string Key,
    string Label,
    IReadOnlyList<StudioUx6HChoice> States,
    IReadOnlyList<StudioUx6HChoice> Objectives);

internal sealed record StudioUx6HWorldCatalog(
    IReadOnlyList<StudioUx6HChoice> Items,
    IReadOnlyList<StudioUx6HChoice> Actors,
    IReadOnlyList<StudioUx6HQuestChoice> Quests,
    IReadOnlyList<StudioUx6HChoice> Dialogues,
    IReadOnlyList<StudioUx6HChoice> Factions)
{
    internal bool Complete => Items.Count > 0 && Actors.Count > 1 && Quests.Count > 0 &&
                              Dialogues.Count > 0 && Factions.Count > 0;
}

internal sealed record StudioUx6HWorldDraft(
    string InventoryOwner,
    string InventoryRecipient,
    string ItemId,
    int Quantity,
    string QuestId,
    string QuestScope,
    string QuestFromState,
    string QuestToState,
    string QuestObjective,
    int QuestDelta,
    string DialogueId,
    string DialogueSpeaker,
    string DialogueListener,
    string DialogueOutcome,
    string FactionActor,
    string FactionId,
    int ReputationDelta,
    bool JoinFaction,
    string FactionScope,
    string Observer,
    string Target,
    string Sense,
    double Distance,
    double Range,
    bool LineOfSight,
    double Signal,
    int Seed);

internal sealed record StudioUx6HSourcePlan(
    bool Succeeded,
    string Message,
    string ReviewedSource,
    string CandidateSource,
    IReadOnlyList<StudioUx6FDiagnostic> Diagnostics);

internal sealed record StudioUx6HWorldResult(
    bool Succeeded,
    string Message,
    bool OptionalFallbackSafe,
    bool RequiredMissingDiagnostic,
    bool InventoryCommitted,
    bool InventoryRollback,
    int InventoryQuantity,
    int InventoryCapacity,
    bool QuestTransitioned,
    int QuestProgress,
    bool DialogueStarted,
    bool DialogueCompleted,
    bool DialogueInterrupted,
    bool FactionJoined,
    int Reputation,
    bool Perceived,
    double Confidence,
    bool DeterministicPerception,
    bool StaleHandleBlocked,
    bool NativeSandboxExecuted,
    IReadOnlyList<string> NativeEvents,
    IReadOnlyList<string> Trace,
    IReadOnlyList<StudioUx6FDiagnostic> Diagnostics);

internal sealed record StudioUx6HSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool SharedSourceAuthority,
    bool HumanCatalogs,
    bool ReviewedSource,
    bool InventoryApi,
    bool QuestApi,
    bool DialogueApi,
    bool FactionApi,
    bool PerceptionApi,
    bool OptionalRequiredResolution,
    bool RollbackAndStaleHandle,
    bool NativeSandboxExecution,
    bool CanonicalDryRun,
    bool Compilation,
    bool SandboxExecution,
    bool StaleApprovalBlocked,
    bool ApplyUndoRedo,
    bool Persistence,
    bool ProjectNeighborsPreserved,
    bool TemporaryWorkspaceReleased);
