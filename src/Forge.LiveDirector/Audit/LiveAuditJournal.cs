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

    public IReadOnlyList<Entry> Query(Guid? actorId = null, string? command = null, Guid? correlationId = null,
        int limit = 100) => _entries
        .Where(entry => actorId is null || entry.ActorId == actorId)
        .Where(entry => string.IsNullOrWhiteSpace(command) || entry.Command.Contains(command, StringComparison.OrdinalIgnoreCase))
        .Where(entry => correlationId is null || entry.CorrelationId == correlationId)
        .TakeLast(Math.Clamp(limit, 1, 10_000))
        .ToArray();

    public bool Verify()
    {
        for (var index = 0; index < _entries.Count; index++)
        {
            var entry = _entries[index];
            var expectedPrevious = index == 0 ? "GENESIS" : _entries[index - 1].Hash;
            if (!string.Equals(entry.PreviousHash, expectedPrevious, StringComparison.Ordinal)) return false;
            var canonical = $"{entry.Sequence}|{entry.At:O}|{entry.ActorId}|{entry.Command}|{string.Join(',', entry.Targets.Order())}|{entry.Before}|{entry.After}|{entry.Reason}|{entry.CorrelationId}|{entry.PreviousHash}";
            var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
            if (!string.Equals(entry.Hash, expectedHash, StringComparison.Ordinal)) return false;
        }
        return true;
    }
}
