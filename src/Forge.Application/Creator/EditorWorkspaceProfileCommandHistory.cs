using Forge.Domain.Authoring;
using Forge.Domain.Validation;

namespace Forge.Application.Creator;

public sealed record UpdateEditorWorkspaceProfileCommand(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    EditorWorkspaceProfile Candidate,
    bool DryRun = false);

public sealed record EditorWorkspaceProfileCommandResult(
    bool Succeeded,
    bool Applied,
    EditorWorkspaceProfile Profile,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public sealed class EditorWorkspaceProfileCommandHistory
{
    public const int CurrentSchemaVersion = 1;
    public const string InvalidIdCode = "editor.workspace.command.id_invalid";
    public const string UnsupportedSchemaCode = "editor.workspace.command.schema_unsupported";
    public const string MissingProvenanceCode = "editor.workspace.command.provenance_missing";
    public const string ProfileIdentityCode = "editor.workspace.command.profile_identity_mismatch";

    private readonly Stack<EditorWorkspaceProfile> _undo = new();
    private readonly Stack<EditorWorkspaceProfile> _redo = new();

    public EditorWorkspaceProfile Current { get; private set; }

    public EditorWorkspaceProfileCommandHistory(EditorWorkspaceProfile initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        Current = initial;
    }

    public EditorWorkspaceProfileCommandResult Execute(UpdateEditorWorkspaceProfileCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var root = $"editorWorkspace/commands/{command.Id:D}";
        if (command.Id == Guid.Empty)
            return Failure(InvalidIdCode, "Workspace command UUID must be non-empty.", $"{root}/id");
        if (command.SchemaVersion != CurrentSchemaVersion)
            return Failure(UnsupportedSchemaCode, "Workspace command schema is unsupported.", $"{root}/schemaVersion");
        if (string.IsNullOrWhiteSpace(command.Provenance))
            return Failure(MissingProvenanceCode, "Workspace modification provenance is required.", $"{root}/provenance");
        if (command.Candidate.Id != Current.Id)
            return Failure(ProfileIdentityCode, "Workspace profile identity cannot change during an update.", "editorWorkspace/id");
        if (command.DryRun)
            return new EditorWorkspaceProfileCommandResult(true, false, command.Candidate, []);

        _undo.Push(Current);
        Current = command.Candidate;
        _redo.Clear();
        return new EditorWorkspaceProfileCommandResult(true, true, Current, []);
    }

    public EditorWorkspaceProfileCommandResult Undo()
    {
        if (!_undo.TryPop(out var previous))
            return new EditorWorkspaceProfileCommandResult(false, false, Current, []);
        _redo.Push(Current);
        Current = previous;
        return new EditorWorkspaceProfileCommandResult(true, true, Current, []);
    }

    public EditorWorkspaceProfileCommandResult Redo()
    {
        if (!_redo.TryPop(out var next))
            return new EditorWorkspaceProfileCommandResult(false, false, Current, []);
        _undo.Push(Current);
        Current = next;
        return new EditorWorkspaceProfileCommandResult(true, true, Current, []);
    }

    private EditorWorkspaceProfileCommandResult Failure(string code, string message, string path) =>
        new(false, false, Current, [new DomainDiagnostic(code, message, path)]);
}
