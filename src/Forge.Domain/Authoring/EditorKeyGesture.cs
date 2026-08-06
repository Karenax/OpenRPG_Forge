namespace Forge.Domain.Authoring;

public sealed record EditorKeyGesture
{
    private static readonly HashSet<string> NamedKeys = new(StringComparer.Ordinal)
    {
        "DELETE", "HOME", "END", "SPACE", "ENTER", "ESCAPE", "TAB",
        "UP", "DOWN", "LEFT", "RIGHT",
    };

    public string Key { get; }

    public bool Ctrl { get; }

    public bool Alt { get; }

    public bool Shift { get; }

    public bool Meta { get; }

    public string CanonicalText => string.Join('+', Modifiers().Append(DisplayKey(Key)));

    public EditorKeyGesture(string key, bool ctrl = false, bool alt = false, bool shift = false, bool meta = false)
    {
        var normalized = NormalizeKey(key);
        if (!IsSupportedKey(normalized))
            throw new ArgumentException($"Unsupported editor shortcut key '{key}'.", nameof(key));
        Key = normalized;
        Ctrl = ctrl;
        Alt = alt;
        Shift = shift;
        Meta = meta;
    }

    public bool Matches(string key, bool ctrl, bool alt, bool shift, bool meta) =>
        string.Equals(Key, NormalizeKey(key), StringComparison.Ordinal) &&
        Ctrl == ctrl && Alt == alt && Shift == shift && Meta == meta;

    public static bool TryParse(string? value, out EditorKeyGesture? gesture)
    {
        gesture = null;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var parts = value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        var ctrl = false;
        var alt = false;
        var shift = false;
        var meta = false;
        string? key = null;
        foreach (var part in parts)
        {
            switch (part.Trim().ToUpperInvariant())
            {
                case "CTRL" or "CONTROL": ctrl = true; break;
                case "ALT": alt = true; break;
                case "SHIFT" or "MAJ": shift = true; break;
                case "META" or "CMD" or "WIN": meta = true; break;
                default:
                    if (key is not null) return false;
                    key = part;
                    break;
            }
        }

        if (key is null) return false;
        try
        {
            gesture = new EditorKeyGesture(key, ctrl, alt, shift, meta);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private IEnumerable<string> Modifiers()
    {
        if (Ctrl) yield return "Ctrl";
        if (Alt) yield return "Alt";
        if (Shift) yield return "Shift";
        if (Meta) yield return "Meta";
    }

    private static string NormalizeKey(string? key)
    {
        var normalized = (key ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "DEL" => "DELETE",
            "RETURN" => "ENTER",
            "ESC" => "ESCAPE",
            _ => normalized,
        };
    }

    private static bool IsSupportedKey(string key) =>
        key.Length == 1 && char.IsAsciiLetterOrDigit(key[0]) ||
        key.Length is 2 or 3 && key[0] == 'F' && int.TryParse(key[1..], out var function) && function is >= 1 and <= 12 ||
        NamedKeys.Contains(key);

    private static string DisplayKey(string key) => key switch
    {
        "DELETE" => "Delete",
        "HOME" => "Home",
        "END" => "End",
        "SPACE" => "Space",
        "ENTER" => "Enter",
        "ESCAPE" => "Escape",
        "TAB" => "Tab",
        "UP" => "Up",
        "DOWN" => "Down",
        "LEFT" => "Left",
        "RIGHT" => "Right",
        _ => key,
    };
}
