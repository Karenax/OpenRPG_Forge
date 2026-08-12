using Forge.Network.Authoring;
using Forge.Network.Operations;

namespace Forge.Network.Tests;

public sealed class NetworkPlayerExperienceProfileTests
{
    [Fact]
    public void R121_Discovery_CoversDirectLanDirectoryFavoritesRecentsAndInvites()
    {
        var profile = NetworkPlayerExperienceProfile.CreateDefault();

        Assert.True(profile.Discovery.DirectAddress);
        Assert.True(profile.Discovery.LanDiscovery);
        Assert.True(profile.Discovery.CuratedDirectory);
        Assert.True(profile.Discovery.Favorites);
        Assert.True(profile.Discovery.RecentServers);
        Assert.True(profile.Discovery.InviteLinks);
        Assert.Empty(NetworkPlayerExperienceValidator.Validate(profile));
    }

    [Fact]
    public void R121_LobbyAndBuilder_AreRulesetDrivenVisibleAndServerApproved()
    {
        var profile = NetworkPlayerExperienceProfile.CreateDefault();

        Assert.Equal("ruleset:default", profile.Lobby.RulesetId);
        Assert.True(profile.Lobby.CharacterSelection);
        Assert.True(profile.Lobby.PermissionsVisible);
        Assert.True(profile.Lobby.ReadyState);
        Assert.True(profile.CharacterBuilder.PreviewEnabled);
        Assert.True(profile.CharacterBuilder.ImportAllowed);
        Assert.True(profile.CharacterBuilder.ServerApprovalRequired);
        Assert.NotEmpty(profile.CharacterBuilder.TemplateIds);
    }

    [Fact]
    public void R121_CharacterAuthorityAndCombat_ExposeSecureModesAndAllPolicyLayers()
    {
        var profile = NetworkPlayerExperienceProfile.CreateDefault();

        Assert.Equal(4, profile.CharacterAuthority.AllowedModes.Count);
        Assert.True(profile.CharacterAuthority.SignedUploadsRequired);
        Assert.True(profile.CharacterAuthority.EncryptedAtRest);
        Assert.True(profile.CharacterAuthority.MigrationEnabled);
        Assert.True(profile.CharacterAuthority.AccountBindingRequired);
        Assert.True(profile.Combat.GlobalPolicy);
        Assert.True(profile.Combat.AreaOverrides);
        Assert.True(profile.Combat.PartyOverrides);
        Assert.True(profile.Combat.ConsentRequired);
        Assert.True(profile.Combat.DuelsEnabled);
        Assert.True(profile.Combat.CrimeEnabled);
        Assert.True(profile.Combat.SafeZonesEnabled);
        Assert.True(profile.Combat.ServerOverride);
    }

    [Fact]
    public void R121_PreviewApplyUndoRedoAndRoundTrip_AreCanonical()
    {
        var initial = NetworkPlayerExperienceProfile.CreateDefault();
        var candidate = initial with
        {
            ServerRevision = 1,
            Lobby = initial.Lobby with { MaximumSlots = 12 },
            CharacterBuilder = initial.CharacterBuilder with { ImportAllowed = false }
        };
        var history = new NetworkPlayerExperienceHistory(initial);
        var command = new NetworkPlayerExperienceCommand(Guid.NewGuid(), 1, "test:r121", 0,
            "author:karen", NetworkRole.Administrator, false, candidate);

        var preview = history.Execute(command);
        Assert.True(preview.Succeeded);
        Assert.False(preview.Applied);
        Assert.Equal(initial, history.Current);
        Assert.Equal("lobby,character-builder", preview.SemanticDiff);
        Assert.True(history.Execute(command with { Apply = true }).Applied);
        Assert.Equal(initial, history.Undo("author:karen", NetworkRole.Administrator).Profile);
        Assert.Equal(candidate, history.Redo("author:karen", NetworkRole.Administrator).Profile);

        var store = new NetworkPlayerExperienceStore();
        var serialized = store.Serialize(candidate);
        Assert.Equal(serialized, store.Serialize(store.Deserialize(serialized)));
    }

    [Fact]
    public void R121_InvalidPermissionRevisionAndRuntimeAbsence_ReturnLocalizedDiagnostics()
    {
        var initial = NetworkPlayerExperienceProfile.CreateDefault();
        var invalid = initial with
        {
            ServerRevision = 1,
            Discovery = initial.Discovery with
            {
                DirectAddress = false,
                LanDiscovery = false,
                CuratedDirectory = false
            }
        };
        var validation = NetworkPlayerExperienceValidator.Validate(invalid);
        Assert.Contains(validation, item => item is
        {
            Code: "NET_DISCOVERY_ROUTE_REQUIRED",
            Path: "/network/playerExperience/discovery"
        } && item.ObjectId == invalid.Discovery.Id);

        var rejected = new NetworkPlayerExperienceHistory(initial).Execute(new(Guid.NewGuid(), 1,
            "test:r121", 8, "player", NetworkRole.Player, true, invalid));
        Assert.False(rejected.Succeeded);
        Assert.Contains(rejected.Diagnostics, item => item.Code == "NET_PLAYER_PERMISSION_DENIED");
        Assert.Contains(rejected.Diagnostics, item => item.Code == "NET_PLAYER_REVISION_STALE");

        Assert.Empty(NetworkPlayerExperienceRuntimeAdapter.Resolve(null, false).Diagnostics);
        Assert.Equal("NET_PLAYER_EXPERIENCE_REQUIRED_MISSING",
            Assert.Single(NetworkPlayerExperienceRuntimeAdapter.Resolve(null, true).Diagnostics).Code);
        var scripts = new NetworkPlayerExperienceScriptService(initial);
        Assert.Equal(3, scripts.Read(scripts.Handle).ConnectionRoutes);
        Assert.Throws<InvalidOperationException>(() => scripts.Read(scripts.Handle with { Generation = 2 }));
    }
}
