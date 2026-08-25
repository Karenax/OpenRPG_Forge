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
        ModulePackageLimits? limits = null,
        string? availableRuntimeVersion = null)
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
            var allowedGltfBinarySidecars = CollectAllowedGltfBinarySidecars(files, limits.MaximumFileBytes);
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
                if (!AllowedExtensions.Contains(extension) &&
                    !(extension.Equals(".bin", StringComparison.OrdinalIgnoreCase) &&
                      allowedGltfBinarySidecars.Contains(entry.FullName)))
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
            if (!string.IsNullOrWhiteSpace(availableRuntimeVersion))
            {
                var compatibility = ModuleRuntimeCompatibility.Evaluate(manifest, availableRuntimeVersion);
                if (compatibility is not null) diagnostics.Add(compatibility);
            }
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
                            item.SourceReference,
                            item.ApprovalState,
                            item.RiskLevel,
                            item.ApproverRoles,
                            item.RequiredApprovals,
                            item.SecurityAuditReference))
                        .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    if (expectedApprovals.Length != manifest.ThirdPartyDependencies.Count ||
                        expectedApprovals.Zip(manifest.ThirdPartyDependencies)
                            .Any(pair => !EquivalentApproval(pair.First, pair.Second)))
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

    private static HashSet<string> CollectAllowedGltfBinarySidecars(
        IReadOnlyList<ZipArchiveEntry> files,
        long maximumFileBytes)
    {
        var sidecars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var maximumGltfBytes = Math.Min(maximumFileBytes, 16 * 1024 * 1024);
        foreach (var entry in files.Where(item =>
                     Path.GetExtension(item.FullName).Equals(".gltf", StringComparison.OrdinalIgnoreCase) &&
                     item.Length <= maximumGltfBytes))
        {
            try
            {
                using var stream = entry.Open();
                using var document = JsonDocument.Parse(stream);
                if (!document.RootElement.TryGetProperty("buffers", out var buffers) ||
                    buffers.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var buffer in buffers.EnumerateArray())
                {
                    if (buffer.ValueKind == JsonValueKind.Object &&
                        buffer.TryGetProperty("uri", out var uriElement) &&
                        uriElement.ValueKind == JsonValueKind.String &&
                        TryResolveGltfBinarySidecar(entry.FullName, uriElement.GetString(), out var sidecar))
                    {
                        sidecars.Add(sidecar);
                    }
                }
            }
            catch (JsonException)
            {
                // Invalid glTF documents are rejected by asset validation; they cannot authorize a sidecar here.
            }
        }

        return sidecars;
    }

    private static bool TryResolveGltfBinarySidecar(string gltfPath, string? rawUri, out string sidecar)
    {
        sidecar = string.Empty;
        if (string.IsNullOrWhiteSpace(rawUri)) return false;

        string uri;
        try
        {
            uri = Uri.UnescapeDataString(rawUri.Trim()).Replace('\\', '/');
        }
        catch (UriFormatException)
        {
            return false;
        }

        if (uri.Contains(':', StringComparison.Ordinal) || uri.Contains('?', StringComparison.Ordinal) ||
            uri.Contains('#', StringComparison.Ordinal) ||
            !Path.GetExtension(uri).Equals(".bin", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = uri.Split('/');
        if (segments.Any(segment => segment is "" or "." or "..")) return false;

        var normalizedGltfPath = gltfPath.Replace('\\', '/');
        var separator = normalizedGltfPath.LastIndexOf('/');
        sidecar = separator < 0 ? uri : $"{normalizedGltfPath[..(separator + 1)]}{uri}";
        return IsSafeArchivePath(sidecar);
    }

    private static bool EquivalentApproval(ModuleManifestDependency expected, ModuleManifestDependency actual) =>
        expected.Id == actual.Id &&
        string.Equals(expected.Name, actual.Name, StringComparison.Ordinal) &&
        string.Equals(expected.Version, actual.Version, StringComparison.Ordinal) &&
        string.Equals(expected.LicenseSpdx, actual.LicenseSpdx, StringComparison.Ordinal) &&
        string.Equals(expected.Owner, actual.Owner, StringComparison.Ordinal) &&
        string.Equals(expected.SourceReference, actual.SourceReference, StringComparison.Ordinal) &&
        expected.ApprovalState == actual.ApprovalState && expected.RiskLevel == actual.RiskLevel &&
        expected.RequiredApprovals == actual.RequiredApprovals &&
        string.Equals(expected.SecurityAuditReference, actual.SecurityAuditReference, StringComparison.Ordinal) &&
        expected.ApproverRoles.SequenceEqual(actual.ApproverRoles, StringComparer.OrdinalIgnoreCase);

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
