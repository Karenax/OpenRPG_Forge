using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.Validation;

namespace Forge.Application.Quests;

public sealed class SetQuestHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";

    private const string CommandName = "set_quest";
    private readonly IClock _clock;

    public SetQuestHandler(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public CommandResult Handle(ProjectState state, SetQuestCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Quest);
        if (command.BaseRevision != state.Revision)
        {
            return Failure(
                state,
                command,
                [new CommandDiagnostic(
                    RevisionConflictCode,
                    $"Command revision {command.BaseRevision} does not match project revision {state.Revision}.")]);
        }

        var previous = state.Project.Quests.SingleOrDefault(quest => quest.Id == command.Quest.Id);
        var quests = previous is null
            ? [.. state.Project.Quests, command.Quest]
            : state.Project.Quests.Select(quest => quest.Id == command.Quest.Id ? command.Quest : quest).ToArray();
        var updatedProject = new ProjectDefinition(
            state.Project.Id,
            state.Project.Title,
            state.Project.Module,
            state.Project.Blueprints,
            state.Project.Areas,
            state.Project.Dialogues,
            quests);
        var diagnostics = ProjectValidator.Validate(updatedProject);
        if (diagnostics.Count > 0)
        {
            return Failure(
                state,
                command,
                diagnostics.Select(diagnostic => new CommandDiagnostic(diagnostic.Code, diagnostic.Message)).ToArray());
        }

        var path = $"quests/{command.Quest.Id}";
        var patch = new ProjectPatch(
            previous is null ? "add" : "replace",
            path,
            $"Store quest '{command.Quest.DisplayName}'.");
        var inverse = previous is null
            ? new ProjectPatch("remove", path, $"Remove quest '{command.Quest.DisplayName}'.")
            : new ProjectPatch("replace", path, $"Restore quest '{previous.DisplayName}'.");
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
            new ProjectState(updatedProject, revision),
            command.BaseRevision,
            revision,
            [],
            patch,
            inverse,
            CreateAudit(command, true));
    }

    private CommandResult Failure(
        ProjectState state,
        SetQuestCommand command,
        IReadOnlyList<CommandDiagnostic> diagnostics) =>
        new(
            false,
            state,
            command.BaseRevision,
            null,
            diagnostics,
            null,
            null,
            CreateAudit(command, false));

    private AuditEntry CreateAudit(SetQuestCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);
}
