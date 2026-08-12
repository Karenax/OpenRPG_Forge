using Forge.Domain.Factions;

namespace Forge.RpgKit.Application.Factions;

public sealed record FactionProjectState(long Revision, FactionCatalog Catalog);
public sealed record FactionMutationResult(bool Succeeded, FactionProjectState State, string? DiagnosticCode, string Patch, string Inverse, bool Applied);

public static class FactionAuthoringService
{
    public static FactionMutationResult Upsert(FactionProjectState state, long baseRevision, FactionDefinition faction, bool dryRun)
    {
        if (baseRevision != state.Revision) return Failure(state, "RPG_FACTION_REVISION_CONFLICT");
        var existing = state.Catalog.Factions.FirstOrDefault(item => item.Id == faction.Id); var operation = existing is null ? "add" : "replace";
        var factions = existing is null ? [.. state.Catalog.Factions, faction] : state.Catalog.Factions.Select(item => item.Id == faction.Id ? faction : item).ToArray();
        var catalog = state.Catalog with { Factions = factions }; var diagnostics = FactionValidator.Validate(catalog);
        if (diagnostics.Count > 0) return Failure(state, diagnostics[0].Code);
        var patch = $"{operation} factions/{faction.Id}"; var inverse = existing is null ? $"remove factions/{faction.Id}" : $"replace factions/{faction.Id}";
        return dryRun ? new(true, state, null, patch, inverse, false) : new(true, new(state.Revision + 1, catalog), null, patch, inverse, true);
    }

    public static FactionMutationResult ArchiveOrRemove(FactionProjectState state, long baseRevision, Guid factionId, Guid? migrateMembersTo, bool remove, bool dryRun)
    {
        if (baseRevision != state.Revision) return Failure(state, "RPG_FACTION_REVISION_CONFLICT");
        var faction = state.Catalog.Factions.FirstOrDefault(item => item.Id == factionId); if (faction is null) return Failure(state, "RPG_FACTION_NOT_FOUND");
        var hasMembers = state.Catalog.Members.Any(item => item.FactionId == factionId) ||
            state.Catalog.Advanced?.Memberships.Any(item => item.FactionId == factionId) == true;
        if (remove && hasMembers && migrateMembersTo is null) return Failure(state, "RPG_FACTION_MEMBERS_REQUIRE_MIGRATION");
        if (migrateMembersTo is not null && !state.Catalog.Factions.Any(item => item.Id == migrateMembersTo)) return Failure(state, "RPG_FACTION_MIGRATION_TARGET_MISSING");
        var members = state.Catalog.Members.Select(item => item.FactionId == factionId && migrateMembersTo is not null ? item with { FactionId = migrateMembersTo.Value } : item).ToArray();
        var factions = remove
            ? state.Catalog.Factions.Where(item => item.Id != factionId).Select(item => item with
            {
                ParentIds = item.ParentIds.Where(parent => parent != factionId)
                    .Concat(migrateMembersTo is { } target && item.ParentIds.Contains(factionId) ? [target] : [])
                    .Distinct().ToArray(),
            }).ToArray()
            : state.Catalog.Factions.Select(item => item.Id == factionId ? item with { Archived = true } : item).ToArray();
        var relationships = remove ? state.Catalog.Relationships.Where(item => item.SourceId != factionId && item.TargetId != factionId).ToArray() : state.Catalog.Relationships;
        var personal = state.Catalog.EffectivePersonalReputation.Select(item => item with
        {
            SourceFactionId = item.SourceFactionId == factionId && migrateMembersTo is { } sourceTarget
                ? sourceTarget : item.SourceFactionId,
            TargetFactionId = item.TargetFactionId == factionId && migrateMembersTo is { } reputationTarget
                ? reputationTarget : item.TargetFactionId,
        }).Where(item => item.SourceFactionId != factionId && item.TargetFactionId != factionId &&
            item.SourceFactionId != item.TargetFactionId).ToArray();
        var advanced = remove && state.Catalog.Advanced is { } profile ? profile with
        {
            Inheritance = profile.Inheritance.Where(item => item.FactionId != factionId).ToArray(),
            WitnessPolicies = profile.WitnessPolicies.Select(item =>
                item.FactionId == factionId && migrateMembersTo is { } witnessTarget
                    ? item with { FactionId = witnessTarget } : item)
                .Where(item => item.FactionId != factionId).ToArray(),
            Memberships = profile.Memberships.Select(item =>
                item.FactionId == factionId && migrateMembersTo is { } membershipTarget
                    ? item with { FactionId = membershipTarget } : item)
                .Where(item => item.FactionId != factionId).ToArray(),
            DynamicFactions = profile.DynamicFactions.Where(item => item.FactionId != factionId)
                .Select(item => item with
                {
                    Diplomacy = item.Diplomacy.Where(state => state.TargetFactionId != factionId).ToArray(),
                }).ToArray(),
        } : state.Catalog.Advanced;
        var reputation = remove && state.Catalog.Reputation is { } reputationProfile
            ? reputationProfile with
            {
                Transactions = reputationProfile.Transactions.Select(item => item with
                {
                    SourceFactionId = item.SourceFactionId == factionId && migrateMembersTo is { } sourceTarget
                            ? sourceTarget : item.SourceFactionId,
                    TargetFactionId = item.TargetFactionId == factionId && migrateMembersTo is { } target
                            ? target : item.TargetFactionId,
                })
                    .Where(item => item.SourceFactionId != factionId && item.TargetFactionId != factionId &&
                        item.SourceFactionId != item.TargetFactionId).ToArray(),
                HeatmapHistory = reputationProfile.HeatmapHistory.Select(snapshot => snapshot with
                {
                    Relationships = snapshot.Relationships.Select(item => item with
                    {
                        SourceId = item.SourceId == factionId && migrateMembersTo is { } sourceTarget
                                ? sourceTarget : item.SourceId,
                        TargetId = item.TargetId == factionId && migrateMembersTo is { } target
                                ? target : item.TargetId,
                    })
                            .Where(item => item.SourceId != factionId && item.TargetId != factionId &&
                                item.SourceId != item.TargetId)
                            .GroupBy(item => (item.SourceId, item.TargetId))
                            .Select(group => group.First()).ToArray(),
                }).ToArray(),
            }
            : state.Catalog.Reputation;
        var updated = state.Catalog with
        {
            Factions = factions,
            Members = members,
            Relationships = relationships,
            PersonalReputation = personal,
            Advanced = advanced,
            Reputation = reputation,
        };
        var diagnostics = FactionValidator.Validate(updated);
        if (diagnostics.Count > 0) return Failure(state, diagnostics[0].Code);
        var patch = $"{(remove ? "remove" : "archive")} factions/{factionId}"; var inverse = $"restore factions/{factionId}";
        return dryRun ? new(true, state, null, patch, inverse, false) : new(true, new(state.Revision + 1, updated), null, patch, inverse, true);
    }

    private static FactionMutationResult Failure(FactionProjectState state, string code) => new(false, state, code, "", "", false);
}
