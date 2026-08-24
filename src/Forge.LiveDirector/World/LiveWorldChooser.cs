using Forge.Network.Operations;

namespace Forge.LiveDirector.World;

public sealed class LiveWorldChooser(AuthorizationService authorization)
{
    public sealed record Entry(Guid ObjectId, Guid ZoneId, string Kind, string Name, string Tag, Guid? PlayerAccountId, string State, IReadOnlyList<string> Diagnostics, string InstanceId = "default", bool Private = false, double X = 0, double Y = 0, double Z = 0, double Heading = 0);
    public sealed record Query(Guid? ZoneId, string? Kind, string? Text, Guid? PlayerAccountId,
        string? State, string? InstanceId = null);

    private readonly Dictionary<Guid, Entry> _entries = [];

    public void Upsert(Entry entry)
    {
        if (entry.ObjectId == Guid.Empty || entry.ZoneId == Guid.Empty || string.IsNullOrWhiteSpace(entry.Name))
            throw new ArgumentException("LIVE_WORLD_ENTRY_INVALID", nameof(entry));
        _entries[entry.ObjectId] = entry;
    }

    public Entry? Find(Guid objectId) => _entries.GetValueOrDefault(objectId);

    public bool Remove(Guid objectId) => _entries.Remove(objectId);

    public IReadOnlyList<Entry> Search(AuthorizationSubject actor, Query query,
        bool includePrivate = false)
    {
        if (!authorization.Authorize(actor, NetworkCapability.ViewOperations, query.ZoneId).Allowed) return [];
        return _entries.Values
            .Where(x => query.ZoneId is null || x.ZoneId == query.ZoneId)
            .Where(x => string.IsNullOrWhiteSpace(query.InstanceId) ||
                        string.Equals(x.InstanceId, query.InstanceId, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(query.Kind) || string.Equals(x.Kind, query.Kind, StringComparison.OrdinalIgnoreCase))
            .Where(x => query.PlayerAccountId is null || x.PlayerAccountId == query.PlayerAccountId)
            .Where(x => string.IsNullOrWhiteSpace(query.State) || string.Equals(x.State, query.State, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(query.Text) || x.Name.Contains(query.Text, StringComparison.OrdinalIgnoreCase) || x.Tag.Contains(query.Text, StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.Private || includePrivate && authorization.Authorize(actor,
                NetworkCapability.Observe, x.ZoneId, "live.private.override").Allowed)
            .OrderBy(x => x.ZoneId).ThenBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.ObjectId).ToArray();
    }
}
