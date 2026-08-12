using Forge.Domain.Gameplay;
using Forge.Domain.Modules;
using Forge.Domain.Spatial;

namespace Forge.Application.Gameplay;

public readonly record struct GameplaySimulationHandle(ModuleId ModuleId, Guid ProfileId, int Generation);
public readonly record struct FlightSimulationRef(ModuleId ModuleId, Guid ProfileId, int Generation);
public readonly record struct ProjectileSimulationRef(ModuleId ModuleId, Guid ProfileId, int Generation);
public readonly record struct PhysicsSimulationRef(ModuleId ModuleId, Guid ProfileId, int Generation);
public readonly record struct TargetingSimulationRef(ModuleId ModuleId, Guid ProfileId, int Generation);
public readonly record struct WorldTimeSimulationRef(ModuleId ModuleId, Guid ProfileId, int Generation);

public sealed record GameplaySimulationScriptSnapshot(Guid Id, bool Enabled, bool RequiredAtRuntime,
    Guid FlightId, Guid ProjectileId, Guid PhysicsId, Guid TargetingId, Guid WorldTimeId,
    string Provenance);

public sealed class GameplaySimulationScriptService
{
    private const int Generation = 1;
    private readonly ModuleId _moduleId;
    private readonly GameplaySimulationProfile _profile;

    public GameplaySimulationScriptService(ModuleId moduleId, GameplaySimulationProfile profile)
    {
        _moduleId = moduleId ?? throw new ArgumentNullException(nameof(moduleId));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Handle = new(moduleId, profile.Id, Generation);
    }

    public GameplaySimulationHandle Handle { get; }

    public GameplaySimulationScriptSnapshot Read(GameplaySimulationHandle handle)
    {
        Validate(handle);
        return new(_profile.Id, _profile.Enabled, _profile.RequiredAtRuntime, _profile.Flight.Id,
            _profile.Projectiles.Id, _profile.Physics.Id, _profile.Targeting.Id, _profile.WorldTime.Id,
            _profile.Provenance);
    }

    public FlightSimulationRef Flight(GameplaySimulationHandle handle)
    {
        Validate(handle);
        return new(_moduleId, _profile.Flight.Id, Generation);
    }

    public ProjectileSimulationRef Projectiles(GameplaySimulationHandle handle)
    {
        Validate(handle);
        return new(_moduleId, _profile.Projectiles.Id, Generation);
    }

    public PhysicsSimulationRef Physics(GameplaySimulationHandle handle)
    {
        Validate(handle);
        return new(_moduleId, _profile.Physics.Id, Generation);
    }

    public TargetingSimulationRef Targeting(GameplaySimulationHandle handle)
    {
        Validate(handle);
        return new(_moduleId, _profile.Targeting.Id, Generation);
    }

    public WorldTimeSimulationRef WorldTime(GameplaySimulationHandle handle)
    {
        Validate(handle);
        return new(_moduleId, _profile.WorldTime.Id, Generation);
    }

    public FlyingMovementResolution PreviewFlight(FlightSimulationRef reference,
        GameplayVectorState state, Vector3Value input, float deltaSeconds, FlightNavigationVolume volume)
    {
        Validate(reference.ModuleId, reference.ProfileId, reference.Generation, _profile.Flight.Id,
            "FLIGHT_SIMULATION_HANDLE_INVALID");
        return GameplaySimulation.FlyStep(state, _profile.Flight, input, deltaSeconds, volume);
    }

    public ProjectileResolution Launch(ProjectileSimulationRef reference, ProjectileRuntimeState state,
        Vector3Value target, float deltaSeconds, bool authorityGranted)
    {
        Validate(reference.ModuleId, reference.ProfileId, reference.Generation, _profile.Projectiles.Id,
            "PROJECTILE_SIMULATION_HANDLE_INVALID");
        return GameplaySimulation.StepProjectile(state, _profile.Projectiles, target, deltaSeconds,
            authorityGranted);
    }

    public PhysicsSceneResolution StepPhysics(PhysicsSimulationRef reference,
        IReadOnlyList<PhysicsBodyRuntimeState> states, float deltaSeconds, bool authorityGranted)
    {
        Validate(reference.ModuleId, reference.ProfileId, reference.Generation, _profile.Physics.Id,
            "PHYSICS_SIMULATION_HANDLE_INVALID");
        return GameplaySimulation.SimulatePhysics(_profile.Physics, states, deltaSeconds, authorityGranted);
    }

    public TargetingPreview PreviewTarget(TargetingSimulationRef reference, Vector3Value origin,
        Vector3Value direction, IReadOnlyList<TargetCandidate> candidates)
    {
        Validate(reference.ModuleId, reference.ProfileId, reference.Generation, _profile.Targeting.Id,
            "TARGETING_SIMULATION_HANDLE_INVALID");
        return GameplaySimulation.PreviewTargeting(_profile.Targeting, origin, direction, candidates);
    }

    public WorldSimulationResolution AdvanceWorld(WorldTimeSimulationRef reference, WorldClockState clock,
        float realMinutes, string? areaId, IReadOnlySet<string>? pausedDomains, bool authorityGranted)
    {
        Validate(reference.ModuleId, reference.ProfileId, reference.Generation, _profile.WorldTime.Id,
            "WORLD_TIME_SIMULATION_HANDLE_INVALID");
        if (_profile.WorldTime.Schedules.Any(item => item.ServerAuthoritative) && !authorityGranted)
            throw new InvalidOperationException("WORLD_TIME_SIMULATION_AUTHORITY_REQUIRED");
        return GameplaySimulation.AdvanceWorld(_profile.WorldTime, clock, realMinutes, areaId, pausedDomains);
    }

    private void Validate(GameplaySimulationHandle handle) => Validate(handle.ModuleId, handle.ProfileId,
        handle.Generation, _profile.Id, "GAMEPLAY_SIMULATION_HANDLE_INVALID");

    private void Validate(ModuleId moduleId, Guid profileId, int generation, Guid expectedId, string code)
    {
        if (moduleId != _moduleId || profileId != expectedId || generation != Generation)
            throw new InvalidOperationException(code);
    }
}
