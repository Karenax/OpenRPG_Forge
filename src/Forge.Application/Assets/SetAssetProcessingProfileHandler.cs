using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public sealed class SetAssetProcessingProfileHandler
{
    public const string RevisionConflictCode = "asset.profile.revision_conflict";

    private const string CommandName = "set_asset_processing_profile";
    private readonly IClock _clock;

    public SetAssetProcessingProfileHandler(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public AssetProcessingProfileCommandResult Handle(
        AssetProcessingProfileState state,
        SetAssetProcessingProfileCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
        {
            return Failure(
                state,
                command,
                RevisionConflictCode,
                "Asset processing profiles changed before this command was applied.");
        }

        var diagnostics = AssetProcessingProfileValidator.Validate(command.Profile)
            .Select(diagnostic => new CommandDiagnostic(diagnostic.Code, diagnostic.Message))
            .ToArray();
        if (diagnostics.Length > 0)
        {
            return new AssetProcessingProfileCommandResult(
                false,
                state,
                command.BaseRevision,
                null,
                diagnostics,
                null,
                null,
                Audit(command, false));
        }

        var previous = state.Find(command.Profile.AssetId, command.Profile.Kind);
        var path = $"assets/{command.Profile.AssetId}/profiles/{command.Profile.Kind.ToString().ToLowerInvariant()}";
        var operation = previous is null ? "add" : "replace";
        var patch = new ProjectPatch(operation, path, $"Set {command.Profile.Kind} processing profile.");
        var inverse = new ProjectPatch(
            previous is null ? "remove" : "replace",
            path,
            previous is null ? "Remove asset processing profile." : "Restore previous asset processing profile.");
        if (command.DryRun)
        {
            return new AssetProcessingProfileCommandResult(
                true,
                state,
                command.BaseRevision,
                null,
                [],
                patch,
                inverse,
                Audit(command, false));
        }

        var profiles = state.Profiles
            .Where(profile => profile.AssetId != command.Profile.AssetId || profile.Kind != command.Profile.Kind)
            .Append(command.Profile)
            .ToArray();
        var revision = state.Revision.Next();
        return new AssetProcessingProfileCommandResult(
            true,
            new AssetProcessingProfileState(profiles, revision),
            command.BaseRevision,
            revision,
            [],
            patch,
            inverse,
            Audit(command, true));
    }

    private AssetProcessingProfileCommandResult Failure(
        AssetProcessingProfileState state,
        SetAssetProcessingProfileCommand command,
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

    private AuditEntry Audit(SetAssetProcessingProfileCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);
}
