using Forge.Domain.Validation;

namespace Forge.Domain.Assistant;

public sealed record AssistantTestAutomationProfile
{
    public const int CurrentSchemaVersion = 1;
    public static readonly Guid DefaultId = Guid.Parse("a1250000-0000-4000-8000-000000000001");
    public static readonly IReadOnlySet<string> SupportedGameplayActions = new HashSet<string>(
        ["dialogue.choose", "quest.raise_event", "player.move", "entity.interact"],
        StringComparer.Ordinal);

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public bool Enabled { get; }
    public bool RequiredAtRuntime { get; }
    public int MaximumScenarioSteps { get; }
    public int MaximumDialoguePaths { get; }
    public int MaximumQuestPaths { get; }
    public bool DetectToneInconsistencies { get; }
    public bool DetectRewardExploits { get; }
    public bool VerifyMultiplayerDeterminism { get; }
    public bool VerifySaveResume { get; }
    public IReadOnlyList<string> AllowedGameplayActions { get; }

    public AssistantTestAutomationProfile(
        Guid id,
        int schemaVersion,
        string provenance,
        bool enabled,
        bool requiredAtRuntime,
        int maximumScenarioSteps,
        int maximumDialoguePaths,
        int maximumQuestPaths,
        bool detectToneInconsistencies,
        bool detectRewardExploits,
        bool verifyMultiplayerDeterminism,
        bool verifySaveResume,
        IReadOnlyList<string>? allowedGameplayActions)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = (provenance ?? string.Empty).Trim();
        Enabled = enabled;
        RequiredAtRuntime = requiredAtRuntime;
        MaximumScenarioSteps = maximumScenarioSteps;
        MaximumDialoguePaths = maximumDialoguePaths;
        MaximumQuestPaths = maximumQuestPaths;
        DetectToneInconsistencies = detectToneInconsistencies;
        DetectRewardExploits = detectRewardExploits;
        VerifyMultiplayerDeterminism = verifyMultiplayerDeterminism;
        VerifySaveResume = verifySaveResume;
        AllowedGameplayActions = (allowedGameplayActions ?? [])
            .Select(action => action.Trim())
            .Where(action => action.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public static AssistantTestAutomationProfile CreateDefault() => new(
        DefaultId,
        CurrentSchemaVersion,
        "openrpg-forge:r120",
        true,
        false,
        128,
        1024,
        1024,
        true,
        true,
        true,
        true,
        SupportedGameplayActions.Order(StringComparer.Ordinal).ToArray());
}

public sealed record AssistantTestAutomationResolution(
    AssistantTestAutomationProfile? Profile,
    IReadOnlyList<DomainDiagnostic> Diagnostics)
{
    public bool Available => Profile is not null && Diagnostics.Count == 0;
}

public static class AssistantTestAutomationProfileValidator
{
    public const string MissingRequiredCode = "assistant.test.profile_required_missing";
    public const string SchemaUnsupportedCode = "assistant.test.profile_schema_unsupported";
    public const string IdentityInvalidCode = "assistant.test.profile_identity_invalid";
    public const string BoundsInvalidCode = "assistant.test.profile_bounds_invalid";
    public const string ActionInvalidCode = "assistant.test.profile_action_invalid";
    public const string RequiredDisabledCode = "assistant.test.profile_required_disabled";

    public static IReadOnlyList<DomainDiagnostic> Validate(AssistantTestAutomationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<DomainDiagnostic>();
        const string root = "assistant/testAutomation";
        if (profile.SchemaVersion != AssistantTestAutomationProfile.CurrentSchemaVersion)
            diagnostics.Add(new(SchemaUnsupportedCode, "Assistant test profile schema is unsupported.", $"{root}/schemaVersion"));
        if (profile.Id == Guid.Empty || string.IsNullOrWhiteSpace(profile.Provenance))
            diagnostics.Add(new(IdentityInvalidCode, "Assistant test profile UUID and provenance are required.", $"{root}/id"));
        if (profile.MaximumScenarioSteps is < 1 or > 512 ||
            profile.MaximumDialoguePaths is < 1 or > 4096 ||
            profile.MaximumQuestPaths is < 1 or > 4096)
            diagnostics.Add(new(BoundsInvalidCode, "Assistant test limits exceed their supported bounded scope.", $"{root}/limits"));
        if (profile.AllowedGameplayActions.Count == 0 ||
            profile.AllowedGameplayActions.Any(action => !AssistantTestAutomationProfile.SupportedGameplayActions.Contains(action)))
            diagnostics.Add(new(ActionInvalidCode, "Assistant tests may use only the supported gameplay action allowlist.", $"{root}/allowedGameplayActions"));
        if (profile.RequiredAtRuntime && !profile.Enabled)
            diagnostics.Add(new(RequiredDisabledCode, "A runtime-required assistant test profile cannot be disabled.", $"{root}/enabled"));
        return diagnostics;
    }

    public static AssistantTestAutomationResolution Resolve(
        AssistantTestAutomationProfile? profile,
        bool required)
    {
        if (profile is null)
            return required
                ? new(null, [new DomainDiagnostic(MissingRequiredCode,
                    "Required assistant test automation is unavailable.", "assistant/testAutomation")])
                : new(null, []);
        var diagnostics = Validate(profile);
        return profile.Enabled
            ? new(profile, diagnostics)
            : required
                ? new(null, diagnostics.Append(new DomainDiagnostic(MissingRequiredCode,
                    "Required assistant test automation is disabled.", "assistant/testAutomation/enabled")).ToArray())
                : new(null, diagnostics);
    }
}
