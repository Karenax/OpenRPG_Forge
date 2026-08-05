using System.IO.Enumeration;
using System.Security.Cryptography;

namespace Forge.ModuleFormat.Content;

public static class ContentBatchTransferService
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumFiles = 10_000;
    public const long MaximumFileBytes = 512L * 1024 * 1024;
    public const long MaximumBatchBytes = 4L * 1024 * 1024 * 1024;

    public static ContentBatchTransferReport Plan(ContentBatchTransferRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Directory.Exists(request.SourceRoot))
        {
            return Invalid(request, $"content.batch.source_missing:{request.SourceRoot}");
        }

        var diagnostics = new List<string>();
        var entries = new List<ContentBatchTransferEntry>();
        long totalBytes = 0;
        foreach (var sourcePath in Directory.EnumerateFiles(request.SourceRoot, "*", SearchOption.AllDirectories)
                     .Order(StringComparer.OrdinalIgnoreCase))
        {
            var attributes = File.GetAttributes(sourcePath);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                diagnostics.Add($"content.batch.reparse_point_rejected:{sourcePath}");
                continue;
            }

            var relativePath = Path.GetRelativePath(request.SourceRoot, sourcePath).Replace('\\', '/');
            if (!IsSafeRelativePath(relativePath) || !Matches(request, relativePath))
            {
                continue;
            }

            var info = new FileInfo(sourcePath);
            if (info.Length > MaximumFileBytes)
            {
                diagnostics.Add($"content.batch.file_too_large:{relativePath}");
                continue;
            }

            totalBytes += info.Length;
            if (entries.Count >= MaximumFiles || totalBytes > MaximumBatchBytes)
            {
                diagnostics.Add("content.batch.budget_exceeded");
                break;
            }

            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath))).ToLowerInvariant();
            var targetPath = ResolveTarget(request, relativePath);
            var collisionPath = File.Exists(targetPath) ? targetPath : null;
            var status = collisionPath is null
                ? ContentBatchEntryStatus.Planned
                : request.CollisionPolicy switch
                {
                    Domain.Content.ContentBatchCollisionPolicy.Fail => ContentBatchEntryStatus.Failed,
                    Domain.Content.ContentBatchCollisionPolicy.Skip => ContentBatchEntryStatus.Skipped,
                    Domain.Content.ContentBatchCollisionPolicy.Replace => ContentBatchEntryStatus.Planned,
                    Domain.Content.ContentBatchCollisionPolicy.PreserveBoth => ContentBatchEntryStatus.Planned,
                    _ => ContentBatchEntryStatus.Failed,
                };
            if (collisionPath is not null && request.CollisionPolicy == Domain.Content.ContentBatchCollisionPolicy.Fail)
            {
                diagnostics.Add($"content.batch.collision:{relativePath}");
            }

            if (collisionPath is not null && request.CollisionPolicy == Domain.Content.ContentBatchCollisionPolicy.PreserveBoth)
            {
                targetPath = AppendHash(targetPath, hash);
            }

            entries.Add(new ContentBatchTransferEntry(
                sourcePath,
                targetPath,
                relativePath,
                hash,
                info.Length,
                request.Provenance,
                request.LicenseSpdx,
                status,
                collisionPath));
        }

        if (entries.Count == 0 && diagnostics.Count == 0)
        {
            diagnostics.Add("content.batch.no_matches");
        }

        return new ContentBatchTransferReport(
            CurrentSchemaVersion,
            request.Operation,
            request.DryRun,
            diagnostics.Count == 0,
            entries,
            diagnostics);
    }

    public static ContentBatchTransferReport Execute(ContentBatchTransferRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var plan = Plan(request);
        if (request.DryRun || !plan.Valid)
        {
            return plan;
        }

        var stagingRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "content-batch", Guid.NewGuid().ToString("N"));
        var stagedRoot = Path.Combine(stagingRoot, "staged");
        var backupRoot = Path.Combine(stagingRoot, "backup");
        var applied = new List<(string Target, string? Backup)>();
        try
        {
            Directory.CreateDirectory(stagedRoot);
            Directory.CreateDirectory(backupRoot);
            var completed = new List<ContentBatchTransferEntry>();
            foreach (var entry in plan.Entries)
            {
                if (entry.Status == ContentBatchEntryStatus.Skipped)
                {
                    completed.Add(entry);
                    continue;
                }

                var stagedPath = ResolveWithin(stagedRoot, entry.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(stagedPath)!);
                File.Copy(entry.SourcePath, stagedPath, overwrite: false);
                var stagedHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(stagedPath))).ToLowerInvariant();
                if (!string.Equals(stagedHash, entry.ContentSha256, StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"Staged content hash mismatch for '{entry.RelativePath}'.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(entry.TargetPath)!);
                string? backup = null;
                var replaced = File.Exists(entry.TargetPath);
                if (replaced)
                {
                    backup = ResolveWithin(backupRoot, entry.RelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    File.Copy(entry.TargetPath, backup, overwrite: false);
                }

                File.Copy(stagedPath, entry.TargetPath, overwrite: replaced);
                applied.Add((entry.TargetPath, backup));
                completed.Add(entry with
                {
                    Status = entry.CollisionPath is not null && request.CollisionPolicy == Domain.Content.ContentBatchCollisionPolicy.PreserveBoth
                        ? ContentBatchEntryStatus.PreservedBoth
                        : replaced ? ContentBatchEntryStatus.Replaced : ContentBatchEntryStatus.Added,
                });
            }

            return plan with { Entries = completed, Valid = true };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            foreach (var (target, backup) in applied.AsEnumerable().Reverse())
            {
                if (backup is not null && File.Exists(backup))
                {
                    File.Copy(backup, target, overwrite: true);
                }
                else if (File.Exists(target))
                {
                    File.Delete(target);
                }
            }

            return plan with
            {
                Valid = false,
                Diagnostics = [.. plan.Diagnostics, $"content.batch.execution_failed:{exception.Message}"],
            };
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
    }

    private static bool Matches(ContentBatchTransferRequest request, string relativePath)
    {
        var extension = Path.GetExtension(relativePath);
        if (!request.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (request.Glob is "*" or "**/*")
        {
            return true;
        }

        var expression = request.Glob.StartsWith("**/", StringComparison.Ordinal) ? request.Glob[3..] : request.Glob;
        return FileSystemName.MatchesSimpleExpression(expression, relativePath, ignoreCase: true) ||
               FileSystemName.MatchesSimpleExpression(expression, Path.GetFileName(relativePath), ignoreCase: true);
    }

    private static string ResolveTarget(ContentBatchTransferRequest request, string relativePath) =>
        ResolveWithin(request.DestinationRoot, relativePath);

    private static string ResolveWithin(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Content path '{relativePath}' escapes its root.");
        }

        return candidate;
    }

    private static bool IsSafeRelativePath(string path) =>
        !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) &&
        !path.Split('/').Any(segment => segment is "" or "." or "..");

    private static string AppendHash(string targetPath, string hash) =>
        Path.Combine(
            Path.GetDirectoryName(targetPath)!,
            $"{Path.GetFileNameWithoutExtension(targetPath)}~{hash[..8]}{Path.GetExtension(targetPath)}");

    private static ContentBatchTransferReport Invalid(ContentBatchTransferRequest request, string diagnostic) =>
        new(CurrentSchemaVersion, request.Operation, request.DryRun, false, [], [diagnostic]);
}
