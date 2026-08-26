using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Assets;
using Forge.Domain.Projects;

namespace Forge.Application.Assets;

public sealed class InstallAssetBlueprintHandler
{
    public const string RevisionConflictCode = "asset.blueprint.revision_conflict";
    public const string GovernanceInvalidCode = "asset.blueprint.governance_invalid";
    public const string CanonicalModelRequiredCode = "asset.blueprint.canonical_model_required";
    public const string IdentityConflictCode = "asset.blueprint.identity_conflict";

    private const string CommandName = "install_asset_blueprint";
    private readonly IClock _clock;

    public InstallAssetBlueprintHandler(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public CommandResult Handle(ProjectState state, InstallAssetBlueprintCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
        {
            return Failure(state, command, RevisionConflictCode, "Project changed before asset blueprint installation.");
        }

        var manifestDiagnostics = AssetPackageManifestValidator.Validate(command.Manifest);
        if (!command.Item.LicenseVerified || command.Item.Id != command.Manifest.AssetId ||
            !string.Equals(command.Item.LicenseSpdx, command.Manifest.LicenseSpdx, StringComparison.OrdinalIgnoreCase) ||
            manifestDiagnostics.Count > 0)
        {
            return Failure(
                state,
                command,
                GovernanceInvalidCode,
                manifestDiagnostics.Count > 0
                    ? manifestDiagnostics[0].Message
                    : "Asset blueprint requires matching verified license, identity and manifest governance.");
        }

        var modelChunks = command.Manifest.Chunks.Where(chunk => chunk.Role == AssetChunkRole.Model).ToArray();
        if (modelChunks.Length != 1 || !modelChunks[0].Required ||
            !IsRuntimeModel(modelChunks[0].RelativePath))
        {
            return Failure(
                state,
                command,
                CanonicalModelRequiredCode,
                "Asset blueprint placement requires one governed glTF or GLB model chunk.");
        }

        var blueprint = AssetBlueprintFactory.Create(command.Item, command.Manifest);
        var existing = state.Project.Blueprints.SingleOrDefault(candidate => candidate.Id == blueprint.Id);
        if ((existing is not null &&
             (!AssetBlueprintFactory.TryGetAssetId(existing, out var existingAssetId) || existingAssetId != command.Item.Id)) ||
            state.Project.Blueprints.Any(candidate =>
                candidate.Id != blueprint.Id && string.Equals(candidate.Slug, blueprint.Slug, StringComparison.Ordinal)))
        {
            return Failure(
                state,
                command,
                IdentityConflictCode,
                "Asset blueprint ID or slug conflicts with unrelated project content.");
        }

        var patch = new ProjectPatch(
            existing is null ? "add" : "replace",
            $"blueprints/{blueprint.Id}",
            $"Install governed asset blueprint '{blueprint.Slug}'.");
        var inverse = new ProjectPatch(
            existing is null ? "remove" : "replace",
            $"blueprints/{blueprint.Id}",
            existing is null
                ? $"Remove governed asset blueprint '{blueprint.Slug}'."
                : $"Restore previous asset blueprint '{existing.Slug}'.");
        if (command.DryRun)
        {
            return new(true, state, command.BaseRevision, null, [], patch, inverse, Audit(command, false));
        }

        var blueprints = existing is null
            ? state.Project.Blueprints.Append(blueprint).ToArray()
            : state.Project.Blueprints.Select(candidate => candidate.Id == blueprint.Id ? blueprint : candidate).ToArray();
        var project = new ProjectDefinition(
            state.Project.Id,
            state.Project.Title,
            state.Project.Module,
            blueprints,
            state.Project.Areas,
            state.Project.Dialogues,
            state.Project.Quests);
        var revision = state.Revision.Next();
        return new(
            true,
            new ProjectState(project, revision),
            command.BaseRevision,
            revision,
            [],
            patch,
            inverse,
            Audit(command, true));
    }

    private CommandResult Failure(
        ProjectState state,
        InstallAssetBlueprintCommand command,
        string code,
        string message) =>
        new(
            false,
            state,
            command.BaseRevision,
            null,
            [new CommandDiagnostic(code, message)],
            null,
            null,
            Audit(command, false));

    private AuditEntry Audit(InstallAssetBlueprintCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);

    private static bool IsRuntimeModel(string path) =>
        string.Equals(Path.GetExtension(path), ".glb", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Path.GetExtension(path), ".gltf", StringComparison.OrdinalIgnoreCase);
}
