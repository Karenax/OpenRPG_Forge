namespace Forge.ModuleFormat.Saves;

public sealed record RuntimeSaveDocument(
    string FormatVersion,
    string ModuleId,
    string ModuleContentSha256,
    string AreaId,
    DateTimeOffset SavedAtUtc,
    RuntimeVector3 PlayerPosition,
    RuntimeCombatantSave Player,
    IReadOnlyList<string> Inventory,
    IReadOnlyList<RuntimeKeyValue> Variables,
    string? QuestId,
    string? QuestStateId,
    IReadOnlyList<RuntimeCombatantSave> Hostiles,
    RuntimeProgressFlags Progress,
    RuntimeWeatherSave? Weather,
    RuntimeProgressionSave? Progression = null,
    IReadOnlyList<RuntimeEntityStateSave>? EntitySnapshots = null,
    RuntimeWorldClockSave? WorldClock = null)
{
    public const string CurrentFormatVersion = "1.4.0";
    public const string PreWorldClockFormatVersion = "1.3.0";
    public const string PreRuntimeExtensionsFormatVersion = "1.2.0";
    public const string PreRulesFormatVersion = "1.1.0";
}

public sealed record RuntimeVector3(double X, double Y, double Z);

public sealed record RuntimeCombatantSave(string EntityId, int Health, int Resource);

public sealed record RuntimeKeyValue(string Key, string Value);

public sealed record RuntimeWeatherSave(
    int SchemaVersion,
    string StateId,
    int Revision,
    long ServerRevision,
    double ElapsedSeconds);

public sealed record RuntimeProgressFlags(
    bool HasKey,
    bool DoorUnlocked,
    bool BossPhaseTwo,
    bool BossDefeated,
    bool Escaped);

public sealed record RuntimeProgressionSave(int SchemaVersion, double Experience, int Level);

public sealed record RuntimeWorldClockSave(
    int SchemaVersion,
    long TotalMinutes,
    bool Paused,
    long Revision,
    string Provenance);

public sealed record RuntimeEntityStateSave(
    string EntityId,
    int SchemaVersion,
    IReadOnlyList<RuntimeStateComponentSave> Components,
    IReadOnlyList<string> References,
    IReadOnlyList<string> Inventory,
    IReadOnlyList<RuntimeKeyValue> Blackboard);

public sealed record RuntimeStateComponentSave(string Key, string JsonValue);
