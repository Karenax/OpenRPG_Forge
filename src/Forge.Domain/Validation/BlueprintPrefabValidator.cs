using Forge.Domain.Blueprints;
using Forge.Domain.Projects;

namespace Forge.Domain.Validation;

public static class BlueprintPrefabValidator
{
    public const string VariantIdentityCode = "blueprint.variant.identity_invalid";
    public const string VariantReferenceCode = "blueprint.variant.reference_missing";
    public const string VariantCycleCode = "blueprint.variant.cycle";
    public const string VariantOverrideCode = "blueprint.variant.override_invalid";
    public const string CompositionIdentityCode = "blueprint.composition.identity_invalid";
    public const string CompositionReferenceCode = "blueprint.composition.reference_missing";
    public const string CompositionHierarchyCode = "blueprint.composition.hierarchy_invalid";
    public const string CompositionAssemblyCode = "blueprint.composition.assembly_invalid";
    public const string AssetBudgetIdentityCode = "blueprint.asset_budget.identity_invalid";
    public const string AssetBudgetMetricsCode = "blueprint.asset_budget.metrics_invalid";
    public const string AssetBudgetPlatformCode = "blueprint.asset_budget.platform_invalid";
    public const string AssetBudgetExceededCode = "blueprint.asset_budget.exceeded";

    public static IReadOnlyList<DomainDiagnostic> Validate(ProjectDefinition project, BlueprintDefinition blueprint)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(blueprint);
        var diagnostics = new List<DomainDiagnostic>();
        ValidateVariant(project, blueprint, diagnostics);
        ValidateComposition(project, blueprint, diagnostics);
        ValidateAssetBudget(blueprint, diagnostics);
        return diagnostics;
    }

    private static void ValidateVariant(ProjectDefinition project, BlueprintDefinition blueprint,
        ICollection<DomainDiagnostic> diagnostics)
    {
        var variant = blueprint.Authoring.PrefabVariant;
        if (variant is null) return;
        var path = $"blueprints/{blueprint.Id}/authoring/prefabVariant";
        if (variant.Id == Guid.Empty || variant.SchemaVersion != PrefabVariantDefinition.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(variant.Provenance))
            Add(diagnostics, VariantIdentityCode, "Prefab variant requires UUID, supported schema and provenance.", path);
        var baseBlueprint = project.Blueprints.FirstOrDefault(item => item.Id == variant.BaseBlueprintId);
        if (baseBlueprint is null || baseBlueprint.Kind != blueprint.Kind || variant.BaseBlueprintId == blueprint.Id)
            Add(diagnostics, VariantReferenceCode,
                "Prefab variant base must resolve to another blueprint of the same kind.", $"{path}/baseBlueprintId");
        if (HasVariantCycle(project, blueprint))
            Add(diagnostics, VariantCycleCode, "Prefab variant inheritance must be acyclic.", path);
        if (variant.Overrides.Count == 0 || variant.Overrides.Any(item =>
                string.IsNullOrWhiteSpace(item.Key) || item.Key.Length > 160 || string.IsNullOrWhiteSpace(item.Value)) ||
            variant.Overrides.Select(item => $"{item.Kind}:{item.Key}").Distinct(StringComparer.Ordinal).Count() !=
            variant.Overrides.Count)
            Add(diagnostics, VariantOverrideCode,
                "Prefab variant overrides require unique kind/key pairs and non-empty values.", $"{path}/overrides");
    }

    private static bool HasVariantCycle(ProjectDefinition project, BlueprintDefinition start)
    {
        var visited = new HashSet<BlueprintId>();
        var current = start;
        while (current.Authoring.PrefabVariant is { } variant)
        {
            if (!visited.Add(current.Id)) return true;
            current = project.Blueprints.FirstOrDefault(item => item.Id == variant.BaseBlueprintId)!;
            if (current is null) return false;
        }
        return false;
    }

    private static void ValidateComposition(ProjectDefinition project, BlueprintDefinition blueprint,
        ICollection<DomainDiagnostic> diagnostics)
    {
        var composition = blueprint.Authoring.PrefabComposition;
        if (composition is null) return;
        var path = $"blueprints/{blueprint.Id}/authoring/prefabComposition";
        if (composition.Id == Guid.Empty ||
            composition.SchemaVersion != PrefabCompositionDefinition.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(composition.Provenance))
            Add(diagnostics, CompositionIdentityCode,
                "Prefab composition requires UUID, supported schema and provenance.", path);
        var ids = composition.Nodes.Select(item => item.Id).ToArray();
        if (ids.Length == 0 || ids.Any(item => item == Guid.Empty) || ids.Distinct().Count() != ids.Length ||
            composition.Nodes.Count(item => item.ParentNodeId is null) != 1 || HasHierarchyCycle(composition))
            Add(diagnostics, CompositionHierarchyCode,
                "Prefab composition requires unique node UUIDs, one root and an acyclic hierarchy.", $"{path}/nodes");
        var knownIds = ids.ToHashSet();
        var knownBlueprints = project.Blueprints.Select(item => item.Id).ToHashSet();
        if (composition.Nodes.Any(item => !knownBlueprints.Contains(item.BlueprintId) ||
                                          item.BlueprintId == blueprint.Id ||
                                          item.ParentNodeId is { } parent && !knownIds.Contains(parent)))
            Add(diagnostics, CompositionReferenceCode,
                "Composition nodes must reference existing child blueprints and existing parents.", $"{path}/nodes");
        if (composition.AssemblyRules.Any(InvalidToken) || composition.Parameters.Any(InvalidToken) ||
            composition.AssemblyScripts.Any(item => !SafeScriptPath(item)))
            Add(diagnostics, CompositionAssemblyCode,
                "Assembly rules, parameters and script paths must be stable and traversal-free.", path);
    }

    private static bool HasHierarchyCycle(PrefabCompositionDefinition composition)
    {
        var parents = composition.Nodes.ToDictionary(item => item.Id, item => item.ParentNodeId);
        foreach (var node in composition.Nodes)
        {
            var visited = new HashSet<Guid>();
            var current = node.Id;
            while (parents.TryGetValue(current, out var parent) && parent is { } value)
            {
                if (!visited.Add(current)) return true;
                current = value;
            }
        }
        return false;
    }

    private static void ValidateAssetBudget(BlueprintDefinition blueprint,
        ICollection<DomainDiagnostic> diagnostics)
    {
        var profile = blueprint.Authoring.AssetBudget;
        if (profile is null) return;
        var path = $"blueprints/{blueprint.Id}/authoring/assetBudget";
        if (profile.Id == Guid.Empty || profile.SchemaVersion != BlueprintAssetBudgetProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(profile.Provenance))
            Add(diagnostics, AssetBudgetIdentityCode,
                "Asset budget requires UUID, supported schema and provenance.", path);
        var metrics = profile.Metrics;
        if (metrics.TriangleCount < 0 || metrics.MaterialCount < 0 || metrics.TextureCount < 0 ||
            metrics.MemoryBytes < 0 || metrics.AnimationCount < 0 || metrics.LodLevelCount < 0 ||
            metrics.CollisionShapeCount < 0)
            Add(diagnostics, AssetBudgetMetricsCode, "Asset metrics cannot be negative.", $"{path}/metrics");
        if (profile.Platforms.Count == 0 ||
            profile.Platforms.Select(item => item.Platform).Distinct().Count() != profile.Platforms.Count ||
            profile.Platforms.Any(item => item.MaximumTriangles <= 0 || item.MaximumMaterials <= 0 ||
                                          item.MaximumTextures <= 0 || item.MaximumMemoryBytes <= 0 ||
                                          item.MaximumAnimations < 0 || item.MaximumLodLevels < 0 ||
                                          item.MaximumCollisionShapes < 0))
            Add(diagnostics, AssetBudgetPlatformCode,
                "Asset budgets require unique platforms and positive bounded limits.", $"{path}/platforms");
        foreach (var budget in profile.Platforms.Where(item => item.GateBuild))
        {
            var exceeded = Exceeded(metrics, budget);
            if (exceeded.Count > 0)
                Add(diagnostics, AssetBudgetExceededCode,
                    $"Asset exceeds {budget.Platform} budget: {string.Join(", ", exceeded)}.",
                    $"{path}/platforms/{budget.Platform}");
        }
    }

    private static IReadOnlyList<string> Exceeded(AssetBudgetMetrics metrics, AssetPlatformBudget budget)
    {
        var result = new List<string>();
        if (metrics.TriangleCount > budget.MaximumTriangles) result.Add("triangles");
        if (metrics.MaterialCount > budget.MaximumMaterials) result.Add("materials");
        if (metrics.TextureCount > budget.MaximumTextures) result.Add("textures");
        if (metrics.MemoryBytes > budget.MaximumMemoryBytes) result.Add("memory");
        if (metrics.AnimationCount > budget.MaximumAnimations) result.Add("animations");
        if (metrics.LodLevelCount > budget.MaximumLodLevels) result.Add("lod");
        if (metrics.CollisionShapeCount > budget.MaximumCollisionShapes) result.Add("collision");
        return result;
    }

    private static bool InvalidToken(string value) => string.IsNullOrWhiteSpace(value) || value.Length > 160 ||
        value.Any(character => !char.IsLetterOrDigit(character) && character is not '.' and not '-' and not '_' and not ':');

    private static bool SafeScriptPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value)) return false;
        var normalized = value.Replace('\\', '/');
        return normalized.EndsWith(".as", StringComparison.OrdinalIgnoreCase) &&
               normalized.Split('/').All(segment => segment.Length > 0 && segment is not "." and not "..");
    }

    private static void Add(ICollection<DomainDiagnostic> diagnostics, string code, string message, string path) =>
        diagnostics.Add(new DomainDiagnostic(code, message, path));
}
