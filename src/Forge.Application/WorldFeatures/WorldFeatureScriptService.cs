using Forge.Application.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Application.WorldFeatures;

public sealed class WorldFeatureScriptService
{
    public sealed record Handle(
        int SchemaVersion,
        long Revision,
        string Kind,
        Guid Id,
        string TargetId,
        string Provenance,
        IReadOnlyDictionary<string, string> Properties);

    public IReadOnlyList<Handle> Query(ProjectState state, string? kind = null, string? idOrTarget = null,
        int limit = 100)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        var catalog = state.Project.Module.WorldFeatures;
        var values = new List<Handle>();
        values.AddRange(catalog.Placeables.Select(item => Create(state, "placeable", item.Id,
            item.BlueprintId.ToString(), item.Provenance, new Dictionary<string, string>
            {
                ["faction"] = item.FactionId ?? string.Empty,
                ["events"] = item.Reactive.Events.Count.ToString(),
                ["physics"] = item.Physics?.Mode.ToString() ?? "none",
            })));
        values.AddRange(catalog.Doors.Select(item => Create(state, "door", item.Id,
            item.BlueprintId.ToString(), item.Provenance, new Dictionary<string, string>
            {
                ["damageable"] = (item.Damage is not null).ToString(),
                ["assembly"] = item.Assembly?.Kind.ToString() ?? "single",
                ["events"] = item.Events.Count.ToString(),
            })));
        values.AddRange(catalog.Triggers.Select(item => Create(state, "trigger", item.Id,
            item.BlueprintId.ToString(), item.Provenance, new Dictionary<string, string>
            {
                ["area"] = item.AreaId.ToString(),
                ["discovery"] = (item.Discovery is not null).ToString(),
                ["director"] = (item.Director?.AllowManualFire == true).ToString(),
            })));
        values.AddRange(catalog.Encounters.Select(item => Create(state, "encounter", item.Id,
            item.AreaId.ToString(), item.Provenance, new Dictionary<string, string>
            {
                ["respawn"] = (item.Respawn is not null).ToString(),
                ["reinforcements"] = (item.Reinforcements is not null).ToString(),
                ["visibility"] = item.Visibility.Mode.ToString(),
            })));
        values.AddRange(catalog.VehicleRoutes.Select(item => Create(state, "vehicle-route", item.Id,
            item.AreaId.ToString(), item.Provenance, new Dictionary<string, string>
            {
                ["points"] = item.Points.Count.ToString(),
                ["lanes"] = item.Lanes.Count.ToString(),
                ["speed"] = item.DefaultSpeedLimit.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            })));
        values.AddRange(catalog.Merchants.Select(item => Create(state, "merchant", item.Id,
            item.BlueprintId.ToString(), item.Provenance, new Dictionary<string, string>
            {
                ["currencies"] = item.Currencies.Count.ToString(),
                ["services"] = item.Services.Count.ToString(),
                ["ledger"] = item.Ledger.Enabled.ToString(),
            })));
        values.AddRange(catalog.Companions.Select(item => Create(state, "companion", item.Id,
            item.BlueprintId.ToString(), item.Provenance, new Dictionary<string, string>
            {
                ["formation"] = item.Formation.FormationId,
                ["level"] = item.Progression.CurrentLevel.ToString(),
                ["directControl"] = item.DirectControl.Enabled.ToString(),
            })));
        values.Add(Create(state, "script", catalog.Scripts.Id, state.Project.Module.Id.ToString(),
            catalog.Scripts.Provenance, new Dictionary<string, string>
            {
                ["shots"] = catalog.Scripts.Camera.Shots.Count.ToString(),
                ["tagRoutes"] = catalog.Scripts.TagRouting.Routes.Count.ToString(),
                ["flags"] = catalog.Scripts.ModuleFlags.Count.ToString(),
                ["aiSettings"] = catalog.Scripts.AiSettings.Count.ToString(),
            }));
        return values.Where(item => string.IsNullOrWhiteSpace(kind) || string.Equals(item.Kind, kind, StringComparison.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrWhiteSpace(idOrTarget) || item.Id.ToString().Contains(idOrTarget, StringComparison.OrdinalIgnoreCase) ||
                item.TargetId.Contains(idOrTarget, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Kind, StringComparer.Ordinal).ThenBy(item => item.Id).Take(limit).ToArray();
    }

    private static Handle Create(ProjectState state, string kind, Guid id, string targetId, string provenance,
        IReadOnlyDictionary<string, string> properties) =>
        new(1, state.Revision.Value, kind, id, targetId, provenance, properties);
}
