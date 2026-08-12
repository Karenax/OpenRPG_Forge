namespace Forge.Domain.Factions;

public enum ReputationScope
{
    World,
    Server,
    Campaign,
    Group,
    Player,
}

public sealed record ReputationTransaction(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    ReputationScope Scope,
    string ScopeId,
    Guid SourceFactionId,
    Guid TargetFactionId,
    double Delta,
    string Cause,
    DateTimeOffset OccurredAt);

public sealed record FactionHeatmapSnapshot(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    DateTimeOffset CapturedAt,
    string Label,
    IReadOnlyList<FactionRelationship> Relationships);

public sealed record FactionReactionPolicy(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    ReputationScope DefaultScope,
    string DefaultScopeId,
    bool ApplyDisguise,
    bool ApplyCrimes,
    bool ApplyDialogue,
    bool ApplyZoneRules,
    bool AllowGameMasterOverride);

public sealed record FactionReputationProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    IReadOnlyList<ReputationTransaction> Transactions,
    IReadOnlyList<FactionHeatmapSnapshot> HeatmapHistory,
    FactionReactionPolicy Reaction);

public sealed record FactionReputationRuntimeResolution(
    FactionReputationProfile? Profile,
    IReadOnlyList<FactionDiagnostic> Diagnostics,
    bool OptionalFallback);
