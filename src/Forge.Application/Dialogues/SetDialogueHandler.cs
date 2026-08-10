using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Dialogues;

public sealed class SetDialogueHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";

    private const string CommandName = "set_dialogue";
    private readonly IClock _clock;

    public SetDialogueHandler(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public CommandResult Handle(ProjectState state, SetDialogueCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Dialogue);
        if (command.BaseRevision != state.Revision)
        {
            return Failure(
                state,
                command,
                [new CommandDiagnostic(
                    RevisionConflictCode,
                    $"Command revision {command.BaseRevision} does not match project revision {state.Revision}.")]);
        }

        var previous = state.Project.Dialogues.SingleOrDefault(dialogue => dialogue.Id == command.Dialogue.Id);
        var dialogues = previous is null
            ? [.. state.Project.Dialogues, command.Dialogue]
            : state.Project.Dialogues
                .Select(dialogue => dialogue.Id == command.Dialogue.Id ? command.Dialogue : dialogue)
                .ToArray();
        var updatedProject = new ProjectDefinition(
            state.Project.Id,
            state.Project.Title,
            state.Project.Module,
            state.Project.Blueprints,
            state.Project.Areas,
            dialogues,
            state.Project.Quests);
        var diagnostics = ProjectValidator.Validate(updatedProject);
        if (diagnostics.Count > 0)
        {
            return Failure(
                state,
                command,
                diagnostics.Select(diagnostic => new CommandDiagnostic(diagnostic.Code, diagnostic.Message)).ToArray());
        }

        var operation = previous is null ? "add" : "replace";
        var path = $"dialogues/{command.Dialogue.Id}";
        var patch = new ProjectPatch(operation, path, DescribeChange(previous, command.Dialogue));
        var inverse = previous is null
            ? new ProjectPatch("remove", path, $"Remove dialogue '{command.Dialogue.DisplayName}'.")
            : new ProjectPatch("replace", path, $"Restore dialogue '{previous.DisplayName}'.");
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

        var newRevision = state.Revision.Next();
        return new CommandResult(
            true,
            new ProjectState(updatedProject, newRevision),
            command.BaseRevision,
            newRevision,
            [],
            patch,
            inverse,
            CreateAudit(command, true));
    }

    private CommandResult Failure(
        ProjectState state,
        SetDialogueCommand command,
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

    private AuditEntry CreateAudit(SetDialogueCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);

    private static string DescribeChange(DialogueDefinition? previous, DialogueDefinition current)
    {
        if (previous is null) return $"Store dialogue '{current.DisplayName}'.";
        var reorderedNodes = current.Nodes
            .Select(node => (Current: node, Previous: previous.Nodes.SingleOrDefault(item => item.Id == node.Id)))
            .Where(item => item.Previous is not null)
            .Where(item => item.Current.Choices.Select(choice => choice.Id).ToHashSet()
                .SetEquals(item.Previous!.Choices.Select(choice => choice.Id)))
            .Where(item => !item.Current.Choices.Select(choice => choice.Id)
                .SequenceEqual(item.Previous!.Choices.Select(choice => choice.Id)))
            .Select(item => item.Current.Id.ToString())
            .ToArray();
        return reorderedNodes.Length == 0
            ? $"Store dialogue '{current.DisplayName}'."
            : $"Reorder dialogue '{current.DisplayName}' choices on nodes {string.Join(", ", reorderedNodes)}.";
    }
}
