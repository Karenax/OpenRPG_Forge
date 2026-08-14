using Forge.Application.Combat;
using Forge.Domain.Assets;
using Forge.EngineAdapter.Rendering;
using Godot;

namespace Forge.Runtime;

internal partial class RuntimeCombatImpactPool : Node3D
{
    internal const int ParticleCapacity = 12;
    internal const int AudioCapacity = 8;
    private const int HitStopCapacity = 12;
    private const string ProviderName = "forge-runtime-builtin";

    private static readonly AssetVfxManifest Manifest = new(
        Required: true,
        ChunkId: null,
        Provider: ProviderName,
        MaximumParticles: 32,
        MaximumDrawCalls: 1,
        CullDistance: 45);

    private readonly RuntimeUiMotionPolicy _policy;
    private readonly GpuParticles3D[] _emitters = new GpuParticles3D[ParticleCapacity];
    private readonly AudioStreamPlayer3D[] _audioPlayers = new AudioStreamPlayer3D[AudioCapacity];
    private readonly Action?[] _audioCompletions = new Action?[AudioCapacity];
    private readonly HitStopEntry[] _hitStops = new HitStopEntry[HitStopCapacity];
    private readonly Dictionary<CombatFeedbackAudioKind, AudioStreamWav> _audioStreams = [];
    private ParticleProcessMaterial? _damageMaterial;
    private ParticleProcessMaterial? _healingMaterial;
    private ParticleProcessMaterial? _resourceMaterial;
    private QuadMesh? _particleMesh;
    private CanvasLayer? _screenLayer;
    private ColorRect? _screenAccent;
    private double _screenAccentRemaining;
    private int _particleCursor;
    private int _audioCursor;

    internal RuntimeCombatImpactPool(RuntimeUiMotionPolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        for (var index = 0; index < _hitStops.Length; index++) _hitStops[index] = new HitStopEntry();
    }

    internal int ActiveParticleCount
    {
        get
        {
            var count = 0;
            for (var index = 0; index < _emitters.Length; index++)
                if (_emitters[index]?.Emitting == true) count++;
            return count;
        }
    }

    internal int ActiveAudioCount
    {
        get
        {
            var count = 0;
            for (var index = 0; index < _audioPlayers.Length; index++)
                if (_audioPlayers[index]?.Playing == true) count++;
            return count;
        }
    }

    internal int PeakParticleCount { get; private set; }

    internal int PeakAudioCount { get; private set; }

    internal int RecycledParticleCount { get; private set; }

    internal int RecycledAudioCount { get; private set; }

    internal string LastDiagnosticCode { get; private set; } = string.Empty;

    internal string LastVisualCueKey { get; private set; } = string.Empty;

    internal int LastParticleAmount { get; private set; }

    internal bool LastUsedCueOverride { get; private set; }

    internal bool HitStopActive
    {
        get
        {
            for (var index = 0; index < _hitStops.Length; index++)
                if (_hitStops[index].Active) return true;
            return false;
        }
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        BuildSharedVisualResources();
        for (var index = 0; index < _emitters.Length; index++)
        {
            var emitter = new GpuParticles3D
            {
                Name = $"CombatImpactEmitter{index + 1}",
                Amount = 18,
                Lifetime = .34,
                OneShot = true,
                Explosiveness = 1,
                Randomness = .22f,
                UseFixedSeed = true,
                Seed = (uint)(2026 + index),
                Emitting = false,
                VisibilityAabb = new Aabb(new Vector3(-2, -2, -2), new Vector3(4, 4, 4)),
                ProcessMaterial = _damageMaterial,
                DrawPass1 = _particleMesh,
            };
            _emitters[index] = emitter;
            AddChild(emitter);
        }
        BuildAudioPool();
        BuildScreenAccent();
    }

    public override void _Process(double delta)
    {
        if (_screenAccentRemaining > 0)
        {
            _screenAccentRemaining = Math.Max(0, _screenAccentRemaining - delta);
            if (_screenAccent is not null)
            {
                var alpha = (float)Math.Clamp(_screenAccentRemaining / Math.Max(.01, _policy.ImpactSeconds), 0, 1);
                var color = _screenAccent.Color;
                color.A = .055f * alpha;
                _screenAccent.Color = color;
                _screenAccent.Visible = _screenAccentRemaining > 0;
            }
        }
        AdvanceHitStops(delta);
    }

    internal void Present(CombatFeedbackPresentation presentation, Node3D target, Vector3 viewerPosition)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(target);
        LastVisualCueKey = presentation.VisualCueKey;
        LastUsedCueOverride = false;
        var kind = ResolveImpactKind(presentation);
        var requestedParticles = kind switch
        {
            RuntimeImpactKind.Damage => 20,
            RuntimeImpactKind.Healing => 16,
            RuntimeImpactKind.Resource => 12,
            _ => 10,
        };
        var distance = viewerPosition.DistanceTo(target.GlobalPosition);
        var plan = AssetVfxRenderPlanBuilder.Build(
            new AssetVfxRenderRequest(Manifest, (decimal)distance, requestedParticles, 1,
                IncludeDecal: false, IncludeLight: false, IncludeAudioCue: false),
            BuiltinVfxProvider.Instance);
        LastDiagnosticCode = plan.DiagnosticCode;
        LastParticleAmount = plan.Valid && !plan.Culled && !_policy.ReducedMotion ? plan.ParticleCount : 0;
        if (!plan.Valid || plan.Culled || _policy.ReducedMotion) return;

        var emitter = AcquireEmitter();
        emitter.Amount = plan.ParticleCount;
        emitter.ProcessMaterial = kind switch
        {
            RuntimeImpactKind.Healing => _healingMaterial,
            RuntimeImpactKind.Resource => _resourceMaterial,
            _ => _damageMaterial,
        };
        emitter.GlobalPosition = target.GlobalPosition + Vector3.Up * 1.1f;
        emitter.Emitting = true;
        emitter.Restart();
        PeakParticleCount = Math.Max(PeakParticleCount, ActiveParticleCount);
        ShowScreenAccent(kind);
    }

    internal void PlayAudio(CombatFeedbackAudioKind kind, Vector3 position)
    {
        PlayAudioStream(_audioStreams[kind], position, "SFX", .56f);
    }

    internal void PlayAudioStream(
        AudioStream stream,
        Vector3 position,
        string bus,
        float linearVolume,
        Action? completed = null,
        bool cueOverride = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(bus);
        if (!float.IsFinite(linearVolume) || linearVolume is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(linearVolume));
        var index = AcquireAudioPlayerIndex();
        var player = _audioPlayers[index];
        player.Stream = stream;
        player.GlobalPosition = position;
        player.Bus = AudioServer.GetBusIndex(bus) >= 0 ? bus : "Master";
        player.VolumeDb = Mathf.LinearToDb(linearVolume);
        LastUsedCueOverride = cueOverride;
        _audioCompletions[index] = completed;
        player.Play();
        PeakAudioCount = Math.Max(PeakAudioCount, ActiveAudioCount);
    }

    internal void ApplyHitStop(AnimationPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (_policy.ReducedMotion || _policy.HitStopSeconds <= 0 || !GodotObject.IsInstanceValid(player)) return;
        HitStopEntry? selected = null;
        for (var index = 0; index < _hitStops.Length; index++)
        {
            var entry = _hitStops[index];
            if (entry.Active && entry.Player == player)
            {
                entry.RemainingSeconds = Math.Max(entry.RemainingSeconds, _policy.HitStopSeconds);
                return;
            }
            if (!entry.Active && selected is null) selected = entry;
        }
        if (selected is null) return;
        selected.Active = true;
        selected.Player = player;
        selected.SavedSpeedScale = player.SpeedScale;
        selected.RemainingSeconds = _policy.HitStopSeconds;
        player.SpeedScale = 0;
    }

    internal void SetInterfaceVisible(bool visible)
    {
        Visible = visible;
        if (_screenLayer is not null) _screenLayer.Visible = visible;
    }

    private void BuildSharedVisualResources()
    {
        var meshMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            VertexColorUseAsAlbedo = true,
            AlbedoColor = Colors.White,
        };
        _particleMesh = new QuadMesh
        {
            Size = new Vector2(.13f, .13f),
            Material = meshMaterial,
        };
        _damageMaterial = CreateParticleMaterial(_policy.ResolveColor(RuntimeUiSemanticColor.Damage), 3.8f, 1.2f);
        _healingMaterial = CreateParticleMaterial(_policy.ResolveColor(RuntimeUiSemanticColor.Healing), 2.5f, -.7f);
        _resourceMaterial = CreateParticleMaterial(_policy.ResolveColor(RuntimeUiSemanticColor.Resource), 2.1f, -.4f);
    }

    private static ParticleProcessMaterial CreateParticleMaterial(Color color, float velocity, float gravity)
    {
        color.A = .92f;
        return new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = .18f,
            Direction = Vector3.Up,
            Spread = 72,
            InitialVelocityMin = velocity * .72f,
            InitialVelocityMax = velocity,
            Gravity = new Vector3(0, gravity, 0),
            ScaleMin = .55f,
            ScaleMax = 1.15f,
            Color = color,
        };
    }

    private void BuildAudioPool()
    {
        foreach (var kind in Enum.GetValues<CombatFeedbackAudioKind>())
        {
            var clip = ProceduralCombatFeedbackAudioService.Create(kind);
            _audioStreams[kind] = new AudioStreamWav
            {
                Format = AudioStreamWav.FormatEnum.Format16Bits,
                MixRate = clip.SampleRate,
                Stereo = false,
                LoopMode = AudioStreamWav.LoopModeEnum.Disabled,
                Data = clip.Pcm16Mono,
            };
        }
        var bus = AudioServer.GetBusIndex("SFX") >= 0 ? "SFX" : "Master";
        for (var index = 0; index < _audioPlayers.Length; index++)
        {
            var player = new AudioStreamPlayer3D
            {
                Name = $"CombatSfxPlayer{index + 1}",
                Bus = bus,
                VolumeDb = -7,
                UnitSize = 3,
                MaxDistance = 38,
            };
            var capturedIndex = index;
            player.Finished += () =>
            {
                var completed = _audioCompletions[capturedIndex];
                _audioCompletions[capturedIndex] = null;
                completed?.Invoke();
            };
            _audioPlayers[index] = player;
            AddChild(player);
        }
    }

    private void BuildScreenAccent()
    {
        _screenLayer = new CanvasLayer { Layer = 17 };
        _screenAccent = new ColorRect
        {
            Name = "CombatScreenAccent",
            Color = Colors.Transparent,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        _screenAccent.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _screenLayer.AddChild(_screenAccent);
        AddChild(_screenLayer);
    }

    private GpuParticles3D AcquireEmitter()
    {
        for (var offset = 0; offset < _emitters.Length; offset++)
        {
            var index = (_particleCursor + offset) % _emitters.Length;
            if (_emitters[index].Emitting) continue;
            _particleCursor = (index + 1) % _emitters.Length;
            return _emitters[index];
        }
        var recycled = _emitters[_particleCursor];
        _particleCursor = (_particleCursor + 1) % _emitters.Length;
        recycled.Emitting = false;
        RecycledParticleCount++;
        return recycled;
    }

    private int AcquireAudioPlayerIndex()
    {
        for (var offset = 0; offset < _audioPlayers.Length; offset++)
        {
            var index = (_audioCursor + offset) % _audioPlayers.Length;
            if (_audioPlayers[index].Playing) continue;
            _audioCursor = (index + 1) % _audioPlayers.Length;
            return index;
        }
        var recycledIndex = _audioCursor;
        var recycled = _audioPlayers[recycledIndex];
        _audioCursor = (_audioCursor + 1) % _audioPlayers.Length;
        recycled.Stop();
        var completed = _audioCompletions[recycledIndex];
        _audioCompletions[recycledIndex] = null;
        completed?.Invoke();
        RecycledAudioCount++;
        return recycledIndex;
    }

    private void ShowScreenAccent(RuntimeImpactKind kind)
    {
        if (_screenAccent is null || _policy.ReducedMotion || _policy.ImpactSeconds <= 0) return;
        var color = kind switch
        {
            RuntimeImpactKind.Healing => _policy.ResolveColor(RuntimeUiSemanticColor.Healing),
            RuntimeImpactKind.Resource => _policy.ResolveColor(RuntimeUiSemanticColor.Resource),
            _ => _policy.ResolveColor(RuntimeUiSemanticColor.Damage),
        };
        color.A = .055f;
        _screenAccent.Color = color;
        _screenAccent.Visible = true;
        _screenAccentRemaining = _policy.ImpactSeconds;
    }

    private void AdvanceHitStops(double delta)
    {
        for (var index = 0; index < _hitStops.Length; index++)
        {
            var entry = _hitStops[index];
            if (!entry.Active) continue;
            entry.RemainingSeconds = Math.Max(0, entry.RemainingSeconds - delta);
            if (entry.RemainingSeconds > 0) continue;
            if (entry.Player is not null && GodotObject.IsInstanceValid(entry.Player))
                entry.Player.SpeedScale = entry.SavedSpeedScale;
            entry.Active = false;
            entry.Player = null;
        }
    }

    private static RuntimeImpactKind ResolveImpactKind(CombatFeedbackPresentation presentation)
    {
        for (var index = 0; index < presentation.Amounts.Count; index++)
        {
            switch (presentation.Amounts[index].Kind)
            {
                case CombatFeedbackAmountKind.Damage: return RuntimeImpactKind.Damage;
                case CombatFeedbackAmountKind.Healing: return RuntimeImpactKind.Healing;
                case CombatFeedbackAmountKind.ResourceRestored: return RuntimeImpactKind.Resource;
            }
        }
        return RuntimeImpactKind.Neutral;
    }

    private enum RuntimeImpactKind
    {
        Neutral,
        Damage,
        Healing,
        Resource,
    }

    private sealed class HitStopEntry
    {
        internal bool Active { get; set; }

        internal AnimationPlayer? Player { get; set; }

        internal float SavedSpeedScale { get; set; }

        internal double RemainingSeconds { get; set; }
    }

    private sealed class BuiltinVfxProvider : IAssetVfxRenderProvider
    {
        internal static BuiltinVfxProvider Instance { get; } = new();

        public VfxProviderCapabilities Capabilities { get; } =
            new(ProviderName, Particles: true, Decals: false, Lights: false, AudioCues: false);
    }
}
