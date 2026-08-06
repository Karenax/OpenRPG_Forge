using Forge.Domain.Validation;

namespace Forge.Domain.Assets;

public enum Model3DProviderKind
{
    DeterministicOffline,
    TrellisLocal,
    TripoSrLocal,
}

public enum Model3DInputKind
{
    Text,
    Image,
}

public sealed record Model3DProviderDefinition(
    int SchemaVersion,
    Guid Id,
    string Key,
    Model3DProviderKind Kind,
    string? Endpoint,
    string? CredentialEnvironmentVariable,
    bool NetworkAllowed,
    int TimeoutSeconds,
    long MaximumArtifactBytes,
    IReadOnlySet<string> AllowedLicenses,
    string Provenance)
{
    public const int CurrentSchemaVersion = 1;

    public static Model3DProviderDefinition CreateOffline() => new(
        CurrentSchemaVersion,
        Guid.Parse("b1330000-0000-4000-8000-000000000001"),
        "deterministic-offline-3d",
        Model3DProviderKind.DeterministicOffline,
        null,
        null,
        false,
        30,
        4 * 1024 * 1024,
        new HashSet<string>(["CC0-1.0"], StringComparer.Ordinal),
        "openrpg-forge:model3d-provider:offline");
}

public sealed record Model3DTopologyPolicy(
    int MaximumSourceTriangles,
    IReadOnlyList<decimal> LodRatios,
    bool GenerateCollision);

public sealed record Model3DGenerationReceipt(
    int SchemaVersion,
    Guid Id,
    string Slug,
    string DisplayName,
    Model3DInputKind InputKind,
    string PromptSha256,
    string? ImageSourceRelativePath,
    string? ImageSourceSha256,
    Model3DProviderDefinition Provider,
    string LicenseSpdx,
    string ProviderProvenance,
    string SourceRelativePath,
    string SourceSha256,
    long SourceBytes,
    int SourceTriangleCount,
    Model3DTopologyPolicy Topology,
    bool HumanAccepted,
    string? HumanAcceptanceProvenance,
    string OutputRelativePath,
    string ModificationProvenance)
{
    public const int CurrentSchemaVersion = 1;
}

public static class Model3DGenerationValidator
{
    public const string ProviderInvalidCode = "assistant.model3d.provider_invalid";
    public const string EndpointUnsafeCode = "assistant.model3d.endpoint_unsafe";
    public const string SecretInvalidCode = "assistant.model3d.secret_invalid";
    public const string LicenseDeniedCode = "assistant.model3d.license_denied";
    public const string SourceInvalidCode = "assistant.model3d.source_invalid";
    public const string TopologyInvalidCode = "assistant.model3d.topology_invalid";
    public const string AcceptanceRequiredCode = "assistant.model3d.acceptance_required";
    public const string PathUnsafeCode = "assistant.model3d.path_unsafe";

    public static IReadOnlyList<DomainDiagnostic> ValidateProvider(Model3DProviderDefinition provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var path = $"assistant/model3d/providers/{provider.Id:D}";
        var diagnostics = new List<DomainDiagnostic>();
        if (provider.SchemaVersion != Model3DProviderDefinition.CurrentSchemaVersion || provider.Id == Guid.Empty ||
            !SafeIdentifier(provider.Key) || provider.TimeoutSeconds is < 1 or > 900 ||
            provider.MaximumArtifactBytes is < 1024 or > 512L * 1024 * 1024 ||
            string.IsNullOrWhiteSpace(provider.Provenance))
            diagnostics.Add(new(ProviderInvalidCode, "Provider identity, limits and provenance are invalid.", path));
        if (provider.Kind == Model3DProviderKind.DeterministicOffline)
        {
            if (provider.Endpoint is not null || provider.NetworkAllowed || provider.CredentialEnvironmentVariable is not null)
                diagnostics.Add(new(EndpointUnsafeCode, "Offline provider cannot configure network or credentials.", path));
        }
        else if (!provider.NetworkAllowed || !IsLoopbackEndpoint(provider.Endpoint))
            diagnostics.Add(new(EndpointUnsafeCode,
                "Optional model providers require an explicitly authorized loopback HTTP endpoint.", $"{path}/endpoint"));
        if (provider.CredentialEnvironmentVariable is not null && !SafeEnvironmentName(provider.CredentialEnvironmentVariable))
            diagnostics.Add(new(SecretInvalidCode,
                "Credentials must be referenced by environment-variable name and never stored inline.",
                $"{path}/credentialEnvironmentVariable"));
        if (provider.AllowedLicenses.Count == 0 || provider.AllowedLicenses.Any(item => item != "CC0-1.0"))
            diagnostics.Add(new(LicenseDeniedCode, "The generated 3D staging profile only allows CC0-1.0.",
                $"{path}/allowedLicenses"));
        return diagnostics;
    }

    public static IReadOnlyList<DomainDiagnostic> ValidateReceipt(
        Model3DGenerationReceipt receipt,
        bool requireHumanAcceptance = true)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var path = $"assets/ai-3d/{receipt.Id:D}";
        var diagnostics = new List<DomainDiagnostic>(ValidateProvider(receipt.Provider));
        if (receipt.SchemaVersion != Model3DGenerationReceipt.CurrentSchemaVersion || receipt.Id == Guid.Empty ||
            !SafeIdentifier(receipt.Slug) || string.IsNullOrWhiteSpace(receipt.DisplayName) ||
            !Sha256(receipt.PromptSha256) || !Sha256(receipt.SourceSha256) || receipt.SourceBytes is < 64 ||
            receipt.SourceBytes > receipt.Provider.MaximumArtifactBytes || string.IsNullOrWhiteSpace(receipt.ProviderProvenance) ||
            string.IsNullOrWhiteSpace(receipt.ModificationProvenance))
            diagnostics.Add(new(SourceInvalidCode, "Generated source identity, hashes, size or provenance are invalid.", path));
        if (receipt.InputKind == Model3DInputKind.Image &&
            (string.IsNullOrWhiteSpace(receipt.ImageSourceRelativePath) ||
             !AssetCatalogValidator.IsSafeRelativePath(receipt.ImageSourceRelativePath) ||
             !Sha256(receipt.ImageSourceSha256)))
            diagnostics.Add(new(SourceInvalidCode, "Image-to-3D requires a confined source image and SHA-256.",
                $"{path}/imageSource"));
        if (!receipt.Provider.AllowedLicenses.Contains(receipt.LicenseSpdx) || receipt.LicenseSpdx != "CC0-1.0")
            diagnostics.Add(new(LicenseDeniedCode, "Provider output license is not allowed by the project policy.",
                $"{path}/licenseSpdx"));
        if (receipt.Topology.MaximumSourceTriangles is < 4 or > 500_000 ||
            receipt.SourceTriangleCount is < 4 ||
            receipt.SourceTriangleCount > receipt.Topology.MaximumSourceTriangles ||
            receipt.Topology.LodRatios.Count is < 1 or > 4 ||
            receipt.Topology.LodRatios.Distinct().Count() != receipt.Topology.LodRatios.Count ||
            receipt.Topology.LodRatios.Any(item => item is < .01m or > .99m))
            diagnostics.Add(new(TopologyInvalidCode, "Topology and LOD policy exceed bounded authoring limits.",
                $"{path}/topology"));
        if ((requireHumanAcceptance && !receipt.HumanAccepted) ||
            receipt.HumanAccepted != !string.IsNullOrWhiteSpace(receipt.HumanAcceptanceProvenance))
            diagnostics.Add(new(AcceptanceRequiredCode, "Human acceptance is required before provider output is staged.",
                $"{path}/humanAccepted"));
        if (!SafeStagingPath(receipt.SourceRelativePath, receipt.Slug, ".obj") ||
            !SafeStagingPath(receipt.OutputRelativePath, receipt.Slug, ".glb"))
            diagnostics.Add(new(PathUnsafeCode, "Provider staging and output paths are outside their confined roots.", path));
        return diagnostics;
    }

    private static bool IsLoopbackEndpoint(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var endpoint) &&
        endpoint.Scheme is "http" or "https" && endpoint.IsLoopback && string.IsNullOrEmpty(endpoint.UserInfo);

    private static bool SafeStagingPath(string value, string slug, string extension)
    {
        if (!AssetCatalogValidator.IsSafeRelativePath(value)) return false;
        var prefix = extension == ".obj" ? $"assets/staging/ai-3d/{slug}/" : $"assets/generated/{slug}/";
        return value.StartsWith(prefix, StringComparison.Ordinal) && value.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Sha256(string? value) => value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool SafeIdentifier(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 64 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private static bool SafeEnvironmentName(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128 &&
        (char.IsAsciiLetter(value[0]) || value[0] == '_') &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
}
