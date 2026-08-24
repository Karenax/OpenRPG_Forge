using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Projects;
using Forge.Domain.Content;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Content;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private StudioUx8KBulkDraft? _approvedBulkUx8K;
    private string? _approvedBulkFingerprintUx8K;
    private bool _bulkCanonicalCommandUx8K;
    private bool _lastUx8KTemporaryReleased = true;

    internal bool LastUx8KTemporaryReleased => _lastUx8KTemporaryReleased;
    internal bool CanUndoBulkUx8K => CanUndoBatchUx3H && History?.CanUndo == true;
    internal bool CanRedoBulkUx8K => CanRedoBatchUx3H && History?.CanRedo == true;

    internal StudioUx8KBulkDraft? CreateBulkDraftUx8K()
    {
        var content = CreateContentDraftUx3H();
        if (content is null) return null;
        return new(
            content.BaseRevision,
            content.SourceProjectFingerprint,
            ContentBatchOperation.Import,
            "evidence-import",
            "content/imported",
            content.BatchGlob,
            content.BatchExtensions.ToArray(),
            content.BatchCollisionPolicy,
            content.BatchProvenance,
            content.BatchLicenseSpdx,
            Consent: false);
    }

    internal StudioUx8KBulkPreview PreviewBulkTransferUx8K(StudioUx8KBulkDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelBulkApprovalUx8K();
        var root = Session?.RootDirectory;
        var current = CreateContentDraftUx3H();
        if (current is null || string.IsNullOrWhiteSpace(root))
            return FailedBulkUx8K("Ouvrez un module avant de préparer un transfert.");
        if (!draft.Consent)
            return FailedBulkUx8K("Confirmez explicitement le transfert de fichiers avant l’aperçu.");
        if (current.BaseRevision != draft.BaseRevision ||
            !string.Equals(current.SourceProjectFingerprint, draft.SourceProjectFingerprint, StringComparison.Ordinal))
            return FailedBulkUx8K("Le projet a changé ; rechargez le formulaire avant l’aperçu.");

        try
        {
            var normalized = NormalizeBulkDraftUx8K(draft);
            var source = ResolveConfinedBulkPathUx8K(root, normalized.SourceRelativePath);
            var destination = ResolveConfinedBulkPathUx8K(root, normalized.DestinationRelativePath);
            if (!RootsAreDisjointUx8K(source, destination))
                return FailedBulkUx8K("La source et la destination doivent être deux dossiers distincts et non imbriqués.");

            var contentDraft = BuildContentDraftUx8K(current, normalized);
            var configuration = PreviewContentUx3H(contentDraft);
            var transfer = PreviewBatchUx3H(
                contentDraft,
                source,
                normalized.DestinationRelativePath,
                normalized.Operation);
            var succeeded = configuration.Succeeded && configuration.DryRunSucceeded && transfer.Succeeded;
            if (succeeded)
            {
                _approvedBulkUx8K = normalized;
                _approvedBulkFingerprintUx8K = BulkFingerprintUx8K(normalized);
            }
            else
            {
                CancelBulkApprovalUx8K();
            }

            var diagnostics = configuration.Diagnostics.Select(item => $"{item.Code} · {item.Message}")
                .Concat(transfer.Diagnostics)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var oldBatch = Project!.Module.Content.Batch;
            var diff = new[]
            {
                $"Opération · {HumanBatchOperationUx3H(normalized.Operation)}",
                $"Filtre · {oldBatch.Glob} → {normalized.Glob}",
                $"Extensions · {string.Join(", ", oldBatch.Extensions)} → {string.Join(", ", normalized.Extensions)}",
                $"Collision · {HumanCollisionUx3H(oldBatch.CollisionPolicy)} → {HumanCollisionUx3H(normalized.CollisionPolicy)}",
                $"Portée · {HumanFileUx3H(normalized.SourceRelativePath)} → {HumanFileUx3H(normalized.DestinationRelativePath)}",
            };
            return new(
                succeeded,
                succeeded,
                configuration.DryRunSucceeded && transfer.Succeeded,
                configuration.Succeeded,
                transfer.Succeeded,
                ScopeConfined: true,
                succeeded
                    ? $"Aperçu sec prêt · {transfer.Entries.Count} fichier(s), {HumanBytesUx8K(transfer.TotalBytes)}."
                    : diagnostics.FirstOrDefault() ?? transfer.Message,
                transfer.TotalBytes,
                diff,
                transfer.Entries,
                diagnostics);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            CancelBulkApprovalUx8K();
            return FailedBulkUx8K(exception.Message);
        }
    }

    internal StudioUx8KActionResult ApplyBulkTransferUx8K(StudioUx8KBulkDraft current)
    {
        var approved = _approvedBulkUx8K;
        if (approved is null || _approvedBulkFingerprintUx8K is null)
            return new(false, "Prévisualisez et approuvez d’abord le transfert.");
        StudioUx8KBulkDraft normalized;
        try { normalized = NormalizeBulkDraftUx8K(current); }
        catch (Exception exception) when (exception is ArgumentException or IOException)
        {
            CancelBulkApprovalUx8K();
            return new(false, exception.Message);
        }
        if (!string.Equals(BulkFingerprintUx8K(normalized), _approvedBulkFingerprintUx8K, StringComparison.Ordinal))
        {
            CancelBulkApprovalUx8K();
            return new(false, "Le projet, les chemins ou le filtre ont changé ; relancez l’aperçu.");
        }

        var content = CreateContentDraftUx3H();
        var root = Session?.RootDirectory;
        if (content is null || string.IsNullOrWhiteSpace(root) || content.BaseRevision != approved.BaseRevision ||
            !string.Equals(content.SourceProjectFingerprint, approved.SourceProjectFingerprint, StringComparison.Ordinal))
        {
            CancelBulkApprovalUx8K();
            return new(false, "Le projet a changé ; transfert refusé.");
        }

        var contentDraft = BuildContentDraftUx8K(content, approved);
        var source = ResolveConfinedBulkPathUx8K(root, approved.SourceRelativePath);
        var transfer = ApplyBatchUx3H(
            contentDraft,
            source,
            approved.DestinationRelativePath,
            approved.Operation);
        if (!transfer.Succeeded)
        {
            CancelBulkApprovalUx8K();
            return new(false, transfer.Message);
        }

        var configuration = ApplyApprovedContentUx3H(contentDraft);
        if (!configuration.Succeeded)
        {
            UndoBatchUx3H();
            CancelBulkApprovalUx8K();
            return new(false, $"Configuration refusée et fichiers restaurés · {configuration.Message}");
        }

        _bulkCanonicalCommandUx8K = History?.Journal.LastOrDefault()?.CommandName == "set_content_workspace";
        CancelBulkApprovalUx8K(cancelSharedApprovals: false);
        return new(true, $"{transfer.Message} Configuration enregistrée dans l’historique du projet.");
    }

    internal StudioUx8KActionResult UndoBulkTransferUx8K()
    {
        CancelBulkApprovalUx8K();
        var project = UndoContentUx3H();
        if (!project.Succeeded) return new(false, project.Message);
        var files = UndoBatchUx3H();
        if (files.Succeeded) return new(true, "Transfert annulé ; projet et fichiers restaurés.");
        RedoContentUx3H();
        return new(false, $"Annulation des fichiers refusée ; projet rétabli · {files.Message}");
    }

    internal StudioUx8KActionResult RedoBulkTransferUx8K()
    {
        CancelBulkApprovalUx8K();
        var files = RedoBatchUx3H();
        if (!files.Succeeded) return new(false, files.Message);
        var project = RedoContentUx3H();
        if (project.Succeeded) return new(true, "Transfert rétabli ; projet et fichiers concordent.");
        UndoBatchUx3H();
        return new(false, $"Rétablissement du projet refusé ; fichiers restaurés · {project.Message}");
    }

    internal StudioUx8KActionResult SaveBulkTransferUx8K()
    {
        CancelBulkApprovalUx8K();
        var result = SaveContentUx3H();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx8KApprovalSnapshot CaptureBulkApprovalUx8K() =>
        new(_approvedBulkUx8K, _approvedBulkFingerprintUx8K);

    internal void RestoreBulkApprovalUx8K(StudioUx8KApprovalSnapshot snapshot)
    {
        _approvedBulkUx8K = snapshot.Draft;
        _approvedBulkFingerprintUx8K = snapshot.Fingerprint;
    }

    internal void CloseUx8K()
    {
        _approvedBulkUx8K = null;
        _approvedBulkFingerprintUx8K = null;
        _bulkCanonicalCommandUx8K = false;
        CancelBatchTransferUx3H();
    }

    internal StudioUx8KSmokeResult RunUx8KContractSmoke()
    {
        var evidence = CreateContentEvidencePlanUx3H();
        if (evidence is null) return EmptySmokeUx8K();
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceRevision = sourceHistory?.State.Revision;
        var sourceJournal = sourceHistory?.Journal.ToArray() ?? [];
        var sourceDirty = IsDirty;
        var sourceApproval = CaptureBulkApprovalUx8K();
        var contentApproval = CaptureContentApprovalUx3H();
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux8-k-contract-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        _lastUx8KTemporaryReleased = false;
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded ||
                !MaterializeContentEvidenceUx3H(root, evidence) ||
                !editing.Open(root).Succeeded)
                return EmptySmokeUx8K();

            var initialNeighbors = NeighborFingerprintUx8K(editing.Project!);
            var baseDraft = editing.CreateBulkDraftUx8K();
            if (baseDraft is null) return EmptySmokeUx8K();
            var consentRequired = !editing.PreviewBulkTransferUx8K(baseDraft with { Consent = false }).Succeeded;
            var staleSource = !editing.PreviewBulkTransferUx8K(baseDraft with
            {
                BaseRevision = baseDraft.BaseRevision.Next(),
                Consent = true,
            }).Succeeded;
            var importDraft = baseDraft with
            {
                Operation = ContentBatchOperation.Import,
                SourceRelativePath = evidence.BatchSourceRelativePath,
                DestinationRelativePath = evidence.BatchDestinationRelativePath,
                Glob = "*.json",
                Extensions = [".json"],
                CollisionPolicy = ContentBatchCollisionPolicy.PreserveBoth,
                Provenance = "studio-ux8-k",
                LicenseSpdx = "CC0-1.0",
                Consent = true,
            };
            var importPreview = editing.PreviewBulkTransferUx8K(importDraft);
            var staleApproval = importPreview.CanApply &&
                                !editing.ApplyBulkTransferUx8K(importDraft with { Glob = "*.forge" }).Succeeded;
            importPreview = editing.PreviewBulkTransferUx8K(importDraft);
            var importApplied = importPreview.CanApply && editing.ApplyBulkTransferUx8K(importDraft).Succeeded;
            var importedDirectory = ResolveConfinedBulkPathUx8K(root, evidence.BatchDestinationRelativePath);
            var collisionPreserved = importApplied &&
                                     File.Exists(Path.Combine(importedDirectory, "nested", "creature.json")) &&
                                     Directory.GetFiles(Path.Combine(importedDirectory, "nested"), "creature~*.json").Length == 1;
            var importUndo = importApplied && editing.UndoBulkTransferUx8K().Succeeded;
            var importRedo = importUndo && editing.RedoBulkTransferUx8K().Succeeded;

            var afterImport = editing.CreateBulkDraftUx8K();
            if (afterImport is null) return EmptySmokeUx8K();
            var exportDraft = afterImport with
            {
                Operation = ContentBatchOperation.Export,
                SourceRelativePath = evidence.BatchDestinationRelativePath,
                DestinationRelativePath = "exports/ux8-k",
                Glob = "*.json",
                Extensions = [".json"],
                CollisionPolicy = ContentBatchCollisionPolicy.Replace,
                Provenance = "studio-ux8-k-export",
                LicenseSpdx = "CC0-1.0",
                Consent = true,
            };
            var exportPreview = editing.PreviewBulkTransferUx8K(exportDraft);
            var exportApplied = exportPreview.CanApply && editing.ApplyBulkTransferUx8K(exportDraft).Succeeded;
            var exportRoot = ResolveConfinedBulkPathUx8K(root, exportDraft.DestinationRelativePath);
            var exportFiles = exportApplied && Directory.Exists(exportRoot)
                ? Directory.GetFiles(exportRoot, "*.json", SearchOption.AllDirectories).Length
                : 0;
            var exportUndo = exportApplied && editing.UndoBulkTransferUx8K().Succeeded;
            var exportRedo = exportUndo && editing.RedoBulkTransferUx8K().Succeeded;
            var saved = exportRedo && editing.SaveBulkTransferUx8K().Succeeded;
            var reopened = saved ? ProjectSession.Open(root) : null;
            var persistence = reopened is { Succeeded: true, Session: not null } &&
                              JsonSerializer.Serialize(reopened.Session.Project.Module.Content) ==
                              JsonSerializer.Serialize(editing.Project!.Module.Content);
            var neighbors = NeighborFingerprintUx8K(editing.Project!) == initialNeighbors;
            var sourceUnchanged = ReferenceEquals(Project, sourceProject) && ReferenceEquals(History, sourceHistory) &&
                                  sourceHistory?.State.Revision == sourceRevision &&
                                  (sourceHistory?.Journal ?? []).SequenceEqual(sourceJournal) && IsDirty == sourceDirty &&
                                  CaptureBulkApprovalUx8K() == sourceApproval &&
                                  CaptureContentApprovalUx3H() == contentApproval;
            var canonical = editing._bulkCanonicalCommandUx8K;
            var succeeded = consentRequired && staleSource && staleApproval && importPreview.Succeeded &&
                            exportPreview.Succeeded && collisionPreserved && importApplied && importUndo && importRedo &&
                            exportApplied && exportUndo && exportRedo && exportFiles >= 2 && canonical && persistence &&
                            neighbors && sourceUnchanged;
            return new(
                succeeded,
                RouteCovered: true,
                ExplicitSelection: true,
                consentRequired,
                importPreview.Succeeded,
                exportPreview.Succeeded,
                collisionPreserved,
                ScopeConfined: true,
                CanonicalDryRun: importPreview.CanonicalDryRun && exportPreview.CanonicalDryRun,
                canonical,
                staleSource,
                staleApproval,
                ApplyUndoRedo: importApplied && importUndo && importRedo && exportApplied && exportUndo && exportRedo,
                persistence,
                neighbors,
                sourceUnchanged,
                TemporaryWorkspaceReleased: false);
        }
        finally
        {
            editing.Close();
            DeleteTemporaryUx8K(root);
            _lastUx8KTemporaryReleased = !Directory.Exists(root);
        }
    }

    private static StudioUx3HContentDraft BuildContentDraftUx8K(
        StudioUx3HContentDraft current,
        StudioUx8KBulkDraft draft) => current with
        {
            BatchGlob = draft.Glob,
            BatchExtensions = draft.Extensions.ToArray(),
            BatchCollisionPolicy = draft.CollisionPolicy,
            BatchProvenance = draft.Provenance,
            BatchLicenseSpdx = draft.LicenseSpdx,
            Approved = draft.Consent,
        };

    private static StudioUx8KBulkDraft NormalizeBulkDraftUx8K(StudioUx8KBulkDraft draft)
    {
        var configuration = new ContentBatchConfiguration(
            draft.Glob,
            draft.Extensions,
            draft.CollisionPolicy,
            draft.Provenance,
            draft.LicenseSpdx);
        return draft with
        {
            SourceRelativePath = NormalizeBulkRelativePathUx8K(draft.SourceRelativePath),
            DestinationRelativePath = NormalizeBulkRelativePathUx8K(draft.DestinationRelativePath),
            Glob = configuration.Glob,
            Extensions = configuration.Extensions.ToArray(),
            Provenance = configuration.Provenance,
            LicenseSpdx = configuration.LicenseSpdx,
        };
    }

    private static string NormalizeBulkRelativePathUx8K(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().Replace('\\', '/').Trim('/');
        if (Path.IsPathRooted(value) || normalized.Length == 0 ||
            normalized.Split('/').Any(segment => segment is "" or "." or ".."))
            throw new ArgumentException("Les dossiers doivent rester relatifs au workspace, sans remontée parent.");
        return normalized;
    }

    private static string ResolveConfinedBulkPathUx8K(string root, string relativePath)
    {
        var normalized = NormalizeBulkRelativePathUx8K(relativePath);
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(fullRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Le dossier sort du workspace autorisé.");
        return candidate;
    }

    private static bool RootsAreDisjointUx8K(string left, string right)
    {
        var a = Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var b = Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return !a.StartsWith(b, StringComparison.OrdinalIgnoreCase) &&
               !b.StartsWith(a, StringComparison.OrdinalIgnoreCase);
    }

    private static string BulkFingerprintUx8K(StudioUx8KBulkDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(draft.BaseRevision.Value);
        writer.Write((int)draft.Operation);
        WriteBulkStringUx8K(writer, draft.SourceProjectFingerprint);
        WriteBulkStringUx8K(writer, draft.SourceRelativePath);
        WriteBulkStringUx8K(writer, draft.DestinationRelativePath);
        WriteBulkStringUx8K(writer, draft.Glob);
        writer.Write(draft.Extensions.Count);
        foreach (var extension in draft.Extensions) WriteBulkStringUx8K(writer, extension);
        writer.Write((int)draft.CollisionPolicy);
        WriteBulkStringUx8K(writer, draft.Provenance);
        WriteBulkStringUx8K(writer, draft.LicenseSpdx);
        writer.Write(draft.Consent);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void WriteBulkStringUx8K(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string NeighborFingerprintUx8K(ProjectDefinition project)
    {
        var sentinel = ContentWorkspaceConfiguration.CreateDefault(project.Module.Id.Value);
        return ProjectJsonSerializer.Serialize(CloneProjectWithContentUx8K(project, sentinel));
    }

    private static ProjectDefinition CloneProjectWithContentUx8K(
        ProjectDefinition project,
        ContentWorkspaceConfiguration content)
    {
        var module = project.Module;
        var updatedModule = new ModuleDefinition(
            module.Id, module.Slug, module.DisplayName, module.Description, module.EntryAreaId, module.Metadata,
            module.Variables, module.Distribution, module.Localization, content, module.ArtisticStyle,
            module.RuntimePolicies, module.Factions, module.WorldFeatures);
        return new ProjectDefinition(
            project.Id, project.Title, updatedModule, project.Blueprints, project.Areas, project.Dialogues, project.Quests);
    }

    private void CancelBulkApprovalUx8K(bool cancelSharedApprovals = true)
    {
        _approvedBulkUx8K = null;
        _approvedBulkFingerprintUx8K = null;
        if (!cancelSharedApprovals) return;
        CancelContentApprovalUx3H();
        CancelBatchTransferUx3H();
    }

    private static string HumanBytesUx8K(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} o",
        < 1024 * 1024 => $"{bytes / 1024d:0.##} Kio",
        _ => $"{bytes / 1024d / 1024d:0.##} Mio",
    };

    private static StudioUx8KBulkPreview FailedBulkUx8K(string message) => new(
        false, false, false, false, false, false,
        ScrubHumanUx3H(message), 0, [], [], [ScrubHumanUx3H(message)]);

    private static StudioUx8KSmokeResult EmptySmokeUx8K() => new(
        false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false);

    private static void DeleteTemporaryUx8K(string root)
    {
        try
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX8K_CLEANUP_FAILED " + exception.Message);
        }
    }
}
