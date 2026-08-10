using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Assets;
using Forge.Scripting;

namespace Forge.Application.Assets;

public readonly record struct AssetGovernanceRevision(long Value)
{
    public AssetGovernanceRevision Next() => new(checked(Value + 1));
}

public sealed record AssetGovernanceState
{
    public IReadOnlyList<AssetGovernanceRecord> Records { get; }
    public AssetGovernanceRevision Revision { get; }

    public AssetGovernanceState(
        IReadOnlyList<AssetGovernanceRecord>? records = null,
        AssetGovernanceRevision revision = default)
    {
        var materialized = (records ?? []).ToArray();
        if (materialized.GroupBy(record => record.AssetId).Any(group => group.Count() > 1))
            throw new ArgumentException("Asset governance records must be unique by asset UUID.", nameof(records));
        Records = materialized.OrderBy(record => record.AssetId.ToString(), StringComparer.Ordinal).ToArray();
        Revision = revision;
    }

    public AssetGovernanceRecord? Find(AssetCatalogItemId assetId) =>
        Records.FirstOrDefault(record => record.AssetId == assetId);
}

public sealed record SetAssetGovernanceCommand(
    CommandId CommandId,
    AssetGovernanceRevision BaseRevision,
    AssetGovernanceRecord Record,
    bool DryRun);

public sealed record AssetGovernanceCommandResult(
    bool Succeeded,
    AssetGovernanceState State,
    AssetGovernanceRevision BaseRevision,
    AssetGovernanceRevision? NewRevision,
    IReadOnlyList<CommandDiagnostic> Diagnostics,
    ProjectPatch? Patch,
    ProjectPatch? Inverse,
    AuditEntry Audit);

public sealed class SetAssetGovernanceHandler
{
    public const string RevisionConflictCode = "asset.governance.revision_conflict";
    private readonly IClock _clock;

    public SetAssetGovernanceHandler(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public AssetGovernanceCommandResult Handle(AssetGovernanceState state, SetAssetGovernanceCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (state.Revision != command.BaseRevision)
            return Failure(state, command, RevisionConflictCode, "Asset governance changed before this command was applied.");
        var diagnostics = AssetGovernanceValidator.Validate(command.Record, requireDistributionReady: false)
            .Select(item => new CommandDiagnostic(item.Code, $"{item.Message} [{item.Path}]"))
            .ToArray();
        if (diagnostics.Length > 0)
            return new(false, state, command.BaseRevision, null, diagnostics, null, null, Audit(command, false));

        var previous = state.Find(command.Record.AssetId);
        var path = $"assets/governance/{command.Record.AssetId}";
        var patch = new ProjectPatch(previous is null ? "add" : "replace", path,
            $"Set governance for asset {command.Record.AssetId}.");
        var inverse = new ProjectPatch(previous is null ? "remove" : "replace", path,
            previous is null ? "Remove asset governance." : "Restore previous asset governance.");
        if (command.DryRun)
            return new(true, state, command.BaseRevision, null, [], patch, inverse, Audit(command, false));

        var revision = state.Revision.Next();
        var records = state.Records.Where(item => item.AssetId != command.Record.AssetId).Append(command.Record).ToArray();
        return new(true, new(records, revision), command.BaseRevision, revision, [], patch, inverse, Audit(command, true));
    }

    private AssetGovernanceCommandResult Failure(AssetGovernanceState state, SetAssetGovernanceCommand command,
        string code, string message) => new(false, state, command.BaseRevision, null,
        [new CommandDiagnostic(code, message)], null, null, Audit(command, false));

    private AuditEntry Audit(SetAssetGovernanceCommand command, bool applied) =>
        new(command.CommandId, "set_asset_governance", _clock.UtcNow, applied);
}

public sealed class AssetGovernanceCommandHistory
{
    private readonly Stack<(IReadOnlyList<AssetGovernanceRecord> Before, IReadOnlyList<AssetGovernanceRecord> After)> _undo = [];
    private readonly Stack<(IReadOnlyList<AssetGovernanceRecord> Before, IReadOnlyList<AssetGovernanceRecord> After)> _redo = [];
    public AssetGovernanceState State { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public AssetGovernanceCommandHistory(IReadOnlyList<AssetGovernanceRecord>? records = null) =>
        State = new(records, default);

    public void Record(AssetGovernanceCommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Succeeded || !result.Audit.Applied || result.NewRevision is null ||
            result.Patch is null || result.Inverse is null || result.BaseRevision != State.Revision)
            throw new InvalidOperationException("Only an applied governance command at the current revision can be recorded.");
        _undo.Push((State.Records, result.State.Records));
        _redo.Clear();
        State = result.State;
    }

    public bool Undo()
    {
        if (!_undo.TryPop(out var entry)) return false;
        _redo.Push(entry);
        State = new(entry.Before, State.Revision.Next());
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPop(out var entry)) return false;
        _undo.Push(entry);
        State = new(entry.After, State.Revision.Next());
        return true;
    }
}

public sealed class AssetGovernanceScriptService
{
    private readonly AssetGovernanceState _state;
    private readonly ForgeScriptHandleRegistry _handles;

    public AssetGovernanceScriptService(AssetGovernanceState state, ForgeScriptHandleRegistry handles)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _handles = handles ?? throw new ArgumentNullException(nameof(handles));
    }

    public bool TryOpen(AssetCatalogItemId assetId, out ForgeScriptOpaqueHandle handle)
    {
        ArgumentNullException.ThrowIfNull(assetId);
        var record = _state.Find(assetId);
        handle = record is null ? default : _handles.Register(ForgeScriptHandleKind.AssetGovernance, record.Id.Value);
        return record is not null;
    }

    public AssetGovernanceRecord? Resolve(ForgeScriptOpaqueHandle handle)
    {
        if (!_handles.TryResolve(handle, ForgeScriptHandleKind.AssetGovernance, out var id, out _)) return null;
        return _state.Records.FirstOrDefault(record => record.Id.Value == id);
    }
}
