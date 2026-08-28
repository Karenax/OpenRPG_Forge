using Forge.Network.Transport;
using Godot;

namespace Forge.Runtime;

internal partial class RuntimeInternetMultiplayer : Node3D
{
    private const double PublishIntervalSeconds = .05;
    private readonly Dictionary<Guid, RemoteAvatar> _avatars = [];
    private TcpGameClient? _client;
    private PlayerController? _localPlayer;
    private Func<string?>? _areaId;
    private double _publishElapsed;
    private long _lastSnapshotRevision = -1;
    private int _lastPlayerCount = -1;
    private string? _reportedServerCode;

    internal void Configure(
        TcpGameClient client,
        PlayerController localPlayer,
        Func<string?> areaId)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _localPlayer = localPlayer ?? throw new ArgumentNullException(nameof(localPlayer));
        _areaId = areaId ?? throw new ArgumentNullException(nameof(areaId));
        Name = "InternetMultiplayer";
        ProcessMode = ProcessModeEnum.Always;
    }

    internal void Stop()
    {
        ProcessMode = ProcessModeEnum.Disabled;
        _client = null;
        _localPlayer = null;
        _areaId = null;
    }

    public override void _Process(double delta)
    {
        if (_client is null || _localPlayer is null || _areaId is null) return;
        var areaId = _areaId();
        if (string.IsNullOrWhiteSpace(areaId)) return;

        _publishElapsed += delta;
        if (_publishElapsed >= PublishIntervalSeconds)
        {
            _publishElapsed %= PublishIntervalSeconds;
            var position = _localPlayer.GlobalPosition;
            _client.PublishPose(areaId, position.X, position.Y, position.Z, _localPlayer.Rotation.Y);
        }

        if (_client.LastServerCode is { } serverCode && serverCode != _reportedServerCode)
        {
            _reportedServerCode = serverCode;
            GD.PrintErr($"OPENRPG_FORGE_RUNTIME_INTERNET_WARNING code={serverCode}");
        }

        var snapshot = _client.LatestSnapshot;
        if (snapshot is null) return;
        ApplySnapshot(snapshot, areaId, delta);
    }

    private void ApplySnapshot(InternetWorldSnapshot snapshot, string localAreaId, double delta)
    {
        var remoteStates = snapshot.Players
            .Where(item => item.ConnectionId != _client!.ConnectionId)
            .ToDictionary(item => item.ConnectionId);
        foreach (var removed in _avatars.Keys.Except(remoteStates.Keys).ToArray())
        {
            _avatars[removed].Node.QueueFree();
            _avatars.Remove(removed);
        }

        foreach (var state in remoteStates.Values)
        {
            if (!_avatars.TryGetValue(state.ConnectionId, out var avatar))
            {
                avatar = CreateAvatar(state);
                _avatars.Add(state.ConnectionId, avatar);
            }

            avatar.Node.Visible = string.Equals(state.AreaId, localAreaId, StringComparison.Ordinal);
            if (!avatar.Node.Visible) continue;
            var target = new Vector3((float)state.X, (float)state.Y, (float)state.Z);
            var interpolation = 1f - MathF.Exp(-12f * (float)Math.Clamp(delta, 0, .1));
            avatar.Node.GlobalPosition = avatar.Node.GlobalPosition.Lerp(target, interpolation);
            var rotation = avatar.Node.Rotation;
            rotation.Y = Mathf.LerpAngle(rotation.Y, (float)state.Yaw, interpolation);
            avatar.Node.Rotation = rotation;
        }

        if (snapshot.Revision > _lastSnapshotRevision)
        {
            _lastSnapshotRevision = snapshot.Revision;
            if (snapshot.Players.Count != _lastPlayerCount)
            {
                _lastPlayerCount = snapshot.Players.Count;
                GD.Print(
                    $"OPENRPG_FORGE_RUNTIME_INTERNET_PLAYERS session={snapshot.SessionId:D} " +
                    $"players={snapshot.Players.Count} revision={snapshot.Revision}");
            }
        }
    }

    private RemoteAvatar CreateAvatar(InternetPlayerState state)
    {
        var node = new Node3D
        {
            Name = $"RemotePlayer_{state.ConnectionId:N}",
            GlobalPosition = new Vector3((float)state.X, (float)state.Y, (float)state.Z),
        };
        var bytes = state.ConnectionId.ToByteArray();
        var color = new Color(
            .35f + (bytes[0] / 255f * .45f),
            .35f + (bytes[5] / 255f * .45f),
            .35f + (bytes[10] / 255f * .45f));
        node.AddChild(new MeshInstance3D
        {
            Name = "NetworkAvatarVisual",
            Mesh = new CapsuleMesh { Radius = .45f, Height = 1.8f },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color,
                Roughness = .72f,
                Metallic = .08f,
            },
        });
        node.AddChild(new Label3D
        {
            Name = "NetworkAvatarLabel",
            Text = state.DisplayName,
            Position = new Vector3(0, 1.55f, 0),
            Modulate = new Color("e8f5ff"),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 28,
            OutlineSize = 7,
            NoDepthTest = true,
        });
        AddChild(node);
        return new RemoteAvatar(node);
    }

    private sealed record RemoteAvatar(Node3D Node);
}
