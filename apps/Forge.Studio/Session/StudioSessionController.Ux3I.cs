using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Forge.Application.Assets;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.RuntimeExtensions;
using Forge.Application.Time;
using Forge.Domain.Assets;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetAssetGovernanceHandler _setGovernanceUx3I = new(new SystemClock());
    private readonly SetRuntimeExtensionsHandler _setRuntimeExtensionsUx3I = new(new SystemClock());
    private AssetGovernanceCommandHistory? _governanceHistoryUx3I;
    private string? _governanceWorkspaceRootUx3I;
    private IReadOnlyList<StudioUx3IAssetChoice> _governanceAssetsUx3I = [];
    private bool _useEvidenceScannerUx3I;
    private StudioUx3IGovernanceDraft? _approvedGovernanceUx3I;
    private AssetGovernanceRevision? _approvedGovernanceRevisionUx3I;
    private string? _approvedGovernanceSourceUx3I;
    private string? _approvedGovernanceDraftUx3I;
    private StudioUx3IMediaDraft? _approvedMediaUx3I;
    private ProjectRevision? _approvedMediaRevisionUx3I;
    private string? _approvedMediaSourceUx3I;
    private string? _approvedMediaDraftUx3I;
    private StudioUx3IQuarantineReceipt? _quarantineReceiptUx3I;
    private bool _governanceCanonicalCommandUx3I;
    private bool _mediaCanonicalCommandUx3I;
    private bool _lastUx3ITemporaryReleased = true;

    internal bool CanUndoGovernanceUx3I => _quarantineReceiptUx3I is { Undone: false };
    internal bool CanRedoGovernanceUx3I => _quarantineReceiptUx3I is { Undone: true };
    internal bool LastUx3ITemporaryReleased => _lastUx3ITemporaryReleased;

    internal IReadOnlyList<StudioUx3IAssetChoice> ListGovernanceAssetsUx3I()
    {
        return EnsureGovernanceStateUx3I(out _) ? _governanceAssetsUx3I : [];
    }

    internal StudioUx3IGovernanceDraft? CreateGovernanceDraftUx3I(AssetCatalogItemId assetId)
    {
        if (!EnsureGovernanceStateUx3I(out _)) return null;
        var choice = _governanceAssetsUx3I.FirstOrDefault(item => item.AssetId == assetId);
        if (choice is null) return null;
        var existing = _governanceHistoryUx3I!.State.Find(assetId);
        var quarantine = existing?.Quarantine;
        var ip = existing?.IpBoundary;
        return new(
            _governanceHistoryUx3I.State.Revision,
            GovernanceStateFingerprintUx3I(_governanceHistoryUx3I.State),
            choice,
            existing?.LicenseSpdx ?? choice.LicenseSpdx,
            existing?.Author ?? choice.Author,
            existing?.SourceUrl ?? choice.SourceUrl,
            existing?.PermissionEvidence ?? choice.PermissionEvidence,
            existing?.Modifications ?? "Import original non modifié ; transformations à consigner.",
            existing?.AiGenerated ?? false,
            existing?.AiModel ?? string.Empty,
            existing?.AiPromptPolicySha256 ?? string.Empty,
            existing?.PlatformBudgetBytes ?? Math.Max(1, choice.Bytes),
            ip?.Origin ?? AssetOriginKind.OpenLicensed,
            ip?.RedistributionAllowed ?? true,
            ip?.CompatibilityIsolated ?? false,
            ip?.LegalReviewReference ?? string.Empty,
            quarantine?.Status != AssetQuarantineStatus.Rejected,
            quarantine?.Reviewer ?? "Studio author",
            Approved: false);
    }

    internal StudioUx3IGovernancePreview PreviewGovernanceUx3I(StudioUx3IGovernanceDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelGovernanceApprovalUx3I();
        if (!EnsureGovernanceStateUx3I(out var stateError))
            return FailedGovernanceUx3I(stateError, "asset.governance.unavailable", "assets/governance");
        var currentChoice = _governanceAssetsUx3I.FirstOrDefault(item => item.AssetId == draft.Asset.AssetId);
        if (currentChoice is null)
            return FailedGovernanceUx3I("Choisissez explicitement un asset disponible.",
                "asset.governance.selection_required", "assets/governance");
        if (_governanceHistoryUx3I!.State.Revision != draft.BaseRevision ||
            !string.Equals(GovernanceStateFingerprintUx3I(_governanceHistoryUx3I.State),
                draft.SourceFingerprint, StringComparison.Ordinal))
            return FailedGovernanceUx3I("Le registre a changé ; rechargez le dossier.",
                SetAssetGovernanceHandler.RevisionConflictCode, "assets/governance");

        var staged = StageQuarantineDryRunUx3I(draft, out var review, out var scannerLabel);
        if (!staged.Succeeded || review is null)
            return new(false, false, false, false, false, true,
                staged.Diagnostics.FirstOrDefault()?.Message ?? "La quarantaine a refusé la source.",
                "Aucun fichier écrit.", "Aucun changement.", [],
                staged.Diagnostics.Select(item => new StudioUx3IDiagnostic(item.Code, item.Path,
                    HumanUx3I(item.Message))).ToArray(), scannerLabel,
                "Le test est exécuté dans un dossier temporaire supprimé immédiatement.");

        AssetGovernanceRecord candidate;
        try
        {
            var decided = AssetImportQuarantine.Decide(review, draft.ApproveImport,
                string.IsNullOrWhiteSpace(draft.Reviewer) ? "Studio author" : draft.Reviewer,
                DateTimeOffset.UnixEpoch);
            candidate = BuildGovernanceRecordUx3I(draft, decided);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return FailedGovernanceUx3I(exception.Message, "asset.governance.invalid", "assets/governance");
        }

        var command = new SetAssetGovernanceCommand(CommandId.From(Guid.NewGuid()),
            _governanceHistoryUx3I.State.Revision, candidate, DryRun: true);
        var result = _setGovernanceUx3I.Handle(_governanceHistoryUx3I.State, command);
        var distributionDiagnostics = AssetGovernanceValidator.Validate(candidate);
        var distributionReady = distributionDiagnostics.Count == 0;
        var diagnostics = result.Diagnostics.Select(item => new StudioUx3IDiagnostic(item.Code,
            $"assets/governance/{draft.Asset.Label}", HumanUx3I(item.Message))).ToArray();
        var dryRun = result.Succeeded && result.Patch is not null && result.Inverse is not null &&
                     !result.Audit.Applied;
        var succeeded = dryRun && diagnostics.Length == 0;
        if (succeeded && draft.Approved)
        {
            _approvedGovernanceUx3I = FreezeGovernanceDraftUx3I(draft);
            _approvedGovernanceRevisionUx3I = draft.BaseRevision;
            _approvedGovernanceSourceUx3I = draft.SourceFingerprint;
            _approvedGovernanceDraftUx3I = GovernanceDraftFingerprintUx3I(_approvedGovernanceUx3I);
        }
        var previous = _governanceHistoryUx3I.State.Find(draft.Asset.AssetId);
        var diff = new[]
        {
            $"Licence : {previous?.LicenseSpdx ?? "absente"} → {candidate.LicenseSpdx}",
            $"Provenance : {HumanUx3I(previous?.Author ?? "absente")} → {HumanUx3I(candidate.Author)}",
            $"Divulgation IA : {(previous?.AiGenerated == true ? "oui" : "non")} → {(candidate.AiGenerated ? "oui" : "non")}",
            $"Quarantaine : {HumanQuarantineUx3I(previous?.Quarantine.Status)} → {HumanQuarantineUx3I(candidate.Quarantine.Status)}",
            $"Distribution : {(distributionReady ? "prête selon le contrat" : "bloquée")}",
        };
        return new(succeeded, succeeded && draft.Approved, dryRun, true, distributionReady, true,
            succeeded
                ? "Aperçu sec prêt ; le scan porte sur une copie temporaire et aucun registre n’est modifié."
                : diagnostics.FirstOrDefault()?.Message ?? "Le dossier doit être corrigé.",
            HumanUx3I(result.Patch?.Summary ?? "Préparer le dossier de gouvernance."),
            HumanUx3I(result.Inverse?.Summary ?? "Restaurer le dossier précédent."), diff, diagnostics,
            scannerLabel,
            $"Copier {HumanUx3I(draft.Asset.SourceRelativePath)} vers la quarantaine confinée, " +
            $"puis {(draft.ApproveImport ? "soumettre l’approbation humaine" : "enregistrer le refus et supprimer la copie")}.");
    }

    internal StudioUx3IActionResult ApplyApprovedGovernanceUx3I(StudioUx3IGovernanceDraft current)
    {
        if (!EnsureGovernanceStateUx3I(out var error)) return new(false, error);
        var approved = _approvedGovernanceUx3I;
        if (approved is null || _approvedGovernanceRevisionUx3I is null ||
            _approvedGovernanceSourceUx3I is null || _approvedGovernanceDraftUx3I is null ||
            _governanceHistoryUx3I!.State.Revision != _approvedGovernanceRevisionUx3I ||
            !string.Equals(GovernanceStateFingerprintUx3I(_governanceHistoryUx3I.State),
                _approvedGovernanceSourceUx3I, StringComparison.Ordinal) ||
            !string.Equals(GovernanceDraftFingerprintUx3I(approved), _approvedGovernanceDraftUx3I,
                StringComparison.Ordinal) ||
            !string.Equals(GovernanceDraftFingerprintUx3I(current), _approvedGovernanceDraftUx3I,
                StringComparison.Ordinal))
        {
            CancelGovernanceApprovalUx3I();
            return new(false, "Le dossier ou le registre a changé ; relancez l’aperçu sec.");
        }

        var scanner = CreateScannerUx3I(out var scannerError);
        if (scanner is null)
        {
            CancelGovernanceApprovalUx3I();
            return new(false, scannerError);
        }
        var request = CreateStageRequestUx3I(approved);
        var stage = AssetImportQuarantine.Stage(_governanceWorkspaceRootUx3I!, request, scanner);
        if (!stage.Succeeded || stage.QuarantineRelativePath is null)
        {
            CancelGovernanceApprovalUx3I();
            return new(false, HumanUx3I(stage.Diagnostics.FirstOrDefault()?.Message ?? "La quarantaine a refusé la source."));
        }

        AssetGovernanceRecord candidate;
        try
        {
            var decision = AssetImportQuarantine.Decide(stage.Review, approved.ApproveImport,
                approved.Reviewer, DateTimeOffset.UtcNow);
            candidate = BuildGovernanceRecordUx3I(approved, decision);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            DeleteQuarantineCopyUx3I(_governanceWorkspaceRootUx3I!, stage.QuarantineRelativePath);
            CancelGovernanceApprovalUx3I();
            return new(false, exception.Message);
        }

        var command = new SetAssetGovernanceCommand(CommandId.From(Guid.NewGuid()),
            _governanceHistoryUx3I.State.Revision, candidate, DryRun: true);
        var dryRun = _setGovernanceUx3I.Handle(_governanceHistoryUx3I.State, command);
        if (!dryRun.Succeeded)
        {
            DeleteQuarantineCopyUx3I(_governanceWorkspaceRootUx3I!, stage.QuarantineRelativePath);
            CancelGovernanceApprovalUx3I();
            return new(false, HumanUx3I(dryRun.Diagnostics.FirstOrDefault()?.Message ?? "Le re-dry-run refuse le dossier."));
        }
        var applied = _setGovernanceUx3I.Handle(_governanceHistoryUx3I.State, command with { DryRun = false });
        if (!applied.Succeeded)
        {
            DeleteQuarantineCopyUx3I(_governanceWorkspaceRootUx3I!, stage.QuarantineRelativePath);
            CancelGovernanceApprovalUx3I();
            return new(false, HumanUx3I(applied.Diagnostics.FirstOrDefault()?.Message ?? "Le dossier est refusé."));
        }
        var saved = AssetGovernanceStore.Save(_governanceWorkspaceRootUx3I!, applied.State.Records);
        if (!saved.Succeeded)
        {
            DeleteQuarantineCopyUx3I(_governanceWorkspaceRootUx3I!, stage.QuarantineRelativePath);
            CancelGovernanceApprovalUx3I();
            return new(false, saved.Diagnostics.FirstOrDefault()?.Message ?? "Le registre ne peut pas être écrit.");
        }
        _governanceHistoryUx3I.Record(applied);
        if (!approved.ApproveImport)
            DeleteQuarantineCopyUx3I(_governanceWorkspaceRootUx3I!, stage.QuarantineRelativePath);
        _quarantineReceiptUx3I = new(request, _governanceWorkspaceRootUx3I!,
            approved.ApproveImport ? stage.QuarantineRelativePath : null, approved.ApproveImport, false);
        _governanceCanonicalCommandUx3I = applied.Audit.Applied &&
                                          applied.Audit.CommandName == "set_asset_governance";
        CancelGovernanceApprovalUx3I();
        return new(true, approved.ApproveImport
            ? "Dossier enregistré ; la copie reste confinée en quarantaine après approbation humaine."
            : "Refus enregistré ; la copie de quarantaine a été supprimée.");
    }

    internal StudioUx3IActionResult UndoGovernanceUx3I()
    {
        if (!EnsureGovernanceStateUx3I(out var error)) return new(false, error);
        var receipt = _quarantineReceiptUx3I;
        if (receipt is null || receipt.Undone || !_governanceHistoryUx3I!.Undo())
            return new(false, "Aucune transaction de quarantaine à annuler.");
        var saved = AssetGovernanceStore.Save(_governanceWorkspaceRootUx3I!, _governanceHistoryUx3I.State.Records);
        if (!saved.Succeeded)
        {
            _governanceHistoryUx3I.Redo();
            return new(false, saved.Diagnostics.FirstOrDefault()?.Message ?? "Annulation impossible.");
        }
        if (receipt.Approved && receipt.QuarantineRelativePath is not null)
            DeleteQuarantineCopyUx3I(receipt.WorkspaceRoot, receipt.QuarantineRelativePath);
        _quarantineReceiptUx3I = receipt with { Undone = true, QuarantineRelativePath = null };
        CancelGovernanceApprovalUx3I();
        return new(true, "Transaction annulée ; registre et copie confinée sont revenus à l’état précédent.");
    }

    internal StudioUx3IActionResult RedoGovernanceUx3I()
    {
        if (!EnsureGovernanceStateUx3I(out var error)) return new(false, error);
        var receipt = _quarantineReceiptUx3I;
        if (receipt is null || !receipt.Undone) return new(false, "Aucune transaction à rétablir.");
        string? stagedPath = null;
        if (receipt.Approved)
        {
            var scanner = CreateScannerUx3I(out var scannerError);
            if (scanner is null) return new(false, scannerError);
            var stage = AssetImportQuarantine.Stage(receipt.WorkspaceRoot, receipt.Request, scanner);
            if (!stage.Succeeded || stage.QuarantineRelativePath is null)
                return new(false, HumanUx3I(stage.Diagnostics.FirstOrDefault()?.Message ?? "La source ne peut plus être remise en quarantaine."));
            stagedPath = stage.QuarantineRelativePath;
        }
        if (!_governanceHistoryUx3I!.Redo())
        {
            if (stagedPath is not null) DeleteQuarantineCopyUx3I(receipt.WorkspaceRoot, stagedPath);
            return new(false, "L’historique ne peut pas être rétabli.");
        }
        var saved = AssetGovernanceStore.Save(_governanceWorkspaceRootUx3I!, _governanceHistoryUx3I.State.Records);
        if (!saved.Succeeded)
        {
            _governanceHistoryUx3I.Undo();
            if (stagedPath is not null) DeleteQuarantineCopyUx3I(receipt.WorkspaceRoot, stagedPath);
            return new(false, saved.Diagnostics.FirstOrDefault()?.Message ?? "Rétablissement impossible.");
        }
        _quarantineReceiptUx3I = receipt with { Undone = false, QuarantineRelativePath = stagedPath };
        return new(true, "Transaction rétablie ; registre et quarantaine concordent.");
    }

    internal StudioUx3IActionResult SaveGovernanceUx3I()
    {
        if (!EnsureGovernanceStateUx3I(out var error)) return new(false, error);
        var saved = AssetGovernanceStore.Save(_governanceWorkspaceRootUx3I!, _governanceHistoryUx3I!.State.Records);
        return new(saved.Succeeded, saved.Succeeded ? "Registre de gouvernance enregistré." :
            saved.Diagnostics.FirstOrDefault()?.Message ?? "Enregistrement impossible.");
    }

    internal StudioUx3IMediaDraft? CreateMediaDraftUx3I(bool explicitlySelected)
    {
        var project = Project;
        var history = History;
        if (!explicitlySelected || project is null || history is null) return null;
        var extensions = project.Module.RuntimePolicies.Extensions;
        var media = extensions.PostLaunch.Production.MediaGeneration;
        return new(history.State.Revision, ProjectFingerprintUx3I(project), extensions,
            media.Enabled, media.MediaKinds.ToArray(), media.Providers.ToArray(), media.MaximumJobs,
            media.MaximumBytesPerJob, media.ModuleIndependentFallback,
            media.MediaKinds.FirstOrDefault() ?? "image", media.Providers.FirstOrDefault() ?? "local",
            "CC0-1.0", "Auteur confirmé · droits de génération et redistribution vérifiés",
            Math.Min(media.MaximumBytesPerJob, 1024 * 1024), media.Providers.Count > 0, Approved: false);
    }

    internal StudioUx3IMediaPreview PreviewMediaUx3I(StudioUx3IMediaDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelMediaApprovalUx3I();
        var project = Project;
        var history = History;
        if (project is null || history is null)
            return FailedMediaUx3I("Ouvrez un projet et choisissez sa politique média.",
                "post.media.selection_required", "module/runtimePolicies/extensions/postLaunch/production/mediaGeneration");
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(ProjectFingerprintUx3I(project), draft.SourceFingerprint, StringComparison.Ordinal))
            return FailedMediaUx3I("Le projet a changé ; rechargez la politique média.",
                "command.revision_conflict", "module/runtimePolicies/extensions/postLaunch/production/mediaGeneration");

        ModuleRuntimeExtensionProfile candidate;
        MediaGenerationPlan plan;
        try
        {
            candidate = BuildMediaCandidateUx3I(draft);
            plan = PostLaunchRuntimeService.PlanMediaGeneration(candidate.PostLaunch, Guid.NewGuid(),
                draft.JobKind, draft.JobProvider, draft.JobLicenseSpdx, draft.JobRightsProvenance,
                draft.JobExpectedBytes, draft.JobProviderAvailable);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException)
        {
            return FailedMediaUx3I(exception.Message, "post.media.invalid",
                "module/runtimePolicies/extensions/postLaunch/production/mediaGeneration");
        }
        var command = new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            candidate, DryRun: true);
        var result = _setRuntimeExtensionsUx3I.Handle(history.State, command);
        var diagnostics = result.Diagnostics.Select(item => new StudioUx3IDiagnostic(item.Code,
            "module/runtimePolicies/extensions/postLaunch/production/mediaGeneration", HumanUx3I(item.Message))).ToList();
        if (!plan.Succeeded)
            diagnostics.Add(new(plan.Code, "media/job", HumanMediaPlanFailureUx3I(plan.Code)));
        var dryRun = result.Succeeded && result.Patch is not null && result.Inverse is not null && !result.Audit.Applied;
        var succeeded = dryRun && plan.Succeeded && diagnostics.Count == 0;
        var frozen = FreezeMediaDraftUx3I(draft);
        if (succeeded && draft.Approved)
        {
            _approvedMediaUx3I = frozen;
            _approvedMediaRevisionUx3I = draft.BaseRevision;
            _approvedMediaSourceUx3I = draft.SourceFingerprint;
            _approvedMediaDraftUx3I = MediaDraftFingerprintUx3I(frozen);
        }
        var source = project.Module.RuntimePolicies.Extensions.PostLaunch.Production.MediaGeneration;
        var target = candidate.PostLaunch.Production.MediaGeneration;
        var diff = new[]
        {
            $"Génération gouvernée : {(source.Enabled ? "active" : "inactive")} → {(target.Enabled ? "active" : "inactive")}",
            $"Formats : {HumanListUx3I(source.MediaKinds)} → {HumanListUx3I(target.MediaKinds)}",
            $"Fournisseurs autorisés : {HumanListUx3I(source.Providers)} → {HumanListUx3I(target.Providers)}",
            $"Quota : {source.MaximumJobs} tâche(s) / {source.MaximumBytesPerJob} octets → " +
            $"{target.MaximumJobs} tâche(s) / {target.MaximumBytesPerJob} octets",
        };
        var plans = new[]
        {
            $"Plan : {HumanMediaCodeUx3I(plan.Code)}",
            $"Candidat confiné : {HumanUx3I(plan.StagingPath)}",
            $"Licence : {HumanUx3I(plan.LicenseSpdx)} · revue humaine {(plan.RequiresReview ? "obligatoire" : "non demandée")}",
            plan.UsesModuleIndependentFallback
                ? "Fallback manuel indépendant du module ; aucun fournisseur distant n’est appelé."
                : $"Fournisseur allowlisté : {HumanUx3I(draft.JobProvider)} · aucun appel exécuté pendant l’aperçu.",
        };
        return new(succeeded, succeeded && draft.Approved, dryRun, plan.Succeeded, true,
            succeeded ? "Politique et plan média validés sans appeler de fournisseur." :
                diagnostics.FirstOrDefault()?.Message ?? "La politique média doit être corrigée.",
            HumanUx3I(result.Patch?.Summary ?? "Configurer la politique média."),
            HumanUx3I(result.Inverse?.Summary ?? "Restaurer la politique précédente."),
            diff, diagnostics, plans);
    }

    internal StudioUx3IActionResult ApplyApprovedMediaUx3I(StudioUx3IMediaDraft current)
    {
        var project = Project;
        var history = History;
        var approved = _approvedMediaUx3I;
        if (project is null || history is null || approved is null || _approvedMediaRevisionUx3I is null ||
            _approvedMediaSourceUx3I is null || _approvedMediaDraftUx3I is null ||
            history.State.Revision != _approvedMediaRevisionUx3I ||
            !string.Equals(ProjectFingerprintUx3I(project), _approvedMediaSourceUx3I, StringComparison.Ordinal) ||
            !string.Equals(MediaDraftFingerprintUx3I(approved), _approvedMediaDraftUx3I, StringComparison.Ordinal) ||
            !string.Equals(MediaDraftFingerprintUx3I(current), _approvedMediaDraftUx3I, StringComparison.Ordinal))
        {
            CancelMediaApprovalUx3I();
            return new(false, "La politique ou le projet a changé ; relancez l’aperçu sec.");
        }
        ModuleRuntimeExtensionProfile candidate;
        try { candidate = BuildMediaCandidateUx3I(approved); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            CancelMediaApprovalUx3I();
            return new(false, exception.Message);
        }
        var command = new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            candidate, DryRun: true);
        var dryRun = _setRuntimeExtensionsUx3I.Handle(history.State, command);
        if (!dryRun.Succeeded)
        {
            CancelMediaApprovalUx3I();
            return new(false, HumanUx3I(dryRun.Diagnostics.FirstOrDefault()?.Message ?? "Le re-dry-run refuse la politique."));
        }
        var applied = _setRuntimeExtensionsUx3I.Handle(history.State, command with { DryRun = false });
        CancelMediaApprovalUx3I();
        if (!applied.Succeeded)
            return new(false, HumanUx3I(applied.Diagnostics.FirstOrDefault()?.Message ?? "La politique est refusée."));
        history.Record(applied);
        _mediaCanonicalCommandUx3I = applied.Audit.Applied && applied.Audit.CommandName == "set_runtime_extensions";
        InvalidateContentIndexes();
        return new(true, "Politique média appliquée ; aucun média n’a été généré ni importé.");
    }

    internal StudioUx3IActionResult UndoMediaUx3I()
    {
        CancelMediaApprovalUx3I();
        var result = Undo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx3IActionResult RedoMediaUx3I()
    {
        CancelMediaApprovalUx3I();
        var result = Redo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx3IActionResult SaveMediaUx3I()
    {
        CancelMediaApprovalUx3I();
        var result = Save();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx3IGovernanceApprovalSnapshot CaptureGovernanceApprovalUx3I() =>
        new(_approvedGovernanceUx3I, _approvedGovernanceRevisionUx3I,
            _approvedGovernanceSourceUx3I, _approvedGovernanceDraftUx3I);

    internal void RestoreGovernanceApprovalUx3I(StudioUx3IGovernanceApprovalSnapshot snapshot)
    {
        _approvedGovernanceUx3I = snapshot.Draft;
        _approvedGovernanceRevisionUx3I = snapshot.Revision;
        _approvedGovernanceSourceUx3I = snapshot.SourceFingerprint;
        _approvedGovernanceDraftUx3I = snapshot.DraftFingerprint;
    }

    internal StudioUx3IMediaApprovalSnapshot CaptureMediaApprovalUx3I() =>
        new(_approvedMediaUx3I, _approvedMediaRevisionUx3I, _approvedMediaSourceUx3I, _approvedMediaDraftUx3I);

    internal void RestoreMediaApprovalUx3I(StudioUx3IMediaApprovalSnapshot snapshot)
    {
        _approvedMediaUx3I = snapshot.Draft;
        _approvedMediaRevisionUx3I = snapshot.Revision;
        _approvedMediaSourceUx3I = snapshot.SourceFingerprint;
        _approvedMediaDraftUx3I = snapshot.DraftFingerprint;
    }

    internal void CloseUx3I()
    {
        CancelGovernanceApprovalUx3I();
        CancelMediaApprovalUx3I();
        _governanceHistoryUx3I = null;
        _governanceWorkspaceRootUx3I = null;
        _governanceAssetsUx3I = [];
        _useEvidenceScannerUx3I = false;
        _quarantineReceiptUx3I = null;
        _governanceCanonicalCommandUx3I = false;
        _mediaCanonicalCommandUx3I = false;
    }

    internal StudioUx3IEvidencePlan? CreateEvidencePlanUx3I()
    {
        if (Project is null || Session is null) return null;
        var choices = ListGovernanceAssetsUx3I();
        var real = choices.OrderBy(item => item.Bytes).FirstOrDefault(item => item.Bytes is > 0 and <= 32 * 1024 * 1024);
        var fallback = real is null ? CreateCanonicalAssetChoiceUx3I(Session.RootDirectory) : real;
        return new(Project, fallback, CreateNeighborGovernanceUx3I(),
            UsesRealModule: real is not null, UsesCanonicalFallback: real is null);
    }

    internal static bool MaterializeEvidenceUx3I(string root, StudioUx3IEvidencePlan plan,
        out StudioUx3IAssetChoice isolatedChoice)
    {
        isolatedChoice = plan.Asset;
        try
        {
            if (!ProjectSession.Create(root, plan.Project).Succeeded) return false;
            var extension = Path.GetExtension(plan.Asset.SourceRelativePath);
            if (string.IsNullOrWhiteSpace(extension)) extension = ".png";
            var relative = $"incoming/ux3-i/governed-asset{extension.ToLowerInvariant()}";
            var target = ResolveContainedUx3I(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var source = ResolveContainedUx3I(plan.Asset.SourceRoot, plan.Asset.SourceRelativePath);
            if (plan.UsesRealModule && File.Exists(source)) File.Copy(source, target, overwrite: true);
            else File.WriteAllBytes(target, CanonicalPngUx3I());
            var hash = HashFileUx3I(target);
            var media = DetectMediaTypeUx3I(target);
            isolatedChoice = plan.Asset with
            {
                SourceRoot = root,
                SourceRelativePath = relative,
                MediaType = media,
                ContentSha256 = hash,
                Bytes = new FileInfo(target).Length,
            };
            return AssetGovernanceStore.Save(root, [plan.Neighbor]).Succeeded;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    internal void UseEvidenceContextUx3I(StudioUx3IAssetChoice choice)
    {
        if (Session is null) throw new InvalidOperationException("Aucun workspace de preuve ouvert.");
        var loaded = AssetGovernanceStore.Load(Session.RootDirectory);
        if (!loaded.Succeeded) throw new InvalidOperationException("Le registre de preuve est illisible.");
        _governanceWorkspaceRootUx3I = Session.RootDirectory;
        _governanceHistoryUx3I = new AssetGovernanceCommandHistory(loaded.Records);
        _governanceAssetsUx3I = [choice];
        _useEvidenceScannerUx3I = true;
        _quarantineReceiptUx3I = null;
        CancelGovernanceApprovalUx3I();
    }

    internal StudioUx3ISmokeResult RunUx3IContractSmoke()
    {
        var evidence = CreateEvidencePlanUx3I();
        if (evidence is null) return EmptySmokeUx3I();
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux3-i-contract-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        _lastUx3ITemporaryReleased = false;
        try
        {
            if (!MaterializeEvidenceUx3I(root, evidence, out var choice) || !editing.Open(root).Succeeded)
                return EmptySmokeUx3I();
            editing.UseEvidenceContextUx3I(choice);
            var neighborBefore = GovernanceRecordFingerprintUx3I(
                editing._governanceHistoryUx3I!.State.Records.Single(item => item.AssetId == evidence.Neighbor.AssetId));
            var draft = editing.CreateGovernanceDraftUx3I(choice.AssetId);
            if (draft is null) return EmptySmokeUx3I();
            var invalid = !editing.PreviewGovernanceUx3I(draft with
            {
                PermissionEvidence = string.Empty,
                Approved = true,
            }).Succeeded;
            draft = draft with { Approved = true, Reviewer = "Relectrice UX3-I" };
            var preview = editing.PreviewGovernanceUx3I(draft);
            var staleSource = !editing.PreviewGovernanceUx3I(draft with
            {
                SourceFingerprint = draft.SourceFingerprint + "stale",
            }).Succeeded;
            draft = editing.CreateGovernanceDraftUx3I(choice.AssetId)! with
            {
                Approved = true,
                Reviewer = "Relectrice UX3-I",
            };
            preview = editing.PreviewGovernanceUx3I(draft);
            var staleApproval = preview.CanApply && !editing.ApplyApprovedGovernanceUx3I(draft with
            {
                Modifications = draft.Modifications + " modifié après approbation",
            }).Succeeded;
            preview = editing.PreviewGovernanceUx3I(draft);
            var applied = preview.CanApply && editing.ApplyApprovedGovernanceUx3I(draft).Succeeded;
            var undo = editing.UndoGovernanceUx3I().Succeeded;
            var redo = editing.RedoGovernanceUx3I().Succeeded;
            var governanceSaved = editing.SaveGovernanceUx3I().Succeeded;

            var projectBeforeMedia = editing.Project!;
            var media = editing.CreateMediaDraftUx3I(explicitlySelected: true);
            if (media is null) return EmptySmokeUx3I();
            media = media with
            {
                Enabled = true,
                MediaKinds = ["image", "audio", "vfx"],
                Providers = ["studio-evidence"],
                JobKind = "image",
                JobProvider = "studio-evidence",
                JobLicenseSpdx = "CC0-1.0",
                JobRightsProvenance = "Auteur confirmé · preuve UX3-I isolée",
                JobExpectedBytes = 4096,
                JobProviderAvailable = true,
                Approved = true,
            };
            var mediaPreview = editing.PreviewMediaUx3I(media);
            var expectedMediaProfile = BuildMediaCandidateUx3I(media);
            var expectedMediaResult = editing._setRuntimeExtensionsUx3I.Handle(
                new ProjectState(projectBeforeMedia, new ProjectRevision(0)),
                new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()), new ProjectRevision(0),
                    expectedMediaProfile, DryRun: false));
            var mediaApplied = mediaPreview.CanApply && editing.ApplyApprovedMediaUx3I(media).Succeeded;
            var mediaUndo = editing.UndoMediaUx3I().Succeeded;
            var mediaRedo = editing.RedoMediaUx3I().Succeeded;
            var mediaSaved = editing.SaveMediaUx3I().Succeeded;

            var reopened = new StudioSessionController();
            var reopenedReady = reopened.Open(root).Succeeded;
            var loaded = AssetGovernanceStore.Load(root);
            var persistence = governanceSaved && mediaSaved && reopenedReady && loaded.Succeeded &&
                              loaded.Records.Count == editing._governanceHistoryUx3I.State.Records.Count &&
                              ProjectJsonSerializer.Serialize(reopened.Project!) ==
                              ProjectJsonSerializer.Serialize(editing.Project!);
            reopened.Close();
            var neighborPreserved = GovernanceRecordFingerprintUx3I(
                editing._governanceHistoryUx3I.State.Records.Single(item => item.AssetId == evidence.Neighbor.AssetId)) ==
                neighborBefore;
            var projectPreserved = expectedMediaResult.Succeeded &&
                                   ProjectJsonSerializer.Serialize(expectedMediaResult.State.Project) ==
                                   ProjectJsonSerializer.Serialize(editing.Project!);
            var human = !choice.Label.Contains(choice.AssetId.ToString(), StringComparison.OrdinalIgnoreCase) &&
                        !Regex.IsMatch(choice.Label, GuidPatternUx3I(), RegexOptions.CultureInvariant);
            var succeeded = invalid && preview.DryRunSucceeded && preview.QuarantineDryRunSucceeded &&
                            preview.DistributionReady && staleSource && staleApproval && applied && undo && redo &&
                            mediaPreview.DryRunSucceeded && mediaPreview.JobPlanSucceeded && mediaApplied &&
                            mediaUndo && mediaRedo && persistence && neighborPreserved && projectPreserved && human &&
                            preview.RuntimeClaimsBounded && mediaPreview.RuntimeClaimsBounded &&
                            editing._governanceCanonicalCommandUx3I && editing._mediaCanonicalCommandUx3I;
            return new(succeeded, true, invalid && preview.DistributionReady,
                preview.QuarantineDryRunSucceeded, applied, mediaApplied,
                mediaPreview.JobPlanSucceeded, editing._governanceCanonicalCommandUx3I,
                editing._mediaCanonicalCommandUx3I, staleSource, staleApproval, undo && redo && mediaUndo && mediaRedo,
                persistence, neighborPreserved, projectPreserved, human,
                preview.RuntimeClaimsBounded && mediaPreview.RuntimeClaimsBounded, false);
        }
        finally
        {
            editing.Close();
            _lastUx3ITemporaryReleased = DeleteTemporaryBlueprintUx3A(root);
        }
    }

    internal string GovernanceDraftFingerprintUx3I(StudioUx3IGovernanceDraft draft) => HashTextUx3I(
        JsonSerializer.Serialize(new
        {
            revision = draft.BaseRevision.Value,
            draft.SourceFingerprint,
            asset = draft.Asset.AssetId.ToString(),
            draft.LicenseSpdx,
            draft.Author,
            draft.SourceUrl,
            draft.PermissionEvidence,
            draft.Modifications,
            draft.AiGenerated,
            draft.AiModel,
            draft.AiPromptPolicySha256,
            draft.PlatformBudgetBytes,
            origin = (int)draft.Origin,
            draft.RedistributionAllowed,
            draft.CompatibilityIsolated,
            draft.LegalReviewReference,
            draft.ApproveImport,
            draft.Reviewer,
            draft.Approved,
        }));

    internal string MediaDraftFingerprintUx3I(StudioUx3IMediaDraft draft) => HashTextUx3I(
        JsonSerializer.Serialize(new
        {
            revision = draft.BaseRevision.Value,
            draft.SourceFingerprint,
            draft.Enabled,
            kinds = draft.MediaKinds.Order(StringComparer.Ordinal).ToArray(),
            providers = draft.Providers.Order(StringComparer.Ordinal).ToArray(),
            draft.MaximumJobs,
            draft.MaximumBytesPerJob,
            draft.ModuleIndependentFallback,
            draft.JobKind,
            draft.JobProvider,
            draft.JobLicenseSpdx,
            draft.JobRightsProvenance,
            draft.JobExpectedBytes,
            draft.JobProviderAvailable,
            draft.Approved,
        }));

    private bool EnsureGovernanceStateUx3I(out string error)
    {
        error = string.Empty;
        if (Session is null || Project is null)
        {
            error = "Aucun workspace Studio n’est ouvert.";
            return false;
        }
        if (_governanceWorkspaceRootUx3I == Session.RootDirectory && _governanceHistoryUx3I is not null)
            return true;
        var loaded = AssetGovernanceStore.Load(Session.RootDirectory);
        if (!loaded.Succeeded)
        {
            error = loaded.Diagnostics.FirstOrDefault()?.Message ?? "Le registre de gouvernance est illisible.";
            return false;
        }
        _governanceWorkspaceRootUx3I = Session.RootDirectory;
        _governanceHistoryUx3I = new AssetGovernanceCommandHistory(loaded.Records);
        _governanceAssetsUx3I = LoadAssetChoicesUx3I();
        _useEvidenceScannerUx3I = false;
        _quarantineReceiptUx3I = null;
        CancelGovernanceApprovalUx3I();
        return true;
    }

    private IReadOnlyList<StudioUx3IAssetChoice> LoadAssetChoicesUx3I()
    {
        if (!EnsureAssetAuthoringStateUx3F(out _)) return [];
        var catalog = _catalogLoadUx3F?.Catalog;
        var vault = _catalogLoadUx3F?.VaultRoot;
        if (catalog is null || string.IsNullOrWhiteSpace(vault)) return [];
        var choices = new List<StudioUx3IAssetChoice>();
        foreach (var asset in catalog.Items)
        {
            var relative = SupportedSourceUx3I(asset.RelativeModelPath)
                ? asset.RelativeModelPath
                : SupportedSourceUx3I(asset.RelativePreviewPath) ? asset.RelativePreviewPath : null;
            if (string.IsNullOrWhiteSpace(relative)) continue;
            string full;
            try { full = ResolveContainedUx3I(vault, relative); }
            catch (ArgumentException) { continue; }
            if (!File.Exists(full) || File.GetAttributes(full).HasFlag(FileAttributes.ReparsePoint)) continue;
            var info = new FileInfo(full);
            if (info.Length <= 0 || info.Length > 32L * 1024 * 1024) continue;
            choices.Add(new(asset.Id,
                $"{HumanUx3I(asset.DisplayName)} · {HumanMediaTypeUx3I(DetectMediaTypeUx3I(full))}",
                vault, relative, DetectMediaTypeUx3I(full), asset.ContentSha256, info.Length,
                asset.LicenseSpdx, asset.Author, asset.SourcePage,
                $"catalogue vérifié · {HumanUx3I(asset.Provenance)}", asset.Provenance,
                UsesRealModuleSource: true));
        }
        return choices.OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private AssetQuarantineStageResult StageQuarantineDryRunUx3I(StudioUx3IGovernanceDraft draft,
        out AssetQuarantineReview? review, out string scannerLabel)
    {
        review = null;
        var scanner = CreateScannerUx3I(out scannerLabel);
        if (scanner is null)
        {
            return new(false, null, null,
                new(draft.Asset.SourceRelativePath, draft.Asset.MediaType, draft.Asset.MediaType,
                    draft.Asset.Bytes, Math.Max(1, draft.PlatformBudgetBytes), "indisponible", "not-run", "not-run",
                    AssetQuarantineStatus.Pending, string.Empty, null),
                [new(AssetGovernanceValidator.QuarantineScanFailedCode, scannerLabel,
                    $"assets/quarantine/{draft.Asset.Label}/scanner")]);
        }
        var temp = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux3-i-preview-{Guid.NewGuid():N}");
        try
        {
            var result = AssetImportQuarantine.Stage(temp, CreateStageRequestUx3I(draft), scanner);
            review = result.Succeeded ? result.Review : null;
            scannerLabel = scanner.AdapterName;
            return result;
        }
        finally
        {
            DeleteTemporaryBlueprintUx3A(temp);
        }
    }

    private IAssetMalwareScanner? CreateScannerUx3I(out string message)
    {
        if (_useEvidenceScannerUx3I)
        {
            var scanner = new StudioEvidenceScannerUx3I();
            message = scanner.AdapterName + " · adaptateur déterministe réservé à la preuve isolée";
            return scanner;
        }
        if (StudioWindowsDefenderScannerUx3I.TryCreate(out var defender))
        {
            message = defender.AdapterName;
            return defender;
        }
        message = "Aucun adaptateur antivirus système disponible ; la quarantaine reste bloquée sans scan réel.";
        return null;
    }

    private static AssetQuarantineStageRequest CreateStageRequestUx3I(StudioUx3IGovernanceDraft draft) => new(
        draft.Asset.AssetId, draft.Asset.SourceRoot, draft.Asset.SourceRelativePath, draft.Asset.MediaType,
        Math.Max(draft.Asset.Bytes, draft.PlatformBudgetBytes), draft.Asset.ContentSha256);

    private static AssetGovernanceRecord BuildGovernanceRecordUx3I(StudioUx3IGovernanceDraft draft,
        AssetQuarantineReview review) => new(
        AssetGovernanceRecord.CurrentSchemaVersion,
        AssetGovernanceId.ForAsset(draft.Asset.AssetId),
        draft.Asset.AssetId,
        AssetPackageManifestId.ForAsset(draft.Asset.AssetId),
        draft.LicenseSpdx.Trim(),
        draft.Asset.ContentSha256,
        draft.Author.Trim(),
        draft.SourceUrl.Trim(),
        draft.PermissionEvidence.Trim(),
        draft.Modifications.Trim(),
        draft.AiGenerated,
        draft.AiGenerated ? draft.AiModel.Trim() : string.Empty,
        draft.AiGenerated ? draft.AiPromptPolicySha256.Trim().ToLowerInvariant() : string.Empty,
        draft.PlatformBudgetBytes,
        review,
        new(draft.Origin, draft.RedistributionAllowed, draft.CompatibilityIsolated,
            draft.LegalReviewReference.Trim()));

    private static ModuleRuntimeExtensionProfile BuildMediaCandidateUx3I(StudioUx3IMediaDraft draft)
    {
        var source = draft.SourceExtensions;
        var post = source.PostLaunch;
        var production = post.Production;
        var media = production.MediaGeneration with
        {
            Enabled = draft.Enabled,
            MediaKinds = draft.MediaKinds,
            Providers = draft.Providers,
            RequireRightsMetadata = true,
            RequireHumanReview = true,
            StageBeforeImport = true,
            MaximumJobs = draft.MaximumJobs,
            MaximumBytesPerJob = draft.MaximumBytesPerJob,
            ModuleIndependentFallback = draft.ModuleIndependentFallback,
        };
        var updatedProduction = new ProductionAutomationProfile(production.Id, production.SchemaVersion,
            media, production.SoakTests, "studio:ux3-i:media-governance");
        var updatedPost = new PostLaunchCapabilityProfile(post.Id, post.SchemaVersion, post.Gameplay,
            post.LiveDirector, post.Network, post.CreatorExperience, updatedProduction,
            "studio:ux3-i:media-governance");
        return new ModuleRuntimeExtensionProfile(source.Id, source.SchemaVersion, source.RuleSchemas,
            source.UiDocuments, source.JsonDocuments, source.SnapshotPolicy, source.Provenance,
            source.ModernTooling, source.OperationalReadiness, updatedPost);
    }

    private void CancelGovernanceApprovalUx3I()
    {
        _approvedGovernanceUx3I = null;
        _approvedGovernanceRevisionUx3I = null;
        _approvedGovernanceSourceUx3I = null;
        _approvedGovernanceDraftUx3I = null;
    }

    private void CancelMediaApprovalUx3I()
    {
        _approvedMediaUx3I = null;
        _approvedMediaRevisionUx3I = null;
        _approvedMediaSourceUx3I = null;
        _approvedMediaDraftUx3I = null;
    }

    private static StudioUx3IGovernanceDraft FreezeGovernanceDraftUx3I(StudioUx3IGovernanceDraft draft) =>
        draft with { Asset = draft.Asset with { } };

    private static StudioUx3IMediaDraft FreezeMediaDraftUx3I(StudioUx3IMediaDraft draft) => draft with
    {
        MediaKinds = draft.MediaKinds.ToArray(),
        Providers = draft.Providers.ToArray(),
    };

    private static string GovernanceStateFingerprintUx3I(AssetGovernanceState state) => HashTextUx3I(
        JsonSerializer.Serialize(state.Records));

    private static string GovernanceRecordFingerprintUx3I(AssetGovernanceRecord record) =>
        HashTextUx3I(JsonSerializer.Serialize(record));

    private static string ProjectFingerprintUx3I(ProjectDefinition project) =>
        HashTextUx3I(ProjectJsonSerializer.Serialize(project));

    private static string HashTextUx3I(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string HashFileUx3I(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string ResolveContainedUx3I(string root, string relative)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!AssetGovernanceValidator.IsSafeRelativePath(relative))
            throw new ArgumentException("Le chemin de source doit rester relatif et confiné.", nameof(relative));
        var path = Path.GetFullPath(Path.Combine(fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = fullRoot + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Le chemin sort du dossier autorisé.", nameof(relative));
        return path;
    }

    private static void DeleteQuarantineCopyUx3I(string root, string relative)
    {
        try
        {
            var path = ResolveContainedUx3I(root, relative);
            if (File.Exists(path)) File.Delete(path);
            var directory = Path.GetDirectoryName(path);
            if (directory is not null && Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                Directory.Delete(directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // The caller still preserves the canonical governance state; cleanup is reported by evidence checks.
        }
    }

    private static bool SupportedSourceUx3I(string? path) => Path.GetExtension(path ?? string.Empty)
        .ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".glb" or ".gltf";

    private static string DetectMediaTypeUx3I(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".glb" => "model/gltf-binary",
        ".gltf" => "model/gltf+json",
        _ => "application/octet-stream",
    };

    private static StudioUx3IAssetChoice CreateCanonicalAssetChoiceUx3I(string sourceRoot)
    {
        var bytes = CanonicalPngUx3I();
        return new(AssetCatalogItemId.ForExternalId("studio:ux3-i:canonical-governed-image"),
            "Image gouvernée de preuve · fallback canonique isolé", sourceRoot,
            "incoming/ux3-i/canonical.png", "image/png",
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), bytes.Length,
            "CC0-1.0", "OpenRPG Forge Studio", "https://openrpg-forge.invalid/evidence/ux3-i",
            "fixture isolée créée par Studio", "studio:ux3-i:canonical-fallback", false);
    }

    private static byte[] CanonicalPngUx3I() =>
    [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 1, 0, 0, 0, 1];

    private static AssetGovernanceRecord CreateNeighborGovernanceUx3I()
    {
        var asset = AssetCatalogItemId.ForExternalId("studio:ux3-i:neighbor-governance");
        return new(1, AssetGovernanceId.ForAsset(asset), asset, AssetPackageManifestId.ForAsset(asset),
            "CC0-1.0", new string('c', 64), "Voisine protégée", "https://example.invalid/ux3-i/neighbor",
            "permission:fixture", "Aucune modification.", false, string.Empty, string.Empty, 4096,
            new("incoming/neighbor.png", "image/png", "image/png", 24, 4096, "fixture-scanner", "clean",
                "valid", AssetQuarantineStatus.Approved, "Relectrice", DateTimeOffset.UnixEpoch),
            new(AssetOriginKind.UserOwned, true, false, string.Empty));
    }

    private static StudioUx3IGovernancePreview FailedGovernanceUx3I(string message, string code, string path) =>
        new(false, false, false, false, false, true, HumanUx3I(message), "Aucun changement.",
            "Aucun changement.", [], [new(code, path, HumanUx3I(message))], "Scan non exécuté", "Aucun fichier écrit.");

    private static StudioUx3IMediaPreview FailedMediaUx3I(string message, string code, string path) =>
        new(false, false, false, false, true, HumanUx3I(message), "Aucun changement.", "Aucun changement.",
            [], [new(code, path, HumanUx3I(message))], ["Aucun fournisseur appelé."]);

    private static StudioUx3ISmokeResult EmptySmokeUx3I() =>
        new(false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false);

    private static string HumanUx3I(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "absent";
        var scrubbed = Regex.Replace(value, GuidPatternUx3I(), "identité protégée",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        return scrubbed.Replace('\\', '/').Trim();
    }

    private static string GuidPatternUx3I() =>
        "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}";

    private static string HumanQuarantineUx3I(AssetQuarantineStatus? status) => status switch
    {
        AssetQuarantineStatus.Pending => "en attente",
        AssetQuarantineStatus.Approved => "approuvée",
        AssetQuarantineStatus.Rejected => "refusée",
        _ => "absente",
    };

    private static string HumanMediaTypeUx3I(string media) => media switch
    {
        "image/png" => "image PNG",
        "image/jpeg" => "image JPEG",
        "model/gltf-binary" => "modèle GLB",
        "model/gltf+json" => "modèle glTF",
        _ => "format non pris en charge",
    };

    private static string HumanListUx3I(IReadOnlyList<string> values) =>
        values.Count == 0 ? "aucun" : string.Join(", ", values.Select(HumanUx3I));

    private static string HumanMediaCodeUx3I(string code) => code switch
    {
        "POST_MEDIA_PLANNED" => "candidat gouverné prêt à être remis au fournisseur après approbation",
        "POST_MEDIA_FALLBACK_PLANNED" => "fallback manuel gouverné prêt",
        _ => HumanUx3I(code),
    };

    private static string HumanMediaPlanFailureUx3I(string code) => code switch
    {
        "POST_MEDIA_KIND_DISABLED" => "Le type de média n’est pas activé dans la politique.",
        "POST_MEDIA_PROVIDER_NOT_ALLOWED" => "Le fournisseur n’est pas dans l’allowlist.",
        "POST_MEDIA_RIGHTS_REQUIRED" => "Licence et preuve de droits sont obligatoires.",
        "POST_MEDIA_SIZE_EXCEEDED" => "La taille attendue dépasse le quota par tâche.",
        "POST_MEDIA_PROVIDER_UNAVAILABLE" => "Le fournisseur est indisponible et le fallback est désactivé.",
        _ => "Le plan média a été refusé.",
    };
}

internal sealed class StudioEvidenceScannerUx3I : IAssetMalwareScanner
{
    public string AdapterName => "Studio UX3-I · scanner déterministe de preuve isolée";

    public AssetMalwareScanResult Scan(string confinedFilePath) =>
        new(File.Exists(confinedFilePath) &&
            !File.GetAttributes(confinedFilePath).HasFlag(FileAttributes.ReparsePoint), "clean");
}

internal sealed class StudioWindowsDefenderScannerUx3I : IAssetMalwareScanner
{
    private readonly string _executable;
    private StudioWindowsDefenderScannerUx3I(string executable) => _executable = executable;
    public string AdapterName => "Microsoft Defender · analyse locale sans remédiation";

    public static bool TryCreate(out StudioWindowsDefenderScannerUx3I scanner)
    {
        scanner = null!;
        if (!OperatingSystem.IsWindows()) return false;
        var candidates = new List<string>();
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var platform = Path.Combine(programData, "Microsoft", "Windows Defender", "Platform");
        if (Directory.Exists(platform))
            candidates.AddRange(Directory.EnumerateDirectories(platform)
                .OrderByDescending(item => item, StringComparer.OrdinalIgnoreCase)
                .Select(item => Path.Combine(item, "MpCmdRun.exe")));
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        candidates.Add(Path.Combine(programFiles, "Windows Defender", "MpCmdRun.exe"));
        var executable = candidates.FirstOrDefault(File.Exists);
        if (executable is null) return false;
        scanner = new(executable);
        return true;
    }

    public AssetMalwareScanResult Scan(string confinedFilePath)
    {
        try
        {
            var start = new ProcessStartInfo(_executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            start.ArgumentList.Add("-Scan");
            start.ArgumentList.Add("-ScanType");
            start.ArgumentList.Add("3");
            start.ArgumentList.Add("-File");
            start.ArgumentList.Add(Path.GetFullPath(confinedFilePath));
            start.ArgumentList.Add("-DisableRemediation");
            using var process = Process.Start(start);
            if (process is null || !process.WaitForExit(60_000))
            {
                try
                {
                    process?.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException exception)
                {
                    Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX3I_SCANNER_TERMINATION_FAILED " + exception.Message);
                }
                return new(false, "scanner-timeout");
            }
            return process.ExitCode == 0
                ? new(true, "clean")
                : new(false, $"defender-exit-{process.ExitCode}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new(false, "scanner-unavailable");
        }
    }
}
