using System.Text.Json;
using Forge.Domain.Blueprints;
using Forge.Domain.Combat;

namespace Forge.RpgKit.Domain.Abilities;

public enum AbilityTestKind
{
    ValidTarget,
    InvalidTarget,
    ResourceCost,
    Cooldown,
    Effect,
}

public sealed record AbilityTestScenario(Guid Id, AbilityTestKind Kind, bool Passed, string Evidence);

public sealed record AbilityBalanceReport(
    double PowerScore,
    int TotalMagnitude,
    int ResourceCost,
    double CooldownSeconds,
    bool Passed,
    string Notes);

public sealed record AbilityOrchestrationDefinition(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    BlueprintId OwnerBlueprintId,
    AbilityDefinition Ability,
    string ScriptPath,
    string? RequiredItemSlug,
    string AudioCueKey,
    IReadOnlySet<string> AiTags,
    string BalanceIntent);

public sealed record AbilityDiagnostic(string Code, string Path, string Message);

public static class AbilityOrchestrationService
{
    public const int CurrentSchemaVersion = 1;

    public static IReadOnlyList<AbilityDiagnostic> Validate(
        AbilityOrchestrationDefinition definition,
        CombatProfile ownerCombat,
        IReadOnlySet<string> availableScripts,
        IReadOnlySet<string> availableItemSlugs)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(ownerCombat);
        ArgumentNullException.ThrowIfNull(availableScripts);
        ArgumentNullException.ThrowIfNull(availableItemSlugs);
        var root = $"abilities/{definition.Id:D}";
        var diagnostics = new List<AbilityDiagnostic>();
        if (definition.Id == Guid.Empty || definition.SchemaVersion != CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(definition.Provenance) || definition.Ability.Id.Value != definition.Id)
            diagnostics.Add(new("RPG_ABILITY_IDENTITY_INVALID", root,
                "Ability identity, schema and provenance must be stable and aligned."));
        if (definition.Ability.ResourceCost > ownerCombat.Stats.MaximumResource)
            diagnostics.Add(new("RPG_ABILITY_COST_EXCEEDS_POOL", $"{root}/resourceCost",
                "Ability resource cost exceeds its owner combat resource pool."));
        if (definition.Ability.CooldownSeconds > 300)
            diagnostics.Add(new("RPG_ABILITY_COOLDOWN_EXCESSIVE", $"{root}/cooldownSeconds",
                "Ability cooldown must not exceed the bounded five-minute limit."));
        if (definition.Ability.Effects.Count is < 1 or > 8 ||
            definition.Ability.Effects.Any(item => item.Magnitude > 100_000))
            diagnostics.Add(new("RPG_ABILITY_EFFECT_INVALID", $"{root}/effects",
                "Ability requires one to eight bounded canonical effects."));
        if (string.IsNullOrWhiteSpace(definition.ScriptPath) || !availableScripts.Contains(definition.ScriptPath))
            diagnostics.Add(new("RPG_ABILITY_SCRIPT_MISSING", $"{root}/scriptPath",
                $"Ability script '{definition.ScriptPath}' is unavailable."));
        if (definition.RequiredItemSlug is { Length: > 0 } item && !availableItemSlugs.Contains(item))
            diagnostics.Add(new("RPG_ABILITY_ITEM_MISSING", $"{root}/requiredItemSlug",
                $"Required item '{item}' is unavailable."));
        if (string.IsNullOrWhiteSpace(definition.Ability.VisualCue.CueKey) ||
            string.IsNullOrWhiteSpace(definition.AudioCueKey))
            diagnostics.Add(new("RPG_ABILITY_CUE_MISSING", $"{root}/cues",
                "VFX and audio cue references are required."));
        if (definition.AiTags.Count == 0 || definition.AiTags.Any(string.IsNullOrWhiteSpace))
            diagnostics.Add(new("RPG_ABILITY_AI_TAGS_REQUIRED", $"{root}/aiTags",
                "At least one non-empty AI behavior tag is required."));
        if (string.IsNullOrWhiteSpace(definition.BalanceIntent))
            diagnostics.Add(new("RPG_ABILITY_BALANCE_INTENT_REQUIRED", $"{root}/balanceIntent",
                "An author-visible balance intent is required."));
        return diagnostics;
    }

    public static IReadOnlyList<AbilityTestScenario> RunTests(
        AbilityOrchestrationDefinition definition,
        CombatProfile ownerCombat)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(ownerCombat);
        var profile = new CombatProfile(ownerCombat.Stats,
            ownerCombat.Abilities.Where(item => item.Id != definition.Ability.Id).Append(definition.Ability).ToArray(),
            ownerCombat.Loot);
        return Enum.GetValues<AbilityTestKind>().Select(kind => Run(kind, definition, profile)).ToArray();
    }

    public static AbilityBalanceReport Balance(AbilityOrchestrationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var magnitude = definition.Ability.Effects.Sum(item => item.Magnitude);
        var targetingFactor = definition.Ability.Targeting == AbilityTargeting.PointArea ? 1.5 : 1;
        var score = (magnitude * targetingFactor) /
                    (1 + definition.Ability.CooldownSeconds + (definition.Ability.ResourceCost * .1));
        var passed = double.IsFinite(score) && score is >= .05 and <= 100_000 &&
                     definition.Ability.CooldownSeconds <= 300 && magnitude <= 800_000;
        return new(score, magnitude, definition.Ability.ResourceCost, definition.Ability.CooldownSeconds, passed,
            $"intent:{definition.BalanceIntent}|score:{score:0.###}|magnitude:{magnitude}|cost:{definition.Ability.ResourceCost}|cooldown:{definition.Ability.CooldownSeconds:0.##}");
    }

    public static string ToCanonicalJson(
        AbilityOrchestrationDefinition definition,
        IReadOnlyList<AbilityTestScenario> tests,
        AbilityBalanceReport balance) => JsonSerializer.Serialize(new
        {
            id = definition.Id,
            definition.SchemaVersion,
            ownerBlueprintId = definition.OwnerBlueprintId.Value,
            ability = new
            {
                id = definition.Ability.Id.Value,
                definition.Ability.Slug,
                definition.Ability.DisplayName,
                definition.Ability.ResourceCost,
                definition.Ability.CooldownSeconds,
                targeting = definition.Ability.Targeting.ToString(),
                effects = definition.Ability.Effects.Select(item => new
                {
                    kind = item.Kind.ToString(),
                    item.Magnitude,
                }),
                vfx = definition.Ability.VisualCue.CueKey,
            },
            definition.ScriptPath,
            definition.RequiredItemSlug,
            definition.AudioCueKey,
            aiTags = definition.AiTags.Order(StringComparer.Ordinal),
            tests = tests.Select(item => new { id = item.Id, kind = item.Kind.ToString(), item.Passed, item.Evidence }),
            balance = new { balance.PowerScore, balance.TotalMagnitude, balance.ResourceCost, balance.CooldownSeconds, balance.Passed, balance.Notes },
            definition.BalanceIntent,
            definition.Provenance,
        });

    private static AbilityTestScenario Run(
        AbilityTestKind kind,
        AbilityOrchestrationDefinition definition,
        CombatProfile profile)
    {
        var id = StableId(definition.Id, $"test:{kind}");
        try
        {
            return kind switch
            {
                AbilityTestKind.ValidTarget => ValidTarget(id, definition, profile),
                AbilityTestKind.InvalidTarget => InvalidTarget(id, definition, profile),
                AbilityTestKind.ResourceCost => ResourceCost(id, definition, profile),
                AbilityTestKind.Cooldown => Cooldown(id, definition, profile),
                AbilityTestKind.Effect => Effect(id, definition, profile),
                _ => new(id, kind, false, "unsupported"),
            };
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return new(id, kind, false, exception.Message);
        }
    }

    private static AbilityTestScenario ValidTarget(Guid id, AbilityOrchestrationDefinition definition,
        CombatProfile profile)
    {
        var (caster, target) = Pair(profile, definition.Ability.Targeting, valid: true);
        PrepareEffectTarget(caster, target, definition.Ability);
        var result = CombatResolver.Cast(caster, target, definition.Ability.Id, 0);
        return new(id, AbilityTestKind.ValidTarget, result.Ability.Id == definition.Ability.Id,
            $"target:{definition.Ability.Targeting}|resolved:{result.Ability.Id}");
    }

    private static AbilityTestScenario InvalidTarget(Guid id, AbilityOrchestrationDefinition definition,
        CombatProfile profile)
    {
        if (definition.Ability.Targeting == AbilityTargeting.PointArea)
            return new(id, AbilityTestKind.InvalidTarget, true, "point-area-accepts-spatial-targets-by-contract");
        var (caster, target) = Pair(profile, definition.Ability.Targeting, valid: false);
        try
        {
            CombatResolver.Cast(caster, target, definition.Ability.Id, 0);
            return new(id, AbilityTestKind.InvalidTarget, false, "invalid-target-accepted");
        }
        catch (InvalidOperationException)
        {
            return new(id, AbilityTestKind.InvalidTarget, true, "invalid-target-rejected");
        }
    }

    private static AbilityTestScenario ResourceCost(Guid id, AbilityOrchestrationDefinition definition,
        CombatProfile profile)
    {
        if (definition.Ability.ResourceCost == 0)
            return new(id, AbilityTestKind.ResourceCost, true, "zero-cost-explicit");
        var (caster, target) = Pair(profile, definition.Ability.Targeting, valid: true);
        caster.Restore(caster.Health, 0);
        try
        {
            CombatResolver.Cast(caster, target, definition.Ability.Id, 0);
            return new(id, AbilityTestKind.ResourceCost, false, "insufficient-resource-accepted");
        }
        catch (InvalidOperationException)
        {
            return new(id, AbilityTestKind.ResourceCost, true, "insufficient-resource-rejected");
        }
    }

    private static AbilityTestScenario Cooldown(Guid id, AbilityOrchestrationDefinition definition,
        CombatProfile profile)
    {
        if (definition.Ability.CooldownSeconds == 0)
            return new(id, AbilityTestKind.Cooldown, true, "zero-cooldown-explicit");
        var (caster, target) = Pair(profile, definition.Ability.Targeting, valid: true);
        PrepareEffectTarget(caster, target, definition.Ability);
        CombatResolver.Cast(caster, target, definition.Ability.Id, 0);
        try
        {
            CombatResolver.Cast(caster, target, definition.Ability.Id, 0);
            return new(id, AbilityTestKind.Cooldown, false, "cooldown-not-enforced");
        }
        catch (InvalidOperationException)
        {
            return new(id, AbilityTestKind.Cooldown, true, "cooldown-enforced");
        }
    }

    private static AbilityTestScenario Effect(Guid id, AbilityOrchestrationDefinition definition,
        CombatProfile profile)
    {
        var (caster, target) = Pair(profile, definition.Ability.Targeting, valid: true);
        PrepareEffectTarget(caster, target, definition.Ability);
        var result = CombatResolver.Cast(caster, target, definition.Ability.Id, 0);
        var passed = definition.Ability.Effects.All(effect => effect.Kind switch
        {
            AbilityEffectKind.Damage => result.Damage > 0,
            AbilityEffectKind.Heal => result.Healing > 0,
            AbilityEffectKind.RestoreResource => result.ResourceRestored > 0,
            _ => false,
        });
        return new(id, AbilityTestKind.Effect, passed,
            $"damage:{result.Damage}|healing:{result.Healing}|resource:{result.ResourceRestored}");
    }

    private static (CombatantState Caster, CombatantState Target) Pair(
        CombatProfile profile,
        AbilityTargeting targeting,
        bool valid)
    {
        var caster = new CombatantState("caster", profile);
        if (targeting == AbilityTargeting.Self)
            return valid ? (caster, caster) : (caster, new CombatantState("other", profile));
        var allied = new CombatProfile(new CombatStats(100, 100, 5, 2, "ally", "heroes"));
        var enemy = new CombatProfile(new CombatStats(100, 100, 5, 2, "enemy", "hostile"));
        return targeting switch
        {
            AbilityTargeting.SingleAlly => (caster, new CombatantState("target", valid ? allied : enemy)),
            AbilityTargeting.SingleEnemy => (caster, new CombatantState("target", valid ? enemy : allied)),
            AbilityTargeting.PointArea => valid
                ? (caster, new CombatantState("target", enemy))
                : (caster, caster),
            _ => (caster, caster),
        };
    }

    private static void PrepareEffectTarget(
        CombatantState caster,
        CombatantState target,
        AbilityDefinition ability)
    {
        if (ability.Effects.Any(item => item.Kind == AbilityEffectKind.Heal))
            target.Restore(Math.Max(0, target.Health - 20), target.Resource);
        if (ability.Effects.Any(item => item.Kind == AbilityEffectKind.RestoreResource))
            target.Restore(target.Health, 0);
        if (ReferenceEquals(caster, target) && ability.ResourceCost > 0 &&
            ability.Effects.Any(item => item.Kind == AbilityEffectKind.RestoreResource))
            caster.Restore(caster.Health, Math.Max(ability.ResourceCost, 1));
    }

    private static Guid StableId(Guid root, string discriminator)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{root:D}:{discriminator}"));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }
}
