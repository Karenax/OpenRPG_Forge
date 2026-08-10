using Forge.Domain.Projects;

namespace Forge.Application.Projects;

public sealed record ProjectFoundationHandle(
    Guid Id,
    int SchemaVersion,
    Guid ProjectId,
    string Slug,
    string PackageNamespace,
    ProjectTemplateKind Template,
    bool RequiredAtRuntime);

public sealed record PlayerStartHandle(
    Guid Id,
    string Key,
    Guid AreaId,
    Guid SpawnEntityId,
    Guid? WaypointId,
    float OrientationDegrees,
    string GameMode,
    string EntryClass,
    string PlayerSelectionRule);

public static class ProjectFoundationScriptService
{
    public static ProjectFoundationHandle? Read(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var profile = project.Module.Metadata.Foundation;
        return profile is null ? null : new(profile.Id, profile.SchemaVersion, profile.Identity.ProjectId.Value,
            profile.Identity.Slug, profile.Identity.PackageNamespace, profile.Template, profile.RequiredAtRuntime);
    }

    public static PlayerStartHandle? ResolvePlayerStart(
        ProjectDefinition project,
        PlayerStartSelectionContext context)
    {
        ArgumentNullException.ThrowIfNull(project);
        var selected = PlayerStartProfileResolver.Resolve(project.Module.Metadata.Foundation, context);
        if (!selected.Succeeded) throw new InvalidOperationException(selected.Diagnostic!.Message);
        var item = selected.Profile;
        return item is null ? null : new(item.Id, item.Key, item.AreaId.Value, item.SpawnEntityId.Value,
            item.WaypointId, item.OrientationDegrees, item.GameMode, item.EntryClass, item.PlayerSelectionRule);
    }
}
