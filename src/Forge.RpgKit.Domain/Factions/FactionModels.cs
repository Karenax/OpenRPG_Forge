namespace Forge.RpgKit.Domain.Factions;

public sealed record FactionDefinition(Guid Id, int SchemaVersion, string Provenance, string Slug, string DisplayName, bool Archived, IReadOnlyList<Guid> ParentIds, IReadOnlySet<string> Tags);
public sealed record FactionRelationship(Guid SourceId, Guid TargetId, double Score, string Explanation, string Preset);
public sealed record DispositionBand(string Id, double Minimum, double Maximum, int Priority);
public sealed record FactionMember(string ActorId, Guid FactionId);
public sealed record FactionCatalog(IReadOnlyList<FactionDefinition> Factions, IReadOnlyList<FactionRelationship> Relationships, IReadOnlyList<DispositionBand> Bands, IReadOnlyList<FactionMember> Members);
public sealed record FactionDiagnostic(string Code, string Path, string Message);

public static class FactionValidator
{
    public static IReadOnlyList<FactionDiagnostic> Validate(FactionCatalog catalog)
    {
        var diagnostics = new List<FactionDiagnostic>(); var ids = catalog.Factions.Select(item => item.Id).ToHashSet();
        foreach (var faction in catalog.Factions)
        {
            var root = $"factions/{faction.Id}";
            if (faction.Id == Guid.Empty || faction.SchemaVersion != 1 || string.IsNullOrWhiteSpace(faction.Provenance) || string.IsNullOrWhiteSpace(faction.Slug)) diagnostics.Add(new("RPG_FACTION_INVALID", root, "Faction identity is invalid."));
            foreach (var parent in faction.ParentIds.Where(parent => !ids.Contains(parent))) diagnostics.Add(new("RPG_FACTION_PARENT_ORPHAN", $"{root}/parents/{parent}", "Parent faction does not exist."));
            if (HasCycle(faction.Id, catalog.Factions, new HashSet<Guid>())) diagnostics.Add(new("RPG_FACTION_INHERITANCE_CYCLE", $"{root}/parents", "Faction inheritance contains a cycle."));
        }
        foreach (var relationship in catalog.Relationships)
            if (!ids.Contains(relationship.SourceId) || !ids.Contains(relationship.TargetId) || relationship.Score is < -100 or > 100) diagnostics.Add(new("RPG_FACTION_RELATION_INVALID", $"factions/relations/{relationship.SourceId}/{relationship.TargetId}", "Directional relationship is invalid."));
        foreach (var member in catalog.Members.Where(member => !ids.Contains(member.FactionId))) diagnostics.Add(new("RPG_FACTION_MEMBER_ORPHAN", $"factions/members/{member.ActorId}", "Member faction does not exist."));
        for (var i = 0; i < catalog.Bands.Count; i++)
            for (var j = i + 1; j < catalog.Bands.Count; j++)
                if (catalog.Bands[i].Priority == catalog.Bands[j].Priority && catalog.Bands[i].Minimum <= catalog.Bands[j].Maximum && catalog.Bands[j].Minimum <= catalog.Bands[i].Maximum) diagnostics.Add(new("RPG_FACTION_BANDS_OVERLAP", $"factions/bands/{catalog.Bands[i].Id}", "Disposition bands overlap at the same priority."));
        return diagnostics;
    }

    public static string ResolveBand(FactionCatalog catalog, Guid sourceId, Guid targetId, double? overrideScore = null)
    {
        var score = overrideScore ?? catalog.Relationships.FirstOrDefault(item => item.SourceId == sourceId && item.TargetId == targetId)?.Score ?? 0;
        return catalog.Bands.Where(band => score >= band.Minimum && score <= band.Maximum).OrderByDescending(band => band.Priority).ThenBy(band => band.Id, StringComparer.Ordinal).FirstOrDefault()?.Id ?? "undefined";
    }

    private static bool HasCycle(Guid id, IReadOnlyList<FactionDefinition> factions, HashSet<Guid> visiting)
    {
        if (!visiting.Add(id)) return true;
        var faction = factions.FirstOrDefault(item => item.Id == id);
        var cycle = faction is not null && faction.ParentIds.Any(parent => HasCycle(parent, factions, visiting));
        visiting.Remove(id); return cycle;
    }
}
