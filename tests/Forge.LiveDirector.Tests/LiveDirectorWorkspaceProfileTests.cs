using Forge.LiveDirector.Authoring;
using Forge.LiveDirector.Catalog;
using Forge.LiveDirector.World;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Tests;

public sealed class LiveDirectorWorkspaceProfileTests
{
    [Fact]
    public void R125_ClientCreatorSpawnChooserAndFocusPolicies_AreComplete()
    {
        var profile = LiveDirectorWorkspaceProfile.CreateDefault();
        Assert.True(profile.Client.SeparateClient && profile.Client.EngineIndependent && profile.Client.SecondFactor);
        Assert.True(profile.Creator.ServerAllowlist && profile.Creator.Thumbnails && profile.Creator.PermissionFilter);
        Assert.Equal(3, profile.Spawn.PlacementModes.Count);
        Assert.True(profile.Spawn.FormationCoordinates && profile.Spawn.SafeUndo);
        Assert.True(profile.Chooser.HierarchicalZones && profile.Chooser.InstanceNavigation);
        Assert.True(profile.Focus.SpectatorCamera && profile.Focus.PrivacyOverrideRequiresPermission && profile.Focus.ReadOnly);
        Assert.Empty(LiveDirectorWorkspaceValidator.Validate(profile));
    }

    [Fact]
    public void R125_PreviewApplyUndoRedoRoundTripRuntimeAndScripts_AreCanonical()
    {
        var initial = LiveDirectorWorkspaceProfile.CreateDefault();
        var candidate = initial with { ServerRevision = 1, Spawn = initial.Spawn with { MaximumInstances = 80 } };
        var history = new LiveDirectorWorkspaceHistory(initial);
        var command = new LiveDirectorWorkspaceCommand(Guid.NewGuid(), 1, "test:r125", 0, "gm:karen", NetworkRole.GameMaster, false, candidate);
        Assert.False(history.Execute(command).Applied);
        Assert.True(history.Execute(command with { Apply = true }).Applied);
        Assert.Equal(initial, history.Undo("gm:karen", NetworkRole.GameMaster).Profile);
        Assert.Equal(candidate, history.Redo("gm:karen", NetworkRole.GameMaster).Profile);
        var store = new LiveDirectorWorkspaceStore();
        Assert.Equal(store.Serialize(candidate), store.Serialize(store.Deserialize(store.Serialize(candidate))));
        Assert.Empty(LiveDirectorWorkspaceRuntimeAdapter.Resolve(null, false).Diagnostics);
        Assert.Equal("LIVE_WORKSPACE_REQUIRED_MISSING", Assert.Single(LiveDirectorWorkspaceRuntimeAdapter.Resolve(null, true).Diagnostics).Code);
        var scripts = new LiveDirectorWorkspaceScriptService(initial);
        Assert.Equal(5, scripts.Read(scripts.Handle).FilterCount);
    }

    [Fact]
    public void R125_InvalidPolicyPermissionAndRevision_ReturnLocalizedDiagnostics()
    {
        var initial = LiveDirectorWorkspaceProfile.CreateDefault();
        var invalid = initial with { ServerRevision = 1, Spawn = initial.Spawn with { MaximumInstances = 0 } };
        Assert.Contains(LiveDirectorWorkspaceValidator.Validate(invalid), x => x.Code == "LIVE_SPAWN_POLICY_INVALID");
        var result = new LiveDirectorWorkspaceHistory(initial).Execute(new(Guid.NewGuid(), 1, "test:r125", 9, "player", NetworkRole.Player, true, invalid));
        Assert.Contains(result.Diagnostics, x => x.Code == "LIVE_WORKSPACE_PERMISSION_DENIED");
        Assert.Contains(result.Diagnostics, x => x.Code == "LIVE_WORKSPACE_REVISION_STALE");
    }

    [Fact]
    public void R125_RuntimeUsesAuthenticatedCatalogSpawnChooserFocusAndCompensationAuthorities()
    {
        var profile = LiveDirectorWorkspaceProfile.CreateDefault("test:r125:runtime") with
        {
            Spawn = LiveDirectorWorkspaceProfile.CreateDefault().Spawn with
            {
                MaximumInstances = 8
            }
        };
        var now = new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        var nextObject = 100;
        var runtime = LiveDirectorWorkspaceRuntimeAdapter.Create(profile, () => now,
            () => Guid.Parse("d1250000-0000-4000-8000-000000000100"),
            () => Guid.Parse($"d1250000-0000-4000-8000-{nextObject++:000000000000}"));
        var zone = Guid.Parse("d1250000-0000-4000-8000-000000000200");
        var blueprint = Guid.Parse("d1250000-0000-4000-8000-000000000201");
        var account = runtime.Accounts.Register("studio-director", "correct-horse-battery");
        runtime.Authentication.GrantRole(account.AccountId, NetworkRole.Administrator);

        Assert.Equal("LIVE_GM_SECOND_FACTOR_INVALID", runtime.Authentication.Authenticate(
            new("studio-director", "correct-horse-battery", null)).Code);
        var session = runtime.Authentication.Authenticate(
            new("studio-director", "correct-horse-battery", "246810"),
            factor => factor == "246810");
        Assert.True(session.Authenticated);
        Assert.True(runtime.Authentication.IsActive(session));
        Assert.Equal(account.AccountId,
            runtime.Client.Connect(Guid.Parse("d1250000-0000-4000-8000-000000000202"), session)
                .AccountId);

        runtime.Catalog.Replace([
            new(blueprint, "Gardien des braises", "creature", "crypt", 10,
                new HashSet<Guid> { zone }, true, "res://thumbs/guardian.png"),
            new(Guid.Parse("d1250000-0000-4000-8000-000000000203"), "Interdit",
                "creature", "crypt", 1, new HashSet<Guid> { zone }, false)
        ]);
        var actor = session.Subject!;
        var allowed = Assert.Single(runtime.Catalog.Search(actor,
            new("gardien", "creature", zone, 20, "crypt")));
        Assert.NotNull(allowed.Thumbnail);

        var commandId = Guid.Parse("d1250000-0000-4000-8000-000000000204");
        var request = new LiveSpawnService.Request(commandId, blueprint, zone, 4,
            "formation", 0, "crypt-01", 10, 2, 20, 2);
        var preview = runtime.Spawn.Execute(actor, request, false);
        var applied = runtime.Spawn.Execute(actor, request, true);
        Assert.Equal("LIVE_SPAWN_PREVIEW", preview.Code);
        Assert.Equal(preview.ObjectIds, applied.ObjectIds);
        Assert.Equal(4, applied.Instances.Select(item => (item.X, item.Y, item.Z)).Distinct().Count());
        Assert.Equal(4, runtime.Chooser.Search(actor,
            new(zone, "creature", "braises", null, "spawned", "crypt-01")).Count);

        var privateTarget = Guid.Parse("d1250000-0000-4000-8000-000000000205");
        runtime.Chooser.Upsert(new(privateTarget, zone, "waypoint", "Secret", "secret", null,
            "active", ["private"], "crypt-01", true, 4, 0, 8));
        Assert.DoesNotContain(runtime.Chooser.Search(actor,
            new(zone, null, null, null, null)), item => item.ObjectId == privateTarget);
        Assert.Contains(runtime.Chooser.Search(actor,
            new(zone, null, null, null, null), includePrivate: true),
            item => item.ObjectId == privateTarget);
        var beforeFocus = runtime.Chooser.Find(privateTarget);
        var focus = runtime.WorldActions.Execute(actor,
            new(Guid.Parse("d1250000-0000-4000-8000-000000000206"), "director.focus",
                privateTarget, null, 0,
                new Dictionary<string, string> { ["privacyOverride"] = "approved" }), true);
        Assert.True(focus.Applied);
        Assert.Equal(beforeFocus, runtime.Chooser.Find(privateTarget));

        var player = new AuthorizationSubject(
            Guid.Parse("d1250000-0000-4000-8000-000000000207"), NetworkRole.Player,
            new HashSet<Guid> { zone });
        Assert.Equal("LIVE_SPAWN_COMPENSATION_DENIED",
            runtime.Spawn.Compensate(player, commandId, 1, true).Code);
        var compensated = runtime.Spawn.Compensate(actor, commandId, 1, true);
        Assert.Equal("LIVE_SPAWN_COMPENSATED", compensated.Code);
        Assert.Empty(runtime.Chooser.Search(actor,
            new(zone, "creature", "braises", null, "spawned", "crypt-01")));
        Assert.Contains("LIVE_SESSION_REVOKED", runtime.Authentication.Revoke(session));
        Assert.False(runtime.Authentication.IsActive(session));
    }
}
