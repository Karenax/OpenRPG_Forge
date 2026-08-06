using Forge.Domain.Validation;

namespace Forge.Domain.Assets;

public enum BlenderPrimitiveKind
{
    Cube,
    Cylinder,
    Sphere,
    Cone,
}

public enum BlenderUvProjection
{
    Generated,
    Cube,
    Cylinder,
    Sphere,
}

public sealed record BlenderVector3(decimal X, decimal Y, decimal Z);

public sealed record BlenderColor(decimal Red, decimal Green, decimal Blue, decimal Alpha);

public sealed record BlenderPrimitiveDefinition(
    Guid Id,
    string Name,
    BlenderPrimitiveKind Kind,
    BlenderVector3 PositionMeters,
    BlenderVector3 RotationDegrees,
    BlenderVector3 ScaleMeters);

public sealed record BlenderMaterialDefinition(
    string Name,
    BlenderColor BaseColor,
    decimal Metallic,
    decimal Roughness);

public sealed record AssistedBlenderCreationDefinition(
    int SchemaVersion,
    Guid Id,
    string Slug,
    string DisplayName,
    string Provenance,
    string LicenseSpdx,
    IReadOnlyList<BlenderPrimitiveDefinition> Primitives,
    BlenderMaterialDefinition Material,
    BlenderUvProjection UvProjection,
    IReadOnlyList<decimal> LodRatios,
    bool GenerateCollision,
    string OutputRelativePath)
{
    public const int CurrentSchemaVersion = 1;
}

public static class AssistedBlenderCreationValidator
{
    public const string IdentityInvalidCode = "assistant.blender.identity_invalid";
    public const string GovernanceInvalidCode = "assistant.blender.governance_invalid";
    public const string PrimitiveInvalidCode = "assistant.blender.primitive_invalid";
    public const string MaterialInvalidCode = "assistant.blender.material_invalid";
    public const string LodInvalidCode = "assistant.blender.lod_invalid";
    public const string PathUnsafeCode = "assistant.blender.path_unsafe";

    public static IReadOnlyList<DomainDiagnostic> Validate(AssistedBlenderCreationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var root = $"assets/assisted-3d/{definition.Id:D}";
        var diagnostics = new List<DomainDiagnostic>();
        if (definition.SchemaVersion != AssistedBlenderCreationDefinition.CurrentSchemaVersion ||
            definition.Id == Guid.Empty || !IsSlug(definition.Slug) ||
            string.IsNullOrWhiteSpace(definition.DisplayName))
            diagnostics.Add(new(IdentityInvalidCode, "Creation identity, schema, slug and name are required.", root));
        if (string.IsNullOrWhiteSpace(definition.Provenance) ||
            !string.Equals(definition.LicenseSpdx, "CC0-1.0", StringComparison.Ordinal))
            diagnostics.Add(new(GovernanceInvalidCode,
                "Generated core assets require explicit provenance and the CC0-1.0 license.", $"{root}/governance"));
        if (definition.Primitives.Count is < 1 or > 64 ||
            definition.Primitives.Select(item => item.Id).Distinct().Count() != definition.Primitives.Count ||
            definition.Primitives.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != definition.Primitives.Count ||
            definition.Primitives.Any(item => item.Id == Guid.Empty || !IsSafeName(item.Name) ||
                !ValidPosition(item.PositionMeters) || !ValidRotation(item.RotationDegrees) || !ValidScale(item.ScaleMeters)))
            diagnostics.Add(new(PrimitiveInvalidCode,
                "One to 64 uniquely identified bounded primitives are required.", $"{root}/primitives"));
        var color = definition.Material.BaseColor;
        if (!IsSafeName(definition.Material.Name) || !Between(color.Red, 0, 1) || !Between(color.Green, 0, 1) ||
            !Between(color.Blue, 0, 1) || !Between(color.Alpha, 0, 1) ||
            !Between(definition.Material.Metallic, 0, 1) || !Between(definition.Material.Roughness, 0, 1))
            diagnostics.Add(new(MaterialInvalidCode,
                "Material name, RGBA, metallic and roughness values must be bounded.", $"{root}/material"));
        if (definition.LodRatios.Count is < 1 or > 4 ||
            definition.LodRatios.Distinct().Count() != definition.LodRatios.Count ||
            definition.LodRatios.Any(ratio => !Between(ratio, 0.01m, 0.99m)))
            diagnostics.Add(new(LodInvalidCode, "One to four unique LOD ratios in [0.01, 0.99] are required.",
                $"{root}/lodRatios"));
        if (!AssetCatalogValidator.IsSafeRelativePath(definition.OutputRelativePath) ||
            !definition.OutputRelativePath.StartsWith("assets/generated/", StringComparison.Ordinal) ||
            !definition.OutputRelativePath.EndsWith("/model.glb", StringComparison.OrdinalIgnoreCase))
            diagnostics.Add(new(PathUnsafeCode, "Output must be a confined assets/generated/*/model.glb path.",
                $"{root}/outputRelativePath"));
        return diagnostics;
    }

    private static bool ValidPosition(BlenderVector3 value) =>
        Between(value.X, -1_000, 1_000) && Between(value.Y, -1_000, 1_000) && Between(value.Z, -1_000, 1_000);

    private static bool ValidRotation(BlenderVector3 value) =>
        Between(value.X, -360, 360) && Between(value.Y, -360, 360) && Between(value.Z, -360, 360);

    private static bool ValidScale(BlenderVector3 value) =>
        Between(value.X, 0.001m, 1_000) && Between(value.Y, 0.001m, 1_000) && Between(value.Z, 0.001m, 1_000);

    private static bool Between(decimal value, decimal minimum, decimal maximum) =>
        value >= minimum && value <= maximum;

    private static bool IsSlug(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 64 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');

    private static bool IsSafeName(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 64 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}
