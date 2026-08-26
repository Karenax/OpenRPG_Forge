namespace Forge.Domain.WorldGeneration;

public sealed record WorldGenerationRecipeCatalog
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public IReadOnlyList<WorldGenerationRecipe> Recipes { get; }
    public string Provenance { get; }

    public WorldGenerationRecipeCatalog(
        Guid id,
        int schemaVersion,
        IReadOnlyList<WorldGenerationRecipe>? recipes,
        string provenance)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        Recipes = (recipes ?? []).OrderBy(item => item.Title, StringComparer.Ordinal).ThenBy(item => item.Id).ToArray();
        Provenance = (provenance ?? string.Empty).Trim();
    }

    public WorldGenerationRecipe? Find(Guid recipeId) => Recipes.FirstOrDefault(item => item.Id == recipeId);

    public static WorldGenerationRecipeCatalog CreateDefault() => new(
        Guid.Parse("e0870000-0000-4000-8000-000000000001"),
        CurrentSchemaVersion,
        [],
        "migration-default:world-generation-v1");
}
