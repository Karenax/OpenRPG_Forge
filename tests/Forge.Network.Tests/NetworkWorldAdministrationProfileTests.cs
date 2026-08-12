using Forge.Network.Authoring;
using Forge.Network.Identity;
using Forge.Network.Operations;
using Forge.Network.World;

namespace Forge.Network.Tests;

public sealed class NetworkWorldAdministrationProfileTests
{
    [Fact]
    public void R122_InstancesAndDynamicAreas_CoverLifecycleAllocationPersistenceCloneOverridesAndBounds()
    {
        var profile = NetworkWorldAdministrationProfile.CreateDefault();
        Assert.True(profile.Instances.PartyAllocation);
        Assert.True(profile.Instances.PersistenceEnabled);
        Assert.True(profile.Instances.RuntimeSceneInstantiation);
        Assert.InRange(profile.Instances.GarbageCollectionMinutes, 1, 10_080);
        Assert.True(profile.DynamicAreas.TemplateInstantiation);
        Assert.True(profile.DynamicAreas.CloneExistingArea);
        Assert.NotEmpty(profile.DynamicAreas.AllowedOverrideKeys);
        Assert.True(profile.DynamicAreas.ServerAuthorityRequired);

        var service = new ZoneInstanceService();
        var template = new ZoneTemplate(Guid.NewGuid(), "area:crypt-template", 2, 16, true);
        Assert.Empty(service.RegisterTemplate(template));
        var party = Guid.NewGuid();
        var created = service.Create(template.TemplateId, ZoneLifecycle.Session, party);
        Assert.True(created.Decision.Accepted);
        Assert.True(service.Enter(created.Instance!.InstanceId, Guid.NewGuid(), party, 1).Accepted);
    }

    [Fact]
    public void R122_Authentication_IsPluggablePrivateAndBackedBySafeLocalProvider()
    {
        var profile = NetworkWorldAdministrationProfile.CreateDefault();
        Assert.Equal(5, profile.Authentication.Providers.Count);
        Assert.True(profile.Authentication.StableAccountId);
        Assert.True(profile.Authentication.PrivacyByDefault);
        Assert.True(profile.Authentication.SecretsHashed);
        Assert.True(profile.Authentication.NonEnumeratingFailures);

        var accounts = new AccountService();
        var account = accounts.Register("hero.account", "forge-password-2026");
        Assert.NotEqual("forge-password-2026", Convert.ToHexString(account.PasswordHash));
        Assert.True(accounts.Authenticate("hero.account", "forge-password-2026").Authenticated);
        Assert.Equal("NET_AUTH_INVALID_CREDENTIALS",
            accounts.Authenticate("missing.account", "forge-password-2026").Code);
    }

    [Fact]
    public void R122_AdminAndModeration_UseAuthorizationPreviewRatePolicyAuditAndEvidenceContract()
    {
        var profile = NetworkWorldAdministrationProfile.CreateDefault();
        Assert.True(profile.Administration.ApiEnabled && profile.Administration.CliEnabled);
        Assert.True(profile.Administration.AuthenticationRequired && profile.Administration.AuditEnabled);
        Assert.NotEmpty(profile.Administration.SafeCommandIds);
        Assert.True(profile.Moderation.AccountAdapter && profile.Moderation.IpAdapter && profile.Moderation.DeviceAdapter);
        Assert.True(profile.Moderation.Reports && profile.Moderation.Evidence && profile.Moderation.AppealNotes);

        var admin = new AuthorizationSubject(Guid.NewGuid(), NetworkRole.Administrator, new HashSet<Guid>());
        var administration = new AdministrationService(new AuthorizationService());
        var command = new AdministrationCommand(Guid.NewGuid(), admin.AccountId, "operations.inspect",
            null, null, 0, new Dictionary<string, string>());
        Assert.Equal("NET_ADMIN_PREVIEW", administration.Execute(admin, command, false).Code);
        Assert.True(administration.Execute(admin, command, true).Applied);

        var moderator = new AuthorizationSubject(Guid.NewGuid(), NetworkRole.Moderator, new HashSet<Guid>());
        var target = Guid.NewGuid();
        var moderation = new ModerationService(new AuthorizationService(), TimeProvider.System);
        Assert.True(moderation.Apply(moderator, ModerationActionKind.Mute, target, "evidence:report-1",
            TimeSpan.FromMinutes(30)).Applied);
        Assert.Single(moderation.AuditFor(target));
    }

    [Fact]
    public void R122_PreviewApplyUndoRedoAndRoundTrip_AreCanonical()
    {
        var initial = NetworkWorldAdministrationProfile.CreateDefault();
        var candidate = initial with
        {
            ServerRevision = 1,
            Instances = initial.Instances with { MaximumInstances = 48 },
            Moderation = initial.Moderation with { PrivacyRetentionDays = 120 }
        };
        var history = new NetworkWorldAdministrationHistory(initial);
        var command = new NetworkWorldAdministrationCommand(Guid.NewGuid(), 1, "test:r122", 0,
            "admin:karen", NetworkRole.Administrator, false, candidate);
        Assert.False(history.Execute(command).Applied);
        Assert.True(history.Execute(command with { Apply = true }).Applied);
        Assert.Equal(initial, history.Undo("admin:karen", NetworkRole.Administrator).Profile);
        Assert.Equal(candidate, history.Redo("admin:karen", NetworkRole.Administrator).Profile);
        var store = new NetworkWorldAdministrationStore();
        var json = store.Serialize(candidate);
        Assert.Equal(json, store.Serialize(store.Deserialize(json)));
    }

    [Fact]
    public void R122_InvalidPolicyPermissionRevisionAndMissingRuntime_ReturnLocalizedDiagnostics()
    {
        var initial = NetworkWorldAdministrationProfile.CreateDefault();
        var invalid = initial with
        {
            ServerRevision = 1,
            Administration = initial.Administration with { RequestsPerMinute = 0 }
        };
        Assert.Contains(NetworkWorldAdministrationValidator.Validate(invalid), item => item is
        {
            Code: "NET_REMOTE_ADMIN_POLICY_INVALID",
            Path: "/network/worldAdministration/administration"
        });
        var rejected = new NetworkWorldAdministrationHistory(initial).Execute(new(Guid.NewGuid(), 1,
            "test:r122", 9, "player", NetworkRole.Player, true, invalid));
        Assert.Contains(rejected.Diagnostics, item => item.Code == "NET_WORLD_ADMIN_PERMISSION_DENIED");
        Assert.Contains(rejected.Diagnostics, item => item.Code == "NET_WORLD_ADMIN_REVISION_STALE");
        Assert.Empty(NetworkWorldAdministrationRuntimeAdapter.Resolve(null, false).Diagnostics);
        Assert.Equal("NET_WORLD_ADMIN_REQUIRED_MISSING",
            Assert.Single(NetworkWorldAdministrationRuntimeAdapter.Resolve(null, true).Diagnostics).Code);
        var scripts = new NetworkWorldAdministrationScriptService(initial);
        Assert.Equal(5, scripts.Read(scripts.Handle).ProviderCount);
        Assert.Throws<InvalidOperationException>(() => scripts.Read(scripts.Handle with { Generation = 2 }));
    }
}
