using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;

namespace Forge.Network.Transport;

public sealed record InternetClientOptions(
    string Address,
    int Port,
    string PlayerName,
    string ModuleFingerprint,
    string ProtocolVersion = InternetTransportProtocol.GameProtocolVersion,
    Guid? ConnectionId = null,
    Guid? AccountId = null,
    string? AuthenticationToken = null,
    bool RequireTls = false,
    string? PinnedServerCertificateSha256 = null);

public sealed class TcpGameClient : IAsyncDisposable, IDisposable
{
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);

    private readonly TcpClient _tcpClient;
    private readonly Stream _stream;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Channel<InternetClientPacket> _outgoing = Channel.CreateBounded<InternetClientPacket>(
        new BoundedChannelOptions(32)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });
    private Task? _readTask;
    private Task? _writeTask;
    private InternetWorldSnapshot? _latestSnapshot;
    private InternetGameplaySnapshot? _latestGameplaySnapshot;
    private long _poseSequence;
    private long _sentPacketCount;
    private bool _disposed;

    private TcpGameClient(
        TcpClient tcpClient,
        Stream stream,
        Guid connectionId,
        Guid accountId,
        Guid sessionId,
        bool encrypted,
        bool authenticated,
        string? serverCertificateSha256,
        string? tlsProtocol)
    {
        _tcpClient = tcpClient;
        _stream = stream;
        ConnectionId = connectionId;
        AccountId = accountId;
        SessionId = sessionId;
        IsEncrypted = encrypted;
        IsAuthenticated = authenticated;
        ServerCertificateSha256 = serverCertificateSha256;
        TlsProtocol = tlsProtocol;
    }

    public Guid ConnectionId { get; }

    public Guid AccountId { get; }

    public Guid SessionId { get; }

    public InternetWorldSnapshot? LatestSnapshot => Volatile.Read(ref _latestSnapshot);

    public InternetGameplaySnapshot? LatestGameplaySnapshot => Volatile.Read(ref _latestGameplaySnapshot);

    public bool IsEncrypted { get; }

    public bool IsAuthenticated { get; }

    public string? ServerCertificateSha256 { get; }

    public string? TlsProtocol { get; }

    public string? LastServerCode { get; private set; }

    public long SentPacketCount => Interlocked.Read(ref _sentPacketCount);

    public static async Task<TcpGameClient> ConnectAsync(
        InternetClientOptions options,
        CancellationToken cancellationToken = default)
    {
        Validate(options);
        var connectionId = options.ConnectionId ?? Guid.NewGuid();
        var accountId = options.AccountId ?? Guid.NewGuid();
        var tcpClient = new TcpClient { NoDelay = true };
        try
        {
            using var handshake = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            handshake.CancelAfter(HandshakeTimeout);
            await tcpClient.ConnectAsync(options.Address.Trim(), options.Port, handshake.Token);
            Stream stream = tcpClient.GetStream();
            SslStream? secureStream = null;
            var certificateMismatch = false;
            if (options.RequireTls)
            {
                var expectedCertificate = options.PinnedServerCertificateSha256!.ToLowerInvariant();
                secureStream = new SslStream(
                    stream,
                    leaveInnerStreamOpen: false,
                    (_, certificate, _, _) =>
                    {
                        if (certificate is null)
                        {
                            certificateMismatch = true;
                            return false;
                        }
                        var actual = Convert.ToHexString(SHA256.HashData(certificate.GetRawCertData()))
                            .ToLowerInvariant();
                        certificateMismatch = !string.Equals(actual, expectedCertificate, StringComparison.Ordinal);
                        return !certificateMismatch;
                    });
                try
                {
                    await secureStream.AuthenticateAsClientAsync(
                        new SslClientAuthenticationOptions
                        {
                            TargetHost = options.Address.Trim(),
                            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                        },
                        handshake.Token);
                    stream = secureStream;
                }
                catch (AuthenticationException exception)
                {
                    throw new InternetTransportException(
                        certificateMismatch ? "NET_SERVER_CERTIFICATE_MISMATCH" : "NET_TLS_HANDSHAKE_FAILED",
                        "The secure server identity could not be verified.",
                        exception);
                }
            }
            await InternetFrameCodec.WriteAsync(
                stream,
                new InternetClientPacket(
                    InternetTransportProtocol.SchemaVersion,
                    InternetTransportProtocol.HelloKind,
                    new InternetTransportHello(
                        connectionId,
                        accountId,
                        options.PlayerName.Trim(),
                        options.ModuleFingerprint,
                        options.ProtocolVersion,
                        options.AuthenticationToken)),
                handshake.Token);
            var response = await InternetFrameCodec.ReadAsync<InternetServerPacket>(stream, handshake.Token);
            if (response?.SchemaVersion != InternetTransportProtocol.SchemaVersion ||
                !string.Equals(response.Kind, InternetTransportProtocol.WelcomeKind, StringComparison.Ordinal) ||
                response.Welcome is null)
                throw new InternetTransportException("NET_WELCOME_INVALID", "The server returned an invalid welcome packet.");
            if (!response.Welcome.Accepted)
                throw new InternetTransportException(
                    response.Welcome.Code,
                    $"The server refused the connection: {response.Welcome.Code}");
            if (options.RequireTls &&
                (!response.Welcome.Encrypted || !response.Welcome.Authenticated ||
                 !string.Equals(
                     response.Welcome.CertificateSha256,
                     options.PinnedServerCertificateSha256,
                     StringComparison.OrdinalIgnoreCase)))
                throw new InternetTransportException(
                    "NET_SECURE_WELCOME_INVALID",
                    "The server did not confirm the pinned authenticated transport.");

            var client = new TcpGameClient(
                tcpClient,
                stream,
                connectionId,
                accountId,
                response.Welcome.SessionId,
                response.Welcome.Encrypted,
                response.Welcome.Authenticated,
                response.Welcome.CertificateSha256,
                secureStream?.SslProtocol.ToString());
            // Runtime engines commonly install a main-thread synchronization context.
            // Network pumps must remain live while that thread performs synchronous scene
            // construction, so they deliberately run on the thread pool.
            client._readTask = Task.Run(() => client.ReadLoopAsync(client._lifetime.Token));
            client._writeTask = Task.Run(() => client.WriteLoopAsync(client._lifetime.Token));
            return client;
        }
        catch (InternetTransportException)
        {
            tcpClient.Dispose();
            throw;
        }
        catch (Exception exception) when (exception is SocketException or IOException or OperationCanceledException)
        {
            tcpClient.Dispose();
            throw new InternetTransportException(
                exception is OperationCanceledException ? "NET_CONNECT_TIMEOUT" : "NET_CONNECT_FAILED",
                $"Unable to connect to {options.Address}:{options.Port}.",
                exception);
        }
    }

    public bool PublishPose(string areaId, double x, double y, double z, double yaw)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(areaId);
        var sequence = Interlocked.Increment(ref _poseSequence);
        return _outgoing.Writer.TryWrite(
            new InternetClientPacket(
                InternetTransportProtocol.SchemaVersion,
                InternetTransportProtocol.PlayerPoseKind,
                Pose: new InternetPlayerPose(sequence, areaId.Trim(), x, y, z, yaw)));
    }

    public async ValueTask<Guid> PublishGameplayIntentAsync(
        string kind,
        IReadOnlyDictionary<string, string> payload,
        CancellationToken cancellationToken = default)
    {
        var snapshot = LatestGameplaySnapshot
            ?? throw new InvalidOperationException("NET_GAMEPLAY_SNAPSHOT_REQUIRED");
        var intentId = Guid.NewGuid();
        await PublishGameplayIntentAsync(
            intentId,
            snapshot.Revision,
            kind,
            payload,
            cancellationToken);
        return intentId;
    }

    public async ValueTask PublishGameplayIntentAsync(
        Guid intentId,
        long expectedRevision,
        string kind,
        IReadOnlyDictionary<string, string> payload,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (intentId == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(intentId));
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(payload);
        if (kind.Length > 64 || expectedRevision < 0 || payload.Count > 16 ||
            payload.Any(item => item.Key.Length is < 1 or > 64 || item.Value.Length > 256))
            throw new ArgumentException("NET_GAMEPLAY_INTENT_INVALID", nameof(payload));
        await _outgoing.Writer.WriteAsync(
            new InternetClientPacket(
                InternetTransportProtocol.SchemaVersion,
                InternetTransportProtocol.GameplayIntentKind,
                GameplayIntent: new InternetGameplayIntent(intentId, expectedRevision, kind.Trim(), payload)),
            cancellationToken);
    }

    public async Task<InternetWorldSnapshot> WaitForSnapshotAsync(
        Func<InternetWorldSnapshot, bool> predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        wait.CancelAfter(timeout);
        try
        {
            while (true)
            {
                var snapshot = LatestSnapshot;
                if (snapshot is not null && predicate(snapshot)) return snapshot;
                await Task.Delay(10, wait.Token);
            }
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"NET_SNAPSHOT_TIMEOUT players={LatestSnapshot?.Players.Count ?? 0} " +
                $"names={string.Join(',', LatestSnapshot?.Players.Select(item => item.DisplayName) ?? [])} " +
                $"code={LastServerCode ?? "none"}",
                exception);
        }
    }

    public async Task<InternetGameplaySnapshot> WaitForGameplaySnapshotAsync(
        Func<InternetGameplaySnapshot, bool> predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        wait.CancelAfter(timeout);
        try
        {
            while (true)
            {
                var snapshot = LatestGameplaySnapshot;
                if (snapshot is not null && predicate(snapshot)) return snapshot;
                await Task.Delay(10, wait.Token);
            }
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"NET_GAMEPLAY_SNAPSHOT_TIMEOUT revision={LatestGameplaySnapshot?.Revision ?? -1} " +
                $"event={LatestGameplaySnapshot?.EventCode ?? "none"} code={LastServerCode ?? "none"}",
                exception);
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var packet = await InternetFrameCodec.ReadAsync<InternetServerPacket>(_stream, cancellationToken);
                if (packet is null)
                {
                    LastServerCode = "NET_SERVER_DISCONNECTED";
                    return;
                }
                if (packet.SchemaVersion != InternetTransportProtocol.SchemaVersion)
                {
                    LastServerCode = "NET_TRANSPORT_SCHEMA_MISMATCH";
                    continue;
                }
                if (string.Equals(packet.Kind, InternetTransportProtocol.SnapshotKind, StringComparison.Ordinal) &&
                    packet.Snapshot is not null)
                {
                    var current = Volatile.Read(ref _latestSnapshot);
                    if (current is null || packet.Snapshot.Revision > current.Revision)
                        Volatile.Write(ref _latestSnapshot, packet.Snapshot);
                }
                else if (string.Equals(
                             packet.Kind,
                             InternetTransportProtocol.GameplaySnapshotKind,
                             StringComparison.Ordinal) && packet.GameplaySnapshot is not null)
                {
                    var current = Volatile.Read(ref _latestGameplaySnapshot);
                    if (current is null || packet.GameplaySnapshot.Revision > current.Revision ||
                        (packet.GameplaySnapshot.Revision == current.Revision &&
                         !string.Equals(packet.GameplaySnapshot.EventCode, current.EventCode,
                             StringComparison.Ordinal)))
                        Volatile.Write(ref _latestGameplaySnapshot, packet.GameplaySnapshot);
                }
                else if (string.Equals(packet.Kind, InternetTransportProtocol.ErrorKind, StringComparison.Ordinal))
                    LastServerCode = packet.Code ?? "NET_SERVER_ERROR";
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException or SocketException or InvalidDataException)
        {
            LastServerCode = "NET_SERVER_DISCONNECTED";
        }
    }

    private async Task WriteLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var packet in _outgoing.Reader.ReadAllAsync(cancellationToken))
            {
                await InternetFrameCodec.WriteAsync(_stream, packet, cancellationToken);
                Interlocked.Increment(ref _sentPacketCount);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException or SocketException or InvalidDataException)
        {
            LastServerCode = "NET_SERVER_DISCONNECTED";
        }
    }

    private static void Validate(InternetClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Address) || options.Address.Length > 253 ||
            options.Address.Any(char.IsWhiteSpace))
            throw new ArgumentException("NET_ADDRESS_INVALID", nameof(options));
        if (options.Port is < 1 or > 65_535) throw new ArgumentOutOfRangeException(nameof(options));
        if (string.IsNullOrWhiteSpace(options.PlayerName) || options.PlayerName.Trim().Length > 40)
            throw new ArgumentException("NET_PLAYER_NAME_INVALID", nameof(options));
        if (string.IsNullOrWhiteSpace(options.ModuleFingerprint))
            throw new ArgumentException("NET_FINGERPRINT_REQUIRED", nameof(options));
        if (string.IsNullOrWhiteSpace(options.ProtocolVersion))
            throw new ArgumentException("NET_PROTOCOL_REQUIRED", nameof(options));
        if (options.ConnectionId == Guid.Empty || options.AccountId == Guid.Empty)
            throw new ArgumentException("NET_IDENTITY_INVALID", nameof(options));
        if (options.RequireTls)
        {
            if (options.PinnedServerCertificateSha256 is not { Length: 64 } fingerprint ||
                fingerprint.Any(character => !Uri.IsHexDigit(character)))
                throw new ArgumentException("NET_SERVER_CERTIFICATE_PIN_INVALID", nameof(options));
            if (string.IsNullOrWhiteSpace(options.AuthenticationToken) ||
                options.AuthenticationToken.Length is < 24 or > 256)
                throw new ArgumentException("NET_AUTH_TOKEN_INVALID", nameof(options));
        }
        else if (!string.IsNullOrEmpty(options.AuthenticationToken))
        {
            throw new ArgumentException("NET_AUTH_TOKEN_REQUIRES_TLS", nameof(options));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        _outgoing.Writer.TryComplete();
        _tcpClient.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        var tasks = new[] { _readTask, _writeTask }.Where(task => task is not null).Cast<Task>().ToArray();
        if (tasks.Length > 0)
        {
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _lifetime.Dispose();
    }
}
