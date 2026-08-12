using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;

namespace Forge.Application.Blueprints;

public static class BlueprintUpdatePlanner
{
    public const string MissingBlueprintCode = "blueprint.update.blueprint_missing";
    public const string ReadOnlyCode = "blueprint.update.read_only";
    public const string MissingSourceCode = "blueprint.update.source_missing";
    public const string SourceMismatchCode = "blueprint.update.source_mismatch";
    public const string MissingOverrideCode = "blueprint.update.override_missing";

    public static BlueprintUpdatePlan Build(
        ProjectDefinition project,
        BlueprintId blueprintId,
        EntityId sourceEntityId,
        IReadOnlyList<string> promotedOverrideKeys,
        BlueprintVersionBump versionBump,
        IReadOnlyList<EntityId>? excludedInstanceIds = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        var blueprint = project.Blueprints.FirstOrDefault(item => item.Id == blueprintId)
            ?? throw new BlueprintUpdatePlanException(MissingBlueprintCode, $"Blueprint '{blueprintId}' does not exist.",
                $"blueprints/{blueprintId}");
        if (blueprint.IsReadOnly())
            throw new BlueprintUpdatePlanException(ReadOnlyCode, $"Installed blueprint '{blueprint.Slug}' is read-only.",
                $"blueprints/{blueprintId}");

        var located = project.Areas.SelectMany(area => area.Entities.Select(entity => (area, entity)))
            .FirstOrDefault(item => item.entity.Id == sourceEntityId);
        if (located.entity is null)
            throw new BlueprintUpdatePlanException(MissingSourceCode, $"Source entity '{sourceEntityId}' does not exist.",
                $"entities/{sourceEntityId}");
        if (located.entity.BlueprintId != blueprintId)
            throw new BlueprintUpdatePlanException(SourceMismatchCode,
                $"Source entity '{sourceEntityId}' does not instantiate blueprint '{blueprintId}'.",
                $"areas/{located.area.Id}/entities/{sourceEntityId}");

        var requested = (promotedOverrideKeys ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var available = located.entity.Overrides.Properties.ToDictionary(item => item.Key, StringComparer.Ordinal);
        var missing = requested.FirstOrDefault(key => !available.ContainsKey(key));
        if (missing is not null)
            throw new BlueprintUpdatePlanException(MissingOverrideCode,
                $"Override '{missing}' is not present on source entity '{sourceEntityId}'.",
                $"areas/{located.area.Id}/entities/{sourceEntityId}/overrides/{missing}");

        var promoted = blueprint.Authoring.PromotedDefaults
            .Where(item => !requested.Contains(item.Key, StringComparer.Ordinal))
            .Concat(requested.Select(key => available[key]))
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToArray();
        var targetVersion = Bump(blueprint.Authoring.Version, versionBump);
        var excluded = (excludedInstanceIds ?? []).ToHashSet();
        var impacts = project.Areas.SelectMany(area => area.Entities
                .Where(entity => entity.BlueprintId == blueprintId)
                .Select(entity => new BlueprintInstanceMigrationImpact(
                    entity.Id,
                    $"areas/{area.Id}/entities/{entity.Id}",
                    entity.BlueprintVersionRequirement,
                    ResolveAction(entity, blueprint.Authoring.Version, targetVersion, excluded))))
            .OrderBy(item => item.DocumentPath, StringComparer.Ordinal)
            .ToArray();
        return new BlueprintUpdatePlan(blueprintId, sourceEntityId, blueprint.Authoring.Version, targetVersion,
            promoted, impacts);
    }

    private static string ResolveAction(EntityInstance entity, string previousVersion, string targetVersion,
        IReadOnlySet<EntityId> excluded)
    {
        if (excluded.Contains(entity.Id)) return "excluded";
        if (entity.BlueprintVersionRequirement == "*") return "inherits-latest";
        if (string.Equals(entity.BlueprintVersionRequirement, previousVersion, StringComparison.Ordinal) ||
            string.Equals(entity.BlueprintVersionRequirement, $"={previousVersion}", StringComparison.Ordinal))
            return "upgrade-exact-pin";
        return BlueprintVersionConstraint.Matches(entity.BlueprintVersionRequirement, targetVersion)
            ? "compatible-range"
            : "pinned-incompatible";
    }

    private static string Bump(string version, BlueprintVersionBump bump)
    {
        var core = version.Split('-', 2)[0].Split('+', 2)[0];
        var parts = core.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor) ||
            !int.TryParse(parts[2], out var patch))
            throw new ArgumentException($"Blueprint version '{version}' cannot be bumped.", nameof(version));
        return bump switch
        {
            BlueprintVersionBump.Patch => $"{major}.{minor}.{checked(patch + 1)}",
            BlueprintVersionBump.Minor => $"{major}.{checked(minor + 1)}.0",
            BlueprintVersionBump.Major => $"{checked(major + 1)}.0.0",
            _ => throw new ArgumentOutOfRangeException(nameof(bump)),
        };
    }
}

public sealed class BlueprintUpdatePlanException(string code, string message, string path) : Exception(message)
{
    public string Code { get; } = code;
    public string Path { get; } = path;
}
