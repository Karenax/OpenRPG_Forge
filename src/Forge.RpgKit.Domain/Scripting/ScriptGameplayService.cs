using System.Text.Json;
using System.Text.Json.Serialization;

namespace Forge.RpgKit.Domain.Scripting;

public enum EffectStacking
{
    Replace,
    RefreshDuration,
    StackMagnitude,
    StackDuration,
    Independent,
    HighestWins,
}

public sealed record DamageSpec(
    double Amount,
    string DamageType,
    IReadOnlySet<string> Tags,
    bool CriticalAllowed,
    double ArmorPenetration);

public sealed record AttackContext(
    Guid TransactionId,
    string AttackerId,
    string TargetId,
    string AbilityId,
    double Accuracy,
    double CriticalChance,
    int Seed,
    string Authority,
    string Cause);

public sealed record ScriptCombatState(
    IReadOnlyDictionary<string, double> Health,
    IReadOnlyList<string> EffectIds,
    long Revision,
    IReadOnlyDictionary<string, double>? Armor = null);

public sealed record ScriptCombatResult(
    bool Committed,
    ScriptCombatState State,
    double AppliedDamage,
    bool Critical,
    IReadOnlyList<string> Events,
    string SemanticDiff);

public sealed record EffectDefinition(
    string Id,
    int SchemaVersion,
    string Provenance,
    string Kind,
    EffectStacking Stacking,
    TimeSpan Duration,
    TimeSpan Period,
    double Magnitude,
    IReadOnlySet<string> DispelTags,
    bool Serializable);

public sealed record EffectSpec(
    Guid InstanceId,
    string DefinitionId,
    string SourceId,
    string TargetId,
    double MagnitudeMultiplier,
    TimeSpan? DurationOverride,
    DateTimeOffset AppliedAt);

public sealed record ActiveEffect(
    EffectSpec Spec,
    EffectDefinition Definition,
    int Stacks,
    DateTimeOffset ExpiresAt,
    DateTimeOffset NextTickAt);

public sealed record EffectApplyResult(
    IReadOnlyList<ActiveEffect> Effects,
    IReadOnlyList<string> Events,
    string SemanticDiff);

public sealed record EffectStateSnapshot(
    int SchemaVersion,
    DateTimeOffset CapturedAt,
    IReadOnlyList<ActiveEffect> Effects);

public static class ScriptGameplayService
{
    private static readonly JsonSerializerOptions SnapshotJson = CreateSnapshotJson();

    private static JsonSerializerOptions CreateSnapshotJson()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };
        options.Converters.Add(new ReadOnlySetJsonConverter());
        return options;
    }

    public static ScriptCombatResult Attack(
        ScriptCombatState original,
        AttackContext context,
        DamageSpec damage)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(damage);
        if (context.TransactionId == Guid.Empty || string.IsNullOrWhiteSpace(context.AttackerId) ||
            string.IsNullOrWhiteSpace(context.TargetId) || string.IsNullOrWhiteSpace(context.AbilityId) ||
            context.Authority != "server" || damage.Amount < 0 || !double.IsFinite(damage.Amount) ||
            string.IsNullOrWhiteSpace(damage.DamageType) || damage.ArmorPenetration is < 0 or > 1 ||
            context.Accuracy is < 0 or > 1 || context.CriticalChance is < 0 or > 1 ||
            !original.Health.ContainsKey(context.TargetId))
            return new(false, original, 0, false, ["combat-rejected"], "no-change");

        var random = new Random(context.Seed);
        if (random.NextDouble() > context.Accuracy)
            return new(true, original with { Revision = checked(original.Revision + 1) }, 0, false,
                ["attack-missed", $"attribution:{context.AttackerId}"],
                $"attack:{context.TransactionId}|result:miss|cause:{context.Cause}");

        var critical = damage.CriticalAllowed && random.NextDouble() <= context.CriticalChance;
        var raw = damage.Amount * (critical ? 2 : 1);
        var armor = Math.Max(0, original.Armor?.GetValueOrDefault(context.TargetId) ?? 0);
        var mitigated = Math.Max(0, raw - armor * (1 - damage.ArmorPenetration));
        var health = original.Health[context.TargetId];
        var applied = Math.Min(health, mitigated);
        var updated = original.Health.ToDictionary();
        updated[context.TargetId] = health - applied;
        return new(true, original with { Health = updated, Revision = checked(original.Revision + 1) },
            applied, critical,
            ["attack-hit", $"damage:{damage.DamageType}", $"attribution:{context.AttackerId}",
                $"ability:{context.AbilityId}", .. damage.Tags.Order(StringComparer.Ordinal)
                    .Select(tag => $"effect-tag:{tag}")],
            $"attack:{context.TransactionId}|damage:{applied:0.##}|critical:{critical}|cause:{context.Cause}");
    }

    public static EffectApplyResult ApplyEffect(
        IReadOnlyList<ActiveEffect> current,
        EffectDefinition definition,
        EffectSpec spec)
    {
        ArgumentNullException.ThrowIfNull(current);
        Validate(definition, spec);
        if (current.Any(item => item.Spec.InstanceId == spec.InstanceId))
            throw new InvalidOperationException("RPG_SCRIPT_EFFECT_INSTANCE_DUPLICATE");

        var effects = current.ToList();
        var matches = effects.Where(item => item.Definition.Id == definition.Id &&
                                            item.Spec.TargetId == spec.TargetId).ToArray();
        var expires = spec.AppliedAt + (spec.DurationOverride ?? definition.Duration);
        var nextTick = definition.Period > TimeSpan.Zero ? spec.AppliedAt + definition.Period : expires;
        var created = new ActiveEffect(spec, definition, 1, expires, nextTick);
        switch (definition.Stacking)
        {
            case EffectStacking.Replace:
                effects.RemoveAll(item => matches.Contains(item));
                effects.Add(created);
                break;
            case EffectStacking.RefreshDuration when matches.Length > 0:
                Replace(effects, matches[0], matches[0] with { ExpiresAt = expires });
                break;
            case EffectStacking.StackMagnitude when matches.Length > 0:
                Replace(effects, matches[0], matches[0] with { Stacks = matches[0].Stacks + 1 });
                break;
            case EffectStacking.StackDuration when matches.Length > 0:
                Replace(effects, matches[0], matches[0] with
                {
                    ExpiresAt = matches[0].ExpiresAt + definition.Duration,
                });
                break;
            case EffectStacking.HighestWins when matches.Length > 0 &&
                                                  matches[0].Spec.MagnitudeMultiplier >= spec.MagnitudeMultiplier:
                break;
            case EffectStacking.HighestWins when matches.Length > 0:
                Replace(effects, matches[0], created);
                break;
            default:
                effects.Add(created);
                break;
        }
        return new(effects.OrderBy(item => item.Spec.InstanceId).ToArray(),
            ["effect-applied", $"stacking:{definition.Stacking}", $"source:{spec.SourceId}"],
            $"apply-effect:{spec.InstanceId}|definition:{definition.Id}|target:{spec.TargetId}");
    }

    public static EffectApplyResult AdvanceEffects(
        IReadOnlyList<ActiveEffect> current,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(current);
        var effects = new List<ActiveEffect>();
        var events = new List<string>();
        foreach (var effect in current.OrderBy(item => item.Spec.InstanceId))
        {
            if (now >= effect.ExpiresAt)
            {
                events.Add($"effect-expired:{effect.Spec.InstanceId}");
                continue;
            }
            var next = effect;
            if (effect.Definition.Period > TimeSpan.Zero && now >= effect.NextTickAt)
            {
                var elapsed = now - effect.NextTickAt;
                var ticks = 1 + (int)(elapsed.Ticks / effect.Definition.Period.Ticks);
                events.Add($"effect-tick:{effect.Spec.InstanceId}:{ticks}:" +
                           $"{effect.Definition.Magnitude * effect.Spec.MagnitudeMultiplier * effect.Stacks:0.##}");
                next = effect with { NextTickAt = effect.NextTickAt + effect.Definition.Period * ticks };
            }
            effects.Add(next);
        }
        return new(effects, events, $"advance-effects:{now:O}|events:{events.Count}");
    }

    public static EffectApplyResult Dispel(
        IReadOnlyList<ActiveEffect> current,
        string targetId,
        IReadOnlySet<string> tags)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        ArgumentNullException.ThrowIfNull(tags);
        var removed = current.Where(item => item.Spec.TargetId == targetId &&
                                            item.Definition.DispelTags.Overlaps(tags)).ToArray();
        return new(current.Except(removed).ToArray(),
            removed.Select(item => $"effect-dispelled:{item.Spec.InstanceId}").ToArray(),
            $"dispel:{targetId}|removed:{removed.Length}");
    }

    public static string SerializeEffects(IReadOnlyList<ActiveEffect> current, DateTimeOffset capturedAt)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.Any(item => !item.Definition.Serializable))
            throw new InvalidOperationException("RPG_SCRIPT_EFFECT_NOT_SERIALIZABLE");
        return JsonSerializer.Serialize(new EffectStateSnapshot(1, capturedAt,
            current.OrderBy(item => item.Spec.InstanceId).ToArray()), SnapshotJson);
    }

    public static EffectStateSnapshot DeserializeEffects(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<EffectStateSnapshot>(json, SnapshotJson) ??
                       throw new InvalidOperationException("RPG_SCRIPT_EFFECT_STATE_INVALID");
        if (snapshot.SchemaVersion != 1 || snapshot.Effects.Any(item =>
                !item.Definition.Serializable || item.Spec.InstanceId == Guid.Empty))
            throw new InvalidOperationException("RPG_SCRIPT_EFFECT_STATE_INVALID");
        return snapshot;
    }

    private static void Validate(EffectDefinition definition, EffectSpec spec)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(spec);
        if (spec.InstanceId == Guid.Empty || definition.SchemaVersion != 1 ||
            string.IsNullOrWhiteSpace(definition.Id) || string.IsNullOrWhiteSpace(definition.Provenance) ||
            string.IsNullOrWhiteSpace(definition.Kind) || definition.Duration <= TimeSpan.Zero ||
            definition.Period < TimeSpan.Zero || !double.IsFinite(definition.Magnitude) ||
            !double.IsFinite(spec.MagnitudeMultiplier) || spec.MagnitudeMultiplier < 0 ||
            string.IsNullOrWhiteSpace(spec.SourceId) || string.IsNullOrWhiteSpace(spec.TargetId) ||
            spec.DefinitionId != definition.Id)
            throw new InvalidOperationException("RPG_SCRIPT_EFFECT_INVALID");
    }

    private static void Replace(List<ActiveEffect> effects, ActiveEffect before, ActiveEffect after)
    {
        effects[effects.IndexOf(before)] = after;
    }

    private sealed class ReadOnlySetJsonConverter : JsonConverter<IReadOnlySet<string>>
    {
        public override IReadOnlySet<string> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
            (JsonSerializer.Deserialize<string[]>(ref reader, options) ?? [])
            .ToHashSet(StringComparer.Ordinal);

        public override void Write(
            Utf8JsonWriter writer,
            IReadOnlySet<string> value,
            JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.Order(StringComparer.Ordinal).ToArray(), options);
    }
}
