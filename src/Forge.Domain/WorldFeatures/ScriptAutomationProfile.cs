namespace Forge.Domain.WorldFeatures;

public sealed record ScriptAutomationProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    ScriptAutomationProfile.CameraSequencePolicy Camera,
    ScriptAutomationProfile.TagRoutingPolicy TagRouting,
    IReadOnlyList<ScriptAutomationProfile.FeatureFlag> ModuleFlags,
    IReadOnlyList<ScriptAutomationProfile.AiSetting> AiSettings)
{
    public enum FlagScope { Module, Area, Party, Player }
    public enum SettingKind { Boolean, Integer, Decimal, Text, Choice }

    public sealed record CameraShot(
        Guid Id,
        string RigId,
        string ShotId,
        double DurationSeconds,
        double BlendSeconds,
        bool LockPlayerControl,
        bool Skippable);

    public sealed record CameraSequencePolicy(
        IReadOnlyList<CameraShot> Shots,
        string AbortRecovery,
        bool RestorePlayerControl,
        bool AccessibilitySkipEnabled);

    public sealed record TagRoute(string BlueprintTag, string Event, string ScriptId, bool Enabled);

    public sealed record TagRoutingPolicy(bool Enabled, bool ExplicitOnly, IReadOnlyList<TagRoute> Routes);

    public sealed record FeatureFlag(
        string Id,
        SettingKind Kind,
        string DefaultValue,
        FlagScope Scope,
        string Documentation,
        int IntroducedSchemaVersion);

    public sealed record AiSetting(
        string Id,
        SettingKind Kind,
        string DefaultValue,
        IReadOnlyList<string> AllowedValues,
        string Preview,
        bool ServerAuthoritative);

    public static ScriptAutomationProfile CreateDefault(Guid id, string provenance) =>
        new(id, 1, provenance,
            new([], "restore-camera-and-control", true, true),
            new(false, true, []), [], []);
}
