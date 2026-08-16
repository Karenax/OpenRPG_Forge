using Forge.Domain.Validation;

namespace Forge.Domain.Authoring;

public sealed record EditorKeymapProfile
{
    public const int CurrentSchemaVersion = 1;
    public static readonly EditorKeymapProfileId DefaultId = EditorKeymapProfileId.From(
        Guid.Parse("a1240000-0000-4000-8000-000000000001"));

    public EditorKeymapProfileId Id { get; }

    public int SchemaVersion { get; }

    public EditorKeymapPreset Preset { get; }

    public IReadOnlyList<EditorCommandBinding> Bindings { get; }

    public string Provenance { get; }

    public EditorKeymapProfile(
        EditorKeymapProfileId id,
        int schemaVersion,
        EditorKeymapPreset preset,
        IReadOnlyList<EditorCommandBinding> bindings,
        string provenance)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(bindings);
        Id = id;
        SchemaVersion = schemaVersion;
        Preset = preset;
        Bindings = bindings.OrderBy(binding => binding.CommandId, StringComparer.Ordinal).ToArray();
        Provenance = (provenance ?? string.Empty).Trim();
    }

    public EditorCommandBinding? Resolve(string key, bool ctrl, bool alt, bool shift, bool meta) =>
        Bindings.FirstOrDefault(binding => binding.Gesture.Matches(key, ctrl, alt, shift, meta));

    public static EditorKeymapProfile CreatePreset(EditorKeymapPreset preset, string provenance = "openrpg-forge:g13-a:default")
    {
        if (preset == EditorKeymapPreset.Custom) preset = EditorKeymapPreset.Standard;
        var gestures = preset switch
        {
            EditorKeymapPreset.Aurora => AuroraGestures(),
            EditorKeymapPreset.Blender => BlenderGestures(),
            _ => StandardGestures(),
        };
        var bindings = EditorKeymapCatalog.Commands.Select(command =>
            new EditorCommandBinding(command.Id, command.DisplayName, command.Category, gestures[command.Id])).ToArray();
        return new EditorKeymapProfile(DefaultId, CurrentSchemaVersion, preset, bindings, provenance);
    }

    private static Dictionary<string, EditorKeyGesture> StandardGestures() => CommonGestures(
        new("F"), new("F6"), new("K", ctrl: true), new("F1"), new("F2"), new("F3"));

    private static Dictionary<string, EditorKeyGesture> AuroraGestures() => CommonGestures(
        new("F"), new("F6"), new("P", ctrl: true), new("W", ctrl: true, shift: true),
        new("B", ctrl: true, shift: true), new("D", ctrl: true, shift: true));

    private static Dictionary<string, EditorKeyGesture> BlenderGestures() => CommonGestures(
        new("HOME"), new("F6"), new("F3"), new("F1"), new("F2"), new("F4"));

    private static Dictionary<string, EditorKeyGesture> CommonGestures(
        EditorKeyGesture focus,
        EditorKeyGesture playtest,
        EditorKeyGesture search,
        EditorKeyGesture world,
        EditorKeyGesture blueprints,
        EditorKeyGesture dialogues) =>
        new(StringComparer.Ordinal)
        {
            [EditorKeymapCatalog.SaveProject] = new("S", ctrl: true),
            [EditorKeymapCatalog.Undo] = new("Z", ctrl: true),
            [EditorKeymapCatalog.Redo] = new("Y", ctrl: true),
            [EditorKeymapCatalog.CopySelection] = new("C", ctrl: true),
            [EditorKeymapCatalog.CutSelection] = new("X", ctrl: true),
            [EditorKeymapCatalog.PasteSelection] = new("V", ctrl: true),
            [EditorKeymapCatalog.DuplicateSelection] = new("D", ctrl: true),
            [EditorKeymapCatalog.DeleteSelection] = new("DELETE"),
            [EditorKeymapCatalog.FocusSelection] = focus,
            [EditorKeymapCatalog.RunPlaytest] = playtest,
            [EditorKeymapCatalog.OpenSearch] = search,
            [EditorKeymapCatalog.OpenWorld] = world,
            [EditorKeymapCatalog.OpenBlueprints] = blueprints,
            [EditorKeymapCatalog.OpenDialogues] = dialogues,
        };
}

public static class EditorKeymapProfileValidator
{
    public const string SchemaUnsupportedCode = "authoring.keymap.schema_unsupported";
    public const string IdInvalidCode = "authoring.keymap.id_invalid";
    public const string PresetInvalidCode = "authoring.keymap.preset_invalid";
    public const string ProvenanceMissingCode = "authoring.keymap.provenance_missing";
    public const string CommandDuplicateCode = "authoring.keymap.command_duplicate";
    public const string CommandUnknownCode = "authoring.keymap.command_unknown";
    public const string CommandMissingCode = "authoring.keymap.command_missing";
    public const string ConflictCode = "authoring.keymap.conflict";

    public static IReadOnlyList<DomainDiagnostic> Validate(EditorKeymapProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        const string path = ".forge/editor-keymap";
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.SchemaVersion != EditorKeymapProfile.CurrentSchemaVersion)
            diagnostics.Add(new(SchemaUnsupportedCode, "Unsupported editor keymap schema.", $"{path}/schemaVersion"));
        if (profile.Id.Value == Guid.Empty)
            diagnostics.Add(new(IdInvalidCode, "Editor keymap profile ID cannot be empty.", $"{path}/id"));
        if (!Enum.IsDefined(profile.Preset))
            diagnostics.Add(new(PresetInvalidCode, "Editor keymap preset is not supported.", $"{path}/preset"));
        if (string.IsNullOrWhiteSpace(profile.Provenance))
            diagnostics.Add(new(ProvenanceMissingCode, "Editor keymap requires modification provenance.", $"{path}/provenance"));

        var commandGroups = profile.Bindings.GroupBy(binding => binding.CommandId, StringComparer.Ordinal).ToArray();
        foreach (var group in commandGroups.Where(group => group.Count() > 1))
            diagnostics.Add(new(CommandDuplicateCode, $"Command '{group.Key}' is bound more than once.", $"{path}/bindings/{group.Key}"));
        foreach (var binding in profile.Bindings.Where(binding => EditorKeymapCatalog.Find(binding.CommandId) is null))
            diagnostics.Add(new(CommandUnknownCode, $"Command '{binding.CommandId}' is not in the editor command catalogue.", $"{path}/bindings/{binding.CommandId}"));
        foreach (var command in EditorKeymapCatalog.Commands.Where(command => commandGroups.All(group => group.Key != command.Id)))
            diagnostics.Add(new(CommandMissingCode, $"Command '{command.Id}' has no shortcut.", $"{path}/bindings/{command.Id}"));

        foreach (var conflict in profile.Bindings.GroupBy(binding => binding.Gesture.CanonicalText, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            diagnostics.Add(new(ConflictCode,
                $"Shortcut '{conflict.Key}' is assigned to {string.Join(", ", conflict.Select(binding => binding.CommandId))}.",
                $"{path}/bindings"));
        }

        return diagnostics;
    }
}
