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

    public static IReadOnlyList<RuntimeSaveDiagnostic> Validate(RuntimeSaveDocument save)
    {
        ArgumentNullException.ThrowIfNull(save);
        var diagnostics = new List<RuntimeSaveDiagnostic>();
        if (!string.Equals(save.FormatVersion, RuntimeSaveDocument.CurrentFormatVersion, StringComparison.Ordinal))
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

        return diagnostics;
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
