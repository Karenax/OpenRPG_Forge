using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;

namespace Forge.Application.Blueprints;

public sealed class UpdateBlueprintFromEntityHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    private const string CommandName = "update_blueprint_from_entity";
    private readonly IClock _clock;

    public UpdateBlueprintFromEntityHandler(IClock clock) => _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, UpdateBlueprintFromEntityCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (state.Revision != command.BaseRevision)
            return Failure(state, command, RevisionConflictCode,
                $"Command revision {command.BaseRevision} does not match project revision {state.Revision}.");

        BlueprintUpdatePlan plan;
        try
        {
            plan = BlueprintUpdatePlanner.Build(state.Project, command.BlueprintId, command.SourceEntityId,
                command.PromotedOverrideKeys, command.VersionBump, command.ExcludedInstanceIds);
        }
        catch (BlueprintUpdatePlanException exception)
        {
            return Failure(state, command, exception.Code, exception.Message);
        }
        catch (ArgumentException exception)
        {
            return Failure(state, command, "blueprint.update.invalid", exception.Message);
        }

        var summary = $"Promote {plan.PromotedDefaults.Count} defaults, bump {plan.PreviousVersion} to " +
                      $"{plan.TargetVersion}, inspect {plan.Instances.Count} instances (" +
                      $"{plan.Instances.Count(item => item.Action == "excluded")} excluded, " +
                      $"{plan.Instances.Count(item => item.Action == "pinned-incompatible")} pinned).";
        var patch = new ProjectPatch("replace", $"blueprints/{command.BlueprintId}", summary);
        var inverse = new ProjectPatch("replace", $"blueprints/{command.BlueprintId}",
            $"Restore blueprint version {plan.PreviousVersion} and every instance version constraint.");
        if (command.DryRun)
            return new(true, state, command.BaseRevision, null, [], patch, inverse, Audit(command, false));

        var blueprint = state.Project.Blueprints.Single(item => item.Id == command.BlueprintId);
        var old = blueprint.Authoring;
        var authoring = new BlueprintAuthoringMetadata(old.SchemaVersion, plan.TargetVersion, old.PackageNamespace,
            old.ManifestPath, old.ExposedParameters, old.Dependencies, old.PreviewPath, old.PlatformByteBudget, old.Kit,
            old.Styles, old.Compatibility, old.Tags, command.CategoryPaths ?? old.CategoryPaths,
            plan.PromotedDefaults, old.AuthorCollections, old.PrefabVariant, old.PrefabComposition, old.AssetBudget);
        var hashInput = $"{blueprint.Provenance.ContentHash}|{plan.TargetVersion}|" +
                        string.Join('|', plan.PromotedDefaults.Select(item => $"{item.Key}:{item.Kind}:{item.Value}"));
        var contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput))).ToLowerInvariant();
        var provenance = new BlueprintProvenance(blueprint.Provenance.SchemaVersion, blueprint.Provenance.Source,
            blueprint.Provenance.Author, blueprint.Provenance.LicenseSpdx, contentHash,
            blueprint.Provenance.IsAiGenerated,
            [.. blueprint.Provenance.Modifications, $"Promoted overrides from entity {command.SourceEntityId} to {plan.TargetVersion}"],
            blueprint.Provenance.PermissionEvidence);
        var updatedBlueprint = blueprint.WithPaletteUpdate(authoring, provenance);
        var impactById = plan.Instances.ToDictionary(item => item.EntityId);
        var areas = state.Project.Areas.Select(area => area.WithEntities(area.Entities.Select(entity =>
        {
            if (!impactById.TryGetValue(entity.Id, out var impact) || impact.Action != "upgrade-exact-pin")
                return entity;
            return new EntityInstance(entity.Id, entity.BlueprintId, entity.Transform, entity.Tags, entity.Overrides,
                $"={plan.TargetVersion}");
        }).ToArray())).ToArray();
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module,
            state.Project.Blueprints.Select(item => item.Id == updatedBlueprint.Id ? updatedBlueprint : item).ToArray(),
            areas, state.Project.Dialogues, state.Project.Quests);
        var revision = state.Revision.Next();
        return new(true, new ProjectState(project, revision), command.BaseRevision, revision, [], patch, inverse,
            Audit(command, true));
    }

    private CommandResult Failure(ProjectState state, UpdateBlueprintFromEntityCommand command, string code,
        string message) => new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null,
        null, Audit(command, false));

    private AuditEntry Audit(UpdateBlueprintFromEntityCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);
}
