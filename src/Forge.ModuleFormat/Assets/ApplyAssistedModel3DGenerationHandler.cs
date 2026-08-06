using System.Security.Cryptography;
using System.Text;
using Forge.Application.Assets;
using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Assets;
using Forge.Domain.Assistant;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Assets;

public sealed record ApplyAssistedModel3DGenerationCommand(
    CommandId CommandId,
    Guid TransactionId,
    Guid ExecutionPlanId,
    AssetExternalEditRevision BaseRevision,
    AssistedModel3DGenerationProposal Proposal,
    AssistantPermissionLevel Permission,
    bool DryRun);

public sealed record AssistedModel3DFilePatch(string RelativePath, string? Before, string After);

public sealed record AssistedModel3DGenerationApplyResult(
    bool Succeeded,
    AssetExternalEditState State,
    AssetExternalEditRevision BaseRevision,
    AssetExternalEditRevision? NewRevision,
    Guid TransactionId,
    Guid ExecutionPlanId,
    string SemanticDiff,
    IReadOnlyList<AssistedModel3DFilePatch> Patches,
    IReadOnlyList<string> Diagnostics,
    bool Applied,
    AuditEntry Audit);

public sealed class ApplyAssistedModel3DGenerationHandler
{
    public const string RevisionConflictCode = "assistant.model3d.revision_conflict";
    public const string ApprovalRequiredCode = "assistant.model3d.approval_required";
    public const string PermissionDeniedCode = "assistant.model3d.permission_denied";
    public const string ProposalInvalidCode = "assistant.model3d.proposal_invalid";
    public const string PlanNotPreviewedCode = "assistant.model3d.plan_not_previewed";
    public const string SourceChangedCode = "assistant.model3d.source_changed";
    public const string WriteFailedCode = "assistant.model3d.write_failed";
    private readonly IClock _clock;
    private readonly SetAssetExternalEditHandler _externalEditHandler;
    private readonly Dictionary<Guid, (AssetExternalEditRevision Revision, Guid TransactionId, string Fingerprint)> _previews = [];

    public ApplyAssistedModel3DGenerationHandler(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _externalEditHandler = new SetAssetExternalEditHandler(clock);
    }

    public AssistedModel3DGenerationApplyResult Handle(
        string workspaceRoot,
        AssetExternalEditState state,
        ApplyAssistedModel3DGenerationCommand command)
    {
        if (command.BaseRevision != state.Revision) return Failure(state, command, RevisionConflictCode);
        if (command.TransactionId == Guid.Empty || command.ExecutionPlanId == Guid.Empty)
            return Failure(state, command, PlanNotPreviewedCode);
        if (!command.Proposal.ApprovedForApplication || !command.Proposal.Receipt.HumanAccepted)
            return Failure(state, command, ApprovalRequiredCode);
        if (command.Permission < AssistantPermissionLevel.ApplyApprovedPlan)
            return Failure(state, command, PermissionDeniedCode);
        if (!command.Proposal.CanApply || !ProposalIsConsistent(command.Proposal))
            return Failure(state, command, ProposalInvalidCode);

        var sourcePath = AssistedModel3DGenerationService.ResolvePath(workspaceRoot,
            command.Proposal.Receipt.SourceRelativePath, "assets/staging/ai-3d/");
        var receiptPath = AssistedModel3DGenerationService.ResolvePath(workspaceRoot,
            command.Proposal.ReceiptRelativePath, "assets/staging/ai-3d/");
        if (AssistedModel3DGenerationService.Sha256(ReadOptional(sourcePath) ?? string.Empty) != command.Proposal.BaseSourceSha256 ||
            AssistedModel3DGenerationService.Sha256(ReadOptional(receiptPath) ?? string.Empty) != command.Proposal.BaseReceiptSha256)
            return Failure(state, command, SourceChangedCode);

        var external = _externalEditHandler.Handle(state, new SetAssetExternalEditCommand(
            command.CommandId, state.Revision, command.Proposal.Configuration, command.DryRun));
        if (!external.Succeeded) return Failure(state, command, external.Diagnostics[0].Code);
        var requestRelative = $"assets/roundtrip/{command.Proposal.Configuration.AssetId}.worker-request.json";
        var patches = new[]
        {
            Snapshot(workspaceRoot, command.Proposal.Receipt.SourceRelativePath, command.Proposal.SourceObj),
            Snapshot(workspaceRoot, command.Proposal.ReceiptRelativePath, command.Proposal.ReceiptJson),
            Snapshot(workspaceRoot, AssetExternalEditStore.RelativePath, string.Empty),
            Snapshot(workspaceRoot, requestRelative, string.Empty),
        };
        var fingerprint = Fingerprint(command.Proposal);
        if (command.DryRun)
        {
            lock (_previews)
            {
                if (_previews.Count >= 128) _previews.Clear();
                _previews[command.ExecutionPlanId] = (command.BaseRevision, command.TransactionId, fingerprint);
            }
            return new(true, state, state.Revision, null, command.TransactionId, command.ExecutionPlanId,
                command.Proposal.SemanticDiff, patches, [], false,
                new AuditEntry(command.CommandId, "apply_assisted_model3d_generation", _clock.UtcNow, false));
        }
        lock (_previews)
        {
            if (!_previews.TryGetValue(command.ExecutionPlanId, out var preview) || preview.Revision != command.BaseRevision ||
                preview.TransactionId != command.TransactionId || preview.Fingerprint != fingerprint)
                return Failure(state, command, PlanNotPreviewedCode);
            _previews.Remove(command.ExecutionPlanId);
        }
        try
        {
            WriteAtomic(sourcePath, command.Proposal.SourceObj);
            WriteAtomic(receiptPath, command.Proposal.ReceiptJson);
            var externalSave = AssetExternalEditStore.Save(workspaceRoot, external.State.Configurations);
            if (!externalSave.Succeeded) throw new IOException(externalSave.Diagnostics[0].Message);
            var options = new BlenderWorkerOptions(true, 1m, "ground_center", command.Proposal.Receipt.Topology.LodRatios,
                command.Proposal.Receipt.Topology.GenerateCollision, [], []);
            var request = BlenderWorkerRequestStore.Save(workspaceRoot, command.Proposal.Configuration, options);
            if (!request.Succeeded) throw new IOException(request.Diagnostics[0].Message);
            var after = patches.Select(item => item with
            {
                After = File.ReadAllText(ResolveWorkspacePath(workspaceRoot, item.RelativePath), Encoding.UTF8),
            }).ToArray();
            return new(true, external.State, state.Revision, external.State.Revision, command.TransactionId,
                command.ExecutionPlanId, command.Proposal.SemanticDiff.Replace("stage-provider-model", "apply-provider-model",
                    StringComparison.Ordinal), after, [], true,
                new AuditEntry(command.CommandId, "apply_assisted_model3d_generation", _clock.UtcNow, true));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Restore(workspaceRoot, patches, false);
            return Failure(state, command, WriteFailedCode, exception.Message);
        }
    }

    public static void Restore(string workspaceRoot, IReadOnlyList<AssistedModel3DFilePatch> patches, bool useAfter)
    {
        foreach (var patch in patches.Reverse())
        {
            var path = ResolveWorkspacePath(workspaceRoot, patch.RelativePath);
            var content = useAfter ? patch.After : patch.Before;
            if (content is null) { if (File.Exists(path)) File.Delete(path); }
            else WriteAtomic(path, content);
        }
    }

    private static AssistedModel3DFilePatch Snapshot(string root, string relative, string after) =>
        new(relative, ReadOptional(ResolveWorkspacePath(root, relative)), after);
    private static string? ReadOptional(string path) => File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;

    private static string ResolveWorkspacePath(string workspaceRoot, string relativePath)
    {
        if (!AssetCatalogValidator.IsSafeRelativePath(relativePath))
            throw new ArgumentException("Assisted 3D patch path is unsafe.", nameof(relativePath));
        var root = Path.GetFullPath(workspaceRoot);
        var full = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Assisted 3D patch escapes the workspace.", nameof(relativePath));
        return full;
    }

    private static void WriteAtomic(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, content, new UTF8Encoding(false)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private AssistedModel3DGenerationApplyResult Failure(AssetExternalEditState state,
        ApplyAssistedModel3DGenerationCommand command, string code, string? message = null) =>
        new(false, state, state.Revision, null, command.TransactionId, command.ExecutionPlanId, "no-change", [],
            [message is null ? code : $"{code}: {message}"], false,
            new AuditEntry(command.CommandId, "apply_assisted_model3d_generation", _clock.UtcNow, false));

    private static string Fingerprint(AssistedModel3DGenerationProposal proposal)
    {
        var value = $"{proposal.Receipt.Id:D}|{proposal.Receipt.SourceSha256}|" +
                    $"{AssistedModel3DGenerationService.Sha256(proposal.ReceiptJson)}|{proposal.SemanticDiff}|" +
                    $"{proposal.Receipt.HumanAcceptanceProvenance}|{proposal.ApprovedForApplication}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static bool ProposalIsConsistent(AssistedModel3DGenerationProposal proposal)
    {
        var receipt = proposal.Receipt;
        var configuration = proposal.Configuration;
        var sourceBytes = Encoding.UTF8.GetBytes(proposal.SourceObj);
        return Model3DGenerationValidator.ValidateReceipt(receipt).Count == 0 &&
               AssetExternalEditValidator.Validate(configuration).Count == 0 &&
               AssistedModel3DGenerationService.Sha256(sourceBytes) == receipt.SourceSha256 &&
               sourceBytes.LongLength == receipt.SourceBytes &&
               proposal.ReceiptJson == AssistedModel3DGenerationService.SerializeReceipt(receipt) &&
               configuration.SourceContentSha256 == receipt.SourceSha256 &&
               configuration.SourceRelativePath == receipt.SourceRelativePath &&
               configuration.OutputRelativePath == receipt.OutputRelativePath &&
               configuration.LicenseSpdx == receipt.LicenseSpdx &&
               configuration.ModificationProvenance == receipt.ModificationProvenance &&
               proposal.ReceiptRelativePath == $"assets/staging/ai-3d/{receipt.Slug}/receipt.json";
    }
}

public sealed class AssistedModel3DGenerationCommandHistory
{
    private readonly List<(IReadOnlyList<AssistedModel3DFilePatch> Patches,
        IReadOnlyList<AssetExternalEditConfiguration> Before,
        IReadOnlyList<AssetExternalEditConfiguration> After)> _entries = [];
    private int _position;

    public AssistedModel3DGenerationCommandHistory(AssetExternalEditState state) => State = state;
    public AssetExternalEditState State { get; private set; }

    public void Record(AssistedModel3DGenerationApplyResult result)
    {
        if (!result.Succeeded || !result.Applied) throw new InvalidOperationException("Only applied 3D results can be recorded.");
        if (_position < _entries.Count) _entries.RemoveRange(_position, _entries.Count - _position);
        _entries.Add((result.Patches, State.Configurations, result.State.Configurations));
        State = result.State;
        _position++;
    }

    public bool Undo(string workspaceRoot)
    {
        if (_position == 0) return false;
        var entry = _entries[_position - 1];
        ApplyAssistedModel3DGenerationHandler.Restore(workspaceRoot, entry.Patches, false);
        State = new AssetExternalEditState(entry.Before, State.Revision.Next());
        _position--;
        return true;
    }

    public bool Redo(string workspaceRoot)
    {
        if (_position >= _entries.Count) return false;
        var entry = _entries[_position];
        ApplyAssistedModel3DGenerationHandler.Restore(workspaceRoot, entry.Patches, true);
        State = new AssetExternalEditState(entry.After, State.Revision.Next());
        _position++;
        return true;
    }
}
