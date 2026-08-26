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
        var semanticPatch = AreaEnvironmentPatch.Between(area.Environment, command.Environment);
        var environment = command.Environment;
        var candidateArea = area.WithEnvironment(environment);
        var environmentDiagnostics = AreaEnvironmentAuthoringValidator.Validate(candidateArea, environment);
        if (environmentDiagnostics.Count > 0)
            return new(false, state, command.BaseRevision, null,
                environmentDiagnostics.Select(item => new CommandDiagnostic(item.Code, $"{item.Path} · {item.Message}"))
                    .ToArray(), null, null,
                new AuditEntry(command.CommandId, "set_area_environment", _clock.UtcNow, false));
        var audioDiagnostics = AudioEmitterCatalogValidator.Validate(
            environment.Audio.Emitters,
            environment.Audio.Orchestration.MixBuses.Select(item => item.Id).ToHashSet(),
            path: $"areas/{area.Id}/environment/audio/emitters");
        if (audioDiagnostics.Count > 0)
            return new(false, state, command.BaseRevision, null,
                audioDiagnostics.Select(item => new CommandDiagnostic(item.Code, item.Message)).ToArray(),
                null, null, new AuditEntry(command.CommandId, "set_area_environment", _clock.UtcNow, false));
        var audioAuthoringDiagnostics = AudioAuthoringProfileValidator.Validate(
            environment.Audio.Authoring,
            environment.Audio.Playlist,
            environment.Audio.Emitters,
            $"areas/{area.Id}/environment/audio/authoring");
        if (audioAuthoringDiagnostics.Count > 0)
            return new(false, state, command.BaseRevision, null,
                audioAuthoringDiagnostics.Select(item => new CommandDiagnostic(item.Code,
                    $"{item.Path} · {item.Message}")).ToArray(),
                null, null, new AuditEntry(command.CommandId, "set_area_environment", _clock.UtcNow, false));
        var captionDiagnostics = AudioCaptionValidator.Validate(environment.Audio,
            $"areas/{area.Id}/environment/audio/orchestration/captions");
        if (captionDiagnostics.Count > 0)
            return new(false, state, command.BaseRevision, null,
                captionDiagnostics.Select(item => new CommandDiagnostic(item.Code,
                    $"{item.Path} · {item.Message}")).ToArray(),
                null, null, new AuditEntry(command.CommandId, "set_area_environment", _clock.UtcNow, false));
        var patch = new ProjectPatch("replace", $"areas/{area.Id}/environment",
            $"Set classification '{environment.Authoring.Classification.Key}', " +
            $"lighting '{environment.Authoring.Lighting.PresetKey}' with " +
            $"{environment.Authoring.Lighting.Curves.Count} curve point(s), " +
            $"{environment.Authoring.Lighting.Probes.Count} probe(s), " +
            $"{environment.Authoring.Lighting.VolumeOverrides.Count} override volume(s), and " +
            $"{environment.Authoring.Fog.LocalVolumes.Count} fog volume(s). " +
            $"Changed sections: {string.Join(", ", semanticPatch.Fields.Select(item => item.ToString()))}.");
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
