using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Validation;

namespace Forge.Application.Projects;

public sealed record RestoreRecoverySnapshotCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    ProjectDefinition Snapshot,
    RecoveryRestoreSelection Selection,
    bool DryRun);

public static class RecoveryRestorePlanner
{
    public const string ProjectMismatchCode = "recovery.snapshot.project_mismatch";

    public static ProjectDefinition Merge(ProjectDefinition current, ProjectDefinition snapshot,
        RecoveryRestoreSelection selection)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(selection);
        if (current.Id != snapshot.Id)
            throw new InvalidOperationException(ProjectMismatchCode);

        var sections = selection.Sections;
        return new ProjectDefinition(
            current.Id,
            sections.HasFlag(RecoveryRestoreSection.ProjectAndModule) ? snapshot.Title : current.Title,
            sections.HasFlag(RecoveryRestoreSection.ProjectAndModule) ? snapshot.Module : current.Module,
            sections.HasFlag(RecoveryRestoreSection.Blueprints) ? snapshot.Blueprints : current.Blueprints,
            sections.HasFlag(RecoveryRestoreSection.Areas) ? snapshot.Areas : current.Areas,
            sections.HasFlag(RecoveryRestoreSection.Dialogues) ? snapshot.Dialogues : current.Dialogues,
            sections.HasFlag(RecoveryRestoreSection.Quests) ? snapshot.Quests : current.Quests);
    }
}

public sealed class RestoreRecoverySnapshotHandler
{
    public const string SelectionInvalidCode = "recovery.restore.selection_invalid";
    private readonly IClock _clock;

    public RestoreRecoverySnapshotHandler(IClock clock) =>
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, RestoreRecoverySnapshotCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Fail(state, command, "command.revision_conflict", "Project revision changed.");

        ProjectDefinition candidate;
        try
        {
            candidate = RecoveryRestorePlanner.Merge(state.Project, command.Snapshot, command.Selection);
        }
        catch (InvalidOperationException exception) when (exception.Message == RecoveryRestorePlanner.ProjectMismatchCode)
        {
            return Fail(state, command, RecoveryRestorePlanner.ProjectMismatchCode,
                "Recovery snapshot belongs to a different project UUID.");
        }
        catch (ArgumentException exception)
        {
            return Fail(state, command, SelectionInvalidCode, exception.Message);
        }

        var diagnostic = ProjectValidator.Validate(candidate).FirstOrDefault();
        if (diagnostic is not null)
            return Fail(state, command, diagnostic.Code, diagnostic.Message);

        var selected = string.Join(',', Enum.GetValues<RecoveryRestoreSection>()
            .Where(section => section is not RecoveryRestoreSection.None and not RecoveryRestoreSection.All &&
                              command.Selection.Sections.HasFlag(section)));
        var patch = new ProjectPatch("restore", "project/recovery",
            $"Restore selected recovery sections: {selected}.");
        var inverse = new ProjectPatch("restore", "project/recovery",
            "Restore the canonical project state from before recovery.");
        var audit = new AuditEntry(command.CommandId, "restore_recovery_snapshot", _clock.UtcNow, !command.DryRun);
        if (command.DryRun)
            return new CommandResult(true, state, command.BaseRevision, null, [], patch, inverse,
                audit with { Applied = false });

        var revision = state.Revision.Next();
        return new CommandResult(true, new ProjectState(candidate, revision), command.BaseRevision, revision, [],
            patch, inverse, audit);
    }

    private CommandResult Fail(ProjectState state, RestoreRecoverySnapshotCommand command, string code,
        string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            new AuditEntry(command.CommandId, "restore_recovery_snapshot", _clock.UtcNow, false));
}
