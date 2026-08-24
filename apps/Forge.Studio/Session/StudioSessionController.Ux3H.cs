using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Forge.Application.Commands;
using Forge.Application.Content;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Content;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Content;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const long MaximumInteractiveTransferBytesUx3H = 32L * 1024 * 1024;

    private readonly SetContentWorkspaceHandler _setContentUx3H = new(new SystemClock());
    private StudioUx3HContentDraft? _approvedContentUx3H;
    private ProjectRevision? _approvedContentRevisionUx3H;
    private string? _approvedContentProjectUx3H;
    private string? _approvedContentDraftUx3H;
    private ContentBatchTransferRequest? _approvedBatchUx3H;
    private string? _approvedBatchFingerprintUx3H;
    private Ux3HBatchReceipt? _batchReceiptUx3H;
    private bool _batchUndoneUx3H;
    private Ux3HExportPlan? _approvedExportUx3H;
    private string? _approvedExportFingerprintUx3H;
    private Ux3HExportReceipt? _exportReceiptUx3H;
    private bool _exportUndoneUx3H;
    private bool _contentCanonicalCommandUx3H;
    private bool _lastUx3HTemporaryReleased = true;

    internal bool CanUndoBatchUx3H => _batchReceiptUx3H is not null && !_batchUndoneUx3H;
    internal bool CanRedoBatchUx3H => _batchReceiptUx3H is not null && _batchUndoneUx3H;
    internal bool CanUndoExportUx3H => _exportReceiptUx3H is not null && !_exportUndoneUx3H;
    internal bool CanRedoExportUx3H => _exportReceiptUx3H is not null && _exportUndoneUx3H;
    internal bool LastUx3HTemporaryReleased => _lastUx3HTemporaryReleased;

    internal StudioUx3HContentDraft? CreateContentDraftUx3H()
    {
        var project = Project;
        var history = History;
        if (project is null || history is null) return null;
        var content = project.Module.Content;
        return new(
            history.State.Revision,
            ProjectFingerprintUx3H(project),
            content.Id,
            content.Batch.Glob,
            content.Batch.Extensions.ToArray(),
            content.Batch.CollisionPolicy,
            content.Batch.Provenance,
            content.Batch.LicenseSpdx,
            content.MergePolicy,
            content.Provenance,
            content.Packages,
            content.DataTables.ToArray(),
            Approved: false);
    }

    internal StudioUx3HContentPreview PreviewContentUx3H(StudioUx3HContentDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var project = Project;
        var history = History;
        var root = Session?.RootDirectory;
        if (project is null || history is null || string.IsNullOrWhiteSpace(root))
            return FailedContentUx3H("Ouvrez un workspace avant l’aperçu.", "content.workspace.unavailable", "module/content");
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(ProjectFingerprintUx3H(project), draft.SourceProjectFingerprint, StringComparison.Ordinal))
        {
            CancelContentApprovalUx3H();
            return FailedContentUx3H(
                "Le projet a changé ; rechargez le brouillon avant l’aperçu.",
                SetContentWorkspaceHandler.RevisionConflictCode,
                "module/content");
        }

        ContentWorkspaceConfiguration candidate;
        try { candidate = BuildContentUx3H(draft); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException)
        {
            CancelContentApprovalUx3H();
            return FailedContentUx3H(exception.Message, "content.workspace.invalid", "module/content");
        }

        var command = new SetContentWorkspaceCommand(
            CommandId.From(Guid.NewGuid()), history.State.Revision, candidate, DryRun: true);
        var result = _setContentUx3H.Handle(history.State, command);
        var compilation = ContentPackageCompiler.Compile(root, project.Module.Id.Value, candidate.Packages);
        var diagnostics = result.Diagnostics.Select(item =>
                new StudioUx3HDiagnostic(item.Code, "module/content", ScrubHumanUx3H(item.Message)))
            .Concat(compilation.Diagnostics.Select(item =>
                new StudioUx3HDiagnostic(item.Code, item.Path, ScrubHumanUx3H(item.Message))))
            .DistinctBy(item => (item.Code, item.Path)).ToArray();
        var dryRun = result.Succeeded && result.Patch is not null && result.Inverse is not null && !result.Audit.Applied;
        var sourcesResolved = compilation.Success;
        var tablesValid = candidate.DataTables.All(table => table.Columns.Count > 0);
        var succeeded = dryRun && sourcesResolved && tablesValid && diagnostics.Length == 0;
        var frozen = FreezeContentUx3H(draft);
        if (succeeded && draft.Approved)
        {
            _approvedContentUx3H = frozen;
            _approvedContentRevisionUx3H = draft.BaseRevision;
            _approvedContentProjectUx3H = draft.SourceProjectFingerprint;
            _approvedContentDraftUx3H = ContentDraftFingerprintUx3H(frozen);
        }
        else CancelContentApprovalUx3H();

        var sourcePlan = candidate.Packages.Packages.SelectMany(package => package.Sources.Select(source =>
            $"{HumanSourceKindUx3H(source.Kind)} · {ScrubHumanUx3H(source.LogicalResourceId)} · " +
            $"{HumanFileUx3H(source.RelativePath)} · {(source.Required ? "requise" : "optionnelle")}"))
            .DefaultIfEmpty("Aucune source texte déclarée ; état vide conservé.").ToArray();
        var tablePlan = candidate.DataTables.Select(table =>
            $"{ScrubHumanUx3H(table.DisplayName)} · {table.Columns.Count} colonne(s) · {table.Rows.Count} ligne(s) · " +
            $"{table.LicenseSpdx}").DefaultIfEmpty("Aucune table typée déclarée ; état vide conservé.").ToArray();
        var previous = project.Module.Content;
        var diff = new[]
        {
            $"Filtre import : {previous.Batch.Glob} → {candidate.Batch.Glob}",
            $"Sources texte : {previous.Packages.Packages.Sum(item => item.Sources.Count)} → " +
            $"{candidate.Packages.Packages.Sum(item => item.Sources.Count)}",
            $"Tables typées : {previous.DataTables.Count} → {candidate.DataTables.Count}",
            $"Politique de fusion : {HumanMergeUx3H(previous.MergePolicy)} → {HumanMergeUx3H(candidate.MergePolicy)}",
        };
        return new(
            succeeded,
            succeeded && draft.Approved,
            dryRun,
            sourcesResolved,
            tablesValid,
            RuntimeClaimsBounded: true,
            succeeded
                ? "Aperçu sec canonique prêt ; sources et tables validées sans mutation."
                : diagnostics.FirstOrDefault()?.Message ?? "Le contenu doit être corrigé.",
            HumanPatchUx3H(result.Patch?.Summary, candidate),
            HumanInverseUx3H(result.Inverse?.Summary, previous),
            diff,
            diagnostics,
            sourcePlan,
            tablePlan);
    }

    internal StudioUx3HActionResult ApplyApprovedContentUx3H(StudioUx3HContentDraft current)
    {
        var project = Project;
        var history = History;
        var approved = _approvedContentUx3H;
        if (project is null || history is null || approved is null || _approvedContentRevisionUx3H is null ||
            _approvedContentProjectUx3H is null || _approvedContentDraftUx3H is null)
            return new(false, "Prévisualisez et approuvez d’abord la configuration contenu.");
        var stale = history.State.Revision != _approvedContentRevisionUx3H ||
                    !string.Equals(ProjectFingerprintUx3H(project), _approvedContentProjectUx3H, StringComparison.Ordinal) ||
                    !string.Equals(ContentDraftFingerprintUx3H(approved), _approvedContentDraftUx3H, StringComparison.Ordinal) ||
                    !string.Equals(ContentDraftFingerprintUx3H(current), _approvedContentDraftUx3H, StringComparison.Ordinal);
        if (stale)
        {
            CancelContentApprovalUx3H();
            return new(false, "Le brouillon ou le projet a changé ; relancez l’aperçu.");
        }

        ContentWorkspaceConfiguration candidate;
        try { candidate = BuildContentUx3H(approved); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException)
        {
            CancelContentApprovalUx3H();
            return new(false, exception.Message);
        }
        var dryRun = _setContentUx3H.Handle(history.State, new(
            CommandId.From(Guid.NewGuid()), history.State.Revision, candidate, DryRun: true));
        if (!dryRun.Succeeded)
        {
            CancelContentApprovalUx3H();
            return new(false, ScrubHumanUx3H(dryRun.Diagnostics.FirstOrDefault()?.Message ?? "Le re-dry-run refuse la configuration."));
        }
        var result = _setContentUx3H.Handle(history.State, new(
            CommandId.From(Guid.NewGuid()), history.State.Revision, candidate, DryRun: false));
        CancelContentApprovalUx3H();
        if (!result.Succeeded)
            return new(false, ScrubHumanUx3H(result.Diagnostics.FirstOrDefault()?.Message ?? "Application refusée."));
        history.Record(result);
        _contentCanonicalCommandUx3H = result.Audit.Applied &&
                                      string.Equals(result.Audit.CommandName, "set_content_workspace", StringComparison.Ordinal);
        return new(true, HumanPatchUx3H(result.Patch?.Summary, candidate));
    }

    internal StudioUx3HActionResult UndoContentUx3H()
    {
        CancelContentApprovalUx3H();
        var result = Undo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx3HActionResult RedoContentUx3H()
    {
        CancelContentApprovalUx3H();
        var result = Redo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx3HActionResult SaveContentUx3H()
    {
        CancelContentApprovalUx3H();
        var result = Save();
        return new(result.Succeeded, result.Message);
    }

    internal IReadOnlyList<StudioUx3HBlueprintChoice> ListBlueprintsUx3H() => (Project?.Blueprints ?? [])
        .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(item => item.Slug, StringComparer.Ordinal)
        .Select(item => new StudioUx3HBlueprintChoice(
            item.Id,
            ScrubHumanUx3H(item.DisplayName),
            HumanBlueprintKindUx3H(item.Kind),
            ScrubHumanUx3H(item.Provenance.Source)))
        .ToArray();

    internal StudioUx3HActionResult ReadSourceHashUx3H(string relativePath, out string contentSha256)
    {
        contentSha256 = string.Empty;
        var root = Session?.RootDirectory;
        if (string.IsNullOrWhiteSpace(root)) return new(false, "Aucun workspace ouvert.");
        try
        {
            var path = ResolveConfinedUx3H(root, ContentPackageDefinition.NormalizePath(relativePath), allowRoot: false);
            if (!File.Exists(path)) return new(false, $"Le fichier « {HumanFileUx3H(relativePath)} » est absent.");
            contentSha256 = HashFileUx3H(path);
            return new(true, $"Empreinte recalculée depuis « {HumanFileUx3H(relativePath)} ».");
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return new(false, exception.Message);
        }
    }

    internal static string ComputeTableHashUx3H(
        string slug,
        string displayName,
        IReadOnlyList<DataColumnDefinition> columns,
        IReadOnlyList<DataRowDefinition> rows,
        DataTableId? overridesTableId,
        string provenance,
        string licenseSpdx) => HashBinaryUx3H(writer =>
    {
        WriteStringUx3H(writer, slug);
        WriteStringUx3H(writer, displayName);
        foreach (var column in columns)
        {
            WriteStringUx3H(writer, column.Id.ToString());
            WriteStringUx3H(writer, column.Key);
            WriteStringUx3H(writer, column.DisplayName);
            writer.Write((int)column.Kind);
            writer.Write(column.Required);
            WriteStringUx3H(writer, column.ReferenceTableId?.ToString());
        }
        foreach (var row in rows)
        {
            WriteStringUx3H(writer, row.Id.ToString());
            WriteStringUx3H(writer, row.Provenance);
            foreach (var value in row.Values.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                WriteStringUx3H(writer, value.Key);
                WriteStringUx3H(writer, value.Value);
            }
        }
        WriteStringUx3H(writer, overridesTableId?.ToString());
        WriteStringUx3H(writer, provenance);
        WriteStringUx3H(writer, licenseSpdx);
    });

    internal StudioUx3HBatchPreview PreviewBatchUx3H(
        StudioUx3HContentDraft contentDraft,
        string sourceRoot,
        string destinationRelativePath,
        ContentBatchOperation operation = ContentBatchOperation.Import)
    {
        CancelBatchApprovalUx3H();
        var root = Session?.RootDirectory;
        if (string.IsNullOrWhiteSpace(root))
            return FailedBatchUx3H("Aucun workspace ouvert.");
        try
        {
            var destination = ResolveConfinedUx3H(root, destinationRelativePath, allowRoot: false);
            var request = new ContentBatchTransferRequest(
                operation,
                sourceRoot,
                destination,
                contentDraft.BatchGlob,
                contentDraft.BatchExtensions,
                contentDraft.BatchCollisionPolicy,
                contentDraft.BatchProvenance,
                contentDraft.BatchLicenseSpdx,
                dryRun: true);
            var report = ContentBatchTransferService.Plan(request);
            var bounded = report.TotalBytes <= MaximumInteractiveTransferBytesUx3H;
            var succeeded = report.Valid && report.Entries.Count > 0 && bounded;
            if (succeeded)
            {
                _approvedBatchUx3H = request;
                _approvedBatchFingerprintUx3H = BatchFingerprintUx3H(request, contentDraft);
            }
            var entries = report.Entries.Select(entry =>
                $"{HumanFileUx3H(entry.RelativePath)} · {HumanBytesUx3H(entry.Bytes)} · " +
                $"{HumanBatchStatusUx3H(entry.Status, entry.CollisionPath is not null)}").ToArray();
            var diagnostics = report.Diagnostics.Select(ScrubHumanUx3H).ToList();
            if (!bounded) diagnostics.Add("Le transfert interactif dépasse 32 Mio ; réduisez le filtre ou le dossier source.");
            return new(
                succeeded,
                succeeded,
                succeeded ? $"{report.Entries.Count} fichier(s) prêts · {HumanBytesUx3H(report.TotalBytes)}." :
                    diagnostics.FirstOrDefault() ?? "Aucun fichier ne correspond au filtre.",
                request.SourceRoot,
                destinationRelativePath,
                report.TotalBytes,
                entries,
                diagnostics);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return FailedBatchUx3H(exception.Message);
        }
    }

    internal StudioUx3HActionResult ApplyBatchUx3H(
        StudioUx3HContentDraft current,
        string sourceRoot,
        string destinationRelativePath,
        ContentBatchOperation operation = ContentBatchOperation.Import)
    {
        var approved = _approvedBatchUx3H;
        var approvedFingerprint = _approvedBatchFingerprintUx3H;
        if (approved is null || approvedFingerprint is null)
            return new(false, "Prévisualisez d’abord le transfert de masse.");
        ContentBatchTransferRequest request;
        try
        {
            var root = Session?.RootDirectory ?? throw new InvalidOperationException("Aucun workspace ouvert.");
            request = new ContentBatchTransferRequest(
                operation,
                sourceRoot,
                ResolveConfinedUx3H(root, destinationRelativePath, allowRoot: false),
                current.BatchGlob,
                current.BatchExtensions,
                current.BatchCollisionPolicy,
                current.BatchProvenance,
                current.BatchLicenseSpdx,
                dryRun: true);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            CancelBatchApprovalUx3H();
            return new(false, exception.Message);
        }
        if (!string.Equals(BatchFingerprintUx3H(request, current), approvedFingerprint, StringComparison.Ordinal))
        {
            CancelBatchApprovalUx3H();
            return new(false, "Le filtre, les chemins ou la configuration ont changé ; relancez l’aperçu.");
        }
        var plan = ContentBatchTransferService.Plan(approved);
        if (!plan.Valid || plan.TotalBytes > MaximumInteractiveTransferBytesUx3H)
        {
            CancelBatchApprovalUx3H();
            return new(false, plan.Diagnostics.FirstOrDefault() ?? "Le re-dry-run refuse le transfert.");
        }
        try
        {
            var backups = plan.Entries.Where(item => item.Status != ContentBatchEntryStatus.Skipped)
                .Select(item => new Ux3HFileBackup(
                    item.TargetPath,
                    File.Exists(item.TargetPath) ? File.ReadAllBytes(item.TargetPath) : null,
                    item.ContentSha256))
                .ToArray();
            var applied = ContentBatchTransferService.Execute(new ContentBatchTransferRequest(
                approved.Operation, approved.SourceRoot, approved.DestinationRoot, approved.Glob, approved.Extensions,
                approved.CollisionPolicy, approved.Provenance, approved.LicenseSpdx, dryRun: false));
            CancelBatchApprovalUx3H();
            if (!applied.Valid) return new(false, applied.Diagnostics.FirstOrDefault() ?? "Le transfert a échoué.");
            _batchReceiptUx3H = new(approved, backups);
            _batchUndoneUx3H = false;
            return new(true,
                $"{HumanBatchOperationUx3H(operation)} appliqué · {applied.Added} ajouté(s), {applied.Replaced} remplacé(s), " +
                $"{applied.PreservedBoth} variante(s) conservée(s).");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            CancelBatchApprovalUx3H();
            return new(false, exception.Message);
        }
    }

    internal StudioUx3HActionResult UndoBatchUx3H()
    {
        var receipt = _batchReceiptUx3H;
        if (receipt is null || _batchUndoneUx3H) return new(false, "Aucun transfert appliqué à annuler.");
        try
        {
            foreach (var backup in receipt.Backups)
            {
                if (!File.Exists(backup.TargetPath) || !string.Equals(HashFileUx3H(backup.TargetPath), backup.AppliedHash, StringComparison.Ordinal))
                    return new(false, $"Le fichier « {HumanFileUx3H(backup.TargetPath)} » a changé ; annulation refusée.");
            }
            foreach (var backup in receipt.Backups.Reverse())
            {
                if (backup.OriginalBytes is null) File.Delete(backup.TargetPath);
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(backup.TargetPath)!);
                    File.WriteAllBytes(backup.TargetPath, backup.OriginalBytes);
                }
            }
            _batchUndoneUx3H = true;
            return new(true, $"{HumanBatchOperationUx3H(receipt.Request.Operation)} annulé ; les fichiers précédents sont restaurés.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(false, exception.Message);
        }
    }

    internal StudioUx3HActionResult RedoBatchUx3H()
    {
        var receipt = _batchReceiptUx3H;
        if (receipt is null || !_batchUndoneUx3H) return new(false, "Aucun transfert annulé à rétablir.");
        var request = receipt.Request;
        var result = ContentBatchTransferService.Execute(new ContentBatchTransferRequest(
            request.Operation, request.SourceRoot, request.DestinationRoot, request.Glob, request.Extensions,
            request.CollisionPolicy, request.Provenance, request.LicenseSpdx, dryRun: false));
        if (!result.Valid) return new(false, result.Diagnostics.FirstOrDefault() ?? "Rétablissement refusé.");
        _batchUndoneUx3H = false;
        return new(true, $"{HumanBatchOperationUx3H(request.Operation)} rétabli depuis la même source gouvernée.");
    }

    internal StudioUx3HExportPreview PreviewBlueprintExportUx3H(
        IReadOnlyList<BlueprintId> selectedBlueprintIds,
        string outputRelativePath,
        bool includePreviews)
    {
        CancelExportApprovalUx3H();
        var project = Project;
        var root = Session?.RootDirectory;
        if (project is null || string.IsNullOrWhiteSpace(root))
            return FailedExportUx3H("Aucun workspace ouvert.");
        try
        {
            var normalized = NormalizeExportPathUx3H(outputRelativePath);
            var tempRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux3-h-export-{Guid.NewGuid():N}");
            var tempPath = Path.Combine(tempRoot, Path.GetFileName(normalized));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var result = BlueprintExportPackageBuilder.Build(project, selectedBlueprintIds, tempPath, includePreviews);
                var entries = ReadArchiveEntriesUx3H(tempPath);
                var plan = new Ux3HExportPlan(
                    selectedBlueprintIds.Distinct().OrderBy(item => item.Value).ToArray(),
                    normalized,
                    includePreviews,
                    ProjectFingerprintUx3H(project),
                    result.ContentSha256);
                _approvedExportUx3H = plan;
                _approvedExportFingerprintUx3H = ExportFingerprintUx3H(plan);
                return new(true, true,
                    $"Pack déterministe prêt · {result.BlueprintCount} blueprint(s) et {result.DependencyCount} dépendance(s).",
                    normalized, result.ContentSha256, result.Bytes, result.BlueprintCount, result.DependencyCount,
                    result.PreviewCount, entries);
            }
            finally { DeleteTemporaryUx3H(tempRoot); }
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidOperationException)
        {
            return FailedExportUx3H(exception.Message);
        }
    }

    internal StudioUx3HActionResult ApplyBlueprintExportUx3H(
        IReadOnlyList<BlueprintId> selectedBlueprintIds,
        string outputRelativePath,
        bool includePreviews)
    {
        var project = Project;
        var root = Session?.RootDirectory;
        var approved = _approvedExportUx3H;
        if (project is null || string.IsNullOrWhiteSpace(root) || approved is null || _approvedExportFingerprintUx3H is null)
            return new(false, "Prévisualisez d’abord l’export de blueprints.");
        Ux3HExportPlan current;
        try
        {
            current = new(
                selectedBlueprintIds.Distinct().OrderBy(item => item.Value).ToArray(),
                NormalizeExportPathUx3H(outputRelativePath),
                includePreviews,
                ProjectFingerprintUx3H(project),
                approved.ExpectedHash);
        }
        catch (ArgumentException exception)
        {
            CancelExportApprovalUx3H();
            return new(false, exception.Message);
        }
        if (!string.Equals(ExportFingerprintUx3H(current), _approvedExportFingerprintUx3H, StringComparison.Ordinal))
        {
            CancelExportApprovalUx3H();
            return new(false, "La sélection, la sortie ou le projet a changé ; relancez l’aperçu.");
        }
        try
        {
            var output = ResolveConfinedUx3H(root, approved.OutputRelativePath, allowRoot: false);
            var original = File.Exists(output) ? File.ReadAllBytes(output) : null;
            var result = BlueprintExportPackageBuilder.Build(project, approved.BlueprintIds, output, approved.IncludePreviews);
            CancelExportApprovalUx3H();
            if (!string.Equals(result.ContentSha256, approved.ExpectedHash, StringComparison.Ordinal))
                return new(false, "L’export diffère du dry-run déterministe ; sortie refusée.");
            _exportReceiptUx3H = new(approved, output, original, result.ContentSha256);
            _exportUndoneUx3H = false;
            return new(true, $"Pack exporté · {result.BlueprintCount} blueprint(s) · {HumanBytesUx3H(result.Bytes)}.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            CancelExportApprovalUx3H();
            return new(false, exception.Message);
        }
    }

    internal StudioUx3HActionResult UndoBlueprintExportUx3H()
    {
        var receipt = _exportReceiptUx3H;
        if (receipt is null || _exportUndoneUx3H) return new(false, "Aucun export appliqué à annuler.");
        try
        {
            if (!File.Exists(receipt.OutputPath) || !string.Equals(HashFileUx3H(receipt.OutputPath), receipt.AppliedHash, StringComparison.Ordinal))
                return new(false, "Le pack exporté a changé ; annulation refusée.");
            if (receipt.OriginalBytes is null) File.Delete(receipt.OutputPath);
            else File.WriteAllBytes(receipt.OutputPath, receipt.OriginalBytes);
            _exportUndoneUx3H = true;
            return new(true, "Export annulé ; la sortie précédente est restaurée.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(false, exception.Message);
        }
    }

    internal StudioUx3HActionResult RedoBlueprintExportUx3H()
    {
        var project = Project;
        var receipt = _exportReceiptUx3H;
        if (project is null || receipt is null || !_exportUndoneUx3H)
            return new(false, "Aucun export annulé à rétablir.");
        try
        {
            var result = BlueprintExportPackageBuilder.Build(
                project, receipt.Plan.BlueprintIds, receipt.OutputPath, receipt.Plan.IncludePreviews);
            if (!string.Equals(result.ContentSha256, receipt.AppliedHash, StringComparison.Ordinal))
                return new(false, "Le pack rétabli n’est plus déterministe.");
            _exportUndoneUx3H = false;
            return new(true, "Export rétabli bit à bit.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new(false, exception.Message);
        }
    }

    internal StudioUx3HApprovalSnapshot CaptureContentApprovalUx3H() => new(
        _approvedContentUx3H,
        _approvedContentRevisionUx3H,
        _approvedContentProjectUx3H,
        _approvedContentDraftUx3H,
        _approvedBatchFingerprintUx3H,
        _approvedExportFingerprintUx3H);

    internal void RestoreContentApprovalUx3H(StudioUx3HApprovalSnapshot snapshot)
    {
        _approvedContentUx3H = snapshot.Draft;
        _approvedContentRevisionUx3H = snapshot.Revision;
        _approvedContentProjectUx3H = snapshot.ProjectFingerprint;
        _approvedContentDraftUx3H = snapshot.DraftFingerprint;
        _approvedBatchFingerprintUx3H = snapshot.BatchFingerprint;
        _approvedExportFingerprintUx3H = snapshot.ExportFingerprint;
        _approvedBatchUx3H = null;
        _approvedExportUx3H = null;
    }

    internal void CancelContentApprovalUx3H()
    {
        _approvedContentUx3H = null;
        _approvedContentRevisionUx3H = null;
        _approvedContentProjectUx3H = null;
        _approvedContentDraftUx3H = null;
    }

    internal void CancelBatchTransferUx3H() => CancelBatchApprovalUx3H();

    internal void CloseUx3H()
    {
        CancelContentApprovalUx3H();
        CancelBatchApprovalUx3H();
        CancelExportApprovalUx3H();
        _batchReceiptUx3H = null;
        _exportReceiptUx3H = null;
        _batchUndoneUx3H = false;
        _exportUndoneUx3H = false;
    }

    internal StudioUx3HEvidencePlan? CreateContentEvidencePlanUx3H()
    {
        var project = Project;
        var root = Session?.RootDirectory;
        if (project is null || string.IsNullOrWhiteSpace(root) || project.Blueprints.Count == 0) return null;

        const string sourceRelative = "content/ux3-h/creatures.json";
        var sourceBytes = EvidenceSourceBytesUx3H();
        var source = new ContentPackageSource(
            StableGuidUx3H("source"),
            1,
            "studio-evidence",
            "creatures/ash-warden",
            sourceRelative,
            ContentSourceKind.Json,
            Required: true,
            "CC0-1.0",
            HashBytesUx3H(sourceBytes),
            65_536,
            ["studio.ux3h:text/creatures/warden"],
            []);
        var packageDraft = new ContentPackageDefinition(
            StableGuidUx3H("package"),
            ContentPackageDefinition.CurrentSchemaVersion,
            "studio-evidence",
            "studio.ux3h",
            "1.0.0",
            "CC0-1.0",
            new string('0', 64),
            100,
            [],
            [],
            [source],
            []);
        var package = RebuildPackageUx3H(packageDraft);
        var tableId = DataTableId.From(StableGuidUx3H("table"));
        var columns = new[]
        {
            new DataColumnDefinition(DataColumnId.From(StableGuidUx3H("column-name")),
                "name", "Nom", DataColumnKind.Text, required: true),
            new DataColumnDefinition(DataColumnId.From(StableGuidUx3H("column-level")),
                "level", "Niveau", DataColumnKind.Integer, required: true),
            new DataColumnDefinition(DataColumnId.From(StableGuidUx3H("column-elite")),
                "elite", "Élite", DataColumnKind.Boolean),
        };
        var rows = new[]
        {
            new DataRowDefinition(DataRowId.From(StableGuidUx3H("row-warden")),
                new Dictionary<string, string>
                {
                    ["name"] = "Gardien des cendres",
                    ["level"] = "12",
                    ["elite"] = "true",
                }, "studio-evidence"),
            new DataRowDefinition(DataRowId.From(StableGuidUx3H("row-scout")),
                new Dictionary<string, string>
                {
                    ["name"] = "Éclaireur de la crypte",
                    ["level"] = "6",
                    ["elite"] = "false",
                }, "studio-evidence"),
        };
        var table = new DataTableDefinition(
            tableId,
            "creature-archetypes",
            "Archétypes de créatures",
            columns,
            rows,
            provenance: "studio-evidence",
            licenseSpdx: "CC0-1.0",
            contentHash: HashBytesUx3H(Encoding.UTF8.GetBytes("ux3-h-table-v1")));
        var content = new ContentWorkspaceConfiguration(
            ContentWorkspaceConfigurationId.From(project.Module.Id.Value),
            new ContentBatchConfiguration("*", [".json", ".txt"], ContentBatchCollisionPolicy.PreserveBoth,
                "studio-evidence", "CC0-1.0"),
            [table],
            ContentMergePolicy.PreserveBoth,
            "studio-evidence",
            packages: new ContentPackageCatalog(project.Module.Id.Value, ContentPackageCatalog.CurrentSchemaVersion,
                "studio-evidence", ContentConflictPolicy.HighestPriority, [package]));
        var module = CloneModuleWithContentUx3H(project.Module, content);
        var evidenceProject = new ProjectDefinition(
            project.Id, project.Title, module, project.Blueprints, project.Areas, project.Dialogues, project.Quests);
        return new(
            evidenceProject,
            root,
            "evidence-import",
            "content/imported",
            sourceRelative,
            project.Blueprints.OrderBy(item => item.Id.Value).First().Id,
            UsesRealModule: false,
            UsesCanonicalFallback: true);
    }

    internal static bool MaterializeContentEvidenceUx3H(string root, StudioUx3HEvidencePlan plan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(plan);
        try
        {
            var sourcePath = ResolveConfinedUx3H(root, plan.SourceFileRelativePath, allowRoot: false);
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllBytes(sourcePath, EvidenceSourceBytesUx3H());

            var batchRoot = ResolveConfinedUx3H(root, plan.BatchSourceRelativePath, allowRoot: false);
            Directory.CreateDirectory(Path.Combine(batchRoot, "nested"));
            File.WriteAllText(Path.Combine(batchRoot, "nested", "creature.json"),
                "{\"name\":\"Témoin UX3-H\"}\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(batchRoot, "notes.txt"),
                "Import de masse gouverné UX3-H.\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(batchRoot, "ignored.bin"), "ignored", new UTF8Encoding(false));

            var destination = ResolveConfinedUx3H(root, plan.BatchDestinationRelativePath, allowRoot: false);
            Directory.CreateDirectory(Path.Combine(destination, "nested"));
            File.WriteAllText(Path.Combine(destination, "nested", "creature.json"),
                "{\"name\":\"Original préservé\"}\n", new UTF8Encoding(false));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    internal StudioUx3HSmokeResult RunUx3HContractSmoke()
    {
        var evidence = CreateContentEvidencePlanUx3H();
        if (evidence is null) return EmptySmokeUx3H();
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceRevision = sourceHistory?.State.Revision;
        var sourceJournal = sourceHistory?.Journal.ToArray() ?? [];
        var sourceApproval = CaptureContentApprovalUx3H();
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux3-h-contract-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        _lastUx3HTemporaryReleased = false;
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded ||
                !MaterializeContentEvidenceUx3H(root, evidence) ||
                !editing.Open(root).Succeeded)
                return EmptySmokeUx3H();
            var initialProject = ProjectJsonSerializer.Serialize(editing.Project!);
            var initialNeighbors = NeighborFingerprintUx3H(editing.Project!);
            var draft = editing.CreateContentDraftUx3H();
            if (draft is null) return EmptySmokeUx3H();
            var textSources = draft.Packages.Packages.SelectMany(item => item.Sources).Any(source =>
                source.Kind == ContentSourceKind.Json && source.Required && source.ContentSha256.Length == 64);
            var dataTables = draft.DataTables is [{ Columns.Count: >= 3, Rows.Count: >= 2 }];
            var batchConfiguration = draft.BatchCollisionPolicy == ContentBatchCollisionPolicy.PreserveBoth &&
                                     draft.BatchExtensions.Contains(".json", StringComparer.OrdinalIgnoreCase);

            var staleSource = !editing.PreviewContentUx3H(draft with
            {
                BaseRevision = draft.BaseRevision.Next(),
                Approved = true,
            }).Succeeded;
            var approvedDraft = draft with { Provenance = "studio-evidence-approved", Approved = true };
            var approvedPreview = editing.PreviewContentUx3H(approvedDraft);
            var staleApproval = approvedPreview.CanApply &&
                                !editing.ApplyApprovedContentUx3H(approvedDraft with { BatchGlob = "*.yaml" }).Succeeded;
            approvedPreview = editing.PreviewContentUx3H(approvedDraft);

            var batchSource = ResolveConfinedUx3H(root, evidence.BatchSourceRelativePath, allowRoot: false);
            var batchPreview = editing.PreviewBatchUx3H(
                approvedDraft, batchSource, evidence.BatchDestinationRelativePath);
            var batchApplied = batchPreview.CanApply && editing.ApplyBatchUx3H(
                approvedDraft, batchSource, evidence.BatchDestinationRelativePath).Succeeded;
            var batchUndo = batchApplied && editing.UndoBatchUx3H().Succeeded;
            var batchRedo = batchUndo && editing.RedoBatchUx3H().Succeeded;

            var exportPreview = editing.PreviewBlueprintExportUx3H(
                [evidence.BlueprintId], "exports/ux3-h-evidence.orpblueprints", includePreviews: true);
            var exportApplied = exportPreview.CanApply && editing.ApplyBlueprintExportUx3H(
                [evidence.BlueprintId], "exports/ux3-h-evidence.orpblueprints", includePreviews: true).Succeeded;
            var exportUndo = exportApplied && editing.UndoBlueprintExportUx3H().Succeeded;
            var exportRedo = exportUndo && editing.RedoBlueprintExportUx3H().Succeeded;

            var applied = approvedPreview.CanApply && editing.ApplyApprovedContentUx3H(approvedDraft).Succeeded;
            var canonical = applied && editing._contentCanonicalCommandUx3H;
            var undo = applied && editing.UndoContentUx3H().Succeeded;
            var redo = undo && editing.RedoContentUx3H().Succeeded;
            var save = redo && editing.SaveContentUx3H().Succeeded;
            var reopened = save ? ProjectSession.Open(root) : null;
            var persisted = reopened is { Succeeded: true, Session: not null } &&
                            JsonSerializer.Serialize(reopened.Session.Project.Module.Content) ==
                            JsonSerializer.Serialize(editing.Project!.Module.Content);
            var neighborPreserved = NeighborFingerprintUx3H(editing.Project!) == initialNeighbors;
            var projectChangedOnlyInContent = initialProject != ProjectJsonSerializer.Serialize(editing.Project!) && neighborPreserved;
            var human = !editing.ListBlueprintsUx3H().Any(item => Regex.IsMatch(item.Label,
                "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
                RegexOptions.CultureInvariant));
            var sourcePreserved = ReferenceEquals(Project, sourceProject) && ReferenceEquals(History, sourceHistory) &&
                                  sourceHistory?.State.Revision == sourceRevision &&
                                  (sourceHistory?.Journal ?? []).SequenceEqual(sourceJournal) &&
                                  CaptureContentApprovalUx3H() == sourceApproval;
            var succeeded = batchConfiguration && batchPreview.Succeeded && batchApplied && batchUndo && batchRedo &&
                            exportPreview.Succeeded && exportApplied && exportUndo && exportRedo && textSources && dataTables &&
                            approvedPreview.DryRunSucceeded && staleSource && staleApproval && canonical && undo && redo &&
                            persisted && neighborPreserved && projectChangedOnlyInContent && sourcePreserved && human;
            return new(
                succeeded,
                ExplicitSelections: true,
                batchConfiguration,
                batchPreview.Succeeded,
                batchApplied && batchUndo && batchRedo,
                exportPreview.Succeeded,
                exportApplied && exportUndo && exportRedo,
                textSources,
                dataTables,
                canonical,
                staleSource,
                staleApproval,
                undo && redo,
                persisted,
                neighborPreserved,
                sourcePreserved,
                human,
                RuntimeClaimsBounded: true,
                TemporaryWorkspaceReleased: false);
        }
        finally
        {
            editing.Close();
            DeleteTemporaryUx3H(root);
            _lastUx3HTemporaryReleased = !Directory.Exists(root);
        }
    }

    internal string ContentDraftFingerprintUx3H(StudioUx3HContentDraft draft)
    {
        var configuration = BuildContentUx3H(draft);
        return HashBinaryUx3H(writer =>
        {
            writer.Write(draft.BaseRevision.Value);
            WriteStringUx3H(writer, draft.SourceProjectFingerprint);
            WriteStringUx3H(writer,
                JsonSerializer.Serialize(configuration));
            writer.Write(draft.Approved);
        });
    }

    internal static ContentPackageDefinition RebuildPackageUx3H(ContentPackageDefinition package)
    {
        var draft = new ContentPackageDefinition(
            package.Id, package.SchemaVersion, package.Provenance, package.PackageNamespace, package.Version,
            package.LicenseSpdx, new string('0', 64), package.Priority, package.SealedResources,
            package.Dependencies, package.Sources, package.Assets);
        return new ContentPackageDefinition(
            draft.Id, draft.SchemaVersion, draft.Provenance, draft.PackageNamespace, draft.Version,
            draft.LicenseSpdx, ContentPackageCompiler.ComputePackageHash(draft), draft.Priority,
            draft.SealedResources, draft.Dependencies, draft.Sources, draft.Assets);
    }

    private static ContentWorkspaceConfiguration BuildContentUx3H(StudioUx3HContentDraft draft) => new(
        draft.Id,
        new ContentBatchConfiguration(
            draft.BatchGlob,
            draft.BatchExtensions,
            draft.BatchCollisionPolicy,
            draft.BatchProvenance,
            draft.BatchLicenseSpdx),
        draft.DataTables.ToArray(),
        draft.MergePolicy,
        draft.Provenance,
        packages: ClonePackagesUx3H(draft.Packages));

    private static ContentPackageCatalog ClonePackagesUx3H(ContentPackageCatalog catalog) => new(
        catalog.Id,
        catalog.SchemaVersion,
        catalog.Provenance,
        catalog.ConflictPolicy,
        catalog.Packages.Select(RebuildPackageUx3H).ToArray());

    private static StudioUx3HContentDraft FreezeContentUx3H(StudioUx3HContentDraft draft)
    {
        var configuration = BuildContentUx3H(draft);
        return draft with
        {
            BatchExtensions = configuration.Batch.Extensions.ToArray(),
            Packages = configuration.Packages,
            DataTables = configuration.DataTables.ToArray(),
            Approved = true,
        };
    }

    private static ModuleDefinition CloneModuleWithContentUx3H(
        ModuleDefinition module,
        ContentWorkspaceConfiguration content) => new(
        module.Id,
        module.Slug,
        module.DisplayName,
        module.Description,
        module.EntryAreaId,
        module.Metadata,
        module.Variables,
        module.Distribution,
        module.Localization,
        content,
        module.ArtisticStyle,
        module.RuntimePolicies,
        module.Factions,
        module.WorldFeatures);

    private void CancelBatchApprovalUx3H()
    {
        _approvedBatchUx3H = null;
        _approvedBatchFingerprintUx3H = null;
    }

    private void CancelExportApprovalUx3H()
    {
        _approvedExportUx3H = null;
        _approvedExportFingerprintUx3H = null;
    }

    private static string BatchFingerprintUx3H(ContentBatchTransferRequest request, StudioUx3HContentDraft draft) =>
        HashBinaryUx3H(writer =>
        {
            WriteStringUx3H(writer, request.SourceRoot);
            WriteStringUx3H(writer, request.DestinationRoot);
            writer.Write((int)request.Operation);
            WriteStringUx3H(writer, request.Glob);
            foreach (var extension in request.Extensions) WriteStringUx3H(writer, extension);
            writer.Write((int)request.CollisionPolicy);
            WriteStringUx3H(writer, request.Provenance);
            WriteStringUx3H(writer, request.LicenseSpdx);
            WriteStringUx3H(writer, draft.SourceProjectFingerprint);
        });

    private static string ExportFingerprintUx3H(Ux3HExportPlan plan) => HashBinaryUx3H(writer =>
    {
        foreach (var id in plan.BlueprintIds) WriteStringUx3H(writer, id.ToString());
        WriteStringUx3H(writer, plan.OutputRelativePath);
        writer.Write(plan.IncludePreviews);
        WriteStringUx3H(writer, plan.ProjectFingerprint);
        WriteStringUx3H(writer, plan.ExpectedHash);
    });

    private static string ProjectFingerprintUx3H(ProjectDefinition project) =>
        HashBytesUx3H(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project)));

    private static string NeighborFingerprintUx3H(ProjectDefinition project) => HashBinaryUx3H(writer =>
    {
        WriteStringUx3H(writer, JsonSerializer.Serialize(project.Blueprints));
        WriteStringUx3H(writer, JsonSerializer.Serialize(project.Areas));
        WriteStringUx3H(writer, JsonSerializer.Serialize(project.Dialogues));
        WriteStringUx3H(writer, JsonSerializer.Serialize(project.Quests));
        WriteStringUx3H(writer, JsonSerializer.Serialize(project.Module.Metadata));
        WriteStringUx3H(writer, JsonSerializer.Serialize(project.Module.Localization));
        WriteStringUx3H(writer, JsonSerializer.Serialize(project.Module.ArtisticStyle));
        WriteStringUx3H(writer, JsonSerializer.Serialize(project.Module.RuntimePolicies));
        WriteStringUx3H(writer, JsonSerializer.Serialize(project.Module.Factions));
        WriteStringUx3H(writer, JsonSerializer.Serialize(project.Module.WorldFeatures));
    });

    private static StudioUx3HContentPreview FailedContentUx3H(string message, string code, string path) => new(
        false, false, false, false, false, true, ScrubHumanUx3H(message), "Aucun patch.", "Aucun inverse.", [],
        [new(code, path, ScrubHumanUx3H(message))], [], []);

    private static StudioUx3HBatchPreview FailedBatchUx3H(string message) => new(
        false, false, ScrubHumanUx3H(message), string.Empty, string.Empty, 0, [], [ScrubHumanUx3H(message)]);

    private static StudioUx3HExportPreview FailedExportUx3H(string message) => new(
        false, false, ScrubHumanUx3H(message), string.Empty, string.Empty, 0, 0, 0, 0, []);

    private static StudioUx3HSmokeResult EmptySmokeUx3H() => new(
        false, false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false);

    private static string NormalizeExportPathUx3H(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalized = path.Replace('\\', '/').Trim().TrimStart('/');
        if (Path.IsPathRooted(path) || normalized.Split('/').Any(segment => segment is "" or "." or "..") ||
            !normalized.EndsWith(".orpblueprints", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("La sortie doit être un chemin relatif sûr terminé par .orpblueprints.", nameof(path));
        return normalized;
    }

    private static string ResolveConfinedUx3H(string root, string relativePath, bool allowRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Path.IsPathRooted(relativePath)) throw new ArgumentException("Le chemin doit rester relatif au workspace.");
        var normalized = relativePath.Replace('\\', '/').Trim().TrimStart('/');
        if (normalized.Split('/').Any(segment => segment is "" or "." or ".."))
            throw new ArgumentException("Le chemin relatif contient un segment interdit.");
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(fullRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) || !allowRoot && candidate == fullRoot.TrimEnd(Path.DirectorySeparatorChar))
            throw new ArgumentException("Le chemin sort du workspace.");
        return candidate;
    }

    private static IReadOnlyList<string> ReadArchiveEntriesUx3H(string path)
    {
        using var archive = System.IO.Compression.ZipFile.OpenRead(path);
        return archive.Entries.Select(entry => HumanFileUx3H(entry.FullName)).Order(StringComparer.Ordinal).ToArray();
    }

    private static string HashFileUx3H(string path) => HashBytesUx3H(File.ReadAllBytes(path));

    private static string HashBytesUx3H(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string HashBinaryUx3H(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) write(writer);
        return HashBytesUx3H(stream.ToArray());
    }

    private static void WriteStringUx3H(BinaryWriter writer, string? value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static byte[] EvidenceSourceBytesUx3H() =>
        Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"creature\":\"ash-warden\",\"level\":12}\n");

    private static Guid StableGuidUx3H(string suffix)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:ux3-h:{suffix}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }

    internal static string ScrubHumanUx3H(string value) => Regex.Replace(
        value ?? string.Empty,
        "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
        "identifiant protégé",
        RegexOptions.CultureInvariant).Trim();

    internal static string HumanFileUx3H(string path)
    {
        var normalized = (path ?? string.Empty).Replace('\\', '/');
        var name = Path.GetFileName(normalized);
        return string.IsNullOrWhiteSpace(name) ? "fichier protégé" : ScrubHumanUx3H(name);
    }

    internal static string HumanBytesUx3H(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / (1024d * 1024d):0.##} Mio",
        >= 1024 => $"{bytes / 1024d:0.##} Kio",
        _ => $"{bytes} octet(s)",
    };

    internal static string HumanSourceKindUx3H(ContentSourceKind kind) => kind switch
    {
        ContentSourceKind.Json => "JSON",
        ContentSourceKind.Text => "Texte",
        ContentSourceKind.Yaml => "YAML",
        ContentSourceKind.ForgeScript => "ForgeScript",
        _ => "Format protégé",
    };

    internal static string HumanColumnKindUx3H(DataColumnKind kind) => kind switch
    {
        DataColumnKind.Text => "Texte",
        DataColumnKind.Integer => "Entier",
        DataColumnKind.Decimal => "Décimal",
        DataColumnKind.Boolean => "Booléen",
        DataColumnKind.Reference => "Référence",
        _ => "Type protégé",
    };

    internal static string HumanMergeUx3H(ContentMergePolicy policy) => policy switch
    {
        ContentMergePolicy.Manual => "Manuelle",
        ContentMergePolicy.PreferOurs => "Préférer notre version",
        ContentMergePolicy.PreferTheirs => "Préférer la version importée",
        ContentMergePolicy.PreserveBoth => "Conserver les deux",
        _ => "Politique protégée",
    };

    internal static string HumanCollisionUx3H(ContentBatchCollisionPolicy policy) => policy switch
    {
        ContentBatchCollisionPolicy.Fail => "Refuser",
        ContentBatchCollisionPolicy.Skip => "Ignorer le fichier entrant",
        ContentBatchCollisionPolicy.Replace => "Remplacer",
        ContentBatchCollisionPolicy.PreserveBoth => "Conserver les deux",
        _ => "Politique protégée",
    };

    internal static string HumanBatchOperationUx3H(ContentBatchOperation operation) => operation switch
    {
        ContentBatchOperation.Import => "Import de masse",
        ContentBatchOperation.Export => "Export de masse",
        _ => "Transfert de masse",
    };

    internal static string HumanBlueprintKindUx3H(BlueprintKind kind) => kind switch
    {
        BlueprintKind.Creature => "Créature",
        BlueprintKind.Placeable => "Objet plaçable",
        BlueprintKind.Item => "Objet d’inventaire",
        BlueprintKind.PlayerSpawn => "Départ joueur",
        _ => ScrubHumanUx3H(kind.ToString()),
    };

    private static string HumanBatchStatusUx3H(ContentBatchEntryStatus status, bool collision) => status switch
    {
        ContentBatchEntryStatus.Planned when collision => "collision traitée par la politique choisie",
        ContentBatchEntryStatus.Planned => "ajout planifié",
        ContentBatchEntryStatus.Skipped => "ignoré",
        ContentBatchEntryStatus.Failed => "refusé",
        ContentBatchEntryStatus.Added => "ajouté",
        ContentBatchEntryStatus.Replaced => "remplacé",
        ContentBatchEntryStatus.PreservedBoth => "variante conservée",
        _ => "état protégé",
    };

    private static string HumanPatchUx3H(string? summary, ContentWorkspaceConfiguration content) =>
        summary is null
            ? "Aucun patch."
            : $"Mettre à jour l’espace contenu : {content.DataTables.Count} table(s), " +
              $"{content.Packages.Packages.Count} package(s) et politique {HumanCollisionUx3H(content.Batch.CollisionPolicy)}.";

    private static string HumanInverseUx3H(string? summary, ContentWorkspaceConfiguration content) =>
        summary is null
            ? "Aucun inverse."
            : $"Restaurer {content.DataTables.Count} table(s) et {content.Packages.Packages.Count} package(s).";

    private static void DeleteTemporaryUx3H(string root)
    {
        try
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }

    private sealed record Ux3HFileBackup(string TargetPath, byte[]? OriginalBytes, string AppliedHash);
    private sealed record Ux3HBatchReceipt(ContentBatchTransferRequest Request, IReadOnlyList<Ux3HFileBackup> Backups);
    private sealed record Ux3HExportPlan(
        IReadOnlyList<BlueprintId> BlueprintIds,
        string OutputRelativePath,
        bool IncludePreviews,
        string ProjectFingerprint,
        string ExpectedHash);
    private sealed record Ux3HExportReceipt(
        Ux3HExportPlan Plan,
        string OutputPath,
        byte[]? OriginalBytes,
        string AppliedHash);
}
