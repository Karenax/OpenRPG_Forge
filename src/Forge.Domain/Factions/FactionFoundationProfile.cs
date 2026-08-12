namespace Forge.Domain.Factions;

public enum PersonalReputationSubjectKind
{
    Actor,
    Player,
    Group,
}

public sealed record PersonalReputationOverlay(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string SubjectId,
    Guid SourceFactionId,
    Guid TargetFactionId,
    double Delta,
    DateTimeOffset? ExpiresAt,
    string Cause,
    PersonalReputationSubjectKind SubjectKind = PersonalReputationSubjectKind.Actor);

public sealed record FactionFoundationRuntimeResolution(
    FactionCatalog? Catalog,
    IReadOnlyList<FactionDiagnostic> Diagnostics,
    bool OptionalFallback);
