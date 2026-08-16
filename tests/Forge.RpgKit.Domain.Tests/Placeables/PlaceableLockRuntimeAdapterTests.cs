using Forge.Domain.Blueprints;
using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Application.Placeables;
using Forge.RpgKit.Domain.Placeables;

namespace Forge.RpgKit.Domain.Tests.Placeables;

public sealed class PlaceableLockRuntimeAdapterTests
{
    [Fact]
    public void Projects_all_authored_states_and_mechanisms()
    {
        var adapter = new PlaceableLockRuntimeAdapter();
        foreach (var state in Enum.GetValues<PlaceableAuthoredLockState>())
        {
            var placeable = Placeable(state);
            var resolution = adapter.Resolve(placeable);
            var runtime = adapter.Build(resolution.Handle!, placeable);

            Assert.True(resolution.Available);
            Assert.Equal(state.ToString(), runtime.State.ToString());
            Assert.Contains("item:20000000-0000-4000-8000-000000000001", runtime.Credentials);
            Assert.Equal("mechanics", runtime.SkillId);
            Assert.Equal(14, runtime.Difficulty);
            Assert.True(runtime.Hackable && runtime.Relockable && runtime.CanJam);
            Assert.Contains("lock.admin", runtime.Permissions);
        }
    }

    [Fact]
    public void Unlocks_by_credential_code_skill_hacking_permission_and_game_master()
    {
        var placeable = Placeable();
        var adapter = new PlaceableLockRuntimeAdapter();
        var handle = adapter.Resolve(placeable).Handle!;
        var attempts = new[]
        {
            Attempt(credentials: new HashSet<string>
                { "item:20000000-0000-4000-8000-000000000001" }),
            Attempt(codeHash: "sha256:test"),
            Attempt(skill: 14),
            Attempt(skill: 14, hacking: true),
            Attempt(permissions: new HashSet<string> { "lock.admin" }),
            Attempt(gameMaster: true),
        };

        var results = attempts.Select(attempt => adapter.PreviewUnlock(handle, placeable, attempt)).ToArray();

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Equal(["credential", "code", "skill", "hacking", "permission", "gm"],
            results.Select(result => result.Method));
    }

    [Fact]
    public void Relock_and_jam_obey_the_authored_policy()
    {
        var placeable = Placeable();
        var adapter = new PlaceableLockRuntimeAdapter();
        var handle = adapter.Resolve(placeable).Handle!;
        var opened = adapter.PreviewUnlock(handle, placeable, Attempt(gameMaster: true));
        var relocked = adapter.PreviewRelock(handle, placeable, opened.Lock, false, "player:test");
        var jammed = adapter.PreviewRelock(handle, placeable, opened.Lock, true, "player:test");

        Assert.True(relocked.Succeeded && jammed.Succeeded);
        Assert.Equal(LockState.Locked, relocked.Lock.State);
        Assert.Equal(LockState.Jammed, jammed.Lock.State);
        Assert.Equal("LockJammed", jammed.EventName);
    }

    [Fact]
    public void Resolves_optional_required_and_rejects_stale_handles()
    {
        var placeable = Placeable();
        var adapter = new PlaceableLockRuntimeAdapter();
        var handle = adapter.Resolve(placeable).Handle!;
        var optional = placeable with
        {
            Operations = placeable.Operations! with { Lock = null },
            Foundation = placeable.Foundation! with
            {
                Components = placeable.Foundation.Components with { HasLock = false },
            },
        };
        var required = optional with
        {
            Foundation = optional.Foundation! with
            {
                Components = optional.Foundation.Components with { HasLock = true },
            },
        };

        Assert.True(adapter.Resolve(optional).OptionalFallback);
        Assert.Equal("WORLD_PLACEABLE_LOCK_REQUIRED_MISSING", adapter.Resolve(required).DiagnosticCode);
        Assert.Throws<InvalidOperationException>(() => adapter.Build(
            handle with { LockId = Guid.NewGuid() }, placeable));
    }

    private static LockAttempt Attempt(
        IReadOnlySet<string>? credentials = null,
        IReadOnlySet<string>? permissions = null,
        string? codeHash = null,
        double skill = 0,
        bool hacking = false,
        bool gameMaster = false)
        => new("player:test", credentials ?? new HashSet<string>(), permissions ?? new HashSet<string>(),
            codeHash, skill, hacking, gameMaster);

    private static PlaceableWorldProfile Placeable(
        PlaceableAuthoredLockState state = PlaceableAuthoredLockState.Locked)
    {
        var foundation = new PlaceableWorldProfile.FoundationPolicy(
            Guid.Parse("20000000-0000-4000-8000-000000000010"),
            1,
            "test:ux1-p",
            "vault-lock",
            [new("fr-FR", "Coffre", "Déverrouiller")],
            ["lock"],
            "test",
            new("asset:vault", ["mesh:vault"], [], "asset:icon", "asset:portrait", []),
            new("box", new(1, 1, 1), true, true, "world"),
            null,
            null,
            new(true, false, true, false, false, new Dictionary<string, string>()),
            new(new(0, 0, 0), new(0, 0, 0), new(1, 1, 1), new(0, 0, 0), .25, null, true, null),
            new Dictionary<string, string>());
        var lockProfile = new PlaceableLockProfile(
            Guid.Parse("20000000-0000-4000-8000-000000000020"),
            state,
            ["item:20000000-0000-4000-8000-000000000001", "guild:watch"],
            "mechanics",
            14,
            "sha256:test",
            true,
            true,
            true,
            ["lock.admin"]);
        var operations = new PlaceableOperationsProfile(
            Guid.Parse("20000000-0000-4000-8000-000000000030"),
            1,
            "test:ux1-p",
            null,
            null,
            null,
            null,
            lockProfile);
        return new(
            Guid.Parse("20000000-0000-4000-8000-000000000040"),
            1,
            "test:ux1-p",
            BlueprintId.From(Guid.Parse("20000000-0000-4000-8000-000000000050")),
            null,
            null,
            null,
            new([], new(false, 1, 1, true, "placeable")),
            null,
            foundation,
            true,
            operations,
            true);
    }
}
