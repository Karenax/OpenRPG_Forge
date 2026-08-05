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

        Assert.Equal(RuntimeSaveValidator.InventoryDuplicateCode, Assert.Single(duplicate.Diagnostics).Code);
        Assert.Equal(RuntimeSaveValidator.WeatherInvalidCode, Assert.Single(weather.Diagnostics).Code);
        Assert.Equal(RuntimeSaveStore.SaveFailedCode, Assert.Single(extension.Diagnostics).Code);
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
            14.5));
}
