using Forge.Scripting;

namespace Forge.RpgKit.Application.Scripting;

public sealed record ForgeScriptInteropHandle(Guid ProfileId, Guid SourceId);

public sealed record ForgeScriptInteropResolution(
    bool Available,
    bool OptionalFallback,
    ForgeScriptInteropHandle? Handle,
    string? DiagnosticCode);

public sealed record ForgeScriptInteropRuntimeState(
    ForgeScriptInteropProfile Profile,
    ForgeScriptSourceDocument Document);

public sealed class ForgeScriptInteropRuntimeAdapter
{
    public const string RequiredMissingCode = "FORGESCRIPT_INTEROP_REQUIRED_MISSING";

    public ForgeScriptInteropResolution Resolve(
        ForgeScriptInteropProfile? profile,
        ForgeScriptSourceDocument? document,
        bool requiredAtRuntime)
    {
        if (profile is null || document is null)
            return requiredAtRuntime
                ? new(false, false, null, RequiredMissingCode)
                : new(false, true, null, null);
        return new(true, false, new(profile.Id, document.Id), null);
    }

    public ForgeScriptInteropRuntimeState Build(
        ForgeScriptInteropHandle handle,
        ForgeScriptInteropProfile profile,
        ForgeScriptSourceDocument document)
    {
        Require(handle, profile, document);
        var diagnostics = ForgeScriptInteropProfileValidator.Validate(profile);
        if (diagnostics.Count > 0)
            throw new InvalidOperationException($"FORGESCRIPT_INTEROP_PROFILE_INVALID:{diagnostics[0].Code}");
        var sourceDiagnostics = ForgeScriptSourceValidator.Validate(document, []);
        if (sourceDiagnostics.Any(item => item.Severity == "error"))
            throw new InvalidOperationException($"FORGESCRIPT_INTEROP_DOCUMENT_INVALID:{sourceDiagnostics[0].Code}");
        return new(profile, document);
    }

    public ForgeScriptExecutionResult Execute(
        ForgeScriptInteropHandle handle,
        ForgeScriptInteropRuntimeState state,
        IForgeScriptRuntime runtime,
        ForgeScriptQuotas? quotas = null)
    {
        Require(handle, state.Profile, state.Document);
        ArgumentNullException.ThrowIfNull(runtime);
        var compiled = runtime.Compile(state.Document.Source);
        if (!compiled.Success || compiled.Artifact is null)
            return new(ForgeScriptExecutionStatus.Failed, [], compiled.Diagnostics, null);
        return runtime.Execute(compiled.Artifact, quotas ?? state.Profile.TestQuotas());
    }

    private static void Require(
        ForgeScriptInteropHandle handle,
        ForgeScriptInteropProfile profile,
        ForgeScriptSourceDocument document)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(document);
        if (handle.ProfileId == Guid.Empty || handle.SourceId == Guid.Empty ||
            handle.ProfileId != profile.Id || handle.SourceId != document.Id)
            throw new InvalidOperationException("FORGESCRIPT_INTEROP_HANDLE_STALE");
    }
}
