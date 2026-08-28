using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Forge.Network.Sessions;

namespace Forge.Network.Transport;

public sealed class TcpAuthoritativeServer : IAsyncDisposable, IDisposable
{
    private const double MaximumCoordinateMagnitude = 100_000;
    private const double MaximumPoseStep = 50;
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);

    private readonly object _gate = new();
    private readonly DedicatedServerConfiguration _configuration;
    private readonly AuthoritativeSession _session;
    private readonly InternetServerSecurityOptions? _security;
    private readonly InternetAuthoritativeGameplay _gameplay;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly ConcurrentDictionary<int, Task> _clientTasks = new();
    private readonly Dictionary<Guid, ClientConnection> _connections = [];
    private TcpListener? _listener;
    private Task? _acceptTask;
    private int _nextClientTaskId;
    private long _worldRevision;
    private bool _disposed;

    public TcpAuthoritativeServer(
        DedicatedServerConfiguration configuration,
        Guid? sessionId = null,
        InternetServerSecurityOptions? security = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var diagnostics = NetworkValidation.Validate(configuration);
        if (diagnostics.Count > 0) throw new ArgumentException(diagnostics[0].Code, nameof(configuration));
        if (!IPAddress.TryParse(configuration.BindAddress, out _))
            throw new ArgumentException("NET_BIND_ADDRESS_INVALID", nameof(configuration));
        _configuration = configuration;
        _session = new AuthoritativeSession(configuration, sessionId);
        _security = security;
        _gameplay = new InternetAuthoritativeGameplay(_session.SessionId);
    }

    public Guid SessionId => _session.SessionId;

    public bool IsRunning => _listener is not null && !_disposed;

    public bool IsSecure => _security is not null;

    public string? CertificateSha256 => _security?.CertificateSha256;

    public int ConnectedPlayers
    {
        get
        {
            lock (_gate) return _connections.Count;
        }
    }

    public string? LastTransportCode { get; private set; }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_listener is not null) throw new InvalidOperationException("NET_SERVER_ALREADY_STARTED");
        var address = IPAddress.Parse(_configuration.BindAddress);
        var listener = new TcpListener(address, _configuration.Port);
        listener.Start(_configuration.Capacity);
        _listener = listener;
        // The server can be created from an engine synchronization context (Godot's main
        // thread in particular). Keep the transport lifetime on the thread pool so a long
        // synchronous scene load cannot stall accepts, TLS handshakes, or broadcasts.
        _acceptTask = Task.Run(() => AcceptLoopAsync(listener, _lifetime.Token));
    }

    public async Task WaitForShutdownAsync(CancellationToken cancellationToken = default)
    {
        if (_acceptTask is null) throw new InvalidOperationException("NET_SERVER_NOT_STARTED");
        await _acceptTask.WaitAsync(cancellationToken);
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken);
                client.NoDelay = true;
                var taskId = Interlocked.Increment(ref _nextClientTaskId);
                var task = HandleClientAsync(client, cancellationToken);
                _clientTasks[taskId] = task;
                _ = task.ContinueWith(
                    _ => RemoveClientTask(taskId),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (SocketException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken serverCancellation)
    {
        ClientConnection? connection = null;
        var sessionConnected = false;
        try
        {
            await using var stream = await CreateTransportStreamAsync(client.GetStream(), serverCancellation);
            using var handshake = CancellationTokenSource.CreateLinkedTokenSource(serverCancellation);
            handshake.CancelAfter(HandshakeTimeout);
            var packet = await InternetFrameCodec.ReadAsync<InternetClientPacket>(stream, handshake.Token);
            var helloCode = ValidateHello(packet);
            if (helloCode is not null)
            {
                await SendWelcomeAsync(stream, false, helloCode, Guid.Empty, serverCancellation);
                return;
            }

            var hello = packet!.Hello!;
            AuthorityDecision decision;
            lock (_gate)
            {
                decision = _session.Connect(
                    hello.ConnectionId,
                    new SessionIdentity(hello.AccountId, hello.DisplayName.Trim(), SessionRole.Player),
                    hello.ModuleFingerprint,
                    hello.ProtocolVersion);
                if (decision.Accepted)
                {
                    var gameplayDecision = _gameplay.Connect(hello.ConnectionId, hello.AccountId);
                    if (!gameplayDecision.Accepted)
                    {
                        _ = _session.Disconnect(hello.ConnectionId);
                        decision = new AuthorityDecision(
                            false,
                            gameplayDecision.Code,
                            _session.Snapshot.Revision,
                            _session.Snapshot,
                            $"rejected:{gameplayDecision.Code}:connection={hello.ConnectionId}");
                    }
                    else
                    {
                        connection = new ClientConnection(
                            client, stream, hello.ConnectionId, hello.DisplayName.Trim());
                        sessionConnected = true;
                    }
                }
            }

            await SendWelcomeAsync(
                stream,
                decision.Accepted,
                decision.Code,
                hello.ConnectionId,
                serverCancellation);
            if (!decision.Accepted) return;
            var activeConnection = connection
                ?? throw new InvalidOperationException("NET_CONNECTION_REGISTRATION_FAILED");
            lock (_gate)
            {
                _connections.Add(activeConnection.ConnectionId, activeConnection);
                _worldRevision = Math.Max(_worldRevision + 1, decision.Revision);
            }
            await BroadcastSnapshotAsync(serverCancellation);
            await BroadcastGameplayAsync(serverCancellation);

            while (!serverCancellation.IsCancellationRequested)
            {
                packet = await InternetFrameCodec.ReadAsync<InternetClientPacket>(stream, serverCancellation);
                if (packet is null) break;
                if (packet.SchemaVersion != InternetTransportProtocol.SchemaVersion)
                {
                    await activeConnection.SendAsync(Error("NET_TRANSPORT_SCHEMA_MISMATCH"), serverCancellation);
                    continue;
                }

                switch (packet.Kind)
                {
                    case InternetTransportProtocol.PlayerPoseKind:
                        var poseCode = ApplyPose(activeConnection, packet.Pose);
                        if (poseCode is not null)
                        {
                            LastTransportCode = poseCode;
                            await activeConnection.SendAsync(Error(poseCode), serverCancellation);
                        }
                        else
                            await BroadcastSnapshotAsync(serverCancellation);
                        break;
                    case InternetTransportProtocol.GameplayIntentKind:
                        var gameplayCode = ApplyGameplayIntent(activeConnection, packet.GameplayIntent);
                        if (gameplayCode is not null)
                        {
                            LastTransportCode = gameplayCode;
                            await activeConnection.SendAsync(Error(gameplayCode), serverCancellation);
                        }
                        await BroadcastGameplayAsync(serverCancellation);
                        break;
                    case InternetTransportProtocol.PingKind:
                        await activeConnection.SendAsync(
                            new InternetServerPacket(
                                InternetTransportProtocol.SchemaVersion,
                                InternetTransportProtocol.PongKind),
                            serverCancellation);
                        break;
                    default:
                        await activeConnection.SendAsync(Error("NET_PACKET_KIND_INVALID"), serverCancellation);
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (serverCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException or SocketException or InvalidDataException or
                                                   AuthenticationException)
        {
            LastTransportCode = exception.Message;
        }
        finally
        {
            if (connection is not null)
            {
                var removed = false;
                lock (_gate)
                {
                    removed = _connections.Remove(connection.ConnectionId);
                    if (sessionConnected)
                    {
                        _ = _session.Disconnect(connection.ConnectionId);
                        _gameplay.Disconnect(connection.ConnectionId);
                        if (removed) _worldRevision++;
                    }
                }

                if (removed && !serverCancellation.IsCancellationRequested)
                {
                    try
                    {
                        await BroadcastSnapshotAsync(serverCancellation);
                        await BroadcastGameplayAsync(serverCancellation);
                    }
                    catch (Exception exception) when (exception is IOException or SocketException or
                                                       OperationCanceledException or ObjectDisposedException)
                    {
                    }
                }
            }

            client.Dispose();
        }
    }

    private void RemoveClientTask(int taskId) => _clientTasks.TryRemove(taskId, out Task? _);

    private async Task<Stream> CreateTransportStreamAsync(
        NetworkStream networkStream,
        CancellationToken cancellationToken)
    {
        if (_security is null) return networkStream;
        var secureStream = new SslStream(networkStream, leaveInnerStreamOpen: false);
        try
        {
            await secureStream.AuthenticateAsServerAsync(
                new SslServerAuthenticationOptions
                {
                    ServerCertificate = _security.ServerCertificate,
                    ClientCertificateRequired = false,
                    EnabledSslProtocols = _security.EnabledProtocols,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                },
                cancellationToken);
            return secureStream;
        }
        catch
        {
            await secureStream.DisposeAsync();
            throw;
        }
    }

    private string? ApplyPose(ClientConnection connection, InternetPlayerPose? pose)
    {
        if (pose is null || pose.Sequence < 1 || string.IsNullOrWhiteSpace(pose.AreaId) || pose.AreaId.Length > 100)
            return "NET_POSE_INVALID";
        if (!IsFiniteAndBounded(pose.X) || !IsFiniteAndBounded(pose.Y) || !IsFiniteAndBounded(pose.Z) ||
            !double.IsFinite(pose.Yaw))
            return "NET_POSE_INVALID";

        lock (_gate)
        {
            if (!_connections.ContainsKey(connection.ConnectionId)) return "NET_CONNECTION_UNKNOWN";
            if (connection.LastPose is { } previous)
            {
                if (pose.Sequence <= previous.Sequence) return "NET_POSE_SEQUENCE_STALE";
                if (string.Equals(previous.AreaId, pose.AreaId, StringComparison.Ordinal) &&
                    SquaredDistance(previous, pose) > MaximumPoseStep * MaximumPoseStep)
                    return "NET_POSE_STEP_REJECTED";
            }

            connection.LastPose = pose with { AreaId = pose.AreaId.Trim() };
            _worldRevision++;
        }

        return null;
    }

    private string? ApplyGameplayIntent(
        ClientConnection connection,
        InternetGameplayIntent? intent)
    {
        lock (_gate)
        {
            if (!_connections.ContainsKey(connection.ConnectionId)) return "NET_CONNECTION_UNKNOWN";
            var decision = _gameplay.Apply(
                connection.ConnectionId,
                intent,
                connectionId => _connections.GetValueOrDefault(connectionId)?.LastPose);
            return decision.Accepted ? null : decision.Code;
        }
    }

    private async Task SendWelcomeAsync(
        Stream stream,
        bool accepted,
        string code,
        Guid connectionId,
        CancellationToken cancellationToken)
    {
        long revision;
        lock (_gate) revision = _worldRevision;
        await InternetFrameCodec.WriteAsync(
            stream,
            new InternetServerPacket(
                InternetTransportProtocol.SchemaVersion,
                InternetTransportProtocol.WelcomeKind,
                new InternetTransportWelcome(
                    accepted,
                    code,
                    SessionId,
                    connectionId,
                    revision,
                    Encrypted: _security is not null,
                    Authenticated: accepted && _security is not null,
                    CertificateSha256: _security?.CertificateSha256)),
            cancellationToken);
    }

    private async Task BroadcastSnapshotAsync(CancellationToken cancellationToken)
    {
        ClientConnection[] connections;
        InternetWorldSnapshot snapshot;
        lock (_gate)
        {
            connections = _connections.Values.ToArray();
            snapshot = new InternetWorldSnapshot(
                SessionId,
                _worldRevision,
                connections
                    .Where(item => item.LastPose is not null)
                    .Select(item =>
                    {
                        var pose = item.LastPose!;
                        return new InternetPlayerState(
                            item.ConnectionId,
                            item.DisplayName,
                            pose.Sequence,
                            pose.AreaId,
                            pose.X,
                            pose.Y,
                            pose.Z,
                            pose.Yaw);
                    })
                    .OrderBy(item => item.ConnectionId)
                    .ToArray());
        }

        if (connections.Length == 0) return;
        var packet = new InternetServerPacket(
            InternetTransportProtocol.SchemaVersion,
            InternetTransportProtocol.SnapshotKind,
            Snapshot: snapshot);
        await Task.WhenAll(connections.Select(async item =>
        {
            try
            {
                await item.SendAsync(packet, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException)
            {
                item.Dispose();
            }
        }));
    }

    private async Task BroadcastGameplayAsync(CancellationToken cancellationToken)
    {
        (ClientConnection Connection, InternetGameplaySnapshot Snapshot)[] packets;
        lock (_gate)
        {
            packets = _connections.Values
                .OrderBy(item => item.ConnectionId)
                .Select(item => (item, _gameplay.Snapshot(item.ConnectionId)))
                .ToArray();
        }

        await Task.WhenAll(packets.Select(async item =>
        {
            try
            {
                await item.Connection.SendAsync(
                    new InternetServerPacket(
                        InternetTransportProtocol.SchemaVersion,
                        InternetTransportProtocol.GameplaySnapshotKind,
                        GameplaySnapshot: item.Snapshot),
                    cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException)
            {
                item.Connection.Dispose();
            }
        }));
    }

    private string? ValidateHello(InternetClientPacket? packet)
    {
        if (packet is null || packet.SchemaVersion != InternetTransportProtocol.SchemaVersion ||
            !string.Equals(packet.Kind, InternetTransportProtocol.HelloKind, StringComparison.Ordinal) ||
            packet.Hello is null)
            return "NET_HELLO_INVALID";
        var hello = packet.Hello;
        if (hello.ConnectionId == Guid.Empty || hello.AccountId == Guid.Empty ||
            string.IsNullOrWhiteSpace(hello.DisplayName) || hello.DisplayName.Trim().Length > 40 ||
            string.IsNullOrWhiteSpace(hello.ModuleFingerprint) ||
            string.IsNullOrWhiteSpace(hello.ProtocolVersion))
            return "NET_HELLO_INVALID";
        return _security?.Authenticate(hello.AccountId, hello.AuthenticationToken);
    }

    private static InternetServerPacket Error(string code) => new(
        InternetTransportProtocol.SchemaVersion,
        InternetTransportProtocol.ErrorKind,
        Code: code);

    private static bool IsFiniteAndBounded(double value) =>
        double.IsFinite(value) && Math.Abs(value) <= MaximumCoordinateMagnitude;

    private static double SquaredDistance(InternetPlayerPose left, InternetPlayerPose right)
    {
        var x = left.X - right.X;
        var y = left.Y - right.Y;
        var z = left.Z - right.Z;
        return (x * x) + (y * y) + (z * z);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        _listener?.Stop();
        _listener = null;
        ClientConnection[] connections;
        lock (_gate)
        {
            connections = _connections.Values.ToArray();
            _connections.Clear();
        }
        foreach (var connection in connections) connection.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        if (_acceptTask is not null)
        {
            try
            {
                await _acceptTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        var clientTasks = _clientTasks.Values.ToArray();
        if (clientTasks.Length > 0)
        {
            try
            {
                await Task.WhenAll(clientTasks);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _lifetime.Dispose();
    }

    private sealed class ClientConnection(
        TcpClient client,
        Stream stream,
        Guid connectionId,
        string displayName) : IDisposable
    {
        private readonly SemaphoreSlim _sendGate = new(1, 1);
        private bool _disposed;

        internal Guid ConnectionId { get; } = connectionId;

        internal string DisplayName { get; } = displayName;

        internal InternetPlayerPose? LastPose { get; set; }

        internal async Task SendAsync(InternetServerPacket packet, CancellationToken cancellationToken)
        {
            await _sendGate.WaitAsync(cancellationToken);
            try
            {
                if (_disposed) throw new ObjectDisposedException(nameof(ClientConnection));
                await InternetFrameCodec.WriteAsync(stream, packet, cancellationToken);
            }
            finally
            {
                _sendGate.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            client.Dispose();
        }
    }
}
