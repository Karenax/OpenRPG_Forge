using Forge.Domain.Factions;

namespace Forge.Application.Factions;

public sealed record FactionDefinitionHandle(Guid Id, int SchemaVersion);

public sealed record FactionFoundationScriptSnapshot(
    FactionDefinitionHandle Handle,
    string Provenance,
    string Slug,
    string DisplayName,
    bool Archived,
    int OutgoingRelationshipCount,
    int PersonalReputationCount);

/// <summary>Typed read boundary for the canonical P1 faction foundation.</summary>
public sealed class FactionFoundationScriptService
{
    private readonly FactionCatalog _catalog;

    public FactionFoundationScriptService(FactionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var resolution = FactionRuntimeResolver.ResolveFoundation(catalog);
        if (resolution.Diagnostics.Count > 0)
            throw new ArgumentException(string.Join(';', resolution.Diagnostics.Select(item => item.Code)),
                nameof(catalog));
        if (resolution.Catalog is null)
            throw new ArgumentException("FACTION_FOUNDATION_MISSING", nameof(catalog));
        _catalog = catalog;
    }

    public FactionDefinitionHandle Handle(Guid factionId)
    {
        var faction = Find(factionId);
        return new(faction.Id, faction.SchemaVersion);
    }

    public FactionFoundationScriptSnapshot Read(FactionDefinitionHandle handle)
    {
        var faction = EnsureHandle(handle);
        return new(handle, faction.Provenance, faction.Slug, faction.DisplayName, faction.Archived,
            _catalog.Relationships.Count(item => item.SourceId == faction.Id),
            _catalog.EffectivePersonalReputation.Count(item =>
                item.SourceFactionId == faction.Id || item.TargetFactionId == faction.Id));
    }

    public FactionRelationship? Relationship(
        FactionDefinitionHandle source,
        FactionDefinitionHandle target)
    {
        EnsureHandle(source);
        EnsureHandle(target);
        return _catalog.Relationships.SingleOrDefault(item =>
            item.SourceId == source.Id && item.TargetId == target.Id);
    }

    public string Disposition(
        FactionDefinitionHandle source,
        FactionDefinitionHandle target,
        string? subjectId = null,
        DateTimeOffset? at = null)
    {
        EnsureHandle(source);
        EnsureHandle(target);
        var score = Relationship(source, target)?.Score ?? 0;
        if (!string.IsNullOrWhiteSpace(subjectId))
            score += _catalog.EffectivePersonalReputation.Where(item =>
                item.SubjectId == subjectId && item.SourceFactionId == source.Id &&
                item.TargetFactionId == target.Id &&
                (at is null || item.ExpiresAt is null || item.ExpiresAt > at))
                .Sum(item => item.Delta);
        return FactionValidator.ResolveBand(_catalog, source.Id, target.Id, Math.Clamp(score, -100, 100));
    }

    private FactionDefinition EnsureHandle(FactionDefinitionHandle handle)
    {
        var faction = Find(handle.Id);
        if (faction.SchemaVersion != handle.SchemaVersion)
            throw new KeyNotFoundException("FACTION_DEFINITION_HANDLE_INVALID");
        return faction;
    }

    private FactionDefinition Find(Guid factionId) =>
        _catalog.Factions.SingleOrDefault(item => item.Id == factionId)
        ?? throw new KeyNotFoundException("FACTION_DEFINITION_HANDLE_INVALID");
}
