using System.Security.Cryptography;
using Forge.Domain.Assets;
using Forge.Domain.Validation;

namespace Forge.ModuleFormat.Assets;

public sealed record AssetMalwareScanResult(bool Clean, string ResultCode);

public interface IAssetMalwareScanner
{
    string AdapterName { get; }
    AssetMalwareScanResult Scan(string confinedFilePath);
}

public sealed record AssetQuarantineStageRequest(
    AssetCatalogItemId AssetId,
    string SourceRoot,
    string SourceRelativePath,
    string DeclaredMediaType,
    long MaximumBytes,
    string ExpectedSha256);

public sealed record AssetQuarantineStageResult(
    bool Succeeded,
    string? QuarantineRelativePath,
    string? ContentSha256,
    AssetQuarantineReview Review,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class AssetImportQuarantine
{
    public static AssetQuarantineStageResult Stage(
        string workspaceRoot,
        AssetQuarantineStageRequest request,
        IAssetMalwareScanner scanner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(scanner);
        var diagnostics = new List<DomainDiagnostic>();
        var detected = "application/octet-stream";
        var bytes = 0L;
        var scannerResult = "not-run";
        var parserResult = "not-run";
        string? quarantineRelative = null;
        string? hash = null;
        var path = $"assets/quarantine/{request.AssetId}";

        if (!AssetGovernanceValidator.IsSafeRelativePath(request.SourceRelativePath))
            diagnostics.Add(new(AssetGovernanceValidator.QuarantineUnsafePathCode,
                "The source path must be safe and relative.", $"{path}/sourceRelativePath"));
        if (request.MaximumBytes <= 0)
            diagnostics.Add(new(AssetGovernanceValidator.QuarantineSizeExceededCode,
                "The maximum byte count must be positive.", $"{path}/maximumBytes"));

        string? source = null;
        if (diagnostics.Count == 0)
        {
            var sourceRoot = Path.GetFullPath(request.SourceRoot);
            source = Path.GetFullPath(Path.Combine(sourceRoot,
                request.SourceRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsContained(sourceRoot, source) || !File.Exists(source) ||
                File.GetAttributes(source).HasFlag(FileAttributes.ReparsePoint))
                diagnostics.Add(new(AssetGovernanceValidator.QuarantineUnsafePathCode,
                    "The source is absent, outside its root or a reparse point.", $"{path}/sourceRelativePath"));
        }

        if (diagnostics.Count == 0 && source is not null)
        {
            bytes = new FileInfo(source).Length;
            if (bytes > request.MaximumBytes)
                diagnostics.Add(new(AssetGovernanceValidator.QuarantineSizeExceededCode,
                    $"The source has {bytes} bytes but the limit is {request.MaximumBytes}.", $"{path}/bytes"));
            detected = DetectMediaType(source);
            if (!string.Equals(request.DeclaredMediaType, detected, StringComparison.OrdinalIgnoreCase))
                diagnostics.Add(new(AssetGovernanceValidator.QuarantineTypeMismatchCode,
                    $"Declared '{request.DeclaredMediaType}', detected '{detected}'.", $"{path}/detectedMediaType"));
            hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant();
            if (!string.Equals(hash, request.ExpectedSha256, StringComparison.Ordinal))
                diagnostics.Add(new(AssetGovernanceValidator.HashInvalidCode,
                    "The source SHA-256 does not match the expected hash.", $"{path}/contentSha256"));
            parserResult = IsStructurallyValid(source, detected) ? "valid" : "invalid";
            if (parserResult == "invalid")
                diagnostics.Add(new(AssetGovernanceValidator.QuarantineParserFailedCode,
                    "The hardened format check rejected the source.", $"{path}/parserResult"));
        }

        if (diagnostics.Count == 0 && source is not null)
        {
            var root = Path.GetFullPath(workspaceRoot);
            var quarantineDirectory = Path.Combine(root, ".forge", "assets", "quarantine", request.AssetId.ToString());
            Directory.CreateDirectory(quarantineDirectory);
            var destination = Path.Combine(quarantineDirectory, Path.GetFileName(source));
            if (!IsContained(root, destination))
                throw new InvalidOperationException("asset.quarantine.destination_unsafe");
            File.Copy(source, destination, overwrite: true);
            quarantineRelative = Path.GetRelativePath(root, destination).Replace('\\', '/');
            var scan = scanner.Scan(destination);
            scannerResult = scan.Clean ? "clean" : scan.ResultCode;
            if (!scan.Clean)
            {
                File.Delete(destination);
                quarantineRelative = null;
                diagnostics.Add(new(AssetGovernanceValidator.QuarantineScanFailedCode,
                    $"Scanner '{scanner.AdapterName}' rejected the source: {scan.ResultCode}.", $"{path}/scannerResult"));
            }
        }

        var review = new AssetQuarantineReview(
            request.SourceRelativePath.Replace('\\', '/'), request.DeclaredMediaType, detected, bytes,
            request.MaximumBytes, scanner.AdapterName, scannerResult, parserResult,
            AssetQuarantineStatus.Pending, string.Empty, null);
        return new(diagnostics.Count == 0, quarantineRelative, hash, review, diagnostics);
    }

    public static AssetQuarantineReview Decide(
        AssetQuarantineReview review,
        bool approve,
        string reviewer,
        DateTimeOffset reviewedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(review);
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewer);
        if (approve && (!string.Equals(review.ScannerResult, "clean", StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(review.ParserResult, "valid", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("asset.quarantine.approval_unsafe");
        return review with
        {
            Status = approve ? AssetQuarantineStatus.Approved : AssetQuarantineStatus.Rejected,
            Reviewer = reviewer.Trim(),
            ReviewedAtUtc = reviewedAtUtc.ToUniversalTime(),
        };
    }

    private static bool IsContained(string root, string path)
    {
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string DetectMediaType(string path)
    {
        Span<byte> header = stackalloc byte[12];
        using var stream = File.OpenRead(path);
        var read = stream.Read(header);
        if (read >= 8 && header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (read >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff) return "image/jpeg";
        if (read >= 4 && header[..4].SequenceEqual("glTF"u8)) return "model/gltf-binary";
        if (string.Equals(Path.GetExtension(path), ".gltf", StringComparison.OrdinalIgnoreCase) && read > 0 && header[0] == (byte)'{')
            return "model/gltf+json";
        return "application/octet-stream";
    }

    private static bool IsStructurallyValid(string path, string mediaType) => mediaType switch
    {
        "image/png" => new FileInfo(path).Length >= 24,
        "image/jpeg" => new FileInfo(path).Length >= 4 && File.ReadAllBytes(path)[^2..].SequenceEqual(new byte[] { 0xff, 0xd9 }),
        "model/gltf-binary" => new FileInfo(path).Length >= 12,
        "model/gltf+json" => IsJsonObject(path),
        _ => false,
    };

    private static bool IsJsonObject(string path)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
