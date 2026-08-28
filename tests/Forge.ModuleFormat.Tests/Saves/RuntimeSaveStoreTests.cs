using System.Text.Json;
using System.Text.Json.Nodes;
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
    public void Inventory_stack_quantities_round_trip_and_snapshot_budget_counts_stacks_not_units()
    {
        var path = Path.Combine(_directory, "inventory-stacks.orpsave");
        var save = CreateSave() with
        {
            Inventory =
            [
                new RuntimeInventorySaveStack("governed-item", 5),
                new RuntimeInventorySaveStack("unknown-alpha", 1),
                new RuntimeInventorySaveStack("unknown-beta", 1),
            ],
            EntitySnapshots =
            [
                CreateSave().EntitySnapshots![0] with
                {
                    Inventory = [new RuntimeInventorySaveStack("bulk-stack", 5000)],
                },
            ],
        };

        var stored = RuntimeSaveStore.Save(path, save);
        var loaded = RuntimeSaveStore.Load(path, ModuleId, ModuleHash);

        Assert.True(stored.Succeeded);
        Assert.True(loaded.Succeeded);
        Assert.Equal(RuntimeSaveDocument.CurrentFormatVersion, loaded.Save!.FormatVersion);
        Assert.Equal(save.Inventory, loaded.Save.Inventory);
        Assert.Equal(5, loaded.Save.Inventory.Single(stack => stack.Slug == "governed-item").Quantity);
        Assert.Equal(3, loaded.Save.Inventory.Count);
        var snapshotStack = Assert.Single(Assert.Single(loaded.Save.EntitySnapshots!).Inventory);
        Assert.Equal(5000, snapshotStack.Quantity);
    }

    [Fact]
    public void Quest_states_acceptance_and_tracking_round_trip_without_collapsing_other_quests()
    {
        var path = Path.Combine(_directory, "quest-states.orpsave");
        var trackedQuestId = "50000000-0000-4000-8000-000000000002";
        var save = CreateSave() with
        {
            QuestId = null,
            QuestStateId = null,
            QuestStates =
            [
                new("50000000-0000-4000-8000-000000000001",
                    "51000000-0000-4000-8000-000000000001", IsAccepted: false),
                new(trackedQuestId, "51000000-0000-4000-8000-000000000002", IsAccepted: true),
                new("50000000-0000-4000-8000-000000000003",
                    "51000000-0000-4000-8000-000000000003", IsAccepted: true),
                new("50000000-0000-4000-8000-000000000004",
                    "51000000-0000-4000-8000-000000000004", IsAccepted: true),
            ],
            TrackedQuestId = trackedQuestId,
        };

        Assert.True(RuntimeSaveStore.Save(path, save).Succeeded);
        var loaded = RuntimeSaveStore.Load(path, ModuleId, ModuleHash);

        Assert.True(loaded.Succeeded);
        Assert.Equal(save.QuestStates, loaded.Save!.QuestStates);
        Assert.Equal(trackedQuestId, loaded.Save.TrackedQuestId);
        Assert.False(loaded.Save.QuestStates![0].IsAccepted);
        Assert.Equal(4, loaded.Save.QuestStates.Count);
    }

    [Fact]
    public void Load_migrates_the_legacy_single_quest_and_its_acceptance_to_the_typed_collection()
    {
        var path = Path.Combine(_directory, "legacy-single-quest.orpsave");
        var legacy = CreateSave() with
        {
            FormatVersion = RuntimeSaveDocument.PreQuestCollectionFormatVersion,
            QuestStates = null,
            TrackedQuestId = null,
        };
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, JsonSerializer.Serialize(legacy,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        var loaded = RuntimeSaveStore.Load(path, ModuleId, ModuleHash);

        Assert.True(loaded.Succeeded);
        var state = Assert.Single(loaded.Save!.QuestStates!);
        Assert.Equal(legacy.QuestId, state.QuestId);
        Assert.Equal(legacy.QuestStateId, state.StateId);
        Assert.True(state.IsAccepted);
        Assert.Equal(legacy.QuestId, loaded.Save.TrackedQuestId);
        Assert.Equal(RuntimeSaveDocument.CurrentFormatVersion, loaded.Save!.FormatVersion);
    }

    [Fact]
    public void Save_and_load_reject_duplicate_tracked_quest_states_without_throwing()
    {
        const string questId = "50000000-0000-4000-8000-000000000010";
        var invalid = CreateSave() with
        {
            QuestStates =
            [
                new RuntimeQuestSaveState(questId, "51000000-0000-4000-8000-000000000010", true),
                new RuntimeQuestSaveState(
                    Guid.Parse(questId).ToString("B"),
                    "51000000-0000-4000-8000-000000000011",
                    true),
            ],
            TrackedQuestId = questId,
        };
        RuntimeSaveOperationResult? rejectedSave = null;
        var saveException = Record.Exception(() => rejectedSave = RuntimeSaveStore.Save(
            Path.Combine(_directory, "duplicate-tracked-save.orpsave"), invalid));

        var loadPath = Path.Combine(_directory, "duplicate-tracked-load.orpsave");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(loadPath, JsonSerializer.Serialize(invalid,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        RuntimeSaveOperationResult? rejectedLoad = null;
        var loadException = Record.Exception(() => rejectedLoad = RuntimeSaveStore.Load(
            loadPath, ModuleId, ModuleHash));

        Assert.Null(saveException);
        Assert.Null(loadException);
        Assert.Contains(rejectedSave!.Diagnostics,
            diagnostic => diagnostic.Code == RuntimeSaveValidator.QuestReferenceInvalidCode);
        Assert.Contains(rejectedLoad!.Diagnostics,
            diagnostic => diagnostic.Code == RuntimeSaveValidator.QuestReferenceInvalidCode);
    }

    [Fact]
    public void Load_migrates_legacy_inventory_slug_arrays_to_quantity_one()
    {
        var path = Path.Combine(_directory, "legacy-inventory.orpsave");
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var document = JsonNode.Parse(JsonSerializer.Serialize(CreateSave(), options))!.AsObject();
        document["formatVersion"] = RuntimeSaveDocument.PreInventoryStacksFormatVersion;
        document["inventory"] = new JsonArray(JsonValue.Create("ash-key"), JsonValue.Create("legacy-token"));
        document["entitySnapshots"]![0]!["inventory"] =
            new JsonArray(JsonValue.Create("ash-key"), JsonValue.Create("legacy-token"));
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, document.ToJsonString(options));

        var loaded = RuntimeSaveStore.Load(path, ModuleId, ModuleHash);

        Assert.True(loaded.Succeeded);
        Assert.Equal(RuntimeSaveDocument.CurrentFormatVersion, loaded.Save!.FormatVersion);
        Assert.Equal(
            [new RuntimeInventorySaveStack("ash-key", 1), new RuntimeInventorySaveStack("legacy-token", 1)],
            loaded.Save.Inventory);
        Assert.All(Assert.Single(loaded.Save.EntitySnapshots!).Inventory,
            stack => Assert.Equal(1, stack.Quantity));
    }

    [Fact]
    public void Save_and_load_reject_invalid_or_stale_inventory_quantities()
    {
        var invalid = CreateSave() with { Inventory = [new RuntimeInventorySaveStack("ash-key", 0)] };
        var rejectedSave = RuntimeSaveStore.Save(Path.Combine(_directory, "invalid-quantity.orpsave"), invalid);

        var stalePath = Path.Combine(_directory, "stale-quantity.orpsave");
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var document = JsonNode.Parse(JsonSerializer.Serialize(CreateSave(), options))!.AsObject();
        document["inventory"] = new JsonArray(new JsonObject { ["slug"] = "ash-key" });
        Directory.CreateDirectory(_directory);
        File.WriteAllText(stalePath, document.ToJsonString(options));
        var rejectedLoad = RuntimeSaveStore.Load(stalePath, ModuleId, ModuleHash);

        Assert.Equal(RuntimeSaveValidator.InventoryQuantityInvalidCode,
            Assert.Single(rejectedSave.Diagnostics).Code);
        Assert.Equal(RuntimeSaveValidator.InventoryQuantityInvalidCode,
            Assert.Single(rejectedLoad.Diagnostics).Code);
    }

    [Fact]
    public void Load_rejects_unknown_future_save_version_without_migrating_it()
    {
        var path = Path.Combine(_directory, "future-version.orpsave");
        var future = CreateSave() with { FormatVersion = "99.0.0" };
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, JsonSerializer.Serialize(future,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        var loaded = RuntimeSaveStore.Load(path, ModuleId, ModuleHash);

        Assert.False(loaded.Succeeded);
        Assert.Equal(RuntimeSaveValidator.VersionUnsupportedCode, Assert.Single(loaded.Diagnostics).Code);
    }

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
    public void SaveAndLoadAllowAPlayerWithoutACombatComponent()
    {
        var path = Path.Combine(_directory, "non-combat-player.orpsave");
        var save = CreateSave() with { Player = null! };

        var stored = RuntimeSaveStore.Save(path, save);
        var loaded = RuntimeSaveStore.Load(path, ModuleId, ModuleHash);

        Assert.True(stored.Succeeded);
        Assert.True(loaded.Succeeded);
        Assert.Null(loaded.Save!.Player);
    }

    [Fact]
    public void SaveAndLoadRoundTripIndependentAreaStates()
    {
        const string secondAreaId = "93b6f81b-49eb-4e3d-a156-08ad78529526";
        var path = Path.Combine(_directory, "multi-area.orpsave");
        var save = CreateSave();
        var currentAreaState = new RuntimeAreaSaveState(
            save.AreaId,
            save.Hostiles,
            save.Weather,
            save.EntitySnapshots!,
            save.MapExploration);
        var secondAreaState = new RuntimeAreaSaveState(
            secondAreaId,
            [],
            new RuntimeWeatherSave(1, "6f550000-0000-4000-8000-000000000003", 8, 12, 42),
            [],
            null);
        save = save with { AreaStates = [currentAreaState, secondAreaState] };

        Assert.True(RuntimeSaveStore.Save(path, save).Succeeded);
        var loaded = RuntimeSaveStore.Load(path, ModuleId, ModuleHash);

        Assert.True(loaded.Succeeded);
        Assert.Equal(2, loaded.Save!.AreaStates!.Count);
        var loadedSecondArea = loaded.Save.AreaStates.Single(state => state.AreaId == secondAreaId);
        Assert.Empty(loadedSecondArea.Hostiles);
        Assert.Empty(loadedSecondArea.EntitySnapshots);
        Assert.Equal(secondAreaState.Weather, loadedSecondArea.Weather);
        Assert.Null(loadedSecondArea.MapExploration);
    }

    [Fact]
    public void LoadMigratesVersionOneSevenCurrentAreaStateIntoTheAreaCollection()
    {
        var path = Path.Combine(_directory, "legacy-area-state.orpsave");
        var legacy = CreateSave() with
        {
            FormatVersion = RuntimeSaveDocument.PreAreaStateCollectionFormatVersion,
            AreaStates = null,
        };
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, JsonSerializer.Serialize(legacy,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        var loaded = RuntimeSaveStore.Load(path, ModuleId, ModuleHash);

        Assert.True(loaded.Succeeded);
        var areaState = Assert.Single(loaded.Save!.AreaStates!);
        Assert.Equal(legacy.AreaId, areaState.AreaId);
        Assert.Equal(legacy.Hostiles, areaState.Hostiles);
        Assert.Equal(legacy.Weather, areaState.Weather);
        Assert.Equal(legacy.EntitySnapshots!.Select(item => item.EntityId),
            areaState.EntitySnapshots.Select(item => item.EntityId));
        Assert.Equal(RuntimeSaveDocument.CurrentFormatVersion, loaded.Save.FormatVersion);
    }

    [Fact]
    public void SaveRejectsDuplicateAreaStates()
    {
        var save = CreateSave();
        var areaState = new RuntimeAreaSaveState(
            save.AreaId,
            save.Hostiles,
            save.Weather,
            save.EntitySnapshots!,
            save.MapExploration);

        var result = RuntimeSaveStore.Save(
            Path.Combine(_directory, "duplicate-area.orpsave"),
            save with { AreaStates = [areaState, areaState] });

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, item => item.Code == RuntimeSaveValidator.AreaStateInvalidCode);
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
