using System.Globalization;

namespace Forge.Domain.Entities;

public enum EntityClickTriggerAction
{
    ShowMessage,
    StartDialogue,
    ToggleVisibility,
    ToggleDoor,
}

public sealed record EntityClickTriggerSettings
{
    public const string PropertyPrefix = "interaction.click.";
    public const string EnabledProperty = PropertyPrefix + "enabled";
    public const string ActionProperty = PropertyPrefix + "action";
    public const string MessageProperty = PropertyPrefix + "message";
    public const string TargetProperty = PropertyPrefix + "target";
    public const string MaximumDistanceProperty = PropertyPrefix + "maxDistance";
    public const float DefaultMaximumDistance = 3;
    public const float MinimumDistance = .5f;
    public const float MaximumDistance = 100;

    public bool Enabled { get; }

    public EntityClickTriggerAction Action { get; }

    public string Message { get; }

    public string TargetId { get; }

    public float MaximumDistanceMeters { get; }

    public EntityClickTriggerSettings(
        bool enabled,
        EntityClickTriggerAction action,
        string? message,
        string? targetId,
        float maximumDistanceMeters = DefaultMaximumDistance)
    {
        if (!float.IsFinite(maximumDistanceMeters) ||
            maximumDistanceMeters < MinimumDistance ||
            maximumDistanceMeters > MaximumDistance)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumDistanceMeters),
                $"Click interaction distance must be between {MinimumDistance} and {MaximumDistance} metres.");
        }
        Enabled = enabled;
        Action = action;
        Message = message?.Trim() ?? string.Empty;
        TargetId = targetId?.Trim() ?? string.Empty;
        MaximumDistanceMeters = maximumDistanceMeters;
    }

    public void EnsureValid()
    {
        if (!Enabled) return;
        if (Action == EntityClickTriggerAction.ShowMessage && string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("A click message cannot be empty.", nameof(Message));
        }
        if (Action is EntityClickTriggerAction.StartDialogue or EntityClickTriggerAction.ToggleVisibility &&
            !Guid.TryParse(TargetId, out _))
        {
            throw new ArgumentException("The selected click target must be a stable GUID.", nameof(TargetId));
        }
    }

    public static EntityClickTriggerSettings Read(EntityPropertyOverrides overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        var properties = overrides.Properties.ToDictionary(property => property.Key, StringComparer.Ordinal);
        var enabled = properties.TryGetValue(EnabledProperty, out var enabledValue) &&
                      bool.TryParse(enabledValue.Value, out var parsedEnabled) && parsedEnabled;
        var action = properties.TryGetValue(ActionProperty, out var actionValue)
            ? ParseAction(actionValue.Value)
            : EntityClickTriggerAction.ShowMessage;
        var message = properties.GetValueOrDefault(MessageProperty)?.Value ??
                      "Vous examinez cet objet.";
        var target = properties.GetValueOrDefault(TargetProperty)?.Value ?? string.Empty;
        var distance = properties.TryGetValue(MaximumDistanceProperty, out var distanceValue) &&
                       float.TryParse(
                           distanceValue.Value,
                           NumberStyles.Float,
                           CultureInfo.InvariantCulture,
                           out var parsedDistance) &&
                       float.IsFinite(parsedDistance) &&
                       parsedDistance is >= MinimumDistance and <= MaximumDistance
            ? parsedDistance
            : DefaultMaximumDistance;
        return new EntityClickTriggerSettings(enabled, action, message, target, distance);
    }

    public static EntityPropertyOverrides Merge(
        EntityPropertyOverrides current,
        EntityClickTriggerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(settings);
        settings.EnsureValid();
        var properties = current.Properties
            .Where(property => !property.Key.StartsWith(PropertyPrefix, StringComparison.Ordinal))
            .ToList();
        if (settings.Enabled)
        {
            properties.Add(new EntityPropertyOverride(
                EnabledProperty,
                EntityPropertyValueKind.Boolean,
                "true"));
            properties.Add(new EntityPropertyOverride(
                ActionProperty,
                EntityPropertyValueKind.String,
                ActionKey(settings.Action)));
            properties.Add(new EntityPropertyOverride(
                MaximumDistanceProperty,
                EntityPropertyValueKind.Number,
                settings.MaximumDistanceMeters.ToString("0.###", CultureInfo.InvariantCulture)));
            if (settings.Action == EntityClickTriggerAction.ShowMessage)
            {
                properties.Add(new EntityPropertyOverride(
                    MessageProperty,
                    EntityPropertyValueKind.String,
                    settings.Message));
            }
            else if (settings.Action != EntityClickTriggerAction.ToggleDoor)
            {
                properties.Add(new EntityPropertyOverride(
                    TargetProperty,
                    EntityPropertyValueKind.String,
                    settings.TargetId));
            }
        }
        return new EntityPropertyOverrides(EntityPropertyOverrides.CurrentSchemaVersion, properties);
    }

    public static string ActionKey(EntityClickTriggerAction action) => action switch
    {
        EntityClickTriggerAction.ShowMessage => "message",
        EntityClickTriggerAction.StartDialogue => "dialogue",
        EntityClickTriggerAction.ToggleVisibility => "toggle-visibility",
        EntityClickTriggerAction.ToggleDoor => "toggle-door",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown click trigger action."),
    };

    public static EntityClickTriggerAction ParseAction(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "dialogue" => EntityClickTriggerAction.StartDialogue,
        "toggle-visibility" => EntityClickTriggerAction.ToggleVisibility,
        "toggle-door" => EntityClickTriggerAction.ToggleDoor,
        _ => EntityClickTriggerAction.ShowMessage,
    };
}
