using System.Collections.Concurrent;
using System.Security.Cryptography;
using Forge.Application.Gameplay;
using Forge.Domain.Validation;

namespace Forge.ModuleFormat.Audio;

public sealed record GovernedDialogueVoiceImportRequest(
    int SchemaVersion,
    string SourceRoot,
    string SourceRelativePath,
    string DestinationRelativePath,
    string ExpectedSha256,
    string MimeType,
    string LicenseSpdx,
    string Provenance);

public sealed record GovernedDialogueVoiceImportPlan(
    GovernedDialogueVoiceImportRequest Request,
    string DestinationRelativePath,
    string ContentSha256,
    long SourceBytes,
    bool Reused);

public sealed record GovernedDialogueVoiceImportPlanResult(
    GovernedDialogueVoiceImportPlan? Plan,
    IReadOnlyList<DomainDiagnostic> Diagnostics)
{
    public bool Succeeded => Plan is not null && Diagnostics.Count == 0;
}

public sealed record GovernedDialogueVoiceImportApplyResult(
    bool Succeeded,
    bool Created,
    bool Reused,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public sealed record GovernedDialogueVoiceImportRevertResult(
    bool Succeeded,
    IReadOnlyList<DomainDiagnostic> Diagnostics,
    bool DeletionSkipped);

public static class GovernedDialogueVoiceImportService
{
    public const long MaximumBytes = 64L * 1024 * 1024;
    public const string SchemaUnsupportedCode = "dialogue.voice.import.schema_unsupported";
    public const string SourcePathUnsafeCode = "dialogue.voice.import.source_path_unsafe";
    public const string DestinationPathUnsafeCode = "dialogue.voice.import.destination_path_unsafe";
    public const string ExtensionUnsupportedCode = "dialogue.voice.import.extension_unsupported";
    public const string MimeInvalidCode = "dialogue.voice.import.mime_invalid";
    public const string SourceInvalidCode = "dialogue.voice.import.source_invalid";
    public const string SizeInvalidCode = "dialogue.voice.import.size_invalid";
    public const string HashMismatchCode = "dialogue.voice.import.hash_mismatch";
    public const string LicenseInvalidCode = "dialogue.voice.import.license_invalid";
    public const string ProvenanceInvalidCode = "dialogue.voice.import.provenance_invalid";
    public const string CollisionCode = "dialogue.voice.import.destination_collision";
    public const string PlanMismatchCode = "dialogue.voice.import.plan_mismatch";
    public const string ApplyFailedCode = "dialogue.voice.import.apply_failed";
    public const string RevertConflictCode = "dialogue.voice.import.revert_conflict";
    public const string BlobRetainedCode = "dialogue.voice.import.blob_retained";
    public const string CleanupFailedCode = "dialogue.voice.import.cleanup_failed";

    private static readonly ConcurrentDictionary<string, object> MutationGates =
        new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    public static GovernedDialogueVoiceImportPlanResult Plan(
        string workspaceRoot,
        GovernedDialogueVoiceImportRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(request);
        var diagnostics = new List<DomainDiagnostic>();
        if (request.SchemaVersion != 1) Add(SchemaUnsupportedCode, "Voice import schema must be 1.", "schemaVersion");
        if (!ValidSpdx(request.LicenseSpdx)) Add(LicenseInvalidCode, "Voice import requires a valid SPDX license.", "licenseSpdx");
        if (string.IsNullOrWhiteSpace(request.Provenance)) Add(ProvenanceInvalidCode, "Voice import requires provenance.", "provenance");

        var extension = Path.GetExtension(request.SourceRelativePath);
        if (extension is not (".mp3" or ".MP3" or ".wav" or ".WAV"))
            Add(ExtensionUnsupportedCode, "Voice import supports MP3 and WAV files only.", "sourceRelativePath");
        var source = RuntimeModuleAssetPathResolver.ResolveVoiceAudio(request.SourceRoot, request.SourceRelativePath);
        if (source is null) Add(SourcePathUnsafeCode, "Voice source must be a regular file contained by the approved source root.", "sourceRelativePath");
        var destination = RuntimeModuleAssetPathResolver.ResolveVoiceAudioDestination(workspaceRoot,
            request.DestinationRelativePath);
        if (destination is null || !request.DestinationRelativePath.Replace('\\', '/').StartsWith(
                "audio/dialogues/", StringComparison.Ordinal))
            Add(DestinationPathUnsafeCode, "Voice destination must be a safe path below audio/dialogues.", "destinationRelativePath");
        if (diagnostics.Count > 0) return new(null, diagnostics);

        try
        {
            var info = new FileInfo(source!);
            if (!info.Exists || (info.Attributes & FileAttributes.Directory) != 0)
                Add(SourceInvalidCode, "Voice source must be a regular file.", "sourceRelativePath");
            if (info.Length is < 1 or > MaximumBytes)
                Add(SizeInvalidCode, $"Voice source must be between 1 and {MaximumBytes} bytes.", "sourceRelativePath");
            var bytes = File.ReadAllBytes(source!);
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (request.ExpectedSha256 is not { Length: 64 } ||
                !string.Equals(hash, request.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                Add(HashMismatchCode, "Voice source SHA-256 does not match the expected hash.", "expectedSha256");
            if (!AudioMediaStructureValidator.Matches(extension, request.MimeType, bytes))
                Add(MimeInvalidCode, "Voice source extension, MIME type, and file signature do not agree.", "mimeType");

            var reused = false;
            if (File.Exists(destination!))
            {
                var destinationHash = HashFile(destination!);
                if (!string.Equals(destinationHash, hash, StringComparison.OrdinalIgnoreCase))
                    Add(CollisionCode, "Voice destination already contains different bytes.", "destinationRelativePath");
                else reused = true;
            }
            return diagnostics.Count == 0
                ? new(new(request, request.DestinationRelativePath.Replace('\\', '/'), hash, info.Length, reused), [])
                : new(null, diagnostics);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            return new(null, [new DomainDiagnostic(SourceInvalidCode,
                $"Voice source could not be read: {exception.GetType().Name}.", "sourceRelativePath")]);
        }

        void Add(string code, string message, string path) => diagnostics.Add(new(code, message, path));
    }

    public static GovernedDialogueVoiceImportApplyResult Apply(
        string workspaceRoot,
        GovernedDialogueVoiceImportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        try
        {
            var root = Path.GetFullPath(workspaceRoot);
            lock (MutationGates.GetOrAdd(root, static _ => new object()))
            {
                var refreshed = Plan(workspaceRoot, plan.Request);
                if (!refreshed.Succeeded) return new(false, false, false, refreshed.Diagnostics);
                var canonicalPlan = refreshed.Plan!;
                if (!string.Equals(canonicalPlan.DestinationRelativePath, plan.DestinationRelativePath,
                        StringComparison.Ordinal) ||
                    !string.Equals(canonicalPlan.ContentSha256, plan.ContentSha256,
                        StringComparison.OrdinalIgnoreCase) || canonicalPlan.SourceBytes != plan.SourceBytes)
                    return new(false, false, false, [new(PlanMismatchCode,
                        "Voice import plan no longer matches its canonical request and source.",
                        "destinationRelativePath")]);
                if (canonicalPlan.Reused) return new(true, false, true, []);

                var source = RuntimeModuleAssetPathResolver.ResolveVoiceAudio(plan.Request.SourceRoot,
                    plan.Request.SourceRelativePath);
                var destination = RuntimeModuleAssetPathResolver.ResolveVoiceAudioDestination(workspaceRoot,
                    canonicalPlan.DestinationRelativePath);
                if (source is null || destination is null)
                    return new(false, false, false, [new(DestinationPathUnsafeCode,
                        "Voice source or destination is no longer safely contained.", "destinationRelativePath")]);

                var sourceBytes = File.ReadAllBytes(source);
                var sourceHash = Convert.ToHexString(SHA256.HashData(sourceBytes)).ToLowerInvariant();
                if (!string.Equals(sourceHash, plan.ContentSha256, StringComparison.OrdinalIgnoreCase))
                    return new(false, false, false, [new(HashMismatchCode,
                        "Voice source changed while applying the plan.", "sourceRelativePath")]);

                var directory = Path.GetDirectoryName(destination)!;
                Directory.CreateDirectory(directory);
                var rechecked = RuntimeModuleAssetPathResolver.ResolveVoiceAudioDestination(workspaceRoot,
                    canonicalPlan.DestinationRelativePath);
                if (!string.Equals(destination, rechecked, PathComparison()))
                    return new(false, false, false, [new(DestinationPathUnsafeCode,
                        "Voice destination changed after directory creation.", "destinationRelativePath")]);

                var temporary = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
                GovernedDialogueVoiceImportApplyResult result;
                try
                {
                    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        output.Write(sourceBytes);
                        output.Flush(flushToDisk: true);
                    }
                    if (!string.Equals(HashFile(temporary), plan.ContentSha256, StringComparison.OrdinalIgnoreCase))
                        result = new(false, false, false, [new(HashMismatchCode,
                            "Copied voice bytes do not match the planned SHA-256.", "destinationRelativePath")]);
                    else if (File.Exists(destination))
                    {
                        result = string.Equals(HashFile(destination), plan.ContentSha256,
                                StringComparison.OrdinalIgnoreCase)
                            ? new(true, false, true, [])
                            : new(false, false, false, [new(CollisionCode,
                                "Voice destination changed before atomic publication.", "destinationRelativePath")]);
                    }
                    else
                    {
                        File.Move(temporary, destination, overwrite: false);
                        result = new(true, true, false, []);
                    }
                }
                catch (Exception exception) when (IsFileSystemFailure(exception))
                {
                    result = new(false, false, false, [new(ApplyFailedCode,
                        $"Voice import could not be applied: {exception.GetType().Name}.", "destinationRelativePath")]);
                }

                if (File.Exists(temporary))
                {
                    try
                    {
                        File.Delete(temporary);
                    }
                    catch (Exception exception) when (IsFileSystemFailure(exception))
                    {
                        return new(false, result.Created, result.Reused,
                            result.Diagnostics.Append(new DomainDiagnostic(CleanupFailedCode,
                                $"Voice import staging cleanup failed: {exception.GetType().Name}.",
                                "destinationRelativePath")).ToArray());
                    }
                }
                return result;
            }
        }
        catch (Exception exception) when (IsFileSystemFailure(exception))
        {
            return new(false, false, false, [new(ApplyFailedCode,
                $"Voice import could not be applied: {exception.GetType().Name}.", "destinationRelativePath")]);
        }
    }

    public static GovernedDialogueVoiceImportRevertResult Revert(
        string workspaceRoot,
        GovernedDialogueVoiceImportPlan plan,
        GovernedDialogueVoiceImportApplyResult applied)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(applied);
        // Voice resources are immutable content-addressed blobs. Revert removes the cue through the
        // canonical inverse command and deliberately retains the blob; no path-based delete is safe
        // against a concurrent parent swap on every supported platform.
        return applied.Created
            ? new(true, [new(BlobRetainedCode,
                "Deletion was skipped by the immutable retention policy; no dialogue cue references this materialization after revert.",
                "destinationRelativePath")], DeletionSkipped: true)
            : new(true, [], DeletionSkipped: true);
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static bool ValidSpdx(string value) => SpdxLicenseValidator.IsSupported(value);

    private static StringComparison PathComparison() =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static bool IsFileSystemFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or CryptographicException or
            ArgumentException or NotSupportedException;
}
