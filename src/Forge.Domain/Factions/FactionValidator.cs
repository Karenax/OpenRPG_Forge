namespace Forge.Domain.Factions;

public static class FactionValidator
{
    public const string AdvancedMissingCode = "faction.advanced.required_missing";

    public static IReadOnlyList<FactionDiagnostic> Validate(FactionCatalog catalog, string root = "module/factions")
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var diagnostics = new List<FactionDiagnostic>();
        var ids = catalog.Factions.Select(item => item.Id).ToHashSet();
        var slugs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var faction in catalog.Factions)
        {
            var path = $"{root}/definitions/{faction.Id}";
            if (faction.Id == Guid.Empty || faction.SchemaVersion != 1 ||
                string.IsNullOrWhiteSpace(faction.Provenance) || string.IsNullOrWhiteSpace(faction.Slug) ||
                string.IsNullOrWhiteSpace(faction.DisplayName))
                diagnostics.Add(new("RPG_FACTION_INVALID", path, "Faction identity is invalid."));
            if (!slugs.Add(faction.Slug))
                diagnostics.Add(new("RPG_FACTION_SLUG_DUPLICATE", $"{path}/slug", "Faction slugs must be unique."));
            foreach (var parent in faction.ParentIds.Where(parent => !ids.Contains(parent)))
                diagnostics.Add(new("RPG_FACTION_PARENT_ORPHAN", $"{path}/parents/{parent}",
                    "Parent faction does not exist."));
            if (HasCycle(faction.Id, catalog.Factions, new HashSet<Guid>()))
                diagnostics.Add(new("RPG_FACTION_INHERITANCE_CYCLE", $"{path}/parents",
                    "Faction inheritance contains a cycle."));
        }

        foreach (var relationship in catalog.Relationships)
            if (!ids.Contains(relationship.SourceId) || !ids.Contains(relationship.TargetId) ||
                !double.IsFinite(relationship.Score) || relationship.Score is < -100 or > 100)
                diagnostics.Add(new("RPG_FACTION_RELATION_INVALID",
                    $"{root}/relationships/{relationship.SourceId}/{relationship.TargetId}",
                    "Directional relationship is invalid."));
        foreach (var member in catalog.Members.Where(member => !ids.Contains(member.FactionId)))
            diagnostics.Add(new("RPG_FACTION_MEMBER_ORPHAN", $"{root}/members/{member.ActorId}",
                "Member faction does not exist."));
        for (var i = 0; i < catalog.Bands.Count; i++)
            for (var j = i + 1; j < catalog.Bands.Count; j++)
                if (catalog.Bands[i].Priority == catalog.Bands[j].Priority &&
                    catalog.Bands[i].Minimum <= catalog.Bands[j].Maximum &&
                    catalog.Bands[j].Minimum <= catalog.Bands[i].Maximum)
                    diagnostics.Add(new("RPG_FACTION_BANDS_OVERLAP", $"{root}/bands/{catalog.Bands[i].Id}",
                        "Disposition bands overlap at the same priority."));

        if (catalog.Advanced is null)
        {
            if (catalog.RequireAdvancedRuntime)
                diagnostics.Add(new(AdvancedMissingCode, $"{root}/advanced",
                    "The module requires the advanced faction profile at runtime."));
            return diagnostics;
        }

        ValidateAdvanced(catalog, catalog.Advanced, root, ids, diagnostics);
        return diagnostics;
    }

    public static string ResolveBand(
        FactionCatalog catalog, Guid sourceId, Guid targetId, double? overrideScore = null)
    {
        var score = overrideScore ?? catalog.Relationships
            .FirstOrDefault(item => item.SourceId == sourceId && item.TargetId == targetId)?.Score ?? 0;
        return catalog.Bands.Where(band => score >= band.Minimum && score <= band.Maximum)
            .OrderByDescending(band => band.Priority).ThenBy(band => band.Id, StringComparer.Ordinal)
            .FirstOrDefault()?.Id ?? "undefined";
    }

    private static void ValidateAdvanced(
        FactionCatalog catalog,
        FactionAdvancedProfile profile,
        string root,
        IReadOnlySet<Guid> factionIds,
        List<FactionDiagnostic> diagnostics)
    {
        var path = $"{root}/advanced";
        if (profile.Id == Guid.Empty || profile.SchemaVersion != 1 || string.IsNullOrWhiteSpace(profile.Provenance))
            diagnostics.Add(new("faction.advanced.identity_invalid", path,
                "Advanced faction profile identity is invalid."));
        if (!profile.ServerAuthoritative)
            diagnostics.Add(new("faction.advanced.authority_required", $"{path}/serverAuthoritative",
                "Advanced faction mutations must be server authoritative."));

        var uuids = new HashSet<Guid>();
        AddUuid(profile.Id, path, uuids, diagnostics);
        var inheritanceFactions = new HashSet<Guid>();
        foreach (var item in profile.Inheritance)
        {
            var itemPath = $"{path}/inheritance/{item.Id}";
            AddUuid(item.Id, itemPath, uuids, diagnostics);
            if (!factionIds.Contains(item.FactionId))
                diagnostics.Add(new("faction.inheritance.faction_missing", $"{itemPath}/factionId",
                    "Inheritance policy references an unknown faction."));
            if (!inheritanceFactions.Add(item.FactionId))
                diagnostics.Add(new("faction.inheritance.duplicate", itemPath,
                    "A faction can own only one inheritance policy."));
        }

        foreach (var item in profile.WitnessPolicies)
        {
            var itemPath = $"{path}/witnessPolicies/{item.Id}";
            AddUuid(item.Id, itemPath, uuids, diagnostics);
            if (!factionIds.Contains(item.FactionId))
                diagnostics.Add(new("faction.witness.faction_missing", $"{itemPath}/factionId",
                    "Witness policy references an unknown faction."));
            if (!double.IsFinite(item.MinimumPerception) || item.MinimumPerception is < 0 or > 1 ||
                !double.IsFinite(item.MinimumEvidence) || item.MinimumEvidence is < 0 or > 1 ||
                !double.IsFinite(item.CommunicationRadiusMeters) || item.CommunicationRadiusMeters is <= 0 or > 10000 ||
                item.MemorySeconds is < 1 or > 604800 || !double.IsFinite(item.ReputationDelta) ||
                item.ReputationDelta is < -100 or > 100)
                diagnostics.Add(new("faction.witness.policy_invalid", itemPath,
                    "Witness thresholds, memory, radius, or reputation delta are outside supported bounds."));
        }

        var primaryActors = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in profile.Memberships)
        {
            var itemPath = $"{path}/memberships/{item.Id}";
            AddUuid(item.Id, itemPath, uuids, diagnostics);
            if (item.SchemaVersion != 1 || string.IsNullOrWhiteSpace(item.Provenance) ||
                string.IsNullOrWhiteSpace(item.ActorId) || string.IsNullOrWhiteSpace(item.Rank) ||
                !factionIds.Contains(item.FactionId) || !double.IsFinite(item.Loyalty) ||
                item.Loyalty is < -100 or > 100 || !double.IsFinite(item.Weight) || item.Weight is < 0 or > 1)
                diagnostics.Add(new("faction.membership.invalid", itemPath,
                    "Faction membership identity, reference, rank, loyalty, or weight is invalid."));
            if (item.IsPrimary && !primaryActors.Add(item.ActorId))
                diagnostics.Add(new("faction.membership.primary_duplicate", $"{itemPath}/isPrimary",
                    "An actor can have only one primary faction membership."));
        }

        foreach (var item in profile.DynamicFactions)
        {
            var itemPath = $"{path}/dynamicFactions/{item.Id}";
            AddUuid(item.Id, itemPath, uuids, diagnostics);
            if (item.SchemaVersion != 1 || string.IsNullOrWhiteSpace(item.Provenance) ||
                !factionIds.Contains(item.FactionId) || item.Revision < 0 ||
                (item.ExpiresAt is { } expiry && expiry <= item.CreatedAt))
                diagnostics.Add(new("faction.dynamic.invalid", itemPath,
                    "Dynamic faction identity, reference, revision, or lifetime is invalid."));
            if (!item.Replicated)
                diagnostics.Add(new("faction.dynamic.replication_required", $"{itemPath}/replicated",
                    "Dynamic faction state must be replicated by the authoritative session."));
            foreach (var diplomacy in item.Diplomacy)
            {
                var diplomacyPath = $"{itemPath}/diplomacy/{diplomacy.Id}";
                AddUuid(diplomacy.Id, diplomacyPath, uuids, diagnostics);
                if (!factionIds.Contains(diplomacy.TargetFactionId) || diplomacy.TargetFactionId == item.FactionId ||
                    string.IsNullOrWhiteSpace(diplomacy.Cause) ||
                    (diplomacy.ExpiresAt is { } diplomacyExpiry && diplomacyExpiry <= diplomacy.Since))
                    diagnostics.Add(new("faction.dynamic.diplomacy_invalid", diplomacyPath,
                        "Dynamic diplomacy target, cause, or lifetime is invalid."));
            }
        }
    }

    private static void AddUuid(
        Guid id, string path, HashSet<Guid> uuids, List<FactionDiagnostic> diagnostics)
    {
        if (id == Guid.Empty)
            diagnostics.Add(new("faction.advanced.uuid_empty", path, "Advanced faction UUID cannot be empty."));
        else if (!uuids.Add(id))
            diagnostics.Add(new("faction.advanced.uuid_duplicate", path,
                "Advanced faction UUIDs must be unique within the profile."));
    }

    private static bool HasCycle(Guid id, IReadOnlyList<FactionDefinition> factions, HashSet<Guid> visiting)
    {
        if (!visiting.Add(id)) return true;
        var faction = factions.FirstOrDefault(item => item.Id == id);
        var cycle = faction is not null && faction.ParentIds.Any(parent => HasCycle(parent, factions, visiting));
        visiting.Remove(id);
        return cycle;
    }
}
