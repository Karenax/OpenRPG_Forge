using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.Validation;

namespace Forge.Application.Projects;

public sealed class DeleteAuthoringElementHandler(IClock clock)
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string NotFoundCode = "authoring.delete.not_found";
    public const string LastAreaCode = "authoring.delete.last_area";
    public const string EntryAreaCode = "authoring.delete.entry_area";
    public const string ReadOnlyCode = "authoring.delete.read_only";

    public CommandResult Handle(ProjectState state, DeleteAuthoringElementCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, RevisionConflictCode,
                $"Command revision {command.BaseRevision} does not match project revision {state.Revision}.");

        var project = state.Project;
        var label = ResolveLabel(project, command);
        if (label is null) return Failure(state, command, NotFoundCode,
            $"{command.Kind} '{command.ElementId:D}' does not exist.");
        if (command.Kind == AuthoringElementKind.Area && project.Areas.Count <= 1)
            return Failure(state, command, LastAreaCode, "A project must retain at least one area.");
        if (command.Kind == AuthoringElementKind.Area && project.Module.EntryAreaId.Value == command.ElementId)
            return Failure(state, command, EntryAreaCode, "The module entry area cannot be deleted.");
        if (command.Kind == AuthoringElementKind.Blueprint && project.Blueprints
                .Single(item => item.Id.Value == command.ElementId).IsReadOnly())
            return Failure(state, command, ReadOnlyCode, $"Installed blueprint '{label}' is read-only.");

        var updated = new ProjectDefinition(project.Id, project.Title, project.Module,
            command.Kind == AuthoringElementKind.Blueprint
                ? project.Blueprints.Where(item => item.Id.Value != command.ElementId).ToArray()
                : project.Blueprints,
            command.Kind == AuthoringElementKind.Area
                ? project.Areas.Where(item => item.Id.Value != command.ElementId).ToArray()
                : project.Areas,
            command.Kind == AuthoringElementKind.Dialogue
                ? project.Dialogues.Where(item => item.Id.Value != command.ElementId).ToArray()
                : project.Dialogues,
            command.Kind == AuthoringElementKind.Quest
                ? project.Quests.Where(item => item.Id.Value != command.ElementId).ToArray()
                : project.Quests);
        var diagnostics = ProjectValidator.Validate(updated);
        if (diagnostics.Count > 0)
            return Failure(state, command, diagnostics.Select(item =>
                new CommandDiagnostic(item.Code, item.Message)).ToArray());

        var path = $"{command.Kind.ToString().ToLowerInvariant()}s/{command.ElementId:D}";
        var patch = new ProjectPatch("remove", path, $"Remove {command.Kind} '{label}'.");
        var inverse = new ProjectPatch("add", path, $"Restore {command.Kind} '{label}'.");
        if (command.DryRun)
            return new(true, state, command.BaseRevision, null, [], patch, inverse, Audit(command, false));
        var revision = state.Revision.Next();
        return new(true, new ProjectState(updated, revision), command.BaseRevision, revision, [], patch, inverse,
            Audit(command, true));
    }

    private static string? ResolveLabel(ProjectDefinition project, DeleteAuthoringElementCommand command) =>
        command.Kind switch
        {
            AuthoringElementKind.Area => project.Areas
                .SingleOrDefault(item => item.Id.Value == command.ElementId)?.DisplayName,
            AuthoringElementKind.Blueprint => project.Blueprints
                .SingleOrDefault(item => item.Id.Value == command.ElementId)?.DisplayName,
            AuthoringElementKind.Dialogue => project.Dialogues
                .SingleOrDefault(item => item.Id.Value == command.ElementId)?.DisplayName,
            AuthoringElementKind.Quest => project.Quests
                .SingleOrDefault(item => item.Id.Value == command.ElementId)?.DisplayName,
            _ => null,
        };

    private CommandResult Failure(ProjectState state, DeleteAuthoringElementCommand command, string code,
        string message) => Failure(state, command, [new CommandDiagnostic(code, message)]);

    private CommandResult Failure(ProjectState state, DeleteAuthoringElementCommand command,
        IReadOnlyList<CommandDiagnostic> diagnostics) => new(false, state, command.BaseRevision, null, diagnostics,
        null, null, Audit(command, false));

    private AuditEntry Audit(DeleteAuthoringElementCommand command, bool applied) =>
        new(command.CommandId, "delete_authoring_element", clock.UtcNow, applied);
}
