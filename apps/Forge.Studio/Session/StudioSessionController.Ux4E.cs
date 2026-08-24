using System.Security.Cryptography;
using System.Text;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
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
    private readonly SetCreaturePresentationHandler _setCreaturePresentationUx4E = new(new SystemClock());
    private StudioCreaturePresentationDraftUx4E? _approvedCreaturePresentationDraftUx4E;
    private CreaturePresentationProfile? _approvedCreaturePresentationCandidateUx4E;
    private ProjectRevision? _approvedCreaturePresentationRevisionUx4E;
    private string? _approvedCreaturePresentationProjectFingerprintUx4E;
    private string? _approvedCreaturePresentationDraftFingerprintUx4E;
    private string? _approvedCreaturePresentationCandidateFingerprintUx4E;

    internal IReadOnlyList<StudioCreaturePresentationChoiceUx4E> CreaturePresentationChoicesUx4E => Project?
        .Blueprints.Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
        .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
        .ThenBy(item => item.Slug, StringComparer.Ordinal)
        .Select(item => new StudioCreaturePresentationChoiceUx4E(
            item.Id,
            $"{item.DisplayName} · {(item.Kind == BlueprintKind.PlayerSpawn ? "joueur" : "créature")} · " +
            (item.CreaturePresentation is null ? "présentation absente" : "présentation prête"),
            item.CreaturePresentation is not null,
            item.IsReadOnly()))
        .ToArray() ?? [];

    internal StudioCreaturePresentationDraftUx4E? CreateCreaturePresentationDraftUx4E(BlueprintId blueprintId)
    {
        var project = Project;
        var history = History;
        var blueprint = project?.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        if (project is null || history is null || blueprint is null || blueprint.IsReadOnly() ||
            blueprint.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn))
        {
            return null;
        }

        var source = blueprint.CreaturePresentation;
        var profile = source ?? CreaturePresentationProfile.CreateDefault(
            StableCreaturePresentationIdUx4E(blueprint.Id.Value, "presentation"), "studio:ux4-e");
        return new StudioCreaturePresentationDraftUx4E(
            history.State.Revision,
            FingerprintProjectUx4E(project),
            blueprint.Id,
            blueprint,
            source,
            profile.GenderId,
            profile.BodyTypeId,
            profile.Pronouns.Subject,
            profile.Pronouns.Object,
            profile.Pronouns.Possessive,
            profile.Pronouns.Reflexive,
            profile.UseModelFallback,
            profile.RigId,
            profile.Portraits.ToArray(),
            profile.BodySlots.ToArray(),
            profile.MaterialColors.ToArray(),
            profile.Attachments.ToArray());
    }

    internal StudioCreaturePresentationPreviewUx4E PreviewCreaturePresentationUx4E(
        StudioCreaturePresentationDraftUx4E draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelCreaturePresentationUx4E();
        var project = Project;
        var history = History;
        if (project is null || history is null)
            return FailedCreaturePresentationPreviewUx4E("Aucun module n’est ouvert.", "studio.creature.project_missing");
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintProjectUx4E(project), draft.SourceProjectFingerprint, StringComparison.Ordinal))
        {
            return FailedCreaturePresentationPreviewUx4E(
                "Le projet a changé depuis l’ouverture de la fiche. Rechargez-la avant de prévisualiser.",
                SetCreaturePresentationHandler.RevisionConflictCode);
        }
        if (!TryBuildCreaturePresentationCandidateUx4E(draft, out var candidate, out var diagnostics))
        {
            return new(false, false, "La présentation contient des valeurs invalides.", string.Empty, string.Empty,
                diagnostics, IdentitySummaryUx4E(draft), PortraitSummaryUx4E(draft), AppearanceSummaryUx4E(draft),
                "Runtime non évalué.");
        }

        var command = new SetCreaturePresentationCommand(
            CommandId.From(Guid.NewGuid()), draft.BaseRevision, draft.BlueprintId, candidate, DryRun: true);
        var dryRun = _setCreaturePresentationUx4E.Handle(history.State, command);
        if (!dryRun.Succeeded)
        {
            return new(false, false, "La commande canonique refuse la présentation.", string.Empty, string.Empty,
                CreaturePresentationDiagnosticsUx4E(dryRun.Diagnostics), IdentitySummaryUx4E(draft),
                PortraitSummaryUx4E(draft), AppearanceSummaryUx4E(draft), "Runtime non évalué.");
        }

        var runtime = CreaturePresentationRuntimeResolver.Resolve(candidate, required: true);
        if (!runtime.Succeeded || runtime.Profile is null)
        {
            return new(false, false, "La résolution runtime refuse la présentation.", string.Empty, string.Empty,
                runtime.Diagnostics.Select(CreaturePresentationDiagnosticUx4E).ToArray(), IdentitySummaryUx4E(draft),
                PortraitSummaryUx4E(draft), AppearanceSummaryUx4E(draft), "Runtime refusé.");
        }

        var frozen = FreezeCreaturePresentationDraftUx4E(draft);
        _approvedCreaturePresentationDraftUx4E = frozen;
        _approvedCreaturePresentationCandidateUx4E = candidate;
        _approvedCreaturePresentationRevisionUx4E = history.State.Revision;
        _approvedCreaturePresentationProjectFingerprintUx4E = FingerprintProjectUx4E(project);
        _approvedCreaturePresentationDraftFingerprintUx4E = FingerprintCreaturePresentationDraftUx4E(frozen);
        _approvedCreaturePresentationCandidateFingerprintUx4E =
            FingerprintCreaturePresentationCandidateUx4E(project, draft.BlueprintId, candidate);
        return new(true, true, "Présentation valide · prête à être appliquée.",
            dryRun.Patch?.Summary ?? "Remplacer la présentation de créature.",
            dryRun.Inverse?.Summary ?? "Restaurer la présentation précédente.", [],
            IdentitySummaryUx4E(draft), PortraitSummaryUx4E(draft), AppearanceSummaryUx4E(draft),
            $"Profil résolu · repli modèle {candidate.UseModelFallback.ToString().ToLowerInvariant()} · voix voisine préservée");
    }

    internal StudioCreaturePresentationActionResultUx4E ApplyApprovedCreaturePresentationUx4E(
        StudioCreaturePresentationDraftUx4E currentDraft)
    {
        ArgumentNullException.ThrowIfNull(currentDraft);
        var project = Project;
        var history = History;
        var approvedDraft = _approvedCreaturePresentationDraftUx4E;
        var candidate = _approvedCreaturePresentationCandidateUx4E;
        if (project is null || history is null || approvedDraft is null || candidate is null ||
            _approvedCreaturePresentationRevisionUx4E is null ||
            _approvedCreaturePresentationProjectFingerprintUx4E is null ||
            _approvedCreaturePresentationDraftFingerprintUx4E is null ||
            _approvedCreaturePresentationCandidateFingerprintUx4E is null)
        {
            return new(false, "Prévisualisez la présentation avant de l’appliquer.");
        }

        var stale = history.State.Revision != _approvedCreaturePresentationRevisionUx4E ||
                    !string.Equals(FingerprintProjectUx4E(project),
                        _approvedCreaturePresentationProjectFingerprintUx4E, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreaturePresentationDraftUx4E(approvedDraft),
                        _approvedCreaturePresentationDraftFingerprintUx4E, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreaturePresentationDraftUx4E(currentDraft),
                        _approvedCreaturePresentationDraftFingerprintUx4E, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreaturePresentationCandidateUx4E(project, currentDraft.BlueprintId,
                            candidate), _approvedCreaturePresentationCandidateFingerprintUx4E,
                        StringComparison.Ordinal);
        if (stale)
        {
            CancelCreaturePresentationUx4E();
            return new(false, "La présentation ou le projet a changé depuis l’aperçu. Prévisualisez à nouveau.");
        }

        var dryRun = _setCreaturePresentationUx4E.Handle(history.State, new(
            CommandId.From(Guid.NewGuid()), history.State.Revision, currentDraft.BlueprintId, candidate, true));
        if (!dryRun.Succeeded)
        {
            CancelCreaturePresentationUx4E();
            return new(false, "La revalidation canonique a refusé la présentation.");
        }
        var result = _setCreaturePresentationUx4E.Handle(history.State, new(
            CommandId.From(Guid.NewGuid()), history.State.Revision, currentDraft.BlueprintId, candidate, false));
        CancelCreaturePresentationUx4E();
        if (!result.Succeeded) return new(false, "La commande n’a pas pu appliquer la présentation.");
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, "Présentation appliquée · Ctrl+Z pour annuler.");
    }

    internal StudioCreaturePresentationActionResultUx4E UndoCreaturePresentationUx4E()
    {
        CancelCreaturePresentationUx4E();
        var result = Undo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioCreaturePresentationActionResultUx4E RedoCreaturePresentationUx4E()
    {
        CancelCreaturePresentationUx4E();
        var result = Redo();
        return new(result.Succeeded, result.Message);
    }

    internal void CancelCreaturePresentationUx4E()
    {
        _approvedCreaturePresentationDraftUx4E = null;
        _approvedCreaturePresentationCandidateUx4E = null;
        _approvedCreaturePresentationRevisionUx4E = null;
        _approvedCreaturePresentationProjectFingerprintUx4E = null;
        _approvedCreaturePresentationDraftFingerprintUx4E = null;
        _approvedCreaturePresentationCandidateFingerprintUx4E = null;
    }

    internal void CloseUx4E() => CancelCreaturePresentationUx4E();

    internal StudioCreaturePresentationApprovalSnapshotUx4E CaptureCreaturePresentationApprovalUx4E() => new(
        _approvedCreaturePresentationDraftUx4E, _approvedCreaturePresentationCandidateUx4E,
        _approvedCreaturePresentationRevisionUx4E, _approvedCreaturePresentationProjectFingerprintUx4E,
        _approvedCreaturePresentationDraftFingerprintUx4E, _approvedCreaturePresentationCandidateFingerprintUx4E);

    internal void RestoreCreaturePresentationApprovalUx4E(StudioCreaturePresentationApprovalSnapshotUx4E snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedCreaturePresentationDraftUx4E = snapshot.Draft;
        _approvedCreaturePresentationCandidateUx4E = snapshot.Candidate;
        _approvedCreaturePresentationRevisionUx4E = snapshot.Revision;
        _approvedCreaturePresentationProjectFingerprintUx4E = snapshot.ProjectFingerprint;
        _approvedCreaturePresentationDraftFingerprintUx4E = snapshot.DraftFingerprint;
        _approvedCreaturePresentationCandidateFingerprintUx4E = snapshot.CandidateFingerprint;
    }

    internal string? ResolveCreaturePortraitAbsolutePathUx4E(string assetPath)
    {
        var root = Session?.RootDirectory;
        if (string.IsNullOrWhiteSpace(root) || !IsSafeRelativeAssetPathUx4E(assetPath)) return null;
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath)
            ? fullPath
            : null;
    }

    internal StudioCreaturePresentationEvidenceProjectUx4E? CreateUx4EEvidenceProject()
    {
        var project = Project;
        var root = Session?.RootDirectory;
        if (project is null || string.IsNullOrWhiteSpace(root)) return null;
        var editable = project.Blueprints.Where(item =>
                item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn && !item.IsReadOnly())
            .OrderByDescending(item => item.CreaturePresentation is not null)
            .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault();
        if (editable is null) return null;

        var real = editable.CreaturePresentation;
        if (real is not null && CreaturePresentationValidator.Validate(real).Count == 0 &&
            real.Portraits.FirstOrDefault() is { } realPortrait &&
            ResolveCreaturePortraitAbsolutePathUx4E(realPortrait.AssetPath) is { } realPath)
        {
            return new(project, editable.Id, true, false, realPortrait.AssetPath, File.ReadAllBytes(realPath));
        }

        var bytes = EvidencePortraitBytesUx4E();
        var presentation = CreateCanonicalEvidencePresentationUx4E(editable, bytes);
        return new(WithCreaturePresentationUx4E(project, editable.Id, presentation), editable.Id, false, true,
            presentation.Portraits[0].AssetPath, bytes);
    }

    internal StudioUx4ESmokeResult RunUx4EContractSmoke()
    {
        var result = new StudioUx4ESmokeResult(false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false);
        var evidence = CreateUx4EEvidenceProject();
        if (evidence is null) return result;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4e-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded ||
                !WriteEvidencePortraitUx4E(root, evidence.PortraitAssetPath, evidence.PortraitBytes) ||
                !editing.Open(root).Succeeded)
                return result;
            var explicitSelection = editing.CreaturePresentationChoicesUx4E.Count > 0;
            var draft = editing.CreateCreaturePresentationDraftUx4E(evidence.BlueprintId);
            if (draft is null) return result with { ExplicitSelection = explicitSelection };
            var changed = ChangedCreaturePresentationDraftUx4E(draft);
            var sourceProject = editing.Project!;
            var sourceHistory = editing.History!;
            var preview = editing.PreviewCreaturePresentationUx4E(changed);
            var dryRun = preview.Succeeded && ReferenceEquals(sourceProject, editing.Project) &&
                         ReferenceEquals(sourceHistory, editing.History) && sourceHistory.State.Revision == draft.BaseRevision;
            var invalid = editing.PreviewCreaturePresentationUx4E(changed with { GenderId = "?" });
            var invalidBlocked = !invalid.Succeeded && invalid.Diagnostics.Any(item =>
                item.Code == "studio.creature.presentation.identifier_noncanonical" ||
                item.Code == "creature.presentation.gender_invalid");

            var stalePreview = editing.PreviewCreaturePresentationUx4E(changed);
            var intermediate = ToggleCreaturePresentationFallbackUx4E(editing, evidence.BlueprintId);
            var staleDraft = editing.PreviewCreaturePresentationUx4E(changed);
            var staleDraftBlocked = intermediate && stalePreview.Succeeded && !staleDraft.Succeeded;

            var fresh = editing.CreateCreaturePresentationDraftUx4E(evidence.BlueprintId)!;
            var freshChanged = ChangedCreaturePresentationDraftUx4E(fresh);
            var approved = editing.PreviewCreaturePresentationUx4E(freshChanged);
            var secondIntermediate = ToggleCreaturePresentationFallbackUx4E(editing, evidence.BlueprintId);
            var staleApproval = editing.ApplyApprovedCreaturePresentationUx4E(freshChanged);
            var staleApprovalBlocked = approved.Succeeded && secondIntermediate && !staleApproval.Succeeded;

            fresh = editing.CreateCreaturePresentationDraftUx4E(evidence.BlueprintId)!;
            freshChanged = ChangedCreaturePresentationDraftUx4E(fresh);
            var expectedBuilt = editing.TryBuildCreaturePresentationCandidateUx4E(
                freshChanged, out var expectedCandidate, out _);
            var finalPreview = editing.PreviewCreaturePresentationUx4E(freshChanged);
            var beforeApply = editing.Project!;
            var apply = editing.ApplyApprovedCreaturePresentationUx4E(freshChanged);
            var appliedProject = editing.Project!;
            var appliedBlueprint = appliedProject.Blueprints.Single(item => item.Id == evidence.BlueprintId);
            var applied = appliedBlueprint.CreaturePresentation;
            var gender = applied is { GenderId: "nonbinary" } && applied.Pronouns.Subject == "iel" &&
                         applied.Pronouns.Reflexive == "soi-même";
            var portraits = applied is { UseModelFallback: false } && applied.Portraits.Count == 2 &&
                            applied.Portraits.Any(item => item.Expression == "angry" &&
                                item.AltText.Contains("colère", StringComparison.OrdinalIgnoreCase));
            var appearance = applied is { BodyTypeId: "humanoid-athletic", RigId: "rig.humanoid" };
            var bodyParts = applied?.BodySlots.Count == 2 &&
                            applied.BodySlots.Any(item => item.SlotId == "head" && item.HiddenSlots.Contains("hair"));
            var colorsAttachments = applied?.MaterialColors.Count == 2 && applied.Attachments.Count == 2 &&
                                    applied.Attachments.Any(item => item.Kind == "wings") &&
                                    applied.Attachments.Any(item => item.Kind == "tail");
            var neighbors = expectedBuilt &&
                            ProjectJsonSerializer.Serialize(WithCreaturePresentationUx4E(
                                beforeApply, evidence.BlueprintId, expectedCandidate)) ==
                            ProjectJsonSerializer.Serialize(appliedProject);
            var canonical = finalPreview.Succeeded && apply.Succeeded && applied is not null;
            var undo = editing.UndoCreaturePresentationUx4E();
            var undone = undo.Succeeded && ReferenceEquals(beforeApply, editing.Project);
            var redo = editing.RedoCreaturePresentationUx4E();
            var redone = redo.Succeeded && ReferenceEquals(appliedProject, editing.Project);
            var saved = redone && editing.Save().Succeeded;
            var persisted = saved && reopened.Open(root).Succeeded &&
                            ProjectJsonSerializer.Serialize(reopened.Project!) ==
                            ProjectJsonSerializer.Serialize(appliedProject);

            result = new(canonical && explicitSelection && gender && portraits && appearance && bodyParts &&
                         colorsAttachments && invalidBlocked && dryRun && staleDraftBlocked &&
                         staleApprovalBlocked && undone && redone && persisted && neighbors,
                explicitSelection, gender, portraits, appearance, bodyParts, colorsAttachments, invalidBlocked,
                dryRun, staleDraftBlocked, staleApprovalBlocked, canonical, undone && redone, persisted, neighbors,
                false);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX4E_CONTRACT_FAILED stage=exception " + exception);
        }
        finally
        {
            reopened.Close();
            editing.Close();
            var released = DeleteUx4ESmokeRoot(root);
            result = result with { Succeeded = result.Succeeded && released, TemporaryWorkspaceReleased = released };
        }
        return result;
    }

    private static StudioCreaturePresentationDraftUx4E ChangedCreaturePresentationDraftUx4E(
        StudioCreaturePresentationDraftUx4E draft)
    {
        var portraits = draft.Portraits
            .Select(item => item.Expression == "neutral" ? item with { AltText = "Portrait neutre de preuve" } : item)
            .Concat([new CreaturePresentationProfile.PortraitVariant("angry", "assets/portraits/ux4e-proof.bmp",
                32, 32, "CC0-1.0", Convert.ToHexString(SHA256.HashData(EvidencePortraitBytesUx4E())).ToLowerInvariant(),
                "Créature en colère")]).DistinctBy(item => $"{item.Expression}:{item.Width}x{item.Height}").ToArray();
        return draft with
        {
            GenderId = "nonbinary",
            BodyTypeId = "humanoid-athletic",
            PronounSubject = "iel",
            PronounObject = "iel",
            PronounPossessive = "son",
            PronounReflexive = "soi-même",
            UseModelFallback = false,
            RigId = "rig.humanoid",
            Portraits = portraits,
            BodySlots =
            [
                new("head", "asset.helmet", "rig.humanoid", 4, ["hair"], "hide-covered"),
                new("torso", "asset.armor", "rig.humanoid", 2, [], "replace"),
            ],
            MaterialColors = [new("primary", "#7A5CFF"), new("secondary", "#D68A3A")],
            Attachments =
            [
                new("back", "wings", "asset.wings", "rig.humanoid"),
                new("tail-base", "tail", "asset.tail", "rig.humanoid"),
            ],
        };
    }

    private bool TryBuildCreaturePresentationCandidateUx4E(
        StudioCreaturePresentationDraftUx4E draft,
        out CreaturePresentationProfile candidate,
        out IReadOnlyList<StudioCreaturePresentationDiagnosticUx4E> diagnostics)
    {
        var errors = new List<StudioCreaturePresentationDiagnosticUx4E>();
        foreach (var (value, path) in new[]
                 {
                     (draft.GenderId, "creature/presentation/gender"),
                     (draft.BodyTypeId, "creature/presentation/bodyType"),
                     (draft.RigId, "creature/presentation/rig"),
                 })
        {
            if (!IsCanonicalInputUx4E(value))
                errors.Add(new("studio.creature.presentation.identifier_noncanonical", path,
                    "Utilisez un identifiant en minuscules sans espace ni normalisation implicite."));
        }
        foreach (var value in draft.BodySlots.SelectMany(item => new[] { item.SlotId, item.RequiredRigId })
                     .Concat(draft.MaterialColors.Select(item => item.ParameterId))
                     .Concat(draft.Attachments.SelectMany(item => new[] { item.SlotId, item.Kind, item.RequiredRigId })))
        {
            if (!IsCanonicalInputUx4E(value))
                errors.Add(new("studio.creature.presentation.identifier_noncanonical", "creature/presentation/collections",
                    "Les identifiants de collection doivent être canoniques avant l’aperçu."));
        }
        var source = draft.SourcePresentation ?? CreaturePresentationProfile.CreateDefault(
            StableCreaturePresentationIdUx4E(draft.BlueprintId.Value, "presentation"), "studio:ux4-e");
        candidate = new CreaturePresentationProfile(source.Id, source.SchemaVersion, source.Provenance,
            draft.GenderId, draft.BodyTypeId,
            new(draft.PronounSubject, draft.PronounObject, draft.PronounPossessive, draft.PronounReflexive),
            draft.Portraits, draft.UseModelFallback, draft.RigId, draft.BodySlots, draft.MaterialColors,
            draft.Attachments, source.Voice);
        errors.AddRange(CreaturePresentationValidator.Validate(candidate,
                $"blueprints/{draft.BlueprintId}/creaturePresentation")
            .Select(CreaturePresentationDiagnosticUx4E));
        diagnostics = errors.GroupBy(item => (item.Code, item.Path)).Select(item => item.First()).ToArray();
        return diagnostics.Count == 0;
    }

    private static CreaturePresentationProfile CreateCanonicalEvidencePresentationUx4E(
        BlueprintDefinition blueprint,
        byte[] portraitBytes)
    {
        var hash = Convert.ToHexString(SHA256.HashData(portraitBytes)).ToLowerInvariant();
        return new(StableCreaturePresentationIdUx4E(blueprint.Id.Value, "presentation"),
            CreaturePresentationProfile.CurrentSchemaVersion, "studio:ux4-e-evidence", "woman", "humanoid-medium",
            new("elle", "elle", "son", "elle-même"),
            [new("neutral", "assets/portraits/ux4e-proof.bmp", 32, 32, "CC0-1.0", hash,
                $"Portrait de {blueprint.DisplayName}")],
            false, "rig.humanoid",
            [new("head", "asset.hood", "rig.humanoid", 3, ["hair"], "hide-covered")],
            [new("primary", "#D68A3A")],
            [new("back", "wings", "asset.wings", "rig.humanoid")],
            new("voice.protected", "fr-FR", ["audio.protected"], ["calm"], null, ["default"],
                "CC0-1.0", "fixture-protected"));
    }

    private static ProjectDefinition WithCreaturePresentationUx4E(
        ProjectDefinition project,
        BlueprintId blueprintId,
        CreaturePresentationProfile presentation)
    {
        var blueprints = project.Blueprints.Select(item => item.Id == blueprintId
            ? new BlueprintDefinition(item.Id, item.Slug, item.DisplayName, item.Kind, item.Combat,
                item.PaletteOrigin, item.IsFavorite, item.Provenance, presentation, item.CreatureProgression,
                item.CreatureLoot, item.CreatureRoleBehavior, item.CreatureEvents, item.ItemAdvanced, item.Authoring,
                item.CreatureFoundation, item.ItemFoundation)
            : item).ToArray();
        return new(project.Id, project.Title, project.Module, blueprints, project.Areas, project.Dialogues,
            project.Quests);
    }

    private static bool ToggleCreaturePresentationFallbackUx4E(
        StudioSessionController session,
        BlueprintId blueprintId)
    {
        var project = session.Project;
        var history = session.History;
        var source = project?.Blueprints.SingleOrDefault(item => item.Id == blueprintId)?.CreaturePresentation;
        if (project is null || history is null || source is null) return false;
        var toggled = new CreaturePresentationProfile(source.Id, source.SchemaVersion, source.Provenance,
            source.GenderId, source.BodyTypeId, source.Pronouns, source.Portraits, !source.UseModelFallback,
            source.RigId, source.BodySlots, source.MaterialColors, source.Attachments, source.Voice);
        if (toggled.Portraits.Count == 0 && !toggled.UseModelFallback) return false;
        var handler = new SetCreaturePresentationHandler(new SystemClock());
        var result = handler.Handle(history.State, new(CommandId.From(Guid.NewGuid()), history.State.Revision,
            blueprintId, toggled, DryRun: false));
        if (!result.Succeeded) return false;
        history.Record(result);
        session.InvalidateContentIndexes();
        return true;
    }

    private static StudioCreaturePresentationPreviewUx4E FailedCreaturePresentationPreviewUx4E(
        string message,
        string code) => new(false, false, message, string.Empty, string.Empty,
        [new(code, "creature/presentation", message)], "Identité non évaluée.", "Portrait non évalué.",
        "Apparence non évaluée.", "Runtime non évalué.");

    private static IReadOnlyList<StudioCreaturePresentationDiagnosticUx4E> CreaturePresentationDiagnosticsUx4E(
        IReadOnlyList<CommandDiagnostic> diagnostics) => diagnostics.Select(item =>
    {
        var separator = item.Message.IndexOf(": ", StringComparison.Ordinal);
        var path = separator > 0 ? item.Message[..separator] : "creature/presentation";
        var message = separator > 0 ? item.Message[(separator + 2)..] : item.Message;
        return new StudioCreaturePresentationDiagnosticUx4E(item.Code, path,
            HumanCreaturePresentationMessageUx4E(item.Code, message));
    }).ToArray();

    private static StudioCreaturePresentationDiagnosticUx4E CreaturePresentationDiagnosticUx4E(
        CreaturePresentationDiagnostic item) => new(item.Code, item.Path,
        HumanCreaturePresentationMessageUx4E(item.Code, item.Message));

    private static string HumanCreaturePresentationMessageUx4E(string code, string fallback) => code switch
    {
        "creature.presentation.pronouns_incomplete" => "Les quatre formes de pronoms sont obligatoires.",
        "creature.presentation.portrait_or_fallback_required" =>
            "Ajoutez un portrait gouverné ou activez le repli vers le modèle.",
        "creature.presentation.portrait_invalid" =>
            "Chaque portrait exige expression, chemin relatif sûr, dimensions, licence, SHA-256 et texte alternatif.",
        "creature.presentation.body_slot_invalid" =>
            "Chaque partie du corps exige un slot, un asset, le rig courant, une couche 0..64 et une règle de clipping.",
        "creature.presentation.material_color_invalid" =>
            "Chaque couleur exige un paramètre unique et une couleur #RRGGBB ou #RRGGBBAA.",
        "creature.presentation.attachment_invalid" =>
            "Chaque aile, queue ou attachement exige un slot unique, un type, un asset et le rig courant.",
        _ => fallback,
    };

    private static string IdentitySummaryUx4E(StudioCreaturePresentationDraftUx4E draft) =>
        $"{draft.GenderId} · {draft.BodyTypeId} · {draft.PronounSubject}/{draft.PronounObject}/" +
        $"{draft.PronounPossessive}/{draft.PronounReflexive}";

    private static string PortraitSummaryUx4E(StudioCreaturePresentationDraftUx4E draft) =>
        $"{draft.Portraits.Count} portrait(s) gouverné(s) · repli modèle {draft.UseModelFallback.ToString().ToLowerInvariant()}";

    private static string AppearanceSummaryUx4E(StudioCreaturePresentationDraftUx4E draft) =>
        $"{draft.RigId} · {draft.BodySlots.Count} partie(s) · {draft.MaterialColors.Count} couleur(s) · " +
        $"{draft.Attachments.Count} attachement(s)";

    private static StudioCreaturePresentationDraftUx4E FreezeCreaturePresentationDraftUx4E(
        StudioCreaturePresentationDraftUx4E draft) => draft with
        {
            Portraits = draft.Portraits.ToArray(),
            BodySlots = draft.BodySlots.Select(item => item with { HiddenSlots = item.HiddenSlots.ToArray() }).ToArray(),
            MaterialColors = draft.MaterialColors.ToArray(),
            Attachments = draft.Attachments.ToArray(),
        };

    private static string FingerprintProjectUx4E(ProjectDefinition project) =>
        HashUx4E(ProjectJsonSerializer.Serialize(project));

    private static string FingerprintCreaturePresentationCandidateUx4E(
        ProjectDefinition project,
        BlueprintId blueprintId,
        CreaturePresentationProfile candidate) =>
        FingerprintProjectUx4E(WithCreaturePresentationUx4E(project, blueprintId, candidate));

    private static string FingerprintCreaturePresentationDraftUx4E(StudioCreaturePresentationDraftUx4E draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(draft.BaseRevision.Value);
        WriteUx4E(writer, draft.SourceProjectFingerprint);
        WriteUx4E(writer, draft.BlueprintId.ToString());
        foreach (var value in new[] { draft.GenderId, draft.BodyTypeId, draft.PronounSubject, draft.PronounObject,
                     draft.PronounPossessive, draft.PronounReflexive, draft.RigId }) WriteUx4E(writer, value);
        writer.Write(draft.UseModelFallback);
        writer.Write(draft.Portraits.Count);
        foreach (var item in draft.Portraits)
        {
            WriteUx4E(writer, item.Expression); WriteUx4E(writer, item.AssetPath); writer.Write(item.Width);
            writer.Write(item.Height); WriteUx4E(writer, item.LicenseSpdx); WriteUx4E(writer, item.ContentSha256);
            WriteUx4E(writer, item.AltText);
        }
        writer.Write(draft.BodySlots.Count);
        foreach (var item in draft.BodySlots)
        {
            WriteUx4E(writer, item.SlotId); WriteUx4E(writer, item.AssetId); WriteUx4E(writer, item.RequiredRigId);
            writer.Write(item.Layer); writer.Write(item.HiddenSlots.Count);
            foreach (var hidden in item.HiddenSlots) WriteUx4E(writer, hidden);
            WriteUx4E(writer, item.ClippingRule);
        }
        writer.Write(draft.MaterialColors.Count);
        foreach (var item in draft.MaterialColors)
        {
            WriteUx4E(writer, item.ParameterId); WriteUx4E(writer, item.HexColor);
        }
        writer.Write(draft.Attachments.Count);
        foreach (var item in draft.Attachments)
        {
            WriteUx4E(writer, item.SlotId); WriteUx4E(writer, item.Kind); WriteUx4E(writer, item.AssetId);
            WriteUx4E(writer, item.RequiredRigId);
        }
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteUx4E(BinaryWriter writer, string? value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string HashUx4E(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static Guid StableCreaturePresentationIdUx4E(Guid basis, string segment)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{basis:D}:ux4-e:{segment}"));
        Span<byte> id = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(id);
        id[7] = (byte)((id[7] & 0x0F) | 0x40);
        id[8] = (byte)((id[8] & 0x3F) | 0x80);
        return new Guid(id);
    }

    private static bool IsCanonicalInputUx4E(string value) =>
        !string.IsNullOrWhiteSpace(value) && string.Equals(value, value.Trim().ToLowerInvariant(), StringComparison.Ordinal);

    private static bool IsSafeRelativeAssetPathUx4E(string path) =>
        !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) && !path.Contains('\\') &&
        path.Split('/').All(segment => segment is not "" and not "." and not "..");

    internal static byte[] EvidencePortraitBytesUx4E()
    {
        const int width = 32;
        const int height = 32;
        const int rowSize = width * 3;
        const int pixelBytes = rowSize * height;
        using var stream = new MemoryStream(54 + pixelBytes);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write((byte)'B'); writer.Write((byte)'M');
        writer.Write(54 + pixelBytes); writer.Write(0); writer.Write(54);
        writer.Write(40); writer.Write(width); writer.Write(height);
        writer.Write((short)1); writer.Write((short)24); writer.Write(0); writer.Write(pixelBytes);
        writer.Write(2835); writer.Write(2835); writer.Write(0); writer.Write(0);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var accent = ((x / 8) + (y / 8)) % 2 == 0;
                writer.Write((byte)(accent ? 58 : 28));
                writer.Write((byte)(accent ? 112 : 62));
                writer.Write((byte)(accent ? 218 : 124));
            }
        writer.Flush();
        return stream.ToArray();
    }

    internal static bool WriteEvidencePortraitUx4E(string root, string assetPath, byte[] bytes)
    {
        try
        {
            if (!IsSafeRelativeAssetPathUx4E(assetPath)) return false;
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(Path.Combine(root, assetPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, bytes);
            return File.Exists(full);
        }
        catch
        {
            return false;
        }
    }

    private static bool DeleteUx4ESmokeRoot(string root)
    {
        try
        {
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4e-smoke-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full) && !File.Exists(full);
        }
        catch
        {
            return false;
        }
    }
}
