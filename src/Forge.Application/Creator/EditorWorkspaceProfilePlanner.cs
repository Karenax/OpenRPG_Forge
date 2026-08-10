using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;

namespace Forge.Application.Creator;

public static class EditorWorkspaceProfilePlanner
{
    public static EditorWorkspaceProfile OpenArea(EditorWorkspaceProfile profile, AreaId areaId, bool activate = true)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(areaId);
        var tabs = profile.OpenAreaTabs.Where(candidate => candidate != areaId).Append(areaId).TakeLast(8).ToArray();
        return Copy(profile, openAreaTabs: tabs, activeAreaId: activate ? areaId : profile.ActiveAreaId);
    }

    public static EditorWorkspaceProfile CloseArea(EditorWorkspaceProfile profile, AreaId areaId)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(areaId);
        var tabs = profile.OpenAreaTabs.Where(candidate => candidate != areaId).ToArray();
        var active = profile.ActiveAreaId == areaId ? tabs.LastOrDefault() : profile.ActiveAreaId;
        var comparison = profile.ComparisonAreaId == areaId ? null : profile.ComparisonAreaId;
        return Copy(
            profile,
            openAreaTabs: tabs,
            activeAreaId: active,
            overrideActive: true,
            comparisonAreaId: comparison,
            overrideComparison: true);
    }

    public static EditorWorkspaceProfile ReconcileAreas(
        EditorWorkspaceProfile profile,
        IReadOnlyList<AreaId> availableAreaIds,
        AreaId? preferredAreaId = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(availableAreaIds);
        var available = availableAreaIds.Distinct().ToArray();
        var availableSet = available.ToHashSet();
        var tabs = profile.OpenAreaTabs.Where(availableSet.Contains).ToList();
        var preferred = preferredAreaId is not null && availableSet.Contains(preferredAreaId)
            ? preferredAreaId
            : available.FirstOrDefault();
        if (tabs.Count == 0 && preferred is not null)
        {
            tabs.Add(preferred);
        }

        var active = profile.ActiveAreaId is not null && tabs.Contains(profile.ActiveAreaId)
            ? profile.ActiveAreaId
            : preferred is not null && tabs.Contains(preferred)
                ? preferred
                : tabs.FirstOrDefault();
        var comparison = profile.ComparisonAreaId is not null &&
                         tabs.Contains(profile.ComparisonAreaId) &&
                         profile.ComparisonAreaId != active
            ? profile.ComparisonAreaId
            : null;
        return Copy(
            profile,
            openAreaTabs: tabs,
            activeAreaId: active,
            overrideActive: true,
            comparisonAreaId: comparison,
            overrideComparison: true);
    }

    public static EditorWorkspaceProfile SetComparison(EditorWorkspaceProfile profile, AreaId? comparisonAreaId) =>
        Copy(profile, comparisonAreaId: comparisonAreaId, overrideComparison: true);

    public static EditorWorkspaceProfile RecordRecent(EditorWorkspaceProfile profile, BlueprintId blueprintId)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(blueprintId);
        var recents = new[] { blueprintId }.Concat(profile.RecentBlueprintIds.Where(candidate => candidate != blueprintId))
            .Take(24)
            .ToArray();
        return Copy(profile, recentBlueprintIds: recents);
    }

    public static EditorWorkspaceProfile TogglePersonalFavorite(EditorWorkspaceProfile profile, BlueprintId blueprintId)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(blueprintId);
        var favorites = profile.PersonalFavoriteBlueprintIds.Contains(blueprintId)
            ? profile.PersonalFavoriteBlueprintIds.Where(candidate => candidate != blueprintId).ToArray()
            : profile.PersonalFavoriteBlueprintIds.Append(blueprintId).TakeLast(64).ToArray();
        return Copy(profile, personalFavoriteBlueprintIds: favorites);
    }

    public static EditorWorkspaceProfile ConfigureAccessibility(
        EditorWorkspaceProfile profile,
        double uiScale,
        double textScale,
        string primaryFontFamily,
        IReadOnlyList<string> fallbackFontFamilies) =>
        Copy(
            profile,
            uiScale: uiScale,
            textScale: textScale,
            primaryFontFamily: primaryFontFamily,
            fallbackFontFamilies: fallbackFontFamilies);

    public static EditorWorkspaceProfile ConfigureShell(
        EditorWorkspaceProfile profile,
        EditorShellConfiguration shell) =>
        Copy(profile, shell: shell);

    private static EditorWorkspaceProfile Copy(
        EditorWorkspaceProfile source,
        IReadOnlyList<AreaId>? openAreaTabs = null,
        AreaId? activeAreaId = null,
        bool overrideActive = false,
        AreaId? comparisonAreaId = null,
        bool overrideComparison = false,
        IReadOnlyList<BlueprintId>? personalFavoriteBlueprintIds = null,
        IReadOnlyList<BlueprintId>? recentBlueprintIds = null,
        double? uiScale = null,
        double? textScale = null,
        string? primaryFontFamily = null,
        IReadOnlyList<string>? fallbackFontFamilies = null,
        EditorShellConfiguration? shell = null) =>
        new(
            source.Id,
            source.Name,
            source.Role,
            source.DisplayFilter,
            source.RepeatPlacement,
            openAreaTabs ?? source.OpenAreaTabs,
            overrideActive ? activeAreaId : activeAreaId ?? source.ActiveAreaId,
            overrideComparison ? comparisonAreaId : comparisonAreaId ?? source.ComparisonAreaId,
            source.ContextualHelpEnabled,
            source.Provenance,
            EditorWorkspaceProfile.CurrentSchemaVersion,
            uiScale ?? source.UiScale,
            textScale ?? source.TextScale,
            primaryFontFamily ?? source.PrimaryFontFamily,
            fallbackFontFamilies ?? source.FallbackFontFamilies,
            personalFavoriteBlueprintIds ?? source.PersonalFavoriteBlueprintIds,
            recentBlueprintIds ?? source.RecentBlueprintIds,
            shell ?? source.Shell);
}
