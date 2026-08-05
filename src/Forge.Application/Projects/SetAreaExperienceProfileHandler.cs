using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Validation;

namespace Forge.Application.Projects;

public sealed class SetAreaExperienceProfileHandler(IClock clock)
{
    private readonly IClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, SetAreaExperienceProfileCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Fail(state, command, "command.revision_conflict", "Project revision changed.");
        var area = state.Project.Areas.SingleOrDefault(item => item.Id == command.AreaId);
        if (area is null) return Fail(state, command, "experience.area_missing", "Area does not exist.");

        var previous = area.RuntimePolicies;
        var policies = new AreaRuntimePolicyProfile(previous.Id, previous.SchemaVersion, previous.Heartbeats,
            previous.Rest, "experience-command", previous.Cinematics, previous.Rules, command.Profile);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module,
            state.Project.Blueprints,
            state.Project.Areas.Select(item => item.Id == area.Id ? item.WithRuntimePolicies(policies) : item)
                .ToArray(), state.Project.Dialogues, state.Project.Quests);
        var diagnostic = ProjectValidator.Validate(project).FirstOrDefault();
        if (diagnostic is not null) return Fail(state, command, diagnostic.Code, diagnostic.Message);

        var patch = new ProjectPatch("replace", $"areas/{area.Id}/runtimePolicies/experience",
            $"Configure {command.Profile.AudioCues.Count} cue(s), {command.Profile.ScatterEmitters.Count} scatter " +
            $"emitter(s), {command.Profile.Voices.Count} voice profile(s), " +
            $"{command.Profile.PatrolRoutes.Count} cross-area patrol route(s) and loading presentation.");
        var inverse = new ProjectPatch("replace", $"areas/{area.Id}/runtimePolicies/experience",
            "Restore previous area audio, patrol and loading experience.");
        var audit = new AuditEntry(command.CommandId, "set_area_experience_profile", _clock.UtcNow,
            !command.DryRun);
        if (command.DryRun)
            return new CommandResult(true, state, command.BaseRevision, null, [], patch, inverse,
                audit with { Applied = false });
        var revision = state.Revision.Next();
        return new CommandResult(true, new ProjectState(project, revision), command.BaseRevision, revision, [], patch,
            inverse, audit);
    }

    private CommandResult Fail(ProjectState state, SetAreaExperienceProfileCommand command, string code,
        string message) => new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)],
        null, null, new AuditEntry(command.CommandId, "set_area_experience_profile", _clock.UtcNow, false));
}
