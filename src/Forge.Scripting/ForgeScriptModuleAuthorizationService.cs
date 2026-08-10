namespace Forge.Scripting;

public sealed record ForgeScriptModuleAuthorizationRequest(
    Guid ModuleId,
    string KitId,
    string EventName,
    string Handler,
    IReadOnlySet<string> RequestedCapabilities,
    ForgeScriptEventAuthority Authority,
    bool SignerTrusted,
    bool IsDevelopment,
    bool RequiredAtRuntime);

public sealed record ForgeScriptModuleAuthorizationDecision(
    bool Succeeded,
    bool ShouldExecute,
    IReadOnlySet<string> GrantedCapabilities,
    IReadOnlyList<ForgeScriptModulePermissionDiagnostic> Diagnostics);

public static class ForgeScriptModuleAuthorizationService
{
    public static ForgeScriptModuleAuthorizationDecision Authorize(
        ForgeScriptModulePermissionPolicy? policy,
        ForgeScriptModuleAuthorizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (policy is null)
        {
            return request.RequiredAtRuntime
                ? Denied("script.permission.required_missing", "scriptPermissions",
                    "The required module permission manifest is missing.")
                : new(true, false, new HashSet<string>(StringComparer.Ordinal), []);
        }

        var validation = ForgeScriptModulePermissionValidator.Validate(policy);
        if (validation.Count > 0)
            return new(false, false, new HashSet<string>(StringComparer.Ordinal), validation);
        if (request.ModuleId != policy.ModuleId)
            return Denied("script.permission.module_mismatch", "scriptPermissions/moduleId",
                "The permission manifest does not belong to the executing module.", policy.Id);
        if (!policy.ServerEnforced || request.Authority != ForgeScriptEventAuthority.Server)
            return Denied("script.permission.authority_denied", "scriptPermissions/authority",
                "Only the authoritative server may grant module script capabilities.", policy.Id);
        if (policy.RequireSignedDistribution && !request.SignerTrusted &&
            !(request.IsDevelopment && policy.AllowUnsignedDevelopment))
            return Denied("script.permission.signature_untrusted", "scriptPermissions/signature",
                "The module script permission manifest requires a trusted package signature.", policy.Id);

        var grant = policy.KitGrants.SingleOrDefault(item =>
            string.Equals(item.KitId, request.KitId, StringComparison.Ordinal));
        if (grant is null)
            return Denied("script.permission.kit_denied", "scriptPermissions/kitGrants",
                $"Kit '{request.KitId}' has no capability grant.", policy.Id);
        var binding = policy.EventBindings.SingleOrDefault(item => item.Enabled &&
            string.Equals(item.KitId, request.KitId, StringComparison.Ordinal) &&
            string.Equals(item.EventName, request.EventName, StringComparison.Ordinal) &&
            string.Equals(item.Handler, request.Handler, StringComparison.Ordinal));
        if (binding is null)
            return Denied("script.permission.binding_denied", "scriptPermissions/eventBindings",
                $"Event '{request.EventName}' and handler '{request.Handler}' are not authorized.", policy.Id);

        var granted = policy.AllowedCapabilities.Intersect(grant.Capabilities, StringComparer.Ordinal)
            .Intersect(binding.Capabilities, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var denied = request.RequestedCapabilities.Except(granted, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray();
        if (denied.Length > 0)
            return Denied("script.permission.capability_denied", "scriptPermissions/capabilities",
                $"Undeclared capabilities were denied: {string.Join(", ", denied)}.", policy.Id);
        return new(true, true, granted, []);
    }

    private static ForgeScriptModuleAuthorizationDecision Denied(
        string code,
        string path,
        string message,
        Guid? subjectId = null) => new(false, false, new HashSet<string>(StringComparer.Ordinal),
        [new(code, path, subjectId, "error", message)]);
}

public sealed record ForgeScriptModuleExecutionResult(
    ForgeScriptModuleAuthorizationDecision Authorization,
    ForgeScriptCompileResult? Compilation,
    ForgeScriptExecutionResult? Execution);

public sealed class ForgeScriptModuleExecutionService(IForgeScriptRuntime runtime)
{
    private readonly IForgeScriptRuntime _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

    public ForgeScriptModuleExecutionResult Execute(
        ForgeScriptModulePermissionPolicy? policy,
        ForgeScriptModuleAuthorizationRequest request,
        string source,
        ForgeScriptQuotas? quotas = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var authorization = ForgeScriptModuleAuthorizationService.Authorize(policy, request);
        if (!authorization.Succeeded || !authorization.ShouldExecute)
            return new(authorization, null, null);
        var sourceDiagnostics = ForgeScriptCapabilityManifest.ValidateSource(source);
        if (sourceDiagnostics.Count > 0)
        {
            var denied = new ForgeScriptModuleAuthorizationDecision(false, false,
                authorization.GrantedCapabilities,
                sourceDiagnostics.Select(item => new ForgeScriptModulePermissionDiagnostic(item.Code,
                    item.Path, item.SubjectId ?? policy!.Id, item.Severity, item.Message)).ToArray());
            return new(denied, null, null);
        }
        var compilation = _runtime.Compile(source);
        if (!compilation.Success || compilation.Artifact is null)
            return new(authorization, compilation, null);
        return new(authorization, compilation, _runtime.Execute(compilation.Artifact, quotas));
    }
}
