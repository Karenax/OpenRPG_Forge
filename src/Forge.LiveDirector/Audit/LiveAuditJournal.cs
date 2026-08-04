using System.Security.Cryptography;
using System.Text;

namespace Forge.LiveDirector.Audit;

public sealed class LiveAuditJournal(Func<DateTimeOffset> utcNow)
{
    public sealed record Entry(long Sequence, DateTimeOffset At, Guid ActorId, string Command, IReadOnlyList<Guid> Targets, string Before, string After, string Reason, Guid CorrelationId, string PreviousHash, string Hash);
    private readonly List<Entry> _entries = [];

    public IReadOnlyList<Entry> Entries => _entries.ToArray();

    public Entry Append(Guid actorId, string command, IReadOnlyList<Guid> targets, string before, string after, string reason, Guid correlationId)
    {
        if (actorId == Guid.Empty || correlationId == Guid.Empty || string.IsNullOrWhiteSpace(command) || string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("LIVE_AUDIT_ENTRY_INVALID");
        var sequence = _entries.Count + 1L;
        var at = utcNow();
        var previous = _entries.LastOrDefault()?.Hash ?? "GENESIS";
        var canonical = $"{sequence}|{at:O}|{actorId}|{command}|{string.Join(',', targets.Order())}|{before}|{after}|{reason}|{correlationId}|{previous}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        var entry = new Entry(sequence, at, actorId, command, targets.ToArray(), before, after, reason.Trim(), correlationId, previous, hash);
        _entries.Add(entry);
        return entry;
    }

    public bool Verify() => _entries.Select((entry, index) => index == 0 ? entry.PreviousHash == "GENESIS" : entry.PreviousHash == _entries[index - 1].Hash).All(x => x);
}
