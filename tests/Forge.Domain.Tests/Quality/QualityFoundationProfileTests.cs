using Forge.Domain.Quality;

namespace Forge.Domain.Tests.Quality;

public sealed class QualityFoundationProfileTests
{
    [Fact]
    public void DefaultProfileHasStableIdentityAndCompleteP0Coverage()
    {
        var owner = Guid.Parse("30000000-0000-4000-8000-000000000001");

        var first = QualityFoundationProfile.CreateDefault(owner);
        var second = QualityFoundationProfile.CreateDefault(owner);

        Assert.Equal(first.Id, second.Id);
        Assert.Empty(QualityFoundationProfileValidator.Validate(first));
        Assert.Equal(7, first.Tests.DomainAreas.Count);
        Assert.True(first.Licenses.BlockUnknowns);
        Assert.True(first.Saves.DeterministicState);
        Assert.Equal(PlayerPrivacyMode.Redact, first.Logs.PlayerPrivacy);
        Assert.Equal(5, first.EffectiveValidation.Categories.Count);
        Assert.All(first.EffectiveValidation.Categories.Values, category => Assert.True(category.Enabled));
        Assert.True(first.EffectiveValidation.Schemas.RequiredBeforeBuild);
        Assert.False(first.EffectiveValidation.References.RequiredBeforeBuild);
        Assert.False(first.EffectiveValidation.Graphs.RequiredBeforeBuild);
        Assert.False(first.EffectiveValidation.Geometry.RequiredBeforeBuild);
        Assert.False(first.EffectiveValidation.Navigation.RequiredBeforeBuild);
    }

    [Fact]
    public void IncompletePoliciesReturnStableDiagnostics()
    {
        var valid = QualityFoundationProfile.CreateDefault(Guid.NewGuid());
        var hostile = valid with
        {
            Id = Guid.Empty,
            Provenance = string.Empty,
            SchemaVersion = 99,
            Licenses = valid.Licenses with { BlockUnknowns = false, ApprovedSpdxIdentifiers = ["Unknown"] },
            Tests = valid.Tests with { DomainAreas = ["ids"], ReportRelativePath = "../escape" },
            Saves = valid.Saves with { Rollback = false },
            Logs = valid.Logs with { Categories = [], RetentionDays = 0 },
            Validation = valid.EffectiveValidation with
            {
                Schemas = valid.EffectiveValidation.Schemas with { Enabled = false },
                StructuredMcp = false,
            },
        };

        var codes = QualityFoundationProfileValidator.Validate(hostile).Select(item => item.Code).ToArray();

        Assert.Contains(QualityFoundationProfileValidator.IdentityInvalidCode, codes);
        Assert.Contains(QualityFoundationProfileValidator.SchemaInvalidCode, codes);
        Assert.Contains(QualityFoundationProfileValidator.LicensePolicyInvalidCode, codes);
        Assert.Contains(QualityFoundationProfileValidator.TestInventoryInvalidCode, codes);
        Assert.Contains(QualityFoundationProfileValidator.SaveCoverageInvalidCode, codes);
        Assert.Contains(QualityFoundationProfileValidator.LogPolicyInvalidCode, codes);
        Assert.Contains(QualityFoundationProfileValidator.ValidationPolicyInvalidCode, codes);
    }
}
