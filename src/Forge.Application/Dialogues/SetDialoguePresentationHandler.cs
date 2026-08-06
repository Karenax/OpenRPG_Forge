using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Dialogues;

public sealed class SetDialoguePresentationHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string MissingDialogueCode = "dialogue.presentation.dialogue_missing";
    private readonly IClock _clock;

    public SetDialoguePresentationHandler(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public CommandResult Handle(ProjectState state, SetDialoguePresentationCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, RevisionConflictCode,
                "The project revision changed before dialogue presentation could be applied.");
        var current = state.Project.Dialogues.SingleOrDefault(item => item.Id == command.DialogueId);
        if (current is null)
            return Failure(state, command, MissingDialogueCode,
                $"Dialogue '{command.DialogueId}' does not exist.");

        var updated = current.WithPresentation(command.Presentation);
        var dialogues = state.Project.Dialogues.Select(item => item.Id == command.DialogueId ? updated : item).ToArray();
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module,
            state.Project.Blueprints, state.Project.Areas, dialogues, state.Project.Quests);
        var diagnostics = ProjectValidator.Validate(project);
        if (diagnostics.Count > 0)
            return new(false, state, command.BaseRevision, null,
                diagnostics.Select(item => new CommandDiagnostic(item.Code, $"{item.Path}: {item.Message}")).ToArray(),
                null, null, Audit(command, false));

        var profile = command.Presentation;
        var summary = profile is null
            ? $"Clear presentation for dialogue '{current.DisplayName}'."
            : $"Set {profile.AnimationCues.Count} animation(s), {profile.VoiceCues.Count} voice cue(s), " +
              $"{profile.CameraShots.Count} shot(s), {profile.Bookmarks.Count} bookmark(s), and " +
              $"{profile.InterjectionRules.Count} interjection(s).";
        var patch = new ProjectPatch("replace", $"dialogues/{command.DialogueId}/presentation", summary);
        var inverse = new ProjectPatch("replace", $"dialogues/{command.DialogueId}/presentation",
            current.Presentation is null ? "Restore empty presentation." : "Restore previous presentation profile.");
        if (command.DryRun)
            return new(true, state, command.BaseRevision, null, [], patch, inverse, Audit(command, false));

        var revision = state.Revision.Next();
        return new(true, new ProjectState(project, revision), command.BaseRevision, revision, [], patch, inverse,
            Audit(command, true));
    }

    private CommandResult Failure(ProjectState state, SetDialoguePresentationCommand command, string code, string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            Audit(command, false));

    private AuditEntry Audit(SetDialoguePresentationCommand command, bool applied) =>
        new(command.CommandId, "set_dialogue_presentation", _clock.UtcNow, applied);
}
