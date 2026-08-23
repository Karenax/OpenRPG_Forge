using Forge.LiveDirector.World;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Search;

public sealed class LiveSearchService(LiveWorldChooser chooser)
{
    public sealed record SavedQuery(Guid QueryId, string Name, LiveWorldChooser.Query Query);
    public sealed record Page(IReadOnlyList<LiveWorldChooser.Entry> Matches, int Index,
        LiveWorldChooser.Entry? Current, string? DeepLink, string? VisualFocus);

    private readonly Dictionary<Guid, SavedQuery> _saved = [];
    private readonly Dictionary<Guid, int> _cursorByActor = [];

    public void Save(SavedQuery query)
    {
        if (query.QueryId == Guid.Empty || string.IsNullOrWhiteSpace(query.Name)) throw new ArgumentException("LIVE_SEARCH_QUERY_INVALID", nameof(query));
        _saved[query.QueryId] = query;
    }

    public Page Next(AuthorizationSubject actor, LiveWorldChooser.Query query)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(query);
        var matches = chooser.Search(actor, query);
        if (matches.Count == 0) return new(matches, -1, null, null, null);
        var index = (_cursorByActor.GetValueOrDefault(actor.AccountId, -1) + 1) % matches.Count;
        _cursorByActor[actor.AccountId] = index;
        var current = matches[index];
        return new(matches, index, current,
            $"openrpg://live/zone/{current.ZoneId}/object/{current.ObjectId}",
            $"viewport-focus:object={current.ObjectId}:zone={current.ZoneId}:kind={current.Kind}");
    }

    public Page NextSaved(AuthorizationSubject actor, Guid queryId) => _saved.TryGetValue(queryId, out var saved)
        ? Next(actor, saved.Query)
        : new([], -1, null, null, null);
}
