using Forge.Domain.Modules;
using Forge.Domain.Spatial;

namespace Forge.Domain.Sequences;

public sealed record ModuleCinematicProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public StartupFlowDefinition Startup { get; }
    public IReadOnlyList<SequenceDefinition> Sequences { get; }
    public IReadOnlyList<TransformOverrideComponent> TransformOverrides { get; }
    public IReadOnlyList<AnimationTweenDefinition> Tweens { get; }
    public string Provenance { get; }

    public ModuleCinematicProfile(Guid id, int schemaVersion, StartupFlowDefinition startup,
        IReadOnlyList<SequenceDefinition> sequences, string provenance,
        IReadOnlyList<TransformOverrideComponent>? transformOverrides = null,
        IReadOnlyList<AnimationTweenDefinition>? tweens = null)
    {
        ArgumentNullException.ThrowIfNull(startup);
        ArgumentNullException.ThrowIfNull(sequences);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var values = sequences.OrderBy(item => item.Slug, StringComparer.Ordinal).ThenBy(item => item.Id).ToArray();
        var overrides = (transformOverrides ?? []).OrderBy(item => item.Id).ToArray();
        var animationTweens = (tweens ?? []).OrderBy(item => item.Id).ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion ||
            values.Select(item => item.Id).Distinct().Count() != values.Length ||
            values.Select(item => item.Slug).Distinct(StringComparer.Ordinal).Count() != values.Length ||
            startup.Kind == StartupFlowKind.Sequence && values.All(item => item.Id != startup.ReferenceId) ||
            overrides.Select(item => item.Id).Distinct().Count() != overrides.Length ||
            animationTweens.Select(item => item.Id).Distinct().Count() != animationTweens.Length ||
            startup.FallbackKind == StartupFlowKind.Sequence &&
            values.All(item => item.Id != startup.FallbackReferenceId))
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Module cinematic profile is invalid.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Startup = startup;
        Sequences = values;
        TransformOverrides = overrides;
        Tweens = animationTweens;
        Provenance = provenance.Trim();
    }

    public static ModuleCinematicProfile CreateDefault(ModuleId moduleId)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        var startup = new StartupFlowDefinition(Forge.Domain.RuntimePolicies.RuntimePolicyIdFactory.Create(moduleId.Value,
                "startup-flow"), StartupFlowDefinition.CurrentSchemaVersion, StartupFlowKind.None, null,
            string.Empty, StartupFlowKind.None, null, string.Empty, true, "Passer", "migration-default");
        return new ModuleCinematicProfile(moduleId.Value, CurrentSchemaVersion, startup, [], "migration-default");
    }
}
