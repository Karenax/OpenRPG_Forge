namespace Forge.Domain.Factions;

public enum DynamicFactionKind
{
    Temporary,
    Runtime,
}

public enum FactionDiplomacyStatus
{
    Neutral,
    Alliance,
    War,
    Truce,
}

public sealed record FactionAdvancedProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool ServerAuthoritative,
    IReadOnlyList<FactionInheritancePolicy> Inheritance,
    IReadOnlyList<FactionWitnessPolicy> WitnessPolicies,
    IReadOnlyList<FactionMembership> Memberships,
    IReadOnlyList<DynamicFactionDefinition> DynamicFactions);

public sealed record FactionInheritancePolicy(
    Guid Id,
    Guid FactionId,
    bool InheritTags,
    bool InheritPolicies,
    bool InheritRelationships);

public sealed record FactionWitnessPolicy(
    Guid Id,
    Guid FactionId,
    double MinimumPerception,
    double MinimumEvidence,
    double CommunicationRadiusMeters,
    int MemorySeconds,
    double ReputationDelta,
    bool ShareWithAllies);

public sealed record FactionMembership(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string ActorId,
    Guid FactionId,
    string Rank,
    string? CoverIdentity,
    double Loyalty,
    double Weight,
    bool IsPrimary,
    DateTimeOffset? ExpiresAt);

public sealed record DynamicFactionDefinition(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    Guid FactionId,
    DynamicFactionKind Kind,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    bool Active,
    bool Replicated,
    long Revision,
    IReadOnlyList<FactionDiplomacyState> Diplomacy);

public sealed record FactionDiplomacyState(
    Guid Id,
    Guid TargetFactionId,
    FactionDiplomacyStatus Status,
    DateTimeOffset Since,
    DateTimeOffset? ExpiresAt,
    string Cause);

public sealed record FactionEffectiveDefinition(
    Guid FactionId,
    IReadOnlySet<string> Tags,
    IReadOnlyDictionary<string, string> Policies,
    IReadOnlyList<Guid> InheritancePath);

public sealed record FactionWitnessObservation(
    Guid Id,
    string IncidentId,
    string SubjectActorId,
    string WitnessActorId,
    Guid WitnessFactionId,
    string IncidentTag,
    double Perception,
    double Evidence,
    double DistanceMeters,
    DateTimeOffset ObservedAt,
    bool CanCommunicate);

public sealed record FactionWitnessReport(
    Guid ObservationId,
    string IncidentId,
    string SubjectActorId,
    string WitnessActorId,
    Guid ReportingFactionId,
    double ReputationDelta,
    double Evidence,
    DateTimeOffset ReportedAt);

public sealed record FactionWitnessResult(
    IReadOnlyList<FactionWitnessReport> Reports,
    IReadOnlyList<FactionDiagnostic> Diagnostics);

public sealed record FactionRuntimeEvent(
    string Code,
    Guid FactionId,
    Guid? TargetFactionId,
    string Message,
    long Revision);

public sealed record FactionDynamicResult(
    FactionAdvancedProfile Profile,
    IReadOnlyList<FactionRuntimeEvent> Events,
    IReadOnlyList<FactionDiagnostic> Diagnostics);

public sealed record FactionRuntimeResolution(
    FactionAdvancedProfile? Profile,
    IReadOnlyList<FactionDiagnostic> Diagnostics,
    bool OptionalFallback);
