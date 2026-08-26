using System.Text.Json;
using Forge.ModuleFormat.Saves;

namespace Forge.ModuleFormat.Tests.Saves;

public sealed class RuntimeMapExplorationSaveTests : IDisposable
{
    private const string ModuleId = "30000000-0000-4000-8000-000000000001";
    private const string ModuleHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "openrpg-forge-map-save-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Exploration_round_trips_and_legacy_two_dimensional_cells_migrate_to_floor_zero()
    {
        var canonicalPath = Path.Combine(_directory, "canonical.orpsave");
        var canonical = CreateSave(new RuntimeMapExplorationSave(
            "party",
            "party:test",
            1,
            ["v1:0:1:1", "v1:1:1:1"],
            ["a0290000-0000-4000-8000-000000000003"]));

        Assert.True(RuntimeSaveStore.Save(canonicalPath, canonical).Succeeded);
        var reopened = RuntimeSaveStore.Load(canonicalPath, ModuleId, ModuleHash);

        Assert.True(reopened.Succeeded);
        Assert.Equal(canonical.MapExploration!.Scope, reopened.Save!.MapExploration!.Scope);
        Assert.Equal(canonical.MapExploration.ExplorerId, reopened.Save.MapExploration.ExplorerId);
        Assert.Equal(canonical.MapExploration.ActiveFloor, reopened.Save.MapExploration.ActiveFloor);
        Assert.Equal(canonical.MapExploration.RevealedCells, reopened.Save.MapExploration.RevealedCells);
        Assert.Equal(canonical.MapExploration.RevealedPins, reopened.Save.MapExploration.RevealedPins);

        var legacyPath = Path.Combine(_directory, "legacy-2d.orpsave");
        var legacy = CreateSave(new RuntimeMapExplorationSave(
            "party",
            "party:test",
            0,
            ["0:0"],
            [])) with
        {
            FormatVersion = RuntimeSaveDocument.PreMapExplorationFormatVersion,
        };
        Directory.CreateDirectory(_directory);
        File.WriteAllText(legacyPath, JsonSerializer.Serialize(legacy,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        var migrated = RuntimeSaveStore.Load(legacyPath, ModuleId, ModuleHash);

        Assert.True(migrated.Succeeded);
        Assert.Equal(RuntimeSaveDocument.CurrentFormatVersion, migrated.Save!.FormatVersion);
        Assert.Equal(["v0:0:0:0"], migrated.Save.MapExploration!.RevealedCells);
        Assert.Equal(0, migrated.Save.MapExploration.ActiveFloor);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private static RuntimeSaveDocument CreateSave(RuntimeMapExplorationSave exploration) => new(
        RuntimeSaveDocument.CurrentFormatVersion,
        ModuleId,
        ModuleHash,
        "93b6f81b-49eb-4e3d-a156-08ad78529525",
        new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero),
        new RuntimeVector3(0, 0, 0),
        new RuntimeCombatantSave("20000000-0000-4000-8000-000000000010", 10, 5),
        [new RuntimeInventorySaveStack("ash-key", 1)],
        [],
        null,
        null,
        [],
        new RuntimeProgressFlags(false, false, false, false, false),
        null,
        null,
        [],
        null,
        [],
        null,
        exploration);
}
