using System.Text.Json;
using Forge.Application.WorldGeneration;
using Forge.Domain.WorldGeneration;

namespace Forge.ModuleFormat.Serialization;

internal static class WorldGenerationRecipeJsonSerializer
{
    internal const string CurrentFormatVersion = "1.0.0";
    private static readonly JsonSerializerOptions Options = ProjectJsonSerializer.CreateOptions();

    internal static string Serialize(
        WorldGenerationRecipeCatalog catalog,
        WorldGenerationRecipeRevision revision)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        EnsureValid(catalog);
        if (revision.Value < 0) throw new ArgumentOutOfRangeException(nameof(revision));
        return JsonSerializer.Serialize(
            new WorldGenerationRecipeDocument(CurrentFormatVersion, revision.Value, catalog), Options);
    }

    internal static WorldGenerationRecipePersistenceState Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var document = JsonSerializer.Deserialize<WorldGenerationRecipeDocument>(json, Options)
            ?? throw new JsonException("The world-generation recipe document is empty.");
        if (!string.Equals(document.FormatVersion, CurrentFormatVersion, StringComparison.Ordinal))
            throw new NotSupportedException(
                $"World-generation recipe format '{document.FormatVersion}' is unsupported; expected '{CurrentFormatVersion}'.");
        if (document.Catalog is null)
            throw new JsonException("The world-generation recipe catalog is missing.");
        if (document.Revision < 0)
            throw new JsonException("The world-generation recipe revision cannot be negative.");
        EnsureValid(document.Catalog);
        return new(document.Catalog, new(document.Revision));
    }

    private static void EnsureValid(WorldGenerationRecipeCatalog catalog)
    {
        if (catalog.Id == Guid.Empty)
            throw new JsonException("The world-generation recipe catalog ID cannot be empty.");
        if (catalog.SchemaVersion != WorldGenerationRecipeCatalog.CurrentSchemaVersion)
            throw new JsonException($"World-generation catalog schema '{catalog.SchemaVersion}' is unsupported.");
        if (catalog.Recipes.Select(item => item.Id).Distinct().Count() != catalog.Recipes.Count)
            throw new JsonException("World-generation recipe IDs must be unique.");
        foreach (var recipe in catalog.Recipes)
        {
            var diagnostics = WorldGenerationRecipeValidator.Validate(recipe);
            if (diagnostics.Count > 0)
                throw new JsonException(
                    $"World-generation recipe is invalid: {diagnostics[0].Code} — {diagnostics[0].Message}");
        }
    }

    private sealed record WorldGenerationRecipeDocument(
        string FormatVersion,
        long Revision,
        WorldGenerationRecipeCatalog? Catalog);
}

internal sealed record WorldGenerationRecipePersistenceState(
    WorldGenerationRecipeCatalog Catalog,
    WorldGenerationRecipeRevision Revision);
