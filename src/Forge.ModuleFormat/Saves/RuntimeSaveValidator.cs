namespace Forge.ModuleFormat.Saves;

public static class RuntimeSaveValidator
{
    public const string VersionUnsupportedCode = "save.version_unsupported";
    public const string ModuleIdInvalidCode = "save.module_id_invalid";
    public const string ContentHashInvalidCode = "save.content_hash_invalid";
    public const string AreaIdInvalidCode = "save.area_id_invalid";
    public const string PlayerInvalidCode = "save.player_invalid";
    public const string InventoryDuplicateCode = "save.inventory_duplicate";
    public const string VariableInvalidCode = "save.variable_invalid";
    public const string HostileInvalidCode = "save.hostile_invalid";
    public const string QuestReferenceInvalidCode = "save.quest_reference_invalid";
    public const string WeatherInvalidCode = "save.weather_invalid";
    public const string ProgressionInvalidCode = "save.progression_invalid";
    public const string EntitySnapshotInvalidCode = "save.entity_snapshot_invalid";

    public static IReadOnlyList<RuntimeSaveDiagnostic> Validate(RuntimeSaveDocument save)
    {
        ArgumentNullException.ThrowIfNull(save);
        var diagnostics = new List<RuntimeSaveDiagnostic>();
        if (!string.Equals(save.FormatVersion, RuntimeSaveDocument.CurrentFormatVersion, StringComparison.Ordinal) &&
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

        ValidateCombatant(save.Player, "player", PlayerInvalidCode, diagnostics);
        if (save.Inventory.Any(string.IsNullOrWhiteSpace) ||
            save.Inventory.Distinct(StringComparer.Ordinal).Count() != save.Inventory.Count)
        {
            diagnostics.Add(new RuntimeSaveDiagnostic(
                InventoryDuplicateCode,
                "Inventory item slugs must be non-empty and unique.",
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

        foreach (var hostile in save.Hostiles)
        {
            ValidateCombatant(hostile, $"hostiles/{hostile.EntityId}", HostileInvalidCode, diagnostics);
        }

        if (save.Hostiles.Select(item => item.EntityId).Distinct(StringComparer.Ordinal).Count() != save.Hostiles.Count)
        {
            diagnostics.Add(new RuntimeSaveDiagnostic(
                HostileInvalidCode,
                "Hostile entity IDs must be unique.",
                "hostiles"));
        }

        if ((save.QuestId is null) != (save.QuestStateId is null) ||
            save.QuestId is not null &&
            (!Guid.TryParse(save.QuestId, out _) || !Guid.TryParse(save.QuestStateId, out _)))
        {
            diagnostics.Add(new RuntimeSaveDiagnostic(
                QuestReferenceInvalidCode,
                "Quest ID and state ID must both be absent or valid UUIDs.",
                "questStateId"));
        }

        if (save.Weather is not null &&
            (save.Weather.SchemaVersion != 1 ||
             !Guid.TryParse(save.Weather.StateId, out var weatherStateId) || weatherStateId == Guid.Empty ||
             save.Weather.Revision < 0 || save.Weather.ServerRevision < 0 ||
             save.Weather.ElapsedSeconds < 0 || !double.IsFinite(save.Weather.ElapsedSeconds)))
        {
            diagnostics.Add(new RuntimeSaveDiagnostic(
                WeatherInvalidCode,
                "Weather state requires schema v1, a non-empty state UUID and non-negative runtime counters.",
                "weather"));
        }

        if (save.Progression is not null && (save.Progression.SchemaVersion != 1 ||
            !double.IsFinite(save.Progression.Experience) || save.Progression.Experience < 0 ||
            save.Progression.Level <= 0))
        {
            diagnostics.Add(new RuntimeSaveDiagnostic(ProgressionInvalidCode,
                "Progression state requires schema v1, non-negative experience and a positive level.",
                "progression"));
        }

        var snapshots = save.EntitySnapshots ?? [];
        if (snapshots.Select(item => item.EntityId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshots.Count)
            diagnostics.Add(new RuntimeSaveDiagnostic(EntitySnapshotInvalidCode,
                "Entity snapshot IDs must be unique.", "entitySnapshots"));
        foreach (var snapshot in snapshots)
        {
            var path = $"entitySnapshots/{snapshot.EntityId}";
            var invalid = snapshot.SchemaVersion != 1 ||
                !Guid.TryParse(snapshot.EntityId, out var entityId) || entityId == Guid.Empty ||
                snapshot.Components.Count > 64 || snapshot.References.Count > 4096 ||
                snapshot.Inventory.Count > 4096 ||
                snapshot.Components.Any(component => string.IsNullOrWhiteSpace(component.Key) ||
                    component.JsonValue.Length > 65_536 || !IsJson(component.JsonValue)) ||
                snapshot.Components.Select(component => component.Key).Distinct(StringComparer.Ordinal).Count() != snapshot.Components.Count ||
                snapshot.References.Any(reference => !Guid.TryParse(reference, out var referenceId) || referenceId == Guid.Empty) ||
                snapshot.References.Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshot.References.Count ||
                snapshot.Inventory.Any(string.IsNullOrWhiteSpace) ||
                snapshot.Blackboard.Any(item => string.IsNullOrWhiteSpace(item.Key)) ||
                snapshot.Blackboard.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() != snapshot.Blackboard.Count;
            if (invalid)
                diagnostics.Add(new RuntimeSaveDiagnostic(EntitySnapshotInvalidCode,
                    "Entity state requires schema v1, resolved UUID references, unique bounded components, inventory and blackboard values.",
                    path));
        }

        return diagnostics;
    }

    private static bool IsJson(string value)
    {
        try { using var document = System.Text.Json.JsonDocument.Parse(value); return true; }
        catch (System.Text.Json.JsonException) { return false; }
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
