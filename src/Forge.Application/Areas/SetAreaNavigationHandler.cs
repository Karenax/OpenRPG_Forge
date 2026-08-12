using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Areas;

public sealed class SetAreaNavigationHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string MissingAreaCode = "area.navigation.area_missing";

    private const string CommandName = "set_area_navigation";
    private readonly IClock _clock;

    public SetAreaNavigationHandler(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public CommandResult Handle(ProjectState state, SetAreaNavigationCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
        {
            return Failure(state, command, RevisionConflictCode,
                $"Command revision {command.BaseRevision} does not match project revision {state.Revision}.");
        }

        var area = state.Project.Areas.FirstOrDefault(candidate => candidate.Id == command.AreaId);
        if (area is null)
        {
            return Failure(state, command, MissingAreaCode, $"Area '{command.AreaId}' does not exist.");
        }

        var areas = state.Project.Areas.Select(candidate => candidate.Id == command.AreaId
            ? candidate.WithNavigation(command.Navigation)
            : candidate).ToArray();
        var project = new ProjectDefinition(
            state.Project.Id,
            state.Project.Title,
            state.Project.Module,
            state.Project.Blueprints,
            areas,
            state.Project.Dialogues,
            state.Project.Quests);
        var diagnostics = ProjectValidator.Validate(project);
        if (diagnostics.Count > 0)
        {
            return Failure(state, command, diagnostics[0].Code, diagnostics[0].Message);
        }

        var patch = new ProjectPatch(
            "replace",
            $"areas/{area.Id}/navigation",
            $"Configure {command.Navigation.AgentProfiles.Count} navigation agent(s) and " +
                $"{command.Navigation.OffMeshLinks.Count} off-mesh link(s), " +
                $"{command.Navigation.Anchors.Definitions.Count} anchor definition(s) and " +
                $"{command.Navigation.Anchors.Instances.Count} anchor instance(s), " +
                $"{command.Navigation.Waypoints.Waypoints.Count} waypoint(s) and " +
                $"{command.Navigation.Waypoints.PatrolRoutes.Count} patrol route(s), " +
                $"{command.Navigation.Waypoints.Locators.Count} locator(s) and " +
                $"{command.Navigation.Waypoints.Collections.Count} locator collection(s).");
        var inverse = new ProjectPatch(
            "replace",
            $"areas/{area.Id}/navigation",
            $"Restore navigation cell size {area.Navigation.CellSize:0.##} m.");
        if (command.DryRun)
        {
            return new CommandResult(
                true,
                state,
                command.BaseRevision,
                null,
                [],
                patch,
                inverse,
                CreateAudit(command, false));
        }

        var revision = state.Revision.Next();
        return new CommandResult(
            true,
            new ProjectState(project, revision),
            command.BaseRevision,
            revision,
            [],
            patch,
            inverse,
            CreateAudit(command, true));
    }

    private CommandResult Failure(
        ProjectState state,
        SetAreaNavigationCommand command,
        string code,
        string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            CreateAudit(command, false));

    private AuditEntry CreateAudit(SetAreaNavigationCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);
}
