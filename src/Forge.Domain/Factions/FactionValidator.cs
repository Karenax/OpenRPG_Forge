namespace Forge.Domain.Factions;

public static class FactionValidator
{
    public const string AdvancedMissingCode = "faction.advanced.required_missing";
    public const string FoundationMissingCode = "faction.foundation.required_missing";
    public const string ReputationMissingCode = "faction.reputation.required_missing";

    public static IReadOnlyList<FactionDiagnostic> Validate(FactionCatalog catalog, string root = "module/factions")
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var diagnostics = new List<FactionDiagnostic>();
        var ids = catalog.Factions.Select(item => item.Id).ToHashSet();
        var definitionIds = new HashSet<Guid>();
        var slugs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var faction in catalog.Factions)
        {
            var path = $"{root}/definitions/{faction.Id}";
            if (faction.Id == Guid.Empty || faction.SchemaVersion != 1 ||
                string.IsNullOrWhiteSpace(faction.Provenance) || string.IsNullOrWhiteSpace(faction.Slug) ||
                string.IsNullOrWhiteSpace(faction.DisplayName))
                diagnostics.Add(new("RPG_FACTION_INVALID", path, "Faction identity is invalid."));
            if (!definitionIds.Add(faction.Id))
                diagnostics.Add(new("RPG_FACTION_UUID_DUPLICATE", path, "Faction UUIDs must be unique."));
            if (!slugs.Add(faction.Slug))
                diagnostics.Add(new("RPG_FACTION_SLUG_DUPLICATE", $"{path}/slug", "Faction slugs must be unique."));
            var tags = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tag in faction.Tags)
            {
                var tagPath = $"{path}/tags/{tag}";
                if (string.IsNullOrWhiteSpace(tag) || tag != NormalizeTag(tag) ||
                    tag.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')))
                    diagnostics.Add(new("RPG_FACTION_TAG_INVALID", tagPath,
                        "Faction tags must be normalized semantic identifiers."));
                if (!tags.Add(tag))
                    diagnostics.Add(new("RPG_FACTION_TAG_DUPLICATE", tagPath,
                        "Faction tags must be unique within a definition."));
            }
            foreach (var parent in faction.ParentIds.Where(parent => !ids.Contains(parent)))
                diagnostics.Add(new("RPG_FACTION_PARENT_ORPHAN", $"{path}/parents/{parent}",
                    "Parent faction does not exist."));
            if (HasCycle(faction.Id, catalog.Factions, new HashSet<Guid>()))
                diagnostics.Add(new("RPG_FACTION_INHERITANCE_CYCLE", $"{path}/parents",
                    "Faction inheritance contains a cycle."));
        }

        var relationshipKeys = new HashSet<(Guid Source, Guid Target)>();
        foreach (var relationship in catalog.Relationships)
        {
            var path = $"{root}/relationships/{relationship.SourceId}/{relationship.TargetId}";
            if (!ids.Contains(relationship.SourceId) || !ids.Contains(relationship.TargetId) ||
                !double.IsFinite(relationship.Score) || relationship.Score is < -100 or > 100 ||
                string.IsNullOrWhiteSpace(relationship.Explanation) || string.IsNullOrWhiteSpace(relationship.Preset))
                diagnostics.Add(new("RPG_FACTION_RELATION_INVALID",
                    path,
                    "Directional relationship is invalid."));
            if (!relationshipKeys.Add((relationship.SourceId, relationship.TargetId)))
                diagnostics.Add(new("RPG_FACTION_RELATION_DUPLICATE", path,
                    "A directional relationship can be defined only once."));
        }
        foreach (var member in catalog.Members.Where(member => !ids.Contains(member.FactionId)))
            diagnostics.Add(new("RPG_FACTION_MEMBER_ORPHAN", $"{root}/members/{member.ActorId}",
                "Member faction does not exist."));
        var bandIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var band in catalog.Bands)
        {
            var path = $"{root}/bands/{band.Id}";
            if (string.IsNullOrWhiteSpace(band.Id) || !double.IsFinite(band.Minimum) ||
                !double.IsFinite(band.Maximum) || band.Minimum < -100 || band.Maximum > 100 ||
                band.Minimum > band.Maximum || band.Priority < 0)
                diagnostics.Add(new("RPG_FACTION_BAND_INVALID", path,
                    "Disposition band identity, bounds, or priority is invalid."));
            if (!bandIds.Add(band.Id))
                diagnostics.Add(new("RPG_FACTION_BAND_DUPLICATE", path,
                    "Disposition band IDs must be unique."));
        }
        for (var i = 0; i < catalog.Bands.Count; i++)
            for (var j = i + 1; j < catalog.Bands.Count; j++)
                if (catalog.Bands[i].Priority == catalog.Bands[j].Priority &&
                    catalog.Bands[i].Minimum <= catalog.Bands[j].Maximum &&
                    catalog.Bands[j].Minimum <= catalog.Bands[i].Maximum)
                    diagnostics.Add(new("RPG_FACTION_BANDS_OVERLAP", $"{root}/bands/{catalog.Bands[i].Id}",
                        "Disposition bands overlap at the same priority."));

        var overlayIds = new HashSet<Guid>();
        foreach (var overlay in catalog.EffectivePersonalReputation)
        {
            var path = $"{root}/personalReputation/{overlay.Id}";
            if (overlay.Id == Guid.Empty || overlay.SchemaVersion != 1 ||
                string.IsNullOrWhiteSpace(overlay.Provenance) || string.IsNullOrWhiteSpace(overlay.SubjectId) ||
                !Enum.IsDefined(overlay.SubjectKind) || !ids.Contains(overlay.SourceFactionId) ||
                !ids.Contains(overlay.TargetFactionId) || overlay.SourceFactionId == overlay.TargetFactionId ||
                !double.IsFinite(overlay.Delta) || overlay.Delta is < -100 or > 100 ||
                string.IsNullOrWhiteSpace(overlay.Cause))
                diagnostics.Add(new("RPG_FACTION_PERSONAL_REPUTATION_INVALID", path,
                    "Personal reputation identity, subject, faction references, delta, or cause is invalid."));
            if (!overlayIds.Add(overlay.Id))
                diagnostics.Add(new("RPG_FACTION_PERSONAL_REPUTATION_UUID_DUPLICATE", path,
                    "Personal reputation UUIDs must be unique."));
        }

        if (catalog.RequireFoundationRuntime && catalog.Factions.Count == 0)
            diagnostics.Add(new(FoundationMissingCode, $"{root}/definitions",
                "The module requires faction foundations at runtime."));

        ValidateReputation(catalog, root, ids, diagnostics);

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

    private static void ValidateReputation(
        FactionCatalog catalog,
        string root,
        IReadOnlySet<Guid> factionIds,
        List<FactionDiagnostic> diagnostics)
    {
        var profile = catalog.Reputation;
        var path = $"{root}/reputation";
        if (profile is null)
        {
            if (catalog.RequireReputationRuntime)
                diagnostics.Add(new(ReputationMissingCode, path,
                    "The module requires faction reputation at runtime."));
            return;
        }

        if (profile.Id == Guid.Empty || profile.SchemaVersion != 1 ||
            string.IsNullOrWhiteSpace(profile.Provenance))
            diagnostics.Add(new("RPG_FACTION_REPUTATION_PROFILE_INVALID", path,
                "Faction reputation profile identity is invalid."));

        var uuids = new HashSet<Guid>();
        AddReputationUuid(profile.Id, path, uuids, diagnostics);
        foreach (var transaction in profile.Transactions)
        {
            var transactionPath = $"{path}/transactions/{transaction.Id}";
            AddReputationUuid(transaction.Id, transactionPath, uuids, diagnostics);
            if (transaction.SchemaVersion != 1 || string.IsNullOrWhiteSpace(transaction.Provenance) ||
                !Enum.IsDefined(transaction.Scope) || string.IsNullOrWhiteSpace(transaction.ScopeId) ||
                !factionIds.Contains(transaction.SourceFactionId) ||
                !factionIds.Contains(transaction.TargetFactionId) ||
                transaction.SourceFactionId == transaction.TargetFactionId ||
                !double.IsFinite(transaction.Delta) || transaction.Delta is < -100 or > 100 ||
                string.IsNullOrWhiteSpace(transaction.Cause))
                diagnostics.Add(new("RPG_FACTION_REPUTATION_TRANSACTION_INVALID", transactionPath,
                    "Global reputation identity, scope, faction references, delta, or cause is invalid."));
        }

        foreach (var snapshot in profile.HeatmapHistory)
        {
            var snapshotPath = $"{path}/heatmapHistory/{snapshot.Id}";
            AddReputationUuid(snapshot.Id, snapshotPath, uuids, diagnostics);
            if (snapshot.SchemaVersion != 1 || string.IsNullOrWhiteSpace(snapshot.Provenance) ||
                string.IsNullOrWhiteSpace(snapshot.Label))
                diagnostics.Add(new("RPG_FACTION_HEATMAP_SNAPSHOT_INVALID", snapshotPath,
                    "Heatmap snapshot identity, label, or provenance is invalid."));
            var pairs = new HashSet<(Guid Source, Guid Target)>();
            foreach (var relationship in snapshot.Relationships)
            {
                var relationshipPath = $"{snapshotPath}/relationships/{relationship.SourceId}/{relationship.TargetId}";
                if (!factionIds.Contains(relationship.SourceId) || !factionIds.Contains(relationship.TargetId) ||
                    relationship.SourceId == relationship.TargetId || !double.IsFinite(relationship.Score) ||
                    relationship.Score is < -100 or > 100 || string.IsNullOrWhiteSpace(relationship.Explanation) ||
                    string.IsNullOrWhiteSpace(relationship.Preset))
                    diagnostics.Add(new("RPG_FACTION_HEATMAP_RELATION_INVALID", relationshipPath,
                        "Heatmap relationship is invalid."));
                if (!pairs.Add((relationship.SourceId, relationship.TargetId)))
                    diagnostics.Add(new("RPG_FACTION_HEATMAP_RELATION_DUPLICATE", relationshipPath,
                        "A heatmap snapshot can contain each direction only once."));
            }
        }

        var reaction = profile.Reaction;
        var reactionPath = $"{path}/reaction";
        AddReputationUuid(reaction.Id, reactionPath, uuids, diagnostics);
        if (reaction.SchemaVersion != 1 || string.IsNullOrWhiteSpace(reaction.Provenance) ||
            !Enum.IsDefined(reaction.DefaultScope) || string.IsNullOrWhiteSpace(reaction.DefaultScopeId))
            diagnostics.Add(new("RPG_FACTION_REACTION_POLICY_INVALID", reactionPath,
                "Reaction policy identity, scope, or provenance is invalid."));
    }

    private static void AddReputationUuid(
        Guid id, string path, HashSet<Guid> uuids, List<FactionDiagnostic> diagnostics)
    {
        if (id == Guid.Empty)
            diagnostics.Add(new("RPG_FACTION_REPUTATION_UUID_EMPTY", path,
                "Faction reputation UUID cannot be empty."));
        else if (!uuids.Add(id))
            diagnostics.Add(new("RPG_FACTION_REPUTATION_UUID_DUPLICATE", path,
                "Faction reputation UUIDs must be unique within the profile."));
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

    private static string NormalizeTag(string tag) => tag.Trim().ToLowerInvariant();
}
