using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Validation;

namespace Forge.Domain.Assets;

public sealed record AssetExternalEditId
{
    public Guid Value { get; }

    private AssetExternalEditId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An external edit ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public static AssetExternalEditId From(Guid value) => new(value);

    public static AssetExternalEditId Parse(string value) => From(Guid.Parse(value));

    public static AssetExternalEditId ForAsset(AssetCatalogItemId assetId)
    {
        ArgumentNullException.ThrowIfNull(assetId);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:external-edit:{assetId}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return From(new Guid(bytes[..16]));
    }

    public override string ToString() => Value.ToString("D");
}

public enum AssetExternalTool
{
    Blender,
    ImageEditor,
    AudioEditor,
}

public enum AssetExternalOperation
{
    ValidateModel,
    NormalizeModel,
    GenerateCollision,
    GenerateLods,
    ExportGlb,
    EditImage,
    EditAudio,
    GenerateThumbnail,
    AuditQuality,
    CreatePrimitives,
}

public sealed record AssetExternalEditConfiguration(
    int SchemaVersion,
    AssetExternalEditId Id,
    AssetCatalogItemId AssetId,
    AssetPackageManifestId ManifestId,
    string ModificationProvenance,
    string LicenseSpdx,
    string SourceContentSha256,
    string SourceRelativePath,
    string OutputRelativePath,
    AssetExternalTool Tool,
    IReadOnlyList<AssetExternalOperation> Operations,
    bool WatchForChanges,
    IReadOnlyList<AssetTargetPlatform> TargetPlatforms)
{
    public const int CurrentSchemaVersion = 1;
}

public static class AssetExternalEditValidator
{
    public const string SchemaUnsupportedCode = "asset.external_edit.schema_unsupported";
    public const string GovernanceMissingCode = "asset.external_edit.governance_missing";
    public const string HashInvalidCode = "asset.external_edit.hash_invalid";
    public const string PathUnsafeCode = "asset.external_edit.path_unsafe";
    public const string OperationInvalidCode = "asset.external_edit.operation_invalid";
    public const string PlatformMissingCode = "asset.external_edit.platform_missing";

    public static IReadOnlyList<DomainDiagnostic> Validate(AssetExternalEditConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var path = $"assets/external-edits/{configuration.Id}";
        var diagnostics = new List<DomainDiagnostic>();
        if (configuration.SchemaVersion != AssetExternalEditConfiguration.CurrentSchemaVersion)
        {
            diagnostics.Add(new(SchemaUnsupportedCode, "Unsupported external edit schema.", $"{path}/schemaVersion"));
        }

        if (string.IsNullOrWhiteSpace(configuration.ModificationProvenance) ||
            string.IsNullOrWhiteSpace(configuration.LicenseSpdx))
        {
            diagnostics.Add(new(
                GovernanceMissingCode,
                "External edit requires modification provenance and an SPDX license.",
                $"{path}/governance"));
        }

        if (!IsSha256(configuration.SourceContentSha256))
        {
            diagnostics.Add(new(HashInvalidCode, "External edit source hash is invalid.", $"{path}/sourceContentSha256"));
        }

        if (!AssetCatalogValidator.IsSafeRelativePath(configuration.SourceRelativePath) ||
            !AssetCatalogValidator.IsSafeRelativePath(configuration.OutputRelativePath))
        {
            diagnostics.Add(new(PathUnsafeCode, "External edit paths must stay relative and confined.", $"{path}/paths"));
        }

        var allowed = configuration.Tool switch
        {
            AssetExternalTool.Blender => new HashSet<AssetExternalOperation>
            {
                AssetExternalOperation.ValidateModel,
                AssetExternalOperation.NormalizeModel,
                AssetExternalOperation.GenerateCollision,
                AssetExternalOperation.GenerateLods,
                AssetExternalOperation.ExportGlb,
                AssetExternalOperation.GenerateThumbnail,
                AssetExternalOperation.AuditQuality,
                AssetExternalOperation.CreatePrimitives,
            },
            AssetExternalTool.ImageEditor => [AssetExternalOperation.EditImage],
            AssetExternalTool.AudioEditor => [AssetExternalOperation.EditAudio],
            _ => [],
        };
        if (configuration.Operations.Count == 0 ||
            configuration.Operations.Distinct().Count() != configuration.Operations.Count ||
            configuration.Operations.Any(operation => !allowed.Contains(operation)))
        {
            diagnostics.Add(new(
                OperationInvalidCode,
                "External edit operations must be unique and allowed for the selected tool.",
                $"{path}/operations"));
        }

        if (configuration.TargetPlatforms.Count == 0 ||
            configuration.TargetPlatforms.Distinct().Count() != configuration.TargetPlatforms.Count)
        {
            diagnostics.Add(new(
                PlatformMissingCode,
                "External edit requires unique target platforms.",
                $"{path}/targetPlatforms"));
        }

        return diagnostics;
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character => Uri.IsHexDigit(character));
}

public static class AssetPlatformParityValidator
{
    public const string RequiredRoleMissingCode = "asset.platform.required_role_missing";

    public static IReadOnlyList<DomainDiagnostic> Validate(
        AssetPackageManifest manifest,
        IReadOnlyList<AssetTargetPlatform> targetPlatforms)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(targetPlatforms);
        var diagnostics = new List<DomainDiagnostic>();
        var requiredRoles = manifest.Chunks.Where(chunk => chunk.Required).Select(chunk => chunk.Role).Distinct();
        foreach (var platform in targetPlatforms.Distinct().OrderBy(value => value))
        {
            foreach (var role in requiredRoles)
            {
                if (!manifest.Chunks.Any(chunk =>
                        chunk.Required && chunk.Role == role &&
                        (chunk.Platforms.Contains(platform) || chunk.Platforms.Contains(AssetTargetPlatform.Any))))
                {
                    diagnostics.Add(new(
                        RequiredRoleMissingCode,
                        $"Required role '{role}' has no compatible chunk for {platform}.",
                        $"assets/manifests/{manifest.Id}/platforms/{platform}/{role}"));
                }
            }
        }

        return diagnostics;
    }
}
