using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;

namespace Forge.Application.Areas;

public sealed class PatchAreaEnvironmentHandler
{
    private readonly IClock _clock;
    private readonly SetAreaEnvironmentHandler _environmentHandler;

    public PatchAreaEnvironmentHandler(IClock clock)
    {
        _clock = clock;
        _environmentHandler = new SetAreaEnvironmentHandler(clock);
    }

    public CommandResult Handle(ProjectState state, PatchAreaEnvironmentCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Fail(state, command, "command.revision_conflict", "Project revision changed.");
        var area = state.Project.Areas.SingleOrDefault(item => item.Id == command.AreaId);
        if (area is null)
            return Fail(state, command, "area.environment.area_missing", "Area does not exist.");
        try
        {
            var patch = command.Patch ?? (command.Candidate is null
                ? throw new ArgumentException("A typed patch or complete Studio candidate is required.")
                : AreaEnvironmentPatch.Between(area.Environment, command.Candidate));
            var environment = AreaEnvironmentPatch.Apply(area.Environment, patch);
            var result = _environmentHandler.Handle(state,
                new SetAreaEnvironmentCommand(command.CommandId, command.BaseRevision, command.AreaId,
                    environment, command.DryRun));
            if (!result.Succeeded) return result;
            var relativePaths = patch.RelativePaths;
            var path = relativePaths.Count == 1
                ? $"areas/{area.Id}/{relativePaths[0]}"
                : $"areas/{area.Id}/environment";
            var fields = patch.Fields.Select(item => item.ToString().ToLowerInvariant()).ToArray();
            return result with
            {
                Patch = new ProjectPatch("patch", path,
                    $"Patch only the selected environment fields: {string.Join(", ", fields)}."),
                Inverse = new ProjectPatch("patch", path, "Restore only the selected environment fields."),
                Audit = new AuditEntry(command.CommandId, "patch_area_environment", _clock.UtcNow,
                    result.Succeeded && !command.DryRun),
            };
        }
        catch (ArgumentException exception)
        {
            return Fail(state, command, "area.environment.patch_invalid", exception.Message);
        }
    }

    private CommandResult Fail(ProjectState state, PatchAreaEnvironmentCommand command, string code,
        string message) => new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)],
        null, null, new AuditEntry(command.CommandId, "patch_area_environment", _clock.UtcNow, false));
}
