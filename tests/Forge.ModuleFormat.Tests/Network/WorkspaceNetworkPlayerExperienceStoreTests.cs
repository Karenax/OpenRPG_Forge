using System.Text;
using Forge.ModuleFormat.Network;
using Forge.Network.Authoring;
using Forge.Network.World;

namespace Forge.ModuleFormat.Tests.Network;

public sealed class WorkspaceNetworkPlayerExperienceStoreTests
{
    [Fact]
    public void MissingDocumentReturnsExplicitDefaultWithoutWriting()
    {
        WithWorkspace(root =>
        {
            var loaded = WorkspaceNetworkPlayerExperienceStore.Load(root);

            Assert.True(loaded.Succeeded);
            Assert.True(loaded.FileMissing);
            Assert.Empty(loaded.Diagnostics);
            Assert.Empty(NetworkPlayerExperienceValidator.Validate(loaded.Profile));
            Assert.False(File.Exists(Path.Combine(root, ".forge", "network", "player-experience.json")));
        });
    }

    [Fact]
    public void SaveIsAtomicDeterministicAndRoundTripsExactly()
    {
        WithWorkspace(root =>
        {
            var initial = NetworkPlayerExperienceProfile.CreateDefault("test:ux8-m");
            var candidate = initial with
            {
                ServerRevision = 4,
                Lobby = initial.Lobby with { MaximumSlots = 12, MaximumTeams = 3 },
                CharacterBuilder = initial.CharacterBuilder with
                {
                    TemplateIds = ["template:adventurer", "template:ranger"],
                    ImportAllowed = false
                },
                Combat = initial.Combat with
                {
                    PvpEnabled = true,
                    FriendlyFire = FriendlyFirePolicy.PartyOnly
                }
            };

            var saved = WorkspaceNetworkPlayerExperienceStore.Save(root, candidate);
            var loaded = WorkspaceNetworkPlayerExperienceStore.Load(root);
            var path = Path.Combine(root, ".forge", "network", "player-experience.json");
            var bytes = File.ReadAllBytes(path);

            Assert.True(saved.Succeeded);
            Assert.True(loaded.Succeeded);
            Assert.False(loaded.FileMissing);
            var canonical = new NetworkPlayerExperienceStore();
            Assert.Equal(canonical.Serialize(candidate), canonical.Serialize(loaded.Profile));
            Assert.True(loaded.Profile.Combat.PvpEnabled);
            Assert.Equal(FriendlyFirePolicy.PartyOnly, loaded.Profile.Combat.FriendlyFire);
            Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
            Assert.EndsWith("\n", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
            Assert.DoesNotContain("\r\n", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
            Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp"));

            var second = WorkspaceNetworkPlayerExperienceStore.Save(root, loaded.Profile);
            Assert.True(second.Succeeded);
            Assert.Equal(bytes, File.ReadAllBytes(path));
        });
    }

    [Fact]
    public void InvalidDocumentAndInvalidCandidateReturnStableDiagnostics()
    {
        WithWorkspace(root =>
        {
            var directory = Path.Combine(root, ".forge", "network");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "player-experience.json");
            File.WriteAllText(path, "{\"schemaVersion\":999}", new UTF8Encoding(false));

            var loaded = WorkspaceNetworkPlayerExperienceStore.Load(root);
            var invalid = NetworkPlayerExperienceProfile.CreateDefault() with { SchemaVersion = 999 };
            var saved = WorkspaceNetworkPlayerExperienceStore.Save(root, invalid);

            Assert.False(loaded.Succeeded);
            Assert.False(loaded.FileMissing);
            Assert.Equal(WorkspaceNetworkPlayerExperienceStore.ReadFailedCode, loaded.Diagnostics.Single().Code);
            Assert.False(saved.Succeeded);
            Assert.Equal(WorkspaceNetworkPlayerExperienceStore.WriteFailedCode, saved.Diagnostics.Single().Code);
            Assert.Equal("{\"schemaVersion\":999}", File.ReadAllText(path, Encoding.UTF8));
        });
    }

    private static void WithWorkspace(Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-network-player-store-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            action(root);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
