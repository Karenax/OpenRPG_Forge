using Forge.Domain.Assets;

namespace Forge.Domain.Tests.Assets;

public sealed class Model3DGenerationTests
{
    [Fact]
    public void OfflineProviderAndAcceptedReceiptAreValid()
    {
        var receipt = Receipt(Model3DProviderDefinition.CreateOffline());

        Assert.Empty(Model3DGenerationValidator.ValidateProvider(receipt.Provider));
        Assert.Empty(Model3DGenerationValidator.ValidateReceipt(receipt));
    }

    [Fact]
    public void RemoteEndpointInlineSecretAndNonCc0LicenseAreRejected()
    {
        var provider = Model3DProviderDefinition.CreateOffline() with
        {
            Kind = Model3DProviderKind.TrellisLocal,
            Endpoint = "https://example.com/generate",
            NetworkAllowed = true,
            CredentialEnvironmentVariable = "inline-secret!",
            AllowedLicenses = new HashSet<string>(["MIT"], StringComparer.Ordinal),
        };
        var codes = Model3DGenerationValidator.ValidateProvider(provider).Select(item => item.Code).ToArray();

        Assert.Contains(Model3DGenerationValidator.EndpointUnsafeCode, codes);
        Assert.Contains(Model3DGenerationValidator.SecretInvalidCode, codes);
        Assert.Contains(Model3DGenerationValidator.LicenseDeniedCode, codes);
    }

    [Fact]
    public void ReceiptRequiresSafeImageBoundedTopologyAndHumanAcceptance()
    {
        var receipt = Receipt(Model3DProviderDefinition.CreateOffline()) with
        {
            InputKind = Model3DInputKind.Image,
            ImageSourceRelativePath = "../portrait.png",
            ImageSourceSha256 = new string('b', 64),
            SourceTriangleCount = 200,
            Topology = new Model3DTopologyPolicy(100, [0.5m], true),
            HumanAccepted = false,
            HumanAcceptanceProvenance = null,
        };
        var codes = Model3DGenerationValidator.ValidateReceipt(receipt).Select(item => item.Code).ToArray();

        Assert.Contains(Model3DGenerationValidator.SourceInvalidCode, codes);
        Assert.Contains(Model3DGenerationValidator.TopologyInvalidCode, codes);
        Assert.Contains(Model3DGenerationValidator.AcceptanceRequiredCode, codes);
    }

    private static Model3DGenerationReceipt Receipt(Model3DProviderDefinition provider) => new(
        1, Guid.Parse("b1330000-0000-4000-8000-000000000010"), "forge-idol", "Forge Idol",
        Model3DInputKind.Text, new string('a', 64), null, null, provider, "CC0-1.0", "provider:test",
        "assets/staging/ai-3d/forge-idol/forge-idol.obj", new string('b', 64), 512, 12,
        new Model3DTopologyPolicy(10_000, [0.5m, 0.25m], true), true, "author:test",
        "assets/generated/forge-idol/model.glb", "provider:test:blender-normalize");
}
