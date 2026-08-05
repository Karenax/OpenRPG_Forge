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
    RuntimeWeatherSave? Weather)
{
    public const string CurrentFormatVersion = "1.1.0";
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
