using System.Buffers;
using System.Buffers.Binary;
using System.Text.Json;

namespace Forge.Network.Transport;

public sealed record InternetTransportHello(
    Guid ConnectionId,
    Guid AccountId,
    string DisplayName,
    string ModuleFingerprint,
    string ProtocolVersion,
    string? AuthenticationToken = null);

public sealed record InternetPlayerPose(
    long Sequence,
    string AreaId,
    double X,
    double Y,
    double Z,
    double Yaw);

public sealed record InternetClientPacket(
    int SchemaVersion,
    string Kind,
    InternetTransportHello? Hello = null,
    InternetPlayerPose? Pose = null,
    InternetGameplayIntent? GameplayIntent = null);

public sealed record InternetTransportWelcome(
    bool Accepted,
    string Code,
    Guid SessionId,
    Guid ConnectionId,
    long Revision,
    bool Encrypted = false,
    bool Authenticated = false,
    string? CertificateSha256 = null);

public sealed record InternetPlayerState(
    Guid ConnectionId,
    string DisplayName,
    long Sequence,
    string AreaId,
    double X,
    double Y,
    double Z,
    double Yaw);

public sealed record InternetWorldSnapshot(
    Guid SessionId,
    long Revision,
    IReadOnlyList<InternetPlayerState> Players);

public sealed record InternetGameplayIntent(
    Guid IntentId,
    long ExpectedRevision,
    string Kind,
    IReadOnlyDictionary<string, string> Payload);

public sealed record InternetPublicCombatantState(
    Guid ConnectionId,
    int Health,
    int MaximumHealth,
    bool Defeated);

public sealed record InternetPrivateGameplayState(
    Guid AccountId,
    IReadOnlyDictionary<string, int> Inventory,
    IReadOnlyDictionary<string, string> Quests,
    int AttacksLanded,
    long SaveRevision,
    string? CheckpointSha256);

public sealed record InternetGameplaySnapshot(
    Guid SessionId,
    long Revision,
    string EventCode,
    IReadOnlyList<InternetPublicCombatantState> Combatants,
    InternetPrivateGameplayState PrivateState);

public sealed record InternetServerPacket(
    int SchemaVersion,
    string Kind,
    InternetTransportWelcome? Welcome = null,
    InternetWorldSnapshot? Snapshot = null,
    InternetGameplaySnapshot? GameplaySnapshot = null,
    string? Code = null);

public static class InternetTransportProtocol
{
    public const int SchemaVersion = 2;
    public const string GameProtocolVersion = "2";
    public const int MaximumFrameBytes = 64 * 1024;
    public const string HelloKind = "hello";
    public const string WelcomeKind = "welcome";
    public const string PlayerPoseKind = "player.pose";
    public const string SnapshotKind = "world.snapshot";
    public const string GameplayIntentKind = "gameplay.intent";
    public const string GameplaySnapshotKind = "gameplay.snapshot";
    public const string PingKind = "ping";
    public const string PongKind = "pong";
    public const string ErrorKind = "error";
}

internal static class InternetFrameCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
    };

    internal static async ValueTask<T?> ReadAsync<T>(Stream stream, CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(stream);
        var header = new byte[sizeof(int)];
        if (!await TryReadExactlyAsync(stream, header, cancellationToken)) return null;
        var length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length is < 1 or > InternetTransportProtocol.MaximumFrameBytes)
            throw new InvalidDataException("NET_FRAME_SIZE_INVALID");

        var buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            if (!await TryReadExactlyAsync(stream, buffer.AsMemory(0, length), cancellationToken))
                throw new EndOfStreamException("NET_FRAME_TRUNCATED");
            try
            {
                return JsonSerializer.Deserialize<T>(buffer.AsSpan(0, length), Options)
                       ?? throw new InvalidDataException("NET_FRAME_EMPTY");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("NET_FRAME_JSON_INVALID", exception);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    internal static async ValueTask WriteAsync<T>(
        Stream stream,
        T packet,
        CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(packet);
        var payload = JsonSerializer.SerializeToUtf8Bytes(packet, Options);
        if (payload.Length is < 1 or > InternetTransportProtocol.MaximumFrameBytes)
            throw new InvalidDataException("NET_FRAME_SIZE_INVALID");
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async ValueTask<bool> TryReadExactlyAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer[read..], cancellationToken);
            if (count == 0) return read == 0;
            read += count;
        }

        return true;
    }
}
