using Forge.Domain.Gameplay;

namespace Forge.Application.Gameplay;

public sealed record RadialActionPage(string ParentKey, IReadOnlyList<RadialActionDefinition> Actions,
    bool CanNavigateBack, string AccessibleSummary);

public static class RadialActionResolver
{
    public static RadialActionPage Open(RadialMenuProfile profile, string? parentKey = null,
        IReadOnlySet<string>? availableTags = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var parent = (parentKey ?? string.Empty).Trim().ToLowerInvariant();
        var tags = availableTags ?? new HashSet<string>(StringComparer.Ordinal);
        var actions = profile.Enabled
            ? profile.Actions.Where(action => action.ParentKey == parent &&
                (action.Tags.Count == 0 || tags.Count == 0 || action.Tags.Any(tags.Contains)))
                .OrderBy(action => action.Order).ThenBy(action => action.Key, StringComparer.Ordinal).ToArray()
            : [];
        var summary = actions.Length == 0
            ? "Aucune action disponible"
            : string.Join(", ", actions.Select(action => action.AccessibleLabel));
        return new RadialActionPage(parent, actions, parent.Length > 0, summary);
    }

    public static RadialActionDefinition? Select(RadialMenuProfile profile, string key) =>
        profile.Actions.SingleOrDefault(action => string.Equals(action.Key, key, StringComparison.OrdinalIgnoreCase));
}
