using Forge.Domain.Authoring;

namespace Forge.Application.Creator;

public static class EditorKeymapPlanner
{
    public static EditorKeymapProfile UsePreset(
        EditorKeymapProfile current,
        EditorKeymapPreset preset,
        string provenance)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (preset == EditorKeymapPreset.Custom)
            return new(current.Id, EditorKeymapProfile.CurrentSchemaVersion, EditorKeymapPreset.Custom,
                current.Bindings, provenance);
        var template = EditorKeymapProfile.CreatePreset(preset, provenance);
        return new(current.Id, EditorKeymapProfile.CurrentSchemaVersion, preset, template.Bindings, provenance);
    }

    public static EditorKeymapProfile Rebind(
        EditorKeymapProfile current,
        string commandId,
        EditorKeyGesture gesture,
        string provenance)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(gesture);
        var definition = EditorKeymapCatalog.Find(commandId) ??
            throw new ArgumentException($"Unknown editor command '{commandId}'.", nameof(commandId));
        var bindings = current.Bindings
            .Where(binding => !string.Equals(binding.CommandId, commandId, StringComparison.Ordinal))
            .Append(new EditorCommandBinding(definition.Id, definition.DisplayName, definition.Category, gesture))
            .ToArray();
        return new(current.Id, EditorKeymapProfile.CurrentSchemaVersion, EditorKeymapPreset.Custom, bindings, provenance);
    }

    public static IReadOnlyList<EditorCommandBinding> Search(EditorKeymapProfile profile, string? query)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var term = (query ?? string.Empty).Trim();
        return profile.Bindings.Where(binding => term.Length == 0 ||
                binding.CommandId.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                binding.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                binding.Category.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                binding.Gesture.CanonicalText.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(binding => binding.Category, StringComparer.Ordinal)
            .ThenBy(binding => binding.DisplayName, StringComparer.Ordinal)
            .ToArray();
    }
}
