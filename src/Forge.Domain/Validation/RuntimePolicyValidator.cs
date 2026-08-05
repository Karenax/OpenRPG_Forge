using Forge.Domain.Areas;
using Forge.Domain.Modules;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.RuntimeExtensions;

namespace Forge.Domain.Validation;

public static class RuntimePolicyValidator
{
    public const string ModuleIdentityCode = "runtime.policy.module_identity";
    public const string AreaIdentityCode = "runtime.policy.area_identity";
    public const string HeartbeatNameCode = "runtime.heartbeat.name_duplicate";
    public const string RestVolumeBoundsCode = "runtime.rest.volume_out_of_bounds";
    public const string GameplayIdentityCode = "runtime.gameplay.identity";
    public const string CombatEventContractCode = "runtime.gameplay.combat_event_contract";
    public const string ModuleRulesIdentityCode = "runtime.rules.module_identity";
    public const string AreaRulesIdentityCode = "runtime.rules.area_identity";
    public const string RulesProfileIdentityCode = "runtime.rules.profile_identity";
    public const string AreaExperienceIdentityCode = "runtime.experience.area_identity";
    public const string RuntimeExtensionIdentityCode = "runtime.extension.module_identity";

    public static IReadOnlyList<DomainDiagnostic> Validate(ModuleDefinition module)
    {
        ArgumentNullException.ThrowIfNull(module);
        var diagnostics = new List<DomainDiagnostic>();
        if (module.RuntimePolicies.Id != module.Id.Value)
        {
            diagnostics.Add(new DomainDiagnostic(ModuleIdentityCode,
                "The module runtime policy ID must match the module ID.", "module/runtimePolicies/id"));
        }

        if (module.RuntimePolicies.Heartbeats.Select(item => item.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != module.RuntimePolicies.Heartbeats.Count)
        {
            diagnostics.Add(new DomainDiagnostic(HeartbeatNameCode,
                "Module heartbeat schedule names must be unique.", "module/runtimePolicies/heartbeats"));
        }

        if (module.RuntimePolicies.Gameplay.Id != module.Id.Value)
        {
            diagnostics.Add(new DomainDiagnostic(GameplayIdentityCode,
                "The gameplay control profile ID must match the module ID.",
                "module/runtimePolicies/gameplay/id"));
        }

        var providers = module.RuntimePolicies.Gameplay.CombatModes.Providers;
        var baseline = providers.FirstOrDefault()?.EventContracts.ToHashSet(StringComparer.Ordinal) ?? [];
        if (providers.Any(provider => !baseline.SetEquals(provider.EventContracts)))
        {
            diagnostics.Add(new DomainDiagnostic(CombatEventContractCode,
                "All combat mode providers must expose the same event contracts.",
                "module/runtimePolicies/gameplay/combatModes/providers"));
        }

        var rules = module.RuntimePolicies.Rules;
        if (rules.Id != module.Id.Value)
            diagnostics.Add(new DomainDiagnostic(ModuleRulesIdentityCode,
                "The module rules profile ID must match the module ID.", "module/runtimePolicies/rules/id"));
        if (rules.Progression.Id == Guid.Empty || rules.Mutators.Id == Guid.Empty ||
            rules.Difficulties.Any(item => item.Id == Guid.Empty))
            diagnostics.Add(new DomainDiagnostic(RulesProfileIdentityCode,
                "Nested module rule profiles require stable identities.", "module/runtimePolicies/rules"));

        if (module.RuntimePolicies.Extensions.Id != module.Id.Value)
            diagnostics.Add(new DomainDiagnostic(RuntimeExtensionIdentityCode,
                "The runtime extension profile ID must match the module ID.",
                "module/runtimePolicies/extensions/id"));

        diagnostics.AddRange(OperationalReadinessValidator.Validate(
            module.RuntimePolicies.Extensions.OperationalReadiness));

        return diagnostics;
    }

    public static IReadOnlyList<DomainDiagnostic> Validate(AreaDefinition area)
    {
        ArgumentNullException.ThrowIfNull(area);
        var diagnostics = new List<DomainDiagnostic>();
        if (area.RuntimePolicies.Id != area.Id.Value)
        {
            diagnostics.Add(new DomainDiagnostic(AreaIdentityCode,
                "The area runtime policy ID must match the area ID.", $"areas/{area.Id}/runtimePolicies/id"));
        }

        if (area.RuntimePolicies.Heartbeats.Select(item => item.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != area.RuntimePolicies.Heartbeats.Count)
        {
            diagnostics.Add(new DomainDiagnostic(HeartbeatNameCode,
                "Area heartbeat schedule names must be unique.", $"areas/{area.Id}/runtimePolicies/heartbeats"));
        }

        foreach (var volume in area.RuntimePolicies.Rest.Volumes)
        {
            var minX = volume.Center.X - (volume.Size.X / 2f);
            var maxX = volume.Center.X + (volume.Size.X / 2f);
            var minZ = volume.Center.Z - (volume.Size.Z / 2f);
            var maxZ = volume.Center.Z + (volume.Size.Z / 2f);
            if (minX < -area.Dimensions.Width / 2f || maxX > area.Dimensions.Width / 2f ||
                minZ < -area.Dimensions.Depth / 2f || maxZ > area.Dimensions.Depth / 2f)
            {
                diagnostics.Add(new DomainDiagnostic(RestVolumeBoundsCode,
                    $"Rest policy volume '{volume.Name}' must remain inside the area X/Z bounds.",
                    $"areas/{area.Id}/runtimePolicies/rest/volumes/{volume.Id}"));
            }
        }

        var rules = area.RuntimePolicies.Rules;
        if (rules.Id != area.Id.Value || rules.Perception.Id != area.Id.Value)
            diagnostics.Add(new DomainDiagnostic(AreaRulesIdentityCode,
                "Area rules and perception profile IDs must match the area ID.",
                $"areas/{area.Id}/runtimePolicies/rules/id"));
        if (rules.Combat.Id == Guid.Empty || rules.Perception.Modifiers.Any(item => item.Id == Guid.Empty))
            diagnostics.Add(new DomainDiagnostic(RulesProfileIdentityCode,
                "Nested area rule profiles require stable identities.",
                $"areas/{area.Id}/runtimePolicies/rules"));

        var experience = area.RuntimePolicies.Experience;
        if (experience.Id != area.Id.Value)
            diagnostics.Add(new DomainDiagnostic(AreaExperienceIdentityCode,
                "The area experience profile ID must match the area ID.",
                $"areas/{area.Id}/runtimePolicies/experience/id"));
        return diagnostics;
    }
}
