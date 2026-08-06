using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public sealed class SetExternalWorkerSecurityPolicyHandler
{
    public const string RevisionConflictCode = "asset.worker_security.revision_conflict";
    public const string PlanRequiredCode = "asset.worker_security.plan_required";
    public const string PlanNotPreviewedCode = "asset.worker_security.plan_not_previewed";
    public const string PermissionDeniedCode = "asset.worker_security.permission_denied";
    private readonly IClock _clock;
    private readonly Dictionary<Guid, (ExternalWorkerSecurityPolicyRevision Revision, string Fingerprint)> _previewedPlans = [];

    public SetExternalWorkerSecurityPolicyHandler(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public ExternalWorkerSecurityPolicyCommandResult Handle(
        ExternalWorkerSecurityPolicyState state,
        SetExternalWorkerSecurityPolicyCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, RevisionConflictCode, "External worker security policy changed concurrently.");
        if (command.PlanId == Guid.Empty)
            return Failure(state, command, PlanRequiredCode, "External worker security changes require a non-empty plan ID.");
        if (command.Permission < command.Policy.RequiredPermission)
            return Failure(state, command, PermissionDeniedCode, "External worker security permission is insufficient.");

        var diagnostics = ExternalWorkerSecurityPolicyValidator.Validate(command.Policy)
            .Select(item => new CommandDiagnostic(item.Code, $"{item.Path}: {item.Message}"))
            .ToArray();
        if (diagnostics.Length > 0)
            return new(false, state, command.BaseRevision, null, command.PlanId, diagnostics, null, null, Audit(command, false));

        var patch = new ProjectPatch("replace", "assets/worker-security",
            $"Keep arbitrary execution denied; allow {command.Policy.AllowedBlenderOperations.Count} typed operations, " +
            $"timeout {command.Policy.MaximumJobSeconds}s and output cap {command.Policy.MaximumOutputBytes} bytes.");
        var inverse = new ProjectPatch("replace", "assets/worker-security",
            $"Restore timeout {state.Policy.MaximumJobSeconds}s and output cap {state.Policy.MaximumOutputBytes} bytes.");
        if (command.DryRun)
        {
            lock (_previewedPlans)
            {
                if (_previewedPlans.Count >= 128) _previewedPlans.Clear();
                _previewedPlans[command.PlanId] = (command.BaseRevision, Fingerprint(command.Policy));
            }
            return new(true, state, command.BaseRevision, null, command.PlanId, [], patch, inverse, Audit(command, false));
        }

        lock (_previewedPlans)
        {
            if (!_previewedPlans.TryGetValue(command.PlanId, out var preview) ||
                preview.Revision != command.BaseRevision ||
                !string.Equals(preview.Fingerprint, Fingerprint(command.Policy), StringComparison.Ordinal))
                return Failure(state, command, PlanNotPreviewedCode,
                    "External worker security policy must match a successful dry-run at the current revision.");
            _previewedPlans.Remove(command.PlanId);
        }

        var revision = state.Revision.Next();
        return new(true, new ExternalWorkerSecurityPolicyState(command.Policy, revision), command.BaseRevision,
            revision, command.PlanId, [], patch, inverse, Audit(command, true));
    }

    private ExternalWorkerSecurityPolicyCommandResult Failure(
        ExternalWorkerSecurityPolicyState state,
        SetExternalWorkerSecurityPolicyCommand command,
        string code,
        string message) =>
        new(false, state, command.BaseRevision, null, command.PlanId,
            [new CommandDiagnostic(code, message)], null, null, Audit(command, false));

    private AuditEntry Audit(SetExternalWorkerSecurityPolicyCommand command, bool applied) =>
        new(command.CommandId, "set_external_worker_security_policy", _clock.UtcNow, applied);

    private static string Fingerprint(ExternalWorkerSecurityPolicy policy)
    {
        var value = string.Join('|', policy.Id, policy.SchemaVersion, policy.DenyFreeFormPython,
            policy.DenyShell, policy.DenyEval, policy.RequireContainedPaths, policy.NetworkAllowed,
            policy.MaximumJobSeconds, policy.MaximumOutputBytes, policy.RequiredPermission,
            policy.Provenance, string.Join(',', policy.AllowedBlenderOperations));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
