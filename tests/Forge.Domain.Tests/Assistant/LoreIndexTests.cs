using Forge.Domain.Assistant;

namespace Forge.Domain.Tests.Assistant;

public sealed class LoreIndexTests
{
    [Fact]
    public void DefaultIndexIsValidAndEmpty()
    {
        var index = LoreIndexDefinition.CreateDefault();

        Assert.Empty(LoreIndexValidator.Validate(index));
        Assert.True(index.Enabled);
        Assert.Empty(index.Entries);
        Assert.Equal(12_000, index.MaximumContextCharacters);
    }

    [Fact]
    public void EntryIdentitySourcesAndRelationshipsAreValidated()
    {
        var firstId = Guid.Parse("b1230000-0000-4000-8000-000000000010");
        var secondId = Guid.Parse("b1230000-0000-4000-8000-000000000011");
        var index = new LoreIndexDefinition(
            Guid.Parse("b1230000-0000-4000-8000-000000000001"),
            1,
            true,
            8_000,
            [
                Entry(firstId, "ember-oath", ["manual:world-bible"], [secondId]),
                Entry(secondId, "cinder-wardens", ["manual:factions"], []),
            ],
            "test:g12-b");

        Assert.Empty(LoreIndexValidator.Validate(index));

        var invalid = new LoreIndexDefinition(
            index.Id,
            1,
            true,
            8_000,
            [Entry(firstId, "bad key", ["file:c:/secret"], [Guid.NewGuid()])],
            "test:g12-b");
        var diagnostics = LoreIndexValidator.Validate(invalid);

        Assert.Contains(diagnostics, item => item.Code == LoreIndexValidator.EntryInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == LoreIndexValidator.SourceInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == LoreIndexValidator.ReferenceInvalidCode);
    }

    private static LoreEntryDefinition Entry(
        Guid id,
        string key,
        IReadOnlyList<string> sources,
        IReadOnlyList<Guid> related) =>
        new(
            id,
            1,
            LoreEntryKind.WorldBible,
            key,
            key,
            "Une vérité canonique sourcée pour le monde.",
            sources,
            related,
            ["canon"],
            "test:g12-b");
}
