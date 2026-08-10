using Forge.Application.Projects;
using Forge.Domain.Projects;

namespace Forge.Application.Commands;

public sealed class ProjectCommandHistory
{
    private readonly Stack<HistoryEntry> _undoEntries = [];
    private readonly Stack<HistoryEntry> _redoEntries = [];
    private readonly List<CommandHistoryJournalEntry> _journal = [];

    public ProjectState State { get; private set; }

    public bool CanUndo => _undoEntries.Count > 0;

    public bool CanRedo => _redoEntries.Count > 0;

    public IReadOnlyList<CommandHistoryJournalEntry> Journal => _journal;

    public ProjectCommandHistory(ProjectState initialState)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        State = initialState;
    }

    public void Record(CommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Succeeded || !result.Audit.Applied || result.NewRevision is null ||
            result.Patch is null || result.Inverse is null)
        {
            throw new InvalidOperationException("Only a successfully applied command can be recorded.");
        }

        if (result.BaseRevision != State.Revision)
        {
            throw new InvalidOperationException(
                $"Command history revision {State.Revision} does not match result base revision {result.BaseRevision}.");
        }

        var journal = new CommandHistoryJournalEntry(
            result.Audit.CommandId.Value,
            CommandHistoryJournalEntry.CurrentSchemaVersion,
            "project-command-history",
            result.Audit.CommandName,
            result.Audit.Timestamp,
            result.BaseRevision.Value,
            result.NewRevision.Value.Value,
            result.Patch,
            result.Inverse,
            true);
        _journal.Add(journal);
        _undoEntries.Push(new HistoryEntry(
            State.Project,
            result.State.Project,
            result.Patch,
            result.Inverse,
            journal.Id));
        _redoEntries.Clear();
        State = result.State;
    }

    public CommandHistoryResult Undo()
    {
        if (!_undoEntries.TryPop(out var entry))
        {
            return new CommandHistoryResult(false, State, null);
        }

        _redoEntries.Push(entry);
        State = new ProjectState(entry.Before, State.Revision.Next());
        SetJournalApplied(entry.JournalId, false);
        return new CommandHistoryResult(true, State, entry.Inverse);
    }

    public CommandHistoryResult Redo()
    {
        if (!_redoEntries.TryPop(out var entry))
        {
            return new CommandHistoryResult(false, State, null);
        }

        _undoEntries.Push(entry);
        State = new ProjectState(entry.After, State.Revision.Next());
        SetJournalApplied(entry.JournalId, true);
        return new CommandHistoryResult(true, State, entry.Patch);
    }

    private sealed record HistoryEntry(
        ProjectDefinition Before,
        ProjectDefinition After,
        ProjectPatch Patch,
        ProjectPatch Inverse,
        Guid JournalId);

    private void SetJournalApplied(Guid id, bool applied)
    {
        var index = _journal.FindIndex(entry => entry.Id == id);
        if (index >= 0) _journal[index] = _journal[index] with { IsApplied = applied };
    }
}
