using Forge.Domain.Blueprints;
using Forge.Domain.Entities;

namespace Forge.Domain.Tests.Blueprints;

public sealed class BlueprintAuthoringMetadataTests
{
    [Fact]
    public void ProjectAndInstalledContractsExposeStablePackageIdentity()
    {
        var id = BlueprintId.From(Guid.Parse("b0300000-0000-4000-8000-000000000001"));
        var projectProvenance = BlueprintProvenance.CreateProjectDefault(id, "crypt-pillar");
        var project = BlueprintAuthoringMetadata.CreateDefault(
            id, "crypt-pillar", BlueprintPaletteOrigin.ProjectContent, projectProvenance);
        var installed = BlueprintAuthoringMetadata.CreateDefault(
            id,
            "crypt-pillar",
            BlueprintPaletteOrigin.InstalledPack,
            new BlueprintProvenance(1, "pack://forge-core/2.3.1/crypt-pillar", "Forge", "CC0-1.0", new string('a', 64)));

        Assert.Equal("1.0.0", project.Version);
        Assert.Equal("project.crypt.pillar", project.PackageNamespace);
        Assert.StartsWith("content/blueprints/", project.ManifestPath, StringComparison.Ordinal);
        Assert.Equal("2.3.1", installed.Version);
        Assert.Equal("forge.core", installed.PackageNamespace);
        Assert.Equal("pack://forge-core/2.3.1/manifest.json", installed.ManifestPath);
    }

    [Theory]
    [InlineData("*", "9.8.7", true)]
    [InlineData("=1.2.3", "1.2.3", true)]
    [InlineData("^1.2.0", "1.9.0", true)]
    [InlineData("~1.2.0", "1.3.0", false)]
    public void VersionConstraintsResolveDeterministically(string requirement, string version, bool expected)
    {
        Assert.True(BlueprintVersionConstraint.IsValid(requirement));
        Assert.Equal(expected, BlueprintVersionConstraint.Matches(requirement, version));
        var entity = new EntityInstance(EntityId.From(Guid.NewGuid()), BlueprintId.From(Guid.NewGuid()),
            Forge.Domain.Spatial.TransformValue.Identity, blueprintVersionRequirement: requirement);
        Assert.Equal(requirement, entity.BlueprintVersionRequirement);
    }

    [Fact]
    public void InvalidAuthoringPathsAndRangesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new BlueprintAuthoringMetadata(
            1, "1.0.0", "project.valid", "../escape.json"));
        Assert.Throws<ArgumentException>(() => new EntityInstance(
            EntityId.From(Guid.NewGuid()), BlueprintId.From(Guid.NewGuid()),
            Forge.Domain.Spatial.TransformValue.Identity, blueprintVersionRequirement: "latest"));
    }

    [Fact]
    public void ContextualPaletteMetadataIsNormalizedAndParticipatesInIdentity()
    {
        var metadata = new BlueprintAuthoringMetadata(
            1, "2.0.0", "project.crypt", "content/blueprints/crypt.json",
            platformByteBudget: 256_000, kit: "Crypt-Stone",
            styles: ["ruined", "Gothic", "gothic"],
            compatibility: ["humanoid-rig", "medieval"],
            tags: ["stone", "dungeon"]);
        var same = new BlueprintAuthoringMetadata(
            1, "2.0.0", "project.crypt", "content/blueprints/crypt.json",
            platformByteBudget: 256_000, kit: "crypt-stone",
            styles: ["Gothic", "gothic", "ruined"],
            compatibility: ["humanoid-rig", "medieval"],
            tags: ["dungeon", "stone"]);

        Assert.Equal("crypt-stone", metadata.Kit);
        Assert.Equal(["Gothic", "gothic", "ruined"], metadata.Styles);
        Assert.Equal(["dungeon", "stone"], metadata.Tags);
        Assert.Equal(same, metadata);
        Assert.Throws<ArgumentException>(() => new BlueprintAuthoringMetadata(
            1, "2.0.0", "project.crypt", "content/blueprints/crypt.json", kit: "bad kit"));
    }

    [Fact]
    public void HierarchicalCategoriesAndPromotedDefaultsAreCanonicalAndPersistent()
    {
        var promoted = new EntityPropertyOverride("visual.scale", EntityPropertyValueKind.Number, "2.0");
        var metadata = new BlueprintAuthoringMetadata(1, "1.2.0", "project.forest",
            "content/blueprints/tree.json", categoryPaths: ["Monde\\Forêt/Arbres", "Décors/Pierre"],
            promotedDefaults: [promoted]);

        Assert.Equal(["Décors/Pierre", "Monde/Forêt/Arbres"], metadata.CategoryPaths);
        Assert.Equal("2", Assert.Single(metadata.PromotedDefaults).Value);
        Assert.Throws<ArgumentException>(() => new BlueprintAuthoringMetadata(1, "1.0.0", "project.bad",
            "content/blueprints/bad.json", promotedDefaults: [promoted, promoted]));
    }
}
