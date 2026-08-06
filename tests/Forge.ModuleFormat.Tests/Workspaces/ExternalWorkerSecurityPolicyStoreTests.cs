using Forge.Domain.Assets;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Workspaces;

public sealed class ExternalWorkerSecurityPolicyStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "openrpg-worker-security", Guid.NewGuid().ToString("N"));

    public ExternalWorkerSecurityPolicyStoreTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void MissingPolicyReturnsSafeDefaultWithoutWriting()
    {
        var loaded = ExternalWorkerSecurityPolicyStore.Load(_root);

        Assert.True(loaded.Succeeded);
        Assert.True(loaded.FileMissing);
        Assert.True(loaded.Policy.DenyFreeFormPython);
        Assert.False(loaded.Policy.NetworkAllowed);
        Assert.False(File.Exists(Path.Combine(_root, "assets", "worker-security.json")));
    }

    [Fact]
    public void SaveReloadAndSaveAgainAreExactAndDeterministic()
    {
        var policy = ExternalWorkerSecurityPolicy.CreateDefault() with
        {
            MaximumJobSeconds = 180,
            MaximumOutputBytes = 256L * 1024 * 1024,
            Provenance = "author:g12-l",
        };
        var firstSave = ExternalWorkerSecurityPolicyStore.Save(_root, policy);
        var path = Path.Combine(_root, "assets", "worker-security.json");
        var first = File.ReadAllText(path);
        var loaded = ExternalWorkerSecurityPolicyStore.Load(_root);
        var secondSave = ExternalWorkerSecurityPolicyStore.Save(_root, loaded.Policy);
        var second = File.ReadAllText(path);

        Assert.True(firstSave.Succeeded);
        Assert.True(loaded.Succeeded);
        Assert.False(loaded.FileMissing);
        Assert.True(secondSave.Succeeded);
        Assert.Equal(policy.Id, loaded.Policy.Id);
        Assert.Equal(policy.MaximumJobSeconds, loaded.Policy.MaximumJobSeconds);
        Assert.Equal(policy.MaximumOutputBytes, loaded.Policy.MaximumOutputBytes);
        Assert.Equal(policy.Provenance, loaded.Policy.Provenance);
        Assert.Equal(policy.AllowedBlenderOperations, loaded.Policy.AllowedBlenderOperations);
        Assert.Equal(first, second);
    }

    [Fact]
    public void InvalidPolicyReturnsStableDiagnosticAndSafeFallback()
    {
        Directory.CreateDirectory(Path.Combine(_root, "assets"));
        File.WriteAllText(Path.Combine(_root, "assets", "worker-security.json"),
            "{\"formatVersion\":\"1.0.0\",\"policy\":{\"schemaVersion\":1}}");

        var loaded = ExternalWorkerSecurityPolicyStore.Load(_root);

        Assert.False(loaded.Succeeded);
        Assert.Equal(ExternalWorkerSecurityPolicyStore.ReadFailedCode, Assert.Single(loaded.Diagnostics).Code);
        Assert.True(loaded.Policy.DenyShell);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
