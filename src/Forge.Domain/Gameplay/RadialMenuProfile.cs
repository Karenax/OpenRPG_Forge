namespace Forge.Domain.Gameplay;

public enum RadialActionKind
{
    Verb,
    Ability,
    Item,
    Navigation,
    System,
}

public sealed record RadialActionDefinition
{
    public Guid Id { get; }
    public string Key { get; }
    public string Label { get; }
    public string AccessibleLabel { get; }
    public RadialActionKind Kind { get; }
    public string Command { get; }
    public string ParentKey { get; }
    public int Order { get; }
    public bool RequiresTarget { get; }
    public IReadOnlyList<string> Tags { get; }

    public RadialActionDefinition(Guid id, string key, string label, string accessibleLabel,
        RadialActionKind kind, string command, string parentKey, int order, bool requiresTarget,
        IReadOnlyList<string> tags)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessibleLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(tags);
        if (id == Guid.Empty || order < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Radial action is invalid.");
        }

        Id = id;
        Key = Normalize(key);
        Label = label.Trim();
        AccessibleLabel = accessibleLabel.Trim();
        Kind = kind;
        Command = Normalize(command);
        ParentKey = Normalize(parentKey);
        Order = order;
        RequiresTarget = requiresTarget;
        Tags = tags.Select(Normalize).Where(value => value.Length > 0).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray();
    }

    private static string Normalize(string value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}

public sealed record RadialMenuProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public bool Enabled { get; }
    public string InputAction { get; }
    public float DeadZone { get; }
    public int MaximumDepth { get; }
    public bool GamepadEnabled { get; }
    public bool KeyboardNavigation { get; }
    public bool ListFallback { get; }
    public bool ScreenReaderLabels { get; }
    public IReadOnlyList<RadialActionDefinition> Actions { get; }
    public string Provenance { get; }

    public RadialMenuProfile(Guid id, int schemaVersion, bool enabled, string inputAction, float deadZone,
        int maximumDepth, bool gamepadEnabled, bool keyboardNavigation, bool listFallback,
        bool screenReaderLabels, IReadOnlyList<RadialActionDefinition> actions, string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputAction);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var values = actions.OrderBy(action => action.ParentKey, StringComparer.Ordinal).ThenBy(action => action.Order)
            .ThenBy(action => action.Key, StringComparer.Ordinal).ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || deadZone is < 0 or >= 1 ||
            maximumDepth is < 1 or > 8 || values.Select(action => action.Id).Distinct().Count() != values.Length ||
            values.Select(action => action.Key).Distinct(StringComparer.Ordinal).Count() != values.Length ||
            values.Any(action => action.ParentKey.Length > 0 && values.All(parent => parent.Key != action.ParentKey)))
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Radial menu profile is invalid.");
        }

        EnsureAcyclic(values, maximumDepth);
        Id = id;
        SchemaVersion = schemaVersion;
        Enabled = enabled;
        InputAction = inputAction.Trim().ToLowerInvariant();
        DeadZone = deadZone;
        MaximumDepth = maximumDepth;
        GamepadEnabled = gamepadEnabled;
        KeyboardNavigation = keyboardNavigation;
        ListFallback = listFallback;
        ScreenReaderLabels = screenReaderLabels;
        Actions = values;
        Provenance = provenance.Trim();
    }

    private static void EnsureAcyclic(IReadOnlyList<RadialActionDefinition> actions, int maximumDepth)
    {
        var byKey = actions.ToDictionary(action => action.Key, StringComparer.Ordinal);
        foreach (var action in actions)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal) { action.Key };
            var parent = action.ParentKey;
            var depth = 1;
            while (parent.Length > 0)
            {
                if (!visited.Add(parent) || ++depth > maximumDepth)
                {
                    throw new ArgumentException("Radial action hierarchy contains a cycle or exceeds maximum depth.", nameof(actions));
                }

                parent = byKey[parent].ParentKey;
            }
        }
    }
}
