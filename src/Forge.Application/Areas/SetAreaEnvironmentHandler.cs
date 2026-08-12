using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Projects;
using Forge.Domain.Audio;
using Forge.Domain.Areas;
using Forge.Domain.Validation;

namespace Forge.Application.Areas;

public sealed class SetAreaEnvironmentHandler
{
    private readonly IClock _clock;
    public SetAreaEnvironmentHandler(IClock clock) => _clock = clock;
    public CommandResult Handle(ProjectState state, SetAreaEnvironmentCommand command)
    {
        if (command.BaseRevision != state.Revision) return Fail(state, command, "command.revision_conflict", "Project revision changed.");
        var area = state.Project.Areas.SingleOrDefault(item => item.Id == command.AreaId);
        if (area is null) return Fail(state, command, "area.environment.area_missing", "Area does not exist.");
        var candidateArea = area.WithEnvironment(command.Environment);
        var environmentDiagnostics = AreaEnvironmentAuthoringValidator.Validate(candidateArea, command.Environment);
        if (environmentDiagnostics.Count > 0)
            return new(false, state, command.BaseRevision, null,
                environmentDiagnostics.Select(item => new CommandDiagnostic(item.Code, $"{item.Path} · {item.Message}"))
                    .ToArray(), null, null,
                new AuditEntry(command.CommandId, "set_area_environment", _clock.UtcNow, false));
        var audioDiagnostics = AudioEmitterCatalogValidator.Validate(
            command.Environment.Audio.Emitters,
            command.Environment.Audio.Orchestration.MixBuses.Select(item => item.Id).ToHashSet(),
            path: $"areas/{area.Id}/environment/audio/emitters");
        if (audioDiagnostics.Count > 0)
            return new(false, state, command.BaseRevision, null,
                audioDiagnostics.Select(item => new CommandDiagnostic(item.Code, item.Message)).ToArray(),
                null, null, new AuditEntry(command.CommandId, "set_area_environment", _clock.UtcNow, false));
        var audioAuthoringDiagnostics = AudioAuthoringProfileValidator.Validate(
            command.Environment.Audio.Authoring,
            command.Environment.Audio.Playlist,
            command.Environment.Audio.Emitters,
            $"areas/{area.Id}/environment/audio/authoring");
        if (audioAuthoringDiagnostics.Count > 0)
            return new(false, state, command.BaseRevision, null,
                audioAuthoringDiagnostics.Select(item => new CommandDiagnostic(item.Code,
                    $"{item.Path} · {item.Message}")).ToArray(),
                null, null, new AuditEntry(command.CommandId, "set_area_environment", _clock.UtcNow, false));
        var captionDiagnostics = AudioCaptionValidator.Validate(command.Environment.Audio,
            $"areas/{area.Id}/environment/audio/orchestration/captions");
        if (captionDiagnostics.Count > 0)
            return new(false, state, command.BaseRevision, null,
                captionDiagnostics.Select(item => new CommandDiagnostic(item.Code,
                    $"{item.Path} · {item.Message}")).ToArray(),
                null, null, new AuditEntry(command.CommandId, "set_area_environment", _clock.UtcNow, false));
        var patch = new ProjectPatch("replace", $"areas/{area.Id}/environment",
            $"Set classification '{command.Environment.Authoring.Classification.Key}', " +
            $"lighting '{command.Environment.Authoring.Lighting.PresetKey}' with " +
            $"{command.Environment.Authoring.Lighting.Curves.Count} curve point(s), " +
            $"{command.Environment.Authoring.Lighting.Probes.Count} probe(s), " +
            $"{command.Environment.Authoring.Lighting.VolumeOverrides.Count} override volume(s), and " +
            $"{command.Environment.Authoring.Fog.LocalVolumes.Count} fog volume(s).");
        var inverse = new ProjectPatch("replace", $"areas/{area.Id}/environment", "Restore previous environment profile.");
        var audit = new AuditEntry(command.CommandId, "set_area_environment", _clock.UtcNow, !command.DryRun);
        if (command.DryRun) return new(true, state, command.BaseRevision, null, [], patch, inverse, audit);
        var updated = candidateArea;
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module, state.Project.Blueprints,
            state.Project.Areas.Select(item => item.Id == area.Id ? updated : item).ToArray(), state.Project.Dialogues, state.Project.Quests);
        var projectDiagnostics = ProjectValidator.Validate(project);
        if (projectDiagnostics.Count > 0)
        {
            var diagnostic = projectDiagnostics[0];
            return Fail(state, command, diagnostic.Code, $"{diagnostic.Path} · {diagnostic.Message}");
        }
        var revision = state.Revision.Next();
        return new(true, new ProjectState(project, revision), command.BaseRevision, revision, [], patch, inverse, audit);
    }
    private CommandResult Fail(ProjectState state, SetAreaEnvironmentCommand command, string code, string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            new AuditEntry(command.CommandId, "set_area_environment", _clock.UtcNow, false));
}
