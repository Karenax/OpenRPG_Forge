using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Application.Creator;

public sealed class ApplyDiagnosticQuickFixHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string MissingDiagnosticCode = "diagnostic.stale_or_missing";
    public const string MissingQuickFixCode = "diagnostic.quick_fix_missing";
    public const string MissingTargetCode = "diagnostic.quick_fix_target_missing";
    public const string MissingSpawnBlueprintCode = "diagnostic.quick_fix.spawn_blueprint_missing";

    private const string CommandName = "apply_diagnostic_quick_fix";
    private readonly IClock _clock;

    public ApplyDiagnosticQuickFixHandler(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public CommandResult Handle(ProjectState state, ApplyDiagnosticQuickFixCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
        {
            return Failure(state, command, RevisionConflictCode,
                $"Command revision {command.BaseRevision} does not match project revision {state.Revision}.");
        }

        var diagnostic = ProjectDiagnosticCatalog.Build(state.Project)
            .FirstOrDefault(candidate => candidate.Id == command.DiagnosticId);
        if (diagnostic is null)
        {
            return Failure(state, command, MissingDiagnosticCode,
                $"Diagnostic '{command.DiagnosticId}' is stale or no longer exists.");
        }

        var fix = diagnostic.QuickFixes.FirstOrDefault(candidate => candidate.Id == command.QuickFixId);
        if (fix is null)
        {
            return Failure(state, command, MissingQuickFixCode,
                $"Quick fix '{command.QuickFixId}' is not available for diagnostic '{diagnostic.Code}'.");
        }

        if (!TryApply(state.Project, diagnostic, fix, out var updatedProject, out var failure))
        {
            return Failure(state, command, failure!.Value.Code, failure.Value.Message);
        }

        var validation = ProjectValidator.Validate(updatedProject!);
        if (validation.Count > 0)
        {
            return Failure(state, command, validation[0].Code, validation[0].Message);
        }

        var patch = new ProjectPatch(
            "replace",
            diagnostic.DocumentPath,
            $"Quick fix '{fix.Label}' resolves {diagnostic.Code} from {diagnostic.Source}.");
        var inverse = new ProjectPatch(
            "replace",
            diagnostic.DocumentPath,
            $"Restore the project state before quick fix '{fix.Label}'.");
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
            new ProjectState(updatedProject!, revision),
            command.BaseRevision,
            revision,
            [],
            patch,
            inverse,
            CreateAudit(command, true));
    }

    private static bool TryApply(
        ProjectDefinition project,
        ActionableDiagnostic diagnostic,
        DiagnosticQuickFixDefinition fix,
        out ProjectDefinition? updated,
        out (string Code, string Message)? failure)
    {
        updated = null;
        failure = null;
        var area = project.Areas.FirstOrDefault(candidate => candidate.Id == fix.AreaId);
        if (area is null)
        {
            failure = (MissingTargetCode, $"Area '{fix.AreaId}' no longer exists.");
            return false;
        }

        AreaDefinition updatedArea;
        switch (fix.Kind)
        {
            case DiagnosticQuickFixKind.ClampEntityToArea:
                var entity = area.Entities.FirstOrDefault(candidate => candidate.Id == fix.EntityId);
                if (entity is null)
                {
                    failure = (MissingTargetCode, $"Entity '{fix.EntityId}' no longer exists.");
                    return false;
                }

                var halfWidth = area.Dimensions.Width / 2f;
                var halfDepth = area.Dimensions.Depth / 2f;
                var position = new Vector3Value(
                    Math.Clamp(
                        entity.Transform.Position.X,
                        -halfWidth + (entity.Transform.Scale.X / 2f),
                        halfWidth - (entity.Transform.Scale.X / 2f)),
                    entity.Transform.Position.Y,
                    Math.Clamp(
                        entity.Transform.Position.Z,
                        -halfDepth + (entity.Transform.Scale.Z / 2f),
                        halfDepth - (entity.Transform.Scale.Z / 2f)));
                var transformed = new EntityInstance(
                    entity.Id,
                    entity.BlueprintId,
                    new TransformValue(position, entity.Transform.Rotation, entity.Transform.Scale),
                    entity.Tags,
                    entity.Overrides);
                updatedArea = area.WithEntities(area.Entities.Select(candidate => candidate.Id == entity.Id
                    ? transformed
                    : candidate).ToArray());
                break;

            case DiagnosticQuickFixKind.CreatePlayerSpawn:
                var spawnBlueprint = project.Blueprints.FirstOrDefault(blueprint =>
                    blueprint.Kind == BlueprintKind.PlayerSpawn);
                if (spawnBlueprint is null)
                {
                    failure = (MissingSpawnBlueprintCode,
                        "No PlayerSpawn blueprint is available for this quick fix.");
                    return false;
                }

                var spawnId = EntityId.From(StableGuid($"spawn:{diagnostic.Id}:{area.Id}"));
                if (project.Areas.SelectMany(candidate => candidate.Entities).Any(entity => entity.Id == spawnId))
                {
                    failure = (MissingTargetCode, $"Generated spawn entity '{spawnId}' already exists.");
                    return false;
                }

                var spawn = new EntityInstance(
                    spawnId,
                    spawnBlueprint.Id,
                    TransformValue.Identity,
                    ["quick-fix", "spawn"]);
                updatedArea = area.WithEntities([.. area.Entities, spawn]);
                break;

            case DiagnosticQuickFixKind.IncreaseNavigationCellSize:
                var required = MathF.Sqrt(
                    (area.Dimensions.Width * area.Dimensions.Depth) /
                    (float)NavigationSurfaceBuilder.MaximumCellCount);
                var roundedRequired = MathF.Ceiling(required * 4f) / 4f;
                var cellSize = MathF.Min(4f, MathF.Max(area.Navigation.CellSize * 2f, roundedRequired));
                updatedArea = area.WithNavigation(new AreaNavigationDefinition(
                    area.Navigation.Id,
                    cellSize,
                    area.Navigation.AgentProfiles,
                    area.Navigation.OffMeshLinks,
                    "diagnostic-quick-fix"));
                break;

            default:
                failure = (MissingQuickFixCode, $"Quick fix kind '{fix.Kind}' is not supported.");
                return false;
        }

        updated = new ProjectDefinition(
            project.Id,
            project.Title,
            project.Module,
            project.Blueprints,
            project.Areas.Select(candidate => candidate.Id == area.Id ? updatedArea : candidate).ToArray(),
            project.Dialogues,
            project.Quests);
        return true;
    }

    private CommandResult Failure(
        ProjectState state,
        ApplyDiagnosticQuickFixCommand command,
        string code,
        string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            CreateAudit(command, false));

    private AuditEntry CreateAudit(ApplyDiagnosticQuickFixCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);

    private static Guid StableGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{value}"));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guid);
        guid[7] = (byte)((guid[7] & 0x0f) | 0x50);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }
}
