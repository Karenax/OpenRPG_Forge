using Forge.Domain.Areas;
using Forge.Domain.Modules;
using Forge.Domain.RuntimePolicies;

namespace Forge.Domain.Validation;

public static class RuntimePolicyValidator
{
    public const string ModuleIdentityCode = "runtime.policy.module_identity";
    public const string AreaIdentityCode = "runtime.policy.area_identity";
    public const string HeartbeatNameCode = "runtime.heartbeat.name_duplicate";
    public const string RestVolumeBoundsCode = "runtime.rest.volume_out_of_bounds";

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

        return diagnostics;
    }
}
