using Forge.Application.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Application.WorldFeatures;

public sealed class EncounterCompositionScriptService
{
    public sealed record DifficultyView(
        Guid Id,
        string RulesetId,
        string PartyAssessmentProviderId,
        string Band,
        int ExpectedPartySize,
        double ExpectedPartyPower,
        double EffectiveBudget);

    public sealed record GroupView(Guid Id, string Slug, int Minimum, int Maximum, double Budget,
        IReadOnlyList<Guid> CandidateIds);

    public sealed record SelectionView(Guid Id, string ProviderId, string Strategy, int Seed,
        bool PreviewComposition);

    public sealed record ActivationView(Guid Id, string Mode, double IntervalSeconds, string? ScriptId,
        string Distribution);

    public sealed record Handle(
        int SchemaVersion,
        long Revision,
        Guid EncounterId,
        string AreaId,
        string Provenance,
        string Slug,
        string DisplayName,
        string Category,
        string Theme,
        string PrimaryRole,
        DifficultyView Difficulty,
        IReadOnlyList<GroupView> Groups,
        SelectionView Selection,
        ActivationView Activation,
        bool RequiredAtRuntime,
        IReadOnlyDictionary<string, string> Overrides);

    public Handle Get(ProjectState state, Guid encounterId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var encounter = state.Project.Module.WorldFeatures.Encounters.SingleOrDefault(item => item.Id == encounterId)
            ?? throw new KeyNotFoundException($"Encounter world profile '{encounterId:D}' was not found.");
        var resolution = EncounterCompositionProfileValidator.Resolve(encounter);
        var profile = resolution.Profile ?? throw new InvalidOperationException(
            resolution.Diagnostics.FirstOrDefault()?.Code ?? "WORLD_ENCOUNTER_COMPOSITION_MISSING");
        if (!resolution.Available)
            throw new InvalidOperationException(string.Join('|', resolution.Diagnostics.Select(item => item.Code)));

        return new(profile.SchemaVersion, state.Revision.Value, encounter.Id, encounter.AreaId.ToString(),
            profile.Provenance, profile.Slug, profile.DisplayName, profile.Category, profile.Theme,
            profile.PrimaryRole.ToString(),
            new(profile.Difficulty.Id, profile.Difficulty.RulesetId,
                profile.Difficulty.PartyAssessmentProviderId, profile.Difficulty.Band.ToString(),
                profile.Difficulty.ExpectedPartySize, profile.Difficulty.ExpectedPartyPower,
                profile.Difficulty.EffectiveBudget),
            profile.Groups.Select(item => new GroupView(item.Id, item.Slug, item.Minimum, item.Maximum,
                item.Budget, item.CandidateIds)).ToArray(),
            new(profile.Selection.Id, profile.Selection.PartyAssessmentProviderId, profile.Selection.Strategy,
                profile.Selection.DeterministicSeed, profile.Selection.PreviewComposition),
            new(profile.Activation.Id, profile.Activation.Mode.ToString(), profile.Activation.IntervalSeconds,
                profile.Activation.ScriptId, profile.Activation.Distribution.ToString()),
            encounter.CompositionRequiredAtRuntime, profile.Overrides);
    }
}
