using Forge.Domain.RuntimePolicies;

namespace Forge.Domain.RuntimeExtensions;

public enum RuntimeColorVisionMode
{
    Standard,
    Deuteranopia,
    Protanopia,
    Tritanopia,
}

public sealed record AccessibilityQualityProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public bool EditorKeyboardOnly { get; }
    public bool EditorScreenReaderLabels { get; }
    public bool EditorColorIndependentStatus { get; }
    public bool EditorShortcutReference { get; }
    public double MinimumContrastRatio { get; }
    public double EditorTextScale { get; }
    public bool RuntimeSubtitles { get; }
    public bool RuntimeCaptions { get; }
    public double RuntimeTextScale { get; }
    public bool RuntimeHighContrast { get; }
    public RuntimeColorVisionMode RuntimeColorVisionMode { get; }
    public bool RuntimeInputAssists { get; }
    public bool RuntimeReducedMotion { get; }
    public bool RuntimeDifficultyAssists { get; }
    public string Provenance { get; }

    public AccessibilityQualityProfile(Guid id, int schemaVersion, bool editorKeyboardOnly,
        bool editorScreenReaderLabels, bool editorColorIndependentStatus, bool editorShortcutReference,
        double minimumContrastRatio, double editorTextScale, bool runtimeSubtitles, bool runtimeCaptions,
        double runtimeTextScale, bool runtimeHighContrast, RuntimeColorVisionMode runtimeColorVisionMode,
        bool runtimeInputAssists, bool runtimeReducedMotion, bool runtimeDifficultyAssists, string provenance)
    {
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion ||
            !double.IsFinite(minimumContrastRatio) || minimumContrastRatio is < 3 or > 21 ||
            !double.IsFinite(editorTextScale) || editorTextScale is < .8 or > 2 ||
            !double.IsFinite(runtimeTextScale) || runtimeTextScale is < .8 or > 2)
            throw new ArgumentOutOfRangeException(nameof(id),
                "Accessibility requires schema v1, WCAG contrast and bounded text scales.");
        Id = id;
        SchemaVersion = schemaVersion;
        EditorKeyboardOnly = editorKeyboardOnly;
        EditorScreenReaderLabels = editorScreenReaderLabels;
        EditorColorIndependentStatus = editorColorIndependentStatus;
        EditorShortcutReference = editorShortcutReference;
        MinimumContrastRatio = minimumContrastRatio;
        EditorTextScale = editorTextScale;
        RuntimeSubtitles = runtimeSubtitles;
        RuntimeCaptions = runtimeCaptions;
        RuntimeTextScale = runtimeTextScale;
        RuntimeHighContrast = runtimeHighContrast;
        RuntimeColorVisionMode = runtimeColorVisionMode;
        RuntimeInputAssists = runtimeInputAssists;
        RuntimeReducedMotion = runtimeReducedMotion;
        RuntimeDifficultyAssists = runtimeDifficultyAssists;
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }

    public static AccessibilityQualityProfile CreateDefault(Guid ownerId) => new(
        RuntimePolicyIdFactory.Create(ownerId, "quality-assurance:accessibility"), 1,
        true, true, true, true, 4.5, 1.25, true, true, 1.2, true,
        RuntimeColorVisionMode.Deuteranopia, true, false, true, "migration-default");
}

public sealed record SecurityFuzzProfile
{
    public const int CurrentSchemaVersion = 1;
    public static IReadOnlySet<string> SupportedBoundaries { get; } = new HashSet<string>(
        ["project-json", "package-archive", "forgescript", "network-json", "image-header", "model-header", "path-sandbox"],
        StringComparer.Ordinal);

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public int Seed { get; }
    public int CasesPerBoundary { get; }
    public int MaximumInputBytes { get; }
    public int TimeBudgetMilliseconds { get; }
    public IReadOnlyList<string> Boundaries { get; }
    public bool RequireNoCrash { get; }
    public bool RequireNoPathEscape { get; }
    public string Provenance { get; }

    public SecurityFuzzProfile(Guid id, int schemaVersion, int seed, int casesPerBoundary,
        int maximumInputBytes, int timeBudgetMilliseconds, IReadOnlyList<string> boundaries,
        bool requireNoCrash, bool requireNoPathEscape, string provenance)
    {
        ArgumentNullException.ThrowIfNull(boundaries);
        var normalized = boundaries.Select(value => DataDrivenRuleSchema.NormalizeKey(value, nameof(boundaries)))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || seed < 0 ||
            casesPerBoundary is < 1 or > 10_000 || maximumInputBytes is < 64 or > 16_777_216 ||
            timeBudgetMilliseconds is < 100 or > 3_600_000 || normalized.Length == 0 ||
            normalized.Any(value => !SupportedBoundaries.Contains(value)))
            throw new ArgumentOutOfRangeException(nameof(id),
                "Security fuzzing requires schema v1 and supported, bounded input surfaces.");
        Id = id;
        SchemaVersion = schemaVersion;
        Seed = seed;
        CasesPerBoundary = casesPerBoundary;
        MaximumInputBytes = maximumInputBytes;
        TimeBudgetMilliseconds = timeBudgetMilliseconds;
        Boundaries = normalized;
        RequireNoCrash = requireNoCrash;
        RequireNoPathEscape = requireNoPathEscape;
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }

    public static SecurityFuzzProfile CreateDefault(Guid ownerId) => new(
        RuntimePolicyIdFactory.Create(ownerId, "quality-assurance:security-fuzz"), 1,
        2026, 16, 65_536, 10_000, SupportedBoundaries.Order(StringComparer.Ordinal).ToArray(),
        true, true, "migration-default");
}
