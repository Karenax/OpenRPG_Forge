using Forge.RpgKit.Domain.Items;
using Forge.RpgKit.Domain.Placeables;

namespace Forge.RpgKit.Domain.Tests.Placeables;

public sealed class PlaceableSecurityServiceTests
{
    [Fact]
    public void Protects_actions_and_audits_game_master_override()
    {
        var policy = new PlaceableProtectionPolicy(new HashSet<string> { "destroy", "move" }, new HashSet<string> { "placeable.plot.override" }, "Cet objet appartient à l’histoire.", true);
        var blocked = PlaceableSecurityService.Authorize(policy, "destroy", new HashSet<string>(), false, "player", ""); Assert.False(blocked.Allowed); Assert.NotEmpty(blocked.Feedback);
        var allowed = PlaceableSecurityService.Authorize(policy, "destroy", new HashSet<string>(), true, "gm:karen", "restore module"); Assert.True(allowed.Allowed); Assert.Contains("restore module", allowed.AuditEntry);
    }

    [Fact]
    public void Resolves_lock_credentials_skill_code_hacking_relock_and_jam()
    {
        var component = new LockComponent(LockState.Locked, new HashSet<string> { "key:lighthouse" }, "mechanics", 12, "hash:042", true, true, true, new HashSet<string> { "lock.admin" });
        var opened = PlaceableSecurityService.AttemptUnlock(component, new("ilyne", new HashSet<string> { "key:lighthouse" }, new HashSet<string>(), null, 0, false, false)); Assert.True(opened.Succeeded); Assert.Equal("credential", opened.Method);
        var jammed = PlaceableSecurityService.Relock(opened.Lock, true, "ilyne"); Assert.True(jammed.Succeeded); Assert.Equal(LockState.Jammed, jammed.Lock.State);
        var hacked = PlaceableSecurityService.AttemptUnlock(component, new("player", new HashSet<string>(), new HashSet<string>(), null, 13, true, false)); Assert.True(hacked.Succeeded); Assert.Equal("hacking", hacked.Method);
    }

    [Fact]
    public void Detects_disarms_triggers_resets_trap_and_preserves_seeded_loot()
    {
        var trap = new TrapComponent(Guid.NewGuid(), "open", "damage:glass", 10, 12, TimeSpan.FromMinutes(1), "glass-faction", "vfx:shards", "village", true, true);
        Assert.False(PlaceableSecurityService.Detect(trap, 9, "outsider").Succeeded); Assert.True(PlaceableSecurityService.Detect(trap, 10, "outsider").Succeeded);
        var disarmed = PlaceableSecurityService.Disarm(trap, 13); Assert.False(disarmed.Trap.Armed); Assert.True(PlaceableSecurityService.Reset(disarmed.Trap).Armed);
        var triggered = PlaceableSecurityService.Trigger(trap, "outsider"); Assert.True(triggered.Succeeded); Assert.False(triggered.Trap.Armed);
        var item = Guid.NewGuid(); var table = new LootTable(Guid.NewGuid(), 1, "author:test", [new(item, "relic", 1, 1, 1, 1, "", true)]); var spawner = new LootSpawner(table.Id, LootSpawnTiming.Open, 42, true, [], 0);
        var first = PlaceableSecurityService.Spawn(spawner, table, LootSpawnTiming.Open, new HashSet<string>()); var restored = PlaceableSecurityService.Spawn(first.Spawner, table, LootSpawnTiming.Open, new HashSet<string>());
        Assert.False(first.ReusedSavedState); Assert.True(restored.ReusedSavedState); Assert.Equal(first.Results, restored.Results);
    }

    [Fact]
    public void Emits_open_and_close_events_with_state_authority_and_result()
    {
        var id = Guid.NewGuid(); var opened = PlaceableSecurityService.ContainerEvent(true, id, "player", "success", "closed", "server", "runtime:test", DateTimeOffset.UnixEpoch);
        var closed = PlaceableSecurityService.ContainerEvent(false, id, "player", "success", "open", "server", "runtime:test", DateTimeOffset.UnixEpoch);
        Assert.Equal("ContainerOpened", opened.Name); Assert.Equal("closed", opened.PreviousState); Assert.Equal("server", opened.Authority); Assert.Equal("ContainerClosed", closed.Name);
    }
}
