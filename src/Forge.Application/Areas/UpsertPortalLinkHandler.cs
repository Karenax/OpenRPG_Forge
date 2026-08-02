using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Areas;

public sealed class UpsertPortalLinkHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string MissingSourceAreaCode = "area.portal.source_area_missing";

    private const string CommandName = "upsert_portal_link";
    private readonly IClock _clock;

    public UpsertPortalLinkHandler(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public CommandResult Handle(ProjectState state, UpsertPortalLinkCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
        {
            return Failure(state, command, RevisionConflictCode,
                $"Command revision {command.BaseRevision} does not match project revision {state.Revision}.");
        }

        var sourceArea = state.Project.Areas.FirstOrDefault(area => area.Id == command.SourceAreaId);
        if (sourceArea is null)
        {
            return Failure(state, command, MissingSourceAreaCode,
                $"Source area '{command.SourceAreaId}' does not exist.");
        }

        var existing = sourceArea.Portals.FirstOrDefault(portal => portal.Id == command.Portal.Id);
        var portals = existing is null
            ? [.. sourceArea.Portals, command.Portal]
            : sourceArea.Portals.Select(portal => portal.Id == command.Portal.Id ? command.Portal : portal).ToArray();
        var updatedArea = sourceArea.WithPortals(portals);
        var updatedProject = new ProjectDefinition(
            state.Project.Id,
            state.Project.Title,
            state.Project.Module,
            state.Project.Blueprints,
            state.Project.Areas.Select(area => area.Id == sourceArea.Id ? updatedArea : area).ToArray(),
            state.Project.Dialogues,
            state.Project.Quests);
        var portalDiagnostics = ProjectValidator.Validate(updatedProject)
            .Where(diagnostic => diagnostic.Code.StartsWith("area.portal.", StringComparison.Ordinal))
            .ToArray();
        if (portalDiagnostics.Length > 0)
        {
            return new CommandResult(
                false,
                state,
                command.BaseRevision,
                null,
                portalDiagnostics.Select(item => new CommandDiagnostic(item.Code, item.Message)).ToArray(),
                null,
                null,
                CreateAudit(command, false));
        }

        var path = $"areas/{sourceArea.Id}/portals/{command.Portal.Id}";
        var patch = new ProjectPatch(
            existing is null ? "add" : "replace",
            path,
            $"{(existing is null ? "Create" : "Update")} portal '{command.Portal.DisplayName}'.");
        var inverse = new ProjectPatch(
            existing is null ? "remove" : "replace",
            path,
            existing is null
                ? $"Remove portal '{command.Portal.DisplayName}'."
                : $"Restore portal '{existing.DisplayName}'.");
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

        var revision = state.Revision.Next();
        return new CommandResult(
            true,
            new ProjectState(updatedProject, revision),
            command.BaseRevision,
            revision,
            [],
            patch,
            inverse,
            CreateAudit(command, true));
    }

    private CommandResult Failure(
        ProjectState state,
        UpsertPortalLinkCommand command,
        string code,
        string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            CreateAudit(command, false));

    private AuditEntry CreateAudit(UpsertPortalLinkCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);
}
