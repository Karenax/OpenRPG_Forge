using System.Globalization;
using System.Text.RegularExpressions;
using Forge.Domain.Lifecycle;

namespace Forge.Domain.Creatures;

public static partial class CreatureEventAiCompositionValidator
{
    public const string MissingRequiredCode = "creature.event_ai.required_missing";

    public static IReadOnlyList<CreatureFoundationDiagnostic> Validate(
        CreatureEventAiCompositionProfile profile,
        string path = "creatureFoundation/eventAiComposition")
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<CreatureFoundationDiagnostic>();
        Add(profile.Id == Guid.Empty, "creature.event_ai.uuid_required", path,
            "A stable creature event/AI UUID is required.");
        Add(profile.SchemaVersion != CreatureEventAiCompositionProfile.CurrentSchemaVersion,
            "creature.event_ai.schema_unsupported", path, "The creature event/AI schema is unsupported.");
        Add(string.IsNullOrWhiteSpace(profile.Provenance), "creature.event_ai.provenance_required", path,
            "Creature event/AI provenance is required.");
        Add(!Valid(profile.AssetId), "creature.event_ai.asset_invalid", $"{path}/assetId",
            "Creature event/AI composition requires a canonical asset identity.");

        var componentIds = EnumerateComponentIds(profile).ToArray();
        Add(componentIds.Any(id => id == Guid.Empty) || componentIds.Distinct().Count() != componentIds.Length,
            "creature.event_ai.component_uuid_invalid", path,
            "Event/AI profile, event groups, bindings, controller and blackboard entries require unique UUIDs.");

        ValidateBinding(profile.Perception.Detected, "creature.stimulus_detected",
            $"{path}/perception/detected", "OnPerceptionDetected");
        ValidateBinding(profile.Perception.Lost, "creature.stimulus_lost",
            $"{path}/perception/lost", "OnPerceptionLost");
        ValidateBinding(profile.PhysicalAttack.Declared, "creature.attack_declared",
            $"{path}/physicalAttack/declared", "BeforePhysicalAttacked");
        ValidateBinding(profile.PhysicalAttack.Resolved, "creature.attack_resolved",
            $"{path}/physicalAttack/resolved", "OnPhysicalAttacked");
        ValidateBinding(profile.Ability.Targeted, "creature.ability_targeted",
            $"{path}/ability/targeted", "BeforeAbilityTargeted");
        ValidateBinding(profile.Ability.Impacted, "creature.ability_impacted",
            $"{path}/ability/impacted", "OnSpellCastAt");
        ValidateBinding(profile.Blocked.Blocked, "creature.movement_blocked",
            $"{path}/blocked/event", "OnBlocked");

        Add(profile.Blocked.RecoveryOrder.Count is < 1 or > 5 ||
            profile.Blocked.RecoveryOrder.Any(item => !Enum.IsDefined(item)),
            "creature.event_ai.recovery_invalid", $"{path}/blocked/recoveryOrder",
            "Movement recovery requires one to five unique supported strategies.");

        var controller = profile.Controller;
        Add(!Enum.IsDefined(controller.Kind) || !Valid(controller.DefinitionId) ||
            controller.AllowedActions.Count is < 1 or > 64 || controller.AllowedActions.Any(item => !Valid(item)) ||
            controller.Blackboard.Count > 64 || HasDuplicate(controller.Blackboard.Select(item => item.Key)) ||
            controller.Blackboard.Any(item => !Valid(item.Key) || !Enum.IsDefined(item.Kind) ||
                !ValidBlackboardValue(item.Kind, item.Value)),
            "creature.event_ai.controller_invalid", $"{path}/controller/{controller.Id}",
            "AI controller requires a supported kind, definition, typed blackboard and bounded actions.");
        Add(controller.Kind == CreatureEventAiControllerKind.Script && !HandlerPattern().IsMatch(controller.DefinitionId),
            "creature.event_ai.script_controller_invalid", $"{path}/controller/definitionId",
            "Script AI controllers require a canonical script definition identifier.");
        Add(profile.Overrides.Any(item => !Valid(item.Key) || string.IsNullOrWhiteSpace(item.Value)),
            "creature.event_ai.override_invalid", $"{path}/overrides",
            "Creature event/AI overrides require canonical keys and explicit values.");
        return diagnostics;

        void Add(bool condition, string code, string diagnosticPath, string message)
        {
            if (condition) diagnostics.Add(new(code, diagnosticPath, message));
        }

        void ValidateBinding(EventBinding binding, string eventName, string bindingPath, string fallbackHandler)
        {
            Add(binding.SchemaVersion != 1 || string.IsNullOrWhiteSpace(binding.Provenance) ||
                binding.ForgeScriptApiVersion != "1.0" || binding.EventName != eventName ||
                !HandlerPattern().IsMatch(binding.Handler) || binding.Authority != EventAuthorityScope.Server,
                "creature.event_ai.binding_invalid", $"{bindingPath}/{binding.Id}",
                $"{eventName} requires schema v1, ForgeScript 1.0, server authority and a handler such as {fallbackHandler}.");
        }
    }

    public static CreatureEventAiDecision Decide(
        CreatureEventAiCompositionProfile profile,
        IReadOnlyDictionary<string, double>? utilityScores = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = Validate(profile);
        if (diagnostics.Count > 0) throw new InvalidOperationException(diagnostics[0].Code);
        var scores = utilityScores ?? new Dictionary<string, double>();
        var controller = profile.Controller;
        var blackboard = controller.Blackboard.ToDictionary(item => item.Key, StringComparer.Ordinal);
        var action = controller.Kind switch
        {
            CreatureEventAiControllerKind.Utility => controller.AllowedActions
                .OrderByDescending(candidate => scores.GetValueOrDefault(candidate))
                .ThenBy(candidate => candidate, StringComparer.Ordinal).First(),
            CreatureEventAiControllerKind.StateMachine when blackboard.TryGetValue("state", out var state) &&
                controller.AllowedActions.Contains(state.Value, StringComparer.Ordinal) => state.Value,
            CreatureEventAiControllerKind.Script => controller.DefinitionId,
            _ => controller.AllowedActions[0],
        };
        return new(action, scores.GetValueOrDefault(action), blackboard);
    }

    public static IEnumerable<Guid> EnumerateComponentIds(CreatureEventAiCompositionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        yield return profile.Id;
        yield return profile.Perception.Id;
        yield return profile.Perception.Detected.Id;
        yield return profile.Perception.Lost.Id;
        yield return profile.PhysicalAttack.Id;
        yield return profile.PhysicalAttack.Declared.Id;
        yield return profile.PhysicalAttack.Resolved.Id;
        yield return profile.Ability.Id;
        yield return profile.Ability.Targeted.Id;
        yield return profile.Ability.Impacted.Id;
        yield return profile.Blocked.Id;
        yield return profile.Blocked.Blocked.Id;
        yield return profile.Controller.Id;
        foreach (var item in profile.Controller.Blackboard) yield return item.Id;
    }

    private static bool ValidBlackboardValue(CreatureEventAiBlackboardValueKind kind, string value) => kind switch
    {
        CreatureEventAiBlackboardValueKind.Boolean => bool.TryParse(value, out _),
        CreatureEventAiBlackboardValueKind.Integer => long.TryParse(value, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out _),
        CreatureEventAiBlackboardValueKind.Number => double.TryParse(value, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var number) && double.IsFinite(number),
        CreatureEventAiBlackboardValueKind.Vector3 => ParseVector(value),
        CreatureEventAiBlackboardValueKind.Entity => Valid(value),
        CreatureEventAiBlackboardValueKind.Text => !string.IsNullOrWhiteSpace(value) && value.Length <= 512,
        _ => false,
    };

    private static bool ParseVector(string value)
    {
        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        return parts.Length == 3 && parts.All(part => double.TryParse(part, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var number) && double.IsFinite(number));
    }

    private static bool Valid(string value) => CreaturePresentationProfile.IsCanonicalIdentifier(value);

    private static bool HasDuplicate<T>(IEnumerable<T> values) where T : notnull
    {
        var items = values.ToArray();
        return items.Distinct().Count() != items.Length;
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_.-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex HandlerPattern();
}

public sealed record CreatureEventAiCompositionRuntimeResult(
    CreatureEventAiCompositionProfile? Profile,
    bool UsedFallback,
    IReadOnlyList<CreatureFoundationDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}

public static class CreatureEventAiCompositionRuntimeResolver
{
    public static CreatureEventAiCompositionRuntimeResult Resolve(
        CreatureEventAiCompositionProfile? profile,
        bool required)
    {
        if (profile is null)
            return new(null, !required, required
                ? [new CreatureFoundationDiagnostic(CreatureEventAiCompositionValidator.MissingRequiredCode,
                    "creatureFoundation/eventAiComposition", "A required creature event/AI profile is missing.")]
                : []);
        var diagnostics = CreatureEventAiCompositionValidator.Validate(profile);
        return new(diagnostics.Count == 0 ? profile : null, false, diagnostics);
    }
}
