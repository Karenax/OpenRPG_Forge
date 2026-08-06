namespace Forge.Domain.Factions;

public sealed record FactionDefinition(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Slug,
    string DisplayName,
    bool Archived,
    IReadOnlyList<Guid> ParentIds,
    IReadOnlyList<string> Tags,
    IReadOnlyDictionary<string, string>? Policies = null);

public sealed record FactionRelationship(
    Guid SourceId,
    Guid TargetId,
    double Score,
    string Explanation,
    string Preset);

public sealed record DispositionBand(string Id, double Minimum, double Maximum, int Priority);

/// <summary>Legacy single-membership entry retained for migration from the G8 catalog.</summary>
public sealed record FactionMember(string ActorId, Guid FactionId);

public sealed record FactionCatalog(
    IReadOnlyList<FactionDefinition> Factions,
    IReadOnlyList<FactionRelationship> Relationships,
    IReadOnlyList<DispositionBand> Bands,
    IReadOnlyList<FactionMember> Members,
    FactionAdvancedProfile? Advanced = null,
    bool RequireAdvancedRuntime = false)
{
    public static FactionCatalog Empty { get; } = new([], [], [], []);
}

public sealed record FactionDiagnostic(string Code, string Path, string Message);
