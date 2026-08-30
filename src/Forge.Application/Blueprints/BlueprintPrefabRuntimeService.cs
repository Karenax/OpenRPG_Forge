using System.Security.Cryptography;
using System.Text;
using System.Numerics;
using Forge.Domain.Areas;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Application.Blueprints;

public enum AssetBudgetEvaluationStatus { Passed, Warning, Failed }

public sealed record AssetBudgetEvaluation(
    AssetTargetPlatform Platform,
    AssetBudgetEvaluationStatus Status,
    IReadOnlyList<string> ExceededMetrics);

public sealed record PrefabVariantRuntimeSnapshot(
    BlueprintId BlueprintId,
    BlueprintId? BaseBlueprintId,
    IReadOnlyList<PrefabVariantOverride> EffectiveOverrides,
    IReadOnlyList<string> Diagnostics);

public sealed record PrefabCompositionRuntimeNode(
    Guid NodeId,
    BlueprintId BlueprintId,
    Guid? ParentNodeId,
    string? Socket,
    TransformValue Transform,
    IReadOnlyList<string> Tags,
    EntityPropertyOverrides Overrides);

public sealed record PrefabCompositionRuntimeSnapshot(
    BlueprintId BlueprintId,
    IReadOnlyList<PrefabCompositionRuntimeNode> Nodes,
    IReadOnlyList<string> Rules,
    IReadOnlyList<string> Parameters,
    IReadOnlyList<string> Scripts,
    IReadOnlyList<string> Diagnostics);

public sealed class BlueprintPrefabRuntimeService
{
    public const string RequiredVariantMissingCode = "blueprint.variant.runtime_required_missing";
    public const string RequiredCompositionMissingCode = "blueprint.composition.runtime_required_missing";
    public const string RequiredAssetBudgetMissingCode = "blueprint.asset_budget.runtime_required_missing";
    private readonly ProjectDefinition _project;

    public BlueprintPrefabRuntimeService(ProjectDefinition project) =>
        _project = project ?? throw new ArgumentNullException(nameof(project));

    public PrefabVariantRuntimeSnapshot ResolveVariant(BlueprintId blueprintId, bool required)
    {
        var blueprint = _project.Blueprints.FirstOrDefault(item => item.Id == blueprintId);
        if (blueprint?.Authoring.PrefabVariant is not { } variant)
            return new(blueprintId, null, [], required ?
                [$"{RequiredVariantMissingCode}: blueprints/{blueprintId}/authoring/prefabVariant"] : []);
        var chain = new Stack<PrefabVariantDefinition>();
        var current = blueprint;
        var visited = new HashSet<BlueprintId>();
        while (current.Authoring.PrefabVariant is { } currentVariant && visited.Add(current.Id))
        {
            chain.Push(currentVariant);
            current = _project.Blueprints.FirstOrDefault(item => item.Id == currentVariant.BaseBlueprintId)!;
            if (current is null) break;
        }
        var effective = new Dictionary<string, PrefabVariantOverride>(StringComparer.Ordinal);
        while (chain.Count > 0)
            foreach (var item in chain.Pop().Overrides)
                effective[$"{item.Kind}:{item.Key}"] = item;
        return new(blueprintId, variant.BaseBlueprintId,
            effective.Values.OrderBy(item => item.Kind).ThenBy(item => item.Key, StringComparer.Ordinal).ToArray(), []);
    }

    public PrefabCompositionRuntimeSnapshot ResolveComposition(BlueprintId blueprintId, bool required)
    {
        var composition = _project.Blueprints.FirstOrDefault(item => item.Id == blueprintId)?.Authoring.PrefabComposition;
        if (composition is null)
            return new(blueprintId, [], [], [], [], required ?
                [$"{RequiredCompositionMissingCode}: blueprints/{blueprintId}/authoring/prefabComposition"] : []);
        return new(blueprintId, composition.Nodes.Select(item => new PrefabCompositionRuntimeNode(item.Id,
                item.BlueprintId, item.ParentNodeId, item.Socket, item.Transform, item.Tags, item.Overrides)).ToArray(),
            composition.AssemblyRules, composition.Parameters, composition.AssemblyScripts, []);
    }

    public IReadOnlyList<AssetBudgetEvaluation> EvaluateBudget(BlueprintId blueprintId, bool required)
    {
        var profile = _project.Blueprints.FirstOrDefault(item => item.Id == blueprintId)?.Authoring.AssetBudget;
        if (profile is null)
            return required
                ? [new(AssetTargetPlatform.Any, AssetBudgetEvaluationStatus.Failed, [RequiredAssetBudgetMissingCode])]
                : [];
        return profile.Platforms.Select(item => Evaluate(profile.Metrics, item)).ToArray();
    }

    public static AssetBudgetEvaluation Evaluate(AssetBudgetMetrics metrics, AssetPlatformBudget budget)
    {
        var exceeded = new List<string>();
        if (metrics.TriangleCount > budget.MaximumTriangles) exceeded.Add("triangles");
        if (metrics.MaterialCount > budget.MaximumMaterials) exceeded.Add("materials");
        if (metrics.TextureCount > budget.MaximumTextures) exceeded.Add("textures");
        if (metrics.MemoryBytes > budget.MaximumMemoryBytes) exceeded.Add("memory");
        if (metrics.AnimationCount > budget.MaximumAnimations) exceeded.Add("animations");
        if (metrics.LodLevelCount > budget.MaximumLodLevels) exceeded.Add("lod");
        if (metrics.CollisionShapeCount > budget.MaximumCollisionShapes) exceeded.Add("collision");
        var status = exceeded.Count == 0 ? AssetBudgetEvaluationStatus.Passed :
            budget.GateBuild ? AssetBudgetEvaluationStatus.Failed : AssetBudgetEvaluationStatus.Warning;
        return new(budget.Platform, status, exceeded);
    }
}

public sealed record PrefabCompositionPlacementPlan(
    BlueprintId CompositionBlueprintId,
    AreaId AreaId,
    IReadOnlyList<EntityInstance> Entities);

public static class PrefabCompositionPlacementPlanner
{
    public static PrefabCompositionPlacementPlan Build(ProjectDefinition project, BlueprintId blueprintId,
        AreaId areaId, Vector3Value origin, Guid commandSeed)
        => Build(project, blueprintId, areaId,
            new TransformValue(origin, QuaternionValue.Identity, Vector3Value.One), commandSeed);

    public static PrefabCompositionPlacementPlan Build(ProjectDefinition project, BlueprintId blueprintId,
        AreaId areaId, TransformValue placement, Guid commandSeed)
    {
        var blueprint = project.Blueprints.SingleOrDefault(item => item.Id == blueprintId)
            ?? throw new ArgumentException("Composition blueprint does not exist.", nameof(blueprintId));
        var composition = blueprint.Authoring.PrefabComposition
            ?? throw new ArgumentException("Blueprint has no prefab composition.", nameof(blueprintId));
        if (project.Areas.All(item => item.Id != areaId))
            throw new ArgumentException("Target area does not exist.", nameof(areaId));
        var diagnostics = BlueprintPrefabValidator.Validate(project, blueprint);
        if (diagnostics.Count > 0) throw new ArgumentException(diagnostics[0].Message, nameof(blueprintId));
        var byId = composition.Nodes.ToDictionary(item => item.Id);
        var transforms = new Dictionary<Guid, TransformValue>();
        TransformValue Resolve(PrefabCompositionNode node)
        {
            if (transforms.TryGetValue(node.Id, out var cached)) return cached;
            var parent = node.ParentNodeId is { } parentId ? Resolve(byId[parentId]) : placement;
            return transforms[node.Id] = Compose(parent, node.Transform);
        }
        var entities = composition.Nodes.Select(node => new EntityInstance(
            EntityId.From(DeterministicGuid(commandSeed, node.Id)), node.BlueprintId, Resolve(node),
            [.. node.Tags, $"composition:{blueprintId}", $"composition-node:{node.Id}"],
            MergeOverrides(blueprint.Authoring.PromotedDefaults, node.Overrides),
            $"={project.Blueprints.Single(item => item.Id == node.BlueprintId).Authoring.Version}"))
            .ToArray();
        return new(blueprintId, areaId, entities);
    }

    private static TransformValue Compose(TransformValue parent, TransformValue local)
    {
        var parentRotation = Quaternion.Normalize(new Quaternion(
            parent.Rotation.X, parent.Rotation.Y, parent.Rotation.Z, parent.Rotation.W));
        var localRotation = Quaternion.Normalize(new Quaternion(
            local.Rotation.X, local.Rotation.Y, local.Rotation.Z, local.Rotation.W));
        var scaledLocalPosition = new Vector3(
            local.Position.X * parent.Scale.X,
            local.Position.Y * parent.Scale.Y,
            local.Position.Z * parent.Scale.Z);
        var rotatedLocalPosition = Vector3.Transform(scaledLocalPosition, parentRotation);
        var rotation = Quaternion.Normalize(parentRotation * localRotation);
        return new TransformValue(
            new Vector3Value(
                parent.Position.X + rotatedLocalPosition.X,
                parent.Position.Y + rotatedLocalPosition.Y,
                parent.Position.Z + rotatedLocalPosition.Z),
            new QuaternionValue(rotation.X, rotation.Y, rotation.Z, rotation.W),
            new Vector3Value(
                parent.Scale.X * local.Scale.X,
                parent.Scale.Y * local.Scale.Y,
                parent.Scale.Z * local.Scale.Z));
    }

    private static EntityPropertyOverrides MergeOverrides(
        IReadOnlyList<EntityPropertyOverride> promotedDefaults,
        EntityPropertyOverrides nodeOverrides)
    {
        if (promotedDefaults.Count == 0) return nodeOverrides;
        var merged = promotedDefaults.ToDictionary(item => item.Key, StringComparer.Ordinal);
        foreach (var property in nodeOverrides.Properties) merged[property.Key] = property;
        return new EntityPropertyOverrides(EntityPropertyOverrides.CurrentSchemaVersion, merged.Values.ToArray());
    }

    private static Guid DeterministicGuid(Guid seed, Guid nodeId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg:composition:{seed:D}:{nodeId:D}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }
}
