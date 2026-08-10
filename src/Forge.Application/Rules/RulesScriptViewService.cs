using System.Collections.ObjectModel;
using Forge.Domain.Rules;

namespace Forge.Application.Rules;

public sealed record RulesScriptHandle(Guid Id, string Scope, int SchemaVersion);

public sealed record RulesScriptSnapshot(
    RulesScriptHandle Handle,
    IReadOnlyDictionary<string, object?> Values);

/// <summary>
/// Exposes stable, copied rule values to ForgeScript. Mutations remain governed by
/// SetRulesProfilesHandler so a script cannot alter authoring state by retaining a reference.
/// </summary>
public sealed class RulesScriptViewService(ModuleRulesProfile module, AreaRulesProfile area)
{
    private readonly ModuleRulesProfile _module = module ?? throw new ArgumentNullException(nameof(module));
    private readonly AreaRulesProfile _area = area ?? throw new ArgumentNullException(nameof(area));

    public RulesScriptHandle ModuleHandle => new(_module.Id, "module-rules", _module.SchemaVersion);
    public RulesScriptHandle AreaHandle => new(_area.Id, "area-rules", _area.SchemaVersion);

    public RulesScriptSnapshot Resolve(RulesScriptHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (handle == ModuleHandle)
        {
            var difficulty = RulesRuntimeService.ResolveDifficulty(_module);
            return Snapshot(handle, new Dictionary<string, object?>
            {
                ["rulesetId"] = _module.Progression.RulesetId,
                ["progressionPolicyId"] = _module.Progression.Id.ToString("D"),
                ["progressionCurvePointCount"] = _module.Progression.Curve.Count,
                ["activeDifficultyKey"] = difficulty.Key,
                ["playerDamageMultiplier"] = difficulty.PlayerDamageMultiplier,
                ["incomingDamageMultiplier"] = difficulty.IncomingDamageMultiplier,
                ["aiIntensityMultiplier"] = difficulty.AiIntensityMultiplier,
                ["economyMultiplier"] = difficulty.EconomyMultiplier,
                ["restRecoveryMultiplier"] = difficulty.RestRecoveryMultiplier,
                ["progressionMultiplier"] = difficulty.ProgressionMultiplier,
                ["friendlyFire"] = difficulty.FriendlyFire.ToString().ToLowerInvariant(),
                ["enabledMutators"] = difficulty.EnabledMutators.ToArray(),
                ["activePluginKey"] = _module.Plugins.ActivePackageKey,
                ["pluginPackageCount"] = _module.Plugins.Packages.Count,
                ["pluginRequiredAtRuntime"] = _module.Plugins.RequiredAtRuntime,
            });
        }

        if (handle == AreaHandle)
        {
            return Snapshot(handle, new Dictionary<string, object?>
            {
                ["rulesetId"] = _area.Perception.RulesetId,
                ["pvpMode"] = _area.Combat.PvpMode.ToString().ToLowerInvariant(),
                ["friendlyFire"] = _area.Combat.FriendlyFire.ToString().ToLowerInvariant(),
                ["crimeSystemEnabled"] = _area.Combat.CrimeEnabled,
                ["serverAuthorityRequired"] = _area.Combat.ServerAuthoritative,
                ["perceptionModifierCount"] = _area.Perception.Modifiers.Count,
            });
        }

        throw new KeyNotFoundException($"Unknown rules handle '{handle.Scope}:{handle.Id}'.");
    }

    private static RulesScriptSnapshot Snapshot(RulesScriptHandle handle, Dictionary<string, object?> values) =>
        new(handle, new ReadOnlyDictionary<string, object?>(values));
}
