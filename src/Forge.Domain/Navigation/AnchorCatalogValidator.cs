using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Domain.Navigation;

public static class AnchorCatalogValidator
{
    public const string CatalogIdentityCode = "anchor.catalog.identity_invalid";
    public const string DefinitionIdentityCode = "anchor.definition.identity_invalid";
    public const string DefinitionDuplicateCode = "anchor.definition.duplicate";
    public const string LocalizationCode = "anchor.definition.localization_invalid";
    public const string BlueprintReferenceCode = "anchor.definition.blueprint_missing";
    public const string InstanceIdentityCode = "anchor.instance.identity_invalid";
    public const string InstanceDuplicateCode = "anchor.instance.duplicate";
    public const string DefinitionReferenceCode = "anchor.instance.definition_missing";
    public const string TransformBoundsCode = "anchor.instance.transform_out_of_bounds";
    public const string SpawnProfileCode = "anchor.spawn.profile_invalid";
    public const string SpawnNavigationCode = "anchor.spawn.navigation_incoherent";
    public const string SpawnFallbackCode = "anchor.spawn.fallback_missing";
    public const string PortalProfileCode = "anchor.portal.profile_invalid";
    public const string PortalDestinationCode = "anchor.portal.destination_missing";
    public const string RequiredContentCode = "anchor.catalog.required_empty";

    public static IReadOnlyList<DomainDiagnostic> Validate(ProjectDefinition project, AreaDefinition area)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(area);
        var result = new List<DomainDiagnostic>();
        var catalog = area.Navigation.Anchors;
        var root = $"areas/{area.Id}/navigation/anchors";
        if (catalog.Id == Guid.Empty || catalog.SchemaVersion != AnchorCatalog.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(catalog.Provenance) || catalog.AreaId != area.Id)
            result.Add(new(CatalogIdentityCode, $"Anchor catalog for area '{area.Id}' has invalid identity.", root));
        if (catalog.RequiredAtRuntime && catalog.Instances.Count == 0)
            result.Add(new(RequiredContentCode,
                $"Required anchor catalog for area '{area.Id}' cannot be empty.", root));

        var allDefinitions = project.Areas.SelectMany(item => item.Navigation.Anchors.Definitions).ToArray();
        var allInstances = project.Areas.SelectMany(item => item.Navigation.Anchors.Instances).ToArray();
        var duplicateDefinitionIds = allDefinitions.GroupBy(item => item.Id).Where(group => group.Count() > 1)
            .Select(group => group.Key).ToHashSet();
        var duplicateInstanceIds = allInstances.GroupBy(item => item.Id).Where(group => group.Count() > 1)
            .Select(group => group.Key).ToHashSet();
        var duplicateSlugs = allDefinitions.GroupBy(item => item.Slug, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        var duplicateOrders = catalog.Instances.GroupBy(item => item.DisplayOrder).Where(group => group.Count() > 1)
            .Select(group => group.Key).ToHashSet();
        // Keep validation diagnostic-first: duplicate definitions are reported above and must not
        // make validation itself throw before callers can inspect the diagnostics.
        var definitions = catalog.Definitions.GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => group.First());
        var blueprints = project.Blueprints.ToDictionary(item => item.Id);

        foreach (var definition in catalog.Definitions)
        {
            var path = $"{root}/definitions/{definition.Id}";
            if (definition.Id == Guid.Empty || definition.SchemaVersion != AnchorDefinition.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(definition.Provenance) || string.IsNullOrWhiteSpace(definition.Slug) ||
                !Enum.IsDefined(definition.Kind) || !Enum.IsDefined(definition.Scope))
                result.Add(new(DefinitionIdentityCode, $"Anchor definition '{definition.Id}' has invalid identity.", path));
            if (duplicateDefinitionIds.Contains(definition.Id) || duplicateSlugs.Contains(definition.Slug))
                result.Add(new(DefinitionDuplicateCode,
                    $"Anchor definition '{definition.Id}' must have a globally unique UUID and slug.", path));
            if (definition.Label.Id == Guid.Empty ||
                definition.Label.SchemaVersion != Dialogues.LocalizedText.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(definition.Label.Provenance) ||
                string.IsNullOrWhiteSpace(definition.Label.Key) || string.IsNullOrWhiteSpace(definition.Label.Fallback))
                result.Add(new(LocalizationCode, $"Anchor definition '{definition.Id}' requires localized text.",
                    $"{path}/label"));
            if (definition.BlueprintId is { } blueprintId && !blueprints.TryGetValue(blueprintId, out var blueprint) ||
                definition.Kind == AnchorKind.Spawn &&
                (definition.BlueprintId is null || !blueprints.TryGetValue(definition.BlueprintId, out var spawnBlueprint) ||
                 spawnBlueprint.Kind != BlueprintKind.PlayerSpawn))
                result.Add(new(BlueprintReferenceCode,
                    $"Anchor definition '{definition.Id}' references an invalid blueprint.",
                    $"{path}/blueprintId"));
        }

        foreach (var instance in catalog.Instances)
        {
            var path = $"{root}/instances/{instance.Id}";
            if (instance.Id == Guid.Empty || instance.SchemaVersion != AnchorInstance.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(instance.Provenance) || instance.DisplayOrder < 1 ||
                duplicateOrders.Contains(instance.DisplayOrder) ||
                instance.Overrides.SchemaVersion != Entities.EntityPropertyOverrides.CurrentSchemaVersion)
                result.Add(new(InstanceIdentityCode,
                    $"Anchor instance '{instance.Id}' has invalid identity, order or overrides.", path));
            if (duplicateInstanceIds.Contains(instance.Id))
                result.Add(new(InstanceDuplicateCode,
                    $"Anchor instance UUID '{instance.Id}' is used more than once.", $"{path}/id"));
            if (!definitions.TryGetValue(instance.DefinitionId, out var definition))
            {
                result.Add(new(DefinitionReferenceCode,
                    $"Anchor instance '{instance.Id}' references missing definition '{instance.DefinitionId}'.",
                    $"{path}/definitionId"));
                continue;
            }
            var position = instance.Transform.Position;
            if (Math.Abs(position.X) > area.Dimensions.Width || Math.Abs(position.Z) > area.Dimensions.Depth ||
                Math.Abs(position.Y) > 4096)
                result.Add(new(TransformBoundsCode,
                    $"Anchor instance '{instance.Id}' lies outside area authoring bounds.", $"{path}/transform"));
            ValidateSpawn(area, catalog, definition, instance, path, result);
            ValidatePortal(project, definition, instance, path, result);
        }
        return result;
    }

    private static void ValidateSpawn(AreaDefinition area, AnchorCatalog catalog, AnchorDefinition definition,
        AnchorInstance instance, string path, ICollection<DomainDiagnostic> result)
    {
        if (instance.Spawn is null)
        {
            if (definition.Kind == AnchorKind.Spawn)
                result.Add(new(SpawnProfileCode, $"Spawn anchor '{instance.Id}' requires a spawn profile.",
                    $"{path}/spawn"));
            return;
        }
        var spawn = instance.Spawn;
        if (definition.Kind != AnchorKind.Spawn || string.IsNullOrWhiteSpace(spawn.Profile) ||
            string.IsNullOrWhiteSpace(spawn.Team) || spawn.Priority is < 1 or > 100 ||
            spawn.Clearance.X <= 0 || spawn.Clearance.Y <= 0 || spawn.Clearance.Z <= 0 ||
            !float.IsFinite(spawn.OrientationDegrees))
            result.Add(new(SpawnProfileCode, $"Spawn anchor '{instance.Id}' has an invalid profile.",
                $"{path}/spawn"));
        if (spawn.RequireNavigation && !area.Navigation.AgentProfiles.Any(agent =>
                agent.Radius * 2 <= spawn.Clearance.X && agent.Height <= spawn.Clearance.Y))
            result.Add(new(SpawnNavigationCode,
                $"Spawn anchor '{instance.Id}' clearance cannot fit any navigation agent.",
                $"{path}/spawn/clearance"));
        if (spawn.FallbackInstanceId is { } fallback &&
            (fallback == instance.Id || catalog.Instances.All(item => item.Id != fallback || item.Spawn is null)))
            result.Add(new(SpawnFallbackCode,
                $"Spawn anchor '{instance.Id}' references an invalid fallback '{fallback}'.",
                $"{path}/spawn/fallbackInstanceId"));
    }

    private static void ValidatePortal(ProjectDefinition project, AnchorDefinition definition,
        AnchorInstance instance, string path, ICollection<DomainDiagnostic> result)
    {
        if (instance.Portal is null)
        {
            if (definition.Kind == AnchorKind.TransitionDestination)
                result.Add(new(PortalProfileCode,
                    $"Transition anchor '{instance.Id}' requires a portal destination.", $"{path}/portal"));
            return;
        }
        var portal = instance.Portal;
        if (definition.Kind != AnchorKind.TransitionDestination || !Enum.IsDefined(portal.Orientation) ||
            !Enum.IsDefined(portal.Velocity) || !float.IsFinite(portal.FixedYawDegrees))
            result.Add(new(PortalProfileCode,
                $"Transition anchor '{instance.Id}' has an invalid portal policy.", $"{path}/portal"));
        var targetArea = project.Areas.SingleOrDefault(item => item.Id == portal.TargetAreaId);
        if (targetArea is null || targetArea.Navigation.Anchors.Instances.All(item =>
                item.Id != portal.TargetAnchorInstanceId))
            result.Add(new(PortalDestinationCode,
                $"Transition anchor '{instance.Id}' references a missing area or destination anchor.",
                $"{path}/portal/targetAnchorInstanceId"));
    }
}
