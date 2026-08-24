using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

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

        var createInitialArea = command.CreateInitialArea ?? command.Template == ProjectTemplateKind.Rpg;
        if (createInitialArea && string.IsNullOrWhiteSpace(command.AreaDisplayName))
        {
            return Failure(command, AreaNameRequiredCode, "An initial area name is required.");
        }

        var moduleSlug = SlugGenerator.FromDisplayName(command.Title, "module");
        var areaSlug = createInitialArea
            ? SlugGenerator.FromDisplayName(command.AreaDisplayName, "area")
            : "area";
        var areaDisplayName = createInitialArea ? command.AreaDisplayName.Trim() : "Zone optionnelle";
        if (command.Template == ProjectTemplateKind.Rpg && !createInitialArea)
            return Failure(command, ProjectFoundationValidator.InitialAreaCode,
                "The RPG template requires an initial area. Path: module/entryAreaId.");
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
            areaDisplayName,
            [floor, playerSpawn],
            command.AreaKind,
            command.Dimensions);
        var foundationId = StableGuid(command.ProjectId.Value, "project-foundation");
        var packageNamespace = string.IsNullOrWhiteSpace(command.PackageNamespace)
            ? $"module.{moduleSlug}"
            : command.PackageNamespace.Trim();
        var foundation = createInitialArea
            ? ProjectFoundationProfile.CreateRpgDefault(
                foundationId, command.ProjectId, moduleSlug, command.AreaId, command.PlayerSpawnEntityId,
                packageNamespace) with
            {
                Template = command.Template,
                Kits = (command.Kits ?? DefaultKits(command.Template)).ToArray(),
                TargetPlatforms = (command.TargetPlatforms ?? [ModuleDistributionPlatform.WindowsX64]).ToArray(),
                RulesProvider = command.RulesProvider,
                RenderPreset = command.RenderPreset,
                NetworkMode = command.NetworkMode,
                RequiresInitialArea = command.Template == ProjectTemplateKind.Rpg,
                PlayerStarts =
                [ProjectFoundationProfile.CreateRpgDefault(
                        foundationId, command.ProjectId, moduleSlug, command.AreaId,
                        command.PlayerSpawnEntityId, packageNamespace).PlayerStarts[0] with
                    {
                        OrientationDegrees = command.PlayerOrientationDegrees,
                        GameMode = command.GameMode,
                        EntryClass = command.EntryClass,
                        PlayerSelectionRule = command.PlayerSelectionRule,
                    }],
            }
            : ProjectFoundationProfile.CreateCoreDefault(
                foundationId, command.ProjectId, moduleSlug, command.Template, packageNamespace) with
            {
                Kits = (command.Kits ?? DefaultKits(command.Template)).ToArray(),
                TargetPlatforms = (command.TargetPlatforms ?? [ModuleDistributionPlatform.WindowsX64]).ToArray(),
                RulesProvider = command.RulesProvider,
                RenderPreset = command.RenderPreset,
                NetworkMode = command.NetworkMode,
            };
        var localizedMetadata = command.Metadata?.Localizations ??
        [new ProjectLocalizedMetadata("fr", command.Title.Trim(), command.ModuleDescription.Trim(),
            command.ModuleDescription.Trim(), string.Empty)];
        var metadata = new ProjectMetadata(
            ProjectMetadataId.From(command.ModuleId.Value),
            command.Metadata?.DefaultLocale ?? "fr",
            localizedMetadata,
            command.Metadata?.Author ?? "Non renseigné",
            command.Metadata?.Version ?? "0.1.0",
            command.Metadata?.License ?? "UNLICENSED",
            command.Metadata?.ImagePath,
            command.Metadata?.Tags ?? [],
            command.Metadata?.Classification ?? "Non classé",
            "openrpg-forge:project-wizard",
            foundation: foundation);
        var module = new ModuleDefinition(
            command.ModuleId,
            moduleSlug,
            command.Title.Trim(),
            command.ModuleDescription.Trim(),
            area.Id,
            metadata);
        var project = new ProjectDefinition(
            command.ProjectId,
            command.Title.Trim(),
            module,
            createInitialArea ? [floorBlueprint, playerSpawnBlueprint] : [],
            createInitialArea ? [area] : []);
        var diagnostics = ProjectValidator.Validate(project);
        if (diagnostics.Count > 0)
            return Failure(command, diagnostics[0].Code,
                $"{diagnostics[0].Message} Path: {diagnostics[0].Path}.");
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

    private static IReadOnlyList<string> DefaultKits(ProjectTemplateKind template) =>
        template == ProjectTemplateKind.Rpg ? ["forge-core", "forge-rpg-kit"] : ["forge-core"];

    private static Guid StableGuid(Guid value, string discriminator)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"openrpg-forge:{value:D}:{discriminator}"));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, guid.Length).CopyTo(guid);
        guid[6] = (byte)((guid[6] & 0x0f) | 0x40);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }
}
