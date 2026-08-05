using Forge.Domain.Assets;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Domain.Tests.Assets;

public sealed class ArtisticStyleProfileTests
{
    [Fact]
    public void AnalyzerReportsMetricTexelShaderEraRealismAndPaletteMismatches()
    {
        var moduleId = ModuleId.From(Guid.Parse("f3000000-0000-4000-8000-000000000001"));
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("f3000000-0000-4000-8000-000000000002")), "style-test", "Style test", []);
        var blueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("f3000000-0000-4000-8000-000000000003")),
            "styled-prop", "Styled prop", BlueprintKind.Placeable);
        var asset = new ArtisticAssetStyle(
            blueprint.Id, 2, 128, "unlit", "science-fiction", ArtisticRealism.Realistic,
            ["neon"], "test");
        var profile = new ArtisticStyleProfile(
            ArtisticStyleProfileId.From(moduleId.Value), 1, 512, 20, 50,
            "pbr-metallic-roughness", "original-fantasy", ArtisticRealism.StylizedRealism,
            ["earth", "stone"], [asset], "test");
        var module = new ModuleDefinition(moduleId, "style-module", "Style module", "", area.Id,
            artisticStyle: profile);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("f3000000-0000-4000-8000-000000000004")),
            "Style project", module, [blueprint], [area]);

        var diagnostics = ArtisticCompatibilityValidator.Analyze(project);

        Assert.Equal(6, diagnostics.Count);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == ArtisticCompatibilityValidator.ScaleMismatchCode);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == ArtisticCompatibilityValidator.TexelMismatchCode);
        Assert.All(ProjectDiagnosticCatalog.Build(project).Where(diagnostic =>
            diagnostic.Source == "ArtisticCompatibilityValidator"),
            diagnostic => Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity));
    }
}
