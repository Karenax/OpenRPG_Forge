using System.Security.Cryptography;
using System.Text;
using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Lifecycle;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2LwBProvenance = "studio:ux2-lw-b:area-lifecycle";
    private readonly SetAreaFoundationHandler _setAreaFoundationUx2LwB = new(new SystemClock());
    private StudioUx2LwBDraft? _approvedAreaLifecycleUx2LwB;
    private ProjectRevision? _approvedAreaLifecycleRevisionUx2LwB;
    private string? _approvedAreaLifecycleSourceFingerprintUx2LwB;
    private string? _approvedAreaLifecycleDraftFingerprintUx2LwB;

    internal IReadOnlyList<StudioUx2LwBAreaChoice> AreaLifecycleChoicesUx2LwB
    {
        get
        {
            var project = Project;
            if (project is null)
            {
                return [];
            }

            var evidenceTarget = ActiveAreaId ?? project.Areas.FirstOrDefault()?.Id;
            var duplicateNames = project.Areas
                .GroupBy(area => area.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.CurrentCultureIgnoreCase);
            return project.Areas.Select(area =>
                {
                    var readableName = HumanAreaNameUx2LwB(area);
                    var label = duplicateNames[area.DisplayName] > 1
                        ? $"{readableName} · {HumanSlugUx2LwB(area.Slug)}"
                        : readableName;
                    var entered = area.Foundation.Lifecycle.Entered is null ? "entrée sans script" : "entrée reliée";
                    var exited = area.Foundation.Lifecycle.Exited is null ? "sortie sans script" : "sortie reliée";
                    return new StudioUx2LwBAreaChoice(
                        area.Id,
                        $"{label} · {entered} · {exited}",
                        area.Id == evidenceTarget);
                })
                .OrderBy(choice => choice.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal StudioUx2LwBDraft? CreateAreaLifecycleDraftUx2LwB(AreaId? requestedAreaId = null)
    {
        var history = History;
        var project = Project;
        if (history is null || project is null || requestedAreaId is null)
        {
            return null;
        }

        var area = project.Areas.SingleOrDefault(item => item.Id == requestedAreaId);
        if (area is null)
        {
            return null;
        }

        return new StudioUx2LwBDraft(
            area.Id,
            history.State.Revision,
            AreaLifecycleSourceFingerprintUx2LwB(area),
            HumanAreaNameUx2LwB(area),
            CreateBindingDraftUx2LwB(area, area.Foundation.Lifecycle.Entered, "area.entered"),
            CreateBindingDraftUx2LwB(area, area.Foundation.Lifecycle.Exited, "area.exited"));
    }

    internal StudioUx2LwBPreview PreviewAreaLifecycleUx2LwB(StudioUx2LwBDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelAreaLifecyclePreviewUx2LwB();
        var history = History;
        var project = Project;
        if (history is null || project is null)
        {
            return FailedAreaLifecyclePreviewUx2LwB("Aucun module n’est ouvert.");
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            return FailedAreaLifecyclePreviewUx2LwB(
                "Le projet a changé depuis l’ouverture de cette fiche. Rechargez la zone avant de prévisualiser.");
        }

        var area = project.Areas.SingleOrDefault(item => item.Id == draft.AreaId);
        if (area is null)
        {
            return FailedAreaLifecyclePreviewUx2LwB("La zone sélectionnée n’existe plus.");
        }
        if (!string.Equals(
                draft.SourceFingerprint,
                AreaLifecycleSourceFingerprintUx2LwB(area),
                StringComparison.Ordinal))
        {
            return FailedAreaLifecyclePreviewUx2LwB(
                "La fiche de la zone a changé. Rechargez-la avant de prévisualiser.");
        }
        if (!TryBuildAreaLifecycleCandidateUx2LwB(
                draft,
                area,
                out var foundation,
                out var candidateArea,
                out var diagnostics))
        {
            return new StudioUx2LwBPreview(
                false,
                diagnostics.FirstOrDefault()?.Message ?? "La fiche contient une valeur invalide.",
                false,
                new StudioUx2LwBSemanticDiff([]),
                diagnostics,
                [],
                string.Empty);
        }

        var command = new SetAreaFoundationCommand(
            CommandId.From(Guid.NewGuid()),
            history.State.Revision,
            area.Id,
            foundation!,
            DryRun: true);
        var dryRun = _setAreaFoundationUx2LwB.Handle(history.State, command);
        if (!dryRun.Succeeded)
        {
            var commandDiagnostics = dryRun.Diagnostics.Select(item => new StudioUx2LwBDiagnostic(
                item.Code,
                $"areas/{area.Id}/foundation/lifecycle",
                HumanizeAreaLifecycleDiagnosticUx2LwB(item.Code))).ToArray();
            return new StudioUx2LwBPreview(
                false,
                commandDiagnostics.FirstOrDefault()?.Message ?? "La commande canonique a refusé l’aperçu.",
                false,
                BuildAreaLifecycleDiffUx2LwB(area.Foundation.Lifecycle, foundation!.Lifecycle),
                commandDiagnostics,
                [],
                string.Empty);
        }

        var diff = BuildAreaLifecycleDiffUx2LwB(area.Foundation.Lifecycle, foundation!.Lifecycle);
        var candidateProject = ReplaceAreaUx2LwB(project, candidateArea!);
        var observations = ObserveAreaLifecycleUx2LwB(candidateProject, candidateArea!);
        if (diff.HasChanges)
        {
            _approvedAreaLifecycleUx2LwB = draft;
            _approvedAreaLifecycleRevisionUx2LwB = history.State.Revision;
            _approvedAreaLifecycleSourceFingerprintUx2LwB = draft.SourceFingerprint;
            _approvedAreaLifecycleDraftFingerprintUx2LwB = AreaLifecycleDraftFingerprintUx2LwB(draft);
        }
        return new StudioUx2LwBPreview(
            true,
            diff.HasChanges
                ? "Aperçu validé · aucun document, script, nœud moteur ni état réseau n’a été modifié."
                : "Aperçu validé · aucune différence sémantique à appliquer.",
            diff.HasChanges,
            diff,
            [],
            observations,
            dryRun.Patch is null ? string.Empty : "Zone sélectionnée / Fondation / Entrée et sortie");
    }

    internal StudioUx2LwBActionResult ApplyApprovedAreaLifecycleUx2LwB()
    {
        var history = History;
        var project = Project;
        var draft = _approvedAreaLifecycleUx2LwB;
        if (history is null || project is null || draft is null ||
            _approvedAreaLifecycleRevisionUx2LwB != history.State.Revision ||
            !string.Equals(
                _approvedAreaLifecycleSourceFingerprintUx2LwB,
                draft.SourceFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                _approvedAreaLifecycleDraftFingerprintUx2LwB,
                AreaLifecycleDraftFingerprintUx2LwB(draft),
                StringComparison.Ordinal))
        {
            CancelAreaLifecyclePreviewUx2LwB();
            return new StudioUx2LwBActionResult(
                false,
                "Prévisualisez à nouveau cette fiche avant de l’appliquer.");
        }

        var area = project.Areas.SingleOrDefault(item => item.Id == draft.AreaId);
        if (area is null || !string.Equals(
                draft.SourceFingerprint,
                AreaLifecycleSourceFingerprintUx2LwB(area),
                StringComparison.Ordinal))
        {
            CancelAreaLifecyclePreviewUx2LwB();
            return new StudioUx2LwBActionResult(
                false,
                "La zone a changé depuis l’aperçu. Rechargez puis prévisualisez à nouveau.");
        }
        if (!TryBuildAreaLifecycleCandidateUx2LwB(
                draft,
                area,
                out var foundation,
                out _,
                out var diagnostics))
        {
            CancelAreaLifecyclePreviewUx2LwB();
            return new StudioUx2LwBActionResult(
                false,
                diagnostics.FirstOrDefault()?.Message ?? "La fiche n’est plus valide.");
        }

        var command = new SetAreaFoundationCommand(
            CommandId.From(Guid.NewGuid()),
            history.State.Revision,
            area.Id,
            foundation!,
            DryRun: false);
        var applied = _setAreaFoundationUx2LwB.Handle(history.State, command);
        if (!applied.Succeeded)
        {
            CancelAreaLifecyclePreviewUx2LwB();
            return new StudioUx2LwBActionResult(
                false,
                HumanizeAreaLifecycleDiagnosticUx2LwB(applied.Diagnostics.FirstOrDefault()?.Code));
        }

        history.Record(applied);
        CancelAreaLifecyclePreviewUx2LwB();
        return new StudioUx2LwBActionResult(
            true,
            "Entrée et sortie appliquées · Ctrl+Z pour annuler · Ctrl+S pour enregistrer.");
    }

    internal void CancelAreaLifecyclePreviewUx2LwB()
    {
        _approvedAreaLifecycleUx2LwB = null;
        _approvedAreaLifecycleRevisionUx2LwB = null;
        _approvedAreaLifecycleSourceFingerprintUx2LwB = null;
        _approvedAreaLifecycleDraftFingerprintUx2LwB = null;
    }

    internal void CloseUx2LwB() => CancelAreaLifecyclePreviewUx2LwB();

    internal StudioUx2LwBApprovalSnapshot CaptureAreaLifecycleApprovalUx2LwB() => new(
        _approvedAreaLifecycleUx2LwB,
        _approvedAreaLifecycleRevisionUx2LwB,
        _approvedAreaLifecycleSourceFingerprintUx2LwB,
        _approvedAreaLifecycleDraftFingerprintUx2LwB);

    internal void RestoreAreaLifecycleApprovalUx2LwB(StudioUx2LwBApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedAreaLifecycleUx2LwB = snapshot.Draft;
        _approvedAreaLifecycleRevisionUx2LwB = snapshot.Revision;
        _approvedAreaLifecycleSourceFingerprintUx2LwB = snapshot.SourceFingerprint;
        _approvedAreaLifecycleDraftFingerprintUx2LwB = snapshot.DraftFingerprint;
    }

    private bool TryBuildAreaLifecycleCandidateUx2LwB(
        StudioUx2LwBDraft draft,
        AreaDefinition sourceArea,
        out AreaFoundationProfile? foundation,
        out AreaDefinition? candidateArea,
        out IReadOnlyList<StudioUx2LwBDiagnostic> diagnostics)
    {
        var problems = new List<StudioUx2LwBDiagnostic>();
        var scripts = FindForgeScripts().Select(item => item.RelativePath)
            .ToHashSet(StringComparer.Ordinal);
        var entered = BuildAreaLifecycleBindingUx2LwB(
            draft.Entered,
            sourceArea.Foundation.Lifecycle.Entered,
            "area.entered",
            scripts,
            problems);
        var exited = BuildAreaLifecycleBindingUx2LwB(
            draft.Exited,
            sourceArea.Foundation.Lifecycle.Exited,
            "area.exited",
            scripts,
            problems);
        if (problems.Count > 0)
        {
            foundation = null;
            candidateArea = null;
            diagnostics = problems;
            return false;
        }

        var lifecycle = new AreaLifecycleBindingProfile(entered, exited);
        var source = sourceArea.Foundation;
        if (ReferenceEquals(entered, source.Lifecycle.Entered) &&
            ReferenceEquals(exited, source.Lifecycle.Exited))
        {
            foundation = source;
            candidateArea = sourceArea;
            diagnostics = [];
            return true;
        }

        foundation = new AreaFoundationProfile(
            source.Id,
            source.SchemaVersion,
            Ux2LwBProvenance,
            source.Creation,
            source.TileKit,
            lifecycle,
            source.Volumes,
            source.Dependencies,
            source.RequireTileEditingRuntimeMetadata);
        candidateArea = sourceArea.WithFoundation(foundation);
        diagnostics = [];
        return true;
    }

    private static AreaLifecycleScriptBinding? BuildAreaLifecycleBindingUx2LwB(
        StudioUx2LwBBindingDraft draft,
        AreaLifecycleScriptBinding? source,
        string expectedEvent,
        IReadOnlySet<string> realScripts,
        ICollection<StudioUx2LwBDiagnostic> diagnostics)
    {
        var path = $"foundation/lifecycle/{(expectedEvent == "area.entered" ? "entered" : "exited")}";
        if (!draft.Enabled)
        {
            if (draft.Required)
            {
                diagnostics.Add(new StudioUx2LwBDiagnostic(
                    "studio.area_lifecycle.required_disabled",
                    path,
                    "Activez cet événement avant de le rendre obligatoire."));
            }
            return null;
        }

        var scriptPath = NormalizeAreaLifecycleScriptPathUx2LwB(draft.ScriptPath);
        if (!string.Equals(draft.EventName, expectedEvent, StringComparison.Ordinal))
        {
            diagnostics.Add(new StudioUx2LwBDiagnostic(
                "studio.area_lifecycle.event_contract",
                path,
                "Rechargez la fiche : le contrat d’événement ne correspond plus à cette carte."));
        }
        if (draft.BindingId == Guid.Empty || string.IsNullOrWhiteSpace(draft.ForgeScriptApiVersion))
        {
            diagnostics.Add(new StudioUx2LwBDiagnostic(
                "studio.area_lifecycle.binding_contract",
                path,
                "Rechargez la fiche : l’identité ou la version du contrat est invalide."));
        }
        if (draft.Required && scriptPath is null)
        {
            diagnostics.Add(new StudioUx2LwBDiagnostic(
                "studio.area_lifecycle.script_required",
                path,
                "Choisissez un script réel du projet avant de rendre cet événement obligatoire."));
        }
        if (scriptPath is not null && !realScripts.Contains(scriptPath))
        {
            diagnostics.Add(new StudioUx2LwBDiagnostic(
                "studio.area_lifecycle.script_missing",
                path,
                "Le script choisi n’est plus disponible dans le projet. Choisissez une source réelle ou retirez le lien."));
        }
        if (diagnostics.Any(item => string.Equals(item.Path, path, StringComparison.Ordinal)))
        {
            return null;
        }

        if (source is not null &&
            source.Id == draft.BindingId &&
            string.Equals(source.EventName, expectedEvent, StringComparison.Ordinal) &&
            string.Equals(NormalizeAreaLifecycleScriptPathUx2LwB(source.ScriptPath), scriptPath,
                StringComparison.Ordinal) &&
            string.Equals(source.ForgeScriptApiVersion, draft.ForgeScriptApiVersion,
                StringComparison.Ordinal) &&
            source.Required == draft.Required)
        {
            return source;
        }

        return new AreaLifecycleScriptBinding(
            draft.BindingId,
            expectedEvent,
            scriptPath ?? string.Empty,
            draft.ForgeScriptApiVersion.Trim(),
            draft.Required,
            Ux2LwBProvenance);
    }

    private static StudioUx2LwBBindingDraft CreateBindingDraftUx2LwB(
        AreaDefinition area,
        AreaLifecycleScriptBinding? binding,
        string eventName)
    {
        if (binding is not null)
        {
            return new StudioUx2LwBBindingDraft(
                true,
                binding.Id,
                eventName,
                binding.ForgeScriptApiVersion,
                NormalizeAreaLifecycleScriptPathUx2LwB(binding.ScriptPath),
                binding.Required,
                binding.Provenance);
        }

        return new StudioUx2LwBBindingDraft(
            false,
            StableAreaLifecycleBindingIdUx2LwB(area.Id, eventName),
            eventName,
            RuntimeLifecycleEvent.CurrentForgeScriptApiVersion,
            null,
            false,
            Ux2LwBProvenance);
    }

    private static StudioUx2LwBSemanticDiff BuildAreaLifecycleDiffUx2LwB(
        AreaLifecycleBindingProfile source,
        AreaLifecycleBindingProfile candidate)
    {
        var changes = new List<string>();
        DescribeAreaLifecycleBindingChangeUx2LwB("Entrée", source.Entered, candidate.Entered, changes);
        DescribeAreaLifecycleBindingChangeUx2LwB("Sortie", source.Exited, candidate.Exited, changes);
        return new StudioUx2LwBSemanticDiff(changes);
    }

    private static void DescribeAreaLifecycleBindingChangeUx2LwB(
        string label,
        AreaLifecycleScriptBinding? source,
        AreaLifecycleScriptBinding? candidate,
        ICollection<string> changes)
    {
        if (source is null && candidate is null)
        {
            return;
        }
        if (source is null)
        {
            changes.Add(candidate!.ScriptPath.Length == 0
                ? $"{label} · observation typée activée, sans script"
                : $"{label} · reliée à « {HumanAreaLifecycleScriptNameUx2LwB(candidate.ScriptPath)} »" +
                  (candidate.Required ? " · obligatoire" : " · optionnelle"));
            return;
        }
        if (candidate is null)
        {
            changes.Add($"{label} · lien retiré ; l’événement typé reste émis par le runtime");
            return;
        }
        if (!string.Equals(source.ScriptPath, candidate.ScriptPath, StringComparison.Ordinal))
        {
            changes.Add(candidate.ScriptPath.Length == 0
                ? $"{label} · script retiré, observation typée conservée"
                : $"{label} · source réelle « {HumanAreaLifecycleScriptNameUx2LwB(candidate.ScriptPath)} »");
        }
        if (source.Required != candidate.Required)
        {
            changes.Add($"{label} · script {(candidate.Required ? "requis" : "optionnel")}");
        }
        if (!string.Equals(source.ForgeScriptApiVersion, candidate.ForgeScriptApiVersion,
                StringComparison.Ordinal))
        {
            changes.Add($"{label} · contrat ForgeScript {candidate.ForgeScriptApiVersion}");
        }
    }

    private static IReadOnlyList<StudioUx2LwBEventObservation> ObserveAreaLifecycleUx2LwB(
        ProjectDefinition project,
        AreaDefinition target)
    {
        var observations = new List<StudioUx2LwBEventObservation>();
        RuntimeLifecycleEvent? entered = null;
        RuntimeLifecycleEvent? exited = null;
        try
        {
            var enterCoordinator = new RuntimeLifecycleCoordinator();
            var loaded = enterCoordinator.LoadProject(
                project,
                RuntimeAuthority.Editor,
                RuntimeLifecycleCause.Playtest);
            if (loaded.Succeeded)
            {
                entered = enterCoordinator.EnterInitialArea(
                    target.Id,
                    null,
                    RuntimeLifecycleCause.Playtest,
                    dryRun: true).Events.SingleOrDefault(item =>
                    item.Kind == RuntimeLifecycleEventKind.AreaEntered);
            }

            var neighbor = project.Areas.FirstOrDefault(area => area.Id != target.Id);
            if (neighbor is not null)
            {
                var exitCoordinator = new RuntimeLifecycleCoordinator();
                if (exitCoordinator.LoadProject(
                        project,
                        RuntimeAuthority.Editor,
                        RuntimeLifecycleCause.Playtest).Succeeded &&
                    exitCoordinator.EnterInitialArea(
                        target.Id,
                        null,
                        RuntimeLifecycleCause.Playtest).Succeeded &&
                    exitCoordinator.StartWorld(RuntimeLifecycleCause.Playtest).Succeeded)
                {
                    exited = exitCoordinator.Transition(
                        neighbor.Id,
                        null,
                        null,
                        RuntimeLifecycleCause.PortalTransition,
                        dryRun: true).Events.SingleOrDefault(item =>
                        item.Kind == RuntimeLifecycleEventKind.AreaExited);
                }
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _ = exception;
        }

        observations.Add(BuildAreaLifecycleObservationUx2LwB(
            "area.entered",
            "Entrée dans la zone",
            target.Foundation.Lifecycle.Entered,
            entered,
            "Événement typé observé en aperçu local ; aucun script n’a été exécuté."));
        observations.Add(BuildAreaLifecycleObservationUx2LwB(
            "area.exited",
            "Sortie de la zone",
            target.Foundation.Lifecycle.Exited,
            exited,
            exited is null
                ? "Contrat validé ; l’observation runtime exige une autre zone et une transition réelle. Aucun script n’a été exécuté."
                : "Événement typé observé pendant une transition locale sèche ; aucun script n’a été exécuté."));
        return observations;
    }

    private static StudioUx2LwBEventObservation BuildAreaLifecycleObservationUx2LwB(
        string eventName,
        string humanEventName,
        AreaLifecycleScriptBinding? binding,
        RuntimeLifecycleEvent? observed,
        string summary) => new(
        eventName,
        humanEventName,
        true,
        observed is not null && string.Equals(observed.Binding, eventName, StringComparison.Ordinal),
        binding is null || string.IsNullOrWhiteSpace(binding.ScriptPath)
            ? null
            : HumanAreaLifecycleScriptNameUx2LwB(binding.ScriptPath),
        binding?.Required == true,
        summary);

    internal ProjectDefinition? CreateUx2LwBEvidenceProject() => Project;

    internal static bool CopyUx2LwBEvidenceScripts(string sourceRoot, string destinationRoot)
    {
        try
        {
            var sourceScripts = Path.Combine(sourceRoot, "scripts");
            if (!Directory.Exists(sourceScripts))
            {
                return true;
            }
            foreach (var sourcePath in Directory.EnumerateFiles(sourceScripts, "*.forge", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceRoot, sourcePath);
                var destinationPath = Path.Combine(destinationRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                File.Copy(sourcePath, destinationPath, overwrite: true);
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal StudioUx2LwBSmokeResult RunUx2LwBContractSmoke()
    {
        var sourceSession = Session;
        var sourceProject = Project;
        if (sourceSession is null || sourceProject is null)
        {
            return FailedUx2LwBSmoke();
        }

        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux2-lw-b-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var removalReopened = new StudioSessionController();
        try
        {
            var created = ProjectSession.Create(tempRoot, sourceProject);
            if (!created.Succeeded ||
                !CopyUx2LwBEvidenceScripts(sourceSession.RootDirectory, tempRoot) ||
                !editing.Open(tempRoot, ActiveAreaId?.ToString()).Succeeded)
            {
                return FailedUx2LwBSmoke();
            }

            var choices = editing.AreaLifecycleChoicesUx2LwB;
            var explicitSelection = editing.CreateAreaLifecycleDraftUx2LwB() is null && choices.Count > 0;
            var targetChoice = choices.FirstOrDefault(item => item.IsEvidenceTarget) ?? choices.FirstOrDefault();
            if (targetChoice is null)
            {
                return FailedUx2LwBSmoke();
            }
            var originalProject = editing.Project!;
            var originalTarget = originalProject.Areas.Single(area => area.Id == targetChoice.AreaId);
            var originalNeighbors = originalProject.Areas.Where(area => area.Id != targetChoice.AreaId).ToArray();
            var scripts = editing.FindForgeScripts();
            var realScriptCatalog = scripts.Count > 0 && scripts.All(script =>
                File.Exists(Path.Combine(tempRoot, script.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
            if (!realScriptCatalog)
            {
                return FailedUx2LwBSmoke() with { ExplicitSelection = explicitSelection };
            }

            var firstScript = scripts[0].RelativePath;
            var secondScript = scripts.Count > 1 ? scripts[1].RelativePath : firstScript;
            StudioUx2LwBDraft Configure(StudioUx2LwBDraft source) => source with
            {
                Entered = source.Entered with
                {
                    Enabled = true,
                    ScriptPath = firstScript,
                    Required = source.Entered.Enabled && string.Equals(
                        source.Entered.ScriptPath,
                        firstScript,
                        StringComparison.Ordinal)
                            ? !source.Entered.Required
                            : true,
                },
                Exited = source.Exited with
                {
                    Enabled = true,
                    ScriptPath = secondScript,
                    Required = false,
                },
            };

            var initialDraft = editing.CreateAreaLifecycleDraftUx2LwB(targetChoice.AreaId)!;
            var requiredWithoutScript = editing.PreviewAreaLifecycleUx2LwB(initialDraft with
            {
                Entered = initialDraft.Entered with
                {
                    Enabled = true,
                    ScriptPath = null,
                    Required = true,
                },
            });
            var missingScript = editing.PreviewAreaLifecycleUx2LwB(initialDraft with
            {
                Entered = initialDraft.Entered with
                {
                    Enabled = true,
                    ScriptPath = "scripts/source-absente.forge",
                    Required = false,
                },
            });
            var requiredScriptBlocked = !requiredWithoutScript.Succeeded &&
                                        requiredWithoutScript.Diagnostics.Any(item =>
                                            item.Code == "studio.area_lifecycle.script_required");
            var missingScriptBlocked = !missingScript.Succeeded && missingScript.Diagnostics.Any(item =>
                item.Code == "studio.area_lifecycle.script_missing");
            var structuredDiagnostics = requiredWithoutScript.Diagnostics.Concat(missingScript.Diagnostics)
                .All(item => !string.IsNullOrWhiteSpace(item.Code) && !string.IsNullOrWhiteSpace(item.Path));

            var configured = Configure(editing.CreateAreaLifecycleDraftUx2LwB(targetChoice.AreaId)!);
            var historyBeforePreview = editing.History!;
            var projectBeforePreview = editing.Project;
            var revisionBeforePreview = historyBeforePreview.State.Revision;
            var journalBeforePreview = historyBeforePreview.Journal.ToArray();
            var undoBeforePreview = historyBeforePreview.CanUndo;
            var redoBeforePreview = historyBeforePreview.CanRedo;
            var dirtyBeforePreview = editing.IsDirty;
            var preview = editing.PreviewAreaLifecycleUx2LwB(configured);
            var dryRun = preview.Succeeded && preview.CanApply &&
                         ReferenceEquals(projectBeforePreview, editing.Project) &&
                         ReferenceEquals(historyBeforePreview, editing.History) &&
                         historyBeforePreview.State.Revision == revisionBeforePreview &&
                         historyBeforePreview.Journal.SequenceEqual(journalBeforePreview) &&
                         historyBeforePreview.CanUndo == undoBeforePreview &&
                         historyBeforePreview.CanRedo == redoBeforePreview &&
                         editing.IsDirty == dirtyBeforePreview;
            var enteredObserved = preview.Observations.Single(item => item.EventName == "area.entered")
                .RuntimeEventObserved;
            var exitedObservation = preview.Observations.Single(item => item.EventName == "area.exited");
            var exitedObserved = originalProject.Areas.Count < 2 || exitedObservation.RuntimeEventObserved;
            var noScriptExecutionClaim = preview.Message.Contains("aucun", StringComparison.OrdinalIgnoreCase) &&
                                         preview.Observations.All(item => item.Summary.Contains(
                                             "aucun script n’a été exécuté",
                                             StringComparison.OrdinalIgnoreCase));

            var staleDraft = Configure(editing.CreateAreaLifecycleDraftUx2LwB(targetChoice.AreaId)!);
            var neighborMutation = ApplyUx2LwBRevisionMutation(editing, targetChoice.AreaId);
            var staleDraftPreview = editing.PreviewAreaLifecycleUx2LwB(staleDraft);
            var staleDraftBlocked = neighborMutation && !staleDraftPreview.Succeeded &&
                                    staleDraftPreview.Message.Contains("projet a changé",
                                        StringComparison.OrdinalIgnoreCase) &&
                                    editing.Undo().Succeeded;

            var staleApprovalDraft = Configure(editing.CreateAreaLifecycleDraftUx2LwB(targetChoice.AreaId)!);
            var staleApprovalPreview = editing.PreviewAreaLifecycleUx2LwB(staleApprovalDraft);
            var staleApprovalMutation = ApplyUx2LwBRevisionMutation(editing, targetChoice.AreaId);
            var staleApprovalApply = editing.ApplyApprovedAreaLifecycleUx2LwB();
            var staleApprovalBlocked = staleApprovalPreview.Succeeded && staleApprovalMutation &&
                                       !staleApprovalApply.Succeeded &&
                                       staleApprovalApply.Message.Contains("Prévisualisez",
                                           StringComparison.Ordinal) &&
                                       editing.Undo().Succeeded;

            var sourceProjectForApply = editing.Project!;
            var sourceTarget = sourceProjectForApply.Areas.Single(area => area.Id == targetChoice.AreaId);
            var fresh = Configure(editing.CreateAreaLifecycleDraftUx2LwB(targetChoice.AreaId)!);
            var freshPreview = editing.PreviewAreaLifecycleUx2LwB(fresh);
            var applied = editing.ApplyApprovedAreaLifecycleUx2LwB();
            var appliedProject = editing.Project!;
            var appliedTarget = appliedProject.Areas.Single(area => area.Id == targetChoice.AreaId);
            var canonicalCommand = freshPreview.Succeeded && applied.Succeeded &&
                                   editing.History!.Journal.LastOrDefault()?.CommandName == "set_area_foundation" &&
                                   string.Equals(appliedTarget.Foundation.Lifecycle.Entered?.ScriptPath,
                                       firstScript,
                                       StringComparison.Ordinal) &&
                                   string.Equals(appliedTarget.Foundation.Lifecycle.Exited?.ScriptPath,
                                       secondScript,
                                       StringComparison.Ordinal);
            var targetNeighborsPreserved =
                ReferenceEquals(sourceTarget.Foundation.Creation, appliedTarget.Foundation.Creation) &&
                ReferenceEquals(sourceTarget.Foundation.TileKit, appliedTarget.Foundation.TileKit) &&
                sourceTarget.Foundation.Volumes.Count == appliedTarget.Foundation.Volumes.Count &&
                sourceTarget.Foundation.Volumes.All(sourceVolume => ReferenceEquals(
                    sourceVolume,
                    appliedTarget.Foundation.Volumes.Single(item => item.Id == sourceVolume.Id))) &&
                sourceTarget.Foundation.Dependencies.Count == appliedTarget.Foundation.Dependencies.Count &&
                sourceTarget.Foundation.Dependencies.All(sourceDependency => ReferenceEquals(
                    sourceDependency,
                    appliedTarget.Foundation.Dependencies.Single(item => item.Id == sourceDependency.Id))) &&
                sourceTarget.Foundation.RequireTileEditingRuntimeMetadata ==
                appliedTarget.Foundation.RequireTileEditingRuntimeMetadata &&
                ReferenceEquals(sourceTarget.Navigation, appliedTarget.Navigation) &&
                ReferenceEquals(sourceTarget.Environment, appliedTarget.Environment) &&
                SameAreaAuthoringUx2A(sourceTarget, appliedTarget) &&
                ReferenceEquals(sourceTarget.World, appliedTarget.World) &&
                ReferenceEquals(sourceTarget.RuntimePolicies, appliedTarget.RuntimePolicies) &&
                ReferenceEquals(sourceTarget.Playtest, appliedTarget.Playtest) &&
                sourceTarget.Entities.Count == appliedTarget.Entities.Count &&
                sourceTarget.Entities.All(sourceEntity => ReferenceEquals(
                    sourceEntity,
                    appliedTarget.Entities.Single(item => item.Id == sourceEntity.Id))) &&
                sourceTarget.Portals.Count == appliedTarget.Portals.Count &&
                sourceTarget.Portals.All(sourcePortal => ReferenceEquals(
                    sourcePortal,
                    appliedTarget.Portals.Single(item => item.Id == sourcePortal.Id)));
            var projectNeighborsPreserved = ReferenceEquals(sourceProjectForApply.Module, appliedProject.Module) &&
                                            sourceProjectForApply.Blueprints.SequenceEqual(appliedProject.Blueprints) &&
                                            sourceProjectForApply.Dialogues.SequenceEqual(appliedProject.Dialogues) &&
                                            sourceProjectForApply.Quests.SequenceEqual(appliedProject.Quests) &&
                                            originalNeighbors.All(neighbor => ReferenceEquals(
                                                sourceProjectForApply.Areas.Single(item => item.Id == neighbor.Id),
                                                appliedProject.Areas.Single(item => item.Id == neighbor.Id)));

            var appliedAreaJson = AreaJsonSerializer.Serialize(appliedTarget);
            var appliedProjectSnapshot = appliedProject;
            var undo = editing.Undo();
            var undone = undo.Succeeded && ReferenceEquals(sourceProjectForApply, editing.Project);
            var redo = editing.Redo();
            var redone = redo.Succeeded && ReferenceEquals(appliedProject, editing.Project);
            var undoRedo = undone && redone;
            var saved = editing.Save();
            var reopenedResult = reopened.Open(tempRoot, targetChoice.AreaId.ToString());
            var restoredProject = reopened.Project;
            var restoredTarget = restoredProject?.Areas.SingleOrDefault(area => area.Id == targetChoice.AreaId);
            var persistence = saved.Succeeded && reopenedResult.Succeeded && restoredProject is not null &&
                              restoredTarget is not null &&
                              string.Equals(appliedAreaJson, AreaJsonSerializer.Serialize(restoredTarget),
                                  StringComparison.Ordinal) &&
                              ProjectRoundTripMatchesUx2A(
                                  appliedProjectSnapshot,
                                  restoredProject,
                                  targetChoice.AreaId);

            var removalDraft = reopened.CreateAreaLifecycleDraftUx2LwB(targetChoice.AreaId)!;
            var removalPreview = reopened.PreviewAreaLifecycleUx2LwB(removalDraft with
            {
                Entered = removalDraft.Entered with { Enabled = false, Required = false, ScriptPath = null },
                Exited = removalDraft.Exited with { Enabled = false, Required = false, ScriptPath = null },
            });
            var removalApplied = reopened.ApplyApprovedAreaLifecycleUx2LwB();
            var removalSaved = reopened.Save();
            var removalOpened = removalReopened.Open(tempRoot, targetChoice.AreaId.ToString());
            var removalTarget = removalReopened.Project?.Areas.SingleOrDefault(area => area.Id == targetChoice.AreaId);
            var optionalRemoval = removalPreview.Succeeded && removalApplied.Succeeded && removalSaved.Succeeded &&
                                  removalOpened.Succeeded && removalTarget is not null &&
                                  removalTarget.Foundation.Lifecycle.Entered is null &&
                                  removalTarget.Foundation.Lifecycle.Exited is null;

            var humanText = !ContainsTechnicalIdentifierUx2LwB(
                string.Join(' ', choices.Select(item => item.Label)) + " " +
                string.Join(' ', preview.Diff.Changes) + " " +
                string.Join(' ', preview.Observations.Select(item => item.Summary)));
            var succeeded = explicitSelection && realScriptCatalog && requiredScriptBlocked &&
                            missingScriptBlocked && dryRun && enteredObserved && exitedObserved &&
                            noScriptExecutionClaim && staleDraftBlocked && staleApprovalBlocked &&
                            canonicalCommand && undoRedo && persistence && targetNeighborsPreserved &&
                            projectNeighborsPreserved && optionalRemoval && structuredDiagnostics && humanText;
            return new StudioUx2LwBSmokeResult(
                succeeded,
                explicitSelection,
                realScriptCatalog,
                requiredScriptBlocked,
                missingScriptBlocked,
                dryRun,
                enteredObserved,
                exitedObserved,
                noScriptExecutionClaim,
                staleDraftBlocked,
                staleApprovalBlocked,
                canonicalCommand,
                undoRedo,
                persistence,
                targetNeighborsPreserved,
                projectNeighborsPreserved,
                optionalRemoval,
                structuredDiagnostics,
                humanText);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_LW_B_CONTRACT_FAILED type={exception.GetType().Name} message={exception.Message}");
            return FailedUx2LwBSmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            removalReopened.Close();
            DeleteUx2LwBTemporaryWorkspace(tempRoot);
        }
    }

    internal static bool DeleteUx2LwBTemporaryWorkspace(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return true;
        }
        try
        {
            var root = Path.GetFullPath(directory);
            var temp = Path.GetFullPath(Path.GetTempPath());
            var prefix = temp.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                         Path.DirectorySeparatorChar;
            var leaf = Path.GetFileName(root.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
            if (!root.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !leaf.StartsWith("openrpg-forge-studio-ux2-lw-b-", StringComparison.Ordinal))
            {
                return false;
            }
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
            return !Directory.Exists(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool ApplyUx2LwBRevisionMutation(
        StudioSessionController controller,
        AreaId areaId)
    {
        var history = controller.History;
        var area = controller.Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (history is null || area is null)
        {
            return false;
        }
        var source = area.Foundation;
        var candidate = new AreaFoundationProfile(
            source.Id,
            source.SchemaVersion,
            source.Provenance + ":revision-check",
            source.Creation,
            source.TileKit,
            source.Lifecycle,
            source.Volumes,
            source.Dependencies,
            source.RequireTileEditingRuntimeMetadata);
        var handler = new SetAreaFoundationHandler(new SystemClock());
        var result = handler.Handle(
            history.State,
            new SetAreaFoundationCommand(
                CommandId.From(Guid.NewGuid()),
                history.State.Revision,
                areaId,
                candidate,
                DryRun: false));
        if (!result.Succeeded)
        {
            return false;
        }
        history.Record(result);
        return true;
    }

    private static ProjectDefinition ReplaceAreaUx2LwB(
        ProjectDefinition project,
        AreaDefinition candidate) => new(
        project.Id,
        project.Title,
        project.Module,
        project.Blueprints,
        project.Areas.Select(area => area.Id == candidate.Id ? candidate : area).ToArray(),
        project.Dialogues,
        project.Quests);

    private static StudioUx2LwBPreview FailedAreaLifecyclePreviewUx2LwB(string message) => new(
        false,
        message,
        false,
        new StudioUx2LwBSemanticDiff([]),
        [],
        [],
        string.Empty);

    private static StudioUx2LwBSmokeResult FailedUx2LwBSmoke() => new(
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false);

    private static string AreaLifecycleSourceFingerprintUx2LwB(AreaDefinition area) =>
        HashTypedAreaLifecycleUx2LwB(builder =>
        {
            AppendTypedAreaLifecycleUx2LwB(builder, "area", AreaJsonSerializer.Serialize(area));
            AppendTypedAreaLifecycleUx2LwB(builder, "route", "ORF-AREA-022");
        });

    private static string AreaLifecycleDraftFingerprintUx2LwB(StudioUx2LwBDraft draft) =>
        HashTypedAreaLifecycleUx2LwB(builder =>
        {
            AppendTypedAreaLifecycleUx2LwB(builder, "area", draft.AreaId.ToString());
            AppendTypedAreaLifecycleUx2LwB(builder, "revision", draft.BaseRevision.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
            AppendTypedAreaLifecycleUx2LwB(builder, "source", draft.SourceFingerprint);
            AppendAreaLifecycleBindingFingerprintUx2LwB(builder, "entered", draft.Entered);
            AppendAreaLifecycleBindingFingerprintUx2LwB(builder, "exited", draft.Exited);
        });

    private static void AppendAreaLifecycleBindingFingerprintUx2LwB(
        StringBuilder builder,
        string prefix,
        StudioUx2LwBBindingDraft binding)
    {
        AppendTypedAreaLifecycleUx2LwB(builder, prefix + ".enabled", binding.Enabled ? "1" : "0");
        AppendTypedAreaLifecycleUx2LwB(builder, prefix + ".id", binding.BindingId.ToString("D"));
        AppendTypedAreaLifecycleUx2LwB(builder, prefix + ".event", binding.EventName);
        AppendTypedAreaLifecycleUx2LwB(builder, prefix + ".api", binding.ForgeScriptApiVersion);
        AppendTypedAreaLifecycleUx2LwB(builder, prefix + ".script", binding.ScriptPath ?? string.Empty);
        AppendTypedAreaLifecycleUx2LwB(builder, prefix + ".required", binding.Required ? "1" : "0");
        AppendTypedAreaLifecycleUx2LwB(builder, prefix + ".provenance", binding.Provenance);
    }

    private static string HashTypedAreaLifecycleUx2LwB(Action<StringBuilder> append)
    {
        var builder = new StringBuilder();
        append(builder);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static void AppendTypedAreaLifecycleUx2LwB(
        StringBuilder builder,
        string name,
        string value)
    {
        builder.Append(name.Length)
            .Append(':')
            .Append(name)
            .Append('=')
            .Append(value.Length)
            .Append(':')
            .Append(value)
            .Append(';');
    }

    private static Guid StableAreaLifecycleBindingIdUx2LwB(AreaId areaId, string eventName)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"ux2-lw-b:{areaId}:{eventName}"));
        return new Guid(bytes[..16]);
    }

    private static string? NormalizeAreaLifecycleScriptPathUx2LwB(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : path.Trim().Replace('\\', '/');

    internal static string HumanAreaLifecycleScriptNameUx2LwB(string? relativePath)
    {
        var stem = Path.GetFileNameWithoutExtension(relativePath ?? string.Empty);
        if (ContainsTechnicalIdentifierUx2LwB(stem))
        {
            return "Script du projet";
        }
        var words = stem.Replace('_', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length == 0
            ? "Script sans nom"
            : string.Join(' ', words.Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }

    private static string HumanAreaNameUx2LwB(AreaDefinition area)
    {
        var value = area.DisplayName.Trim();
        return ContainsTechnicalIdentifierUx2LwB(value) ? "Zone sans nom lisible" : value;
    }

    private static string HumanSlugUx2LwB(string slug)
    {
        if (ContainsTechnicalIdentifierUx2LwB(slug))
        {
            return "zone";
        }
        var words = slug.Replace('_', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length == 0
            ? "zone"
            : string.Join(' ', words.Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }

    internal static bool ContainsTechnicalIdentifierUx2LwB(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        return System.Text.RegularExpressions.Regex.IsMatch(
            text,
            @"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    private static string HumanizeAreaLifecycleDiagnosticUx2LwB(string? code) => code switch
    {
        SetAreaFoundationHandler.RevisionConflictCode =>
            "Le projet a changé. Rechargez cette fiche avant de continuer.",
        SetAreaFoundationHandler.AreaMissingCode =>
            "La zone sélectionnée n’existe plus.",
        AreaFoundationValidator.LifecycleCode =>
            "Vérifiez le lien d’entrée ou de sortie signalé.",
        null => "La commande canonique n’a pas pu valider cette fiche.",
        _ => "La fondation de la zone contient une valeur invalide. Consultez le diagnostic localisé.",
    };
}
