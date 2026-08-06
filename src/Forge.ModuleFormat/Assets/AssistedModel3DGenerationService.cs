using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Application.Assets;
using Forge.Domain.Assets;

namespace Forge.ModuleFormat.Assets;

public sealed record AssistedModel3DGenerationBrief(
    Guid GenerationId,
    string Slug,
    string DisplayName,
    Model3DInputKind InputKind,
    string Prompt,
    string? ImageSourceRelativePath,
    Model3DProviderDefinition Provider,
    Model3DTopologyPolicy Topology,
    IReadOnlyList<string> ContextCitations,
    IReadOnlyList<string> StyleRules,
    string AuthorId,
    int Seed);

public sealed record AssistedModel3DGenerationTestEvidence(string Id, bool Passed, string Detail);

public sealed record AssistedModel3DGenerationProposal(
    bool Succeeded,
    Model3DGenerationReceipt Receipt,
    AssetExternalEditConfiguration Configuration,
    string SourceObj,
    string ReceiptJson,
    string ReceiptRelativePath,
    string BaseSourceSha256,
    string BaseReceiptSha256,
    IReadOnlyList<AssistedModel3DGenerationTestEvidence> Tests,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> Citations,
    IReadOnlyList<string> StyleRules,
    string SemanticDiff,
    decimal EstimatedCostUsd,
    bool ApprovedForApplication)
{
    public bool CanApply => Succeeded && ApprovedForApplication && Receipt.HumanAccepted &&
                            Diagnostics.Count == 0 && Tests.Count == 5 && Tests.All(item => item.Passed);

    public AssistedModel3DGenerationProposal WithApproval(bool approved, string provenance) =>
        AssistedModel3DGenerationService.SetApproval(this, approved, provenance);
}

public static class AssistedModel3DGenerationService
{
    public const int MaximumInputImageBytes = 16 * 1024 * 1024;
    public const string EmptyContentSha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    public static async Task<AssistedModel3DGenerationProposal> ProposeAsync(
        AssistedModel3DGenerationBrief brief,
        string workspaceRoot,
        IModel3DGenerationProvider provider,
        bool networkAuthorized,
        CancellationToken cancellationToken = default)
    {
        ValidateBrief(brief);
        ArgumentNullException.ThrowIfNull(provider);
        byte[]? image = null;
        string? imageHash = null;
        if (brief.InputKind == Model3DInputKind.Image)
        {
            var imagePath = ResolveInputImagePath(workspaceRoot, brief.ImageSourceRelativePath!);
            var info = new FileInfo(imagePath);
            if (!info.Exists || info.Length is < 1 or > MaximumInputImageBytes)
                throw new ArgumentException("Image-to-3D input must exist and stay below sixteen MiB.", nameof(brief));
            image = await File.ReadAllBytesAsync(imagePath, cancellationToken).ConfigureAwait(false);
            imageHash = Sha256(image);
        }
        var response = await provider.GenerateAsync(new Model3DProviderRequest(
            brief.GenerationId, brief.InputKind, brief.Prompt.Trim(), image, imageHash, brief.Seed,
            brief.Topology.MaximumSourceTriangles, networkAuthorized), cancellationToken).ConfigureAwait(false);
        return Generate(brief, workspaceRoot, response, imageHash);
    }

    public static AssistedModel3DGenerationProposal Generate(
        AssistedModel3DGenerationBrief brief,
        string workspaceRoot,
        Model3DProviderResponse response,
        string? imageSha256 = null)
    {
        ValidateBrief(brief);
        ArgumentNullException.ThrowIfNull(response);
        string sourceObj;
        try { sourceObj = new UTF8Encoding(false, true).GetString(response.ObjContent); }
        catch (DecoderFallbackException exception)
        {
            throw new Model3DProviderException(LoopbackModel3DGenerationProvider.ResponseInvalidCode,
                "3D provider OBJ payload must be valid UTF-8.", exception);
        }
        if (response.ProviderKey != brief.Provider.Key || response.ObjContent.Length > brief.Provider.MaximumArtifactBytes)
            throw new Model3DProviderException(LoopbackModel3DGenerationProvider.ResponseInvalidCode,
                "3D provider identity or payload limit does not match the selected provider.");

        var slug = brief.Slug.Trim();
        var sourcePath = $"assets/staging/ai-3d/{slug}/{slug}.obj";
        var receiptPath = $"assets/staging/ai-3d/{slug}/receipt.json";
        var outputPath = $"assets/generated/{slug}/model.glb";
        var sourceHash = Sha256(response.ObjContent);
        var receipt = new Model3DGenerationReceipt(
            Model3DGenerationReceipt.CurrentSchemaVersion, brief.GenerationId, slug, brief.DisplayName.Trim(),
            brief.InputKind, Sha256(brief.Prompt.Trim()), brief.ImageSourceRelativePath, imageSha256,
            brief.Provider, response.LicenseSpdx, response.Provenance, sourcePath, sourceHash,
            response.ObjContent.LongLength, response.TriangleCount, brief.Topology, false, null, outputPath,
            $"{response.Provenance}:blender-normalize");
        var diagnostics = Model3DGenerationValidator.ValidateReceipt(receipt, requireHumanAcceptance: false)
            .Select(item => item.Code).ToList();
        var assetId = AssetCatalogItemId.ForExternalId($"generated:{slug}");
        var operations = new List<AssetExternalOperation>
        {
            AssetExternalOperation.ValidateModel,
            AssetExternalOperation.NormalizeModel,
            AssetExternalOperation.GenerateLods,
        };
        if (brief.Topology.GenerateCollision) operations.Add(AssetExternalOperation.GenerateCollision);
        operations.AddRange([
            AssetExternalOperation.ExportGlb,
            AssetExternalOperation.GenerateThumbnail,
            AssetExternalOperation.AuditQuality,
        ]);
        var configuration = new AssetExternalEditConfiguration(
            1, AssetExternalEditId.ForAsset(assetId), assetId, AssetPackageManifestId.ForAsset(assetId),
            receipt.ModificationProvenance, receipt.LicenseSpdx, sourceHash, sourcePath, outputPath,
            AssetExternalTool.Blender, operations, false, [AssetTargetPlatform.Windows, AssetTargetPlatform.Linux]);
        diagnostics.AddRange(AssetExternalEditValidator.Validate(configuration).Select(item => item.Code));
        var receiptJson = SerializeReceipt(receipt);
        var beforeSource = ReadOptional(ResolvePath(workspaceRoot, sourcePath, "assets/staging/ai-3d/"));
        var beforeReceipt = ReadOptional(ResolvePath(workspaceRoot, receiptPath, "assets/staging/ai-3d/"));
        var tests = new[]
        {
            new AssistedModel3DGenerationTestEvidence("provider", diagnostics.Count == 0,
                $"provider:{brief.Provider.Key}|kind:{brief.Provider.Kind}|network:{brief.Provider.NetworkAllowed}"),
            new("license", response.LicenseSpdx == "CC0-1.0" && brief.Provider.AllowedLicenses.Contains(response.LicenseSpdx),
                $"license:{response.LicenseSpdx}|provenance:{response.Provenance}"),
            new("staging", sourceObj.Contains("v ", StringComparison.Ordinal) && sourceObj.Contains("f ", StringComparison.Ordinal),
                $"source:{sourcePath}|sha256:{sourceHash}|bytes:{response.ObjContent.Length}"),
            new("topology", response.TriangleCount is >= 4 && response.TriangleCount <= brief.Topology.MaximumSourceTriangles,
                $"triangles:{response.TriangleCount}/{brief.Topology.MaximumSourceTriangles}|lod:{string.Join(',', brief.Topology.LodRatios)}"),
            new("worker", configuration.Operations.Contains(AssetExternalOperation.NormalizeModel) &&
                          configuration.Operations.Contains(AssetExternalOperation.ExportGlb),
                $"blender:typed|operations:{configuration.Operations.Count}|python:false"),
        };
        var semanticDiff = $"stage-provider-model:{assetId}|provider:{brief.Provider.Key}|input:{brief.InputKind}|" +
                           $"source:{sourcePath}|triangles:{response.TriangleCount}|license:{response.LicenseSpdx}|" +
                           $"lod:{brief.Topology.LodRatios.Count}|collision:{brief.Topology.GenerateCollision}|output:{outputPath}";
        return new(diagnostics.Count == 0 && tests.All(item => item.Passed), receipt, configuration, sourceObj,
            receiptJson, receiptPath, Sha256(beforeSource ?? string.Empty), Sha256(beforeReceipt ?? string.Empty),
            tests, diagnostics, brief.ContextCitations.ToArray(), brief.StyleRules.ToArray(), semanticDiff,
            response.EstimatedCostUsd, false);
    }

    public static AssistedModel3DGenerationProposal SetApproval(
        AssistedModel3DGenerationProposal proposal,
        bool approved,
        string provenance)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        if (approved && string.IsNullOrWhiteSpace(provenance))
            throw new ArgumentException("Human acceptance provenance is required.", nameof(provenance));
        var receipt = proposal.Receipt with
        {
            HumanAccepted = approved,
            HumanAcceptanceProvenance = approved ? provenance.Trim() : null,
        };
        var diagnostics = Model3DGenerationValidator.ValidateReceipt(receipt, requireHumanAcceptance: approved)
            .Select(item => item.Code).ToArray();
        return proposal with
        {
            Receipt = receipt,
            ReceiptJson = SerializeReceipt(receipt),
            Diagnostics = diagnostics,
            Succeeded = diagnostics.Length == 0 && proposal.Tests.All(item => item.Passed),
            ApprovedForApplication = approved,
        };
    }

    public static string ResolvePath(string workspaceRoot, string relativePath, string requiredPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var normalized = relativePath.Replace('\\', '/');
        if (!AssetCatalogValidator.IsSafeRelativePath(normalized) ||
            !normalized.StartsWith(requiredPrefix, StringComparison.Ordinal))
            throw new ArgumentException($"Assisted 3D path must stay inside {requiredPrefix}.", nameof(relativePath));
        var root = Path.GetFullPath(workspaceRoot);
        var full = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Assisted 3D path escapes the workspace.", nameof(relativePath));
        return full;
    }

    public static string Sha256(string value) => Sha256(Encoding.UTF8.GetBytes(value));
    public static string Sha256(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static string ResolveInputImagePath(string workspaceRoot, string relativePath)
    {
        if (!AssetCatalogValidator.IsSafeRelativePath(relativePath))
            throw new ArgumentException("Image-to-3D input path must be safe and relative.", nameof(relativePath));
        var root = Path.GetFullPath(workspaceRoot);
        var path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Image-to-3D input escapes the workspace.", nameof(relativePath));
        return path;
    }

    private static void ValidateBrief(AssistedModel3DGenerationBrief brief)
    {
        ArgumentNullException.ThrowIfNull(brief);
        if (brief.GenerationId == Guid.Empty || string.IsNullOrWhiteSpace(brief.Slug) ||
            string.IsNullOrWhiteSpace(brief.DisplayName) || string.IsNullOrWhiteSpace(brief.Prompt) ||
            brief.ContextCitations.Count == 0 || brief.StyleRules.Count == 0 ||
            string.IsNullOrWhiteSpace(brief.AuthorId) ||
            brief.InputKind == Model3DInputKind.Image && string.IsNullOrWhiteSpace(brief.ImageSourceRelativePath))
            throw new ArgumentException("Assisted 3D identity, prompt, input, citations, style and author are required.");
        var diagnostic = Model3DGenerationValidator.ValidateProvider(brief.Provider).FirstOrDefault();
        if (diagnostic is not null) throw new ArgumentException(diagnostic.Message, nameof(brief));
    }

    internal static string SerializeReceipt(Model3DGenerationReceipt receipt) =>
        JsonSerializer.Serialize(receipt, JsonOptions) + "\n";

    private static string? ReadOptional(string path) => File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
