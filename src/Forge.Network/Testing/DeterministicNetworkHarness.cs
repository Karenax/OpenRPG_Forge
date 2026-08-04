namespace Forge.Network.Testing;

public sealed record SimulatedPacket(Guid PacketId, string From, string To, long SendTick, long DeliveryTick, string Payload, int Copy);
public sealed record NetworkFaultProfile(int LatencyTicks, int DropEvery, int DuplicateEvery, bool ReverseSameTick);

public sealed class DeterministicNetworkHarness(NetworkFaultProfile profile)
{
    private readonly List<SimulatedPacket> _pending = [];
    private readonly Dictionary<string, List<string>> _received = new(StringComparer.Ordinal);
    private long _tick;
    private int _sent;

    public long Tick => _tick;
    public void Send(string from, string to, string payload)
    {
        _sent++;
        if (profile.DropEvery > 0 && _sent % profile.DropEvery == 0) return;
        var id = GuidFromSequence(_sent);
        _pending.Add(new(id, from, to, _tick, _tick + profile.LatencyTicks, payload, 0));
        if (profile.DuplicateEvery > 0 && _sent % profile.DuplicateEvery == 0) _pending.Add(new(id, from, to, _tick, _tick + profile.LatencyTicks, payload, 1));
    }

    public void Advance(int ticks = 1)
    {
        if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
        for (var step = 0; step < ticks; step++)
        {
            _tick++;
            var due = _pending.Where(x => x.DeliveryTick <= _tick).OrderBy(x => x.PacketId).ThenBy(x => x.Copy).ToList();
            if (profile.ReverseSameTick) due.Reverse();
            foreach (var packet in due)
            {
                if (!_received.TryGetValue(packet.To, out var messages)) _received[packet.To] = messages = [];
                messages.Add(packet.Payload);
                _pending.Remove(packet);
            }
        }
    }

    public IReadOnlyList<string> Received(string endpoint) => _received.GetValueOrDefault(endpoint) ?? [];
    public string Digest() => string.Join("|", _received.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Key}:{string.Join(',', x.Value)}"));
    private static Guid GuidFromSequence(int sequence) { Span<byte> bytes = stackalloc byte[16]; BitConverter.TryWriteBytes(bytes, sequence); return new Guid(bytes); }
}
