using Forge.Domain.Blueprints;

namespace Forge.Domain.Authoring;

public enum EditorExplorerGrouping
{
    Type,
    Folder,
    Area,
    Tag,
    Dependency,
    ValidationStatus,
}

public enum EditorViewportMode
{
    Editing,
    Simulation,
    Lighting,
    Navigation,
    Collisions,
    Performance,
}

public sealed record EditorShellLayout
{
    public const int CurrentSchemaVersion = 1;
    public const int MinimumExplorerWidth = 180;
    public const int MaximumExplorerWidth = 520;
    public const int MinimumContextWidth = 220;
    public const int MaximumContextWidth = 620;
    public const int MinimumBottomHeight = 100;
    public const int MaximumBottomHeight = 420;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public int ExplorerWidth { get; }
    public int ContextWidth { get; }
    public int BottomHeight { get; }
    public bool ExplorerVisible { get; }
    public bool InspectorVisible { get; }
    public bool DiagnosticsVisible { get; }
    public bool PaletteVisible { get; }
    public bool AssistantVisible { get; }

    public EditorShellLayout(
        Guid id,
        int schemaVersion,
        string provenance,
        int explorerWidth,
        int contextWidth,
        int bottomHeight,
        bool explorerVisible = true,
        bool inspectorVisible = true,
        bool diagnosticsVisible = true,
        bool paletteVisible = true,
        bool assistantVisible = true)
    {
        if (id == Guid.Empty) throw new ArgumentException("Shell layout UUID must be non-empty.", nameof(id));
        if (schemaVersion != CurrentSchemaVersion) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (explorerWidth is < MinimumExplorerWidth or > MaximumExplorerWidth)
            throw new ArgumentOutOfRangeException(nameof(explorerWidth));
        if (contextWidth is < MinimumContextWidth or > MaximumContextWidth)
            throw new ArgumentOutOfRangeException(nameof(contextWidth));
        if (bottomHeight is < MinimumBottomHeight or > MaximumBottomHeight)
            throw new ArgumentOutOfRangeException(nameof(bottomHeight));
        if (!explorerVisible && !inspectorVisible && !diagnosticsVisible && !paletteVisible && !assistantVisible)
            throw new ArgumentException("At least one authoring panel must remain visible.");

        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance.Trim();
        ExplorerWidth = explorerWidth;
        ContextWidth = contextWidth;
        BottomHeight = bottomHeight;
        ExplorerVisible = explorerVisible;
        InspectorVisible = inspectorVisible;
        DiagnosticsVisible = diagnosticsVisible;
        PaletteVisible = paletteVisible;
        AssistantVisible = assistantVisible;
    }

    public static EditorShellLayout CreateDefault(Guid profileId) => new(
        StableId(profileId, "shell-layout"), CurrentSchemaVersion, "openrpg-forge-default",
        228, 258, 120);

    private static Guid StableId(Guid source, string suffix) =>
        new(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{source:D}:{suffix}"))[..16]);
}

public sealed record EditorPaletteFilter
{
    public IReadOnlyList<BlueprintKind> Kinds { get; }
    public IReadOnlyList<string> Kits { get; }
    public IReadOnlyList<string> Styles { get; }
    public IReadOnlyList<string> Licenses { get; }
    public IReadOnlyList<string> Compatibility { get; }
    public IReadOnlyList<string> Tags { get; }
    public bool FavoritesOnly { get; }

    public EditorPaletteFilter(
        IReadOnlyList<BlueprintKind>? kinds = null,
        IReadOnlyList<string>? kits = null,
        IReadOnlyList<string>? styles = null,
        IReadOnlyList<string>? licenses = null,
        IReadOnlyList<string>? compatibility = null,
        IReadOnlyList<string>? tags = null,
        bool favoritesOnly = false)
    {
        Kinds = (kinds ?? Enum.GetValues<BlueprintKind>()).Distinct().Order().ToArray();
        if (Kinds.Count == 0) throw new ArgumentException("Palette filter must allow at least one blueprint kind.", nameof(kinds));
        Kits = Normalize(kits);
        Styles = Normalize(styles);
        Licenses = Normalize(licenses);
        Compatibility = Normalize(compatibility);
        Tags = Normalize(tags);
        FavoritesOnly = favoritesOnly;
    }

    public static EditorPaletteFilter ShowAll { get; } = new();

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string>? values) => (values ?? [])
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim().ToLowerInvariant())
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();
}

public sealed record EditorShellConfiguration
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public EditorShellLayout Layout { get; }
    public EditorExplorerGrouping ExplorerGrouping { get; }
    public EditorViewportMode ViewportMode { get; }
    public EditorPaletteFilter PaletteFilter { get; }
    public EditorDirectManipulationProfile DirectManipulation { get; }

    public EditorShellConfiguration(
        Guid id,
        int schemaVersion,
        string provenance,
        EditorShellLayout layout,
        EditorExplorerGrouping explorerGrouping,
        EditorViewportMode viewportMode,
        EditorPaletteFilter paletteFilter,
        EditorDirectManipulationProfile? directManipulation = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Shell configuration UUID must be non-empty.", nameof(id));
        if (schemaVersion != CurrentSchemaVersion) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(paletteFilter);
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance.Trim();
        Layout = layout;
        ExplorerGrouping = explorerGrouping;
        ViewportMode = viewportMode;
        PaletteFilter = paletteFilter;
        DirectManipulation = directManipulation ?? EditorDirectManipulationProfile.CreateDefault(id);
    }

    public static EditorShellConfiguration CreateDefault(Guid profileId) => new(
        StableId(profileId, "shell-configuration"), CurrentSchemaVersion, "openrpg-forge-default",
        EditorShellLayout.CreateDefault(profileId), EditorExplorerGrouping.Area,
        EditorViewportMode.Editing, EditorPaletteFilter.ShowAll,
        EditorDirectManipulationProfile.CreateDefault(profileId));

    private static Guid StableId(Guid source, string suffix) =>
        new(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{source:D}:{suffix}"))[..16]);
}
