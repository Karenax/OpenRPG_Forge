using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public sealed class SetAssetPackageManifestHandler
{
    public const string RevisionConflictCode = "asset.manifest.revision_conflict";

    private const string CommandName = "set_asset_package_manifest";
    private readonly IClock _clock;

    public SetAssetPackageManifestHandler(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public AssetPackageManifestCommandResult Handle(
        AssetPackageManifestState state,
        SetAssetPackageManifestCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
        {
            return Failure(
                state,
                command,
                RevisionConflictCode,
                "Asset package manifests changed before this command was applied.");
        }

        var diagnostics = AssetPackageManifestValidator.Validate(command.Manifest)
            .Select(diagnostic => new CommandDiagnostic(diagnostic.Code, diagnostic.Message))
            .ToArray();
        if (diagnostics.Length > 0)
        {
            return new AssetPackageManifestCommandResult(
                false,
                state,
                command.BaseRevision,
                null,
                diagnostics,
                null,
                null,
                Audit(command, false));
        }

        var previous = state.Find(command.Manifest.AssetId);
        var path = $"assets/manifests/{command.Manifest.Id}";
        var patch = new ProjectPatch(
            previous is null ? "add" : "replace",
            path,
            "Set content-addressed asset package manifest.");
        var inverse = new ProjectPatch(
            previous is null ? "remove" : "replace",
            path,
            previous is null ? "Remove asset package manifest." : "Restore previous asset package manifest.");
        if (command.DryRun)
        {
            return new AssetPackageManifestCommandResult(
                true,
                state,
                command.BaseRevision,
                null,
                [],
                patch,
                inverse,
                Audit(command, false));
        }

        var manifests = state.Manifests
            .Where(manifest => manifest.AssetId != command.Manifest.AssetId)
            .Append(command.Manifest)
            .ToArray();
        var revision = state.Revision.Next();
        return new AssetPackageManifestCommandResult(
            true,
            new AssetPackageManifestState(manifests, revision),
            command.BaseRevision,
            revision,
            [],
            patch,
            inverse,
            Audit(command, true));
    }

    private AssetPackageManifestCommandResult Failure(
        AssetPackageManifestState state,
        SetAssetPackageManifestCommand command,
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

    private AuditEntry Audit(SetAssetPackageManifestCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);
}
