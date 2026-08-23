using System.Security.Cryptography;
using System.Text;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private ForgeScriptModulePermissionHistory? _modulePermissionHistoryUx6K;
    private ForgeScriptModulePermissionPolicy? _persistedModulePermissionUx6K;
    private StudioUx6KPolicyDraft? _approvedModulePermissionUx6K;
    private long? _approvedModulePermissionRevisionUx6K;
    private string? _approvedModulePermissionFingerprintUx6K;

    internal IReadOnlyList<StudioUx6KCapabilityChoice> ModulePermissionCapabilitiesUx6K() =>
        ForgeScriptCapabilityManifest.Capabilities
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new StudioUx6KCapabilityChoice(item.Id, item.DisplayName, item.Authority,
                item.Quota, item.ApiNames))
            .ToArray();

    internal StudioUx6KPolicyDraft? CurrentModulePermissionUx6K()
    {
        if (!EnsureModulePermissionUx6K(out _)) return null;
        var history = _modulePermissionHistoryUx6K!;
        return ToPermissionDraftUx6K(history.Policy, history.Revision);
    }

    internal bool ModulePermissionHasUnsavedChangesUx6K =>
        _modulePermissionHistoryUx6K is not null &&
        (_persistedModulePermissionUx6K is null ||
         !string.Equals(PermissionFingerprintUx6K(_persistedModulePermissionUx6K),
             PermissionFingerprintUx6K(_modulePermissionHistoryUx6K.Policy), StringComparison.Ordinal));

    internal StudioUx6KPreview PreviewModulePermissionUx6K(StudioUx6KPolicyDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelUx6KApproval();
        if (!EnsureModulePermissionUx6K(out var failure)) return FailedPermissionPreviewUx6K(failure);
        var history = _modulePermissionHistoryUx6K!;
        var diagnostics = ValidatePermissionDraftUx6K(draft, history).ToList();
        if (Project is null || draft.ModuleId != Project.Module.Id.Value)
            diagnostics.Add(new("script.permission.module_mismatch", "scriptPermissions/moduleId", "error",
                "Le manifeste doit appartenir au module ouvert."));
        if (diagnostics.Count > 0)
            return FailedPermissionPreviewUx6K("Aperçu refusé · le manifeste source a changé.", diagnostics);

        var candidate = ToPermissionPolicyUx6K(draft);
        var result = history.Preview(new ForgeScriptModulePermissionCommand(Guid.NewGuid(), history.Revision,
            candidate, "UX6-K R015 module permission preview"));
        var mapped = result.Diagnostics.Select(ToStudioPermissionDiagnosticUx6K).ToArray();
        if (!result.Succeeded)
            return new(false, false, true, "Aperçu refusé · corrigez les permissions localisées.",
                result.SemanticDiff, mapped);
        var changed = !string.Equals(PermissionFingerprintUx6K(history.Policy),
            PermissionFingerprintUx6K(candidate), StringComparison.Ordinal);
        if (changed)
        {
            _approvedModulePermissionUx6K = draft;
            _approvedModulePermissionRevisionUx6K = history.Revision;
            _approvedModulePermissionFingerprintUx6K = PermissionFingerprintUx6K(candidate);
        }
        return new(true, changed, true,
            changed
                ? "Aperçu sec valide · le manifeste peut être appliqué."
                : "Aperçu sec valide · aucune valeur n’a changé.",
            result.SemanticDiff, mapped);
    }

    internal StudioUx6KSimulation SimulateModulePermissionUx6K(
        StudioUx6KPolicyDraft draft,
        StudioUx6FDraft source)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(source);
        if (!EnsureModulePermissionUx6K(out var failure)) return FailedPermissionSimulationUx6K(failure);
        var history = _modulePermissionHistoryUx6K!;
        var diagnostics = ValidatePermissionDraftUx6K(draft, history);
        if (diagnostics.Count > 0)
            return FailedPermissionSimulationUx6K("Simulation refusée · le manifeste a changé.", diagnostics);
        var policy = ToPermissionPolicyUx6K(draft);
        var validation = ForgeScriptModulePermissionValidator.Validate(policy);
        if (validation.Count > 0)
            return FailedPermissionSimulationUx6K("Simulation refusée · corrigez le manifeste.",
                validation.Select(ToStudioPermissionDiagnosticUx6K).ToArray());
        var binding = policy.EventBindings.FirstOrDefault(item => item.Enabled);
        var kit = binding is null ? null : policy.KitGrants.FirstOrDefault(item =>
            string.Equals(item.KitId, binding.KitId, StringComparison.Ordinal));
        if (binding is null || kit is null)
            return FailedPermissionSimulationUx6K("Activez au moins une liaison vers un kit valide.");

        try
        {
            var requested = binding.Capabilities.ToHashSet(StringComparer.Ordinal);
            var expected = policy.AllowedCapabilities.Intersect(kit.Capabilities, StringComparer.Ordinal)
                .Intersect(binding.Capabilities, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
            var nominalRequest = PermissionRequestUx6K(policy, binding, requested, signerTrusted: true,
                development: false, ForgeScriptEventAuthority.Server);
            var runtime = new NativeForgeScriptRuntime();
            var service = new ForgeScriptModuleExecutionService(runtime);
            var nominal = service.Execute(policy, nominalRequest, source.Source);
            var trustedServer = nominal.Authorization.Succeeded && nominal.Authorization.ShouldExecute &&
                                nominal.Execution?.Status == ForgeScriptExecutionStatus.Completed;
            var intersection = nominal.Authorization.GrantedCapabilities.SetEquals(expected);
            var undeclared = ForgeScriptModuleAuthorizationService.Authorize(policy,
                nominalRequest with
                {
                    RequestedCapabilities = requested.Append("network").ToHashSet(StringComparer.Ordinal),
                });
            var undeclaredDenied = !undeclared.Succeeded &&
                                   undeclared.Diagnostics.Any(item =>
                                       item.Code == "script.permission.capability_denied");
            var unsigned = ForgeScriptModuleAuthorizationService.Authorize(policy,
                nominalRequest with { SignerTrusted = false });
            var unsignedDenied = !unsigned.Succeeded && unsigned.Diagnostics.Any(item =>
                item.Code == "script.permission.signature_untrusted");
            var development = ForgeScriptModuleAuthorizationService.Authorize(policy,
                nominalRequest with { SignerTrusted = false, IsDevelopment = true });
            var developmentHonored = policy.AllowUnsignedDevelopment
                ? development.Succeeded && development.ShouldExecute
                : !development.Succeeded && development.Diagnostics.Any(item =>
                    item.Code == "script.permission.signature_untrusted");
            var client = ForgeScriptModuleAuthorizationService.Authorize(policy,
                nominalRequest with { Authority = ForgeScriptEventAuthority.Local });
            var clientDenied = !client.Succeeded && client.Diagnostics.Any(item =>
                item.Code == "script.permission.authority_denied");
            var wrongModule = ForgeScriptModuleAuthorizationService.Authorize(policy,
                nominalRequest with { ModuleId = Guid.NewGuid() });
            var moduleDenied = !wrongModule.Succeeded && wrongModule.Diagnostics.Any(item =>
                item.Code == "script.permission.module_mismatch");
            var optional = ForgeScriptModuleAuthorizationService.Authorize(null,
                nominalRequest with { RequiredAtRuntime = false });
            var required = ForgeScriptModuleAuthorizationService.Authorize(null,
                nominalRequest with { RequiredAtRuntime = true });
            var optionalIgnored = optional.Succeeded && !optional.ShouldExecute;
            var requiredDiagnosed = !required.Succeeded && required.Diagnostics.Any(item =>
                item.Code == "script.permission.required_missing");
            var abusive = service.Execute(policy, nominalRequest,
                "void main() { while (true) { int bounded = 1; } }",
                new ForgeScriptQuotas(instructionLimit: 10));
            var quotaBlocked = abusive.Execution?.Status == ForgeScriptExecutionStatus.QuotaExceeded;
            var recovered = service.Execute(policy, nominalRequest,
                "void main() { say(\"recovered-r015\"); }");
            var runtimeRecovered = recovered.Execution?.Status == ForgeScriptExecutionStatus.Completed &&
                                   recovered.Execution.Events.Any(item => item.Value == "recovered-r015");
            var sourceCompiled = nominal.Compilation?.Success == true;
            var succeeded = trustedServer && intersection && undeclaredDenied && unsignedDenied &&
                            developmentHonored && clientDenied && moduleDenied && optionalIgnored &&
                            requiredDiagnosed && quotaBlocked && runtimeRecovered && sourceCompiled;
            var events = new[]
            {
                $"Serveur fiable · {(trustedServer ? "autorisé" : "refusé")}",
                $"Capacités accordées · {string.Join(", ", expected.Order(StringComparer.Ordinal))}",
                "Capacité non déclarée · refusée",
                "Distribution non signée · refusée hors développement explicite",
                "Autorité locale · refusée",
                "Quota abusif · interrompu, runtime récupéré",
            };
            return new(succeeded,
                succeeded
                    ? "Autorité et sandbox R015 cohérentes · les permissions sont appliquées avant compilation."
                    : "La simulation R015 n’a pas satisfait tous les invariants.",
                trustedServer, intersection, undeclaredDenied, unsignedDenied, developmentHonored,
                clientDenied, moduleDenied, optionalIgnored, requiredDiagnosed, quotaBlocked,
                runtimeRecovered, sourceCompiled, events,
                nominal.Compilation?.Diagnostics.Select(ToStudioDiagnosticUx6F).ToArray() ?? []);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or
                                           FormatException or DllNotFoundException or BadImageFormatException)
        {
            return FailedPermissionSimulationUx6K($"Simulation R015 refusée : {exception.Message}",
                [new("script.permission.simulation_failed", "scriptPermissions/simulation", "error",
                    exception.Message)]);
        }
    }

    internal StudioUx6KActionResult ApplyApprovedModulePermissionUx6K(StudioUx6KPolicyDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!EnsureModulePermissionUx6K(out var failure)) return FailedPermissionActionUx6K(failure);
        var history = _modulePermissionHistoryUx6K!;
        var candidate = ToPermissionPolicyUx6K(draft);
        var fingerprint = PermissionFingerprintUx6K(candidate);
        if (_approvedModulePermissionUx6K is null ||
            _approvedModulePermissionRevisionUx6K != history.Revision ||
            !string.Equals(_approvedModulePermissionFingerprintUx6K, fingerprint, StringComparison.Ordinal) ||
            !string.Equals(PermissionFingerprintUx6K(ToPermissionPolicyUx6K(_approvedModulePermissionUx6K)),
                fingerprint, StringComparison.Ordinal) ||
            ValidatePermissionDraftUx6K(draft, history).Count > 0)
            return FailedPermissionActionUx6K("Application refusée · relancez l’aperçu sec du brouillon courant.");
        var command = new ForgeScriptModulePermissionCommand(Guid.NewGuid(), history.Revision, candidate,
            "UX6-K R015 module permission apply");
        var preview = history.Preview(command);
        if (!preview.Succeeded)
            return FailedPermissionActionUx6K(string.Join(" · ", preview.Diagnostics.Select(item => item.Code)));
        try
        {
            new ForgeScriptModulePermissionStore(Session!.RootDirectory).Save(candidate);
            var applied = history.Apply(command);
            if (!applied.Succeeded) return FailedPermissionActionUx6K("La commande canonique a été refusée.");
            _persistedModulePermissionUx6K = candidate;
            CancelUx6KApproval();
            return new(true, "Permissions R015 appliquées atomiquement.", history.CanUndo, history.CanRedo);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException)
        {
            return FailedPermissionActionUx6K($"Écriture refusée : {exception.Message}");
        }
    }

    internal StudioUx6KActionResult UndoModulePermissionUx6K() => MutatePermissionHistoryUx6K(undo: true);
    internal StudioUx6KActionResult RedoModulePermissionUx6K() => MutatePermissionHistoryUx6K(undo: false);

    internal StudioUx6KActionResult SaveModulePermissionUx6K()
    {
        if (!EnsureModulePermissionUx6K(out var failure)) return FailedPermissionActionUx6K(failure);
        try
        {
            var policy = _modulePermissionHistoryUx6K!.Policy;
            new ForgeScriptModulePermissionStore(Session!.RootDirectory).Save(policy);
            _persistedModulePermissionUx6K = policy;
            return new(true, "Manifeste R015 enregistré.", _modulePermissionHistoryUx6K.CanUndo,
                _modulePermissionHistoryUx6K.CanRedo);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException)
        {
            return FailedPermissionActionUx6K($"Enregistrement refusé : {exception.Message}");
        }
    }

    internal StudioUx6KApprovalSnapshot CaptureModulePermissionApprovalUx6K() =>
        new(_approvedModulePermissionUx6K, _approvedModulePermissionRevisionUx6K,
            _approvedModulePermissionFingerprintUx6K);

    internal void RestoreModulePermissionApprovalUx6K(StudioUx6KApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedModulePermissionUx6K = snapshot.Draft;
        _approvedModulePermissionRevisionUx6K = snapshot.Revision;
        _approvedModulePermissionFingerprintUx6K = snapshot.CandidateFingerprint;
    }

    internal void CancelUx6KApproval()
    {
        _approvedModulePermissionUx6K = null;
        _approvedModulePermissionRevisionUx6K = null;
        _approvedModulePermissionFingerprintUx6K = null;
    }

    internal void CloseUx6K()
    {
        _modulePermissionHistoryUx6K = null;
        _persistedModulePermissionUx6K = null;
        CancelUx6KApproval();
    }

    internal StudioUx6KSmokeResult RunUx6KContractSmoke()
    {
        var sourceProject = Project;
        if (Session is null || sourceProject is null || History is null) return FailedPermissionSmokeUx6K();
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux6-k", Guid.NewGuid().ToString("N"));
        var released = false;
        StudioUx6KSmokeResult result;
        try
        {
            if (!ProjectSession.Create(root, sourceProject).Succeeded) return FailedPermissionSmokeUx6K();
            var sourceDocument = EvidenceDocumentUx6F();
            new ForgeScriptSourceStore(root).Save(sourceDocument);
            var editing = new StudioSessionController();
            if (!editing.Open(root).Succeeded ||
                !editing.TryOpenCanonicalSourceUx6F(sourceDocument.RelativePath, out var source, out _) ||
                source is null)
                return FailedPermissionSmokeUx6K();
            var initial = editing.CurrentModulePermissionUx6K();
            var capabilities = editing.ModulePermissionCapabilitiesUx6K();
            if (initial is null || capabilities.Count != ForgeScriptCapabilityManifest.Capabilities.Count)
                return FailedPermissionSmokeUx6K();
            var candidate = initial with
            {
                Provenance = "studio:ux6-k:r015",
                AllowUnsignedDevelopment = false,
            };
            var invalidDeny = !editing.PreviewModulePermissionUx6K(candidate with
            { DenyByDefault = false }).Succeeded;
            var invalidServer = !editing.PreviewModulePermissionUx6K(candidate with
            { ServerEnforced = false }).Succeeded;
            var invalidSignature = !editing.PreviewModulePermissionUx6K(candidate with
            { RequireSignedDistribution = false }).Succeeded;
            var invalidCapability = !editing.PreviewModulePermissionUx6K(candidate with
            {
                AllowedCapabilities = candidate.AllowedCapabilities.Append("network")
                    .ToHashSet(StringComparer.Ordinal),
            }).Succeeded;
            var invalidBinding = !editing.PreviewModulePermissionUx6K(candidate with
            {
                EventBindings = candidate.EventBindings.Select((item, index) => index == 0
                    ? item with { Authority = ForgeScriptEventAuthority.Local }
                    : item).ToArray(),
            }).Succeeded;
            var preview = editing.PreviewModulePermissionUx6K(candidate);
            var store = new ForgeScriptModulePermissionStore(root);
            var dryRun = preview.Succeeded && preview.CanApply && !File.Exists(store.Path) &&
                         editing.CurrentModulePermissionUx6K()?.AllowUnsignedDevelopment ==
                         initial.AllowUnsignedDevelopment;
            var simulation = editing.SimulateModulePermissionUx6K(candidate, source);
            var stale = !editing.ApplyApprovedModulePermissionUx6K(candidate with
            { ApiVersion = "1.0" }).Succeeded;
            _ = editing.PreviewModulePermissionUx6K(candidate);
            var applied = editing.ApplyApprovedModulePermissionUx6K(candidate);
            var appliedDocument = store.Load();
            var undone = editing.UndoModulePermissionUx6K();
            var undoDocument = store.Load();
            var redone = editing.RedoModulePermissionUx6K();
            var redoDocument = store.Load();
            var saved = editing.SaveModulePermissionUx6K();
            var reopened = new StudioSessionController();
            var persistence = reopened.Open(root).Succeeded &&
                              reopened.CurrentModulePermissionUx6K() is { } reopenedDraft &&
                              PermissionFingerprintUx6K(ToPermissionPolicyUx6K(reopenedDraft)) ==
                              PermissionFingerprintUx6K(ToPermissionPolicyUx6K(candidate));
            reopened.Close();
            var applyUndoRedo = applied.Succeeded && undone.Succeeded && redone.Succeeded && saved.Succeeded &&
                                PermissionFingerprintUx6K(appliedDocument) ==
                                PermissionFingerprintUx6K(ToPermissionPolicyUx6K(candidate)) &&
                                PermissionFingerprintUx6K(undoDocument) ==
                                PermissionFingerprintUx6K(ToPermissionPolicyUx6K(initial)) &&
                                PermissionFingerprintUx6K(redoDocument) ==
                                PermissionFingerprintUx6K(ToPermissionPolicyUx6K(candidate));
            var sourcePreserved = SourceDocumentContentsEqualUx6K(
                new ForgeScriptSourceStore(root).Load(sourceDocument.RelativePath), sourceDocument);
            var neighbors = editing.Project is not null && string.Equals(
                ProjectJsonSerializer.Serialize(editing.Project), ProjectJsonSerializer.Serialize(sourceProject),
                StringComparison.Ordinal);
            var invalid = invalidDeny && invalidServer && invalidSignature && invalidCapability && invalidBinding;
            var intersection = simulation.CapabilityIntersection && simulation.UndeclaredCapabilityDenied;
            var authority = simulation.TrustedServerExecuted && simulation.UntrustedSignatureDenied &&
                            simulation.DevelopmentPolicyHonored && simulation.ClientAuthorityDenied &&
                            simulation.ModuleMismatchDenied && simulation.MissingOptionalIgnored &&
                            simulation.MissingRequiredDiagnosed && simulation.QuotaAbuseBlocked &&
                            simulation.RuntimeRecovered && simulation.CanonicalSourceCompiled;
            var succeeded = preview.Succeeded && simulation.Succeeded && dryRun && invalid && stale &&
                            applyUndoRedo && persistence && sourcePreserved && neighbors && intersection && authority;
            result = new(succeeded, true, capabilities.Count == 6,
                initial.SchemaVersion == ForgeScriptModulePermissionPolicy.CurrentSchemaVersion,
                candidate.DenyByDefault, candidate.ServerEnforced, candidate.RequireSignedDistribution,
                intersection, authority, invalid, dryRun, stale, applyUndoRedo, persistence, sourcePreserved,
                neighbors, false);
            editing.Close();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException or FormatException or
                                           DllNotFoundException or BadImageFormatException)
        {
            result = FailedPermissionSmokeUx6K();
        }
        finally
        {
            released = DeleteUx6FTemporaryWorkspace(root);
        }
        return result with { Succeeded = result.Succeeded && released, TemporaryWorkspaceReleased = released };
    }

    private StudioUx6KActionResult MutatePermissionHistoryUx6K(bool undo)
    {
        if (!EnsureModulePermissionUx6K(out var failure)) return FailedPermissionActionUx6K(failure);
        var history = _modulePermissionHistoryUx6K!;
        var result = undo ? history.Undo() : history.Redo();
        if (!result.Succeeded) return FailedPermissionActionUx6K(undo ? "Rien à annuler." : "Rien à rétablir.");
        try
        {
            new ForgeScriptModulePermissionStore(Session!.RootDirectory).Save(result.Policy);
            _persistedModulePermissionUx6K = result.Policy;
            CancelUx6KApproval();
            return new(true, undo ? "Permissions annulées." : "Permissions rétablies.", history.CanUndo,
                history.CanRedo);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException)
        {
            return FailedPermissionActionUx6K($"Persistance refusée : {exception.Message}");
        }
    }

    private bool EnsureModulePermissionUx6K(out string failure)
    {
        failure = string.Empty;
        if (_modulePermissionHistoryUx6K is not null) return true;
        if (Session is null || Project is null)
        {
            failure = "Aucun module ouvert.";
            return false;
        }
        try
        {
            var store = new ForgeScriptModulePermissionStore(Session.RootDirectory);
            var policy = File.Exists(store.Path)
                ? store.Load()
                : ForgeScriptModulePermissionPolicy.CreateDefault(Project.Module.Id.Value);
            if (policy.ModuleId != Project.Module.Id.Value)
            {
                failure = "Le manifeste R015 appartient à un autre module.";
                return false;
            }
            _modulePermissionHistoryUx6K = new ForgeScriptModulePermissionHistory(policy);
            _persistedModulePermissionUx6K = File.Exists(store.Path) ? policy : null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or System.Text.Json.JsonException)
        {
            failure = $"Manifeste R015 illisible : {exception.Message}";
            return false;
        }
    }

    private static IReadOnlyList<StudioUx6FDiagnostic> ValidatePermissionDraftUx6K(
        StudioUx6KPolicyDraft draft,
        ForgeScriptModulePermissionHistory history)
    {
        var diagnostics = new List<StudioUx6FDiagnostic>();
        if (draft.BaseRevision != history.Revision)
            diagnostics.Add(new("script.permission.revision_conflict", "scriptPermissions/revision", "error",
                "La révision du manifeste a changé."));
        if (!string.Equals(draft.SourceFingerprint, PermissionFingerprintUx6K(history.Policy),
                StringComparison.Ordinal))
            diagnostics.Add(new("script.permission.source_changed", "scriptPermissions", "error",
                "Le manifeste source a changé depuis son chargement."));
        return diagnostics;
    }

    private static StudioUx6KPolicyDraft ToPermissionDraftUx6K(
        ForgeScriptModulePermissionPolicy policy,
        long revision) => new(policy.Id, policy.SchemaVersion, policy.Provenance, policy.ApiVersion,
        policy.ModuleId, policy.DenyByDefault, policy.ServerEnforced, policy.RequireSignedDistribution,
        policy.AllowUnsignedDevelopment, policy.RequiredAtRuntime,
        policy.AllowedCapabilities.ToHashSet(StringComparer.Ordinal), policy.KitGrants.ToArray(),
        policy.EventBindings.ToArray(), revision, PermissionFingerprintUx6K(policy));

    private static ForgeScriptModulePermissionPolicy ToPermissionPolicyUx6K(StudioUx6KPolicyDraft draft) =>
        new(draft.Id, draft.SchemaVersion, draft.Provenance, draft.ApiVersion, draft.ModuleId,
            draft.DenyByDefault, draft.ServerEnforced, draft.RequireSignedDistribution,
            draft.AllowUnsignedDevelopment, draft.RequiredAtRuntime,
            draft.AllowedCapabilities.ToHashSet(StringComparer.Ordinal), draft.KitGrants.ToArray(),
            draft.EventBindings.ToArray());

    private static ForgeScriptModuleAuthorizationRequest PermissionRequestUx6K(
        ForgeScriptModulePermissionPolicy policy,
        ForgeScriptPermissionEventBinding binding,
        IReadOnlySet<string> requested,
        bool signerTrusted,
        bool development,
        ForgeScriptEventAuthority authority) => new(policy.ModuleId, binding.KitId, binding.EventName,
        binding.Handler, requested, authority, signerTrusted, development, policy.RequiredAtRuntime);

    private static string PermissionFingerprintUx6K(ForgeScriptModulePermissionPolicy policy)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(policy.Id.ToByteArray());
        writer.Write(policy.SchemaVersion);
        WritePermissionStringUx6K(writer, policy.Provenance);
        WritePermissionStringUx6K(writer, policy.ApiVersion);
        writer.Write(policy.ModuleId.ToByteArray());
        writer.Write(policy.DenyByDefault);
        writer.Write(policy.ServerEnforced);
        writer.Write(policy.RequireSignedDistribution);
        writer.Write(policy.AllowUnsignedDevelopment);
        writer.Write(policy.RequiredAtRuntime);
        WritePermissionSetUx6K(writer, policy.AllowedCapabilities);
        writer.Write(policy.KitGrants.Count);
        foreach (var grant in policy.KitGrants)
        {
            writer.Write(grant.Id.ToByteArray());
            writer.Write(grant.SchemaVersion);
            WritePermissionStringUx6K(writer, grant.Provenance);
            WritePermissionStringUx6K(writer, grant.KitId);
            WritePermissionSetUx6K(writer, grant.Capabilities);
        }
        writer.Write(policy.EventBindings.Count);
        foreach (var binding in policy.EventBindings)
        {
            writer.Write(binding.Id.ToByteArray());
            writer.Write(binding.SchemaVersion);
            WritePermissionStringUx6K(writer, binding.Provenance);
            WritePermissionStringUx6K(writer, binding.EventName);
            WritePermissionStringUx6K(writer, binding.Handler);
            WritePermissionStringUx6K(writer, binding.KitId);
            WritePermissionSetUx6K(writer, binding.Capabilities);
            writer.Write((int)binding.Authority);
            writer.Write(binding.Enabled);
        }
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WritePermissionSetUx6K(BinaryWriter writer, IEnumerable<string> values)
    {
        var ordered = values.Order(StringComparer.Ordinal).ToArray();
        writer.Write(ordered.Length);
        foreach (var value in ordered) WritePermissionStringUx6K(writer, value);
    }

    private static void WritePermissionStringUx6K(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static bool SourceDocumentContentsEqualUx6K(
        ForgeScriptSourceDocument left,
        ForgeScriptSourceDocument right) =>
        left.Id == right.Id && left.SchemaVersion == right.SchemaVersion &&
        string.Equals(left.Provenance, right.Provenance, StringComparison.Ordinal) &&
        string.Equals(left.RelativePath, right.RelativePath, StringComparison.Ordinal) &&
        string.Equals(left.Source, right.Source, StringComparison.Ordinal) &&
        string.Equals(left.ApiVersion, right.ApiVersion, StringComparison.Ordinal) &&
        left.Distribution == right.Distribution && left.RequiredAtRuntime == right.RequiredAtRuntime &&
        left.Dependencies.SequenceEqual(right.Dependencies, StringComparer.Ordinal) &&
        left.EventBindings.SequenceEqual(right.EventBindings) &&
        (left.RequiredCapabilities ?? new HashSet<string>(StringComparer.Ordinal)).SetEquals(
            right.RequiredCapabilities ?? new HashSet<string>(StringComparer.Ordinal));

    private static StudioUx6FDiagnostic ToStudioPermissionDiagnosticUx6K(
        ForgeScriptModulePermissionDiagnostic diagnostic) =>
        new(diagnostic.Code, diagnostic.Path, diagnostic.Severity, diagnostic.Message);

    private static StudioUx6KPreview FailedPermissionPreviewUx6K(string message,
        IReadOnlyList<StudioUx6FDiagnostic>? diagnostics = null) =>
        new(false, false, true, message, "no-change", diagnostics ?? []);

    private static StudioUx6KSimulation FailedPermissionSimulationUx6K(string message,
        IReadOnlyList<StudioUx6FDiagnostic>? diagnostics = null) =>
        new(false, message, false, false, false, false, false, false, false, false, false, false,
            false, false, [], diagnostics ?? []);

    private StudioUx6KActionResult FailedPermissionActionUx6K(string message) =>
        new(false, message, _modulePermissionHistoryUx6K?.CanUndo == true,
            _modulePermissionHistoryUx6K?.CanRedo == true);

    private static StudioUx6KSmokeResult FailedPermissionSmokeUx6K() =>
        new(false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false);
}
