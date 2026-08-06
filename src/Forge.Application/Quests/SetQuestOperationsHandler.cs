using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.Validation;

namespace Forge.Application.Quests;

public sealed class SetQuestOperationsHandler
{
    private readonly IClock _clock;

    public SetQuestOperationsHandler(IClock clock) => _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, SetQuestOperationsCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, "command.revision_conflict", "The project revision changed.");
        var quest = state.Project.Quests.SingleOrDefault(item => item.Id == command.QuestId);
        if (quest is null)
            return Failure(state, command, "quest.missing", $"Quest '{command.QuestId}' does not exist.");

        var updatedQuest = quest.WithOperations(command.Operations);
        var updatedProject = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module,
            state.Project.Blueprints, state.Project.Areas, state.Project.Dialogues,
            state.Project.Quests.Select(item => item.Id == quest.Id ? updatedQuest : item).ToArray());
        var diagnostics = ProjectValidator.Validate(updatedProject);
        if (diagnostics.Count > 0)
            return Failure(state, command, diagnostics);

        var path = $"quests/{quest.Id}/operations";
        var patch = new ProjectPatch(command.Operations is null ? "remove" : quest.Operations is null ? "add" : "replace",
            path, $"Set advanced quest policies for '{quest.Slug}'.");
        var inverse = new ProjectPatch(quest.Operations is null ? "remove" : "replace", path,
            $"Restore advanced quest policies for '{quest.Slug}'.");
        if (command.DryRun)
            return new(true, state, state.Revision, null, [], patch, inverse, Audit(command, false));
        var revision = state.Revision.Next();
        return new(true, new ProjectState(updatedProject, revision), state.Revision, revision, [], patch, inverse,
            Audit(command, true));
    }

    private CommandResult Failure(ProjectState state, SetQuestOperationsCommand command, string code, string message) =>
        Failure(state, command, [new DomainDiagnostic(code, message, $"quests/{command.QuestId}/operations")]);

    private CommandResult Failure(ProjectState state, SetQuestOperationsCommand command,
        IReadOnlyList<DomainDiagnostic> diagnostics) => new(false, state, state.Revision, null,
        diagnostics.Select(item => new CommandDiagnostic(item.Code, item.Message)).ToArray(), null, null,
        Audit(command, false));

    private AuditEntry Audit(SetQuestOperationsCommand command, bool applied) =>
        new(command.CommandId, "set_quest_operations", _clock.UtcNow, applied);
}
