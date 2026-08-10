namespace Forge.Scripting.Tests;

public sealed class ForgeScriptCapabilityManifestTests
{
    [Fact]
    public void Manifest_is_versioned_bounded_and_explicitly_denies_host_capabilities()
    {
        Assert.Equal(1, ForgeScriptCapabilityManifest.SchemaVersion);
        Assert.Equal("1.0.0", ForgeScriptCapabilityManifest.ApiVersion);
        Assert.Equal(["blackboard", "determinism", "movement", "opaque-handles", "utilities", "world"],
            ForgeScriptCapabilityManifest.Capabilities.Select(item => item.Id).Order().ToArray());
        Assert.Contains("filesystem", ForgeScriptCapabilityManifest.ExplicitlyForbiddenCapabilities);
        Assert.Contains("network", ForgeScriptCapabilityManifest.ExplicitlyForbiddenCapabilities);
        Assert.Contains("process", ForgeScriptCapabilityManifest.ExplicitlyForbiddenCapabilities);
        Assert.Contains("native-pointer", ForgeScriptCapabilityManifest.ExplicitlyForbiddenCapabilities);
        Assert.Contains("reflection", ForgeScriptCapabilityManifest.ExplicitlyForbiddenCapabilities);
        Assert.Contains("godot", ForgeScriptCapabilityManifest.ExplicitlyForbiddenCapabilities);
        Assert.Contains("dotnet", ForgeScriptCapabilityManifest.ExplicitlyForbiddenCapabilities);
        Assert.Contains("regex=256/4096", ForgeScriptCapabilityManifest.Summary());
    }

    [Theory]
    [InlineData("void main() { file_open(\"save.dat\"); }")]
    [InlineData("void main() { socket.connect(\"host\"); }")]
    [InlineData("void main() { process(\"tool\"); }")]
    [InlineData("void main() { Godot::Node node; }")]
    [InlineData("void main() { System::Reflection(); }")]
    public void Forbidden_capabilities_are_localized_before_build(string source)
    {
        var diagnostic = Assert.Single(ForgeScriptCapabilityManifest.ValidateSource(source, Guid.NewGuid()));

        Assert.Equal("script.sandbox.capability_forbidden", diagnostic.Code);
        Assert.StartsWith("script/source:1:", diagnostic.Path, StringComparison.Ordinal);
        Assert.Equal("error", diagnostic.Severity);
    }

    [Fact]
    public void Catalog_documents_every_r013_native_entry()
    {
        var names = ForgeScriptApiCatalog.Registrations.Select(item => item.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var capability in ForgeScriptCapabilityManifest.Capabilities)
            foreach (var api in capability.ApiNames)
                Assert.Contains(api, names);
    }

    [Fact]
    public void Canonical_source_validation_rejects_a_forbidden_capability_before_compilation()
    {
        var id = Guid.Parse("13000000-0000-4000-8000-000000000040");
        var document = new ForgeScriptSourceDocument(id, 1, "test:r013", "scripts/unsafe.as",
            "void main() { file_open(\"secret\"); }", "1.0.0",
            ForgeScriptSourceDistributionPolicy.SourceOnly, true, [], []);

        var diagnostics = ForgeScriptSourceValidator.Validate(document, []);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("script.sandbox.capability_forbidden", diagnostic.Code);
        Assert.Equal(id, diagnostic.SubjectId);
        Assert.StartsWith("script/source:1:", diagnostic.Path, StringComparison.Ordinal);
    }
}
