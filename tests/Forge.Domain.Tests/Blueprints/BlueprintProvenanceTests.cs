using Forge.Domain.Blueprints;

namespace Forge.Domain.Tests.Blueprints;

public sealed class BlueprintProvenanceTests
{
    [Fact]
    public void ProvenanceNormalizesHashAndDeterministicallyOrdersModifications()
    {
        var provenance = new BlueprintProvenance(
            BlueprintProvenance.CurrentSchemaVersion,
            "generator://terrain/cliff",
            "Forge Generator",
            "CC0-1.0",
            new string('A', 64),
            true,
            ["Z pass", "A pass", "A pass"],
            "contract:asset-grant-42");

        Assert.Equal(new string('a', 64), provenance.ContentHash);
        Assert.Equal(["A pass", "Z pass"], provenance.Modifications);
        Assert.True(provenance.IsAiGenerated);
        Assert.Equal("contract:asset-grant-42", provenance.PermissionEvidence);
    }

    [Fact]
    public void BlueprintDefaultsToEditableProjectContentWithCanonicalProvenance()
    {
        var id = BlueprintId.From(Guid.Parse("b3000000-0000-4000-8000-000000000001"));
        var blueprint = new BlueprintDefinition(id, "camp-fire", "Camp fire", BlueprintKind.Placeable);

        Assert.Equal(BlueprintPaletteOrigin.ProjectContent, blueprint.PaletteOrigin);
        Assert.False(blueprint.IsReadOnly());
        Assert.False(blueprint.IsFavorite);
        Assert.StartsWith($"project://blueprints/{id}/", blueprint.Provenance.Source, StringComparison.Ordinal);
        Assert.Equal(BlueprintProvenance.CurrentSchemaVersion, blueprint.Provenance.SchemaVersion);
        Assert.Equal("project-author", blueprint.Provenance.PermissionEvidence);
    }

    [Fact]
    public void CreditCatalogAggregatesStableAuthorLicenseAndPermissionEntries()
    {
        var first = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("b0310000-0000-4000-8000-000000000001")),
            "first-credit", "First credit", BlueprintKind.Placeable,
            provenance: new BlueprintProvenance(1, "https://assets.invalid/first", "Asset Author", "CC-BY-4.0",
                new string('a', 64), permissionEvidence: "contract:grant-1"));
        var second = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("b0310000-0000-4000-8000-000000000002")),
            "second-credit", "Second credit", BlueprintKind.Placeable,
            provenance: new BlueprintProvenance(1, "https://assets.invalid/second", "Asset Author", "CC-BY-4.0",
                new string('b', 64), true, permissionEvidence: "contract:grant-1"));

        var credit = Assert.Single(BlueprintCreditCatalog.Aggregate([second, first]));

        Assert.Equal("Asset Author", credit.Author);
        Assert.Equal("contract:grant-1", credit.PermissionEvidence);
        Assert.Equal([first.Id, second.Id], credit.BlueprintIds);
        Assert.True(credit.IncludesAiGeneratedContent);
    }
}
