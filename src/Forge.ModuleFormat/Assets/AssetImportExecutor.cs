using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Domain.Assets;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Assets;

public static class AssetImportExecutor
{
    public const string PathUnsafeCode = "asset.import.execute_path_unsafe";
    public const string SourceMissingCode = "asset.import.execute_source_missing";
    public const string SourceInvalidCode = "asset.import.execute_source_invalid";
    public const string WriteFailedCode = "asset.import.execute_write_failed";

    private static readonly JsonSerializerOptions JsonOptions = ProjectJsonSerializer.CreateOptions();

    public static AssetImportExecutionResult Execute(
        AssetImportPlan plan,
        string vaultRoot,
        string workspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var diagnostics = new List<AssetImportExecutionDiagnostic>();
        if (!TryResolveContained(vaultRoot, plan.SourceRelativePath, out var sourcePath) ||
            !TryResolveContained(workspaceRoot, plan.DestinationRelativeDirectory, out var destinationDirectory))
        {
            diagnostics.Add(new(
                PathUnsafeCode,
                "Asset import source or destination escapes its configured root.",
                plan.DestinationRelativeDirectory));
            return new(null, diagnostics);
        }

        if (!File.Exists(sourcePath))
        {
            diagnostics.Add(new(SourceMissingCode, "Asset import source file is missing.", sourcePath));
            return new(null, diagnostics);
        }

        var sourceFormat = Path.GetExtension(sourcePath).TrimStart('.').ToLowerInvariant();
        if (!string.Equals(sourceFormat, plan.Profile.SourceFormat, StringComparison.Ordinal))
        {
            diagnostics.Add(new(
                SourceInvalidCode,
                $"Source extension '{sourceFormat}' does not match profile format '{plan.Profile.SourceFormat}'.",
                sourcePath));
            return new(null, diagnostics);
        }

        try
        {
            Directory.CreateDirectory(destinationDirectory);
            var sourceSha256 = HashFile(sourcePath);
            var canonicalReady = string.Equals(
                plan.Profile.SourceFormat,
                plan.Profile.CanonicalFormat,
                StringComparison.Ordinal);
            string outputRelativePath;
            string outputPath;
            if (canonicalReady)
            {
                var canonicalTask = plan.Tasks.FirstOrDefault(task =>
                    string.Equals(
                        Path.GetExtension(task.OutputRelativePath).TrimStart('.'),
                        plan.Profile.CanonicalFormat,
                        StringComparison.OrdinalIgnoreCase));
                if (canonicalTask is null ||
                    !TryResolveContained(workspaceRoot, canonicalTask.OutputRelativePath, out outputPath))
                {
                    diagnostics.Add(new(
                        PathUnsafeCode,
                        "Asset import plan does not contain a safe canonical output task.",
                        plan.DestinationRelativeDirectory));
                    return new(null, diagnostics);
                }

                outputRelativePath = canonicalTask.OutputRelativePath;
            }
            else
            {
                outputRelativePath = $"{plan.DestinationRelativeDirectory}/source/{Path.GetFileName(sourcePath)}";
                if (!TryResolveContained(workspaceRoot, outputRelativePath, out outputPath))
                {
                    diagnostics.Add(new(PathUnsafeCode, "Staging output path is unsafe.", outputRelativePath));
                    return new(null, diagnostics);
                }
            }

            var reused = File.Exists(outputPath) &&
                string.Equals(HashFile(outputPath), sourceSha256, StringComparison.Ordinal);
            if (!reused)
            {
                CopyAtomically(sourcePath, outputPath);
            }

            var receipt = new AssetImportReceipt(
                AssetImportReceipt.CurrentSchemaVersion,
                plan.AssetId,
                plan.CacheKey,
                plan.ContentSha256,
                sourceSha256,
                plan.LicenseSpdx,
                plan.Provenance,
                plan.SourceRelativePath,
                outputRelativePath,
                canonicalReady ? AssetImportExecutionStatus.CanonicalReady : AssetImportExecutionStatus.Staged,
                plan.Tasks);
            var receiptRelativePath = $"{plan.DestinationRelativeDirectory}/import-receipt.json";
            if (!TryResolveContained(workspaceRoot, receiptRelativePath, out var receiptPath))
            {
                diagnostics.Add(new(PathUnsafeCode, "Import receipt path is unsafe.", receiptRelativePath));
                return new(null, diagnostics);
            }

            WriteTextAtomically(receiptPath, JsonSerializer.Serialize(receipt, JsonOptions));
            return new(
                new AssetImportExecution(
                    receipt.Status,
                    outputRelativePath,
                    receiptRelativePath,
                    sourceSha256,
                    reused),
                []);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            diagnostics.Add(new(WriteFailedCode, exception.Message, plan.DestinationRelativeDirectory));
            return new(null, diagnostics);
        }
    }

    private static bool TryResolveContained(string root, string relativePath, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (!AssetCatalogValidator.IsSafeRelativePath(relativePath))
        {
            return false;
        }

        var absoluteRoot = Path.GetFullPath(root);
        var candidate = Path.GetFullPath(Path.Combine(
            absoluteRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(absoluteRoot, candidate);
        if (relative.Equals("..", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
        {
            return false;
        }

        resolvedPath = candidate;
        return true;
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void CopyAtomically(string sourcePath, string targetPath)
    {
        var directory = Path.GetDirectoryName(targetPath)
            ?? throw new IOException("Asset output path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Path.GetRandomFileName()}.tmp");
        try
        {
            using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var target = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       81920,
                       FileOptions.WriteThrough))
            {
                source.CopyTo(target);
                target.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void WriteTextAtomically(string targetPath, string content)
    {
        var directory = Path.GetDirectoryName(targetPath)
            ?? throw new IOException("Asset receipt path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Path.GetRandomFileName()}.tmp");
        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}

public enum AssetImportExecutionStatus
{
    Staged,
    CanonicalReady,
}

public sealed record AssetImportReceipt(
    int SchemaVersion,
    AssetCatalogItemId AssetId,
    string CacheKey,
    string CatalogContentSha256,
    string SourceFileSha256,
    string LicenseSpdx,
    string Provenance,
    string SourceRelativePath,
    string OutputRelativePath,
    AssetImportExecutionStatus Status,
    IReadOnlyList<AssetBuildTask> Tasks)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record AssetImportExecution(
    AssetImportExecutionStatus Status,
    string OutputRelativePath,
    string ReceiptRelativePath,
    string SourceFileSha256,
    bool Reused);

public sealed record AssetImportExecutionDiagnostic(string Code, string Message, string Path);

public sealed record AssetImportExecutionResult(
    AssetImportExecution? Execution,
    IReadOnlyList<AssetImportExecutionDiagnostic> Diagnostics)
{
    public bool Succeeded => Execution is not null && Diagnostics.Count == 0;
}
