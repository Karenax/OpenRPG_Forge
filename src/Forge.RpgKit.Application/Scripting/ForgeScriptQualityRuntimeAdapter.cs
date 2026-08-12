using Forge.Scripting;

namespace Forge.RpgKit.Application.Scripting;

public sealed record ForgeScriptQualityHandle(Guid ProfileId, Guid SourceId);

public sealed record ForgeScriptQualityResolution(
    bool Available,
    bool OptionalFallback,
    ForgeScriptQualityHandle? Handle,
    string? DiagnosticCode);

public sealed record ForgeScriptQualityRuntimeState(
    ForgeScriptQualityProfile Profile,
    ForgeScriptSourceDocument Document,
    ForgeScriptQualityService StateService);

public sealed class ForgeScriptQualityRuntimeAdapter
{
    public const string RequiredMissingCode = "FORGESCRIPT_QUALITY_REQUIRED_MISSING";

    public ForgeScriptQualityResolution Resolve(
        ForgeScriptQualityProfile? profile,
        ForgeScriptSourceDocument? document,
        bool requiredAtRuntime)
    {
        if (profile is null || document is null)
            return requiredAtRuntime
                ? new(false, false, null, RequiredMissingCode)
                : new(false, true, null, null);
        return new(true, false, new(profile.Id, document.Id), null);
    }

    public ForgeScriptQualityRuntimeState Build(
        ForgeScriptQualityHandle handle,
        ForgeScriptQualityProfile profile,
        ForgeScriptSourceDocument document)
    {
        Require(handle, profile, document);
        var qualityDiagnostics = ForgeScriptQualityProfileValidator.Validate(profile);
        if (qualityDiagnostics.Count > 0)
            throw new InvalidOperationException($"FORGESCRIPT_QUALITY_PROFILE_INVALID:{qualityDiagnostics[0].Code}");
        var contracts = document.EventBindings
            .GroupBy(binding => binding.EventName, StringComparer.Ordinal)
            .Select(group => new ForgeScriptEventContract(group.First().Id, 1, group.First().Provenance,
                group.Key, group.First().PayloadType, document.RequiredAtRuntime))
            .ToArray();
        var sourceDiagnostics = ForgeScriptSourceValidator.Validate(document, contracts);
        if (sourceDiagnostics.Any(item => item.Severity == "error"))
            throw new InvalidOperationException($"FORGESCRIPT_QUALITY_DOCUMENT_INVALID:{sourceDiagnostics[0].Code}");
        return new(profile, document, new ForgeScriptQualityService(profile.State.MaximumBytes));
    }

    public IReadOnlyList<ScriptTestResult> RunTests(
        ForgeScriptQualityHandle handle,
        ForgeScriptQualityRuntimeState state,
        IReadOnlyList<ScriptTestCase> tests)
    {
        Require(handle, state.Profile, state.Document);
        return ForgeScriptQualityService.RunSuite(tests, state.Profile.UnitTests.MaximumCases);
    }

    public ProfileReport Profile(
        ForgeScriptQualityHandle handle,
        ForgeScriptQualityRuntimeState state,
        IReadOnlyList<ProfileSample> samples)
    {
        Require(handle, state.Profile, state.Document);
        var settings = state.Profile.Profiler;
        return ForgeScriptQualityService.Profile(samples, settings.InstructionThreshold,
            settings.AllocationThresholdBytes, TimeSpan.FromMilliseconds(settings.CpuThresholdMilliseconds));
    }

    public GeneratedApiDocument GenerateDocumentation(
        ForgeScriptQualityHandle handle,
        ForgeScriptQualityRuntimeState state,
        IEnumerable<ApiRegistration> registrations)
    {
        Require(handle, state.Profile, state.Document);
        return ForgeScriptQualityService.GenerateDocumentation(registrations, "1.0");
    }

    public IReadOnlyList<StaticDiagnostic> Analyze(
        ForgeScriptQualityHandle handle,
        ForgeScriptQualityRuntimeState state,
        ScriptSource source)
    {
        Require(handle, state.Profile, state.Document);
        return ForgeScriptQualityService.Analyze(source, state.Profile.StaticAnalysis);
    }

    public ForgeScriptExecutionResult Execute(
        ForgeScriptQualityHandle handle,
        ForgeScriptQualityRuntimeState state,
        IForgeScriptRuntime runtime,
        ForgeScriptQuotas? quotas = null)
    {
        Require(handle, state.Profile, state.Document);
        ArgumentNullException.ThrowIfNull(runtime);
        var compiled = runtime.Compile(state.Document.Source);
        if (!compiled.Success || compiled.Artifact is null)
            return new(ForgeScriptExecutionStatus.Failed, [], compiled.Diagnostics, null);
        return runtime.Execute(compiled.Artifact, quotas);
    }

    private static void Require(
        ForgeScriptQualityHandle handle,
        ForgeScriptQualityProfile profile,
        ForgeScriptSourceDocument document)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(document);
        if (handle.ProfileId == Guid.Empty || handle.SourceId == Guid.Empty ||
            handle.ProfileId != profile.Id || handle.SourceId != document.Id)
            throw new InvalidOperationException("FORGESCRIPT_QUALITY_HANDLE_STALE");
    }
}
