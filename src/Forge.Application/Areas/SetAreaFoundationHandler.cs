using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Projects;

namespace Forge.Application.Areas;

public sealed class SetAreaFoundationHandler(IClock clock)
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string AreaMissingCode = "area.foundation.area_missing";
    public const string BlueprintMissingCode = "area.foundation.blueprint_missing";

    public CommandResult Handle(ProjectState state, SetAreaFoundationCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
        {
            return Failure(state, command, RevisionConflictCode, "Project revision changed.");
        }

        var area = state.Project.Areas.SingleOrDefault(item => item.Id == command.AreaId);
        if (area is null)
        {
            return Failure(state, command, AreaMissingCode, $"Area '{command.AreaId}' does not exist.");
        }

        var diagnostics = AreaFoundationValidator.Validate(area, command.Foundation)
            .Select(item => new CommandDiagnostic(item.Code, $"{item.Path}: {item.Message}"))
            .ToList();
        var blueprints = state.Project.Blueprints.Select(item => item.Id).ToHashSet();
        foreach (var definition in command.Foundation.TileKit.Definitions)
        {
            if (!blueprints.Contains(definition.BlueprintId) ||
                definition.Variants.Any(item => !blueprints.Contains(item.BlueprintId)))
            {
                diagnostics.Add(new(BlueprintMissingCode,
                    $"areas/{area.Id}/foundation/tileKit/definitions/{definition.Id:D}: blueprint reference is missing."));
            }
        }
        var targets = state.Project.Blueprints.Select(item => item.Id.Value)
            .Concat(state.Project.Areas.Select(item => item.Id.Value)).ToHashSet();
        foreach (var dependency in command.Foundation.Dependencies.Where(item => !targets.Contains(item.TargetId)))
        {
            diagnostics.Add(new(AreaFoundationValidator.ReferenceCode,
                $"areas/{area.Id}/foundation/dependencies/{dependency.Id:D}: target UUID is missing."));
        }

        if (diagnostics.Count > 0)
        {
            return new(false, state, command.BaseRevision, null, diagnostics, null, null,
                new AuditEntry(command.CommandId, "set_area_foundation", clock.UtcNow, false));
        }

        var patch = new ProjectPatch("replace", $"areas/{area.Id}/foundation",
            $"Set area foundation '{command.Foundation.Creation.KitKey}'.");
        var inverse = new ProjectPatch("replace", $"areas/{area.Id}/foundation",
            $"Restore area foundation '{area.Foundation.Creation.KitKey}'.");
        if (command.DryRun)
        {
            return new(true, state, command.BaseRevision, null, [], patch, inverse,
                new AuditEntry(command.CommandId, "set_area_foundation", clock.UtcNow, false));
        }

        var updatedArea = area.WithFoundation(command.Foundation);
        var updatedProject = new ProjectDefinition(
            state.Project.Id,
            state.Project.Title,
            state.Project.Module,
            state.Project.Blueprints,
            state.Project.Areas.Select(item => item.Id == area.Id ? updatedArea : item).ToArray(),
            state.Project.Dialogues,
            state.Project.Quests);
        var revision = state.Revision.Next();
        return new(true, new ProjectState(updatedProject, revision), command.BaseRevision, revision, [], patch, inverse,
            new AuditEntry(command.CommandId, "set_area_foundation", clock.UtcNow, true));
    }

    private CommandResult Failure(ProjectState state, SetAreaFoundationCommand command, string code, string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            new AuditEntry(command.CommandId, "set_area_foundation", clock.UtcNow, false));
}
