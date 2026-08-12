using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Validation;

namespace Forge.Application.Projects;

public sealed class SetRulesProfilesHandler
{
    private readonly IClock _clock;

    public SetRulesProfilesHandler(IClock clock) =>
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, SetRulesProfilesCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Fail(state, command, "command.revision_conflict", "Project revision changed.");
        var area = state.Project.Areas.SingleOrDefault(item => item.Id == command.AreaId);
        if (area is null) return Fail(state, command, "rules.area_missing", "Area does not exist.");

        var previousModule = state.Project.Module.RuntimePolicies;
        var modulePolicies = new ModuleRuntimePolicyProfile(previousModule.Id, previousModule.SchemaVersion,
            previousModule.Heartbeats, previousModule.Rest, previousModule.SequenceAbort, "rules-command",
            previousModule.Cinematics, previousModule.Gameplay, command.ModuleProfile, previousModule.Extensions,
            previousModule.GameplayEvents, previousModule.World);
        var previousArea = area.RuntimePolicies;
        var areaPolicies = new AreaRuntimePolicyProfile(previousArea.Id, previousArea.SchemaVersion,
            previousArea.Heartbeats, previousArea.Rest, "rules-command", previousArea.Cinematics,
            command.AreaProfile, previousArea.Experience);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithRuntimePolicies(modulePolicies), state.Project.Blueprints,
            state.Project.Areas.Select(item => item.Id == area.Id ? item.WithRuntimePolicies(areaPolicies) : item)
                .ToArray(), state.Project.Dialogues, state.Project.Quests);
        var diagnostic = ProjectValidator.Validate(project).FirstOrDefault();
        if (diagnostic is not null) return Fail(state, command, diagnostic.Code, diagnostic.Message);

        var patch = new ProjectPatch("replace", "rules",
            $"Configure progression '{command.ModuleProfile.Progression.RulesetId}', difficulty " +
            $"'{command.ModuleProfile.ActiveDifficultyKey}', " +
            $"{command.ModuleProfile.Mutators.Mutators.Count(item => item.Enabled)} mutator(s), " +
            $"{command.AreaProfile.Perception.Modifiers.Count} perception modifier(s) and area PvP " +
            $"'{command.AreaProfile.Combat.PvpMode}'.");
        var inverse = new ProjectPatch("replace", "rules", "Restore previous module and area rules profiles.");
        var audit = new AuditEntry(command.CommandId, "set_rules_profiles", _clock.UtcNow, !command.DryRun);
        if (command.DryRun)
            return new CommandResult(true, state, command.BaseRevision, null, [], patch, inverse,
                audit with { Applied = false });

        var revision = state.Revision.Next();
        return new CommandResult(true, new ProjectState(project, revision), command.BaseRevision, revision, [], patch,
            inverse, audit);
    }

    private CommandResult Fail(ProjectState state, SetRulesProfilesCommand command, string code, string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            new AuditEntry(command.CommandId, "set_rules_profiles", _clock.UtcNow, false));
}
