using System.Text.Json;
using Forge.Domain.Factions;

namespace Forge.RpgKit.Domain.Factions;

public sealed record ReputationState(IReadOnlyList<PersonalReputationOverlay> Personal, IReadOnlyList<ReputationTransaction> Transactions);
public sealed record ReputationMutation(ReputationState State, ReputationTransaction Applied, ReputationTransaction Inverse, double PreviousScore, double CurrentScore);
public sealed record HeatmapDifference(Guid SourceId, Guid TargetId, double Before, double After);
public sealed record EncounterReaction(Guid ActorFactionId, Guid TargetFactionId, double Score, string Disposition);
public sealed record ReactionContext(string SubjectId, Guid SourceFactionId, Guid TargetFactionId, DateTimeOffset At, double DisguiseModifier, double CrimeModifier, double DialogueModifier, double? GameMasterOverride, double? ZoneOverride);
public sealed record ReactionResolution(double Score, string Disposition, IReadOnlyList<string> Explanation);

public static class FactionReputationService
{
    public static ReputationState InitialState(FactionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return new(catalog.EffectivePersonalReputation, catalog.Reputation?.Transactions ?? []);
    }

    public static ReputationMutation Apply(ReputationState state, ReputationTransaction transaction)
    {
        Validate(transaction);
        var previous = SumTransactions(state, transaction.Scope, transaction.ScopeId, transaction.SourceFactionId, transaction.TargetFactionId);
        var transactions = state.Transactions.Concat([transaction]).ToArray();
        var inverse = transaction with { Id = Guid.NewGuid(), Delta = -transaction.Delta, Cause = $"undo:{transaction.Id}", OccurredAt = transaction.OccurredAt };
        return new(new(state.Personal, transactions), transaction, inverse, previous, previous + transaction.Delta);
    }

    public static double EffectiveScore(FactionCatalog catalog, ReputationState state, ReactionContext context, ReputationScope scope, string scopeId)
    {
        var baseline = catalog.Relationships.FirstOrDefault(item => item.SourceId == context.SourceFactionId && item.TargetId == context.TargetFactionId)?.Score ?? 0;
        var global = SumTransactions(state, scope, scopeId, context.SourceFactionId, context.TargetFactionId);
        var personal = state.Personal.Where(item => item.SubjectId == context.SubjectId && item.SourceFactionId == context.SourceFactionId && item.TargetFactionId == context.TargetFactionId && (item.ExpiresAt is null || item.ExpiresAt > context.At)).Sum(item => item.Delta);
        return Math.Clamp(baseline + global + personal, -100, 100);
    }

    public static IReadOnlyList<FactionDefinition> QueryByTags(FactionCatalog catalog, IEnumerable<string> tags, bool requireAll = true)
    {
        var wanted = tags.Select(NormalizeTag).Where(tag => tag.Length > 0).ToHashSet(StringComparer.Ordinal);
        return catalog.Factions.Where(faction =>
        {
            var effective = FactionRuntimeResolver.EffectiveDefinition(catalog, faction.Id).Tags;
            return requireAll ? wanted.All(effective.Contains) : effective.Any(wanted.Contains);
        }).OrderBy(faction => faction.Slug, StringComparer.Ordinal).ToArray();
    }

    public static FactionHeatmapSnapshot Capture(
        FactionCatalog catalog,
        string label,
        DateTimeOffset at,
        string provenance = "rpgkit:heatmap") =>
        new(Guid.NewGuid(), 1, provenance, at, label,
            catalog.Relationships.OrderBy(item => item.SourceId).ThenBy(item => item.TargetId).ToArray());

    public static IReadOnlyList<HeatmapDifference> Compare(FactionHeatmapSnapshot before, FactionHeatmapSnapshot after)
    {
        var keys = before.Relationships.Select(Key).Concat(after.Relationships.Select(Key)).Distinct().OrderBy(key => key.Item1).ThenBy(key => key.Item2);
        return keys.Select(key => new HeatmapDifference(key.Item1, key.Item2, Score(before, key), Score(after, key))).Where(diff => diff.Before != diff.After).ToArray();
    }

    public static IReadOnlyList<EncounterReaction> SimulateEncounter(FactionCatalog catalog, IEnumerable<Guid> actors, Guid targetFactionId)
        => actors.Select(actor => new EncounterReaction(actor, targetFactionId, Score(catalog, actor, targetFactionId), FactionValidator.ResolveBand(catalog, actor, targetFactionId))).ToArray();

    public static string ExportSnapshot(FactionHeatmapSnapshot snapshot) => JsonSerializer.Serialize(snapshot);
    public static FactionHeatmapSnapshot ImportSnapshot(string json)
    {
        var snapshot = JsonSerializer.Deserialize<FactionHeatmapSnapshot>(json) ??
            throw new InvalidOperationException("RPG_FACTION_HEATMAP_INVALID");
        if (snapshot.Id == Guid.Empty || snapshot.SchemaVersion != 1 ||
            string.IsNullOrWhiteSpace(snapshot.Provenance) || string.IsNullOrWhiteSpace(snapshot.Label) ||
            snapshot.Relationships.Any(item => item.SourceId == Guid.Empty || item.TargetId == Guid.Empty ||
                item.SourceId == item.TargetId || !double.IsFinite(item.Score) || item.Score is < -100 or > 100 ||
                string.IsNullOrWhiteSpace(item.Explanation) || string.IsNullOrWhiteSpace(item.Preset)))
            throw new InvalidOperationException("RPG_FACTION_HEATMAP_INVALID");
        return snapshot;
    }

    public static ReactionResolution ResolveReaction(
        FactionCatalog catalog,
        ReputationState state,
        ReactionContext context)
    {
        var policy = catalog.Reputation?.Reaction ??
            new(Guid.Empty, 1, "rpgkit:optional-default", ReputationScope.World, "world",
                true, true, true, true, true);
        return ResolveReaction(catalog, state, context, policy.DefaultScope, policy.DefaultScopeId);
    }

    public static ReactionResolution ResolveReaction(FactionCatalog catalog, ReputationState state, ReactionContext context, ReputationScope scope, string scopeId)
    {
        var policy = catalog.Reputation?.Reaction;
        var explanation = new List<string>();
        var score = EffectiveScore(catalog, state, context, scope, scopeId); explanation.Add($"reputation:{score}");
        if (policy?.ApplyDisguise != false)
        {
            score += context.DisguiseModifier;
            explanation.Add($"disguise:{context.DisguiseModifier}");
        }
        if (policy?.ApplyCrimes != false)
        {
            score += context.CrimeModifier;
            explanation.Add($"crime:{context.CrimeModifier}");
        }
        if (policy?.ApplyDialogue != false)
        {
            score += context.DialogueModifier;
            explanation.Add($"dialogue:{context.DialogueModifier}");
        }
        if (policy?.ApplyZoneRules != false && context.ZoneOverride is { } zone)
        {
            score = zone;
            explanation.Add($"zone-override:{zone}");
        }
        if (policy?.AllowGameMasterOverride != false && context.GameMasterOverride is { } gm)
        {
            score = gm;
            explanation.Add($"gm-override:{gm}");
        }
        score = Math.Clamp(score, -100, 100);
        return new(score, FactionValidator.ResolveBand(catalog, context.SourceFactionId, context.TargetFactionId, score), explanation);
    }

    private static double SumTransactions(ReputationState state, ReputationScope scope, string scopeId, Guid source, Guid target)
        => state.Transactions.Where(item => item.Scope == scope && item.ScopeId == scopeId && item.SourceFactionId == source && item.TargetFactionId == target).Sum(item => item.Delta);
    private static void Validate(ReputationTransaction transaction)
    {
        if (transaction.Id == Guid.Empty || transaction.SchemaVersion != 1 ||
            string.IsNullOrWhiteSpace(transaction.Provenance) || !Enum.IsDefined(transaction.Scope) ||
            string.IsNullOrWhiteSpace(transaction.ScopeId) || transaction.SourceFactionId == Guid.Empty ||
            transaction.TargetFactionId == Guid.Empty || transaction.SourceFactionId == transaction.TargetFactionId ||
            !double.IsFinite(transaction.Delta) || transaction.Delta is < -100 or > 100 ||
            string.IsNullOrWhiteSpace(transaction.Cause))
            throw new InvalidOperationException("RPG_FACTION_REPUTATION_TRANSACTION_INVALID");
    }
    private static string NormalizeTag(string tag) => tag.Trim().ToLowerInvariant();
    private static (Guid, Guid) Key(FactionRelationship item) => (item.SourceId, item.TargetId);
    private static double Score(FactionHeatmapSnapshot snapshot, (Guid, Guid) key) => snapshot.Relationships.FirstOrDefault(item => Key(item) == key)?.Score ?? 0;
    private static double Score(FactionCatalog catalog, Guid source, Guid target) => catalog.Relationships.FirstOrDefault(item => item.SourceId == source && item.TargetId == target)?.Score ?? 0;
}
