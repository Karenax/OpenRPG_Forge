using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Domain.Assets;

namespace Forge.ModuleFormat.Assets;

public static class BlenderWorkerRequestStore
{
    public const int CurrentSchemaVersion = 1;
    public const string WriteFailedCode = "asset.worker.request_write_failed";

    public static BlenderWorkerRequestSaveResult Save(
        string workspaceRoot,
        AssetExternalEditConfiguration configuration,
        BlenderWorkerOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(configuration);
        options ??= BlenderWorkerOptions.Default;
        var diagnostics = AssetExternalEditValidator.Validate(configuration);
        var optionDiagnostic = ValidateOptions(options);
        if (optionDiagnostic is not null)
        {
            return new(null, [optionDiagnostic]);
        }
        if (configuration.Tool != AssetExternalTool.Blender || diagnostics.Count > 0)
        {
            var message = configuration.Tool != AssetExternalTool.Blender
                ? "Blender worker requests require the Blender external tool."
                : diagnostics[0].Message;
            return new(null, [new(WriteFailedCode, message, configuration.OutputRelativePath)]);
        }
        var createsPrimitives = configuration.Operations.Contains(AssetExternalOperation.CreatePrimitives);
        if (createsPrimitives != (options.CreationRecipe is not null) ||
            options.CreationRecipe is not null &&
            !string.Equals(options.CreationRecipe.OutputRelativePath, configuration.OutputRelativePath,
                StringComparison.Ordinal))
            return new(null, [new(WriteFailedCode,
                "Typed primitive creation requires one matching creation recipe.", configuration.OutputRelativePath)]);

        var relativePath = $"assets/roundtrip/{configuration.AssetId}.worker-request.json";
        var path = Path.Combine(
            Path.GetFullPath(workspaceRoot),
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        try
        {
            var directory = Path.GetDirectoryName(path) ?? throw new IOException("Worker request path has no parent.");
            Directory.CreateDirectory(directory);
            var request = new BlenderWorkerRequest(
                CurrentSchemaVersion,
                configuration.Id.Value,
                configuration.AssetId.ToString(),
                configuration.SourceRelativePath,
                configuration.OutputRelativePath,
                configuration.SourceContentSha256,
                configuration.LicenseSpdx,
                configuration.ModificationProvenance,
                configuration.Operations.Select(ToWorkerOperation).ToArray(),
                options);
            WriteAtomically(path, JsonSerializer.Serialize(request, JsonOptions));
            return new(relativePath, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new(null, [new(WriteFailedCode, exception.Message, path)]);
        }
    }

    private static string ToWorkerOperation(AssetExternalOperation operation) => operation switch
    {
        AssetExternalOperation.ValidateModel => "validate_model",
        AssetExternalOperation.NormalizeModel => "normalize_model",
        AssetExternalOperation.GenerateCollision => "generate_collision",
        AssetExternalOperation.GenerateLods => "generate_lods",
        AssetExternalOperation.ExportGlb => "export_glb",
        AssetExternalOperation.GenerateThumbnail => "generate_thumbnail",
        AssetExternalOperation.AuditQuality => "audit_quality",
        AssetExternalOperation.CreatePrimitives => "create_primitives",
        _ => throw new JsonException($"Operation '{operation}' is unsupported by Blender worker."),
    };

    private static BlenderWorkerRequestDiagnostic? ValidateOptions(BlenderWorkerOptions options)
    {
        var animationSources = options.AnimationSources ?? [];
        if (animationSources.Select(source => source.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            animationSources.Count)
        {
            return new(WriteFailedCode, "Blender animation clip names must be unique.", "options.animationSources");
        }

        foreach (var source in animationSources)
        {
            if (!IsSafeName(source.Name) || !AssetCatalogValidator.IsSafeRelativePath(source.RelativePath) ||
                !IsSha256(source.ContentSha256))
            {
                return new(WriteFailedCode, "Blender animation sources require a safe name, confined path and SHA-256.",
                    source.RelativePath);
            }
        }

        foreach (var texture in options.MaterialTextures ?? [])
        {
            if (!IsSafeName(texture.Material) ||
                !string.Equals(texture.Channel, "base_color", StringComparison.Ordinal) ||
                !AssetCatalogValidator.IsSafeRelativePath(texture.RelativePath) ||
                !IsSha256(texture.ContentSha256))
            {
                return new(WriteFailedCode, "Blender material textures require a safe material, base_color channel, confined path and SHA-256.",
                    texture.RelativePath);
            }
        }

        if (options.CreationRecipe is not null)
        {
            var diagnostic = AssistedBlenderCreationValidator.Validate(options.CreationRecipe).FirstOrDefault();
            if (diagnostic is not null)
                return new(WriteFailedCode, diagnostic.Message, diagnostic.Path);
        }

        return null;
    }

    private static bool IsSafeName(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 64 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static void WriteAtomically(string targetPath, string content)
    {
        var directory = Path.GetDirectoryName(targetPath)!;
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Path.GetRandomFileName()}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private sealed record BlenderWorkerRequest(
        int SchemaVersion,
        Guid JobId,
        string AssetId,
        string SourceRelativePath,
        string OutputRelativePath,
        string SourceSha256,
        string LicenseSpdx,
        string Provenance,
        IReadOnlyList<string> Operations,
        BlenderWorkerOptions Options);
}

public sealed record BlenderWorkerOptions(
    bool ApplyTransforms,
    decimal ScaleMeters,
    string Pivot,
    IReadOnlyList<decimal> LodRatios,
    bool GenerateCollision,
    IReadOnlyList<BlenderWorkerAnimationSource>? AnimationSources = null,
    IReadOnlyList<BlenderWorkerMaterialTextureSource>? MaterialTextures = null,
    AssistedBlenderCreationDefinition? CreationRecipe = null)
{
    public static BlenderWorkerOptions Default { get; } =
        new(true, 1m, "ground_center", [0.5m, 0.25m], true, [], []);
}

public sealed record BlenderWorkerAnimationSource(string Name, string RelativePath, string ContentSha256);

public sealed record BlenderWorkerMaterialTextureSource(
    string Material,
    string Channel,
    string RelativePath,
    string ContentSha256);

public sealed record BlenderWorkerRequestDiagnostic(string Code, string Message, string Path);

public sealed record BlenderWorkerRequestSaveResult(
    string? RequestRelativePath,
    IReadOnlyList<BlenderWorkerRequestDiagnostic> Diagnostics)
{
    public bool Succeeded => RequestRelativePath is not null && Diagnostics.Count == 0;
}
