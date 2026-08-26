using Forge.Domain.Areas;
using Forge.Domain.Validation;

namespace Forge.Application.Areas;

public sealed record AreaExperienceAuthoringScriptSnapshot(
    string AreaId,
    string ProfileId,
    IReadOnlyList<string> AudioSnapshots,
    IReadOnlyList<string> EventBindings,
    string MapProjection,
    AreaMapExplorationScope ExplorationScope,
    int MapPins,
    int VisibilityRooms,
    int VisibilityPortals,
    int Occluders,
    int LodOverrides,
    bool FogOfWar,
    bool RuntimeRequired);

public sealed record AreaUserEventDispatch(
    string EventName,
    string PayloadSchema,
    int ApiVersion,
    string ScriptPath,
    IReadOnlyList<string> Subscriptions);

public sealed record AreaMapExplorationState(
    AreaMapExplorationScope Scope,
    string ExplorerId,
    IReadOnlySet<string> RevealedCells,
    IReadOnlySet<Guid> RevealedPins,
    int ActiveFloor = 0);

public sealed record AreaMapExplorationResolution(
    AreaMapExplorationState State,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class AreaExperienceAuthoringScriptService
{
    public const string ActiveFloorOutOfScopeCode = "area.map.active_floor_out_of_scope";
    public static AreaExperienceAuthoringScriptSnapshot Read(AreaDefinition area)
    {
        ArgumentNullException.ThrowIfNull(area);
        var experience = area.Authoring.Experience;
        return new(area.Id.ToString(), experience.Id.ToString("D"),
            experience.AudioSnapshots.Select(item => item.Name).ToArray(),
            area.Authoring.UserEvents.Select(item => item.NamespacedName).ToArray(),
            area.Authoring.Map.Projection, area.Authoring.Map.ExplorationScope,
            area.Authoring.Map.Pins.Count, experience.Visibility.Rooms.Count,
            experience.Visibility.Portals.Count, experience.Visibility.Occluders.Count,
            experience.Lod.AssetOverrides.Count, area.Authoring.Map.FogOfWar,
            experience.RequireRuntimeMetadata);
    }

    public static AreaUserEventDispatch Dispatch(
        AreaDefinition area,
        string namespacedEvent,
        IReadOnlyDictionary<string, object?> payload)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentException.ThrowIfNullOrWhiteSpace(namespacedEvent);
        ArgumentNullException.ThrowIfNull(payload);
        var binding = area.Authoring.UserEvents.SingleOrDefault(item =>
            string.Equals(item.NamespacedName, namespacedEvent, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Area event '{namespacedEvent}' is not registered.");
        if (binding.Required && payload.Count == 0)
            throw new InvalidOperationException($"Area event '{namespacedEvent}' requires a payload.");
        return new(binding.NamespacedName, binding.PayloadSchema, binding.ApiVersion,
            binding.ScriptPath, binding.Subscriptions);
    }

    public static AreaMapExplorationState Reveal(
        AreaDefinition area,
        AreaMapExplorationState state,
        float x,
        float z,
        Guid? pinId = null) => Reveal(area, state, state.ActiveFloor, x, z, pinId);

    public static AreaMapExplorationState Reveal(
        AreaDefinition area,
        AreaMapExplorationState state,
        int activeFloor,
        float x,
        float z,
        Guid? pinId = null)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(state);
        var map = area.Authoring.Map;
        if (!map.Enabled) return state;
        if (state.Scope != map.ExplorationScope)
            throw new InvalidOperationException("Map exploration scope does not match the area policy.");
        if (!float.IsFinite(x) || !float.IsFinite(z))
            throw new ArgumentOutOfRangeException(nameof(x));

        var grid = new AreaMapExplorationGrid(area.Dimensions, map.FloorCount, map.ExplorationCellSize);
        var cell = grid.Resolve(activeFloor, x, z);
        var normalized = Normalize(area, state);
        var cells = normalized.State.RevealedCells.ToHashSet(StringComparer.Ordinal);
        cells.Add(cell.Key);
        var pins = normalized.State.RevealedPins.ToHashSet();
        foreach (var pin in map.Pins.Where(pin => !pin.InitiallyVisible && pin.Floor == activeFloor))
        {
            if (grid.TryResolve(pin.Floor, pin.X, pin.Z, out var pinCell) && pinCell == cell)
                pins.Add(pin.Id);
        }
        if (pinId is { } value)
        {
            if (!map.Pins.Any(item => item.Id == value))
                throw new InvalidOperationException($"Map pin '{value:D}' is not registered in the area.");
            pins.Add(value);
        }
        return normalized.State with
        {
            RevealedCells = cells,
            RevealedPins = pins,
            ActiveFloor = activeFloor,
        };
    }

    public static AreaMapExplorationResolution Normalize(
        AreaDefinition area,
        AreaMapExplorationState state)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(state);
        var map = area.Authoring.Map;
        if (state.Scope != map.ExplorationScope)
            throw new InvalidOperationException("Map exploration scope does not match the area policy.");
        var diagnostics = new List<DomainDiagnostic>();
        var activeFloor = state.ActiveFloor;
        if (activeFloor < 0 || activeFloor >= map.FloorCount)
        {
            diagnostics.Add(new DomainDiagnostic(
                ActiveFloorOutOfScopeCode,
                $"Map floor '{activeFloor}' is outside the active area policy; floor 0 was restored.",
                "activeFloor"));
            activeFloor = 0;
        }
        if (!map.Enabled)
            return new AreaMapExplorationResolution(state with { ActiveFloor = activeFloor }, diagnostics);

        var grid = new AreaMapExplorationGrid(area.Dimensions, map.FloorCount, map.ExplorationCellSize);
        var validation = grid.ValidateKeys(state.RevealedCells);
        diagnostics.AddRange(validation.Diagnostics);
        return new AreaMapExplorationResolution(
            state with { RevealedCells = validation.ValidCells, ActiveFloor = activeFloor },
            diagnostics);
    }

    public static IReadOnlyList<DomainDiagnostic> Validate(AreaDefinition area) =>
        AreaExperienceAuthoringValidator.Validate(area);
}
