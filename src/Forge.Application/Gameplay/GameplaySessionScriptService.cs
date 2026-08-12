using Forge.Domain.Gameplay;
using Forge.Domain.Modules;

namespace Forge.Application.Gameplay;

public readonly record struct GameplaySessionHandle(ModuleId ModuleId, Guid ProfileId, int Generation);
public readonly record struct PersistencePolicyRef(ModuleId ModuleId, Guid ProfileId, int Generation);
public readonly record struct CheckpointRef(ModuleId ModuleId, Guid ProfileId, int Generation);
public readonly record struct RuntimeShellRef(ModuleId ModuleId, Guid ProfileId, int Generation);
public readonly record struct ControlLockRef(ModuleId ModuleId, Guid ProfileId, int Generation);
public readonly record struct ReplayTestRef(ModuleId ModuleId, Guid ProfileId, int Generation);

public sealed record GameplaySessionScriptSnapshot(Guid Id, string PersistenceScope, int CheckpointCount,
    string ShellKit, int ControlContextCount, bool ReplayEnabled, string Provenance);

public sealed class GameplaySessionScriptService
{
    private const int Generation = 1;
    private readonly ModuleId _moduleId;
    private readonly GameplaySessionProfile _profile;

    public GameplaySessionScriptService(ModuleId moduleId, GameplaySessionProfile profile)
    {
        _moduleId = moduleId ?? throw new ArgumentNullException(nameof(moduleId));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Handle = new(moduleId, profile.Id, Generation);
    }

    public GameplaySessionHandle Handle { get; }

    public GameplaySessionScriptSnapshot Read(GameplaySessionHandle handle)
    {
        Validate(handle.ModuleId, handle.ProfileId, handle.Generation, _profile.Id,
            "GAMEPLAY_SESSION_HANDLE_INVALID");
        return new(_profile.Id, _profile.Persistence.Scope.ToString(), _profile.Checkpoints.Count,
            _profile.Shell.KitId, _profile.ControlLocks.Contexts.Count, _profile.Replay.Enabled,
            _profile.Provenance);
    }

    public PersistencePolicyRef Persistence(GameplaySessionHandle handle)
    {
        Read(handle);
        return new(_moduleId, _profile.Persistence.Id, Generation);
    }

    public IReadOnlyList<CheckpointRef> Checkpoints(GameplaySessionHandle handle)
    {
        Read(handle);
        return _profile.Checkpoints.Select(item => new CheckpointRef(_moduleId, item.Id, Generation)).ToArray();
    }

    public RuntimeShellRef Shell(GameplaySessionHandle handle)
    {
        Read(handle);
        return new(_moduleId, _profile.Shell.Id, Generation);
    }

    public ControlLockRef ControlLocks(GameplaySessionHandle handle)
    {
        Read(handle);
        return new(_moduleId, _profile.ControlLocks.Id, Generation);
    }

    public ReplayTestRef Replay(GameplaySessionHandle handle)
    {
        Read(handle);
        return new(_moduleId, _profile.Replay.Id, Generation);
    }

    public RespawnResolution Respawn(CheckpointRef reference, RespawnRequest request, bool authorityGranted)
    {
        var checkpoint = _profile.Checkpoints.SingleOrDefault(item => item.Id == reference.ProfileId);
        Validate(reference.ModuleId, reference.ProfileId, reference.Generation, checkpoint?.Id ?? Guid.Empty,
            "GAMEPLAY_CHECKPOINT_HANDLE_INVALID");
        return GameplaySessionServices.Respawn(checkpoint!, request, authorityGranted);
    }

    public RuntimeShellSnapshot ReadShell(RuntimeShellRef reference)
    {
        Validate(reference.ModuleId, reference.ProfileId, reference.Generation, _profile.Shell.Id,
            "GAMEPLAY_SHELL_HANDLE_INVALID");
        return GameplaySessionServices.ResolveShell(_profile.Shell);
    }

    public ReplayHeadlessReport ReplayHeadless(ReplayTestRef reference, ReplayRecording recording,
        Func<ReplayFrame, string> stateProvider, bool authorityGranted)
    {
        Validate(reference.ModuleId, reference.ProfileId, reference.Generation, _profile.Replay.Id,
            "GAMEPLAY_REPLAY_HANDLE_INVALID");
        if (!authorityGranted) throw new InvalidOperationException("GAME_REPLAY_AUTHORITY_REQUIRED");
        return GameplaySessionServices.ReplayHeadless(_profile.Replay, recording, stateProvider);
    }

    private void Validate(ModuleId moduleId, Guid id, int generation, Guid expected, string code)
    {
        if (moduleId != _moduleId || id == Guid.Empty || id != expected || generation != Generation)
            throw new InvalidOperationException(code);
    }
}
