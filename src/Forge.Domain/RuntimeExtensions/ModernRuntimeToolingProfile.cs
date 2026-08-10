using Forge.Domain.RuntimePolicies;

namespace Forge.Domain.RuntimeExtensions;

public enum RuntimeReferenceCopyMode
{
    PreserveExternal,
    RemapInternal,
    DropExternal,
}

public enum AreaEffectShape
{
    Circle,
    Cone,
    Rectangle,
    Line,
}

public enum TargetIndicatorColorMode
{
    Standard,
    Deuteranopia,
    Tritanopia,
    HighContrast,
}

public sealed record RuntimeObjectCopyPolicy
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public int MaxCopiesPerTransaction { get; }
    public bool RequireServerAuthority { get; }
    public bool CopyOwnership { get; }
    public RuntimeReferenceCopyMode ReferenceMode { get; }
    public IReadOnlyList<string> StateComponents { get; }
    public string Provenance { get; }

    public RuntimeObjectCopyPolicy(Guid id, int schemaVersion, int maxCopiesPerTransaction,
        bool requireServerAuthority, bool copyOwnership, RuntimeReferenceCopyMode referenceMode,
        IReadOnlyList<string> stateComponents, string provenance)
    {
        ArgumentNullException.ThrowIfNull(stateComponents);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || maxCopiesPerTransaction is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(id), "Runtime copy requires schema v1 and a bounded transaction quota.");
        Id = id;
        SchemaVersion = schemaVersion;
        MaxCopiesPerTransaction = maxCopiesPerTransaction;
        RequireServerAuthority = requireServerAuthority;
        CopyOwnership = copyOwnership;
        ReferenceMode = referenceMode;
        StateComponents = stateComponents.Select(item => DataDrivenRuleSchema.NormalizeKey(item, nameof(stateComponents)))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }
}

public sealed record AreaEffectIndicatorProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public IReadOnlyList<AreaEffectShape> Shapes { get; }
    public float MaxRange { get; }
    public float MaxHeightDelta { get; }
    public bool EvaluateCover { get; }
    public bool RequireServerConfirmation { get; }
    public TargetIndicatorColorMode ColorMode { get; }
    public string ValidColor { get; }
    public string InvalidColor { get; }
    public string Provenance { get; }

    public AreaEffectIndicatorProfile(Guid id, int schemaVersion, IReadOnlyList<AreaEffectShape> shapes,
        float maxRange, float maxHeightDelta, bool evaluateCover, bool requireServerConfirmation,
        TargetIndicatorColorMode colorMode, string validColor, string invalidColor, string provenance)
    {
        ArgumentNullException.ThrowIfNull(shapes);
        var ordered = shapes.Distinct().Order().ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || ordered.Length == 0 ||
            maxRange is < 0.5f or > 250f || maxHeightDelta is < 0f or > 100f)
            throw new ArgumentOutOfRangeException(nameof(id), "AOE indicators require schema v1, shapes and bounded distances.");
        Id = id;
        SchemaVersion = schemaVersion;
        Shapes = ordered;
        MaxRange = maxRange;
        MaxHeightDelta = maxHeightDelta;
        EvaluateCover = evaluateCover;
        RequireServerConfirmation = requireServerConfirmation;
        ColorMode = colorMode;
        ValidColor = RequireColor(validColor, nameof(validColor));
        InvalidColor = RequireColor(invalidColor, nameof(invalidColor));
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }

    private static string RequireColor(string value, string parameter)
    {
        var normalized = DataDrivenRuleSchema.RequireText(value, parameter).TrimStart('#').ToLowerInvariant();
        if (normalized.Length is not (6 or 8) || normalized.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("Colors must be RGB or RGBA hexadecimal values.", parameter);
        return normalized;
    }
}

public sealed record ScriptableUiToolkitProfile
{
    public const int CurrentSchemaVersion = 1;
    public const string CurrentToolkitVersion = "2.0";

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string ToolkitVersion { get; }
    public bool ResponsiveLayout { get; }
    public bool Forms { get; }
    public bool Lists { get; }
    public bool Canvas { get; }
    public bool Accessibility { get; }
    public bool Localization { get; }
    public bool DeveloperInspector { get; }
    public string Provenance { get; }

    public ScriptableUiToolkitProfile(Guid id, int schemaVersion, string toolkitVersion,
        bool responsiveLayout, bool forms, bool lists, bool canvas, bool accessibility,
        bool localization, bool developerInspector, string provenance)
    {
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion ||
            !string.Equals(toolkitVersion, CurrentToolkitVersion, StringComparison.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(id), "The runtime UI toolkit requires schema v1 and toolkit 2.0.");
        Id = id;
        SchemaVersion = schemaVersion;
        ToolkitVersion = toolkitVersion;
        ResponsiveLayout = responsiveLayout;
        Forms = forms;
        Lists = lists;
        Canvas = canvas;
        Accessibility = accessibility;
        Localization = localization;
        DeveloperInspector = developerInspector;
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }
}

public sealed record ForgeScriptCompilerProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public bool Incremental { get; }
    public bool StableDiagnostics { get; }
    public bool PerformanceTelemetry { get; }
    public int MaxParallelism { get; }
    public int MaxSourceBytes { get; }
    public IReadOnlyList<string> ConformanceSuites { get; }
    public string Provenance { get; }

    public ForgeScriptCompilerProfile(Guid id, int schemaVersion, bool incremental, bool stableDiagnostics,
        bool performanceTelemetry, int maxParallelism, int maxSourceBytes,
        IReadOnlyList<string> conformanceSuites, string provenance)
    {
        ArgumentNullException.ThrowIfNull(conformanceSuites);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || maxParallelism is < 1 or > 32 ||
            maxSourceBytes is < 1024 or > 4_194_304)
            throw new ArgumentOutOfRangeException(nameof(id), "Compiler policy quotas are invalid.");
        Id = id;
        SchemaVersion = schemaVersion;
        Incremental = incremental;
        StableDiagnostics = stableDiagnostics;
        PerformanceTelemetry = performanceTelemetry;
        MaxParallelism = maxParallelism;
        MaxSourceBytes = maxSourceBytes;
        ConformanceSuites = conformanceSuites.Select(item => DataDrivenRuleSchema.NormalizeKey(item, nameof(conformanceSuites)))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }
}

public sealed record GeneratedDocumentationProfile
{
    public const int CurrentSchemaVersion = 1;
    public static readonly IReadOnlySet<string> SupportedCatalogs = new HashSet<string>(
        ["features", "forgescript-api", "schemas", "cli", "packages", "coverage"], StringComparer.Ordinal);

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string OutputDirectory { get; }
    public IReadOnlyList<string> Catalogs { get; }
    public bool IncludeMcpResources { get; }
    public string Provenance { get; }

    public GeneratedDocumentationProfile(Guid id, int schemaVersion, string outputDirectory,
        IReadOnlyList<string> catalogs, bool includeMcpResources, string provenance)
    {
        ArgumentNullException.ThrowIfNull(catalogs);
        var output = DataDrivenRuleSchema.RequireText(outputDirectory, nameof(outputDirectory)).Replace('\\', '/').Trim('/');
        var ordered = catalogs.Select(item => DataDrivenRuleSchema.NormalizeKey(item, nameof(catalogs)))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || output.Length == 0 ||
            Path.IsPathRooted(output) || output.Split('/').Any(segment => segment is "." or "..") ||
            ordered.Length == 0 || ordered.Any(item => !SupportedCatalogs.Contains(item)))
            throw new ArgumentException("Generated documentation requires a safe relative output and supported catalogs.");
        Id = id;
        SchemaVersion = schemaVersion;
        OutputDirectory = output;
        Catalogs = ordered;
        IncludeMcpResources = includeMcpResources;
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }
}

public sealed record ModernRuntimeToolingProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public RuntimeObjectCopyPolicy RuntimeCopy { get; }
    public AreaEffectIndicatorProfile AreaEffectIndicator { get; }
    public ScriptableUiToolkitProfile UiToolkit { get; }
    public ForgeScriptCompilerProfile Compiler { get; }
    public GeneratedDocumentationProfile Documentation { get; }
    public QualityAssuranceProfile QualityAssurance { get; }
    public string Provenance { get; }

    public ModernRuntimeToolingProfile(Guid id, int schemaVersion, RuntimeObjectCopyPolicy runtimeCopy,
        AreaEffectIndicatorProfile areaEffectIndicator, ScriptableUiToolkitProfile uiToolkit,
        ForgeScriptCompilerProfile compiler, GeneratedDocumentationProfile documentation, string provenance,
        QualityAssuranceProfile? qualityAssurance = null)
    {
        ArgumentNullException.ThrowIfNull(runtimeCopy);
        ArgumentNullException.ThrowIfNull(areaEffectIndicator);
        ArgumentNullException.ThrowIfNull(uiToolkit);
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(documentation);
        var quality = qualityAssurance ?? QualityAssuranceProfile.CreateDefault(id);
        var ids = new[] { id, runtimeCopy.Id, areaEffectIndicator.Id, uiToolkit.Id, compiler.Id, documentation.Id,
            quality.Id, quality.Network.Id, quality.Visual.Id, quality.Benchmarks.Id, quality.Profiler.Id,
            quality.Accessibility.Id, quality.SecurityFuzz.Id, quality.ClickableDiagnostics.Id,
            quality.MaliciousFixtures.Id, quality.AuroraParityCoverage.Id, quality.AcceptanceGates.Id }
            .Concat(quality.PlatformBudgets.Select(item => item.Id)).ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("Modern runtime tooling requires schema v1 and unique stable UUIDs.");
        Id = id;
        SchemaVersion = schemaVersion;
        RuntimeCopy = runtimeCopy;
        AreaEffectIndicator = areaEffectIndicator;
        UiToolkit = uiToolkit;
        Compiler = compiler;
        Documentation = documentation;
        QualityAssurance = quality;
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }

    public static ModernRuntimeToolingProfile CreateDefault(Guid moduleId)
    {
        Guid Stable(string key) => RuntimePolicyIdFactory.Create(moduleId, $"modern-tooling:{key}");
        return new ModernRuntimeToolingProfile(Stable("root"), 1,
            new RuntimeObjectCopyPolicy(Stable("runtime-copy"), 1, 32, true, true,
                RuntimeReferenceCopyMode.RemapInternal, ["transform", "combat", "inventory", "blackboard"], "migration-default"),
            new AreaEffectIndicatorProfile(Stable("aoe-indicator"), 1, Enum.GetValues<AreaEffectShape>(),
                60, 12, true, true, TargetIndicatorColorMode.Deuteranopia, "4dd6a8a0", "ff8a5ba0", "migration-default"),
            new ScriptableUiToolkitProfile(Stable("ui-toolkit"), 1, "2.0", true, true, true, true,
                true, true, true, "migration-default"),
            new ForgeScriptCompilerProfile(Stable("compiler"), 1, true, true, true, 4, 524_288,
                ["syntax", "diagnostics", "sandbox", "incremental"], "migration-default"),
            new GeneratedDocumentationProfile(Stable("documentation"), 1, "docs/generated",
                GeneratedDocumentationProfile.SupportedCatalogs.Order(StringComparer.Ordinal).ToArray(), true, "migration-default"),
            "migration-default");
    }
}
