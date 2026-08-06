using Forge.Domain.Assistant;
using Forge.Domain.Validation;

namespace Forge.Domain.Assets;

public sealed record ExternalWorkerSecurityPolicy
{
    public const int CurrentSchemaVersion = 1;
    public const int MinimumJobSeconds = 5;
    public const int MaximumJobSecondsLimit = 900;
    public const long MinimumOutputBytes = 1024 * 1024;
    public const long MaximumOutputBytesLimit = 2L * 1024 * 1024 * 1024;
    public static readonly Guid DefaultId = Guid.Parse("a1230000-0000-4000-8000-000000000001");

    public Guid Id { get; init; }
    public int SchemaVersion { get; init; }
    public bool DenyFreeFormPython { get; init; }
    public bool DenyShell { get; init; }
    public bool DenyEval { get; init; }
    public bool RequireContainedPaths { get; init; }
    public bool NetworkAllowed { get; init; }
    public int MaximumJobSeconds { get; init; }
    public long MaximumOutputBytes { get; init; }
    public IReadOnlyList<AssetExternalOperation> AllowedBlenderOperations { get; init; }
    public AssistantPermissionLevel RequiredPermission { get; init; }
    public string Provenance { get; init; }

    public ExternalWorkerSecurityPolicy(
        Guid id,
        int schemaVersion,
        bool denyFreeFormPython,
        bool denyShell,
        bool denyEval,
        bool requireContainedPaths,
        bool networkAllowed,
        int maximumJobSeconds,
        long maximumOutputBytes,
        IReadOnlyList<AssetExternalOperation>? allowedBlenderOperations,
        AssistantPermissionLevel requiredPermission,
        string provenance)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        DenyFreeFormPython = denyFreeFormPython;
        DenyShell = denyShell;
        DenyEval = denyEval;
        RequireContainedPaths = requireContainedPaths;
        NetworkAllowed = networkAllowed;
        MaximumJobSeconds = maximumJobSeconds;
        MaximumOutputBytes = maximumOutputBytes;
        AllowedBlenderOperations = (allowedBlenderOperations ?? []).OrderBy(value => value).ToArray();
        RequiredPermission = requiredPermission;
        Provenance = (provenance ?? string.Empty).Trim();
    }

    public static ExternalWorkerSecurityPolicy CreateDefault() =>
        new(
            DefaultId,
            CurrentSchemaVersion,
            true,
            true,
            true,
            true,
            false,
            300,
            512L * 1024 * 1024,
            ExternalWorkerSecurityPolicyValidator.SupportedBlenderOperations,
            AssistantPermissionLevel.Configure,
            "openrpg-forge:g12-l:secure-worker-default");
}

public static class ExternalWorkerSecurityPolicyValidator
{
    public const string SchemaUnsupportedCode = "asset.worker_security.schema_unsupported";
    public const string IdInvalidCode = "asset.worker_security.id_invalid";
    public const string UnsafeExecutionCode = "asset.worker_security.unsafe_execution";
    public const string NetworkForbiddenCode = "asset.worker_security.network_forbidden";
    public const string TimeoutInvalidCode = "asset.worker_security.timeout_invalid";
    public const string OutputLimitInvalidCode = "asset.worker_security.output_limit_invalid";
    public const string OperationInvalidCode = "asset.worker_security.operation_invalid";
    public const string RequiredOperationMissingCode = "asset.worker_security.required_operation_missing";
    public const string PermissionInvalidCode = "asset.worker_security.permission_invalid";
    public const string ProvenanceMissingCode = "asset.worker_security.provenance_missing";

    public static IReadOnlyList<AssetExternalOperation> SupportedBlenderOperations { get; } =
    [
        AssetExternalOperation.ValidateModel,
        AssetExternalOperation.NormalizeModel,
        AssetExternalOperation.GenerateCollision,
        AssetExternalOperation.GenerateLods,
        AssetExternalOperation.ExportGlb,
        AssetExternalOperation.GenerateThumbnail,
        AssetExternalOperation.AuditQuality,
        AssetExternalOperation.CreatePrimitives,
    ];

    public static IReadOnlyList<DomainDiagnostic> Validate(ExternalWorkerSecurityPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        const string path = "assets/worker-security";
        var diagnostics = new List<DomainDiagnostic>();
        if (policy.SchemaVersion != ExternalWorkerSecurityPolicy.CurrentSchemaVersion)
            diagnostics.Add(new(SchemaUnsupportedCode, "Unsupported external worker security schema.", $"{path}/schemaVersion"));
        if (policy.Id == Guid.Empty)
            diagnostics.Add(new(IdInvalidCode, "External worker security policy ID cannot be empty.", $"{path}/id"));
        if (!policy.DenyFreeFormPython || !policy.DenyShell || !policy.DenyEval || !policy.RequireContainedPaths)
            diagnostics.Add(new(UnsafeExecutionCode,
                "Free-form Python, shell, eval and unconfined paths must remain forbidden.", $"{path}/execution"));
        if (policy.NetworkAllowed)
            diagnostics.Add(new(NetworkForbiddenCode, "The Blender worker cannot receive network access.", $"{path}/networkAllowed"));
        if (policy.MaximumJobSeconds is < ExternalWorkerSecurityPolicy.MinimumJobSeconds or > ExternalWorkerSecurityPolicy.MaximumJobSecondsLimit)
            diagnostics.Add(new(TimeoutInvalidCode,
                $"Worker timeout must be between {ExternalWorkerSecurityPolicy.MinimumJobSeconds} and {ExternalWorkerSecurityPolicy.MaximumJobSecondsLimit} seconds.",
                $"{path}/maximumJobSeconds"));
        if (policy.MaximumOutputBytes is < ExternalWorkerSecurityPolicy.MinimumOutputBytes or > ExternalWorkerSecurityPolicy.MaximumOutputBytesLimit)
            diagnostics.Add(new(OutputLimitInvalidCode,
                "Worker output limit must stay between 1 MiB and 2 GiB.", $"{path}/maximumOutputBytes"));

        var supported = SupportedBlenderOperations.ToHashSet();
        if (policy.AllowedBlenderOperations.Count == 0 ||
            policy.AllowedBlenderOperations.Distinct().Count() != policy.AllowedBlenderOperations.Count ||
            policy.AllowedBlenderOperations.Any(operation => !supported.Contains(operation)))
            diagnostics.Add(new(OperationInvalidCode,
                "Worker operations must be unique members of the typed Blender allowlist.", $"{path}/allowedBlenderOperations"));
        if (!policy.AllowedBlenderOperations.Contains(AssetExternalOperation.ValidateModel) ||
            !policy.AllowedBlenderOperations.Contains(AssetExternalOperation.ExportGlb))
            diagnostics.Add(new(RequiredOperationMissingCode,
                "Worker policy must retain validation and GLB export.", $"{path}/allowedBlenderOperations"));
        if (policy.RequiredPermission < AssistantPermissionLevel.Configure)
            diagnostics.Add(new(PermissionInvalidCode,
                "Changing worker security requires Configure permission or higher.", $"{path}/requiredPermission"));
        if (string.IsNullOrWhiteSpace(policy.Provenance))
            diagnostics.Add(new(ProvenanceMissingCode,
                "External worker security policy requires modification provenance.", $"{path}/provenance"));
        return diagnostics;
    }
}
