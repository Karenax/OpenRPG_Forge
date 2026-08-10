using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Build;

namespace Forge.Application.Build;

public sealed class SetWorkspaceBuildProfileHandler
{
    public const string RevisionConflictCode = "build.profile.revision_conflict";
    private readonly IClock _clock;

    public SetWorkspaceBuildProfileHandler(IClock clock) =>
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public WorkspaceBuildProfileCommandResult Handle(
        WorkspaceBuildProfileState state,
        SetWorkspaceBuildProfileCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, RevisionConflictCode, "Build profile changed before this command was applied.");
        var diagnostics = WorkspaceBuildProfileValidator.Validate(command.Profile)
            .Select(item => new CommandDiagnostic(item.Code, $"{item.Path}: {item.Message}"))
            .ToArray();
        if (diagnostics.Length > 0)
            return new(false, state, command.BaseRevision, null, diagnostics, null, null, Audit(command, false));

        var patch = new ProjectPatch("replace", "build/profile",
            $"Configure {command.Profile.Layout.Count} workspace paths and {command.Profile.CliOperations.Count} CLI operations.");
        var inverse = new ProjectPatch("replace", "build/profile", "Restore the previous workspace build profile.");
        if (command.DryRun)
            return new(true, state, command.BaseRevision, null, [], patch, inverse, Audit(command, false));
        var revision = state.Revision.Next();
        return new(true, new WorkspaceBuildProfileState(command.Profile, revision), command.BaseRevision,
            revision, [], patch, inverse, Audit(command, true));
    }

    private WorkspaceBuildProfileCommandResult Failure(WorkspaceBuildProfileState state,
        SetWorkspaceBuildProfileCommand command, string code, string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            Audit(command, false));

    private AuditEntry Audit(SetWorkspaceBuildProfileCommand command, bool applied) =>
        new(command.CommandId, "set_workspace_build_profile", _clock.UtcNow, applied);
}
