using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Areas;
using Forge.Domain.Validation;

namespace Forge.Domain.Authoring;

public enum EditorVolumeKind
{
    Trigger,
    Navigation,
    Audio,
    Lighting,
    Fog,
    Gameplay,
}

public enum EditorPlacementDistribution
{
    Line,
    Grid,
    DeterministicScatter,
}

public enum EditorPlacementAlignment
{
    GridPlane,
    SurfaceNormal,
    Socket,
}

public enum EditorSplitAxis
{
    Horizontal,
    Vertical,
}

[Flags]
public enum EditorPermission
{
    None = 0,
    ViewProject = 1 << 0,
    EditAreas = 1 << 1,
    PlaceEntities = 1 << 2,
    EditNarrative = 1 << 3,
    EditScripts = 1 << 4,
    EditAssets = 1 << 5,
    RunPlaytests = 1 << 6,
    UseLiveDirector = 1 << 7,
    ConfigureProject = 1 << 8,
    BuildPreview = 1 << 9,
}

public enum EditorPanel
{
    ProjectExplorer,
    Viewport,
    Inspector,
    Palette,
    Dialogue,
    Quest,
    Script,
    Assets,
    Diagnostics,
    Assistant,
    LiveDirector,
}

public sealed record EditorRepeatedPlacementProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public bool Enabled { get; }
    public bool BrushMode { get; }
    public bool ContinuousStroke { get; }
    public EditorPlacementDistribution Distribution { get; }
    public EditorPlacementAlignment Alignment { get; }
    public float Spacing { get; }
    public int Columns { get; }
    public int Seed { get; }
    public float SurfaceOffset { get; }

    public EditorRepeatedPlacementProfile(Guid id, int schemaVersion, string provenance, bool enabled,
        bool brushMode, bool continuousStroke, EditorPlacementDistribution distribution,
        EditorPlacementAlignment alignment, float spacing, int columns, int seed, float surfaceOffset)
    {
        if (id == Guid.Empty) throw new ArgumentException("Repeated placement UUID must be non-empty.", nameof(id));
        if (schemaVersion != CurrentSchemaVersion) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (!float.IsFinite(spacing) || spacing is < 0.05f or > 128f)
            throw new ArgumentOutOfRangeException(nameof(spacing));
        if (columns is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(columns));
        if (!float.IsFinite(surfaceOffset) || surfaceOffset is < -16f or > 16f)
            throw new ArgumentOutOfRangeException(nameof(surfaceOffset));
        if (continuousStroke && !brushMode)
            throw new ArgumentException("Continuous placement requires brush mode.", nameof(continuousStroke));

        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance.Trim();
        Enabled = enabled;
        BrushMode = brushMode;
        ContinuousStroke = continuousStroke;
        Distribution = distribution;
        Alignment = alignment;
        Spacing = spacing;
        Columns = columns;
        Seed = seed;
        SurfaceOffset = surfaceOffset;
    }

    public static EditorRepeatedPlacementProfile CreateDefault(Guid ownerId, bool enabled) => new(
        EditorProductivityIds.Create(ownerId, "repeated-placement"), CurrentSchemaVersion,
        "openrpg-forge-default", enabled, enabled, enabled,
        EditorPlacementDistribution.Grid, EditorPlacementAlignment.GridPlane, 2f, 4, 2026, 0f);
}

public sealed record EditorMultiAreaProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public bool SplitViewEnabled { get; }
    public EditorSplitAxis SplitAxis { get; }
    public float SplitRatio { get; }
    public bool ComparisonReadOnly { get; }
    public bool CrossAreaCopyEnabled { get; }

    public EditorMultiAreaProfile(Guid id, int schemaVersion, string provenance, bool splitViewEnabled,
        EditorSplitAxis splitAxis, float splitRatio, bool comparisonReadOnly, bool crossAreaCopyEnabled)
    {
        if (id == Guid.Empty) throw new ArgumentException("Multi-area profile UUID must be non-empty.", nameof(id));
        if (schemaVersion != CurrentSchemaVersion) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (!float.IsFinite(splitRatio) || splitRatio is < 0.25f or > 0.75f)
            throw new ArgumentOutOfRangeException(nameof(splitRatio));
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance.Trim();
        SplitViewEnabled = splitViewEnabled;
        SplitAxis = splitAxis;
        SplitRatio = splitRatio;
        ComparisonReadOnly = comparisonReadOnly;
        CrossAreaCopyEnabled = crossAreaCopyEnabled;
    }

    public static EditorMultiAreaProfile CreateDefault(Guid ownerId) => new(
        EditorProductivityIds.Create(ownerId, "multi-area"), CurrentSchemaVersion,
        "openrpg-forge-default", false, EditorSplitAxis.Horizontal, 0.5f, true, true);
}

public sealed record EditorContextualHelpProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public bool Enabled { get; }
    public bool SourcedAssistantEnabled { get; }
    public string Locale { get; }
    public string ActiveTopicKey { get; }

    public EditorContextualHelpProfile(Guid id, int schemaVersion, string provenance, bool enabled,
        bool sourcedAssistantEnabled, string locale, string activeTopicKey)
    {
        if (id == Guid.Empty) throw new ArgumentException("Contextual help UUID must be non-empty.", nameof(id));
        if (schemaVersion != CurrentSchemaVersion) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);
        ArgumentException.ThrowIfNullOrWhiteSpace(activeTopicKey);
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance.Trim();
        Enabled = enabled;
        SourcedAssistantEnabled = sourcedAssistantEnabled;
        Locale = locale.Trim();
        ActiveTopicKey = activeTopicKey.Trim().ToLowerInvariant();
    }

    public static EditorContextualHelpProfile CreateDefault(Guid ownerId, bool enabled) => new(
        EditorProductivityIds.Create(ownerId, "contextual-help"), CurrentSchemaVersion,
        "openrpg-forge-default", enabled, true, "fr-FR", "workspace.overview");
}

public sealed record EditorRolePolicy
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public EditorRole Role { get; }
    public IReadOnlyList<EditorPanel> VisiblePanels { get; }
    public EditorPermission Permissions { get; }

    public EditorRolePolicy(Guid id, int schemaVersion, string provenance, EditorRole role,
        IReadOnlyList<EditorPanel> visiblePanels, EditorPermission permissions)
    {
        if (id == Guid.Empty) throw new ArgumentException("Role policy UUID must be non-empty.", nameof(id));
        if (schemaVersion != CurrentSchemaVersion) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        ArgumentNullException.ThrowIfNull(visiblePanels);
        var panels = visiblePanels.Distinct().Order().ToArray();
        if (panels.Length == 0 || !panels.Contains(EditorPanel.ProjectExplorer))
            throw new ArgumentException("Every editor role must expose the project explorer.", nameof(visiblePanels));
        if (!permissions.HasFlag(EditorPermission.ViewProject))
            throw new ArgumentException("Every editor role must be allowed to view the project.", nameof(permissions));
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance.Trim();
        Role = role;
        VisiblePanels = panels;
        Permissions = permissions;
    }

    public bool Allows(EditorPermission permission) => Permissions.HasFlag(permission);

    public static EditorRolePolicy CreateDefault(Guid ownerId, EditorRole role)
    {
        var common = EditorPermission.ViewProject | EditorPermission.RunPlaytests;
        var (panels, permissions) = role switch
        {
            EditorRole.Builder => (new[] { EditorPanel.ProjectExplorer, EditorPanel.Viewport, EditorPanel.Inspector,
                EditorPanel.Palette, EditorPanel.Diagnostics, EditorPanel.Assistant },
                common | EditorPermission.EditAreas | EditorPermission.PlaceEntities |
                EditorPermission.ConfigureProject | EditorPermission.BuildPreview),
            EditorRole.Writer => (new[] { EditorPanel.ProjectExplorer, EditorPanel.Dialogue, EditorPanel.Quest,
                EditorPanel.Inspector, EditorPanel.Assistant }, common | EditorPermission.EditNarrative |
                EditorPermission.BuildPreview),
            EditorRole.Scripter => (new[] { EditorPanel.ProjectExplorer, EditorPanel.Script, EditorPanel.Diagnostics,
                EditorPanel.Assistant }, common | EditorPermission.EditScripts | EditorPermission.BuildPreview),
            EditorRole.Artist => (new[] { EditorPanel.ProjectExplorer, EditorPanel.Viewport, EditorPanel.Inspector,
                EditorPanel.Palette, EditorPanel.Assets }, common | EditorPermission.EditAreas |
                EditorPermission.PlaceEntities | EditorPermission.EditAssets),
            EditorRole.GameMaster => (new[] { EditorPanel.ProjectExplorer, EditorPanel.Viewport, EditorPanel.Inspector,
                EditorPanel.LiveDirector, EditorPanel.Assistant }, common | EditorPermission.UseLiveDirector),
            EditorRole.Tester => (new[] { EditorPanel.ProjectExplorer, EditorPanel.Viewport, EditorPanel.Diagnostics,
                EditorPanel.Assistant }, common),
            _ => (new[] { EditorPanel.ProjectExplorer, EditorPanel.Viewport, EditorPanel.Inspector,
                EditorPanel.Palette, EditorPanel.Assistant }, common | EditorPermission.PlaceEntities),
        };
        return new(EditorProductivityIds.Create(ownerId, $"role-policy:{role}"), CurrentSchemaVersion,
            "openrpg-forge-default", role, panels, permissions);
    }
}

public sealed record EditorHelpTopic(Guid Id, int SchemaVersion, string Provenance, string Key, string Title,
    string Description, IReadOnlyList<string> PropertyDescriptions, IReadOnlyList<string> ForgeScriptExamples,
    IReadOnlyList<string> Dependencies, IReadOnlyList<string> Citations, bool AssistantGenerated)
{
    public const int CurrentSchemaVersion = 1;

    public EditorHelpTopic Validate()
    {
        if (Id == Guid.Empty || SchemaVersion != CurrentSchemaVersion || string.IsNullOrWhiteSpace(Provenance) ||
            string.IsNullOrWhiteSpace(Key) || string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Description))
            throw new ArgumentException("Contextual help topic identity and content are required.");
        if (AssistantGenerated && Citations.Count == 0)
            throw new ArgumentException("Assistant-generated help must cite a local source.");
        return this;
    }
}

public static class EditorContextualHelpCatalog
{
    public static IReadOnlyList<EditorHelpTopic> Topics { get; } =
    [
        Topic("workspace.overview", "Espace de travail", "Configure les profils, filtres, zones, aide et placements.",
            ["La zone active est l’unique cible de mutation.", "La comparaison reste en lecture seule."],
            ["workspace.active_area()", "workspace.selection()"], ["AREA", "PALETTE", "SCRIPT"], false),
        Topic("display.filters", "Filtres d’affichage", "Combine types, tags, auteur, état, calques et volumes.",
            ["Isoler limite l’affichage à la sélection.", "Fantôme conserve le contexte sans le rendre éditable."],
            ["workspace.selection()"], ["AREA", "PALETTE"], false),
        Topic("placement.repeated", "Placement répété", "Prépare une ligne, une grille, un scatter ou un pinceau atomique.",
            ["L’alignement de surface conserve un offset borné.", "Une série forme une seule entrée d’historique."],
            ["world.spawn(blueprint, position)"], ["PALETTE", "AREA"], false),
        Topic("areas.split", "Zones en vue partagée", "Affiche deux zones et copie une sélection vers la comparaison.",
            ["La vue secondaire est protégée contre les mutations implicites."],
            ["workspace.active_area()"], ["AREA"], false),
        Topic("assistant.sourced", "Aide assistée sourcée", "Synthèse locale bornée aux documents du projet.",
            ["Une réponse sans citation est refusée."], ["help.find(\"placement\")"], ["AI", "SCRIPT"], true),
    ];

    public static IReadOnlyList<EditorHelpTopic> Search(string query)
    {
        var text = query?.Trim() ?? string.Empty;
        return Topics.Where(topic => text.Length == 0 ||
            topic.Key.Contains(text, StringComparison.OrdinalIgnoreCase) ||
            topic.Title.Contains(text, StringComparison.OrdinalIgnoreCase) ||
            topic.Description.Contains(text, StringComparison.OrdinalIgnoreCase) ||
            topic.PropertyDescriptions.Any(item => item.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
            topic.Dependencies.Any(item => item.Contains(text, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(topic => topic.Key, StringComparer.Ordinal).ToArray();
    }

    private static EditorHelpTopic Topic(string key, string title, string description,
        IReadOnlyList<string> properties, IReadOnlyList<string> examples, IReadOnlyList<string> dependencies,
        bool assistantGenerated) => new EditorHelpTopic(EditorProductivityIds.Create(Guid.Parse("59000000-0000-4000-8000-000000000001"), key),
            EditorHelpTopic.CurrentSchemaVersion, "openrpg-forge-docs", key, title, description, properties,
            examples, dependencies,
            ["Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/01_SHELL.md",
             "docs/aurora-parity/tasks/P1-R059-SHELL-PRODUCTIVITY-ACCEPTANCE.md"], assistantGenerated).Validate();
}

public sealed record EditorProductivityRuntimeHandle(Guid ProfileId, int SchemaVersion, EditorRole Role,
    IReadOnlyList<Guid> VisibleLayerIds, IReadOnlyList<EditorVolumeKind> VisibleVolumeKinds,
    bool RepeatedPlacementEnabled, bool SplitViewEnabled, bool ContextualHelpEnabled,
    EditorPermission Permissions);

public sealed record EditorProductivityRuntimeResolution(EditorProductivityRuntimeHandle? Handle,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class EditorProductivityRuntimeResolver
{
    public const string RequiredMissingCode = "runtime.editor_productivity.required_missing";

    public static EditorProductivityRuntimeResolution Resolve(EditorWorkspaceProfile? profile, bool required)
    {
        if (profile is null)
            return required
                ? new(null, [new(RequiredMissingCode, "Required editor productivity profile is missing.",
                    "editorWorkspace")])
                : new(null, []);
        return new(new(profile.Id.Value, profile.SchemaVersion, profile.Role,
            profile.DisplayFilter.VisibleLayerIds, profile.DisplayFilter.VisibleVolumeKinds,
            profile.RepeatedPlacementProfile.Enabled, profile.MultiArea.SplitViewEnabled, profile.Help.Enabled,
            profile.RolePolicy.Permissions), []);
    }
}

internal static class EditorProductivityIds
{
    public static Guid Create(Guid ownerId, string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{ownerId:D}:{key}"));
        Span<byte> id = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(id);
        id[7] = (byte)((id[7] & 0x0f) | 0x50);
        id[8] = (byte)((id[8] & 0x3f) | 0x80);
        return new Guid(id);
    }
}
