using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Creatures;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetCreatureFoundationHandler _setCreatureMovementUx4K = new(new SystemClock());
    private StudioCreatureMovementDraftUx4K? _approvedCreatureMovementDraftUx4K;
    private CreatureFoundationProfile? _approvedCreatureMovementCandidateUx4K;
    private ProjectRevision? _approvedCreatureMovementRevisionUx4K;
    private string? _approvedCreatureMovementProjectFingerprintUx4K;
    private string? _approvedCreatureMovementDraftFingerprintUx4K;
    private string? _approvedCreatureMovementCandidateFingerprintUx4K;

    internal IReadOnlyList<StudioCreatureMovementChoiceUx4K> CreatureMovementChoicesUx4K =>
        Project?.Blueprints.Where(item =>
                (item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn) &&
                item.CreatureFoundation is not null)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioCreatureMovementChoiceUx4K(item.Id,
                $"{item.DisplayName} · {item.CreatureFoundation!.Movement.Mode} · " +
                $"{item.CreatureFoundation.Movement.MaximumSpeed:R} m/s", item.IsReadOnly())).ToArray() ?? [];

    internal StudioCreatureMovementDraftUx4K? CreateCreatureMovementDraftUx4K(BlueprintId blueprintId)
    {
        var project = Project; var history = History;
        var blueprint = project?.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        if (project is null || history is null || blueprint?.CreatureFoundation is null || blueprint.IsReadOnly() ||
            blueprint.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn)) return null;
        var movement = blueprint.CreatureFoundation.Movement;
        return new(history.State.Revision, FingerprintProjectUx4K(project), blueprint.Id, blueprint,
            blueprint.CreatureFoundation, movement.Mode, FormatUx4K(movement.MaximumSpeed),
            FormatUx4K(movement.Acceleration), FormatUx4K(movement.Deceleration),
            FormatUx4K(movement.MinimumAltitude), FormatUx4K(movement.MaximumAltitude),
            FormatUx4K(movement.MaximumSlopeDegrees));
    }

    internal StudioCreatureMovementPreviewUx4K PreviewCreatureMovementUx4K(StudioCreatureMovementDraftUx4K draft)
    {
        ArgumentNullException.ThrowIfNull(draft); CancelCreatureMovementUx4K();
        var project = Project; var history = History;
        if (project is null || history is null)
            return FailedCreatureMovementUx4K("Aucun module n’est ouvert.", "studio.creature.project_missing");
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintProjectUx4K(project), draft.SourceProjectFingerprint, StringComparison.Ordinal))
            return FailedCreatureMovementUx4K("Le projet a changé ; reconstruisez l’aperçu.",
                "command.revision_conflict");
        var source = project.Blueprints.SingleOrDefault(item => item.Id == draft.BlueprintId);
        if (source?.CreatureFoundation is null)
            return FailedCreatureMovementUx4K("La fondation de la créature n’existe plus.",
                "creature.foundation.blueprint_missing");
        if (!TryBuildCreatureMovementCandidateUx4K(draft, source.CreatureFoundation, out var candidate,
                out var movement, out var diagnostics))
            return new(false, false, "Les valeurs de déplacement sont invalides.", string.Empty, string.Empty,
                diagnostics, string.Empty, "Runtime non évalué.");
        var validation = CreatureFoundationValidator.Validate(candidate,
            $"blueprints/{source.Id}/creatureFoundation");
        if (validation.Count > 0)
            return new(false, false, "La fondation refuse ce déplacement.", string.Empty, string.Empty,
                validation.Select(CreatureMovementDiagnosticUx4K).ToArray(), MovementSummaryUx4K(movement),
                "Runtime non évalué.");
        var dryRun = _setCreatureMovementUx4K.Handle(history.State, new(CommandId.From(Guid.NewGuid()),
            history.State.Revision, draft.BlueprintId, candidate, DryRun: true));
        if (!dryRun.Succeeded)
            return new(false, false, "La commande canonique refuse cet aperçu.", string.Empty, string.Empty,
                CreatureMovementDiagnosticsUx4K(dryRun.Diagnostics), MovementSummaryUx4K(movement),
                "Runtime non évalué.");
        var candidateProject = WithCreatureMovementFoundationUx4K(project, draft.BlueprintId, candidate);
        var script = CreatureFoundationScriptService.Read(candidateProject, draft.BlueprintId);
        var resolved = CreatureFoundationRuntimeResolver.Resolve(candidate, candidate.RequiredAtRuntime);
        if (script is null || !resolved.Succeeded || resolved.Profile is null ||
            !string.Equals(script.MovementMode, movement.Mode.ToString(), StringComparison.Ordinal))
            return FailedCreatureMovementUx4K("La projection typée refuse ce déplacement.",
                "creature.foundation.movement_runtime_invalid");
        var frozen = FreezeCreatureMovementDraftUx4K(draft);
        _approvedCreatureMovementDraftUx4K = frozen;
        _approvedCreatureMovementCandidateUx4K = candidate;
        _approvedCreatureMovementRevisionUx4K = history.State.Revision;
        _approvedCreatureMovementProjectFingerprintUx4K = FingerprintProjectUx4K(project);
        _approvedCreatureMovementDraftFingerprintUx4K = FingerprintCreatureMovementDraftUx4K(frozen);
        _approvedCreatureMovementCandidateFingerprintUx4K =
            FingerprintCreatureMovementCandidateUx4K(project, draft.BlueprintId, candidate);
        var accelerated = Math.Min(movement.MaximumSpeed, movement.Acceleration * .5);
        var braked = Math.Max(0, movement.MaximumSpeed - movement.Deceleration * .5);
        var runtimeSummary = $"Projection typée : mode {movement.Mode}, accélération 0,5 s → " +
                             $"{accelerated:0.###} m/s, freinage 0,5 s → {braked:0.###} m/s. " +
                             "Aucun acteur ni moteur physique n’est exécuté.";
        return new(true, true, dryRun.Patch?.Summary ?? "Prévisualiser le déplacement.",
            dryRun.Patch?.Summary ?? "Définir le déplacement.",
            dryRun.Inverse?.Summary ?? "Restaurer le déplacement précédent.", [],
            MovementSummaryUx4K(movement), runtimeSummary);
    }

    internal StudioCreatureMovementActionResultUx4K ApplyApprovedCreatureMovementUx4K(
        StudioCreatureMovementDraftUx4K currentDraft)
    {
        ArgumentNullException.ThrowIfNull(currentDraft);
        var project = Project; var history = History; var approved = _approvedCreatureMovementDraftUx4K;
        var candidate = _approvedCreatureMovementCandidateUx4K;
        if (project is null || history is null || approved is null || candidate is null ||
            _approvedCreatureMovementRevisionUx4K is null ||
            _approvedCreatureMovementProjectFingerprintUx4K is null ||
            _approvedCreatureMovementDraftFingerprintUx4K is null ||
            _approvedCreatureMovementCandidateFingerprintUx4K is null)
            return new(false, "Prévisualisez le brouillon avant de l’appliquer.");
        var stale = history.State.Revision != _approvedCreatureMovementRevisionUx4K ||
                    !string.Equals(FingerprintProjectUx4K(project),
                        _approvedCreatureMovementProjectFingerprintUx4K, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureMovementDraftUx4K(currentDraft),
                        _approvedCreatureMovementDraftFingerprintUx4K, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureMovementDraftUx4K(approved),
                        _approvedCreatureMovementDraftFingerprintUx4K, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureMovementCandidateUx4K(project, currentDraft.BlueprintId,
                            candidate), _approvedCreatureMovementCandidateFingerprintUx4K, StringComparison.Ordinal);
        if (stale)
            return new(false, "Le brouillon, le projet ou l’approbation a changé ; prévisualisez à nouveau.");
        var result = _setCreatureMovementUx4K.Handle(history.State, new(CommandId.From(Guid.NewGuid()),
            history.State.Revision, currentDraft.BlueprintId, candidate, DryRun: false));
        if (!result.Succeeded)
            return new(false, CreatureMovementDiagnosticsUx4K(result.Diagnostics).FirstOrDefault()?.HumanMessage ??
                              "La commande canonique a refusé le déplacement.");
        history.Record(result); InvalidateContentIndexes(); CancelCreatureMovementUx4K();
        return new(true, result.Patch?.Summary ?? "Déplacement appliqué.");
    }

    internal StudioCreatureMovementActionResultUx4K UndoCreatureMovementUx4K()
    { var result = Undo(); return new(result.Succeeded, result.Succeeded ? "Déplacement annulé." : "Aucune action à annuler."); }
    internal StudioCreatureMovementActionResultUx4K RedoCreatureMovementUx4K()
    { var result = Redo(); return new(result.Succeeded, result.Succeeded ? "Déplacement rétabli." : "Aucune action à rétablir."); }
    internal void CancelCreatureMovementUx4K()
    {
        _approvedCreatureMovementDraftUx4K = null; _approvedCreatureMovementCandidateUx4K = null;
        _approvedCreatureMovementRevisionUx4K = null; _approvedCreatureMovementProjectFingerprintUx4K = null;
        _approvedCreatureMovementDraftFingerprintUx4K = null;
        _approvedCreatureMovementCandidateFingerprintUx4K = null;
    }
    internal void CloseUx4K() => CancelCreatureMovementUx4K();
    internal StudioCreatureMovementApprovalSnapshotUx4K CaptureCreatureMovementApprovalUx4K() =>
        new(_approvedCreatureMovementDraftUx4K, _approvedCreatureMovementCandidateUx4K,
            _approvedCreatureMovementRevisionUx4K, _approvedCreatureMovementProjectFingerprintUx4K,
            _approvedCreatureMovementDraftFingerprintUx4K, _approvedCreatureMovementCandidateFingerprintUx4K);
    internal void RestoreCreatureMovementApprovalUx4K(StudioCreatureMovementApprovalSnapshotUx4K snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot); _approvedCreatureMovementDraftUx4K = snapshot.Draft;
        _approvedCreatureMovementCandidateUx4K = snapshot.CandidateFoundation;
        _approvedCreatureMovementRevisionUx4K = snapshot.Revision;
        _approvedCreatureMovementProjectFingerprintUx4K = snapshot.ProjectFingerprint;
        _approvedCreatureMovementDraftFingerprintUx4K = snapshot.DraftFingerprint;
        _approvedCreatureMovementCandidateFingerprintUx4K = snapshot.CandidateFingerprint;
    }

    internal StudioCreatureMovementEvidenceProjectUx4K? CreateUx4KEvidenceProject()
    {
        var project = Project; if (project is null) return null;
        var real = project.Blueprints.FirstOrDefault(item => !item.IsReadOnly() &&
            (item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn) &&
            item.CreatureFoundation is { } profile && CreatureFoundationValidator.Validate(profile).Count == 0);
        if (string.Equals(project.Module.Slug, "crypt-of-ashes", StringComparison.Ordinal) && real is not null)
            return new(project, real.Id, true, false);
        var fallback = CreateUx4DEvidenceProject();
        if (fallback is null) return null;
        var target = fallback.Project.Blueprints.Single(item => item.Id == fallback.BlueprintId);
        if (target.CreatureFoundation is null) return null;
        return new(fallback.Project, target.Id, false, true);
    }

    internal StudioUx4KSmokeResult RunUx4KContractSmoke()
    {
        var empty = new StudioUx4KSmokeResult(false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false);
        var evidence = CreateUx4KEvidenceProject(); if (evidence is null) return empty;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4k-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController(); var reopened = new StudioSessionController();
        StudioUx4KSmokeResult Execute()
        {
            try
            {
                if (!ProjectSession.Create(root, evidence.Project).Succeeded || !editing.Open(root).Succeeded) return empty;
                var explicitSelection = editing.CreatureMovementChoicesUx4K.Count > 0 &&
                                        editing.CreatureMovementChoicesUx4K.All(item =>
                                            !item.Label.Contains(item.BlueprintId.ToString(), StringComparison.OrdinalIgnoreCase));
                var draft = editing.CreateCreatureMovementDraftUx4K(evidence.BlueprintId);
                if (draft is null) return empty with { ExplicitSelection = explicitSelection };
                var changed = ChangedCreatureMovementDraftUx4K(draft);
                var sourceProject = editing.Project!; var sourceHistory = editing.History!;
                var preview = editing.PreviewCreatureMovementUx4K(changed);
                var dryRun = preview.Succeeded && ReferenceEquals(sourceProject, editing.Project) &&
                             ReferenceEquals(sourceHistory, editing.History) && sourceHistory.State.Revision == draft.BaseRevision;
                var runtimeProjection = preview.Succeeded && preview.RuntimeSummary.Contains("Projection typée", StringComparison.Ordinal) &&
                                        preview.RuntimeSummary.Contains("Aucun acteur", StringComparison.Ordinal);
                var allModes = Enum.GetValues<CreatureFoundationMovementMode>().All(mode =>
                {
                    var modeDraft = changed with { Mode = mode, MinimumAltitude = "0", MaximumAltitude = mode == CreatureFoundationMovementMode.Flying ? "40" : "0" };
                    return editing.PreviewCreatureMovementUx4K(modeDraft).Succeeded;
                });
                var invalid = editing.PreviewCreatureMovementUx4K(changed with { MaximumSpeed = "0" });
                var invalidBlocked = !invalid.Succeeded && invalid.Diagnostics.Any(item =>
                    item.Code is "creature.foundation.movement_invalid" or "studio.creature.movement_number_invalid");
                var invalidFlight = editing.PreviewCreatureMovementUx4K(changed with
                { Mode = CreatureFoundationMovementMode.Flying, MinimumAltitude = "40", MaximumAltitude = "10" });
                var flightBlocked = !invalidFlight.Succeeded && invalidFlight.Diagnostics.Any(item =>
                    item.Code is "creature.foundation.movement_invalid" or "creature.foundation.flight_altitude_invalid");

                var approvedBeforeRevision = editing.PreviewCreatureMovementUx4K(changed);
                var neighborChanged = ToggleCreatureMovementNeighborUx4K(editing, evidence.BlueprintId);
                var staleDraftBlocked = approvedBeforeRevision.Succeeded && neighborChanged &&
                                        !editing.ApplyApprovedCreatureMovementUx4K(changed).Succeeded;
                if (neighborChanged) editing.Undo();

                var fresh = editing.CreateCreatureMovementDraftUx4K(evidence.BlueprintId)!;
                var freshChanged = ChangedCreatureMovementDraftUx4K(fresh);
                var approvedBeforeTamper = editing.PreviewCreatureMovementUx4K(freshChanged);
                var staleApprovalBlocked = approvedBeforeTamper.Succeeded &&
                                           !editing.ApplyApprovedCreatureMovementUx4K(freshChanged with
                                           { MaximumSlopeDegrees = "44.5" }).Succeeded;

                fresh = editing.CreateCreatureMovementDraftUx4K(evidence.BlueprintId)!;
                freshChanged = ChangedCreatureMovementDraftUx4K(fresh);
                var beforeApply = editing.Project!;
                var beforeFoundation = beforeApply.Blueprints.Single(item => item.Id == evidence.BlueprintId)
                    .CreatureFoundation!;
                var finalPreview = editing.PreviewCreatureMovementUx4K(freshChanged);
                var appliedAction = finalPreview.Succeeded
                    ? editing.ApplyApprovedCreatureMovementUx4K(freshChanged)
                    : new StudioCreatureMovementActionResultUx4K(false, "preview failed");
                var appliedProject = editing.Project;
                var appliedMovement = appliedProject?.Blueprints.Single(item => item.Id == evidence.BlueprintId)
                    .CreatureFoundation?.Movement;
                var canonical = appliedAction.Succeeded && appliedMovement is not null &&
                                appliedMovement.Mode == CreatureFoundationMovementMode.Flying &&
                                appliedMovement.MaximumSpeed == 9.25 && appliedMovement.Acceleration == 13.5 &&
                                appliedMovement.Deceleration == 7.75 && appliedMovement.MinimumAltitude == 2.5 &&
                                appliedMovement.MaximumAltitude == 48.125 && appliedMovement.MaximumSlopeDegrees == 42.25;
                var expectedFoundation = WithCreatureMovementUx4K(beforeFoundation, appliedMovement!);
                var neighbors = canonical && ProjectJsonSerializer.Serialize(WithCreatureMovementFoundationUx4K(
                                    beforeApply, evidence.BlueprintId, expectedFoundation)) ==
                                ProjectJsonSerializer.Serialize(appliedProject!);
                var afterApplyJson = appliedProject is null ? string.Empty : ProjectJsonSerializer.Serialize(appliedProject);
                var undo = editing.UndoCreatureMovementUx4K(); var undoRestored = ReferenceEquals(beforeApply, editing.Project);
                var redo = editing.RedoCreatureMovementUx4K(); var redoRestored = ReferenceEquals(appliedProject, editing.Project);
                var undoRedo = undo.Succeeded && redo.Succeeded && undoRestored && redoRestored;
                var saved = undoRedo && editing.Save().Succeeded; editing.Close();
                var reopenedOk = saved && reopened.Open(root).Succeeded;
                var persistence = reopenedOk && ProjectJsonSerializer.Serialize(reopened.Project!) == afterApplyJson;
                reopened.Close();
                var completeValues = canonical && finalPreview.MovementSummary.Contains("48.125", StringComparison.Ordinal);
                var succeeded = explicitSelection && allModes && completeValues && invalidBlocked && flightBlocked &&
                                dryRun && runtimeProjection && staleDraftBlocked && staleApprovalBlocked && canonical &&
                                undoRedo && persistence && neighbors;
                return new(succeeded, explicitSelection, allModes, completeValues, invalidBlocked, flightBlocked,
                    dryRun, runtimeProjection, staleDraftBlocked, staleApprovalBlocked, canonical, undoRedo,
                    persistence, neighbors, false);
            }
            catch { return empty; }
            finally { editing.Close(); reopened.Close(); }
        }
        var result = Execute(); var released = DeleteUx4KSmokeRoot(root);
        return result with { TemporaryWorkspaceReleased = released, Succeeded = result.Succeeded && released };
    }

    private static StudioCreatureMovementDraftUx4K ChangedCreatureMovementDraftUx4K(
        StudioCreatureMovementDraftUx4K draft) => draft with
        {
            Mode = CreatureFoundationMovementMode.Flying,
            MaximumSpeed = "9.25",
            Acceleration = "13.5",
            Deceleration = "7.75",
            MinimumAltitude = "2.5",
            MaximumAltitude = "48.125",
            MaximumSlopeDegrees = "42.25",
        };

    private static bool TryBuildCreatureMovementCandidateUx4K(StudioCreatureMovementDraftUx4K draft,
        CreatureFoundationProfile source, out CreatureFoundationProfile candidate,
        out CreatureFoundationMovement movement, out IReadOnlyList<StudioCreatureMovementDiagnosticUx4K> diagnostics)
    {
        candidate = source; movement = source.Movement;
        var parsed = TryDoubleUx4K(draft.MaximumSpeed, out var maximumSpeed) &
                     TryDoubleUx4K(draft.Acceleration, out var acceleration) &
                     TryDoubleUx4K(draft.Deceleration, out var deceleration) &
                     TryDoubleUx4K(draft.MinimumAltitude, out var minimumAltitude) &
                     TryDoubleUx4K(draft.MaximumAltitude, out var maximumAltitude) &
                     TryDoubleUx4K(draft.MaximumSlopeDegrees, out var maximumSlope);
        if (!parsed)
        {
            diagnostics = [new("studio.creature.movement_number_invalid", "creatureFoundation/movement",
                "Vitesse, accélération, décélération, altitudes et pente doivent être des nombres finis.")];
            return false;
        }
        movement = new(draft.Mode, maximumSpeed, acceleration, deceleration, minimumAltitude, maximumAltitude,
            maximumSlope);
        candidate = WithCreatureMovementUx4K(source, movement); diagnostics = [];
        return true;
    }

    private static CreatureFoundationProfile WithCreatureMovementUx4K(CreatureFoundationProfile source,
        CreatureFoundationMovement movement) => new(source.Id, source.SchemaVersion, source.Provenance,
        source.RequiredAtRuntime, source.Identity, source.DefaultTransform, source.Resources, movement,
        source.References, source.Spawned, source.Overrides, source.AuthoringComposition,
        source.AuthoringCompositionRequiredAtRuntime, source.RulesComposition,
        source.RulesCompositionRequiredAtRuntime, source.InteractionComposition,
        source.InteractionCompositionRequiredAtRuntime, source.EventAiComposition,
        source.EventAiCompositionRequiredAtRuntime);

    private static ProjectDefinition WithCreatureMovementFoundationUx4K(ProjectDefinition project,
        BlueprintId blueprintId, CreatureFoundationProfile foundation) => new(project.Id, project.Title, project.Module,
        project.Blueprints.Select(item => item.Id == blueprintId ? item.WithCreatureFoundation(foundation) : item).ToArray(),
        project.Areas, project.Dialogues, project.Quests);

    private static bool ToggleCreatureMovementNeighborUx4K(StudioSessionController session, BlueprintId blueprintId)
    {
        var project = session.Project; var history = session.History;
        var source = project?.Blueprints.SingleOrDefault(item => item.Id == blueprintId)?.CreatureFoundation;
        if (project is null || history is null || source is null || source.Resources.Count == 0) return false;
        var resources = source.Resources.Select((item, index) => index == 0
            ? item with { Persistent = !item.Persistent }
            : item).ToArray();
        var changed = new CreatureFoundationProfile(source.Id, source.SchemaVersion, source.Provenance,
            source.RequiredAtRuntime, source.Identity, source.DefaultTransform, resources, source.Movement,
            source.References, source.Spawned, source.Overrides, source.AuthoringComposition,
            source.AuthoringCompositionRequiredAtRuntime, source.RulesComposition,
            source.RulesCompositionRequiredAtRuntime, source.InteractionComposition,
            source.InteractionCompositionRequiredAtRuntime, source.EventAiComposition,
            source.EventAiCompositionRequiredAtRuntime);
        var result = new SetCreatureFoundationHandler(new SystemClock()).Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, blueprintId, changed, DryRun: false));
        if (!result.Succeeded) return false; history.Record(result); session.InvalidateContentIndexes(); return true;
    }

    private static StudioCreatureMovementDraftUx4K FreezeCreatureMovementDraftUx4K(
        StudioCreatureMovementDraftUx4K draft) => draft with { };
    private static StudioCreatureMovementPreviewUx4K FailedCreatureMovementUx4K(string message, string code) =>
        new(false, false, message, string.Empty, string.Empty,
            [new(code, "creatureFoundation/movement", HumanCreatureMovementMessageUx4K(code, message))],
            string.Empty, "Runtime non évalué.");
    private static StudioCreatureMovementDiagnosticUx4K CreatureMovementDiagnosticUx4K(
        CreatureFoundationDiagnostic item) => new(item.Code, item.Path,
        HumanCreatureMovementMessageUx4K(item.Code, item.Message));
    private static IReadOnlyList<StudioCreatureMovementDiagnosticUx4K> CreatureMovementDiagnosticsUx4K(
        IReadOnlyList<CommandDiagnostic> diagnostics) => diagnostics.Select(item =>
        {
            var separator = item.Message.IndexOf(": ", StringComparison.Ordinal);
            var path = separator > 0 ? item.Message[..separator] : "creatureFoundation/movement";
            var message = separator > 0 ? item.Message[(separator + 2)..] : item.Message;
            return new StudioCreatureMovementDiagnosticUx4K(item.Code, path,
                HumanCreatureMovementMessageUx4K(item.Code, message));
        }).ToArray();
    private static string HumanCreatureMovementMessageUx4K(string code, string fallback) => code switch
    {
        "creature.foundation.movement_invalid" =>
            "Le mode et les valeurs de vitesse, accélération, altitude et pente doivent rester finis et bornés.",
        "creature.foundation.flight_altitude_invalid" =>
            "Une créature volante exige une altitude maximale strictement supérieure à l’altitude minimale.",
        _ => fallback,
    };
    private static string MovementSummaryUx4K(CreatureFoundationMovement movement) =>
        $"{movement.Mode} · vitesse {FormatUx4K(movement.MaximumSpeed)} m/s · " +
        $"accélération {FormatUx4K(movement.Acceleration)} · " +
        $"décélération {FormatUx4K(movement.Deceleration)} · " +
        $"altitude {FormatUx4K(movement.MinimumAltitude)}..{FormatUx4K(movement.MaximumAltitude)} m · " +
        $"pente {FormatUx4K(movement.MaximumSlopeDegrees)}°.";
    private static bool TryDoubleUx4K(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    private static string FormatUx4K(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string FingerprintProjectUx4K(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));
    private static string FingerprintCreatureMovementCandidateUx4K(ProjectDefinition project, BlueprintId id,
        CreatureFoundationProfile candidate) => FingerprintProjectUx4K(WithCreatureMovementFoundationUx4K(project, id, candidate));
    private static string FingerprintCreatureMovementDraftUx4K(StudioCreatureMovementDraftUx4K draft)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(draft.BaseRevision.Value); WriteUx4K(writer, draft.SourceProjectFingerprint);
        WriteUx4K(writer, draft.BlueprintId.ToString()); writer.Write((int)draft.Mode);
        WriteUx4K(writer, draft.MaximumSpeed); WriteUx4K(writer, draft.Acceleration);
        WriteUx4K(writer, draft.Deceleration); WriteUx4K(writer, draft.MinimumAltitude);
        WriteUx4K(writer, draft.MaximumAltitude); WriteUx4K(writer, draft.MaximumSlopeDegrees);
        writer.Flush(); return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
    private static void WriteUx4K(BinaryWriter writer, string value)
    { var bytes = Encoding.UTF8.GetBytes(value); writer.Write(bytes.Length); writer.Write(bytes); }
    private static bool DeleteUx4KSmokeRoot(string root)
    {
        try
        {
            var full = Path.GetFullPath(root); var temp = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4k-smoke-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, true); return !Directory.Exists(full);
        }
        catch { return false; }
    }
}
