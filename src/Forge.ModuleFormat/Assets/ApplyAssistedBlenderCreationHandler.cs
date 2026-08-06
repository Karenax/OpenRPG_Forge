using System.Security.Cryptography;
using System.Text;
using Forge.Application.Assets;
using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Assets;
using Forge.Domain.Assistant;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Assets;

public sealed record ApplyAssistedBlenderCreationCommand(
    CommandId CommandId,
    Guid TransactionId,
    Guid ExecutionPlanId,
    AssetExternalEditRevision BaseRevision,
    AssistedBlenderCreationProposal Proposal,
    AssistantPermissionLevel Permission,
    bool DryRun);

public sealed record AssistedBlenderFilePatch(string RelativePath, string? Before, string After);

public sealed record AssistedBlenderCreationApplyResult(
    bool Succeeded,
    AssetExternalEditState State,
    AssetExternalEditRevision BaseRevision,
    AssetExternalEditRevision? NewRevision,
    Guid TransactionId,
    Guid ExecutionPlanId,
    string SemanticDiff,
    IReadOnlyList<AssistedBlenderFilePatch> Patches,
    IReadOnlyList<string> Diagnostics,
    bool Applied,
    AuditEntry Audit);

public sealed class ApplyAssistedBlenderCreationHandler
{
    public const string RevisionConflictCode = "assistant.blender.revision_conflict";
    public const string ApprovalRequiredCode = "assistant.blender.approval_required";
    public const string PermissionDeniedCode = "assistant.blender.permission_denied";
    public const string ProposalInvalidCode = "assistant.blender.proposal_invalid";
    public const string PlanNotPreviewedCode = "assistant.blender.plan_not_previewed";
    public const string SourceChangedCode = "assistant.blender.source_changed";
    public const string WriteFailedCode = "assistant.blender.write_failed";
    private readonly IClock _clock;
    private readonly SetAssetExternalEditHandler _externalEditHandler;
    private readonly Dictionary<Guid, (AssetExternalEditRevision Revision, Guid TransactionId, string Fingerprint)> _previews = [];

    public ApplyAssistedBlenderCreationHandler(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _externalEditHandler = new SetAssetExternalEditHandler(clock);
    }

    public AssistedBlenderCreationApplyResult Handle(
        string workspaceRoot,
        AssetExternalEditState state,
        ApplyAssistedBlenderCreationCommand command)
    {
        if (command.BaseRevision != state.Revision) return Failure(state, command, RevisionConflictCode);
        if (command.TransactionId == Guid.Empty || command.ExecutionPlanId == Guid.Empty)
            return Failure(state, command, PlanNotPreviewedCode);
        if (!command.Proposal.ApprovedForApplication) return Failure(state, command, ApprovalRequiredCode);
        if (command.Permission < AssistantPermissionLevel.ApplyApprovedPlan)
            return Failure(state, command, PermissionDeniedCode);
        if (!command.Proposal.CanApply) return Failure(state, command, ProposalInvalidCode);
        var recipePath = AssistedBlenderCreationService.ResolvePath(workspaceRoot, command.Proposal.RecipeRelativePath);
        var beforeRecipe = File.Exists(recipePath) ? File.ReadAllText(recipePath, Encoding.UTF8) : null;
        if (!string.Equals(AssistedBlenderCreationService.Sha256(beforeRecipe ?? string.Empty),
                command.Proposal.BaseRecipeSha256, StringComparison.Ordinal))
            return Failure(state, command, SourceChangedCode);
        var setCommand = new SetAssetExternalEditCommand(command.CommandId, state.Revision,
            command.Proposal.Configuration, command.DryRun);
        var external = _externalEditHandler.Handle(state, setCommand);
        if (!external.Succeeded) return Failure(state, command, external.Diagnostics[0].Code);
        var requestRelative = $"assets/roundtrip/{command.Proposal.Configuration.AssetId}.worker-request.json";
        var externalRelative = AssetExternalEditStore.RelativePath;
        var beforePatches = new[]
        {
            Snapshot(workspaceRoot, command.Proposal.RecipeRelativePath, command.Proposal.RecipeJson),
            Snapshot(workspaceRoot, externalRelative, string.Empty),
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
                command.Proposal.SemanticDiff, beforePatches, [], false,
                new AuditEntry(command.CommandId, "apply_assisted_blender_creation", _clock.UtcNow, false));
        }
        lock (_previews)
        {
            if (!_previews.TryGetValue(command.ExecutionPlanId, out var preview) ||
                preview.Revision != command.BaseRevision || preview.TransactionId != command.TransactionId ||
                preview.Fingerprint != fingerprint)
                return Failure(state, command, PlanNotPreviewedCode);
            _previews.Remove(command.ExecutionPlanId);
        }
        try
        {
            WriteAtomic(recipePath, command.Proposal.RecipeJson);
            var externalSave = AssetExternalEditStore.Save(workspaceRoot, external.State.Configurations);
            if (!externalSave.Succeeded) throw new IOException(externalSave.Diagnostics[0].Message);
            var options = new BlenderWorkerOptions(true, 1m, "ground_center",
                command.Proposal.Definition.LodRatios, command.Proposal.Definition.GenerateCollision,
                [], [], command.Proposal.Definition);
            var request = BlenderWorkerRequestStore.Save(workspaceRoot, command.Proposal.Configuration, options);
            if (!request.Succeeded) throw new IOException(request.Diagnostics[0].Message);
            var patches = beforePatches.Select(item => item with
            {
                After = File.ReadAllText(ResolveWorkspacePath(workspaceRoot, item.RelativePath), Encoding.UTF8),
            }).ToArray();
            return new(true, external.State, state.Revision, external.State.Revision, command.TransactionId,
                command.ExecutionPlanId, command.Proposal.SemanticDiff.Replace("add-generated-asset", "apply-generated-asset",
                    StringComparison.Ordinal), patches, [], true,
                new AuditEntry(command.CommandId, "apply_assisted_blender_creation", _clock.UtcNow, true));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Restore(workspaceRoot, beforePatches, useAfter: false);
            return Failure(state, command, WriteFailedCode, exception.Message);
        }
    }

    public static void Restore(string workspaceRoot, IReadOnlyList<AssistedBlenderFilePatch> patches, bool useAfter)
    {
        foreach (var patch in patches.Reverse())
        {
            var path = ResolveWorkspacePath(workspaceRoot, patch.RelativePath);
            var content = useAfter ? patch.After : patch.Before;
            if (content is null)
            {
                if (File.Exists(path)) File.Delete(path);
                continue;
            }
            WriteAtomic(path, content);
        }
    }

    private static AssistedBlenderFilePatch Snapshot(string workspaceRoot, string relativePath, string after) =>
        new(relativePath, ReadOptional(ResolveWorkspacePath(workspaceRoot, relativePath)), after);

    private static string? ReadOptional(string path) => File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;

    private static string ResolveWorkspacePath(string workspaceRoot, string relativePath)
    {
        var root = Path.GetFullPath(workspaceRoot);
        var full = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Assisted Blender patch escapes the workspace.", nameof(relativePath));
        return full;
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

    private AssistedBlenderCreationApplyResult Failure(AssetExternalEditState state,
        ApplyAssistedBlenderCreationCommand command, string code, string? message = null) =>
        new(false, state, state.Revision, null, command.TransactionId, command.ExecutionPlanId, "no-change", [],
            [message is null ? code : $"{code}: {message}"], false,
            new AuditEntry(command.CommandId, "apply_assisted_blender_creation", _clock.UtcNow, false));

    private static string Fingerprint(AssistedBlenderCreationProposal proposal)
    {
        var value = $"{proposal.Definition.Id:D}|{proposal.RecipeSha256}|{proposal.Configuration.AssetId}|" +
                    $"{proposal.SemanticDiff}|{proposal.Provenance}|{proposal.ApprovedForApplication}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}

public sealed class AssistedBlenderCreationCommandHistory
{
    private readonly List<(IReadOnlyList<AssistedBlenderFilePatch> Patches,
        IReadOnlyList<AssetExternalEditConfiguration> Before,
        IReadOnlyList<AssetExternalEditConfiguration> After)> _entries = [];
    private int _position;

    public AssistedBlenderCreationCommandHistory(AssetExternalEditState state) => State = state;

    public AssetExternalEditState State { get; private set; }

    public void Record(AssistedBlenderCreationApplyResult result)
    {
        if (!result.Succeeded || !result.Applied) throw new InvalidOperationException("Only applied Blender results can be recorded.");
        if (_position < _entries.Count) _entries.RemoveRange(_position, _entries.Count - _position);
        _entries.Add((result.Patches, State.Configurations, result.State.Configurations));
        State = result.State;
        _position++;
    }

    public bool Undo(string workspaceRoot)
    {
        if (_position == 0) return false;
        var entry = _entries[_position - 1];
        ApplyAssistedBlenderCreationHandler.Restore(workspaceRoot, entry.Patches, false);
        State = new AssetExternalEditState(entry.Before, State.Revision.Next());
        _position--;
        return true;
    }

    public bool Redo(string workspaceRoot)
    {
        if (_position >= _entries.Count) return false;
        var entry = _entries[_position];
        ApplyAssistedBlenderCreationHandler.Restore(workspaceRoot, entry.Patches, true);
        State = new AssetExternalEditState(entry.After, State.Revision.Next());
        _position++;
        return true;
    }
}
