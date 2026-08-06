using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Authoring;

namespace Forge.Application.Creator;

public sealed class SetEditorKeymapHandler
{
    public const string RevisionConflictCode = "authoring.keymap.revision_conflict";
    private readonly IClock _clock;

    public SetEditorKeymapHandler(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public EditorKeymapCommandResult Handle(EditorKeymapState state, SetEditorKeymapCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, RevisionConflictCode, "Editor keymap changed concurrently.");

        var diagnostics = EditorKeymapProfileValidator.Validate(command.Profile)
            .Select(item => new CommandDiagnostic(item.Code, $"{item.Path}: {item.Message}"))
            .ToArray();
        if (diagnostics.Length > 0)
            return new(false, state, command.BaseRevision, null, diagnostics, null, null, Audit(command, false));

        var changed = DescribeChanges(state.Profile, command.Profile);
        var patch = new ProjectPatch("replace", ".forge/editor-keymap", changed);
        var inverse = new ProjectPatch("replace", ".forge/editor-keymap",
            $"Restore {state.Profile.Preset} keymap with {state.Profile.Bindings.Count} bindings.");
        if (command.DryRun)
            return new(true, state, command.BaseRevision, null, [], patch, inverse, Audit(command, false));

        var revision = state.Revision.Next();
        return new(true, new EditorKeymapState(command.Profile, revision), command.BaseRevision, revision,
            [], patch, inverse, Audit(command, true));
    }

    private EditorKeymapCommandResult Failure(
        EditorKeymapState state,
        SetEditorKeymapCommand command,
        string code,
        string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)],
            null, null, Audit(command, false));

    private AuditEntry Audit(SetEditorKeymapCommand command, bool applied) =>
        new(command.CommandId, "set_editor_keymap", _clock.UtcNow, applied);

    private static string DescribeChanges(EditorKeymapProfile before, EditorKeymapProfile after)
    {
        var oldById = before.Bindings.ToDictionary(binding => binding.CommandId, StringComparer.Ordinal);
        var changed = after.Bindings.Count(binding =>
            !oldById.TryGetValue(binding.CommandId, out var previous) || previous.Gesture != binding.Gesture);
        return $"Use {after.Preset} keymap and update {changed} of {after.Bindings.Count} searchable command bindings.";
    }
}
