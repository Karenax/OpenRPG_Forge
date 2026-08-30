using Forge.Domain.Assets;

namespace Forge.Domain.Tests.Assets;

public sealed class AssetIntelligenceProfileValidatorTests
{
    [Fact]
    public void FallbackProfileIsVersionedGovernedAndRequiresReview()
    {
        var profile = AssetIntelligenceProfile.CreateUnclassified("fixture:crate", "Crate", "fixture");

        Assert.Empty(AssetIntelligenceProfileValidator.Validate(profile));
        Assert.Equal(AssetIntelligenceProfile.CurrentSchemaVersion, profile.SchemaVersion);
        Assert.Equal("generated-review-required", profile.ReviewStatus);
        Assert.Contains("Crate", profile.Description("en"));
    }

    [Fact]
    public void InvalidBoundsHashAndConfidenceAreRejected()
    {
        var fallback = AssetIntelligenceProfile.CreateUnclassified("fixture:crate", "Crate", "fixture");
        var invalid = fallback with
        {
            ModelContentSha256 = "invalid",
            Confidence = 2,
            Geometry = new AssetGeometryIntelligence(
                new AssetMetadataBounds(
                    new AssetMetadataVector3(2, 0, 0),
                    new AssetMetadataVector3(1, 1, 1)),
                new AssetMetadataVector3(-1, 1, 1),
                new AssetMetadataVector3(0, 0, 0),
                1,
                "y",
                "-z",
                "offset"),
        };

        var diagnostics = AssetIntelligenceProfileValidator.Validate(invalid);

        Assert.Contains(diagnostics, item => item.Path.EndsWith("/identity", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Path.EndsWith("/governance", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Path.EndsWith("/geometry/dimensionsMeters", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Path.EndsWith("/geometry/bounds", StringComparison.Ordinal));
    }

    [Fact]
    public void PlanarGeometryMayHaveAZeroThicknessAxis()
    {
        var fallback = AssetIntelligenceProfile.CreateUnclassified("fixture:decal", "Decal", "fixture");
        var planar = fallback with
        {
            Geometry = new AssetGeometryIntelligence(
                new AssetMetadataBounds(
                    new AssetMetadataVector3(-1, 0, -1),
                    new AssetMetadataVector3(1, 0, 1)),
                new AssetMetadataVector3(2, 0, 2),
                new AssetMetadataVector3(0, 0, 0),
                1,
                "y",
                "-z",
                "surface"),
        };

        Assert.Empty(AssetIntelligenceProfileValidator.Validate(planar));
    }
}
