using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Projects;

public sealed class NewModuleHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string TitleRequiredCode = "module.title_required";
    public const string AreaNameRequiredCode = "module.area_name_required";

    private const string CommandName = "new_module";
    private readonly IClock _clock;

    public NewModuleHandler(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public NewModuleResult Handle(NewModuleCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != new ProjectRevision(0))
        {
            return Failure(
                command,
                RevisionConflictCode,
                "A new module must start at project revision 0.");
        }

        if (string.IsNullOrWhiteSpace(command.Title))
        {
            return Failure(command, TitleRequiredCode, "A module title is required.");
        }

        if (string.IsNullOrWhiteSpace(command.AreaDisplayName))
        {
            return Failure(command, AreaNameRequiredCode, "An initial area name is required.");
        }

        var moduleSlug = SlugGenerator.FromDisplayName(command.Title, "module");
        var areaSlug = SlugGenerator.FromDisplayName(command.AreaDisplayName, "area");
        var floorBlueprint = new BlueprintDefinition(
            command.FloorBlueprintId,
            $"{moduleSlug}-floor",
            "Sol provisoire",
            BlueprintKind.Placeable);
        var playerSpawnBlueprint = new BlueprintDefinition(
            command.PlayerSpawnBlueprintId,
            "player-spawn",
            "Point de départ",
            BlueprintKind.PlayerSpawn);
        var floor = new EntityInstance(
            command.FloorEntityId,
            floorBlueprint.Id,
            new TransformValue(
                new Vector3Value(0, -0.5f, 0),
                QuaternionValue.Identity,
                new Vector3Value(16, 1, 16)),
            ["floor", "solid"]);
        var playerSpawn = new EntityInstance(
            command.PlayerSpawnEntityId,
            playerSpawnBlueprint.Id,
            new TransformValue(
                new Vector3Value(0, 0.8f, 0),
                QuaternionValue.Identity,
                Vector3Value.One),
            ["player"]);
        var area = new AreaDefinition(
            command.AreaId,
            areaSlug,
            command.AreaDisplayName.Trim(),
            [floor, playerSpawn],
            command.AreaKind,
            command.Dimensions);
        var module = new ModuleDefinition(
            command.ModuleId,
            moduleSlug,
            command.Title.Trim(),
            command.ModuleDescription.Trim(),
            area.Id);
        var project = new ProjectDefinition(
            command.ProjectId,
            command.Title.Trim(),
            module,
            [floorBlueprint, playerSpawnBlueprint],
            [area]);
        var state = new ProjectState(project, new ProjectRevision(0));
        var patch = new ProjectPatch("add", $"modules/{module.Id}", $"Create module '{module.DisplayName}'.");
        var inverse = new ProjectPatch("remove", $"modules/{module.Id}", $"Remove module '{module.DisplayName}'.");

        return new NewModuleResult(
            true,
            state,
            command.BaseRevision,
            [],
            patch,
            inverse,
            CreateAudit(command, !command.DryRun));
    }

    private NewModuleResult Failure(
        NewModuleCommand command,
        string code,
        string message) =>
        new(
            false,
            null,
            command.BaseRevision,
            [new CommandDiagnostic(code, message)],
            null,
            null,
            CreateAudit(command, applied: false));

    private AuditEntry CreateAudit(NewModuleCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);
}
