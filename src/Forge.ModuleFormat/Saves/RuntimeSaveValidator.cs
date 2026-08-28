using Forge.Domain.Areas;

namespace Forge.ModuleFormat.Saves;

public static class RuntimeSaveValidator
{
    public const string VersionUnsupportedCode = "save.version_unsupported";
    public const string ModuleIdInvalidCode = "save.module_id_invalid";
    public const string ContentHashInvalidCode = "save.content_hash_invalid";
    public const string AreaIdInvalidCode = "save.area_id_invalid";
    public const string PlayerInvalidCode = "save.player_invalid";
    public const string InventoryDuplicateCode = "save.inventory_duplicate";
    public const string InventoryQuantityInvalidCode = "save.inventory_quantity_invalid";
    public const string VariableInvalidCode = "save.variable_invalid";
    public const string HostileInvalidCode = "save.hostile_invalid";
    public const string QuestReferenceInvalidCode = "save.quest_reference_invalid";
    public const string WeatherInvalidCode = "save.weather_invalid";
    public const string ProgressionInvalidCode = "save.progression_invalid";
    public const string EntitySnapshotInvalidCode = "save.entity_snapshot_invalid";
    public const string WorldClockInvalidCode = "save.world_clock_invalid";
    public const string MapExplorationInvalidCode = "save.map_exploration_invalid";
    public const string AreaStateInvalidCode = "save.area_state_invalid";

    public static IReadOnlyList<RuntimeSaveDiagnostic> Validate(RuntimeSaveDocument save)
    {
        ArgumentNullException.ThrowIfNull(save);
        var diagnostics = new List<RuntimeSaveDiagnostic>();
        if (!string.Equals(save.FormatVersion, RuntimeSaveDocument.CurrentFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(save.FormatVersion, RuntimeSaveDocument.PreAreaStateCollectionFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(save.FormatVersion, RuntimeSaveDocument.PreMapExplorationFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(save.FormatVersion, RuntimeSaveDocument.PreQuestCollectionFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(save.FormatVersion, RuntimeSaveDocument.PreInventoryStacksFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(save.FormatVersion, RuntimeSaveDocument.PreWorldClockFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(save.FormatVersion, RuntimeSaveDocument.PreRuntimeExtensionsFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(save.FormatVersion, RuntimeSaveDocument.PreRulesFormatVersion, StringComparison.Ordinal))
        {
            diagnostics.Add(new RuntimeSaveDiagnostic(
                VersionUnsupportedCode,
                $"Save format '{save.FormatVersion}' is unsupported; expected '{RuntimeSaveDocument.CurrentFormatVersion}'.",
                "formatVersion"));
        }

        if (!Guid.TryParse(save.ModuleId, out var moduleId) || moduleId == Guid.Empty)
        {
            diagnostics.Add(new RuntimeSaveDiagnostic(ModuleIdInvalidCode, "The module ID must be a non-empty UUID.", "moduleId"));
        }

        if (save.ModuleContentSha256.Length != 64 ||
            save.ModuleContentSha256.Any(character => !char.IsAsciiHexDigit(character)))
        {
            diagnostics.Add(new RuntimeSaveDiagnostic(
                ContentHashInvalidCode,
                "The module content hash must contain 64 hexadecimal characters.",
                "moduleContentSha256"));
        }

        if (!Guid.TryParse(save.AreaId, out var areaId) || areaId == Guid.Empty)
        {
            diagnostics.Add(new RuntimeSaveDiagnostic(AreaIdInvalidCode, "The area ID must be a non-empty UUID.", "areaId"));
        }

        if (save.Player is not null)
        {
            ValidateCombatant(save.Player, "player", PlayerInvalidCode, diagnostics);
        }
        if (save.Inventory.Any(stack => string.IsNullOrWhiteSpace(stack.Slug)) ||
            save.Inventory.Select(stack => stack.Slug).Distinct(StringComparer.Ordinal).Count() != save.Inventory.Count)
        {
            diagnostics.Add(new RuntimeSaveDiagnostic(
                InventoryDuplicateCode,
                "Inventory stack slugs must be non-empty and unique.",
                "inventory"));
        }

        if (save.Inventory.Any(stack => stack.Quantity <= 0))
        {
            diagnostics.Add(new RuntimeSaveDiagnostic(
                InventoryQuantityInvalidCode,
                "Inventory stack quantities must be positive.",
                "inventory"));
        }

        if (save.Variables.Any(item => string.IsNullOrWhiteSpace(item.Key)) ||
            save.Variables.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() != save.Variables.Count)
        {
            diagnostics.Add(new RuntimeSaveDiagnostic(
                VariableInvalidCode,
                "Variable keys must be non-empty and unique.",
                "variables"));
        }

        ValidateHostiles(save.Hostiles, "hostiles", diagnostics);

        if ((save.QuestId is null) != (save.QuestStateId is null) ||
            save.QuestId is not null &&
            (!Guid.TryParse(save.QuestId, out _) || !Guid.TryParse(save.QuestStateId, out _)))
        {
            diagnostics.Add(new RuntimeSaveDiagnostic(
                QuestReferenceInvalidCode,
                "Quest ID and state ID must both be absent or valid UUIDs.",
                "questStateId"));
        }

        var questStates = save.QuestStates ?? [];
        var parsedQuestStates = questStates.Select(item =>
        {
            Guid? questId = Guid.TryParse(item.QuestId, out var parsedQuestId) && parsedQuestId != Guid.Empty
                ? parsedQuestId
                : null;
            Guid? stateId = Guid.TryParse(item.StateId, out var parsedStateId) && parsedStateId != Guid.Empty
                ? parsedStateId
                : null;
            return (Item: item, QuestId: questId, StateId: stateId);
        }).ToArray();
        var invalidQuestStates = parsedQuestStates.Any(item => item.QuestId is null || item.StateId is null) ||
            parsedQuestStates.Select(item => item.QuestId)
                .Distinct().Count() != parsedQuestStates.Length;
        var trackedQuestId = Guid.Empty;
        var trackedQuestIdValid = save.TrackedQuestId is null ||
            Guid.TryParse(save.TrackedQuestId, out trackedQuestId) && trackedQuestId != Guid.Empty;
        var trackedMatches = save.TrackedQuestId is null || !trackedQuestIdValid
            ? []
            : parsedQuestStates.Where(item => item.QuestId == trackedQuestId)
                .Select(item => item.Item)
                .ToArray();
        var trackedState = trackedMatches.Length == 1 ? trackedMatches[0] : null;
        if (invalidQuestStates || !trackedQuestIdValid ||
            save.TrackedQuestId is not null && (trackedState is null || !trackedState.IsAccepted))
        {
            diagnostics.Add(new RuntimeSaveDiagnostic(
                QuestReferenceInvalidCode,
                "Quest runtime states require unique quest/state UUIDs and tracking must reference an accepted quest.",
                "questStates"));
        }

        ValidateWeather(save.Weather, "weather", diagnostics);

        if (save.Progression is not null && (save.Progression.SchemaVersion != 1 ||
            !double.IsFinite(save.Progression.Experience) || save.Progression.Experience < 0 ||
            save.Progression.Level <= 0))
        {
            diagnostics.Add(new RuntimeSaveDiagnostic(ProgressionInvalidCode,
                "Progression state requires schema v1, non-negative experience and a positive level.",
                "progression"));
        }

        if (save.WorldClock is not null && (save.WorldClock.SchemaVersion != 1 ||
            save.WorldClock.TotalMinutes < 0 || save.WorldClock.Revision < 0 ||
            string.IsNullOrWhiteSpace(save.WorldClock.Provenance)))
        {
            diagnostics.Add(new RuntimeSaveDiagnostic(WorldClockInvalidCode,
                "World clock state requires schema v1, non-negative minute/revision and provenance.",
                "worldClock"));
        }

        ValidateMapExploration(save.MapExploration, "mapExploration", diagnostics);
        ValidateEntitySnapshots(save.EntitySnapshots ?? [], "entitySnapshots", diagnostics);

        var areaStates = save.AreaStates ?? [];
        if (areaStates.Select(item => item.AreaId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != areaStates.Count)
        {
            diagnostics.Add(new RuntimeSaveDiagnostic(
                AreaStateInvalidCode,
                "Area runtime states must use unique area IDs.",
                "areaStates"));
        }

        foreach (var areaState in areaStates)
        {
            var path = $"areaStates/{areaState.AreaId}";
            if (!Guid.TryParse(areaState.AreaId, out var stateAreaId) || stateAreaId == Guid.Empty)
            {
                diagnostics.Add(new RuntimeSaveDiagnostic(
                    AreaStateInvalidCode,
                    "An area runtime state requires a non-empty area UUID.",
                    path));
            }

            ValidateHostiles(areaState.Hostiles, $"{path}/hostiles", diagnostics);
            ValidateWeather(areaState.Weather, $"{path}/weather", diagnostics);
            ValidateEntitySnapshots(areaState.EntitySnapshots, $"{path}/entitySnapshots", diagnostics);
            ValidateMapExploration(areaState.MapExploration, $"{path}/mapExploration", diagnostics);
        }

        return diagnostics;
    }

    private static bool IsJson(string value)
    {
        try { using var document = System.Text.Json.JsonDocument.Parse(value); return true; }
        catch (System.Text.Json.JsonException) { return false; }
    }

    private static void ValidateHostiles(
        IReadOnlyList<RuntimeCombatantSave> hostiles,
        string path,
        ICollection<RuntimeSaveDiagnostic> diagnostics)
    {
        foreach (var hostile in hostiles)
        {
            ValidateCombatant(hostile, $"{path}/{hostile.EntityId}", HostileInvalidCode, diagnostics);
        }

        if (hostiles.Select(item => item.EntityId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != hostiles.Count)
        {
            diagnostics.Add(new RuntimeSaveDiagnostic(
                HostileInvalidCode,
                "Hostile entity IDs must be unique.",
                path));
        }
    }

    private static void ValidateWeather(
        RuntimeWeatherSave? weather,
        string path,
        ICollection<RuntimeSaveDiagnostic> diagnostics)
    {
        if (weather is not null &&
            (weather.SchemaVersion != 1 ||
             !Guid.TryParse(weather.StateId, out var weatherStateId) || weatherStateId == Guid.Empty ||
             weather.Revision < 0 || weather.ServerRevision < 0 ||
             weather.ElapsedSeconds < 0 || !double.IsFinite(weather.ElapsedSeconds)))
        {
            diagnostics.Add(new RuntimeSaveDiagnostic(
                WeatherInvalidCode,
                "Weather state requires schema v1, a non-empty state UUID and non-negative runtime counters.",
                path));
        }
    }

    private static void ValidateMapExploration(
        RuntimeMapExplorationSave? exploration,
        string path,
        ICollection<RuntimeSaveDiagnostic> diagnostics)
    {
        if (exploration is null) return;

        var validScope = string.Equals(exploration.Scope, "player", StringComparison.Ordinal) ||
                         string.Equals(exploration.Scope, "party", StringComparison.Ordinal) ||
                         string.Equals(exploration.Scope, "world", StringComparison.Ordinal);
        var parsedCells = exploration.RevealedCells.Select(key =>
        {
            var parsed = AreaMapCell.TryParse(key, out var cell, out var legacy);
            return (Key: key, Parsed: parsed, Cell: cell, Legacy: legacy);
        }).ToArray();
        var invalid = !validScope || string.IsNullOrWhiteSpace(exploration.ExplorerId) ||
                      exploration.ActiveFloor < 0 || exploration.RevealedCells.Count > 1_000_000 ||
                      parsedCells.Any(item => !item.Parsed ||
                          (item.Legacy && !string.Equals(item.Key, item.Cell.LegacyKey,
                              StringComparison.Ordinal)) ||
                          item.Cell.Floor < 0 ||
                          item.Cell.X < 0 || item.Cell.Z < 0) ||
                      exploration.RevealedCells.Distinct(StringComparer.Ordinal).Count() !=
                      exploration.RevealedCells.Count ||
                      exploration.RevealedPins.Count > 65_536 ||
                      exploration.RevealedPins.Any(pin => !Guid.TryParse(pin, out var id) || id == Guid.Empty) ||
                      exploration.RevealedPins.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                      exploration.RevealedPins.Count;
        if (invalid)
        {
            diagnostics.Add(new RuntimeSaveDiagnostic(
                MapExplorationInvalidCode,
                "Map exploration requires a known scope, active floor, canonical unique cells and pin UUIDs.",
                path));
        }
    }

    private static void ValidateEntitySnapshots(
        IReadOnlyList<RuntimeEntityStateSave> snapshots,
        string rootPath,
        ICollection<RuntimeSaveDiagnostic> diagnostics)
    {
        if (snapshots.Select(item => item.EntityId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshots.Count)
        {
            diagnostics.Add(new RuntimeSaveDiagnostic(
                EntitySnapshotInvalidCode,
                "Entity snapshot IDs must be unique.",
                rootPath));
        }

        foreach (var snapshot in snapshots)
        {
            var path = $"{rootPath}/{snapshot.EntityId}";
            var invalid = snapshot.SchemaVersion != 1 ||
                !Guid.TryParse(snapshot.EntityId, out var entityId) || entityId == Guid.Empty ||
                snapshot.Components.Count > 64 || snapshot.References.Count > 4096 ||
                snapshot.Inventory.Count > 4096 ||
                snapshot.Components.Any(component => string.IsNullOrWhiteSpace(component.Key) ||
                    component.JsonValue.Length > 65_536 || !IsJson(component.JsonValue)) ||
                snapshot.Components.Select(component => component.Key).Distinct(StringComparer.Ordinal).Count() != snapshot.Components.Count ||
                snapshot.References.Any(reference => !Guid.TryParse(reference, out var referenceId) || referenceId == Guid.Empty) ||
                snapshot.References.Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshot.References.Count ||
                snapshot.Inventory.Any(stack => string.IsNullOrWhiteSpace(stack.Slug) || stack.Quantity <= 0) ||
                snapshot.Inventory.Select(stack => stack.Slug).Distinct(StringComparer.Ordinal).Count() != snapshot.Inventory.Count ||
                snapshot.Blackboard.Any(item => string.IsNullOrWhiteSpace(item.Key)) ||
                snapshot.Blackboard.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() != snapshot.Blackboard.Count;
            if (invalid)
            {
                diagnostics.Add(new RuntimeSaveDiagnostic(
                    EntitySnapshotInvalidCode,
                    "Entity state requires schema v1, resolved UUID references, unique bounded components, positive inventory stacks and blackboard values.",
                    path));
            }
        }
    }

    private static void ValidateCombatant(
        RuntimeCombatantSave combatant,
        string path,
        string code,
        ICollection<RuntimeSaveDiagnostic> diagnostics)
    {
        if (!Guid.TryParse(combatant.EntityId, out var id) || id == Guid.Empty ||
            combatant.Health < 0 || combatant.Resource < 0)
        {
            diagnostics.Add(new RuntimeSaveDiagnostic(
                code,
                "A combatant requires a non-empty entity UUID and non-negative health/resource.",
                path));
        }
    }
}
