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
            ["Z pass", "A pass", "A pass"]);

        Assert.Equal(new string('a', 64), provenance.ContentHash);
        Assert.Equal(["A pass", "Z pass"], provenance.Modifications);
        Assert.True(provenance.IsAiGenerated);
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
    }
}
