using Forge.Application.Localization;
using Forge.Domain.Dialogues;

namespace Forge.Application.Dialogues;

/// <summary>Deterministic text boundary used by the compact dialogue authoring controls.</summary>
public static class DialogueAuthoringCodec
{
    public static string FormatVariants(LocalizedText text) => string.Join('\n', text.Variants.Select(item =>
        $"{item.Locale}|{item.Gender}|{item.PluralCategory}|{item.Text}"));

    public static IReadOnlyList<LocalizedTextVariant> ParseVariants(
        string textKey,
        string value,
        IReadOnlyList<LocalizedTextVariant>? existing = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(textKey);
        var previous = (existing ?? []).ToDictionary(
            item => $"{item.Locale.ToLowerInvariant()}|{item.Gender}|{item.PluralCategory}",
            StringComparer.OrdinalIgnoreCase);
        return Lines(value).Select((line, index) =>
        {
            var parts = line.Split('|', 4, StringSplitOptions.TrimEntries);
            if (parts.Length != 4 || !Enum.TryParse<DialoguePluralCategory>(parts[2], true, out var plural))
                throw new ArgumentException($"Variant line {index + 1} must use locale|gender|plural|text.", nameof(value));
            var created = LocalizedTextVariant.Create(textKey, parts[0], parts[3], parts[1], plural);
            var identity = $"{created.Locale}|{created.Gender}|{created.PluralCategory}";
            return previous.TryGetValue(identity, out var old)
                ? new LocalizedTextVariant(old.Id, old.SchemaVersion, "author:dialogue-editor", created.Locale,
                    created.Gender, created.PluralCategory, created.Text)
                : created;
        }).ToArray();
    }

    public static string FormatConditions(IReadOnlyList<DialogueCondition> conditions) => string.Join('\n',
        conditions.Select(item => $"{(item.Negated ? "NOT" : "IS")}|{item.Kind}|{item.Key}|{item.ExpectedValue}"));

    public static IReadOnlyList<DialogueCondition> ParseConditions(
        string value,
        IReadOnlyList<DialogueCondition>? existing = null)
    {
        var previous = existing ?? [];
        return Lines(value).Select((line, index) =>
        {
            var parts = line.Split('|', 4, StringSplitOptions.TrimEntries);
            if (parts.Length != 4 || !Enum.TryParse<DialogueConditionKind>(parts[1], true, out var kind) ||
                parts[0] is not ("IS" or "NOT"))
                throw new ArgumentException($"Condition line {index + 1} must use IS|Kind|key|expected or NOT|Kind|key|expected.", nameof(value));
            var old = index < previous.Count ? previous[index] : null;
            return new DialogueCondition(
                old?.Id ?? Guid.Empty,
                DialogueCondition.CurrentSchemaVersion,
                "author:dialogue-editor",
                kind,
                parts[2],
                parts[3],
                parts[0] == "NOT",
                "on-choice-visible",
                1);
        }).ToArray();
    }

    public static string FormatActions(IReadOnlyList<DialogueAction> actions) => string.Join('\n', actions.Select(item =>
        $"{item.Kind}|{item.Key}|{item.Value}|{(item.Required ? "required" : "optional")}"));

    public static IReadOnlyList<DialogueAction> ParseActions(
        string value,
        IReadOnlyList<DialogueAction>? existing = null)
    {
        var previous = existing ?? [];
        return Lines(value).Select((line, index) =>
        {
            var parts = line.Split('|', 4, StringSplitOptions.TrimEntries);
            if (parts.Length != 4 || !Enum.TryParse<DialogueActionKind>(parts[0], true, out var kind) ||
                parts[3] is not ("required" or "optional"))
                throw new ArgumentException($"Action line {index + 1} must use Kind|key|value|required or optional.", nameof(value));
            var old = index < previous.Count ? previous[index] : null;
            return new DialogueAction(
                old?.Id ?? Guid.Empty,
                DialogueAction.CurrentSchemaVersion,
                "author:dialogue-editor",
                kind,
                parts[1],
                parts[2],
                parts[3] == "required");
        }).ToArray();
    }

    public static IReadOnlyDictionary<string, string> ParseState(string value)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (line, index) in Lines(value).Select((line, index) => (line, index)))
        {
            var separator = line.IndexOf('=');
            if (separator < 1 || separator == line.Length - 1)
                throw new ArgumentException($"State line {index + 1} must use namespace:key=value.", nameof(value));
            var key = line[..separator].Trim();
            var stateValue = line[(separator + 1)..].Trim();
            if (!key.Contains(':') || !result.TryAdd(key, stateValue))
                throw new ArgumentException($"State line {index + 1} has an invalid or duplicate key.", nameof(value));
        }
        return result;
    }

    public static string PseudoLocalize(LocalizedText text) => PseudoLocalization.Transform(text.Fallback);

    private static IEnumerable<string> Lines(string value) => (value ?? string.Empty)
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
