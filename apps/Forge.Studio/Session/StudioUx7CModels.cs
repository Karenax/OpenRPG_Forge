namespace Forge.Studio.Session;

internal sealed record StudioUx7CSuiteSelection(
    Guid ProjectId,
    string ModuleName,
    string SourceFingerprint,
    int ScriptCount,
    int DialogueCount,
    int QuestCount,
    int AreaCount,
    int NetworkScenarioCount);

internal sealed record StudioUx7CTestStage(
    string Route,
    string Key,
    string Title,
    bool Completed,
    bool Passed,
    int Cases,
    int PassedCases,
    string Summary,
    IReadOnlyList<string> Details,
    string Fingerprint);

internal sealed record StudioUx7CRunReport(
    bool Succeeded,
    bool Cancelled,
    bool Isolated,
    bool SourceUntouched,
    bool TemporaryWorkspaceReleased,
    string Fingerprint,
    IReadOnlyList<StudioUx7CTestStage> Stages,
    string Message);

internal sealed record StudioUx7CSmokeResult(
    bool Succeeded,
    bool FiveRoutes,
    bool ExplicitSelection,
    bool ForgeScriptTests,
    bool DialogueTests,
    bool QuestTests,
    bool EncounterTests,
    bool NetworkTests,
    bool Cancellation,
    bool ResultNavigation,
    bool Deterministic,
    bool Isolated,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);
