using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Areas;

public sealed class EditTerrainBrushHandler
{
    private readonly IClock _clock;

    public EditTerrainBrushHandler(IClock clock) => _clock = clock;

    public CommandResult Handle(ProjectState state, EditTerrainBrushCommand command)
    {
        if (command.BaseRevision != state.Revision)
        {
            return Fail(state, command, "command.revision_conflict", "Project revision changed.");
        }

        var area = state.Project.Areas.SingleOrDefault(item => item.Id == command.AreaId);
        if (area is null)
        {
            return Fail(state, command, "area.terrain.area_missing", "Area does not exist.");
        }

        TerrainBrushEditResult edit;
        try
        {
            edit = TerrainBrushEditor.Apply(area.World.Terrain, command.Stroke, command.CommandId.Value);
        }
        catch (ArgumentException exception)
        {
            return Fail(state, command, "area.terrain.brush_invalid", exception.Message);
        }

        if (edit.ChangedSamples == 0 && edit.ChangedHoleCells == 0)
        {
            return Fail(state, command, "area.terrain.brush_no_change", "Terrain brush does not touch an editable sample or cell.");
        }

        var world = new AreaWorldProfile(area.World.Id, area.World.SchemaVersion, edit.Terrain, area.World.Biomes,
            area.World.Features, area.World.VolumetricNavigation, area.World.FlightRoutes, area.World.Loading,
            "terrain-brush");
        var diagnostics = AreaWorldValidator.Validate(area, world);
        if (diagnostics.Count > 0)
        {
            return new CommandResult(false, state, command.BaseRevision, null,
                diagnostics.Select(item => new CommandDiagnostic(item.Code, item.Message)).ToArray(), null, null,
                new AuditEntry(command.CommandId, "edit_terrain_brush", _clock.UtcNow, false));
        }

        var description = $"{command.Stroke.Operation} terrain brush changed {edit.ChangedSamples} samples and {edit.ChangedHoleCells} hole cells.";
        var patch = new ProjectPatch("replace", $"areas/{area.Id}/world/terrain", description);
        var inverse = new ProjectPatch("replace", $"areas/{area.Id}/world/terrain", "Restore previous canonical terrain.");
        var audit = new AuditEntry(command.CommandId, "edit_terrain_brush", _clock.UtcNow, !command.DryRun);
        if (command.DryRun)
        {
            return new CommandResult(true, state, command.BaseRevision, null, [], patch, inverse, audit);
        }

        var updated = area.WithWorld(world);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module,
            state.Project.Blueprints, state.Project.Areas.Select(item => item.Id == area.Id ? updated : item).ToArray(),
            state.Project.Dialogues, state.Project.Quests);
        var revision = state.Revision.Next();
        return new CommandResult(true, new ProjectState(project, revision), command.BaseRevision, revision, [], patch,
            inverse, audit);
    }

    private CommandResult Fail(ProjectState state, EditTerrainBrushCommand command, string code, string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            new AuditEntry(command.CommandId, "edit_terrain_brush", _clock.UtcNow, false));
}
