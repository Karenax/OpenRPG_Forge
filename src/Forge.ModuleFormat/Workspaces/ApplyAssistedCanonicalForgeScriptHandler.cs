using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Assistant;
using Forge.Scripting;

namespace Forge.ModuleFormat.Workspaces;

public sealed record ApplyAssistedCanonicalForgeScriptCommand(
    Guid CommandId,
    Guid TransactionId,
    Guid ExecutionPlanId,
    long BaseRevision,
    AssistedForgeScriptProposal Proposal,
    ForgeScriptSourceDocument Document,
    AssistantPermissionLevel Permission,
    bool DryRun);

public sealed record AssistedCanonicalForgeScriptFilePatch(
    string RelativePath,
    byte[]? BeforeSource,
    byte[]? BeforeMetadata,
    byte[] AfterSource,
    byte[] AfterMetadata);

public sealed record AssistedCanonicalForgeScriptApplyResult(
    bool Succeeded,
    long BaseRevision,
    Guid TransactionId,
    Guid ExecutionPlanId,
    string SemanticDiff,
    AssistedCanonicalForgeScriptFilePatch? Patch,
    IReadOnlyList<string> Diagnostics,
    bool Applied);

/// <summary>
/// Applies an approved assisted proposal through the canonical AngelScript source store.
/// The legacy .forge handler intentionally remains separate for Toolset and MCP compatibility.
/// </summary>
public sealed class ApplyAssistedCanonicalForgeScriptHandler
{
    public const string RevisionConflictCode = "assistant.script.canonical.revision_conflict";
    public const string ApprovalRequiredCode = "assistant.script.canonical.approval_required";
    public const string PermissionDeniedCode = "assistant.script.canonical.permission_denied";
    public const string ProposalInvalidCode = "assistant.script.canonical.proposal_invalid";
    public const string DocumentInvalidCode = "assistant.script.canonical.document_invalid";
    public const string PlanNotPreviewedCode = "assistant.script.canonical.plan_not_previewed";
    public const string SourceChangedCode = "assistant.script.canonical.source_changed";
    public const string SourcePairInvalidCode = "assistant.script.canonical.source_pair_invalid";
    public const string WriteFailedCode = "assistant.script.canonical.write_failed";

    private readonly Dictionary<Guid, PreviewStamp> _previews = [];

    public AssistedCanonicalForgeScriptApplyResult Handle(
        string workspaceRoot,
        long currentRevision,
        ApplyAssistedCanonicalForgeScriptCommand command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Proposal);
        ArgumentNullException.ThrowIfNull(command.Document);

        if (command.BaseRevision != currentRevision) return Failure(command, RevisionConflictCode);
        if (command.CommandId == Guid.Empty || command.TransactionId == Guid.Empty ||
            command.ExecutionPlanId == Guid.Empty)
            return Failure(command, PlanNotPreviewedCode);
        if (!command.Proposal.ApprovedForApplication) return Failure(command, ApprovalRequiredCode);
        if (command.Permission < AssistantPermissionLevel.ApplyApprovedPlan)
            return Failure(command, PermissionDeniedCode);
        if (!command.Proposal.CanApply) return Failure(command, ProposalInvalidCode);
        if (!TryValidateDocument(command.Proposal, command.Document, out var validationDiagnostics))
            return Failure(command, validationDiagnostics.ToArray());

        string sourcePath;
        try
        {
            sourcePath = AssistedForgeScriptCreationService.ResolvePath(workspaceRoot,
                command.Document.RelativePath);
        }
        catch (ArgumentException)
        {
            return Failure(command, DocumentInvalidCode);
        }

        var metadataPath = sourcePath + ".meta.json";
        var sourceExists = File.Exists(sourcePath);
        var metadataExists = File.Exists(metadataPath);
        if (sourceExists != metadataExists) return Failure(command, SourcePairInvalidCode);

        ForgeScriptSourceDocument? previousDocument = null;
        if (sourceExists)
        {
            try
            {
                previousDocument = new ForgeScriptSourceStore(workspaceRoot).Load(command.Document.RelativePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                               InvalidOperationException or ArgumentException or
                                               System.Text.Json.JsonException)
            {
                return Failure(command, $"{SourcePairInvalidCode}:{exception.Message}");
            }
            if (previousDocument.Id != command.Document.Id)
                return Failure(command, DocumentInvalidCode);
        }

        var beforeSource = sourceExists ? File.ReadAllBytes(sourcePath) : null;
        var beforeMetadata = metadataExists ? File.ReadAllBytes(metadataPath) : null;
        var beforeText = sourceExists ? File.ReadAllText(sourcePath, Encoding.UTF8) : string.Empty;
        if (!string.Equals(AssistedForgeScriptCreationService.Sha256(beforeText),
                command.Proposal.BaseContentSha256, StringComparison.Ordinal))
            return Failure(command, SourceChangedCode);

        var diskFingerprint = DiskFingerprint(beforeSource, beforeMetadata);
        var planFingerprint = Fingerprint(command.Proposal, command.Document, diskFingerprint);
        if (command.DryRun)
        {
            lock (_previews)
            {
                if (_previews.Count >= 128) _previews.Clear();
                _previews[command.ExecutionPlanId] = new(command.BaseRevision, command.TransactionId,
                    planFingerprint, diskFingerprint);
            }
            return new(true, command.BaseRevision, command.TransactionId, command.ExecutionPlanId,
                CanonicalSemanticDiff(command.Proposal, previousDocument, command.Document), null, [], false);
        }

        lock (_previews)
        {
            if (!_previews.TryGetValue(command.ExecutionPlanId, out var preview) ||
                preview.Revision != command.BaseRevision || preview.TransactionId != command.TransactionId ||
                preview.PlanFingerprint != planFingerprint || preview.DiskFingerprint != diskFingerprint)
                return Failure(command, PlanNotPreviewedCode);
            _previews.Remove(command.ExecutionPlanId);
        }

        try
        {
            new ForgeScriptSourceStore(workspaceRoot).Save(command.Document);
            var afterSource = File.ReadAllBytes(sourcePath);
            var afterMetadata = File.ReadAllBytes(metadataPath);
            return new(true, command.BaseRevision, command.TransactionId, command.ExecutionPlanId,
                CanonicalSemanticDiff(command.Proposal, previousDocument, command.Document),
                new(command.Document.RelativePath, beforeSource, beforeMetadata, afterSource, afterMetadata),
                [], true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException or
                                           System.Text.Json.JsonException)
        {
            try
            {
                RestorePair(sourcePath, metadataPath, beforeSource, beforeMetadata);
            }
            catch (Exception restoreException) when (restoreException is IOException or UnauthorizedAccessException)
            {
                return Failure(command, $"{WriteFailedCode}:{exception.Message}|rollback:{restoreException.Message}");
            }
            return Failure(command, $"{WriteFailedCode}:{exception.Message}");
        }
    }

    public static void Restore(
        string workspaceRoot,
        AssistedCanonicalForgeScriptFilePatch patch,
        bool useAfter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(patch);
        var sourcePath = AssistedForgeScriptCreationService.ResolvePath(workspaceRoot, patch.RelativePath);
        RestorePair(sourcePath, sourcePath + ".meta.json",
            useAfter ? patch.AfterSource : patch.BeforeSource,
            useAfter ? patch.AfterMetadata : patch.BeforeMetadata);
    }

    private static bool TryValidateDocument(
        AssistedForgeScriptProposal proposal,
        ForgeScriptSourceDocument document,
        out IReadOnlyList<string> diagnostics)
    {
        var failures = new List<string>();
        if (!ForgeScriptSourceValidator.IsSafeSourcePath(document.RelativePath) ||
            !document.RelativePath.Replace('\\', '/').StartsWith("scripts/", StringComparison.Ordinal) ||
            !string.Equals(proposal.RelativePath, document.RelativePath, StringComparison.Ordinal) ||
            proposal.ScriptId != document.Id ||
            !string.Equals(proposal.Source, document.Source, StringComparison.Ordinal) ||
            !string.Equals(proposal.SourceSha256,
                AssistedForgeScriptCreationService.Sha256(document.Source), StringComparison.Ordinal) ||
            !string.Equals(proposal.ForgeScriptApiVersion, document.ApiVersion, StringComparison.Ordinal) ||
            !string.Equals(proposal.Provenance, document.Provenance, StringComparison.Ordinal))
            failures.Add(DocumentInvalidCode);

        var contracts = document.EventBindings.GroupBy(binding => binding.EventName, StringComparer.Ordinal)
            .Select(group => new ForgeScriptEventContract(group.First().Id, 1, group.First().Provenance,
                group.Key, group.First().PayloadType, document.RequiredAtRuntime)).ToArray();
        failures.AddRange(ForgeScriptSourceValidator.Validate(document, contracts)
            .Where(item => string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase))
            .Select(item => $"{item.Code}:{item.Path}"));
        diagnostics = failures.Distinct(StringComparer.Ordinal).ToArray();
        return diagnostics.Count == 0;
    }

    private static string CanonicalSemanticDiff(
        AssistedForgeScriptProposal proposal,
        ForgeScriptSourceDocument? previous,
        ForgeScriptSourceDocument candidate) =>
        $"canonical-{(previous is null ? "add" : "replace")}-script:{candidate.RelativePath}|" +
        $"bytes:{Encoding.UTF8.GetByteCount(previous?.Source ?? string.Empty)}->" +
        $"{Encoding.UTF8.GetByteCount(candidate.Source)}|bindings:{previous?.EventBindings.Count ?? 0}->" +
        $"{candidate.EventBindings.Count}|api:{candidate.ApiVersion}|tests:{proposal.Tests.Count}";

    private static AssistedCanonicalForgeScriptApplyResult Failure(
        ApplyAssistedCanonicalForgeScriptCommand command,
        params string[] codes) =>
        new(false, command.BaseRevision, command.TransactionId, command.ExecutionPlanId,
            "no-change", null, codes, false);

    private static string Fingerprint(
        AssistedForgeScriptProposal proposal,
        ForgeScriptSourceDocument document,
        string diskFingerprint)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(proposal.ScriptId.ToByteArray());
            writer.Write(proposal.RelativePath);
            writer.Write(proposal.EventBinding);
            writer.Write(proposal.SourceSha256);
            writer.Write(proposal.BaseContentSha256);
            writer.Write(proposal.ForgeScriptApiVersion);
            writer.Write(proposal.Provenance);
            writer.Write(document.SchemaVersion);
            writer.Write(document.Distribution.ToString());
            writer.Write(document.RequiredAtRuntime);
            writer.Write(document.Dependencies.Count);
            foreach (var dependency in document.Dependencies) writer.Write(dependency);
            writer.Write(document.EventBindings.Count);
            foreach (var binding in document.EventBindings)
            {
                writer.Write(binding.Id.ToByteArray());
                writer.Write(binding.SchemaVersion);
                writer.Write(binding.Provenance);
                writer.Write(binding.ComponentId);
                writer.Write(binding.EventName);
                writer.Write(binding.Handler);
                writer.Write(binding.PayloadType);
                writer.Write(binding.Priority);
                writer.Write(binding.EnableCondition ?? string.Empty);
                writer.Write(binding.Authority.ToString());
                writer.Write(binding.Enabled);
            }
            var capabilities = document.RequiredCapabilities?.Order(StringComparer.Ordinal).ToArray() ?? [];
            writer.Write(capabilities.Length);
            foreach (var capability in capabilities) writer.Write(capability);
            writer.Write(diskFingerprint);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static string DiskFingerprint(byte[]? source, byte[]? metadata)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            WriteBytes(writer, source);
            WriteBytes(writer, metadata);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void WriteBytes(BinaryWriter writer, byte[]? value)
    {
        writer.Write(value is not null);
        if (value is null) return;
        writer.Write(value.Length);
        writer.Write(value);
    }

    private static void RestorePair(string sourcePath, string metadataPath, byte[]? source, byte[]? metadata)
    {
        RestoreFile(sourcePath, source);
        RestoreFile(metadataPath, metadata);
    }

    private static void RestoreFile(string path, byte[]? content)
    {
        if (content is null)
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporary, content);
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private sealed record PreviewStamp(
        long Revision,
        Guid TransactionId,
        string PlanFingerprint,
        string DiskFingerprint);
}

public sealed class AssistedCanonicalForgeScriptCommandHistory
{
    private readonly List<AssistedCanonicalForgeScriptFilePatch> _entries = [];
    private int _position;

    public bool CanUndo => _position > 0;
    public bool CanRedo => _position < _entries.Count;

    public void Record(AssistedCanonicalForgeScriptApplyResult result)
    {
        if (!result.Succeeded || !result.Applied || result.Patch is null)
            throw new InvalidOperationException("Only applied canonical ForgeScript results can be recorded.");
        if (_position < _entries.Count) _entries.RemoveRange(_position, _entries.Count - _position);
        _entries.Add(result.Patch);
        _position++;
    }

    public bool Undo(string workspaceRoot)
    {
        if (!CanUndo) return false;
        ApplyAssistedCanonicalForgeScriptHandler.Restore(workspaceRoot, _entries[_position - 1], useAfter: false);
        _position--;
        return true;
    }

    public bool Redo(string workspaceRoot)
    {
        if (!CanRedo) return false;
        ApplyAssistedCanonicalForgeScriptHandler.Restore(workspaceRoot, _entries[_position], useAfter: true);
        _position++;
        return true;
    }

    public void Reset()
    {
        _entries.Clear();
        _position = 0;
    }
}
