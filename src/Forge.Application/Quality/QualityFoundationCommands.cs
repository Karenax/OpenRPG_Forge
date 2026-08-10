using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Quality;

namespace Forge.Application.Quality;

public readonly record struct QualityFoundationRevision(long Value)
{
    public QualityFoundationRevision Next() => new(checked(Value + 1));
}

public sealed record QualityFoundationState(QualityFoundationProfile Profile, QualityFoundationRevision Revision);

public sealed record SetQualityFoundationCommand(
    CommandId CommandId,
    QualityFoundationRevision BaseRevision,
    QualityFoundationProfile Profile,
    bool DryRun);

public sealed record QualityFoundationCommandResult(
    bool Succeeded,
    QualityFoundationState State,
    QualityFoundationRevision? NewRevision,
    IReadOnlyList<CommandDiagnostic> Diagnostics,
    ProjectPatch? Patch,
    ProjectPatch? Inverse,
    AuditEntry Audit);

public sealed class SetQualityFoundationHandler(IClock clock)
{
    public const string RevisionConflictCode = "qa.foundation.revision_conflict";

    public QualityFoundationCommandResult Handle(QualityFoundationState state, SetQualityFoundationCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return new(false, state, null, [new(RevisionConflictCode, "Quality configuration changed before apply.")],
                null, null, Audit(command, false));
        var diagnostics = QualityFoundationProfileValidator.Validate(command.Profile)
            .Select(item => new CommandDiagnostic(item.Code, $"{item.Message} [{item.Path}]")).ToArray();
        if (diagnostics.Length > 0)
            return new(false, state, null, diagnostics, null, null, Audit(command, false));
        var patch = new ProjectPatch("replace", "quality/foundation", "Set QA foundation policy.");
        var inverse = new ProjectPatch("replace", "quality/foundation", "Restore previous QA foundation policy.");
        if (command.DryRun) return new(true, state, null, [], patch, inverse, Audit(command, false));
        var revision = state.Revision.Next();
        return new(true, new(command.Profile, revision), revision, [], patch, inverse, Audit(command, true));
    }

    private AuditEntry Audit(SetQualityFoundationCommand command, bool applied) =>
        new(command.CommandId, "set_quality_foundation", clock.UtcNow, applied);
}

public sealed class QualityFoundationCommandHistory(QualityFoundationProfile profile)
{
    private readonly Stack<(QualityFoundationProfile Before, QualityFoundationProfile After)> _undo = [];
    private readonly Stack<(QualityFoundationProfile Before, QualityFoundationProfile After)> _redo = [];
    public QualityFoundationState State { get; private set; } = new(profile, default);

    public void Record(QualityFoundationCommandResult result)
    {
        if (!result.Succeeded || !result.Audit.Applied || result.NewRevision is null ||
            result.State.Revision != result.NewRevision || result.Patch is null || result.Inverse is null)
            throw new InvalidOperationException("Only an applied QA foundation command can be recorded.");
        _undo.Push((State.Profile, result.State.Profile));
        _redo.Clear();
        State = result.State;
    }

    public bool Undo()
    {
        if (!_undo.TryPop(out var item)) return false;
        _redo.Push(item);
        State = new(item.Before, State.Revision.Next());
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPop(out var item)) return false;
        _undo.Push(item);
        State = new(item.After, State.Revision.Next());
        return true;
    }
}
