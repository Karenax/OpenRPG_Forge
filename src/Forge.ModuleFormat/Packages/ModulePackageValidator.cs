using System.IO.Compression;
using System.Text.Json;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Packages;

public static class ModulePackageValidator
{
    public const string ArchiveInvalidCode = "module.archive.invalid";
    public const string MissingManifestCode = "module.archive.manifest_missing";
    public const string DuplicatePathCode = "module.archive.path_duplicate";
    public const string UnsafePathCode = "module.archive.path_unsafe";
    public const string FileCountCode = "module.archive.file_count";
    public const string FileSizeCode = "module.archive.file_size";
    public const string TotalSizeCode = "module.archive.total_size";
    public const string CompressionRatioCode = "module.archive.compression_ratio";
    public const string ForbiddenFileCode = "module.archive.file_forbidden";
    public const string HashMismatchCode = "module.archive.hash_mismatch";
    public const string MissingProjectCode = "module.archive.project_missing";
    public const string InvalidProjectCode = "module.archive.project_invalid";
    public const string ManifestProjectMismatchCode = "module.archive.manifest_project_mismatch";
    public const string MissingNoticeCode = "module.archive.notice_missing";
    public const string NoticeMismatchCode = "module.archive.notice_mismatch";
    public const string DistributionComplianceCode = "module.archive.distribution_noncompliant";
    public const string DependencyApprovalMismatchCode = "module.archive.dependency_approval_mismatch";

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json", ".forge", ".as", ".asbc", ".glb", ".gltf", ".png", ".jpg", ".jpeg",
        ".webp", ".ogg", ".wav", ".md", ".txt",
    };

    public static ModulePackageValidationResult Validate(
        string packagePath,
        ModulePackageLimits? limits = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        limits ??= new ModulePackageLimits();
        var diagnostics = new List<ModulePackageDiagnostic>();
        ModuleManifest? manifest = null;
        Forge.Domain.Projects.ProjectDefinition? project = null;
        if (!File.Exists(packagePath) ||
            !string.Equals(Path.GetExtension(packagePath), ".orpmod", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new ModulePackageDiagnostic(
                ArchiveInvalidCode,
                "A module package must be an existing .orpmod archive.",
                packagePath));
            return new ModulePackageValidationResult(null, null, diagnostics);
        }

        try
        {
            using var archive = ZipFile.OpenRead(packagePath);
            var files = archive.Entries.Where(entry => !IsDirectory(entry)).ToArray();
            if (files.Length > limits.MaximumFileCount)
            {
                diagnostics.Add(new ModulePackageDiagnostic(
                    FileCountCode,
                    $"Archive contains {files.Length} files; maximum is {limits.MaximumFileCount}."));
            }

            var pathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long totalBytes = 0;
            foreach (var entry in files)
            {
                if (!IsSafeArchivePath(entry.FullName))
                {
                    diagnostics.Add(new ModulePackageDiagnostic(
                        UnsafePathCode,
                        $"Archive path '{entry.FullName}' is unsafe.",
                        entry.FullName));
                }

                if (!pathSet.Add(entry.FullName))
                {
                    diagnostics.Add(new ModulePackageDiagnostic(
                        DuplicatePathCode,
                        $"Archive path '{entry.FullName}' is duplicated.",
                        entry.FullName));
                }

                var extension = Path.GetExtension(entry.FullName);
                if (!AllowedExtensions.Contains(extension))
                {
                    diagnostics.Add(new ModulePackageDiagnostic(
                        ForbiddenFileCode,
                        $"File type '{extension}' is not allowed in community modules.",
                        entry.FullName));
                }

                if (entry.Length > limits.MaximumFileBytes)
                {
                    diagnostics.Add(new ModulePackageDiagnostic(
                        FileSizeCode,
                        $"File '{entry.FullName}' exceeds the per-file limit.",
                        entry.FullName));
                }

                totalBytes = checked(totalBytes + entry.Length);
                if (entry.Length > 0)
                {
                    var ratio = entry.CompressedLength == 0
                        ? double.PositiveInfinity
                        : (double)entry.Length / entry.CompressedLength;
                    if (ratio > limits.MaximumCompressionRatio)
                    {
                        diagnostics.Add(new ModulePackageDiagnostic(
                            CompressionRatioCode,
                            $"File '{entry.FullName}' has hostile compression ratio {ratio:F1}.",
                            entry.FullName));
                    }
                }
            }

            if (totalBytes > limits.MaximumTotalBytes)
            {
                diagnostics.Add(new ModulePackageDiagnostic(
                    TotalSizeCode,
                    $"Archive expands to {totalBytes} bytes; maximum is {limits.MaximumTotalBytes}."));
            }

            var manifestEntry = files.SingleOrDefault(entry =>
                string.Equals(entry.FullName, "manifest.json", StringComparison.Ordinal));
            if (manifestEntry is null)
            {
                diagnostics.Add(new ModulePackageDiagnostic(
                    MissingManifestCode,
                    "Archive does not contain manifest.json."));
            }

            if (diagnostics.Count > 0 || manifestEntry is null)
            {
                return new ModulePackageValidationResult(null, null, diagnostics);
            }

            manifest = ModuleManifestJson.Deserialize(ReadText(manifestEntry));
            diagnostics.AddRange(ModuleManifestValidator.Validate(manifest));
            var noticeEntry = files.SingleOrDefault(entry =>
                string.Equals(entry.FullName, manifest.LicenseNoticePath, StringComparison.Ordinal));
            if (noticeEntry is null)
            {
                diagnostics.Add(new ModulePackageDiagnostic(
                    MissingNoticeCode,
                    $"Archive does not contain '{manifest.LicenseNoticePath}'.",
                    manifest.LicenseNoticePath));
            }
            var actualHash = ModuleContentHash.Compute(files);
            if (!string.Equals(actualHash, manifest.ContentSha256, StringComparison.Ordinal))
            {
                diagnostics.Add(new ModulePackageDiagnostic(
                    HashMismatchCode,
                    $"Content hash mismatch; manifest={manifest.ContentSha256}, actual={actualHash}.",
                    "manifest.json/contentSha256"));
            }

            var projectEntry = files.SingleOrDefault(entry =>
                string.Equals(entry.FullName, "content/project.json", StringComparison.Ordinal));
            if (projectEntry is null)
            {
                diagnostics.Add(new ModulePackageDiagnostic(
                    MissingProjectCode,
                    "Archive does not contain content/project.json."));
            }
            else
            {
                try
                {
                    project = WorkspaceProjectJsonSerializer.Deserialize(
                        ReadText(projectEntry),
                        relativePath =>
                        {
                            var archivePath = $"content/{relativePath}";
                            var areaEntry = files.SingleOrDefault(entry =>
                                string.Equals(entry.FullName, archivePath, StringComparison.Ordinal));
                            if (areaEntry is null)
                            {
                                throw new JsonException($"Area document '{archivePath}' is missing from the archive.");
                            }

                            return ReadText(areaEntry);
                        });
                    if (project.Module.Id != manifest.ModuleId ||
                        !string.Equals(project.Module.Slug, manifest.Slug, StringComparison.Ordinal) ||
                        project.Module.EntryAreaId != manifest.EntryAreaId)
                    {
                        diagnostics.Add(new ModulePackageDiagnostic(
                            ManifestProjectMismatchCode,
                            "Manifest identity or entry area does not match content/project.json."));
                    }

                    foreach (var diagnostic in DistributionComplianceValidator.Validate(project, manifest.Target))
                    {
                        diagnostics.Add(new ModulePackageDiagnostic(
                            DistributionComplianceCode,
                            $"{diagnostic.Code}: {diagnostic.Message}",
                            diagnostic.Path));
                    }

                    var expectedApprovals = project.Module.Distribution.Dependencies
                        .Where(item => item.RequiredForTargets.Contains(manifest.Target))
                        .Select(item => new ModuleManifestDependency(
                            item.Id,
                            item.Name,
                            item.Version,
                            item.LicenseSpdx,
                            item.Owner,
                            item.SourceReference))
                        .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    if (!expectedApprovals.SequenceEqual(manifest.ThirdPartyDependencies))
                    {
                        diagnostics.Add(new ModulePackageDiagnostic(
                            DependencyApprovalMismatchCode,
                            "Manifest third-party approvals do not match the canonical project registry.",
                            "manifest.json/thirdPartyDependencies"));
                    }

                    if (noticeEntry is not null)
                    {
                        var expectedNotice = DistributionNoticeBuilder.Build(project, manifest.Target);
                        if (!string.Equals(ReadText(noticeEntry), expectedNotice, StringComparison.Ordinal))
                        {
                            diagnostics.Add(new ModulePackageDiagnostic(
                                NoticeMismatchCode,
                                "Distribution notice does not match canonical license and provenance metadata.",
                                manifest.LicenseNoticePath));
                        }
                    }
                }
                catch (Exception exception) when (
                    exception is JsonException or ArgumentException or NotSupportedException or ProjectValidationException)
                {
                    diagnostics.Add(new ModulePackageDiagnostic(
                        InvalidProjectCode,
                        $"Project document is invalid: {exception.Message}",
                        "content/project.json"));
                }
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or JsonException)
        {
            diagnostics.Add(new ModulePackageDiagnostic(
                ArchiveInvalidCode,
                $"Module archive is invalid: {exception.Message}",
                packagePath));
        }

        return new ModulePackageValidationResult(manifest, project, diagnostics);
    }

    public static bool IsSafeArchivePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            path.StartsWith("/", StringComparison.Ordinal) ||
            path.StartsWith('\\') ||
            path.Contains('\\') ||
            Path.IsPathFullyQualified(path))
        {
            return false;
        }

        var segments = path.Split('/');
        return segments.All(segment => segment.Length > 0 && segment is not "." and not "..");
    }

    private static bool IsDirectory(ZipArchiveEntry entry) =>
        entry.FullName.EndsWith("/", StringComparison.Ordinal);

    private static string ReadText(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }
}
