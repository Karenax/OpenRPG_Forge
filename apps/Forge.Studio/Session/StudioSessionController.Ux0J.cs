using Forge.Application.Commands;
using Forge.Application.Creator;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Diagnostics;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SafeRenameHandler _safeRename = new(new SystemClock());
    private readonly ApplyDiagnosticQuickFixHandler _applyDiagnosticQuickFix = new(new SystemClock());
    private SafeRenamePreview? _approvedContentRename;
    private WorkspaceResourceRenamePreview? _approvedResourceRename;
    private CreatorSearchDocument? _approvedRenameDocument;
    private readonly Stack<WorkspaceResourceRenamePreview> _resourceRenameUndo = [];
    private readonly Stack<WorkspaceResourceRenamePreview> _resourceRenameRedo = [];
    private DiagnosticId? _approvedDiagnosticId;
    private DiagnosticQuickFixId? _approvedQuickFixId;
    private readonly List<CreatorDiagnostic> _studioOperationalDiagnostics = [];
    private readonly List<StudioStatusMessage> _studioStatusMessages = [];
    private readonly List<string> _studioSessionLog = [];

    internal bool CanUndoResourceRename => _resourceRenameUndo.Count > 0;

    internal bool CanRedoResourceRename => _resourceRenameRedo.Count > 0;

    internal StudioSafeRenameDraft? CreateSafeRenameDraft(CreatorSearchDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (Session is null || Project is null)
        {
            return null;
        }

        if (document.Kind is CreatorContentKind.Script or CreatorContentKind.Asset)
        {
            return new StudioSafeRenameDraft(
                document,
                SafeRenameKindLabel(document.Kind),
                document.DisplayName,
                document.DocumentPath,
                FindReferences(document.Kind, document.Id).Count,
                IsWorkspaceResource: true,
                HasEditableDisplayName: false,
                HasEditableAddress: true);
        }

        if (!SafeRenameHandler.IsSupported(document.Kind))
        {
            return null;
        }

        var preview = SafeRenamePreview.Create(
            Project,
            document.Kind,
            document.Id,
            document.DisplayName,
            document.Kind == CreatorContentKind.Project ? null : ResolveCurrentAddress(document));
        return preview is null
            ? null
            : new StudioSafeRenameDraft(
                document,
                SafeRenameKindLabel(document.Kind),
                preview.PreviousDisplayName,
                preview.PreviousSlug ?? string.Empty,
                preview.PreservedReferenceCount,
                IsWorkspaceResource: false,
                HasEditableDisplayName: true,
                HasEditableAddress: document.Kind is not CreatorContentKind.Project and not CreatorContentKind.Module);
    }

    internal StudioSafeRenamePreviewResult PreviewSafeRename(
        CreatorSearchDocument document,
        string displayName,
        string address)
    {
        ArgumentNullException.ThrowIfNull(document);
        ClearApprovedSafeRename();
        if (History is null || Session is null || Project is null)
        {
            return new StudioSafeRenamePreviewResult(false, "Aucun module n’est ouvert.", string.Empty, false);
        }

        if (document.Kind is CreatorContentKind.Script or CreatorContentKind.Asset)
        {
            try
            {
                var resourcePreview = WorkspaceResourceRenameService.Preview(
                    Session.RootDirectory,
                    document.DocumentPath,
                    address,
                    "studio:ux0-j-resource-rename");
                _approvedResourceRename = resourcePreview;
                _approvedRenameDocument = document;
                return new StudioSafeRenamePreviewResult(
                    true,
                    "Aperçu validé · aucun fichier n’a été déplacé.",
                    $"{resourcePreview.ReferenceEdits.Count} référence(s) seront migrées · identité conservée",
                    true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                                   ArgumentException or InvalidOperationException)
            {
                var message = HumanizeResourceRename(exception);
                AddOperationalDiagnostic(
                    "rename.resource_refused",
                    message,
                    document,
                    CreatorDiagnosticSeverity.Error,
                    "rename:resource");
                return new StudioSafeRenamePreviewResult(false, message, string.Empty, false);
            }
        }

        if (!SafeRenameHandler.IsSupported(document.Kind))
        {
            return new StudioSafeRenamePreviewResult(
                false,
                "Ce contenu se consulte ici, mais son nom dépend de son élément parent.",
                string.Empty,
                false);
        }

        var command = new SafeRenameCommand(
            CommandId.From(Guid.NewGuid()),
            History.State.Revision,
            document.Kind,
            document.Id,
            displayName,
            document.Kind == CreatorContentKind.Project ? null : address,
            DryRun: true);
        var result = _safeRename.Handle(History.State, command);
        if (!result.Succeeded)
        {
            var diagnostic = result.Diagnostics.FirstOrDefault();
            var message = HumanizeSafeRename(diagnostic?.Code);
            AddOperationalDiagnostic(
                diagnostic?.Code ?? "rename.refused",
                message,
                document,
                CreatorDiagnosticSeverity.Error,
                "rename:content");
            return new StudioSafeRenamePreviewResult(false, message, string.Empty, false);
        }

        var preview = SafeRenamePreview.Create(
            Project,
            document.Kind,
            document.Id,
            displayName,
            document.Kind == CreatorContentKind.Project ? null : address);
        if (preview is null)
        {
            return new StudioSafeRenamePreviewResult(
                false,
                "Le contenu a changé depuis son ouverture. Relancez la recherche.",
                string.Empty,
                false);
        }

        _approvedContentRename = preview;
        _approvedRenameDocument = document;
        var addressSummary = document.Kind == CreatorContentKind.Project
            ? "adresse interne inchangée"
            : $"adresse interne « {preview.NewSlug} »";
        return new StudioSafeRenamePreviewResult(
            true,
            "Aperçu validé · le module n’a pas été modifié.",
            $"Nom « {preview.NewDisplayName} » · {addressSummary} · " +
            $"{preview.PreservedReferenceCount} référence(s) conservée(s)",
            true);
    }

    internal StudioActionResult ApplyApprovedSafeRename()
    {
        if (History is null || Session is null || Project is null || _approvedRenameDocument is null)
        {
            return new StudioActionResult(false, "Prévisualisez d’abord le renommage.");
        }

        if (_approvedResourceRename is { } resourcePreview)
        {
            try
            {
                WorkspaceResourceRenameService.Apply(resourcePreview);
                _resourceRenameUndo.Push(resourcePreview);
                _resourceRenameRedo.Clear();
                InvalidateContentIndexes();
                ClearApprovedSafeRename();
                return new StudioActionResult(
                    true,
                    $"Ressource renommée · {resourcePreview.ReferenceEdits.Count} référence(s) migrée(s) · annulation disponible");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                                   ArgumentException or InvalidOperationException)
            {
                var message = HumanizeResourceRename(exception);
                AddOperationalDiagnostic(
                    "rename.resource_apply_refused",
                    message,
                    _approvedRenameDocument,
                    CreatorDiagnosticSeverity.Error,
                    "rename:resource");
                ClearApprovedSafeRename();
                return new StudioActionResult(false, message);
            }
        }

        if (_approvedContentRename is not { } contentPreview)
        {
            return new StudioActionResult(false, "Prévisualisez d’abord le renommage.");
        }

        var command = new SafeRenameCommand(
            CommandId.From(Guid.NewGuid()),
            History.State.Revision,
            contentPreview.TargetKind,
            contentPreview.TargetId,
            contentPreview.NewDisplayName,
            contentPreview.TargetKind == CreatorContentKind.Project ? null : contentPreview.NewSlug,
            DryRun: false);
        var dryRun = _safeRename.Handle(History.State, command with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            var message = HumanizeSafeRename(dryRun.Diagnostics.FirstOrDefault()?.Code);
            ClearApprovedSafeRename();
            return new StudioActionResult(false, message);
        }

        var applied = _safeRename.Handle(History.State, command);
        if (!applied.Succeeded)
        {
            var message = HumanizeSafeRename(applied.Diagnostics.FirstOrDefault()?.Code);
            ClearApprovedSafeRename();
            return new StudioActionResult(false, message);
        }

        History.Record(applied);
        InvalidateContentIndexes();
        ClearApprovedSafeRename();
        return new StudioActionResult(
            true,
            $"{SafeRenameKindLabel(contentPreview.TargetKind)} renommé · références conservées · Ctrl+Z pour annuler");
    }

    internal StudioActionResult UndoResourceRename()
    {
        if (!_resourceRenameUndo.TryPop(out var preview))
        {
            return new StudioActionResult(false, "Aucun renommage de ressource à annuler.");
        }

        try
        {
            WorkspaceResourceRenameService.Undo(preview);
            _resourceRenameRedo.Push(preview);
            InvalidateContentIndexes();
            return new StudioActionResult(true, "Ancien nom de ressource restauré · références restaurées.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                               ArgumentException or InvalidOperationException)
        {
            _resourceRenameUndo.Push(preview);
            return new StudioActionResult(false, HumanizeResourceRename(exception));
        }
    }

    internal StudioActionResult RedoResourceRename()
    {
        if (!_resourceRenameRedo.TryPop(out var preview))
        {
            return new StudioActionResult(false, "Aucun renommage de ressource à rétablir.");
        }

        try
        {
            WorkspaceResourceRenameService.Redo(preview);
            _resourceRenameUndo.Push(preview);
            InvalidateContentIndexes();
            return new StudioActionResult(true, "Nouveau nom de ressource rétabli · références migrées.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                               ArgumentException or InvalidOperationException)
        {
            _resourceRenameRedo.Push(preview);
            return new StudioActionResult(false, HumanizeResourceRename(exception));
        }
    }

    internal void CancelSafeRename() => ClearApprovedSafeRename();

    internal void RecordStudioStatus(string message, bool error)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var normalized = message.Trim();
        var correlation = History is null ? "Session" : $"Révision {History.State.Revision.Value}";
        _studioStatusMessages.Add(new StudioStatusMessage(normalized, error, correlation));
        _studioSessionLog.Add($"{(error ? "error" : "status")}: {normalized}");
        TrimToLast(_studioStatusMessages, 24);
        TrimToLast(_studioSessionLog, 120);
    }

    internal StudioDiagnosticOverview GetDiagnosticOverview(CreatorDiagnosticQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (Project is null || History is null)
        {
            return new StudioDiagnosticOverview([], 0, 0, 0, [new(null, "Toutes les opérations")], []);
        }

        var correlation = $"studio:revision:{History.State.Revision.Value}";
        var all = CreatorDiagnostics.FromProject(Project, correlation)
            .Concat(_studioOperationalDiagnostics)
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        var filtered = CreatorDiagnosticConsole.Filter(all, query);
        var correlations = all
            .Select(item => item.CorrelationId)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(item => new StudioDiagnosticCorrelation(item, HumanizeCorrelation(item!)))
            .Prepend(new StudioDiagnosticCorrelation(null, "Toutes les opérations"))
            .ToArray();
        return new StudioDiagnosticOverview(
            filtered,
            all.Count(item => item.Severity == CreatorDiagnosticSeverity.Error),
            all.Count(item => item.Severity == CreatorDiagnosticSeverity.Warning),
            all.Count(item => item.Severity == CreatorDiagnosticSeverity.Information),
            correlations,
            _studioStatusMessages.TakeLast(8).Reverse().ToArray());
    }

    internal StudioDiagnosticFixPreview PreviewDiagnosticQuickFix(
        CreatorDiagnostic diagnostic,
        DiagnosticQuickFixDefinition fix)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        ArgumentNullException.ThrowIfNull(fix);
        ClearApprovedDiagnosticFix();
        if (History is null)
        {
            return new StudioDiagnosticFixPreview(false, "Aucun module n’est ouvert.", string.Empty, false);
        }

        var command = new ApplyDiagnosticQuickFixCommand(
            CommandId.From(Guid.NewGuid()),
            History.State.Revision,
            diagnostic.DiagnosticId,
            fix.Id,
            DryRun: true);
        var result = _applyDiagnosticQuickFix.Handle(History.State, command);
        if (!result.Succeeded)
        {
            var message = HumanizeDiagnosticFix(result.Diagnostics.FirstOrDefault()?.Code);
            AddOperationalDiagnostic(
                result.Diagnostics.FirstOrDefault()?.Code ?? "diagnostic.quick_fix_refused",
                message,
                diagnostic.Target,
                diagnostic.DocumentPath,
                CreatorDiagnosticSeverity.Error,
                "diagnostic:quick-fix");
            return new StudioDiagnosticFixPreview(false, message, string.Empty, false);
        }

        _approvedDiagnosticId = diagnostic.DiagnosticId;
        _approvedQuickFixId = fix.Id;
        return new StudioDiagnosticFixPreview(
            true,
            "Aperçu validé · le module n’a pas été modifié.",
            $"Correction « {fix.Label} » · {fix.Description}",
            true);
    }

    internal StudioActionResult ApplyApprovedDiagnosticQuickFix()
    {
        if (History is null || _approvedDiagnosticId is null || _approvedQuickFixId is null)
        {
            return new StudioActionResult(false, "Prévisualisez d’abord la correction.");
        }

        var command = new ApplyDiagnosticQuickFixCommand(
            CommandId.From(Guid.NewGuid()),
            History.State.Revision,
            _approvedDiagnosticId,
            _approvedQuickFixId,
            DryRun: false);
        var preview = _applyDiagnosticQuickFix.Handle(History.State, command with { DryRun = true });
        if (!preview.Succeeded)
        {
            ClearApprovedDiagnosticFix();
            return new StudioActionResult(false, HumanizeDiagnosticFix(preview.Diagnostics.FirstOrDefault()?.Code));
        }

        var applied = _applyDiagnosticQuickFix.Handle(History.State, command);
        if (!applied.Succeeded)
        {
            ClearApprovedDiagnosticFix();
            return new StudioActionResult(false, HumanizeDiagnosticFix(applied.Diagnostics.FirstOrDefault()?.Code));
        }

        History.Record(applied);
        InvalidateContentIndexes();
        ClearApprovedDiagnosticFix();
        return new StudioActionResult(true, "Correction appliquée · Ctrl+Z pour annuler · Ctrl+S pour enregistrer.");
    }

    internal StudioDiagnosticBundleReport ExportDiagnosticBundle()
    {
        if (Session is null || Project is null || History is null)
        {
            return new StudioDiagnosticBundleReport(false, "Aucun module n’est ouvert.", string.Empty, 0, false,
                false);
        }

        try
        {
            var diagnostics = GetDiagnosticOverview(new CreatorDiagnosticQuery()).Diagnostics;
            var runtimeLog = string.Join('\n', _studioSessionLog);
            var result = CreatorDiagnosticBundleExporter.Export(Session.RootDirectory, diagnostics, runtimeLog);
            return new StudioDiagnosticBundleReport(
                true,
                result.Reused
                    ? "Bundle de diagnostic déjà à jour · aucune copie superflue créée."
                    : "Bundle de diagnostic exporté atomiquement dans le projet.",
                result.RelativePath,
                result.DiagnosticCount,
                result.IncludesRuntimeLog,
                result.Reused);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                               ArgumentException or InvalidOperationException)
        {
            return new StudioDiagnosticBundleReport(
                false,
                "Le bundle de diagnostic n’a pas pu être exporté : " + exception.Message,
                string.Empty,
                0,
                false,
                false);
        }
    }

    internal StudioUx0JSmokeResult RunUx0JContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux0j-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            Directory.CreateDirectory(tempRoot);
            var defaultDraft = StudioNewProjectDraft.CreateDefault(tempRoot);
            var draft = defaultDraft with
            {
                DraftId = Guid.Parse("d0250000-0000-4000-8000-000000000500"),
                Localizations = defaultDraft.Localizations.Select(item => item with
                {
                    Name = "Atelier de renommage",
                }).ToArray(),
            };
            var created = CreateNewProject(draft);
            if (!created.Succeeded || created.WorkspaceDirectory is null)
            {
                return FailedUx0JSmoke();
            }

            var scripts = Path.Combine(created.WorkspaceDirectory, "scripts");
            Directory.CreateDirectory(scripts);
            File.WriteAllText(Path.Combine(scripts, "ancien-script.forge"), "void main() { say(\"test\"); }");
            File.WriteAllText(
                Path.Combine(created.WorkspaceDirectory, "rename-reference.txt"),
                "scripts/ancien-script.forge\n");
            if (!editing.Open(created.WorkspaceDirectory).Succeeded)
            {
                return FailedUx0JSmoke();
            }

            var originalProject = editing.Project!;
            var originalArea = originalProject.Areas.First();
            var originalAreaId = originalArea.Id;
            var areaDocument = new CreatorSearchDocument(
                CreatorContentKind.Area,
                originalAreaId.ToString(),
                originalArea.DisplayName,
                $"areas/{originalAreaId}",
                originalArea.DisplayName,
                provenance: "studio:ux0-j-smoke");
            var dryRunPreview = editing.PreviewSafeRename(
                areaDocument,
                "Zone renommée",
                "zone-renommee");
            var dryRun = dryRunPreview.Succeeded && ReferenceEquals(originalProject, editing.Project);
            var contentApply = editing.ApplyApprovedSafeRename();
            var contentRename = contentApply.Succeeded &&
                                editing.Project!.Areas.First(item => item.Id == originalAreaId).DisplayName ==
                                "Zone renommée" &&
                                editing.Project.Areas.First(item => item.Id == originalAreaId).Slug == "zone-renommee";
            var stableIdentity = editing.Project?.Areas.Any(item => item.Id == originalAreaId) == true;
            var undoContent = editing.Undo();
            var undoRestored = editing.Project?.Areas.First(item => item.Id == originalAreaId).DisplayName ==
                               originalArea.DisplayName;
            var redoContent = editing.Redo();
            var redoRestored = editing.Project?.Areas.First(item => item.Id == originalAreaId).DisplayName ==
                               "Zone renommée";

            var invalidPreview = editing.PreviewSafeRename(
                areaDocument,
                "Nom refusé",
                "Adresse Interne Invalide");
            var invalidRenameBlocked = !invalidPreview.Succeeded && !invalidPreview.CanApply;

            var resourceDocument = new CreatorSearchDocument(
                CreatorContentKind.Script,
                "scripts/ancien-script.forge",
                "Ancien script",
                "scripts/ancien-script.forge",
                "ancien script",
                provenance: "studio:ux0-j-smoke");
            var resourceIdentity = WorkspaceResourceIdentityStore.Resolve(
                created.WorkspaceDirectory,
                resourceDocument.DocumentPath);
            var resourcePreview = editing.PreviewSafeRename(
                resourceDocument,
                resourceDocument.DisplayName,
                "scripts/nouveau-script.forge");
            var resourceApply = editing.ApplyApprovedSafeRename();
            var referenceMigrated = File.ReadAllText(Path.Combine(
                created.WorkspaceDirectory,
                "rename-reference.txt")).Contains("scripts/nouveau-script.forge", StringComparison.Ordinal);
            var resourceIdentityPreserved = WorkspaceResourceIdentityStore.Resolve(
                created.WorkspaceDirectory,
                "scripts/nouveau-script.forge") == resourceIdentity;
            var resourceUndo = editing.UndoResourceRename();
            var resourceRedo = editing.RedoResourceRename();
            var resourceRename = resourcePreview.Succeeded && resourceApply.Succeeded && resourceUndo.Succeeded &&
                                 resourceRedo.Succeeded && File.Exists(Path.Combine(
                                     created.WorkspaceDirectory,
                                     "scripts",
                                     "nouveau-script.forge"));

            var saved = editing.Save();
            editing.Close();
            var opened = reopened.Open(created.WorkspaceDirectory);
            var persistenceRoundTrip = saved.Succeeded && opened.Succeeded &&
                                       reopened.Project?.Areas.First(item => item.Id == originalAreaId).DisplayName ==
                                       "Zone renommée" &&
                                       reopened.Project.Areas.First(item => item.Id == originalAreaId).Slug ==
                                       "zone-renommee";

            var quickFixPreview = false;
            var quickFixUndo = false;
            var diagnosticsAvailable = false;
            var deepLink = false;
            var diagnosticExport = false;
            if (opened.Succeeded && reopened.Project?.Areas.FirstOrDefault() is { } area &&
                area.Entities.FirstOrDefault() is { } entity)
            {
                var outside = new EntityInstance(
                    entity.Id,
                    entity.BlueprintId,
                    new Forge.Domain.Spatial.TransformValue(
                        new Forge.Domain.Spatial.Vector3Value(area.Dimensions.Width * 2, entity.Transform.Position.Y,
                            area.Dimensions.Depth * 2),
                        entity.Transform.Rotation,
                        entity.Transform.Scale),
                    entity.Tags,
                    entity.Overrides,
                    entity.BlueprintVersionRequirement);
                var invalidArea = area.WithEntities(area.Entities.Select(candidate => candidate.Id == entity.Id
                    ? outside
                    : candidate).ToArray());
                var invalidProject = new ProjectDefinition(
                    reopened.Project.Id,
                    reopened.Project.Title,
                    reopened.Project.Module,
                    reopened.Project.Blueprints,
                    reopened.Project.Areas.Select(candidate => candidate.Id == area.Id ? invalidArea : candidate)
                        .ToArray(),
                    reopened.Project.Dialogues,
                    reopened.Project.Quests);
                reopened.History = new ProjectCommandHistory(new ProjectState(
                    invalidProject,
                    reopened.History!.State.Revision));

                var overview = reopened.GetDiagnosticOverview(new CreatorDiagnosticQuery());
                var actionable = overview.Diagnostics.FirstOrDefault(item => item.QuickFixes.Count > 0);
                diagnosticsAvailable = overview.Diagnostics.Count > 0 && overview.Errors + overview.Warnings > 0;
                deepLink = actionable?.Target is { Kind: CreatorSelectionKind.Entity };
                reopened._studioSessionLog.Add("runtime ready");
                reopened._studioSessionLog.Add("token=ux0-j-secret");
                reopened._studioSessionLog.Add("playerId=UX0-J-PLAYER");
                var firstBundle = reopened.ExportDiagnosticBundle();
                var secondBundle = reopened.ExportDiagnosticBundle();
                diagnosticExport = firstBundle.Succeeded && secondBundle.Succeeded && secondBundle.Reused &&
                                   firstBundle.IncludesSessionLog && firstBundle.DiagnosticCount > 0;
                if (actionable is not null)
                {
                    var fix = actionable.QuickFixes[0];
                    var preview = reopened.PreviewDiagnosticQuickFix(actionable, fix);
                    quickFixPreview = preview.Succeeded && preview.CanApply &&
                                      ReferenceEquals(invalidProject, reopened.Project);
                    var applied = reopened.ApplyApprovedDiagnosticQuickFix();
                    var undo = reopened.Undo();
                    var redo = reopened.Redo();
                    quickFixUndo = applied.Succeeded && undo.Succeeded && redo.Succeeded;
                }
            }

            var atomicUndoRedo = undoContent.Succeeded && undoRestored && redoContent.Succeeded && redoRestored &&
                                 resourceUndo.Succeeded && resourceRedo.Succeeded && quickFixUndo;
            var humanDiagnostics = invalidRenameBlocked &&
                                   !invalidPreview.Message.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                                   !invalidPreview.Message.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
                                   !invalidPreview.Message.Contains("revision", StringComparison.OrdinalIgnoreCase);
            var succeeded = contentRename && resourceRename && stableIdentity && resourceIdentityPreserved &&
                            referenceMigrated && dryRun && atomicUndoRedo && persistenceRoundTrip &&
                            invalidRenameBlocked && diagnosticsAvailable && deepLink && quickFixPreview &&
                            quickFixUndo && diagnosticExport && humanDiagnostics;
            return new StudioUx0JSmokeResult(
                succeeded,
                contentRename,
                resourceRename,
                stableIdentity && resourceIdentityPreserved,
                referenceMigrated,
                dryRun,
                atomicUndoRedo,
                persistenceRoundTrip,
                invalidRenameBlocked,
                diagnosticsAvailable,
                deepLink,
                quickFixPreview,
                quickFixUndo,
                diagnosticExport,
                humanDiagnostics);
        }
        finally
        {
            editing.Close();
            reopened.Close();
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private string ResolveCurrentAddress(CreatorSearchDocument document)
    {
        var project = Project!;
        return document.Kind switch
        {
            CreatorContentKind.Module => project.Module.Slug,
            CreatorContentKind.Area => project.Areas.First(item => item.Id.ToString() == document.Id).Slug,
            CreatorContentKind.Blueprint => project.Blueprints.First(item => item.Id.ToString() == document.Id).Slug,
            CreatorContentKind.Dialogue => project.Dialogues.First(item => item.Id.ToString() == document.Id).Slug,
            CreatorContentKind.Quest => project.Quests.First(item => item.Id.ToString() == document.Id).Slug,
            _ => string.Empty,
        };
    }

    private void AddOperationalDiagnostic(
        string code,
        string message,
        CreatorSearchDocument document,
        CreatorDiagnosticSeverity severity,
        string correlationId) => AddOperationalDiagnostic(
        code,
        message,
        document.Selection,
        document.DocumentPath,
        severity,
        correlationId);

    private void AddOperationalDiagnostic(
        string code,
        string message,
        CreatorSelection? target,
        string documentPath,
        CreatorDiagnosticSeverity severity,
        string correlationId)
    {
        var diagnostic = CreatorDiagnostic.Create(
            severity,
            code,
            message,
            string.IsNullOrWhiteSpace(documentPath) ? "studio/operation" : documentPath,
            correlationId,
            target);
        _studioOperationalDiagnostics.RemoveAll(item => item.Id == diagnostic.Id);
        _studioOperationalDiagnostics.Add(diagnostic);
        TrimToLast(_studioOperationalDiagnostics, 64);
    }

    private void InvalidateContentIndexes()
    {
        _workspaceResources = null;
        _resourceCatalogProject = null;
        _resourceCatalog = null;
    }

    private void ClearApprovedSafeRename()
    {
        _approvedContentRename = null;
        _approvedResourceRename = null;
        _approvedRenameDocument = null;
    }

    private void ClearApprovedDiagnosticFix()
    {
        _approvedDiagnosticId = null;
        _approvedQuickFixId = null;
    }

    private void CloseUx0J()
    {
        ClearApprovedSafeRename();
        ClearApprovedDiagnosticFix();
        _resourceRenameUndo.Clear();
        _resourceRenameRedo.Clear();
        _studioOperationalDiagnostics.Clear();
        _studioStatusMessages.Clear();
        _studioSessionLog.Clear();
    }

    private static void TrimToLast<T>(List<T> values, int maximum)
    {
        if (values.Count > maximum)
        {
            values.RemoveRange(0, values.Count - maximum);
        }
    }

    private static string SafeRenameKindLabel(CreatorContentKind kind) => kind switch
    {
        CreatorContentKind.Project => "Projet",
        CreatorContentKind.Module => "Module",
        CreatorContentKind.Area => "Zone",
        CreatorContentKind.Blueprint => "Blueprint",
        CreatorContentKind.Dialogue => "Dialogue",
        CreatorContentKind.Quest => "Quête",
        CreatorContentKind.Script => "Script",
        CreatorContentKind.Asset => "Asset",
        _ => "Contenu",
    };

    private static string HumanizeSafeRename(string? code) => code switch
    {
        SafeRenameHandler.NameRequiredCode => "Donnez un nom visible avant de prévisualiser.",
        SafeRenameHandler.SlugRequiredCode => "L’adresse interne ne peut pas être vide.",
        SafeRenameHandler.InvalidSlugCode =>
            "L’adresse interne doit utiliser des lettres minuscules, des chiffres et des tirets.",
        SafeRenameHandler.DuplicateSlugCode => "Cette adresse interne est déjà utilisée par un autre contenu.",
        SafeRenameHandler.MissingTargetCode => "Le contenu a changé depuis son ouverture. Relancez la recherche.",
        SafeRenameHandler.RevisionConflictCode =>
            "Le module a changé depuis l’aperçu. Relancez le renommage pour éviter d’écraser une modification.",
        _ => "Ce contenu ne peut pas être renommé en sécurité avec les valeurs proposées.",
    };

    private static string HumanizeResourceRename(Exception exception) => exception switch
    {
        FileNotFoundException => "La ressource a été déplacée depuis l’ouverture. Relancez la recherche.",
        UnauthorizedAccessException => "Le projet ne permet pas de déplacer cette ressource.",
        IOException when exception.Message.Contains("destination", StringComparison.OrdinalIgnoreCase) =>
            "Une ressource porte déjà ce nom à cet emplacement.",
        InvalidOperationException when exception.Message.Contains("changed", StringComparison.OrdinalIgnoreCase) =>
            "La ressource ou une référence a changé depuis l’aperçu. Rien n’a été écrasé.",
        ArgumentException =>
            "Choisissez un chemin sous Scripts ou Assets, sans sortir du projet et sans changer l’extension.",
        _ => "Le renommage de la ressource a été annulé sans modifier le projet.",
    };

    private static string HumanizeDiagnosticFix(string? code) => code switch
    {
        ApplyDiagnosticQuickFixHandler.MissingDiagnosticCode =>
            "Ce problème n’existe plus dans la version courante. Relancez la validation.",
        ApplyDiagnosticQuickFixHandler.MissingQuickFixCode =>
            "Cette correction n’est plus disponible. Relancez la validation.",
        ApplyDiagnosticQuickFixHandler.MissingTargetCode =>
            "La cible de la correction a changé. Aucune modification n’a été appliquée.",
        ApplyDiagnosticQuickFixHandler.RevisionConflictCode =>
            "Le module a changé depuis l’aperçu. Prévisualisez de nouveau la correction.",
        _ => "La correction ne peut pas être appliquée dans l’état courant du module.",
    };

    private static string HumanizeCorrelation(string correlationId)
    {
        if (correlationId.StartsWith("studio:revision:", StringComparison.OrdinalIgnoreCase))
        {
            return "Validation du module courant";
        }
        if (correlationId.StartsWith("rename:", StringComparison.OrdinalIgnoreCase))
        {
            return "Renommage récent";
        }
        if (correlationId.StartsWith("diagnostic:", StringComparison.OrdinalIgnoreCase))
        {
            return "Correction récente";
        }
        return "Opération Studio";
    }

    private static StudioUx0JSmokeResult FailedUx0JSmoke() => new(
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false);
}
