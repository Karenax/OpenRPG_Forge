using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Assistant;

namespace Forge.ModuleFormat.Workspaces;

public sealed record ApplyAssistedForgeScriptCommand(
    Guid CommandId,
    Guid TransactionId,
    Guid ExecutionPlanId,
    long BaseRevision,
    AssistedForgeScriptProposal Proposal,
    AssistantPermissionLevel Permission,
    bool DryRun);

public sealed record AssistedForgeScriptFilePatch(string RelativePath, string? Before, string After);

public sealed record AssistedForgeScriptApplyResult(
    bool Succeeded,
    long BaseRevision,
    Guid TransactionId,
    Guid ExecutionPlanId,
    string SemanticDiff,
    AssistedForgeScriptFilePatch? Patch,
    IReadOnlyList<string> Diagnostics,
    bool Applied);

public sealed class ApplyAssistedForgeScriptHandler
{
    public const string RevisionConflictCode = "assistant.script.revision_conflict";
    public const string ApprovalRequiredCode = "assistant.script.approval_required";
    public const string PermissionDeniedCode = "assistant.script.permission_denied";
    public const string ProposalInvalidCode = "assistant.script.proposal_invalid";
    public const string PlanNotPreviewedCode = "assistant.script.plan_not_previewed";
    public const string SourceChangedCode = "assistant.script.source_changed";
    private readonly Dictionary<Guid, (long Revision, Guid TransactionId, string Fingerprint)> _previews = [];

    public AssistedForgeScriptApplyResult Handle(string workspaceRoot, long currentRevision,
        ApplyAssistedForgeScriptCommand command)
    {
        if (command.BaseRevision != currentRevision) return Failure(command, RevisionConflictCode);
        if (command.CommandId == Guid.Empty || command.TransactionId == Guid.Empty || command.ExecutionPlanId == Guid.Empty)
            return Failure(command, PlanNotPreviewedCode);
        if (!command.Proposal.ApprovedForApplication) return Failure(command, ApprovalRequiredCode);
        if (command.Permission < AssistantPermissionLevel.ApplyApprovedPlan) return Failure(command, PermissionDeniedCode);
        if (!command.Proposal.CanApply) return Failure(command, ProposalInvalidCode);
        var path = AssistedForgeScriptCreationService.ResolvePath(workspaceRoot, command.Proposal.RelativePath);
        var before = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
        var beforeHash = AssistedForgeScriptCreationService.Sha256(before ?? string.Empty);
        if (!string.Equals(beforeHash, command.Proposal.BaseContentSha256, StringComparison.Ordinal))
            return Failure(command, SourceChangedCode);
        var fingerprint = Fingerprint(command.Proposal);
        if (command.DryRun)
        {
            lock (_previews)
            {
                if (_previews.Count >= 128) _previews.Clear();
                _previews[command.ExecutionPlanId] = (command.BaseRevision, command.TransactionId, fingerprint);
            }
            return new(true, command.BaseRevision, command.TransactionId, command.ExecutionPlanId,
                command.Proposal.SemanticDiff, new(command.Proposal.RelativePath, before, command.Proposal.Source), [], false);
        }
        lock (_previews)
        {
            if (!_previews.TryGetValue(command.ExecutionPlanId, out var preview) ||
                preview.Revision != command.BaseRevision || preview.TransactionId != command.TransactionId ||
                preview.Fingerprint != fingerprint)
                return Failure(command, PlanNotPreviewedCode);
            _previews.Remove(command.ExecutionPlanId);
        }
        WriteAtomic(path, command.Proposal.Source);
        return new(true, command.BaseRevision, command.TransactionId, command.ExecutionPlanId,
            command.Proposal.SemanticDiff.Replace("add-script", "apply-script", StringComparison.Ordinal)
                .Replace("replace-script", "apply-script", StringComparison.Ordinal),
            new(command.Proposal.RelativePath, before, command.Proposal.Source), [], true);
    }

    public static void Restore(string workspaceRoot, AssistedForgeScriptFilePatch patch, bool useAfter)
    {
        var path = AssistedForgeScriptCreationService.ResolvePath(workspaceRoot, patch.RelativePath);
        var content = useAfter ? patch.After : patch.Before;
        if (content is null)
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }
        WriteAtomic(path, content);
    }

    private static void WriteAtomic(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static AssistedForgeScriptApplyResult Failure(ApplyAssistedForgeScriptCommand command, string code) =>
        new(false, command.BaseRevision, command.TransactionId, command.ExecutionPlanId, "no-change", null, [code], false);

    private static string Fingerprint(AssistedForgeScriptProposal proposal)
    {
        var value = $"{proposal.ScriptId:D}|{proposal.RelativePath}|{proposal.EventBinding}|{proposal.SourceSha256}|" +
                    $"{proposal.BaseContentSha256}|{proposal.ForgeScriptApiVersion}|{proposal.Provenance}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}

public sealed class AssistedForgeScriptCommandHistory
{
    private readonly List<AssistedForgeScriptFilePatch> _entries = [];
    private int _position;

    public void Record(AssistedForgeScriptApplyResult result)
    {
        if (!result.Succeeded || !result.Applied || result.Patch is null)
            throw new InvalidOperationException("Only applied ForgeScript results can be recorded.");
        if (_position < _entries.Count) _entries.RemoveRange(_position, _entries.Count - _position);
        _entries.Add(result.Patch);
        _position++;
    }

    public bool Undo(string workspaceRoot)
    {
        if (_position == 0) return false;
        ApplyAssistedForgeScriptHandler.Restore(workspaceRoot, _entries[_position - 1], useAfter: false);
        _position--;
        return true;
    }

    public bool Redo(string workspaceRoot)
    {
        if (_position >= _entries.Count) return false;
        ApplyAssistedForgeScriptHandler.Restore(workspaceRoot, _entries[_position], useAfter: true);
        _position++;
        return true;
    }

    public void Reset()
    {
        _entries.Clear();
        _position = 0;
    }
}
