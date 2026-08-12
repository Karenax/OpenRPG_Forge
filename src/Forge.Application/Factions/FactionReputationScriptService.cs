using Forge.Domain.Factions;

namespace Forge.Application.Factions;

public sealed record FactionReputationHandle(Guid Id, int SchemaVersion);

public sealed record FactionReputationScriptSnapshot(
    FactionReputationHandle Handle,
    string Provenance,
    int TransactionCount,
    int HeatmapHistoryCount,
    ReputationScope DefaultScope,
    string DefaultScopeId,
    bool RequiredAtRuntime);

/// <summary>Typed ForgeScript read boundary for the canonical P1 reputation profile.</summary>
public sealed class FactionReputationScriptService
{
    private readonly FactionCatalog _catalog;
    private readonly FactionReputationProfile _profile;

    public FactionReputationScriptService(FactionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var resolution = FactionRuntimeResolver.ResolveReputation(catalog);
        if (resolution.Diagnostics.Count > 0)
            throw new ArgumentException(string.Join(';', resolution.Diagnostics.Select(item => item.Code)),
                nameof(catalog));
        _profile = resolution.Profile ?? throw new ArgumentException("FACTION_REPUTATION_MISSING", nameof(catalog));
        _catalog = catalog;
    }

    public FactionReputationHandle Handle => new(_profile.Id, _profile.SchemaVersion);

    public FactionReputationScriptSnapshot Read(FactionReputationHandle handle)
    {
        EnsureHandle(handle);
        return new(handle, _profile.Provenance, _profile.Transactions.Count, _profile.HeatmapHistory.Count,
            _profile.Reaction.DefaultScope, _profile.Reaction.DefaultScopeId, _catalog.RequireReputationRuntime);
    }

    public IReadOnlyList<ReputationTransaction> Transactions(
        FactionReputationHandle handle,
        ReputationScope? scope = null,
        string? scopeId = null,
        Guid? factionId = null)
    {
        EnsureHandle(handle);
        return _profile.Transactions.Where(item =>
                (scope is null || item.Scope == scope) &&
                (string.IsNullOrWhiteSpace(scopeId) || string.Equals(item.ScopeId, scopeId, StringComparison.Ordinal)) &&
                (factionId is null || item.SourceFactionId == factionId || item.TargetFactionId == factionId))
            .OrderBy(item => item.OccurredAt).ThenBy(item => item.Id).ToArray();
    }

    public IReadOnlyList<FactionHeatmapSnapshot> HeatmapHistory(FactionReputationHandle handle)
    {
        EnsureHandle(handle);
        return _profile.HeatmapHistory.OrderBy(item => item.CapturedAt).ThenBy(item => item.Id).ToArray();
    }

    public FactionReactionPolicy Reaction(FactionReputationHandle handle)
    {
        EnsureHandle(handle);
        return _profile.Reaction;
    }

    public IReadOnlyList<FactionDefinitionHandle> QueryByTags(
        FactionReputationHandle handle,
        IEnumerable<string> tags,
        bool requireAll = true)
    {
        EnsureHandle(handle);
        ArgumentNullException.ThrowIfNull(tags);
        var wanted = tags.Select(item => item.Trim().ToLowerInvariant())
            .Where(item => item.Length > 0).ToHashSet(StringComparer.Ordinal);
        return _catalog.Factions.Where(item =>
            {
                var effective = FactionRuntimeResolver.EffectiveDefinition(_catalog, item.Id).Tags;
                return requireAll ? wanted.All(effective.Contains) : effective.Any(wanted.Contains);
            })
            .OrderBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new FactionDefinitionHandle(item.Id, item.SchemaVersion)).ToArray();
    }

    private void EnsureHandle(FactionReputationHandle handle)
    {
        if (handle.Id != _profile.Id || handle.SchemaVersion != _profile.SchemaVersion)
            throw new KeyNotFoundException("FACTION_REPUTATION_HANDLE_INVALID");
    }
}
