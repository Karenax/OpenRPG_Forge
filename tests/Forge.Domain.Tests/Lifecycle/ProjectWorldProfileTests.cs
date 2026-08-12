using Forge.Domain.Lifecycle;
using Forge.Domain.Modules;

namespace Forge.Domain.Tests.Lifecycle;

public sealed class ProjectWorldProfileTests
{
    [Fact]
    public void R052ProfileValidatesNamespacedEventsClockAreasAndSafeRuntimeResolution()
    {
        var moduleId = Guid.Parse("52000000-0000-4000-8000-000000000001");
        var areaId = "52000000-0000-4000-8000-000000000010";
        var baseline = ProjectWorldProfile.CreateDefault(moduleId);
        var schema = new CustomEventSchema(
            Guid.Parse("52000000-0000-4000-8000-000000000002"),
            1,
            "author:r052",
            "crypt",
            "braziers_lit",
            2,
            new Dictionary<string, string> { ["count"] = "int" },
            false,
            "1.0",
            "OnBraziersLit",
            EventAuthorityScope.Server);
        var clock = baseline.Clock with
        {
            Provenance = "author:r052",
            Speed = 4,
            AreaTimeZoneOffsets = new Dictionary<string, int> { [areaId] = 60 },
        };
        var profile = new ProjectWorldProfile(moduleId, 1, "author:r052", true, true, [schema], clock);

        Assert.Empty(ProjectWorldProfileValidator.Validate(profile, moduleId, new HashSet<string> { areaId }));
        var resolution = ProjectWorldRuntimeResolver.Resolve(profile, moduleId, true);
        Assert.True(resolution.Succeeded);
        Assert.Equal(profile.Id, resolution.Handle!.Id);
    }

    [Fact]
    public void R052ProfileReportsStablePathsAndOptionalRequiredRuntimeBehavior()
    {
        var moduleId = Guid.Parse("52000000-0000-4000-8000-000000000001");
        var baseline = ProjectWorldProfile.CreateDefault(moduleId);
        var invalid = new ProjectWorldProfile(moduleId, 1, "author:r052", true, true,
        [
            new CustomEventSchema(Guid.Empty, 1, "author:r052", "bad space", "event", 1,
                new Dictionary<string, string> { ["value"] = "object" }, true,
                "1.0", "bad handler!", EventAuthorityScope.Local),
        ], baseline.Clock with
        {
            Speed = double.PositiveInfinity,
            AreaTimeZoneOffsets = new Dictionary<string, int> { ["missing-area"] = 60 },
        });

        var diagnostics = ProjectWorldProfileValidator.Validate(invalid, moduleId, new HashSet<string>());
        Assert.Contains(diagnostics, item => item.Code == ProjectWorldProfileValidator.EventIdentityInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == ProjectWorldProfileValidator.EventContractInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == ProjectWorldProfileValidator.ClockInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == ProjectWorldProfileValidator.AreaReferenceMissingCode &&
            item.Path.EndsWith("missing-area", StringComparison.Ordinal));
        Assert.True(ProjectWorldRuntimeResolver.Resolve(null, moduleId, false).Succeeded);
        Assert.Equal(ProjectWorldRuntimeResolver.RequiredMissingCode,
            ProjectWorldRuntimeResolver.Resolve(null, moduleId, true).Diagnostic!.Code);
    }

    [Fact]
    public void R052LocalizedPlayerDocumentationExposesEightBoundedPages()
    {
        var localization = new ProjectLocalizedMetadata("fr", "Module", "Résumé", "Synopsis", "Crédits",
            "Installation", "Règles", "Contrôles", "Licences", "Avertissements", "Compatibilité");
        var metadata = new ProjectMetadata(ProjectMetadataId.From(Guid.NewGuid()), "fr", [localization],
            "Auteur", "1.0.0", "CC0-1.0", null, [], "Tout public", "author:r052");

        var pages = ProjectDocumentationValidator.Pages(localization);
        Assert.Equal(8, pages.Count);
        Assert.Empty(ProjectDocumentationValidator.Validate(metadata));
        var oversized = new ProjectLocalizedMetadata("fr", "Module", "Résumé", "Synopsis", "Crédits",
            "Installation", "Règles", "Contrôles", "Licences",
            new string('x', ProjectDocumentationValidator.MaximumPageCharacters + 1), "Compatibilité");
        var invalid = new ProjectMetadata(metadata.Id, "fr", [oversized], metadata.Author, metadata.Version,
            metadata.License, null, [], metadata.Classification, metadata.Provenance);
        Assert.Equal(ProjectDocumentationValidator.PageTooLargeCode,
            Assert.Single(ProjectDocumentationValidator.Validate(invalid)).Code);
    }
}
