using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Application.Creatures;

public sealed record CreatureFoundationScriptView(
    string BlueprintId,
    string Slug,
    string DisplayName,
    string MovementMode,
    IReadOnlyDictionary<string, double> Resources,
    string SpawnEvent,
    string SpawnHandler);

public static class CreatureFoundationScriptService
{
    public static CreatureFoundationScriptView? Read(ProjectDefinition project, BlueprintId blueprintId)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(blueprintId);
        var blueprint = project.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        var profile = blueprint?.CreatureFoundation;
        if (blueprint is null || profile is null) return null;
        var resolved = CreatureFoundationRuntimeResolver.Resolve(profile, profile.RequiredAtRuntime);
        if (!resolved.Succeeded || resolved.Profile is null) return null;
        return new(blueprint.Id.ToString(), blueprint.Slug, blueprint.DisplayName, profile.Movement.Mode.ToString(),
            profile.Resources.ToDictionary(item => item.ResourceId, item => item.Current, StringComparer.Ordinal),
            profile.Spawned.EventName, profile.Spawned.Handler);
    }
}
