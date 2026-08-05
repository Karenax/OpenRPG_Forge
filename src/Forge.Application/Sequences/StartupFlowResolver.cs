using Forge.Domain.Sequences;
using Forge.Domain.Validation;

namespace Forge.Application.Sequences;

public sealed record StartupFlowResolution(StartupFlowKind Kind, Guid? ReferenceId, string ResourcePath,
    bool UsedFallback, bool Skippable, string SkipLabel, IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class StartupFlowResolver
{
    public const string MissingCode = "startup.flow.resource_missing";

    public static StartupFlowResolution Resolve(ModuleCinematicProfile profile, Func<string, bool> resourceExists)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(resourceExists);
        var startup = profile.Startup;
        if (Available(startup.Kind, startup.ReferenceId, startup.ResourcePath, profile, resourceExists))
        {
            return new StartupFlowResolution(startup.Kind, startup.ReferenceId, startup.ResourcePath, false,
                startup.Skippable, startup.SkipLabel, []);
        }

        if (Available(startup.FallbackKind, startup.FallbackReferenceId, startup.FallbackResourcePath, profile,
                resourceExists))
        {
            return new StartupFlowResolution(startup.FallbackKind, startup.FallbackReferenceId,
                startup.FallbackResourcePath, true, startup.Skippable, startup.SkipLabel,
                [new DomainDiagnostic(MissingCode, "Startup resource is missing; fallback selected.",
                    "module/runtimePolicies/cinematics/startup")]);
        }

        return new StartupFlowResolution(StartupFlowKind.None, null, string.Empty, true, true, startup.SkipLabel,
            [new DomainDiagnostic(MissingCode, "Startup resource and fallback are unavailable.",
                "module/runtimePolicies/cinematics/startup")]);
    }

    private static bool Available(StartupFlowKind kind, Guid? referenceId, string path,
        ModuleCinematicProfile profile, Func<string, bool> resourceExists) => kind switch
        {
            StartupFlowKind.None or StartupFlowKind.Menu => true,
            StartupFlowKind.Sequence => referenceId is not null && profile.Sequences.Any(item => item.Id == referenceId),
            StartupFlowKind.Video or StartupFlowKind.Scene => resourceExists(path),
            StartupFlowKind.Dialogue => referenceId is not null,
            _ => false,
        };
}
