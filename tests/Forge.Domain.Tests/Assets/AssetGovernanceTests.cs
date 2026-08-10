using Forge.Domain.Assets;

namespace Forge.Domain.Tests.Assets;

public sealed class AssetGovernanceTests
{
    [Fact]
    public void CompleteApprovedRecordIsDistributionReady()
    {
        Assert.Empty(AssetGovernanceValidator.Validate(ValidRecord()));
    }

    [Fact]
    public void ProvenanceAiQuarantineAndIpFailuresHaveStablePathsAndCodes()
    {
        var record = ValidRecord() with
        {
            SourceUrl = "relative",
            PermissionEvidence = "",
            AiGenerated = true,
            AiModel = "",
            AiPromptPolicySha256 = "missing",
            Quarantine = ValidRecord().Quarantine with
            {
                SourceRelativePath = "../escape.glb",
                DetectedMediaType = "image/png",
                Bytes = 200,
                MaximumBytes = 100,
                ScannerResult = "malware",
                ParserResult = "invalid",
                Status = AssetQuarantineStatus.Pending,
            },
            IpBoundary = new(AssetOriginKind.ProprietaryNwn, true, false, string.Empty),
        };

        var diagnostics = AssetGovernanceValidator.Validate(record);

        Assert.Contains(diagnostics, item => item.Code == AssetGovernanceValidator.ProvenanceIncompleteCode);
        Assert.Contains(diagnostics, item => item.Code == AssetGovernanceValidator.AiDisclosureIncompleteCode);
        Assert.Contains(diagnostics, item => item.Code == AssetGovernanceValidator.QuarantineUnsafePathCode);
        Assert.Contains(diagnostics, item => item.Code == AssetGovernanceValidator.QuarantineTypeMismatchCode);
        Assert.Contains(diagnostics, item => item.Code == AssetGovernanceValidator.QuarantineSizeExceededCode);
        Assert.Contains(diagnostics, item => item.Code == AssetGovernanceValidator.QuarantineScanFailedCode);
        Assert.Contains(diagnostics, item => item.Code == AssetGovernanceValidator.QuarantineParserFailedCode);
        Assert.Contains(diagnostics, item => item.Code == AssetGovernanceValidator.QuarantineApprovalRequiredCode);
        Assert.Contains(diagnostics, item => item.Code == AssetGovernanceValidator.ProprietaryRedistributionCode);
        Assert.Contains(diagnostics, item => item.Path.EndsWith("/ipBoundary/legalReviewReference", StringComparison.Ordinal));
    }

    [Fact]
    public void OptionalMissingRuntimeGovernanceIsSafeButRequiredMissingIsDiagnosed()
    {
        var assetId = ValidRecord().AssetId;

        Assert.Empty(AssetGovernanceResolver.Resolve([], assetId, required: false).Diagnostics);
        Assert.Equal(AssetGovernanceResolver.RequiredMissingCode,
            Assert.Single(AssetGovernanceResolver.Resolve([], assetId, required: true).Diagnostics).Code);
    }

    internal static AssetGovernanceRecord ValidRecord()
    {
        var assetId = AssetCatalogItemId.ForExternalId("test:governed-model");
        return new(
            AssetGovernanceRecord.CurrentSchemaVersion,
            AssetGovernanceId.ForAsset(assetId),
            assetId,
            AssetPackageManifestId.ForAsset(assetId),
            "CC0-1.0",
            new string('a', 64),
            "OpenRPG test author",
            "https://example.invalid/assets/model",
            "permission:test-fixture",
            "Normalized axes and generated LODs.",
            false,
            string.Empty,
            string.Empty,
            8 * 1024 * 1024,
            new("incoming/model.glb", "model/gltf-binary", "model/gltf-binary", 128, 1024,
                "test-scanner", "clean", "valid", AssetQuarantineStatus.Approved, "reviewer",
                DateTimeOffset.Parse("2026-08-07T10:00:00Z")),
            new(AssetOriginKind.OpenLicensed, true, false, string.Empty));
    }
}
