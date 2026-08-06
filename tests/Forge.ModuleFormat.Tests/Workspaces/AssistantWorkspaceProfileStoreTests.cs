using Forge.Domain.Assistant;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Workspaces;

public sealed class AssistantWorkspaceProfileStoreTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(), "openrpg-forge-assistant-tests", Guid.NewGuid().ToString("N"));

    public AssistantWorkspaceProfileStoreTests()
    {
        Directory.CreateDirectory(_temporaryRoot);
    }

    [Fact]
    public void MissingProfileReturnsSafeOfflineDefault()
    {
        var loaded = AssistantWorkspaceProfileStore.Load(_temporaryRoot);

        Assert.True(loaded.Succeeded);
        Assert.True(loaded.FileMissing);
        Assert.Equal(AssistantProviderKind.DeterministicOffline, loaded.Profile.ActiveProvider.Kind);
    }

    [Fact]
    public void SaveReloadAndSaveAgainAreDeterministic()
    {
        var profile = AssistantWorkspaceProfile.CreateDefault();

        var saved = AssistantWorkspaceProfileStore.Save(_temporaryRoot, profile);
        var path = Path.Combine(_temporaryRoot, "assistant", "profile.json");
        var first = File.ReadAllText(path);
        var loaded = AssistantWorkspaceProfileStore.Load(_temporaryRoot);
        var savedAgain = AssistantWorkspaceProfileStore.Save(_temporaryRoot, loaded.Profile);
        var second = File.ReadAllText(path);

        Assert.True(saved.Succeeded);
        Assert.True(savedAgain.Succeeded);
        Assert.True(loaded.Succeeded);
        Assert.False(loaded.FileMissing);
        Assert.Equal(first, second);
        Assert.Equal(profile.Id, loaded.Profile.Id);
        Assert.Equal(profile.ActiveProvider, loaded.Profile.ActiveProvider);
        Assert.Equal(profile.Prompts[0].Key, loaded.Profile.Prompts[0].Key);
        Assert.Equal(profile.Prompts[0].Version, loaded.Profile.Prompts[0].Version);
        Assert.Equal(profile.Prompts[0].Tests[0].Variables, loaded.Profile.Prompts[0].Tests[0].Variables);
    }

    [Fact]
    public void InvalidProfileReturnsStableReadDiagnosticWithoutPartialState()
    {
        var directory = Path.Combine(_temporaryRoot, "assistant");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "profile.json"), "{ invalid }");

        var loaded = AssistantWorkspaceProfileStore.Load(_temporaryRoot);

        Assert.False(loaded.Succeeded);
        Assert.Equal(AssistantWorkspaceProfileStore.ReadFailedCode, Assert.Single(loaded.Diagnostics).Code);
        Assert.Equal(AssistantProviderKind.DeterministicOffline, loaded.Profile.ActiveProvider.Kind);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot)) Directory.Delete(_temporaryRoot, recursive: true);
    }
}
