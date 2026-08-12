using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;

namespace Forge.Domain.Authoring;

public sealed record EditorWorkspaceProfile
{
    public const int CurrentSchemaVersion = 4;

    public const double MinimumUiScale = 0.75;

    public const double MaximumUiScale = 2.0;

    public const double MinimumTextScale = 0.8;

    public const double MaximumTextScale = 2.0;

    public EditorWorkspaceProfileId Id { get; }

    public int SchemaVersion { get; }

    public string Name { get; }

    public EditorRole Role { get; }

    public EditorDisplayFilter DisplayFilter { get; }

    public bool RepeatPlacement { get; }

    public IReadOnlyList<AreaId> OpenAreaTabs { get; }

    public AreaId? ActiveAreaId { get; }

    public AreaId? ComparisonAreaId { get; }

    public bool ContextualHelpEnabled { get; }

    public string Provenance { get; }

    public double UiScale { get; }

    public double TextScale { get; }

    public string PrimaryFontFamily { get; }

    public IReadOnlyList<string> FallbackFontFamilies { get; }

    public IReadOnlyList<BlueprintId> PersonalFavoriteBlueprintIds { get; }

    public IReadOnlyList<BlueprintId> RecentBlueprintIds { get; }

    public EditorShellConfiguration Shell { get; }

    public EditorRepeatedPlacementProfile RepeatedPlacementProfile { get; }

    public EditorMultiAreaProfile MultiArea { get; }

    public EditorContextualHelpProfile Help { get; }

    public EditorRolePolicy RolePolicy { get; }

    public IReadOnlyList<NamedPaletteCollection> NamedPaletteCollections { get; }

    public EditorWorkspaceProfile(
        EditorWorkspaceProfileId id,
        string name,
        EditorRole role,
        EditorDisplayFilter displayFilter,
        bool repeatPlacement,
        IReadOnlyList<AreaId> openAreaTabs,
        AreaId? activeAreaId,
        AreaId? comparisonAreaId,
        bool contextualHelpEnabled,
        string provenance,
        int schemaVersion = CurrentSchemaVersion,
        double uiScale = 1,
        double textScale = 1,
        string primaryFontFamily = "Noto Sans",
        IReadOnlyList<string>? fallbackFontFamilies = null,
        IReadOnlyList<BlueprintId>? personalFavoriteBlueprintIds = null,
        IReadOnlyList<BlueprintId>? recentBlueprintIds = null,
        EditorShellConfiguration? shell = null,
        EditorRepeatedPlacementProfile? repeatedPlacementProfile = null,
        EditorMultiAreaProfile? multiArea = null,
        EditorContextualHelpProfile? help = null,
        EditorRolePolicy? rolePolicy = null,
        IReadOnlyList<NamedPaletteCollection>? namedPaletteCollections = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(displayFilter);
        ArgumentNullException.ThrowIfNull(openAreaTabs);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (schemaVersion != CurrentSchemaVersion) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        if (uiScale is < MinimumUiScale or > MaximumUiScale) throw new ArgumentOutOfRangeException(nameof(uiScale));
        if (textScale is < MinimumTextScale or > MaximumTextScale) throw new ArgumentOutOfRangeException(nameof(textScale));
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryFontFamily);
        var tabs = openAreaTabs.Distinct().ToArray();
        if (tabs.Length > 8)
        {
            throw new ArgumentException("An editor profile cannot keep more than eight area tabs open.", nameof(openAreaTabs));
        }

        if (activeAreaId is not null && !tabs.Contains(activeAreaId))
        {
            throw new ArgumentException("The active area must be present in the open tabs.", nameof(activeAreaId));
        }

        if (comparisonAreaId is not null && (!tabs.Contains(comparisonAreaId) || comparisonAreaId == activeAreaId))
        {
            throw new ArgumentException("The comparison area must be a different open tab.", nameof(comparisonAreaId));
        }

        var fallbacks = (fallbackFontFamilies ?? ["Noto Sans", "Segoe UI", "Arial"])
            .Select(family => family.Trim())
            .Where(family => family.Length > 0 && !string.Equals(family, primaryFontFamily.Trim(), StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();
        var favorites = (personalFavoriteBlueprintIds ?? []).Distinct().Take(64).ToArray();
        var recents = (recentBlueprintIds ?? []).Distinct().Take(24).ToArray();

        Id = id;
        SchemaVersion = schemaVersion;
        Name = name.Trim();
        Role = role;
        DisplayFilter = displayFilter;
        RepeatedPlacementProfile = repeatedPlacementProfile ??
            EditorRepeatedPlacementProfile.CreateDefault(id.Value, repeatPlacement);
        RepeatPlacement = RepeatedPlacementProfile.Enabled;
        OpenAreaTabs = tabs;
        ActiveAreaId = activeAreaId;
        ComparisonAreaId = comparisonAreaId;
        Help = help ?? EditorContextualHelpProfile.CreateDefault(id.Value, contextualHelpEnabled);
        ContextualHelpEnabled = Help.Enabled;
        Provenance = provenance.Trim();
        UiScale = uiScale;
        TextScale = textScale;
        PrimaryFontFamily = primaryFontFamily.Trim();
        FallbackFontFamilies = fallbacks;
        PersonalFavoriteBlueprintIds = favorites;
        RecentBlueprintIds = recents;
        Shell = shell ?? EditorShellConfiguration.CreateDefault(id.Value);
        MultiArea = multiArea ?? EditorMultiAreaProfile.CreateDefault(id.Value);
        RolePolicy = rolePolicy ?? EditorRolePolicy.CreateDefault(id.Value, role);
        if (RolePolicy.Role != role)
            throw new ArgumentException("The editor role policy must match the workspace role.", nameof(rolePolicy));
        NamedPaletteCollections = (namedPaletteCollections ?? []).OrderBy(item => item.Name, StringComparer.Ordinal)
            .ToArray();
        if (NamedPaletteCollections.Select(item => item.Id).Distinct().Count() != NamedPaletteCollections.Count ||
            NamedPaletteCollections.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != NamedPaletteCollections.Count)
            throw new ArgumentException("Named palette collection IDs and names must be unique.", nameof(namedPaletteCollections));
    }

    public static EditorWorkspaceProfile CreateDefault(EditorRole role, AreaId? areaId = null)
    {
        var name = role switch
        {
            EditorRole.GameMaster => "Maître de jeu",
            _ => role.ToString(),
        };
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:editor-profile:{role}"));
        Span<byte> id = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(id);
        id[7] = (byte)((id[7] & 0x0f) | 0x50);
        id[8] = (byte)((id[8] & 0x3f) | 0x80);
        return new EditorWorkspaceProfile(
            EditorWorkspaceProfileId.From(new Guid(id)),
            name,
            role,
            EditorDisplayFilter.ShowAll,
            repeatPlacement: role is EditorRole.Builder or EditorRole.Artist,
            areaId is null ? [] : [areaId],
            areaId,
            null,
            contextualHelpEnabled: role is EditorRole.Beginner or EditorRole.Builder,
            "openrpg-forge-default",
            uiScale: 1,
            textScale: 1,
            primaryFontFamily: "Noto Sans",
            fallbackFontFamilies: ["Segoe UI", "Arial"]);
    }
}
