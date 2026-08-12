using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.Validation;
namespace Forge.Application.Areas;

public sealed class SetAreaAuthoringHandler
{
    private readonly IClock _clock; public SetAreaAuthoringHandler(IClock clock) => _clock = clock;
    public CommandResult Handle(ProjectState state, SetAreaAuthoringCommand command)
    {
        if (command.BaseRevision != state.Revision) return Fail(state, command, "command.revision_conflict", "Project revision changed.");
        var area = state.Project.Areas.SingleOrDefault(item => item.Id == command.AreaId); if (area is null) return Fail(state, command, "area.authoring.area_missing", "Area does not exist.");
        var candidate = area.WithAuthoring(command.Authoring);
        var authoringDiagnostics = AreaExperienceAuthoringValidator.Validate(candidate)
            .Concat(AreaLayerRuntimeResolver.Resolve(candidate,
                candidate.Authoring.RequireLayerRuntimeMetadata).Diagnostics).ToArray();
        if (authoringDiagnostics.Length > 0)
            return new(false, state, command.BaseRevision, null,
                authoringDiagnostics.Select(item => new CommandDiagnostic(item.Code, $"{item.Path} · {item.Message}"))
                    .ToArray(), null, null,
                new AuditEntry(command.CommandId, "set_area_authoring", _clock.UtcNow, false));
        var patch = new ProjectPatch("replace", $"areas/{area.Id}/authoring",
            $"Set {command.Authoring.UserEvents.Count} event(s), {command.Authoring.Map.Pins.Count} map pin(s), " +
            $"{command.Authoring.Experience.AudioSnapshots.Count} audio snapshot(s), " +
            $"{command.Authoring.Experience.Visibility.Rooms.Count} visibility room(s), " +
            $"{command.Authoring.Experience.Visibility.Portals.Count} portal(s), " +
            $"{command.Authoring.Experience.Visibility.Occluders.Count} occluder(s), and " +
            $"{command.Authoring.Experience.Lod.AssetOverrides.Count} LOD override(s), and " +
            $"{command.Authoring.Layers.Count} editor layer(s)."); var inverse = new ProjectPatch("replace", $"areas/{area.Id}/authoring", "Restore previous authoring profile.");
        var audit = new AuditEntry(command.CommandId, "set_area_authoring", _clock.UtcNow, !command.DryRun); if (command.DryRun) return new(true, state, command.BaseRevision, null, [], patch, inverse, audit);
        var updated = candidate; var project = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module, state.Project.Blueprints, state.Project.Areas.Select(item => item.Id == area.Id ? updated : item).ToArray(), state.Project.Dialogues, state.Project.Quests);
        var projectDiagnostics = ProjectValidator.Validate(project);
        if (projectDiagnostics.Count > 0)
        {
            var diagnostic = projectDiagnostics[0];
            return Fail(state, command, diagnostic.Code, $"{diagnostic.Path} · {diagnostic.Message}");
        }
        var revision = state.Revision.Next(); return new(true, new ProjectState(project, revision), command.BaseRevision, revision, [], patch, inverse, audit);
    }
    private CommandResult Fail(ProjectState state, SetAreaAuthoringCommand command, string code, string message) => new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null, new AuditEntry(command.CommandId, "set_area_authoring", _clock.UtcNow, false));
}
