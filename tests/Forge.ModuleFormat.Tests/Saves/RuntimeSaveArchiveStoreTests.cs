using Forge.Domain.Gameplay;
using Forge.Domain.Modules;
using Forge.ModuleFormat.Saves;

namespace Forge.ModuleFormat.Tests.Saves;

public sealed class RuntimeSaveArchiveStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "openrpg-save-archive-tests", Guid.NewGuid().ToString("N"));
    private static readonly string ModuleId = "c7100000-0000-4000-8000-000000000300";
    private static readonly string ModuleHash = new('a', 64);
    private static readonly RuntimeSavePolicy Policy = GameplayFoundationProfile.CreateDefault(
        Forge.Domain.Modules.ModuleId.From(Guid.Parse(ModuleId))).Save with
    { ChunkBytes = 4096, RollbackSlots = 2 };

    [Fact]
    public void Archive_reuses_chunks_verifies_dependencies_and_loads_rollback()
    {
        var dependency = new RuntimeModuleDependency("c7100000-0000-4000-8000-000000000301", new string('b', 64));
        var first = RuntimeSaveArchiveStore.Save(_root, "quick", 1, CreateSave("active"), Policy, [dependency],
            DateTimeOffset.Parse("2026-08-07T10:00:00Z"));
        var second = RuntimeSaveArchiveStore.Save(_root, "quick", 2, CreateSave("complete"), Policy, [dependency],
            DateTimeOffset.Parse("2026-08-07T10:01:00Z"));

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(2, Directory.GetFiles(Path.Combine(_root, "slots", "quick"), "*.orpsnapshot").Length);
        var latest = RuntimeSaveArchiveStore.Load(_root, "quick", 0, ModuleId, ModuleHash, [dependency]);
        var rollback = RuntimeSaveArchiveStore.Load(_root, "quick", 1, ModuleId, ModuleHash, [dependency]);
        Assert.True(latest.Succeeded);
        Assert.Equal("complete", latest.Save!.Variables.Single().Value);
        Assert.Equal("active", rollback.Save!.Variables.Single().Value);
        Assert.Equal(64, second.Manifest!.PayloadSha256.Length);
    }

    [Fact]
    public void Archive_detects_corrupt_chunk_and_missing_dependency()
    {
        var dependency = new RuntimeModuleDependency("c7100000-0000-4000-8000-000000000302", new string('c', 64));
        var stored = RuntimeSaveArchiveStore.Save(_root, "auto", 1, CreateSave("active"), Policy, [dependency],
            DateTimeOffset.UnixEpoch);
        Assert.True(stored.Succeeded);
        var missing = RuntimeSaveArchiveStore.Load(_root, "auto", 0, ModuleId, ModuleHash, []);
        Assert.Equal(RuntimeSaveArchiveStore.DependencyMismatchCode, Assert.Single(missing.Diagnostics).Code);
        var chunk = Path.Combine(_root, "chunks", $"{stored.Manifest!.ChunkSha256[0]}.bin");
        File.WriteAllText(chunk, "corrupt");
        var corrupt = RuntimeSaveArchiveStore.Load(_root, "auto", 0, ModuleId, ModuleHash, [dependency]);
        Assert.Equal(RuntimeSaveArchiveStore.ChecksumMismatchCode, Assert.Single(corrupt.Diagnostics).Code);
    }

    [Fact]
    public void Archive_rotates_rollback_slots_and_evaluates_autosave_interval()
    {
        for (var revision = 1; revision <= 5; revision++)
            Assert.True(RuntimeSaveArchiveStore.Save(_root, "auto", revision, CreateSave(revision.ToString()), Policy,
                [], DateTimeOffset.UnixEpoch.AddMinutes(revision)).Succeeded);

        Assert.Equal(3, Directory.GetFiles(Path.Combine(_root, "slots", "auto"), "*.orpsnapshot").Length);
        Assert.False(RuntimeSaveArchiveStore.ShouldAutosave(Policy, DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddSeconds(30)));
        Assert.True(RuntimeSaveArchiveStore.ShouldAutosave(Policy, DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddSeconds(60)));
    }

    private static RuntimeSaveDocument CreateSave(string state) => new(
        RuntimeSaveDocument.CurrentFormatVersion, ModuleId, ModuleHash,
        "c7100000-0000-4000-8000-000000000310", DateTimeOffset.UnixEpoch,
        new(1, 2, 3), new("c7100000-0000-4000-8000-000000000311", 10, 4), [],
        [new("quest", state)], null, null, [], new(false, false, false, false, false), null, null, []);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
