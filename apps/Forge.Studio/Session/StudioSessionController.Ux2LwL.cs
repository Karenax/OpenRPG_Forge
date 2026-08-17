using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Validation;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Encounters;
using Forge.RpgKit.Domain.Encounters;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2LwLEvidenceProvenance = "studio:ux2-lw-l:evidence";
    private const string Ux2LwLAuthoringProvenance = "studio:ux2-lw-l:validation-correction";
    private const string Ux2LwLBoundary =
        "Validation d’auteur offline : aucune navigation, apparition, caméra, récompense, condition, suppression, " +
        "simulation moteur ou opération réseau n’est exécutée.";

    private readonly SetWorldFeatureCatalogHandler _setEncounterValidationCatalogUx2LwL = new(new SystemClock());
    private StudioUx2LwLDraft? _approvedEncounterValidationUx2LwL;
    private ProjectRevision? _approvedEncounterValidationRevisionUx2LwL;
    private string? _approvedEncounterValidationSourceFingerprintUx2LwL;
    private string? _approvedEncounterValidationDraftFingerprintUx2LwL;

    internal IReadOnlyList<StudioUx2LwLEncounterChoice> EncounterChoicesUx2LwL
    {
        get
        {
            var project = Project;
            var area = ActiveArea;
            if (project is null || area is null) return [];
            return project.Module.WorldFeatures.Encounters
                .Where(item => item.AreaId == area.Id)
                .Select(item => new StudioUx2LwLEncounterChoice(item.Id,
                    $"{HumanEncounterNameUx2LwL(item)} · " +
                    (item.Foundation is null
                        ? "Foundation absente · validation non modifiable"
                        : $"{item.Foundation.Candidates.Count} candidat(s) · " +
                          $"{item.Foundation.SpawnAnchorIds.Count} ancre(s)"),
                    item.Foundation is not null,
                    string.Equals(item.Provenance, Ux2LwLEvidenceProvenance, StringComparison.Ordinal)))
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioUx2LwLReferenceChoice> EncounterAnchorChoicesUx2LwL(AreaId? areaId)
    {
        var project = Project;
        if (project is null || areaId is null) return [];
        return NavigationDestinationCatalog.Build(project).Entries
            .Where(item => item.AreaId == areaId && item.Kind == NavigationDestinationKind.SpawnAnchor &&
                           !string.IsNullOrWhiteSpace(item.ArrivalAnchorKey))
            .Select(item => new StudioUx2LwLReferenceChoice(item.ArrivalAnchorKey!, item.HumanLabel,
                $"Ancre de spawn enregistrée · {HumanProvenanceUx2LwL(item.Provenance)}"))
            .DistinctBy(item => item.Id, StringComparer.Ordinal)
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal IReadOnlyList<StudioUx2LwLReferenceChoice> EncounterScriptChoicesUx2LwL()
    {
        var session = Session;
        if (session is null) return [];
        var root = Path.Combine(session.RootDirectory, "scripts");
        if (!Directory.Exists(root)) return [];
        try
        {
            return Directory.EnumerateFiles(root, "*.forge", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(session.RootDirectory, path).Replace('\\', '/'))
                .Where(IsSafeRelativeScriptUx2LwL)
                .Distinct(StringComparer.Ordinal)
                .Select(path => new StudioUx2LwLReferenceChoice(path, HumanScriptNameUx2LwL(path),
                    "Source ForgeScript locale réelle · lecture seule pendant la validation"))
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    internal StudioUx2LwLDraft? CreateEncounterValidationDraftUx2LwL(Guid? profileId)
    {
        var history = History;
        var project = Project;
        var area = ActiveArea;
        if (history is null || project is null || area is null || profileId is null) return null;
        var profile = project.Module.WorldFeatures.Encounters.SingleOrDefault(item =>
            item.Id == profileId.Value && item.AreaId == area.Id);
        if (profile?.Foundation is not { } foundation) return null;
        var selected = profile!;
        return new(selected.Id, history.State.Revision, ProjectFingerprintUx2LwL(project),
            foundation.SpawnAnchorIds.ToArray(), foundation.Volume.ActivationScriptId,
            foundation.SpawnBudget, foundation.NetworkActorCap, selected);
    }

    internal StudioUx2LwLPreview PreviewEncounterValidationUx2LwL(StudioUx2LwLDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelEncounterValidationPreviewUx2LwL();
        var history = History;
        var project = Project;
        if (history is null || project is null)
            return FailedEncounterValidationPreviewUx2LwL("Aucun module n’est ouvert.");
        if (draft.BaseRevision != history.State.Revision)
            return FailedEncounterValidationPreviewUx2LwL(
                "Le projet a changé depuis l’ouverture de la fiche. Rechargez la rencontre.");
        if (!string.Equals(draft.SourceFingerprint, ProjectFingerprintUx2LwL(project), StringComparison.Ordinal))
            return FailedEncounterValidationPreviewUx2LwL(
                "Le catalogue ou ses références ont changé. Rechargez la rencontre.");

        var sourceProject = project;
        var sourceRevision = history.State.Revision;
        var sourceJournal = history.Journal.Count;
        if (!TryBuildEncounterValidationCandidateUx2LwL(draft, true, out var command,
                out var candidate, out var candidateProject, out var diagnostics, out var coverage))
            return new(false, false,
                diagnostics.FirstOrDefault(item => item.Blocking)?.Message ??
                "La validation a refusé cette correction.",
                string.Empty, BuildEncounterValidationDiffUx2LwL(draft.OriginalProfile, candidate),
                diagnostics, coverage, true, Ux2LwLBoundary);

        var result = _setEncounterValidationCatalogUx2LwL.Handle(history.State, command!);
        if (!result.Succeeded)
        {
            var commandDiagnostics = result.Diagnostics.Select(item => new StudioUx2LwLDiagnostic(
                item.Code, $"module/worldFeatures/encounters/{draft.ProfileId:D}",
                HumanEncounterValidationDiagnosticUx2LwL(item.Code), true, "Commande canonique")).ToArray();
            return new(false, false,
                commandDiagnostics.FirstOrDefault()?.Message ?? "La commande canonique a refusé la correction.",
                result.Patch?.Summary ?? string.Empty,
                BuildEncounterValidationDiffUx2LwL(draft.OriginalProfile, candidate), commandDiagnostics,
                coverage, true, Ux2LwLBoundary);
        }

        var evaluated = ValidateEncounterCandidateUx2LwL(candidateProject!, candidate!);
        diagnostics = evaluated.Diagnostics;
        coverage = evaluated.Coverage;
        var diff = BuildEncounterValidationDiffUx2LwL(draft.OriginalProfile, candidate);
        var dryRunPure = ReferenceEquals(sourceProject, Project) &&
                         history.State.Revision == sourceRevision && history.Journal.Count == sourceJournal;
        var canApply = dryRunPure && diagnostics.All(item => !item.Blocking) && diff.Any(item => item.Changed);
        if (canApply)
        {
            _approvedEncounterValidationUx2LwL = draft;
            _approvedEncounterValidationRevisionUx2LwL = history.State.Revision;
            _approvedEncounterValidationSourceFingerprintUx2LwL = draft.SourceFingerprint;
            _approvedEncounterValidationDraftFingerprintUx2LwL =
                EncounterValidationDraftFingerprintUx2LwL(draft);
        }

        var succeeded = diagnostics.All(item => !item.Blocking) && dryRunPure;
        return new(succeeded, canApply,
            !succeeded
                ? diagnostics.First(item => item.Blocking).Message
                : canApply
                    ? "Validation sèche terminée · la correction explicite peut être appliquée."
                    : "Validation sèche terminée · aucune correction n’a été authorée.",
            result.Patch?.Summary ?? string.Empty, diff, diagnostics, coverage, dryRunPure, Ux2LwLBoundary);
    }

    internal StudioUx2LwLActionResult ApplyApprovedEncounterValidationUx2LwL()
    {
        var history = History;
        var project = Project;
        var draft = _approvedEncounterValidationUx2LwL;
        if (history is null || project is null || draft is null ||
            _approvedEncounterValidationRevisionUx2LwL != history.State.Revision ||
            !string.Equals(_approvedEncounterValidationSourceFingerprintUx2LwL,
                ProjectFingerprintUx2LwL(project), StringComparison.Ordinal) ||
            !string.Equals(_approvedEncounterValidationDraftFingerprintUx2LwL,
                EncounterValidationDraftFingerprintUx2LwL(draft), StringComparison.Ordinal))
        {
            CancelEncounterValidationPreviewUx2LwL();
            return new(false, "Relancez la validation avant d’appliquer cette correction.");
        }

        if (!TryBuildEncounterValidationCandidateUx2LwL(draft, false, out var command,
                out var candidate, out _, out var diagnostics, out _))
        {
            CancelEncounterValidationPreviewUx2LwL();
            return new(false, diagnostics.FirstOrDefault(item => item.Blocking)?.Message ??
                              "La correction est invalide.");
        }
        if (!BuildEncounterValidationDiffUx2LwL(draft.OriginalProfile, candidate).Any(item => item.Changed))
        {
            CancelEncounterValidationPreviewUx2LwL();
            return new(false, "Aucune correction authorée à appliquer.");
        }

        var result = _setEncounterValidationCatalogUx2LwL.Handle(history.State, command!);
        CancelEncounterValidationPreviewUx2LwL();
        if (!result.Succeeded)
            return new(false, HumanEncounterValidationDiagnosticUx2LwL(result.Diagnostics.FirstOrDefault()?.Code));
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, "Correction appliquée · Ctrl+Z pour annuler, Ctrl+Y pour rétablir.");
    }

    internal void CancelEncounterValidationPreviewUx2LwL()
    {
        _approvedEncounterValidationUx2LwL = null;
        _approvedEncounterValidationRevisionUx2LwL = null;
        _approvedEncounterValidationSourceFingerprintUx2LwL = null;
        _approvedEncounterValidationDraftFingerprintUx2LwL = null;
    }

    internal StudioUx2LwLApprovalSnapshot CaptureEncounterValidationApprovalUx2LwL() => new(
        _approvedEncounterValidationUx2LwL, _approvedEncounterValidationRevisionUx2LwL,
        _approvedEncounterValidationSourceFingerprintUx2LwL,
        _approvedEncounterValidationDraftFingerprintUx2LwL);

    internal void RestoreEncounterValidationApprovalUx2LwL(StudioUx2LwLApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedEncounterValidationUx2LwL = snapshot.Draft;
        _approvedEncounterValidationRevisionUx2LwL = snapshot.Revision;
        _approvedEncounterValidationSourceFingerprintUx2LwL = snapshot.SourceFingerprint;
        _approvedEncounterValidationDraftFingerprintUx2LwL = snapshot.DraftFingerprint;
    }

    internal void CloseUx2LwL() => CancelEncounterValidationPreviewUx2LwL();

    internal ProjectDefinition? CreateUx2LwLEvidenceProject()
    {
        var source = CreateUx2LwFEvidenceProject();
        if (source is null) return null;
        var target = source.Module.WorldFeatures.Encounters.FirstOrDefault(item =>
            item.Foundation is not null && item.Composition is not null && item.Scenario is not null);
        if (target?.Foundation is not { } foundation) return null;
        var targetProfile = target!;
        var authored = targetProfile with
        {
            Provenance = Ux2LwLEvidenceProvenance,
            Foundation = foundation with { RewardIds = ["reward:studio-evidence"] },
        };
        var catalog = ReplaceEncounterUx2LwL(source.Module.WorldFeatures, authored);
        return new(source.Id, "Studio UX2-LW-L", source.Module.WithWorldFeatures(catalog),
            source.Blueprints, source.Areas, source.Dialogues, source.Quests);
    }

    internal static bool WriteUx2LwLEvidenceScript(string root)
    {
        try
        {
            var scripts = Path.Combine(root, "scripts");
            Directory.CreateDirectory(scripts);
            File.WriteAllText(Path.Combine(scripts, "encounter_activate.forge"),
                "void OnEncounterActivate() { }\n", Encoding.UTF8);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private bool TryBuildEncounterValidationCandidateUx2LwL(
        StudioUx2LwLDraft draft,
        bool dryRun,
        out SetWorldFeatureCatalogCommand? command,
        out EncounterWorldProfile? candidate,
        out ProjectDefinition? candidateProject,
        out IReadOnlyList<StudioUx2LwLDiagnostic> diagnostics,
        out IReadOnlyList<StudioUx2LwLCoverage> coverage)
    {
        command = null;
        candidate = null;
        candidateProject = null;
        diagnostics = [];
        coverage = [];
        var history = History;
        var project = Project;
        if (history is null || project is null || draft.BaseRevision != history.State.Revision)
        {
            diagnostics = [new("command.revision_conflict", "project/revision",
                "Le projet a changé. Rechargez la rencontre.", true, "Concurrence")];
            return false;
        }
        if (!string.Equals(draft.SourceFingerprint, ProjectFingerprintUx2LwL(project), StringComparison.Ordinal))
        {
            diagnostics = [new("command.source_changed", "module/worldFeatures/encounters",
                "Le catalogue ou ses références ont changé. Rechargez la rencontre.", true, "Concurrence")];
            return false;
        }

        var current = project.Module.WorldFeatures.Encounters.SingleOrDefault(item => item.Id == draft.ProfileId);
        if (current?.Foundation is not { } foundation)
        {
            diagnostics = [new("world.encounter.foundation_missing",
                $"module/worldFeatures/encounters/{draft.ProfileId:D}/foundation",
                "Cette rencontre ne possède plus de Foundation modifiable.", true, "Socle canonique")];
            return false;
        }
        var currentProfile = current!;

        var anchors = draft.SpawnAnchorIds.Select(item => item.Trim()).Where(item => item.Length > 0).ToArray();
        var script = NormalizeScriptUx2LwL(draft.ActivationScriptId);
        var authoredAnchors = anchors.SequenceEqual(foundation.SpawnAnchorIds, StringComparer.Ordinal)
            ? foundation.SpawnAnchorIds
            : anchors;
        var volume = string.Equals(script, foundation.Volume.ActivationScriptId, StringComparison.Ordinal)
            ? foundation.Volume
            : foundation.Volume with { ActivationScriptId = script };
        var correctedFoundation = foundation with
        {
            Volume = volume,
            SpawnBudget = draft.SpawnBudget,
            SpawnAnchorIds = authoredAnchors,
            NetworkActorCap = draft.NetworkActorCap,
        };
        var corrected = currentProfile with
        {
            Provenance = Ux2LwLAuthoringProvenance,
            Foundation = correctedFoundation,
        };
        candidate = corrected;
        var catalog = ReplaceEncounterUx2LwL(project.Module.WorldFeatures, corrected);
        candidateProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(catalog), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests);
        var evaluated = ValidateEncounterCandidateUx2LwL(candidateProject, corrected);
        diagnostics = evaluated.Diagnostics;
        coverage = evaluated.Coverage;
        if (diagnostics.Any(item => item.Blocking)) return false;
        command = new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            catalog, dryRun);
        return true;
    }

    private (IReadOnlyList<StudioUx2LwLDiagnostic> Diagnostics,
        IReadOnlyList<StudioUx2LwLCoverage> Coverage) ValidateEncounterCandidateUx2LwL(
        ProjectDefinition project,
        EncounterWorldProfile profile)
    {
        var root = $"module/worldFeatures/encounters/{profile.Id}";
        var diagnostics = WorldFeatureCatalogValidator.Validate(project)
            .Where(item => item.Path.StartsWith(root, StringComparison.Ordinal))
            .Select(item => MapEncounterDiagnosticUx2LwL(item, "Socle canonique"))
            .ToList();

        diagnostics.AddRange(EncounterLifecycleRuntimeService.Validate(profile, project.Module.Factions)
            .Select(item => MapEncounterDiagnosticUx2LwL(item, "Lifecycle et scheduler")));
        var area = project.Areas.SingleOrDefault(item => item.Id == profile.AreaId);
        if (area is null)
        {
            diagnostics.Add(new("world.encounter.area_missing", $"{root}/areaId",
                "La zone de la rencontre n’existe plus dans le projet.", true, "Navigation et tactique"));
        }
        else
        {
            diagnostics.AddRange(EncounterTacticalRuntimeService.Validate(profile, project.Module.Factions,
                    area.Navigation.Waypoints)
                .Select(item => MapEncounterDiagnosticUx2LwL(item, "Navigation et tactique")));
        }

        var destinations = NavigationDestinationCatalog.Build(project);
        if (profile.Foundation is { } foundation)
        {
            for (var index = 0; index < foundation.SpawnAnchorIds.Count; index++)
            {
                var id = foundation.SpawnAnchorIds[index];
                var resolved = destinations.FindArrivalAnchor(id);
                if (resolved is null || resolved.Kind != NavigationDestinationKind.SpawnAnchor ||
                    resolved.AreaId != profile.AreaId)
                    diagnostics.Add(new("world.encounter.spawn_anchor_reference_missing",
                        $"{root}/foundation/spawnAnchorIds/{index}",
                        "Cette ancre ne résout pas une apparition enregistrée dans la zone de la rencontre.",
                        true, "Ancres et navigation"));
                else if (!IsNavigationBackedSpawnAnchorUx2LwL(project, profile.AreaId, id))
                    diagnostics.Add(new("world.encounter.spawn_anchor_navigation_unverified",
                        $"{root}/foundation/spawnAnchorIds/{index}",
                        "Cette ancre existe, mais aucune instance de spawn n’exige la navigation.",
                        true, "Ancres et navigation"));
            }

            var knownScripts = EncounterScriptChoicesUx2LwL().Select(item => item.Id)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var (scriptId, path) in EncounterScriptReferencesUx2LwL(profile, root))
                if (!knownScripts.Contains(scriptId))
                    diagnostics.Add(new("world.encounter.script_reference_missing", path,
                        "Ce script ForgeScript n’existe pas dans le workspace ouvert.", true,
                        "Scripts du workspace"));
        }

        if (profile.Foundation is not null && profile.Composition is not null)
        {
            try
            {
                var definition = EncounterCompositionFoundationService.CreateDefinition(profile);
                var runtime = EncounterCompositionFoundationService.CreateRuntimePolicy(profile);
                diagnostics.AddRange(EncounterCompositionService.Validate(definition, runtime)
                    .Select(item => MapEncounterDiagnosticUx2LwL(item, "Composition runtime")));
                if (profile.Scenario is not null)
                {
                    var phases = EncounterCompositionFoundationService.CreatePhases(profile);
                    diagnostics.AddRange(EncounterWaveService.Validate(definition, phases)
                        .Select(item => MapEncounterDiagnosticUx2LwL(item, "Scénario et boucles")));
                }
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                               KeyNotFoundException or OverflowException)
            {
                diagnostics.Add(new("world.encounter.runtime_mapping_invalid", root,
                    HumanEncounterValidationDiagnosticUx2LwL(exception.Message), true,
                    "Composition runtime"));
            }
        }

        var rewardCount = profile.Foundation?.RewardIds.Count ?? 0;
        var conditionCount = profile.Foundation?.Candidates.Sum(item => item.Conditions.Count) ?? 0;
        var scriptCount = EncounterScriptReferencesUx2LwL(profile, root).Count;
        var coverage = new List<StudioUx2LwLCoverage>
        {
            new("Document canonique", "Évalué",
                "Identité, versions, géométrie, candidats, budget local, composition, scénario, statistiques et cap local.", true),
            new("Ancres et navigation", "Évalué",
                "Chaque identifiant doit résoudre une ancre de spawn réelle de la zone avec navigation requise ; aucun pathfinding live n’est exécuté.", true),
            new("Lifecycle, factions et routes", "Évalué",
                "Respawn, scheduler, faction active, renforts, route de patrouille, visibilité et cleanup.", true),
            new("Scripts du workspace", "Évalué",
                scriptCount == 0 ? "Aucun script référencé." : $"{scriptCount} référence(s) comparée(s) aux fichiers .forge réels.", true),
            rewardCount == 0
                ? new("Récompenses", "Évalué", "Aucune référence de récompense à résoudre.", true)
                : new("Récompenses", "Non évalué",
                    $"{rewardCount} identifiant(s) conservé(s) : aucun catalogue canonique de récompenses n’existe encore.", false),
            conditionCount == 0
                ? new("Conditions", "Évalué", "Aucune condition de candidat à résoudre.", true)
                : new("Conditions", "Non évalué",
                    $"{conditionCount} condition(s) conservée(s) : aucun registre canonique de conditions n’existe encore.", false),
            new("Plafonds globaux", "Non évalué",
                "Le budget et le cap locaux sont contrôlés, mais le projet ne publie pas encore de plafond global pour EncounterAuthoringService.", false),
        };
        return (diagnostics
                .DistinctBy(item => (item.Code, item.Path))
                .OrderByDescending(item => item.Blocking)
                .ThenBy(item => item.Section, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Code, StringComparer.Ordinal)
                .ThenBy(item => item.Path, StringComparer.Ordinal)
                .ToArray(),
            coverage.ToArray());
    }

    private static bool IsNavigationBackedSpawnAnchorUx2LwL(
        ProjectDefinition project,
        AreaId areaId,
        string anchorKey)
    {
        var area = project.Areas.SingleOrDefault(item => item.Id == areaId);
        if (area is null) return false;
        var definitionIds = area.Navigation.Anchors.Definitions
            .Where(item => item.Kind == AnchorKind.Spawn &&
                           string.Equals(item.Slug, anchorKey, StringComparison.Ordinal))
            .Select(item => item.Id)
            .ToHashSet();
        return definitionIds.Count > 0 && area.Navigation.Anchors.Instances.Any(item =>
            definitionIds.Contains(item.DefinitionId) && item.Spawn?.RequireNavigation == true);
    }

    private static IReadOnlyList<(string ScriptId, string Path)> EncounterScriptReferencesUx2LwL(
        EncounterWorldProfile profile,
        string root)
    {
        var values = new List<(string, string)>();
        if (profile.Foundation?.Volume.ActivationScriptId is { Length: > 0 } foundationScript)
            values.Add((foundationScript, $"{root}/foundation/volume/activationScriptId"));
        if (profile.Composition?.Activation.ScriptId is { Length: > 0 } compositionScript)
            values.Add((compositionScript, $"{root}/composition/activation/scriptId"));
        if (profile.Scenario is { } scenario)
        {
            if (scenario.Lifecycle.Entered.ScriptId is { Length: > 0 } entered)
                values.Add((entered, $"{root}/scenario/lifecycle/entered/scriptId"));
            if (scenario.Lifecycle.Exited.ScriptId is { Length: > 0 } exited)
                values.Add((exited, $"{root}/scenario/lifecycle/exited/scriptId"));
            if (scenario.Lifecycle.Exhausted.ScriptId is { Length: > 0 } exhausted)
                values.Add((exhausted, $"{root}/scenario/lifecycle/exhausted/scriptId"));
            foreach (var wave in scenario.Phases.SelectMany(item => item.Waves)
                         .Where(item => item.Completion == EncounterWaveCompletionPolicy.Script &&
                                        !string.IsNullOrWhiteSpace(item.CompletionTargetId)))
                values.Add((wave.CompletionTargetId!, $"{root}/scenario/waves/{wave.Id}/completionTargetId"));
        }
        return values.Distinct().ToArray();
    }

    private static WorldFeatureCatalog ReplaceEncounterUx2LwL(
        WorldFeatureCatalog source,
        EncounterWorldProfile replacement) => new(
        source.Id, source.SchemaVersion, source.Provenance,
        source.Placeables, source.Doors, source.Triggers,
        source.Encounters.Select(item => item.Id == replacement.Id ? replacement : item).ToArray(),
        source.VehicleRoutes, source.Merchants, source.Companions, source.Scripts);

    private static StudioUx2LwLDiagnostic MapEncounterDiagnosticUx2LwL(
        DomainDiagnostic diagnostic,
        string section) => new(diagnostic.Code, CanonicalEncounterPathUx2LwL(diagnostic.Path),
        HumanEncounterValidationDiagnosticUx2LwL(diagnostic.Code), true, section);

    private static StudioUx2LwLDiagnostic MapEncounterDiagnosticUx2LwL(
        EncounterDiagnostic diagnostic,
        string section) => new(diagnostic.Code, CanonicalEncounterPathUx2LwL(diagnostic.Path),
        HumanEncounterValidationDiagnosticUx2LwL(diagnostic.Code), true, section);

    private static string CanonicalEncounterPathUx2LwL(string path) =>
        path.StartsWith("encounters/", StringComparison.Ordinal)
            ? $"module/worldFeatures/{path}"
            : path;

    private static IReadOnlyList<StudioUx2LwLDiff> BuildEncounterValidationDiffUx2LwL(
        EncounterWorldProfile before,
        EncounterWorldProfile? after)
    {
        if (after?.Foundation is not { } next || before.Foundation is not { } previous) return [];
        return
        [
            DiffUx2LwL("Ancres de spawn", string.Join(", ", previous.SpawnAnchorIds),
                string.Join(", ", next.SpawnAnchorIds)),
            DiffUx2LwL("Script d’activation", previous.Volume.ActivationScriptId ?? "Aucun",
                next.Volume.ActivationScriptId ?? "Aucun"),
            DiffUx2LwL("Budget local", previous.SpawnBudget.ToString("0.###", CultureInfo.InvariantCulture),
                next.SpawnBudget.ToString("0.###", CultureInfo.InvariantCulture)),
            DiffUx2LwL("Cap réseau local", previous.NetworkActorCap.ToString(CultureInfo.InvariantCulture),
                next.NetworkActorCap.ToString(CultureInfo.InvariantCulture)),
        ];
    }

    private static StudioUx2LwLDiff DiffUx2LwL(string section, string before, string after) =>
        new(section, before, after, !string.Equals(before, after, StringComparison.Ordinal));

    private static StudioUx2LwLPreview FailedEncounterValidationPreviewUx2LwL(string message) => new(
        false, false, message, string.Empty, [], [], [], true, Ux2LwLBoundary);

    private static string ProjectFingerprintUx2LwL(ProjectDefinition project) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));

    private static string EncounterValidationDraftFingerprintUx2LwL(StudioUx2LwLDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteUx2LwL(writer, draft.ProfileId.ToString("D"));
        WriteUx2LwL(writer, draft.BaseRevision.Value.ToString(CultureInfo.InvariantCulture));
        WriteUx2LwL(writer, draft.SourceFingerprint);
        writer.Write(draft.SpawnAnchorIds.Count);
        foreach (var item in draft.SpawnAnchorIds) WriteUx2LwL(writer, item);
        WriteUx2LwL(writer, draft.ActivationScriptId ?? string.Empty);
        writer.Write(BitConverter.DoubleToInt64Bits(draft.SpawnBudget));
        writer.Write(draft.NetworkActorCap);
        writer.Flush();
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteUx2LwL(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string? NormalizeScriptUx2LwL(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().Replace('\\', '/');

    private static bool IsSafeRelativeScriptUx2LwL(string value) =>
        value.StartsWith("scripts/", StringComparison.Ordinal) && value.EndsWith(".forge", StringComparison.Ordinal) &&
        !value.Contains("..", StringComparison.Ordinal) && value.Split('/').All(item => item.Length > 0);

    private static string HumanScriptNameUx2LwL(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path).Replace('-', ' ').Replace('_', ' ').Trim();
        return string.IsNullOrWhiteSpace(name) ? "Script protégé" : name;
    }

    private static string HumanEncounterNameUx2LwL(EncounterWorldProfile profile)
    {
        var value = profile.Composition?.DisplayName?.Trim();
        return string.IsNullOrWhiteSpace(value) || Guid.TryParse(value, out _) ? "Rencontre protégée" : value;
    }

    private static string HumanProvenanceUx2LwL(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "provenance enregistrée" :
        value.StartsWith("sample", StringComparison.OrdinalIgnoreCase) ? "exemple libre du module" :
        value.StartsWith("studio", StringComparison.OrdinalIgnoreCase) ? "Studio local" :
        "auteur du module";

    private static string HumanEncounterValidationDiagnosticUx2LwL(string? code) => code switch
    {
        "world.encounter.spawn_anchor_reference_missing" =>
            "Choisissez une ancre de spawn réelle enregistrée dans la zone de la rencontre.",
        "world.encounter.spawn_anchor_navigation_unverified" =>
            "Choisissez une ancre de spawn qui exige la navigation dans la zone de la rencontre.",
        "world.encounter.script_reference_missing" =>
            "Choisissez un fichier ForgeScript réel du workspace ou retirez la référence optionnelle.",
        "world.encounter.respawn_invalid" =>
            "Corrigez la politique de respawn dans la fiche Lifecycle.",
        "world.encounter.spawn_faction_missing" or "world.encounter.reinforcement_faction_missing" =>
            "Choisissez une faction active réellement enregistrée dans le module.",
        "world.encounter.scheduler_invalid" =>
            "Corrigez cadence, budget et namespace dans la fiche Lifecycle.",
        "world.encounter.reinforcement_route_missing" =>
            "Choisissez une route de patrouille non vide de la zone de la rencontre.",
        "world.encounter.reinforcement_invalid" =>
            "Corrigez seuil, délai, cap et condition d’annulation des renforts.",
        "world.encounter.visibility_invalid" =>
            "Corrigez distance, mode et effet de visibilité dans la fiche tactique.",
        "world.encounter.cleanup_invalid" =>
            "Corrigez les délais de cleanup dans la fiche tactique.",
        "world.encounter.budget_invalid" or "RPG_ENCOUNTER_BUDGET_INVALID" =>
            "Le budget doit être positif et admettre au moins un candidat.",
        "world.encounter.network_cap_invalid" or "RPG_ENCOUNTER_NETWORK_CAP_INVALID" =>
            "Le cap local doit contenir les maxima vivants des candidats.",
        "world.encounter.anchors_invalid" or "RPG_ENCOUNTER_ANCHOR_INVALID" or
            "RPG_ENCOUNTER_SPAWN_ANCHOR_INVALID" =>
            "La rencontre exige au moins une ancre unique et navigable.",
        "world.encounter.pool_invalid" or "world.encounter.candidate_invalid" or
            "world.encounter.candidate_missing" or "RPG_ENCOUNTER_CANDIDATE_MISSING" =>
            "Corrigez le pool de créatures dans la fiche Foundation.",
        "world.encounter.runtime_mapping_invalid" =>
            "La composition runtime ne peut pas être résolue avec les données actuelles.",
        "RPG_ENCOUNTER_COUNT_INVALID" =>
            "Les groupes, budgets et caps de composition sont incohérents.",
        "RPG_ENCOUNTER_PHASE_INVALID" or "RPG_ENCOUNTER_WAVE_INVALID" or
            "world.encounter.wave_transition_invalid" =>
            "Corrigez l’ordre et les transitions de phases et vagues dans la fiche Scénario.",
        "command.revision_conflict" or "command.source_changed" =>
            "Le projet a changé. Rechargez la rencontre avant de continuer.",
        null or "" => "La commande canonique a refusé la correction.",
        _ when code.StartsWith("WORLD_", StringComparison.Ordinal) =>
            "Une dépendance runtime requise est absente ou invalide.",
        _ => "Corrigez la valeur indiquée dans la section correspondante.",
    };

    private static bool SameReferencesUx2LwL<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
        where T : class => left.Count == right.Count &&
                           left.Zip(right).All(pair => ReferenceEquals(pair.First, pair.Second));

    private static bool IsHumanUx2LwLText(string value) =>
        !string.IsNullOrWhiteSpace(value) && !value.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !value.Contains("module/worldFeatures", StringComparison.Ordinal) &&
        !value.Split([' ', '·', '/', ':', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Any(token => Guid.TryParse(token, out _));

    internal StudioUx2LwLSmokeResult RunUx2LwLContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-lw-l-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var fixture = CreateUx2LwLEvidenceProject();
            if (fixture is null || !ProjectSession.Create(tempRoot, fixture).Succeeded ||
                !WriteUx2LwLEvidenceScript(tempRoot) || !editing.Open(tempRoot).Succeeded)
                return FailedUx2LwLSmoke();
            var target = editing.Project!.Module.WorldFeatures.Encounters.Single(item =>
                string.Equals(item.Provenance, Ux2LwLEvidenceProvenance, StringComparison.Ordinal));
            var areaId = target.AreaId;
            if (!editing.SelectArea(areaId)) return FailedUx2LwLSmoke();
            var neighbor = editing.Project.Module.WorldFeatures.Encounters.Single(item => item.Id != target.Id);
            var explicitSelection = editing.CreateEncounterValidationDraftUx2LwL(null) is null &&
                                    editing.EncounterChoicesUx2LwL.Any(item => item.ProfileId == target.Id);
            var loadedProject = editing.Project!;
            var initialCatalog = loadedProject.Module.WorldFeatures;
            var loadedTarget = initialCatalog.Encounters.Single(item => item.Id == target.Id);
            var loadedNeighbor = initialCatalog.Encounters.Single(item => item.Id == neighbor.Id);
            var draft = editing.CreateEncounterValidationDraftUx2LwL(target.Id);
            if (draft is null) return FailedUx2LwLSmoke();

            var invalidAnchor = editing.PreviewEncounterValidationUx2LwL(draft with
                { SpawnAnchorIds = ["anchor:absente"] });
            var invalidScript = editing.PreviewEncounterValidationUx2LwL(draft with
                { ActivationScriptId = "scripts/absent.forge" });
            var invalidBudget = editing.PreviewEncounterValidationUx2LwL(draft with { SpawnBudget = 0 });
            var invalidCap = editing.PreviewEncounterValidationUx2LwL(draft with { NetworkActorCap = 0 });
            var realAnchorValidation = !invalidAnchor.Succeeded && invalidAnchor.Diagnostics.Any(item =>
                item.Code == "world.encounter.spawn_anchor_reference_missing" && item.Path.EndsWith("/0"));
            var realScriptValidation = !invalidScript.Succeeded && invalidScript.Diagnostics.Any(item =>
                item.Code == "world.encounter.script_reference_missing" &&
                item.Path.EndsWith("/activationScriptId"));
            var canonicalAggregation = !invalidBudget.Succeeded && invalidBudget.Diagnostics.Any(item =>
                                           item.Code == "world.encounter.budget_invalid") &&
                                       !invalidCap.Succeeded && invalidCap.Diagnostics.Any(item =>
                                           item.Code == "world.encounter.network_cap_invalid");

            var lifecycleProfile = loadedTarget with
            {
                Respawn = new(double.NaN, -1, "", false, false, 2),
            };
            var lifecycleProject = ReplaceEncounterProjectUx2LwL(loadedProject, lifecycleProfile);
            var lifecycle = editing.ValidateEncounterCandidateUx2LwL(lifecycleProject, lifecycleProfile);
            var lifecycleValidation = lifecycle.Diagnostics.Any(item =>
                item.Code == EncounterLifecycleRuntimeService.RespawnInvalidCode);

            var tacticalProfile = loadedTarget with
            {
                Reinforcements = new(0, "faction-absente", Guid.NewGuid(), 2, 0, "cancel.safe"),
            };
            var tacticalProject = ReplaceEncounterProjectUx2LwL(loadedProject, tacticalProfile);
            var tactical = editing.ValidateEncounterCandidateUx2LwL(tacticalProject, tacticalProfile);
            var tacticalValidation = tactical.Diagnostics.Any(item =>
                                         item.Code == EncounterTacticalRuntimeService.ReinforcementFactionMissingCode) &&
                                     tactical.Diagnostics.Any(item =>
                                         item.Code == EncounterTacticalRuntimeService.ReinforcementRouteMissingCode);

            var baseline = editing.PreviewEncounterValidationUx2LwL(draft);
            var honestUnresolvedCoverage = baseline.Coverage.Any(item =>
                                               item.Section == "Récompenses" && !item.Evaluated &&
                                               item.Status == "Non évalué") &&
                                           baseline.Coverage.Any(item =>
                                               item.Section == "Conditions" && !item.Evaluated &&
                                               item.Status == "Non évalué") &&
                                           baseline.Coverage.Any(item =>
                                               item.Section == "Plafonds globaux" && !item.Evaluated);
            var correctionRequired = baseline.Succeeded && !baseline.CanApply &&
                                     !editing.ApplyApprovedEncounterValidationUx2LwL().Succeeded;

            var staleDraft = draft;
            var mutatedBeforePreview = ApplyUx2LwLIntermediateMutation(editing, neighbor.Id, 1);
            var stalePreview = editing.PreviewEncounterValidationUx2LwL(staleDraft);
            var staleDraftBlocked = mutatedBeforePreview && !stalePreview.Succeeded && editing.Undo().Succeeded;

            var correction = editing.CreateEncounterValidationDraftUx2LwL(target.Id)! with
            {
                SpawnBudget = draft.SpawnBudget + 4.5,
                NetworkActorCap = draft.NetworkActorCap + 2,
            };
            var approval = editing.PreviewEncounterValidationUx2LwL(correction);
            var mutatedAfterPreview = ApplyUx2LwLIntermediateMutation(editing, neighbor.Id, 2);
            var staleApply = editing.ApplyApprovedEncounterValidationUx2LwL();
            var staleApprovalBlocked = approval.Succeeded && approval.CanApply && mutatedAfterPreview &&
                                       !staleApply.Succeeded && editing.Undo().Succeeded;

            correction = editing.CreateEncounterValidationDraftUx2LwL(target.Id)! with
            {
                SpawnBudget = draft.SpawnBudget + 4.5,
                NetworkActorCap = draft.NetworkActorCap + 2,
            };
            var beforeDryRun = ProjectJsonSerializer.Serialize(editing.Project!);
            var beforeJournal = editing.History!.Journal.Count;
            var preview = editing.PreviewEncounterValidationUx2LwL(correction);
            var dryRunPure = preview.Succeeded && preview.CanApply && preview.DryRunPure &&
                             beforeDryRun == ProjectJsonSerializer.Serialize(editing.Project!) &&
                             editing.History.Journal.Count == beforeJournal;
            var applied = editing.ApplyApprovedEncounterValidationUx2LwL();
            if (!applied.Succeeded) return FailedUx2LwLSmoke();

            var appliedProject = editing.Project!;
            var appliedCatalog = appliedProject.Module.WorldFeatures;
            var appliedTarget = appliedCatalog.Encounters.Single(item => item.Id == target.Id);
            var appliedNeighbor = appliedCatalog.Encounters.Single(item => item.Id == neighbor.Id);
            var oldFoundation = loadedTarget.Foundation!;
            var newFoundation = appliedTarget.Foundation!;
            var targetPreserved = appliedTarget.Id == loadedTarget.Id &&
                                  appliedTarget.SchemaVersion == loadedTarget.SchemaVersion &&
                                  appliedTarget.AreaId == loadedTarget.AreaId &&
                                  appliedTarget.Provenance == Ux2LwLAuthoringProvenance &&
                                  ReferenceEquals(oldFoundation.Volume, newFoundation.Volume) &&
                                  ReferenceEquals(oldFoundation.Candidates, newFoundation.Candidates) &&
                                  ReferenceEquals(oldFoundation.SpawnAnchorIds, newFoundation.SpawnAnchorIds) &&
                                  ReferenceEquals(oldFoundation.RewardIds, newFoundation.RewardIds) &&
                                  ReferenceEquals(loadedTarget.Composition, appliedTarget.Composition) &&
                                  loadedTarget.CompositionRequiredAtRuntime == appliedTarget.CompositionRequiredAtRuntime &&
                                  ReferenceEquals(loadedTarget.Scenario, appliedTarget.Scenario) &&
                                  loadedTarget.ScenarioRequiredAtRuntime == appliedTarget.ScenarioRequiredAtRuntime &&
                                  ReferenceEquals(loadedTarget.Statistics, appliedTarget.Statistics) &&
                                  loadedTarget.StatisticsRequiredAtRuntime == appliedTarget.StatisticsRequiredAtRuntime &&
                                  ReferenceEquals(loadedTarget.Respawn, appliedTarget.Respawn) &&
                                  loadedTarget.SpawnFactionId == appliedTarget.SpawnFactionId &&
                                  ReferenceEquals(loadedTarget.Scheduler, appliedTarget.Scheduler) &&
                                  ReferenceEquals(loadedTarget.Reinforcements, appliedTarget.Reinforcements) &&
                                  ReferenceEquals(loadedTarget.Visibility, appliedTarget.Visibility) &&
                                  ReferenceEquals(loadedTarget.Cleanup, appliedTarget.Cleanup);
            var encounterNeighborsPreserved = ReferenceEquals(loadedNeighbor, appliedNeighbor);
            var projectNeighborsPreserved = ReferenceEquals(loadedProject.Module.Factions, appliedProject.Module.Factions) &&
                                            loadedProject.Blueprints.SequenceEqual(appliedProject.Blueprints) &&
                                            loadedProject.Areas.SequenceEqual(appliedProject.Areas) &&
                                            loadedProject.Dialogues.SequenceEqual(appliedProject.Dialogues) &&
                                            loadedProject.Quests.SequenceEqual(appliedProject.Quests);
            var collectionsPreserved = SameReferencesUx2LwL(initialCatalog.Placeables, appliedCatalog.Placeables) &&
                                       SameReferencesUx2LwL(initialCatalog.Doors, appliedCatalog.Doors) &&
                                       SameReferencesUx2LwL(initialCatalog.Triggers, appliedCatalog.Triggers) &&
                                       SameReferencesUx2LwL(initialCatalog.VehicleRoutes, appliedCatalog.VehicleRoutes) &&
                                       SameReferencesUx2LwL(initialCatalog.Merchants, appliedCatalog.Merchants) &&
                                       SameReferencesUx2LwL(initialCatalog.Companions, appliedCatalog.Companions) &&
                                       ReferenceEquals(initialCatalog.Scripts, appliedCatalog.Scripts);

            var undo = editing.Undo();
            var undoneProject = editing.Project!;
            var undone = undoneProject.Module.WorldFeatures.Encounters.Single(item => item.Id == target.Id);
            var redo = editing.Redo();
            var redoneProject = editing.Project!;
            var redone = redoneProject.Module.WorldFeatures.Encounters.Single(item => item.Id == target.Id);
            var undoRedo = undo.Succeeded && redo.Succeeded &&
                           EncounterProfileJsonUx2LwL(undone, undoneProject) ==
                           EncounterProfileJsonUx2LwL(loadedTarget, loadedProject) &&
                           EncounterProfileJsonUx2LwL(redone, redoneProject) ==
                           EncounterProfileJsonUx2LwL(appliedTarget, appliedProject);
            var saved = editing.Save();
            var opened = saved.Succeeded && reopened.Open(tempRoot).Succeeded;
            var persisted = opened
                ? reopened.Project!.Module.WorldFeatures.Encounters.Single(item => item.Id == target.Id)
                : null;
            var persistence = persisted is not null &&
                              EncounterProfileJsonUx2LwL(persisted, reopened.Project!) ==
                              EncounterProfileJsonUx2LwL(appliedTarget, appliedProject) &&
                              ProjectJsonSerializer.Serialize(editing.Project!) ==
                              ProjectJsonSerializer.Serialize(reopened.Project!);
            reopened.Close();
            var canonicalCommand = editing.History!.Journal.Count >= 3 &&
                                   editing.History.Journal.All(item =>
                                       item.CommandName == "set_world_feature_catalog");
            var structuredDiagnostics = new[] { invalidAnchor, invalidScript, invalidBudget, invalidCap }
                .SelectMany(item => item.Diagnostics)
                .All(item => !string.IsNullOrWhiteSpace(item.Code) && !string.IsNullOrWhiteSpace(item.Path));
            var humanText = editing.EncounterChoicesUx2LwL.All(item => IsHumanUx2LwLText(item.Label)) &&
                            preview.Coverage.All(item => IsHumanUx2LwLText(item.Section) &&
                                                         IsHumanUx2LwLText(item.Detail));
            var runtimeBoundary = preview.Boundary.Contains("offline", StringComparison.OrdinalIgnoreCase) &&
                                  preview.Boundary.Contains("aucune navigation", StringComparison.OrdinalIgnoreCase) &&
                                  preview.Boundary.Contains("récompense", StringComparison.OrdinalIgnoreCase) &&
                                  preview.Boundary.Contains("réseau", StringComparison.OrdinalIgnoreCase);
            var succeeded = explicitSelection && structuredDiagnostics && canonicalAggregation &&
                            realAnchorValidation && realScriptValidation && lifecycleValidation &&
                            tacticalValidation && honestUnresolvedCoverage && correctionRequired &&
                            dryRunPure && staleDraftBlocked && staleApprovalBlocked && canonicalCommand &&
                            undoRedo && persistence && targetPreserved && encounterNeighborsPreserved &&
                            projectNeighborsPreserved && collectionsPreserved && humanText && runtimeBoundary;
            return new(succeeded, explicitSelection, structuredDiagnostics, canonicalAggregation,
                realAnchorValidation, realScriptValidation, lifecycleValidation, tacticalValidation,
                honestUnresolvedCoverage, correctionRequired, dryRunPure, staleDraftBlocked,
                staleApprovalBlocked, canonicalCommand, undoRedo, persistence, targetPreserved,
                encounterNeighborsPreserved, projectNeighborsPreserved, collectionsPreserved,
                humanText, runtimeBoundary);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_LW_L_CONTRACT_FAILED exception={exception.Message}");
            return FailedUx2LwLSmoke();
        }
        finally
        {
            reopened.Close();
            editing.Close();
            try
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static ProjectDefinition ReplaceEncounterProjectUx2LwL(
        ProjectDefinition project,
        EncounterWorldProfile profile)
    {
        var catalog = ReplaceEncounterUx2LwL(project.Module.WorldFeatures, profile);
        return new(project.Id, project.Title, project.Module.WithWorldFeatures(catalog),
            project.Blueprints, project.Areas, project.Dialogues, project.Quests);
    }

    private static bool ApplyUx2LwLIntermediateMutation(
        StudioSessionController session,
        Guid profileId,
        double delta)
    {
        var history = session.History;
        var project = session.Project;
        if (history is null || project is null) return false;
        var current = project.Module.WorldFeatures.Encounters.Single(item => item.Id == profileId);
        var replacement = current with
        {
            Cleanup = current.Cleanup with
            {
                AbandonedAfterSeconds = current.Cleanup.AbandonedAfterSeconds + delta,
            },
        };
        var catalog = ReplaceEncounterUx2LwL(project.Module.WorldFeatures, replacement);
        var result = new SetWorldFeatureCatalogHandler(new SystemClock()).Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, catalog, false));
        if (!result.Succeeded) return false;
        history.Record(result);
        return true;
    }

    private static string EncounterProfileJsonUx2LwL(
        EncounterWorldProfile profile,
        ProjectDefinition project)
    {
        var source = project.Module.WorldFeatures;
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, source.Triggers, [profile], source.VehicleRoutes,
            source.Merchants, source.Companions, source.Scripts);
        return ProjectJsonSerializer.Serialize(new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(catalog), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests));
    }

    private static StudioUx2LwLSmokeResult FailedUx2LwLSmoke() => new(
        false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false);
}
