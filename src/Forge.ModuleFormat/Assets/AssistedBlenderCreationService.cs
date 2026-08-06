using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Application.Assistant;
using Forge.Domain.Assets;
using Forge.Domain.Assistant;
using Forge.Domain.Projects;

namespace Forge.ModuleFormat.Assets;

public sealed record AssistedBlenderCreationBrief(
    Guid CreationId,
    string Slug,
    string DisplayName,
    IReadOnlyList<BlenderPrimitiveDefinition> Primitives,
    BlenderMaterialDefinition Material,
    BlenderUvProjection UvProjection,
    IReadOnlyList<decimal> LodRatios,
    bool GenerateCollision,
    IReadOnlyList<string> ContextCitations,
    IReadOnlyList<string> StyleRules,
    string AuthorId,
    int Seed);

public sealed record AssistedBlenderCreationTestEvidence(string Id, bool Passed, string Detail);

public sealed record AssistedBlenderCreationProposal(
    bool Succeeded,
    AssistedBlenderCreationDefinition Definition,
    AssetExternalEditConfiguration Configuration,
    string RecipeRelativePath,
    string RecipeJson,
    string RecipeSha256,
    string BaseRecipeSha256,
    IReadOnlyList<AssistedBlenderCreationTestEvidence> Tests,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> Citations,
    IReadOnlyList<string> StyleRules,
    string SemanticDiff,
    string PromptSha256,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCostUsd,
    string ProviderKey,
    string Provenance,
    bool ApprovedForApplication)
{
    public bool CanApply => Succeeded && Diagnostics.Count == 0 && Tests.Count == 5 && Tests.All(item => item.Passed);

    public AssistedBlenderCreationProposal WithApproval(bool approved, string provenance) => this with
    {
        ApprovedForApplication = approved,
        Provenance = string.IsNullOrWhiteSpace(provenance) ? Provenance : provenance.Trim(),
    };
}

public static class AssistedBlenderCreationService
{
    public const string EmptyContentSha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    public static async Task<AssistedBlenderCreationProposal> ProposeAsync(
        AssistedBlenderCreationBrief brief,
        string workspaceRoot,
        ProjectDefinition project,
        LoreIndexDefinition lore,
        IAssistantProvider provider,
        AssistantProviderDefinition providerDefinition,
        bool networkAuthorized,
        bool projectContentAuthorized,
        CancellationToken cancellationToken = default)
    {
        ValidateBrief(brief);
        var lorePacket = LoreContextAssembler.Assemble(lore, project,
            $"{brief.DisplayName} {string.Join(' ', brief.StyleRules)}", 8);
        const string system = "Compose one bounded 3D asset from typed Blender primitives. Never return Python or mutate files.";
        var user = JsonSerializer.Serialize(new
        {
            brief.DisplayName,
            primitives = brief.Primitives.Select(item => new { item.Name, kind = item.Kind.ToString(), item.ScaleMeters }),
            brief.Material,
            uv = brief.UvProjection.ToString(),
            brief.LodRatios,
            brief.GenerateCollision,
            brief.StyleRules,
            brief.ContextCitations,
            lore = projectContentAuthorized ? lorePacket.Content : "[project content withheld]",
        });
        const string schema = "{\"type\":\"object\",\"required\":[\"primitives\",\"material\",\"uv\",\"lod\",\"collision\"]}";
        var promptHash = Sha256(string.Join('\n', system, user, schema));
        var response = await provider.GenerateAsync(new AssistantProviderRequest(
            brief.CreationId, Guid.NewGuid(),
            new AssistantPromptRenderResult("blender-primitive-creation", "1.0.0", system, user, schema, promptHash),
            AssistantPermissionLevel.Propose, Math.Min(2048, providerDefinition.MaximumOutputTokens),
            networkAuthorized, projectContentAuthorized), cancellationToken).ConfigureAwait(false);
        return Generate(brief, workspaceRoot, response.ProviderKey, response.EstimatedCost, response.Provenance,
            promptHash, response.InputTokens, response.OutputTokens);
    }

    public static AssistedBlenderCreationProposal Generate(
        AssistedBlenderCreationBrief brief,
        string workspaceRoot,
        string providerKey = "offline-ci",
        decimal estimatedCostUsd = 0,
        string provenance = "assistant:deterministic",
        string promptSha256 = "offline",
        int inputTokens = 0,
        int outputTokens = 0)
    {
        ValidateBrief(brief);
        var output = $"assets/generated/{brief.Slug}/model.glb";
        var definition = new AssistedBlenderCreationDefinition(1, brief.CreationId, brief.Slug.Trim(),
            brief.DisplayName.Trim(), provenance, "CC0-1.0", brief.Primitives,
            brief.Material, brief.UvProjection, brief.LodRatios.OrderDescending().ToArray(),
            brief.GenerateCollision, output);
        var diagnostics = AssistedBlenderCreationValidator.Validate(definition)
            .Select(item => item.Code).ToArray();
        var recipePath = $"assets/generated/{definition.Slug}/{definition.Slug}.recipe.json";
        var recipeJson = JsonSerializer.Serialize(definition, JsonOptions) + "\n";
        var recipeHash = Sha256(recipeJson);
        var absolute = ResolvePath(workspaceRoot, recipePath);
        var before = File.Exists(absolute) ? File.ReadAllText(absolute, Encoding.UTF8) : string.Empty;
        var assetId = AssetCatalogItemId.ForExternalId($"generated:{definition.Slug}");
        var operations = new List<AssetExternalOperation>
        {
            AssetExternalOperation.CreatePrimitives,
            AssetExternalOperation.ValidateModel,
            AssetExternalOperation.NormalizeModel,
            AssetExternalOperation.GenerateLods,
        };
        if (definition.GenerateCollision) operations.Add(AssetExternalOperation.GenerateCollision);
        operations.AddRange([
            AssetExternalOperation.ExportGlb,
            AssetExternalOperation.GenerateThumbnail,
            AssetExternalOperation.AuditQuality,
        ]);
        var configuration = new AssetExternalEditConfiguration(1, AssetExternalEditId.ForAsset(assetId), assetId,
            AssetPackageManifestId.ForAsset(assetId), $"{provenance}:typed-blender-creation", "CC0-1.0",
            recipeHash, recipePath, output, AssetExternalTool.Blender, operations, false,
            [AssetTargetPlatform.Windows, AssetTargetPlatform.Linux]);
        var configDiagnostics = AssetExternalEditValidator.Validate(configuration).Select(item => item.Code);
        diagnostics = [.. diagnostics, .. configDiagnostics];
        var tests = new[]
        {
            new AssistedBlenderCreationTestEvidence("recipe", diagnostics.Length == 0,
                $"schema:1|primitives:{definition.Primitives.Count}"),
            new("transforms", definition.Primitives.All(item => item.ScaleMeters.X > 0 && item.ScaleMeters.Y > 0 && item.ScaleMeters.Z > 0),
                "positions:bounded|rotations:bounded|scale:positive"),
            new("material-uv", definition.Material.Roughness is >= 0 and <= 1,
                $"material:{definition.Material.Name}|uv:{definition.UvProjection}"),
            new("lod-collision", definition.LodRatios.Count > 0,
                $"lod:{string.Join(',', definition.LodRatios)}|collision:{definition.GenerateCollision}"),
            new("worker-scope", configuration.Operations.All(item => item != AssetExternalOperation.EditImage &&
                                                                item != AssetExternalOperation.EditAudio),
                $"typed-operations:{configuration.Operations.Count}|python:false"),
        };
        var semanticDiff = $"add-generated-asset:{assetId}|recipe:{recipePath}|primitives:{definition.Primitives.Count}|" +
                           $"material:{definition.Material.Name}|uv:{definition.UvProjection}|lod:{definition.LodRatios.Count}|" +
                           $"collision:{definition.GenerateCollision}|output:{definition.OutputRelativePath}";
        return new(diagnostics.Length == 0 && tests.All(item => item.Passed), definition, configuration, recipePath,
            recipeJson, recipeHash, Sha256(before), tests, diagnostics, brief.ContextCitations.ToArray(),
            brief.StyleRules.ToArray(), semanticDiff, promptSha256, inputTokens, outputTokens, estimatedCostUsd,
            providerKey, provenance, false);
    }

    public static string ResolvePath(string workspaceRoot, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var normalized = relativePath.Replace('\\', '/');
        if (!AssetCatalogValidator.IsSafeRelativePath(normalized) ||
            !normalized.StartsWith("assets/generated/", StringComparison.Ordinal))
            throw new ArgumentException("Assisted Blender path must stay inside assets/generated.", nameof(relativePath));
        var root = Path.GetFullPath(workspaceRoot);
        var full = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Assisted Blender path escapes the workspace.", nameof(relativePath));
        return full;
    }

    public static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static void ValidateBrief(AssistedBlenderCreationBrief brief)
    {
        if (brief.CreationId == Guid.Empty || string.IsNullOrWhiteSpace(brief.Slug) ||
            string.IsNullOrWhiteSpace(brief.DisplayName) || brief.Primitives.Count == 0 ||
            brief.ContextCitations.Count == 0 || brief.StyleRules.Count == 0 ||
            string.IsNullOrWhiteSpace(brief.AuthorId))
            throw new ArgumentException("Assisted Blender identity, primitives, citations, style and author are required.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
