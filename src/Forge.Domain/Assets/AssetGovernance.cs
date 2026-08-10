using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Validation;

namespace Forge.Domain.Assets;

public enum AssetOriginKind
{
    OpenLicensed,
    UserOwned,
    AiGenerated,
    ProprietaryNwn,
    ProprietaryCep,
    Unknown,
}

public enum AssetQuarantineStatus
{
    Pending,
    Approved,
    Rejected,
}

public sealed record AssetGovernanceId
{
    public Guid Value { get; }

    private AssetGovernanceId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("An asset governance ID cannot be empty.", nameof(value));
        Value = value;
    }

    public static AssetGovernanceId From(Guid value) => new(value);

    public static AssetGovernanceId ForAsset(AssetCatalogItemId assetId)
    {
        ArgumentNullException.ThrowIfNull(assetId);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:asset-governance:{assetId}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return From(new Guid(bytes[..16]));
    }

    public override string ToString() => Value.ToString("D");
}

public sealed record AssetQuarantineReview(
    string SourceRelativePath,
    string DeclaredMediaType,
    string DetectedMediaType,
    long Bytes,
    long MaximumBytes,
    string ScannerAdapter,
    string ScannerResult,
    string ParserResult,
    AssetQuarantineStatus Status,
    string Reviewer,
    DateTimeOffset? ReviewedAtUtc);

public sealed record AssetIpBoundary(
    AssetOriginKind Origin,
    bool RedistributionAllowed,
    bool CompatibilityIsolated,
    string LegalReviewReference);

public sealed record AssetGovernanceRecord(
    int SchemaVersion,
    AssetGovernanceId Id,
    AssetCatalogItemId AssetId,
    AssetPackageManifestId PackageManifestId,
    string LicenseSpdx,
    string ContentSha256,
    string Author,
    string SourceUrl,
    string PermissionEvidence,
    string Modifications,
    bool AiGenerated,
    string AiModel,
    string AiPromptPolicySha256,
    long PlatformBudgetBytes,
    AssetQuarantineReview Quarantine,
    AssetIpBoundary IpBoundary)
{
    public const int CurrentSchemaVersion = 1;
}

public static class AssetGovernanceValidator
{
    public const string SchemaInvalidCode = "asset.governance.schema_invalid";
    public const string IdentityInvalidCode = "asset.governance.identity_invalid";
    public const string LicenseMissingCode = "asset.governance.license_missing";
    public const string HashInvalidCode = "asset.governance.hash_invalid";
    public const string ProvenanceIncompleteCode = "asset.governance.provenance_incomplete";
    public const string AiDisclosureIncompleteCode = "asset.governance.ai_disclosure_incomplete";
    public const string BudgetInvalidCode = "asset.governance.budget_invalid";
    public const string QuarantineUnsafePathCode = "asset.quarantine.path_unsafe";
    public const string QuarantineTypeMismatchCode = "asset.quarantine.type_mismatch";
    public const string QuarantineSizeExceededCode = "asset.quarantine.size_exceeded";
    public const string QuarantineScanFailedCode = "asset.quarantine.scan_failed";
    public const string QuarantineParserFailedCode = "asset.quarantine.parser_failed";
    public const string QuarantineApprovalRequiredCode = "asset.quarantine.approval_required";
    public const string ProprietaryRedistributionCode = "asset.ip.proprietary_redistribution";
    public const string CompatibilityIsolationRequiredCode = "asset.ip.compatibility_isolation_required";
    public const string LegalReviewRequiredCode = "asset.ip.legal_review_required";
    public const string OriginUnknownCode = "asset.ip.origin_unknown";

    public static IReadOnlyList<DomainDiagnostic> Validate(AssetGovernanceRecord record, bool requireDistributionReady = true)
    {
        ArgumentNullException.ThrowIfNull(record);
        var path = $"assets/governance/{record.AssetId}";
        var diagnostics = new List<DomainDiagnostic>();
        if (record.SchemaVersion != AssetGovernanceRecord.CurrentSchemaVersion)
            diagnostics.Add(new(SchemaInvalidCode, "The asset governance schema is not supported.", $"{path}/schemaVersion"));
        if (record.Id != AssetGovernanceId.ForAsset(record.AssetId) ||
            record.PackageManifestId != AssetPackageManifestId.ForAsset(record.AssetId))
            diagnostics.Add(new(IdentityInvalidCode, "Governance and package UUIDs must be derived from the asset UUID.", $"{path}/id"));
        if (!DistributionComplianceValidator.IsApprovedLicense(record.LicenseSpdx))
            diagnostics.Add(new(LicenseMissingCode, "A recognized SPDX license is required.", $"{path}/licenseSpdx"));
        if (!IsSha256(record.ContentSha256))
            diagnostics.Add(new(HashInvalidCode, "A lowercase SHA-256 content hash is required.", $"{path}/contentSha256"));
        if (string.IsNullOrWhiteSpace(record.Author) || !IsAbsoluteHttpUrl(record.SourceUrl) ||
            string.IsNullOrWhiteSpace(record.PermissionEvidence) || string.IsNullOrWhiteSpace(record.Modifications))
            diagnostics.Add(new(ProvenanceIncompleteCode, "Author, source URL, permission evidence and modifications are required.", $"{path}/provenance"));
        if (record.AiGenerated && (string.IsNullOrWhiteSpace(record.AiModel) || !IsSha256(record.AiPromptPolicySha256)))
            diagnostics.Add(new(AiDisclosureIncompleteCode, "AI assets require a model and prompt-policy SHA-256.", $"{path}/ai"));
        if (record.PlatformBudgetBytes <= 0)
            diagnostics.Add(new(BudgetInvalidCode, "The platform byte budget must be positive.", $"{path}/platformBudgetBytes"));

        var quarantine = record.Quarantine;
        if (!IsSafeRelativePath(quarantine.SourceRelativePath))
            diagnostics.Add(new(QuarantineUnsafePathCode, "The quarantine source path must be safe and relative.", $"{path}/quarantine/sourceRelativePath"));
        if (string.IsNullOrWhiteSpace(quarantine.DeclaredMediaType) ||
            !string.Equals(quarantine.DeclaredMediaType, quarantine.DetectedMediaType, StringComparison.OrdinalIgnoreCase))
            diagnostics.Add(new(QuarantineTypeMismatchCode, "Declared and detected media types must match.", $"{path}/quarantine/detectedMediaType"));
        if (quarantine.Bytes < 0 || quarantine.MaximumBytes <= 0 || quarantine.Bytes > quarantine.MaximumBytes)
            diagnostics.Add(new(QuarantineSizeExceededCode, "The quarantined file exceeds its declared size boundary.", $"{path}/quarantine/bytes"));
        if (requireDistributionReady && (string.IsNullOrWhiteSpace(quarantine.ScannerAdapter) ||
            !string.Equals(quarantine.ScannerResult, "clean", StringComparison.OrdinalIgnoreCase)))
            diagnostics.Add(new(QuarantineScanFailedCode, "A named scanner adapter must report clean.", $"{path}/quarantine/scannerResult"));
        if (requireDistributionReady && !string.Equals(quarantine.ParserResult, "valid", StringComparison.OrdinalIgnoreCase))
            diagnostics.Add(new(QuarantineParserFailedCode, "The hardened parser must report valid.", $"{path}/quarantine/parserResult"));
        if (requireDistributionReady && (quarantine.Status != AssetQuarantineStatus.Approved || string.IsNullOrWhiteSpace(quarantine.Reviewer) ||
            quarantine.ReviewedAtUtc is null))
            diagnostics.Add(new(QuarantineApprovalRequiredCode, "Explicit human approval is required before canonical import.", $"{path}/quarantine/status"));

        var ip = record.IpBoundary;
        if (requireDistributionReady && ip.Origin == AssetOriginKind.Unknown)
            diagnostics.Add(new(OriginUnknownCode, "The asset origin must be classified.", $"{path}/ipBoundary/origin"));
        if (requireDistributionReady && ip.Origin is (AssetOriginKind.ProprietaryNwn or AssetOriginKind.ProprietaryCep))
        {
            if (ip.RedistributionAllowed)
                diagnostics.Add(new(ProprietaryRedistributionCode, "NWN and CEP proprietary resources must never be redistributed.", $"{path}/ipBoundary/redistributionAllowed"));
            if (!ip.CompatibilityIsolated)
                diagnostics.Add(new(CompatibilityIsolationRequiredCode, "Compatibility work must remain isolated.", $"{path}/ipBoundary/compatibilityIsolated"));
            if (string.IsNullOrWhiteSpace(ip.LegalReviewReference))
                diagnostics.Add(new(LegalReviewRequiredCode, "A legal-review reference is required for proprietary compatibility work.", $"{path}/ipBoundary/legalReviewReference"));
            diagnostics.Add(new(ProprietaryRedistributionCode, "NWN and CEP proprietary resources are excluded from distributable modules.", $"{path}/ipBoundary/origin"));
        }
        else if (requireDistributionReady && !ip.RedistributionAllowed)
            diagnostics.Add(new(ProprietaryRedistributionCode, "This asset is not approved for redistribution.", $"{path}/ipBoundary/redistributionAllowed"));

        return diagnostics;
    }

    private static bool IsSha256(string value) => value.Length == 64 &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsAbsoluteHttpUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https";

    public static bool IsSafeRelativePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value)) return false;
        var normalized = value.Replace('\\', '/');
        return normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).All(segment => segment is not "." and not "..");
    }
}

public sealed record AssetGovernanceResolveResult(
    AssetGovernanceRecord? Record,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class AssetGovernanceResolver
{
    public const string RequiredMissingCode = "asset.governance.required_missing";

    public static AssetGovernanceResolveResult Resolve(
        IReadOnlyList<AssetGovernanceRecord> records,
        AssetCatalogItemId assetId,
        bool required)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(assetId);
        var record = records.FirstOrDefault(item => item.AssetId == assetId);
        return record is not null || !required
            ? new(record, [])
            : new(null, [new(RequiredMissingCode, "Required asset governance metadata is absent.", $"assets/governance/{assetId}")]);
    }
}
