using Forge.ModuleFormat.Saves;

namespace Forge.ModuleFormat.Tests.Saves;

public sealed class RuntimeSaveStoreTests : IDisposable
{
    private const string ModuleId = "30000000-0000-4000-8000-000000000001";
    private const string ModuleHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "openrpg-forge-save-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveAndLoadRoundTripAtomically()
    {
        var path = Path.Combine(_directory, "checkpoint.orpsave");
        var save = CreateSave();

        var stored = RuntimeSaveStore.Save(path, save);
        var loaded = RuntimeSaveStore.Load(path, ModuleId, ModuleHash);

        Assert.True(stored.Succeeded);
        Assert.True(loaded.Succeeded);
        Assert.Equal(save.ModuleId, loaded.Save?.ModuleId);
        Assert.Equal(save.Player, loaded.Save?.Player);
        Assert.Equal(save.Inventory, loaded.Save?.Inventory);
        Assert.Equal(save.Variables, loaded.Save?.Variables);
        Assert.Equal(save.Hostiles, loaded.Save?.Hostiles);
        Assert.Equal(save.Progress, loaded.Save?.Progress);
        Assert.Equal(save.Weather, loaded.Save?.Weather);
        Assert.Equal(save.Progression, loaded.Save?.Progression);
        Assert.Equal(save.WorldClock, loaded.Save?.WorldClock);
        Assert.Equal(save.EntitySnapshots![0].EntityId, loaded.Save!.EntitySnapshots![0].EntityId);
        Assert.Equal(save.EntitySnapshots[0].Components, loaded.Save.EntitySnapshots[0].Components);
        Assert.Equal(save.EntitySnapshots[0].Inventory, loaded.Save.EntitySnapshots[0].Inventory);
        Assert.Equal(save.EntitySnapshots[0].Blackboard, loaded.Save.EntitySnapshots[0].Blackboard);
        Assert.Empty(Directory.EnumerateFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void LoadMigratesPreExtensionSaveToCanonicalEmptySnapshots()
    {
        var path = Path.Combine(_directory, "legacy.orpsave");
        var legacy = CreateSave() with
        {
            FormatVersion = RuntimeSaveDocument.PreRuntimeExtensionsFormatVersion,
            EntitySnapshots = null,
        };
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(legacy,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));

        var loaded = RuntimeSaveStore.Load(path, ModuleId, ModuleHash);

        Assert.True(loaded.Succeeded);
        Assert.Equal(RuntimeSaveDocument.CurrentFormatVersion, loaded.Save!.FormatVersion);
        Assert.Empty(loaded.Save.EntitySnapshots!);
    }

    [Fact]
    public void LoadMigratesBothSupportedLegacyVersions()
    {
        foreach (var version in new[]
                 {
                     RuntimeSaveDocument.PreWorldClockFormatVersion,
                     RuntimeSaveDocument.PreRulesFormatVersion,
                     RuntimeSaveDocument.PreRuntimeExtensionsFormatVersion,
                 })
        {
            var path = Path.Combine(_directory, $"legacy-{version}.orpsave");
            var legacy = CreateSave() with { FormatVersion = version, Progression = null, EntitySnapshots = null };
            Directory.CreateDirectory(_directory);
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(legacy,
                new System.Text.Json.JsonSerializerOptions
                { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));

            var loaded = RuntimeSaveStore.Load(path, ModuleId, ModuleHash);

            Assert.True(loaded.Succeeded);
            Assert.Equal(RuntimeSaveDocument.CurrentFormatVersion, loaded.Save!.FormatVersion);
            Assert.Empty(loaded.Save.EntitySnapshots!);
        }
    }

    [Fact]
    public void RepeatedSaveIsByteDeterministic()
    {
        var path = Path.Combine(_directory, "deterministic.orpsave");
        Assert.True(RuntimeSaveStore.Save(path, CreateSave()).Succeeded);
        var first = File.ReadAllBytes(path);

        Assert.True(RuntimeSaveStore.Save(path, CreateSave()).Succeeded);

        Assert.Equal(first, File.ReadAllBytes(path));
        Assert.False(first.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
    }

    [Fact]
    public void R052WorldClockSaveRoundTripsAndLegacySaveKeepsOptionalClockEmpty()
    {
        var path = Path.Combine(_directory, "r052-world-clock.orpsave");
        var save = CreateSave();
        Assert.True(RuntimeSaveStore.Save(path, save).Succeeded);
        Assert.Equal(save.WorldClock, RuntimeSaveStore.Load(path, ModuleId, ModuleHash).Save!.WorldClock);

        var legacyPath = Path.Combine(_directory, "r052-legacy.orpsave");
        var legacy = save with { FormatVersion = RuntimeSaveDocument.PreWorldClockFormatVersion, WorldClock = null };
        Directory.CreateDirectory(_directory);
        File.WriteAllText(legacyPath, System.Text.Json.JsonSerializer.Serialize(legacy,
            new System.Text.Json.JsonSerializerOptions
            { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));
        var migrated = RuntimeSaveStore.Load(legacyPath, ModuleId, ModuleHash);
        Assert.True(migrated.Succeeded);
        Assert.Null(migrated.Save!.WorldClock);
        Assert.Equal(RuntimeSaveDocument.CurrentFormatVersion, migrated.Save.FormatVersion);
    }

    [Fact]
    public void FailedReplacementPreservesPreviousSaveAndCleansTemporaryFile()
    {
        var path = Path.Combine(_directory, "protected.orpsave");
        var original = CreateSave();
        Assert.True(RuntimeSaveStore.Save(path, original).Succeeded);
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var changed = original with { Inventory = ["ash-key", "new-item"] };
            var failed = RuntimeSaveStore.Save(path, changed);
            Assert.False(failed.Succeeded);
            Assert.Equal(RuntimeSaveStore.SaveFailedCode, Assert.Single(failed.Diagnostics).Code);
        }

        var loaded = RuntimeSaveStore.Load(path, ModuleId, ModuleHash);
        Assert.True(loaded.Succeeded);
        Assert.Equal(original.Inventory, loaded.Save!.Inventory);
        Assert.Empty(Directory.EnumerateFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void LoadRejectsDifferentModuleOrContentRevision()
    {
        var path = Path.Combine(_directory, "checkpoint.orpsave");
        Assert.True(RuntimeSaveStore.Save(path, CreateSave()).Succeeded);

        var moduleMismatch = RuntimeSaveStore.Load(
            path,
            "30000000-0000-4000-8000-000000000099",
            ModuleHash);
        var hashMismatch = RuntimeSaveStore.Load(path, ModuleId, new string('a', 64));

        Assert.Equal(RuntimeSaveStore.ModuleMismatchCode, Assert.Single(moduleMismatch.Diagnostics).Code);
        Assert.Equal(RuntimeSaveStore.ContentMismatchCode, Assert.Single(hashMismatch.Diagnostics).Code);
    }

    [Fact]
    public void SaveRejectsDuplicateStateAndUnsafeExtension()
    {
        var invalid = CreateSave() with { Inventory = ["ash-key", "ash-key"] };
        var invalidWeather = CreateSave() with
        {
            Weather = new RuntimeWeatherSave(
                2,
                "6f550000-0000-4000-8000-000000000002",
                0,
                0,
                0),
        };
        var duplicate = RuntimeSaveStore.Save(Path.Combine(_directory, "invalid.orpsave"), invalid);
        var weather = RuntimeSaveStore.Save(Path.Combine(_directory, "invalid-weather.orpsave"), invalidWeather);
        var extension = RuntimeSaveStore.Save(Path.Combine(_directory, "invalid.json"), CreateSave());
        var invalidSnapshot = CreateSave() with
        {
            EntitySnapshots = [new RuntimeEntityStateSave("not-an-id", 1,
                [new RuntimeStateComponentSave("transform", "not-json")], [], [], [])],
        };
        var snapshot = RuntimeSaveStore.Save(Path.Combine(_directory, "invalid-snapshot.orpsave"), invalidSnapshot);

        Assert.Equal(RuntimeSaveValidator.InventoryDuplicateCode, Assert.Single(duplicate.Diagnostics).Code);
        Assert.Equal(RuntimeSaveValidator.WeatherInvalidCode, Assert.Single(weather.Diagnostics).Code);
        Assert.Equal(RuntimeSaveStore.SaveFailedCode, Assert.Single(extension.Diagnostics).Code);
        Assert.Equal(RuntimeSaveValidator.EntitySnapshotInvalidCode, Assert.Single(snapshot.Diagnostics).Code);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static RuntimeSaveDocument CreateSave() => new(
        RuntimeSaveDocument.CurrentFormatVersion,
        ModuleId,
        ModuleHash,
        "93b6f81b-49eb-4e3d-a156-08ad78529525",
        new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero),
        new RuntimeVector3(1, 2, 3),
        new RuntimeCombatantSave("20000000-0000-4000-8000-000000000010", 9, 4),
        ["ash-key"],
        [new RuntimeKeyValue("quest.accepted", "true")],
        "50000000-0000-4000-8000-000000000001",
        "51000000-0000-4000-8000-000000000003",
        [new RuntimeCombatantSave("20000000-0000-4000-8000-000000000009", 3, 6)],
        new RuntimeProgressFlags(true, true, true, false, false),
        new RuntimeWeatherSave(
            1,
            "6f550000-0000-4000-8000-000000000002",
            3,
            7,
            14.5),
        new RuntimeProgressionSave(1, 240, 2),
        [new RuntimeEntityStateSave("20000000-0000-4000-8000-000000000010", 1,
            [new RuntimeStateComponentSave("transform", "{\"x\":1,\"y\":2,\"z\":3}")], [],
            ["ash-key"], [new RuntimeKeyValue("quest.accepted", "true")])],
        new RuntimeWorldClockSave(1, 720, false, 12, "runtime:r052"));
}
