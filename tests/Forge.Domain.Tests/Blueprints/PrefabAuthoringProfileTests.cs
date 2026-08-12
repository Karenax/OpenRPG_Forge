using Forge.Domain.Assets;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Domain.Tests.Blueprints;

public sealed class PrefabAuthoringProfileTests
{
    [Fact]
    public void ValidVariantCompositionAndPlatformBudgetPassCanonicalValidation()
    {
        var baseBlueprint = Blueprint("b0580000-0000-4000-8000-000000000001", "crypt-pillar");
        var variant = new PrefabVariantDefinition(Guid.Parse("b0580000-0000-4000-8000-000000000010"), 1,
            "author:r058", baseBlueprint.Id,
            [new(PrefabVariantOverrideKind.Material, "body", "asset:stone-moss"),
             new(PrefabVariantOverrideKind.Stat, "hitPoints", "80")], true);
        var composition = new PrefabCompositionDefinition(Guid.Parse("b0580000-0000-4000-8000-000000000011"),
            1, "author:r058",
            [new(Guid.Parse("b0580000-0000-4000-8000-000000000012"), baseBlueprint.Id, null, "floor",
                TransformValue.Identity)], ["socket-compatible"], ["height"], ["scripts/assemble_pillar.as"], true);
        var budget = new BlueprintAssetBudgetProfile(Guid.Parse("b0580000-0000-4000-8000-000000000013"), 1,
            "asset-audit:r058", new(12_000, 2, 4, 8_000_000, 1, 3, 2),
            [new(AssetTargetPlatform.Windows, 20_000, 4, 8, 16_000_000, 2, 4, 4)], true);
        var target = Blueprint("b0580000-0000-4000-8000-000000000002", "crypt-pillar-moss",
            new BlueprintAuthoringMetadata(1, "1.0.0", "project.crypt.pillar.moss",
                "content/blueprints/crypt-pillar-moss.json", authorCollections: ["Crypt/Architecture"],
                prefabVariant: variant, prefabComposition: composition, assetBudget: budget));
        var project = new ProjectDefinition(ProjectId.From(Guid.NewGuid()), "R058", [baseBlueprint, target], [Area()]);

        Assert.Empty(BlueprintPrefabValidator.Validate(project, target));
        Assert.Equal(["Crypt/Architecture"], target.Authoring.AuthorCollections);
    }

    [Fact]
    public void InvalidReferencesHierarchyAndGateBudgetExposeStableLocalizedDiagnostics()
    {
        var baseBlueprint = Blueprint("b0580000-0000-4000-8000-000000000021", "base");
        var first = Guid.Parse("b0580000-0000-4000-8000-000000000022");
        var second = Guid.Parse("b0580000-0000-4000-8000-000000000023");
        var authoring = new BlueprintAuthoringMetadata(1, "1.0.0", "project.invalid",
            "content/blueprints/invalid.json",
            prefabVariant: new(Guid.NewGuid(), 1, "author:r058", BlueprintId.From(Guid.NewGuid()),
                [new(PrefabVariantOverrideKind.Component, "", "")]),
            prefabComposition: new(Guid.NewGuid(), 1, "author:r058",
                [new(first, baseBlueprint.Id, second, null, TransformValue.Identity),
                 new(second, baseBlueprint.Id, first, null, TransformValue.Identity)], [], [], []),
            assetBudget: new(Guid.NewGuid(), 1, "audit:r058", new(200, 2, 2, 200, 2, 2, 2),
                [new(AssetTargetPlatform.Windows, 100, 1, 1, 100, 1, 1, 1)]));
        var invalid = Blueprint("b0580000-0000-4000-8000-000000000024", "invalid", authoring);
        var project = new ProjectDefinition(ProjectId.From(Guid.NewGuid()), "R058 invalid", [baseBlueprint, invalid], [Area()]);
        var diagnostics = BlueprintPrefabValidator.Validate(project, invalid);

        Assert.Contains(diagnostics, item => item.Code == BlueprintPrefabValidator.VariantReferenceCode);
        Assert.Contains(diagnostics, item => item.Code == BlueprintPrefabValidator.VariantOverrideCode);
        Assert.Contains(diagnostics, item => item.Code == BlueprintPrefabValidator.CompositionHierarchyCode);
        Assert.Contains(diagnostics, item => item.Code == BlueprintPrefabValidator.AssetBudgetExceededCode &&
                                             item.Path.Contains("Windows", StringComparison.Ordinal));
    }

    private static BlueprintDefinition Blueprint(string id, string slug, BlueprintAuthoringMetadata? authoring = null) =>
        new(BlueprintId.From(Guid.Parse(id)), slug, slug, BlueprintKind.Placeable, authoring: authoring);

    private static AreaDefinition Area() => new(AreaId.From(Guid.NewGuid()), "r058", "R058", []);
}
