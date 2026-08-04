using Forge.Domain.Validation;

namespace Forge.Domain.Assets;

public static class AssetProcessingProfileValidator
{
    public const string SchemaUnsupportedCode = "asset.profile.schema_unsupported";
    public const string SourceFormatUnsupportedCode = "asset.profile.source_format_unsupported";
    public const string CanonicalFormatInvalidCode = "asset.profile.canonical_format_invalid";
    public const string GovernanceMissingCode = "asset.profile.governance_missing";
    public const string BudgetInvalidCode = "asset.profile.budget_invalid";
    public const string TextureOptionsInvalidCode = "asset.profile.texture_options_invalid";
    public const string ModelOptionsInvalidCode = "asset.profile.model_options_invalid";

    private static readonly IReadOnlySet<string> TextureFormats =
        new HashSet<string>(["png", "tif", "tiff", "exr", "ktx2"], StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> ModelFormats =
        new HashSet<string>(["gltf", "glb", "obj", "fbx"], StringComparer.Ordinal);

    public static IReadOnlyList<DomainDiagnostic> Validate(AssetProcessingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var root = $"assets/{profile.AssetId}/profile";
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.SchemaVersion != AssetProcessingProfile.CurrentSchemaVersion)
        {
            diagnostics.Add(new(SchemaUnsupportedCode, "Asset processing profile schema is unsupported.", $"{root}/schemaVersion"));
        }

        var supportedFormats = profile.Kind == AssetProcessingKind.Texture ? TextureFormats : ModelFormats;
        if (!supportedFormats.Contains(profile.SourceFormat))
        {
            diagnostics.Add(new(SourceFormatUnsupportedCode, $"Source format '{profile.SourceFormat}' is unsupported.", $"{root}/sourceFormat"));
        }

        var expectedCanonicalFormat = profile.Kind == AssetProcessingKind.Texture ? "ktx2" : "glb";
        if (!string.Equals(profile.CanonicalFormat, expectedCanonicalFormat, StringComparison.Ordinal))
        {
            diagnostics.Add(new(CanonicalFormatInvalidCode, $"Canonical format must be '{expectedCanonicalFormat}'.", $"{root}/canonicalFormat"));
        }

        if (string.IsNullOrWhiteSpace(profile.LicenseSpdx) ||
            string.IsNullOrWhiteSpace(profile.Provenance) ||
            !IsSha256(profile.ContentSha256))
        {
            diagnostics.Add(new(GovernanceMissingCode, "License, provenance and a lowercase SHA-256 hash are required.", $"{root}/governance"));
        }

        if (profile.MaximumOutputBytes <= 0)
        {
            diagnostics.Add(new(BudgetInvalidCode, "The platform output budget must be positive.", $"{root}/maximumOutputBytes"));
        }

        if (profile.Kind == AssetProcessingKind.Texture)
        {
            if (profile.Texture is null || profile.Model is not null ||
                profile.Texture.MaximumDimension is < 1 or > 16384 ||
                profile.Texture.TexelDensity <= 0 ||
                profile.Texture.Channels.Count == 0 ||
                profile.Texture.Channels.Values.Any(path =>
                    string.IsNullOrWhiteSpace(path) || !AssetCatalogValidator.IsSafeRelativePath(path)))
            {
                diagnostics.Add(new(TextureOptionsInvalidCode, "Texture channels, dimensions and texel density are invalid.", $"{root}/texture"));
            }
        }
        else if (profile.Model is null || profile.Texture is not null ||
                 profile.Model.LodRatios.Count == 0 ||
                 profile.Model.LodRatios[0] != 1m ||
                 profile.Model.LodRatios.Any(value => value is <= 0 or > 1) ||
                 profile.Model.LodRatios.Distinct().Count() != profile.Model.LodRatios.Count)
        {
            diagnostics.Add(new(ModelOptionsInvalidCode, "Model rig, collision, socket or LOD options are invalid.", $"{root}/model"));
        }

        return diagnostics;
    }

    private static bool IsSha256(string value) =>
        value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
