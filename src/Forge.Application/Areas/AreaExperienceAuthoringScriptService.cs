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
    IReadOnlySet<Guid> RevealedPins);

public static class AreaExperienceAuthoringScriptService
{
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
        var cell = $"{MathF.Floor(x / map.ExplorationCellSize):0}:{MathF.Floor(z / map.ExplorationCellSize):0}";
        var cells = state.RevealedCells.ToHashSet(StringComparer.Ordinal);
        cells.Add(cell);
        var pins = state.RevealedPins.ToHashSet();
        if (pinId is { } value)
        {
            if (!map.Pins.Any(item => item.Id == value))
                throw new InvalidOperationException($"Map pin '{value:D}' is not registered in the area.");
            pins.Add(value);
        }
        return state with { RevealedCells = cells, RevealedPins = pins };
    }

    public static IReadOnlyList<DomainDiagnostic> Validate(AreaDefinition area) =>
        AreaExperienceAuthoringValidator.Validate(area);
}
