using Forge.Domain.RuntimePolicies;

namespace Forge.Domain.Rules;

public enum SaveRestrictionPolicy { Unrestricted, CheckpointsOnly, Disabled }
public enum FriendlyFireMode { Disabled, PartyOnly, Enabled }

public sealed record DifficultyProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Key { get; }
    public string DisplayName { get; }
    public double PlayerDamageMultiplier { get; }
    public double IncomingDamageMultiplier { get; }
    public double AiIntensityMultiplier { get; }
    public double EconomyMultiplier { get; }
    public double RestRecoveryMultiplier { get; }
    public SaveRestrictionPolicy SaveRestrictions { get; }
    public FriendlyFireMode FriendlyFire { get; }
    public DifficultyAccessibilityOptions Accessibility { get; }
    public string Provenance { get; }

    public DifficultyProfile(Guid id, int schemaVersion, string key, string displayName,
        double playerDamageMultiplier, double incomingDamageMultiplier, double aiIntensityMultiplier,
        double economyMultiplier, double restRecoveryMultiplier, SaveRestrictionPolicy saveRestrictions,
        FriendlyFireMode friendlyFire, DifficultyAccessibilityOptions accessibility, string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(accessibility);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion ||
            !ValidMultiplier(playerDamageMultiplier) || !ValidMultiplier(incomingDamageMultiplier) ||
            !ValidMultiplier(aiIntensityMultiplier) || !ValidMultiplier(economyMultiplier) ||
            !ValidMultiplier(restRecoveryMultiplier) || !accessibility.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Difficulty profile is invalid.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Key = key.Trim().ToLowerInvariant();
        DisplayName = displayName.Trim();
        PlayerDamageMultiplier = playerDamageMultiplier;
        IncomingDamageMultiplier = incomingDamageMultiplier;
        AiIntensityMultiplier = aiIntensityMultiplier;
        EconomyMultiplier = economyMultiplier;
        RestRecoveryMultiplier = restRecoveryMultiplier;
        SaveRestrictions = saveRestrictions;
        FriendlyFire = friendlyFire;
        Accessibility = accessibility;
        Provenance = provenance.Trim();
    }

    internal static DifficultyProfile CreateDefault(Guid moduleId) => new(
        RuntimePolicyIdFactory.Create(moduleId, "rules:difficulty:standard"), 1, "standard", "Standard",
        1, 1, 1, 1, 1, SaveRestrictionPolicy.Unrestricted, FriendlyFireMode.Disabled,
        new DifficultyAccessibilityOptions(true, true, 1, 1), "migration-default");

    private static bool ValidMultiplier(double value) => double.IsFinite(value) && value is >= .05 and <= 20;
}

public sealed record DifficultyAccessibilityOptions(
    bool PauseAssist,
    bool TelegraphAssist,
    double TimingWindowMultiplier,
    double PerceptionCueMultiplier)
{
    internal bool IsValid => double.IsFinite(TimingWindowMultiplier) && TimingWindowMultiplier is >= .25 and <= 4 &&
                             double.IsFinite(PerceptionCueMultiplier) && PerceptionCueMultiplier is >= .25 and <= 4;
}
