namespace Forge.Studio.Session;

internal sealed record StudioUx7EGovernanceSelection(
    Guid ProjectId,
    string ModuleName,
    string SourceFingerprint,
    int HealthDomainCount,
    int EditorCheckCount,
    int RuntimeCheckCount,
    int FuzzBoundaryCount,
    int FixtureFamilyCount,
    int AuroraFeatureCount,
    int RequiredGateCount,
    int NetworkClientCount,
    int NetworkReplayPasses,
    int NetworkSoakCycles,
    int SoakDurationMinutes,
    int SoakVirtualPlayers);

internal sealed record StudioUx7EGovernanceStage(
    string Route,
    string Key,
    string Title,
    bool Completed,
    bool Passed,
    int Cases,
    int WarningCases,
    int FailedCases,
    string Summary,
    IReadOnlyList<string> Details,
    string Fingerprint);

internal sealed record StudioUx7EGovernanceRunReport(
    bool Succeeded,
    bool Cancelled,
    bool Isolated,
    bool ArtifactConfined,
    bool SourceUntouched,
    bool TemporaryWorkspaceReleased,
    string Fingerprint,
    IReadOnlyList<StudioUx7EGovernanceStage> Stages,
    string Message);

internal sealed record StudioUx7EGovernanceSmokeResult(
    bool Succeeded,
    bool NineRoutes,
    bool ExplicitSelection,
    bool ProjectHealth,
    bool EditorAccessibility,
    bool RuntimeAccessibility,
    bool SecurityFuzz,
    bool MaliciousFixtures,
    bool AuroraCoverage,
    bool AcceptanceGates,
    bool DeterministicNetwork,
    bool SoakTests,
    bool Cancellation,
    bool ResultNavigation,
    bool Deterministic,
    bool Isolated,
    bool ArtifactConfined,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);
