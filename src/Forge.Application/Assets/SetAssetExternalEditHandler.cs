using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public sealed class SetAssetExternalEditHandler
{
    public const string RevisionConflictCode = "asset.external_edit.revision_conflict";

    private readonly IClock _clock;

    public SetAssetExternalEditHandler(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public AssetExternalEditCommandResult Handle(
        AssetExternalEditState state,
        SetAssetExternalEditCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
        {
            return Failure(state, command, RevisionConflictCode, "External edit configuration changed concurrently.");
        }

        var diagnostics = AssetExternalEditValidator.Validate(command.Configuration)
            .Select(item => new CommandDiagnostic(item.Code, item.Message))
            .ToArray();
        if (diagnostics.Length > 0)
        {
            return new(false, state, command.BaseRevision, null, diagnostics, null, null, Audit(command, false));
        }

        var previous = state.Find(command.Configuration.AssetId);
        var path = $"assets/external-edits/{command.Configuration.Id}";
        var patch = new ProjectPatch(previous is null ? "add" : "replace", path, "Set typed external edit configuration.");
        var inverse = new ProjectPatch(
            previous is null ? "remove" : "replace",
            path,
            previous is null ? "Remove external edit configuration." : "Restore external edit configuration.");
        if (command.DryRun)
        {
            return new(true, state, command.BaseRevision, null, [], patch, inverse, Audit(command, false));
        }

        var configurations = state.Configurations
            .Where(item => item.AssetId != command.Configuration.AssetId)
            .Append(command.Configuration)
            .ToArray();
        var revision = state.Revision.Next();
        return new(
            true,
            new AssetExternalEditState(configurations, revision),
            command.BaseRevision,
            revision,
            [],
            patch,
            inverse,
            Audit(command, true));
    }

    private AssetExternalEditCommandResult Failure(
        AssetExternalEditState state,
        SetAssetExternalEditCommand command,
        string code,
        string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null, Audit(command, false));

    private AuditEntry Audit(SetAssetExternalEditCommand command, bool applied) =>
        new(command.CommandId, "set_asset_external_edit", _clock.UtcNow, applied);
}
