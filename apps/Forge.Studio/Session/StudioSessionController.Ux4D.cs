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
using Forge.RpgKit.Application.Creatures;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetCreatureFoundationHandler _setCreatureFoundationUx4D = new(new SystemClock());
    private StudioCreatureFoundationDraftUx4D? _approvedCreatureFoundationDraftUx4D;
    private CreatureFoundationProfile? _approvedCreatureFoundationCandidateUx4D;
    private ProjectRevision? _approvedCreatureFoundationRevisionUx4D;
    private string? _approvedCreatureFoundationProjectFingerprintUx4D;
    private string? _approvedCreatureFoundationDraftFingerprintUx4D;
    private string? _approvedCreatureFoundationCandidateFingerprintUx4D;

    internal IReadOnlyList<StudioCreatureChoiceUx4D> CreatureChoicesUx4D => Project?.Blueprints
        .Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
        .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
        .ThenBy(item => item.Slug, StringComparer.Ordinal)
        .Select(item => new StudioCreatureChoiceUx4D(
            item.Id,
            item.Slug,
            $"{item.DisplayName} · {(item.Kind == BlueprintKind.PlayerSpawn ? "joueur" : "créature")} · " +
            $"{(item.CreatureFoundation is null ? "fondation absente" : "fondation prête")}",
            item.CreatureFoundation is not null,
            item.CreatureFoundation?.AuthoringComposition is not null,
            item.IsReadOnly()))
        .ToArray() ?? [];

    internal IReadOnlyList<StudioCreatureFactionChoiceUx4D> CreatureFactionChoicesUx4D(BlueprintId blueprintId)
    {
        var project = Project;
        if (project is null) return [];
        var current = project.Blueprints.SingleOrDefault(item => item.Id == blueprintId)?
            .CreatureFoundation?.AuthoringComposition?.FactionId;
        var choices = project.Module.Factions.Factions
            .Where(item => !item.Archived)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new StudioCreatureFactionChoiceUx4D(item.Slug, item.DisplayName, false))
            .ToList();
        if (!string.IsNullOrWhiteSpace(current) && choices.All(item => item.Value != current))
        {
            choices.Insert(0, new(current, "Faction héritée protégée", true));
        }
        if (choices.Count == 0)
        {
            choices.Add(new(current ?? "faction.neutral", "Faction neutre canonique", current is not null));
        }
        return choices;
    }

    internal StudioCreatureFoundationDraftUx4D? CreateCreatureFoundationDraftUx4D(BlueprintId blueprintId)
    {
        var project = Project;
        var history = History;
        var blueprint = project?.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        if (project is null || history is null || blueprint is null || blueprint.IsReadOnly() ||
            blueprint.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn))
        {
            return null;
        }

        var foundation = blueprint.CreatureFoundation;
        var identity = foundation?.Identity;
        var authoring = foundation?.AuthoringComposition;
        var fallbackAuthoring = authoring ?? CreatureAuthoringCompositionProfile.CreateDefault(
            StableCreatureIdUx4D(blueprint.Id.Value, "authoring"), "studio:ux4-d");
        return new(
            history.State.Revision,
            FingerprintProjectUx4D(project),
            blueprint.Id,
            blueprint,
            foundation,
            foundation?.RequiredAtRuntime ?? true,
            Text(identity?.Names, "fr", blueprint.DisplayName),
            Text(identity?.Names, "en", string.Empty),
            Join(identity?.Aliases ?? []),
            identity?.Pronouns ?? "iel",
            Text(identity?.Biographies, "fr", string.Empty),
            Text(identity?.Biographies, "en", string.Empty),
            identity?.AuthorNotes ?? string.Empty,
            Join(identity?.NarrativeTags ?? []),
            fallbackAuthoring.AssetId,
            fallbackAuthoring.ArchetypeId,
            fallbackAuthoring.RulesetId,
            fallbackAuthoring.RigId,
            fallbackAuthoring.FactionId,
            fallbackAuthoring.EncounterRole,
            fallbackAuthoring.ThreatRating.ToString("R", CultureInfo.InvariantCulture),
            fallbackAuthoring.VisualKitId,
            Join(fallbackAuthoring.CombatTags),
            fallbackAuthoring.Species.SpeciesId,
            Join(fallbackAuthoring.Species.ParentSpeciesIds),
            FormatTraitSetsUx4D(fallbackAuthoring.Species.TraitSets),
            Join(fallbackAuthoring.Species.Variants),
            fallbackAuthoring.Species.RequiredSkeletonId);
    }

    internal StudioCreatureFoundationPreviewUx4D PreviewCreatureFoundationUx4D(
        StudioCreatureFoundationDraftUx4D draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelCreatureFoundationUx4D();
        var project = Project;
        var history = History;
        if (project is null || history is null)
        {
            return FailedCreaturePreviewUx4D("Aucun module n’est ouvert.", "studio.creature.project_missing");
        }
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintProjectUx4D(project), draft.SourceProjectFingerprint, StringComparison.Ordinal))
        {
            return FailedCreaturePreviewUx4D(
                "Le projet a changé depuis l’ouverture de la fiche. Rechargez-la avant de prévisualiser.",
                "command.revision_conflict");
        }
        if (!TryBuildCreatureFoundationCandidateUx4D(draft, out var candidate, out var diagnostics))
        {
            return new(false, false, "La fiche contient des valeurs invalides.", string.Empty, string.Empty,
                diagnostics, IdentitySummaryUx4D(draft), WizardSummaryUx4D(draft), SpeciesSummaryUx4D(draft),
                "Runtime non évalué.");
        }

        var command = new SetCreatureFoundationCommand(
            CommandId.From(Guid.NewGuid()), draft.BaseRevision, draft.BlueprintId, candidate, DryRun: true);
        var preview = _setCreatureFoundationUx4D.Handle(history.State, command);
        if (!preview.Succeeded)
        {
            return new(false, false, "Le validateur canonique refuse cette configuration.", string.Empty,
                string.Empty, CreatureDiagnosticsUx4D(preview.Diagnostics), IdentitySummaryUx4D(draft),
                WizardSummaryUx4D(draft), SpeciesSummaryUx4D(draft), "Runtime non évalué.");
        }

        var candidateProject = WithCreatureFoundationUx4D(project, draft.BlueprintId, candidate);
        var runtime = CreatureAuthoringCompositionFoundationService.Resolve(candidateProject, draft.BlueprintId);
        var foundationRuntime = CreatureFoundationRuntimeResolver.Resolve(candidate, candidate.RequiredAtRuntime);
        var scriptView = CreatureFoundationScriptService.Read(candidateProject, draft.BlueprintId);
        if (!runtime.Succeeded || !foundationRuntime.Succeeded || runtime.Creature is null || scriptView is null)
        {
            var runtimeDiagnostics = runtime.Diagnostics.Concat(foundationRuntime.Diagnostics)
                .Select(CreatureDiagnosticUx4D).ToArray();
            return new(false, false, "La projection runtime canonique refuse cette configuration.",
                preview.Patch?.Summary ?? string.Empty, preview.Inverse?.Summary ?? string.Empty,
                runtimeDiagnostics, IdentitySummaryUx4D(draft), WizardSummaryUx4D(draft),
                SpeciesSummaryUx4D(draft), "Runtime refusé.");
        }

        var frozen = CloneCreatureDraftUx4D(draft);
        _approvedCreatureFoundationDraftUx4D = frozen;
        _approvedCreatureFoundationCandidateUx4D = candidate;
        _approvedCreatureFoundationRevisionUx4D = history.State.Revision;
        _approvedCreatureFoundationProjectFingerprintUx4D = FingerprintProjectUx4D(project);
        _approvedCreatureFoundationDraftFingerprintUx4D = FingerprintCreatureDraftUx4D(frozen);
        _approvedCreatureFoundationCandidateFingerprintUx4D =
            FingerprintCreatureCandidateUx4D(project, draft.BlueprintId, candidate);
        return new(true, true, "Aperçu canonique prêt · aucune donnée du module n’a été modifiée.",
            preview.Patch?.Summary ?? "Fondation de créature prévisualisée.",
            preview.Inverse?.Summary ?? "État précédent restaurable.", [], IdentitySummaryUx4D(draft),
            WizardSummaryUx4D(draft), SpeciesSummaryUx4D(draft),
            $"Projection RPG Kit prête · {runtime.Creature.Identity.Name.DefaultText} · " +
            $"{runtime.Creature.Authoring.Role} · menace {runtime.Creature.Authoring.ThreatRating:R}.");
    }

    internal StudioCreatureFoundationActionResultUx4D ApplyApprovedCreatureFoundationUx4D(
        StudioCreatureFoundationDraftUx4D currentDraft)
    {
        ArgumentNullException.ThrowIfNull(currentDraft);
        var project = Project;
        var history = History;
        var approvedDraft = _approvedCreatureFoundationDraftUx4D;
        var candidate = _approvedCreatureFoundationCandidateUx4D;
        if (project is null || history is null || approvedDraft is null || candidate is null ||
            _approvedCreatureFoundationRevisionUx4D is null ||
            _approvedCreatureFoundationProjectFingerprintUx4D is null ||
            _approvedCreatureFoundationDraftFingerprintUx4D is null ||
            _approvedCreatureFoundationCandidateFingerprintUx4D is null)
        {
            return new(false, "Prévisualisez la fiche avant de l’appliquer.");
        }

        var stale = history.State.Revision != _approvedCreatureFoundationRevisionUx4D ||
                    !string.Equals(FingerprintProjectUx4D(project),
                        _approvedCreatureFoundationProjectFingerprintUx4D, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureDraftUx4D(currentDraft),
                        _approvedCreatureFoundationDraftFingerprintUx4D, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureDraftUx4D(approvedDraft),
                        _approvedCreatureFoundationDraftFingerprintUx4D, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureCandidateUx4D(project, currentDraft.BlueprintId, candidate),
                        _approvedCreatureFoundationCandidateFingerprintUx4D, StringComparison.Ordinal);
        if (stale)
        {
            CancelCreatureFoundationUx4D();
            return new(false, "Le projet ou la fiche a changé. Prévisualisez à nouveau avant d’appliquer.");
        }

        var dryRun = _setCreatureFoundationUx4D.Handle(history.State, new(
            CommandId.From(Guid.NewGuid()), history.State.Revision, currentDraft.BlueprintId, candidate, DryRun: true));
        if (!dryRun.Succeeded)
        {
            CancelCreatureFoundationUx4D();
            return new(false, CreatureDiagnosticsUx4D(dryRun.Diagnostics).FirstOrDefault()?.HumanMessage ??
                              "Le contrôle final a refusé la modification.");
        }
        var result = _setCreatureFoundationUx4D.Handle(history.State, new(
            CommandId.From(Guid.NewGuid()), history.State.Revision, currentDraft.BlueprintId, candidate, DryRun: false));
        CancelCreatureFoundationUx4D();
        if (!result.Succeeded)
        {
            return new(false, CreatureDiagnosticsUx4D(result.Diagnostics).FirstOrDefault()?.HumanMessage ??
                              "La commande canonique a refusé la modification.");
        }
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, "Fondation de créature appliquée · Ctrl+Z pour annuler.");
    }

    internal StudioCreatureFoundationActionResultUx4D UndoCreatureFoundationUx4D()
    {
        CancelCreatureFoundationUx4D();
        var result = Undo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioCreatureFoundationActionResultUx4D RedoCreatureFoundationUx4D()
    {
        CancelCreatureFoundationUx4D();
        var result = Redo();
        return new(result.Succeeded, result.Message);
    }

    internal void CancelCreatureFoundationUx4D()
    {
        _approvedCreatureFoundationDraftUx4D = null;
        _approvedCreatureFoundationCandidateUx4D = null;
        _approvedCreatureFoundationRevisionUx4D = null;
        _approvedCreatureFoundationProjectFingerprintUx4D = null;
        _approvedCreatureFoundationDraftFingerprintUx4D = null;
        _approvedCreatureFoundationCandidateFingerprintUx4D = null;
    }

    internal void CloseUx4D() => CancelCreatureFoundationUx4D();

    internal StudioCreatureFoundationApprovalSnapshotUx4D CaptureCreatureFoundationApprovalUx4D() => new(
        _approvedCreatureFoundationDraftUx4D,
        _approvedCreatureFoundationCandidateUx4D,
        _approvedCreatureFoundationRevisionUx4D,
        _approvedCreatureFoundationProjectFingerprintUx4D,
        _approvedCreatureFoundationDraftFingerprintUx4D,
        _approvedCreatureFoundationCandidateFingerprintUx4D);

    internal void RestoreCreatureFoundationApprovalUx4D(StudioCreatureFoundationApprovalSnapshotUx4D snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedCreatureFoundationDraftUx4D = snapshot.Draft;
        _approvedCreatureFoundationCandidateUx4D = snapshot.Candidate;
        _approvedCreatureFoundationRevisionUx4D = snapshot.Revision;
        _approvedCreatureFoundationProjectFingerprintUx4D = snapshot.ProjectFingerprint;
        _approvedCreatureFoundationDraftFingerprintUx4D = snapshot.DraftFingerprint;
        _approvedCreatureFoundationCandidateFingerprintUx4D = snapshot.CandidateFingerprint;
    }

    internal StudioCreatureFoundationEvidenceProjectUx4D? CreateUx4DEvidenceProject()
    {
        var project = Project;
        if (project is null) return null;
        var realCreature = project.Blueprints.FirstOrDefault(item => item.Kind == BlueprintKind.Creature &&
            item.CreatureFoundation?.AuthoringComposition is not null && !item.IsReadOnly());
        var real = string.Equals(project.Module.Slug, "crypt-of-ashes", StringComparison.Ordinal) &&
                   realCreature is not null;
        if (realCreature is not null && real)
        {
            return new(project, realCreature.Id, true, false);
        }

        var source = project.Blueprints.FirstOrDefault(item =>
            item.Kind == BlueprintKind.Creature && !item.IsReadOnly()) ??
                     project.Blueprints.FirstOrDefault(item =>
                         item.Kind == BlueprintKind.PlayerSpawn && !item.IsReadOnly());
        if (source is null) return null;
        var foundation = CreateCanonicalEvidenceFoundationUx4D(source);
        var evidenceProject = WithCreatureFoundationUx4D(project, source.Id, foundation);
        return new(evidenceProject, source.Id, false, true);
    }

    internal StudioUx4DSmokeResult RunUx4DContractSmoke()
    {
        var result = new StudioUx4DSmokeResult(false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false);
        var evidence = CreateUx4DEvidenceProject();
        if (evidence is null) return result;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4d-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded || !editing.Open(root).Succeeded)
                return result;
            var choices = editing.CreatureChoicesUx4D;
            var explicitSelection = choices.Count > 0 && choices.All(item =>
                !item.Label.Contains(item.BlueprintId.ToString(), StringComparison.OrdinalIgnoreCase));
            var draft = editing.CreateCreatureFoundationDraftUx4D(evidence.BlueprintId);
            if (draft is null) return result;
            var changed = ChangedCreatureDraftUx4D(draft);
            var sourceProject = editing.Project!;
            var sourceBlueprint = sourceProject.Blueprints.Single(item => item.Id == evidence.BlueprintId);
            var sourceFoundation = sourceBlueprint.CreatureFoundation;
            var sourceJson = ProjectJsonSerializer.Serialize(sourceProject);
            var preview = editing.PreviewCreatureFoundationUx4D(changed);
            var dryRun = preview.Succeeded && ReferenceEquals(sourceProject, editing.Project) &&
                         sourceJson == ProjectJsonSerializer.Serialize(editing.Project!);
            var invalid = editing.PreviewCreatureFoundationUx4D(changed with { ThreatRating = "-1" });
            var invalidBlocked = !invalid.Succeeded && invalid.Diagnostics.Any(item =>
                item.Code is "creature.authoring.wizard_invalid" or "studio.creature.number_invalid");

            var intermediate = ToggleCreatureRequiredUx4D(editing, evidence.BlueprintId);
            var staleDraft = editing.PreviewCreatureFoundationUx4D(changed);
            var staleDraftBlocked = intermediate && !staleDraft.Succeeded;

            var fresh = editing.CreateCreatureFoundationDraftUx4D(evidence.BlueprintId)!;
            var freshChanged = ChangedCreatureDraftUx4D(fresh);
            var approved = editing.PreviewCreatureFoundationUx4D(freshChanged);
            var secondIntermediate = ToggleCreatureRequiredUx4D(editing, evidence.BlueprintId);
            var staleApproval = editing.ApplyApprovedCreatureFoundationUx4D(freshChanged);
            var staleApprovalBlocked = approved.Succeeded && secondIntermediate && !staleApproval.Succeeded;

            fresh = editing.CreateCreatureFoundationDraftUx4D(evidence.BlueprintId)!;
            freshChanged = ChangedCreatureDraftUx4D(fresh);
            var expectedBuilt = editing.TryBuildCreatureFoundationCandidateUx4D(
                freshChanged, out var expectedCandidate, out var expectedDiagnostics) &&
                                expectedDiagnostics.Count == 0;
            var finalPreview = editing.PreviewCreatureFoundationUx4D(freshChanged);
            var beforeApply = editing.Project!;
            var beforeBlueprint = beforeApply.Blueprints.Single(item => item.Id == evidence.BlueprintId);
            var beforeFoundation = beforeBlueprint.CreatureFoundation;
            var apply = editing.ApplyApprovedCreatureFoundationUx4D(freshChanged);
            var appliedProject = editing.Project;
            var appliedBlueprint = appliedProject?.Blueprints.Single(item => item.Id == evidence.BlueprintId);
            var applied = appliedBlueprint?.CreatureFoundation;
            var initialized = apply.Succeeded && applied is not null;
            var localized = applied?.Identity.Names.GetValueOrDefault("fr") == "Gardien témoin UX4-D" &&
                            applied.Identity.Names.GetValueOrDefault("en") == "UX4-D Witness Warden" &&
                            applied.Identity.Biographies.GetValueOrDefault("fr")?.Contains("preuve isolée",
                                StringComparison.Ordinal) == true;
            var wizard = applied?.AuthoringComposition is { } authored &&
                         authored.ArchetypeId == "archetype.witness" && authored.RulesetId == "ruleset.core" &&
                         authored.VisualKitId == "visual.witness" && authored.CombatTags.Contains("witness");
            var roleThreat = applied?.AuthoringComposition is { EncounterRole: CreatureAuthoringEncounterRole.Controller,
                ThreatRating: 7.25 };
            var species = applied?.AuthoringComposition?.Species is { } authoredSpecies &&
                          authoredSpecies.SpeciesId == "species.emberborn" &&
                          authoredSpecies.ParentSpeciesIds.Contains("species.construct") &&
                          authoredSpecies.TraitSets.Any(item => item.Id == "ember.core" &&
                              item.Traits.Contains("heat.resistant")) &&
                          authoredSpecies.Variants.Contains("awakened");
            var neighbors = appliedBlueprint is not null && applied is not null &&
                            expectedBuilt &&
                            ProjectJsonSerializer.Serialize(WithCreatureFoundationUx4D(
                                beforeApply, evidence.BlueprintId, expectedCandidate)) ==
                            ProjectJsonSerializer.Serialize(appliedProject!) &&
                            ReferenceEquals(beforeBlueprint.Combat, appliedBlueprint.Combat) &&
                            ReferenceEquals(beforeBlueprint.CreaturePresentation, appliedBlueprint.CreaturePresentation) &&
                            ReferenceEquals(beforeBlueprint.CreatureProgression, appliedBlueprint.CreatureProgression) &&
                            ReferenceEquals(beforeBlueprint.CreatureLoot, appliedBlueprint.CreatureLoot) &&
                            ReferenceEquals(beforeBlueprint.CreatureRoleBehavior, appliedBlueprint.CreatureRoleBehavior) &&
                            ReferenceEquals(beforeBlueprint.CreatureEvents, appliedBlueprint.CreatureEvents) &&
                            ReferenceEquals(beforeBlueprint.Authoring, appliedBlueprint.Authoring) &&
                            beforeApply.Blueprints.Where(item => item.Id != evidence.BlueprintId)
                                .SequenceEqual(appliedProject!.Blueprints.Where(item => item.Id != evidence.BlueprintId)) &&
                            beforeApply.Areas.SequenceEqual(appliedProject.Areas) &&
                            beforeApply.Dialogues.SequenceEqual(appliedProject.Dialogues) &&
                            beforeApply.Quests.SequenceEqual(appliedProject.Quests) &&
                            ReferenceEquals(beforeFoundation?.RulesComposition, applied.RulesComposition) &&
                            ReferenceEquals(beforeFoundation?.InteractionComposition, applied.InteractionComposition) &&
                            ReferenceEquals(beforeFoundation?.EventAiComposition, applied.EventAiComposition);
            var undo = editing.UndoCreatureFoundationUx4D();
            var undone = undo.Succeeded && ReferenceEquals(beforeApply, editing.Project);
            var redo = editing.RedoCreatureFoundationUx4D();
            var redone = redo.Succeeded && ReferenceEquals(appliedProject, editing.Project);
            var saved = redone && editing.Save().Succeeded;
            var persisted = saved && reopened.Open(root).Succeeded &&
                            ProjectJsonSerializer.Serialize(reopened.Project!) ==
                            ProjectJsonSerializer.Serialize(editing.Project!);
            result = new(
                finalPreview.Succeeded && initialized && localized && wizard && roleThreat && species &&
                invalidBlocked && dryRun && staleDraftBlocked && staleApprovalBlocked && apply.Succeeded &&
                undone && redone && persisted && neighbors,
                explicitSelection, initialized, localized, wizard, roleThreat, species, invalidBlocked, dryRun,
                staleDraftBlocked, staleApprovalBlocked, finalPreview.Succeeded && apply.Succeeded,
                undone && redone, persisted, neighbors, false);
        }
        catch
        {
            return result;
        }
        finally
        {
            reopened.Close();
            editing.Close();
            var released = DeleteUx4DSmokeRoot(root);
            result = result with { Succeeded = result.Succeeded && released, TemporaryWorkspaceReleased = released };
        }
        return result;
    }

    private static StudioCreatureFoundationDraftUx4D ChangedCreatureDraftUx4D(
        StudioCreatureFoundationDraftUx4D draft) => draft with
    {
        FrenchName = "Gardien témoin UX4-D",
        EnglishName = "UX4-D Witness Warden",
        Aliases = "gardien témoin, veilleur",
        Pronouns = "iel",
        FrenchBiography = "Créature de preuve isolée pour le Studio.",
        AuthorNotes = "Ne jamais muter le module source.",
        NarrativeTags = "studio, witness",
        ArchetypeId = "archetype.witness",
        RulesetId = "ruleset.core",
        EncounterRole = CreatureAuthoringEncounterRole.Controller,
        ThreatRating = "7.25",
        VisualKitId = "visual.witness",
        CombatTags = "controller, witness",
        SpeciesId = "species.emberborn",
        ParentSpeciesIds = "species.construct",
        TraitSets = "ember.core=heat.resistant,ember.sight\nconstruct.core=artificial",
        Variants = "awakened, ancient",
        RequiredSkeletonId = draft.RigId,
    };

    private bool TryBuildCreatureFoundationCandidateUx4D(
        StudioCreatureFoundationDraftUx4D draft,
        out CreatureFoundationProfile candidate,
        out IReadOnlyList<StudioCreatureFoundationDiagnosticUx4D> diagnostics)
    {
        candidate = null!;
        var errors = new List<StudioCreatureFoundationDiagnosticUx4D>();
        if (!double.TryParse(draft.ThreatRating, NumberStyles.Float, CultureInfo.InvariantCulture, out var threat) ||
            !double.IsFinite(threat))
        {
            errors.Add(new("studio.creature.number_invalid", "creature/authoring/threatRating",
                "Le niveau de menace doit être un nombre fini."));
        }
        var traitSets = ParseTraitSetsUx4D(draft.TraitSets, errors);
        if (errors.Count > 0)
        {
            diagnostics = errors;
            return false;
        }

        var blueprint = draft.SourceBlueprint;
        var source = draft.SourceFoundation ?? CreatureFoundationProfile.CreateDefault(
            StableCreatureIdUx4D(blueprint.Id.Value, "foundation"), blueprint.Slug, blueprint.DisplayName,
            "studio:ux4-d");
        var sourceAuthoring = source.AuthoringComposition ?? CreatureAuthoringCompositionProfile.CreateDefault(
            StableCreatureIdUx4D(blueprint.Id.Value, "authoring"), "studio:ux4-d");
        var names = new Dictionary<string, string>(StringComparer.Ordinal) { ["fr"] = draft.FrenchName };
        if (!string.IsNullOrWhiteSpace(draft.EnglishName)) names["en"] = draft.EnglishName;
        var biographies = new Dictionary<string, string>(StringComparer.Ordinal) { ["fr"] = draft.FrenchBiography };
        if (!string.IsNullOrWhiteSpace(draft.EnglishBiography)) biographies["en"] = draft.EnglishBiography;
        var identity = new CreatureFoundationIdentity(
            blueprint.Slug, names, Split(draft.Aliases), draft.Pronouns, biographies, draft.AuthorNotes,
            Split(draft.NarrativeTags));
        var species = new CreatureSpeciesProfile(
            draft.SpeciesId, Split(draft.ParentSpeciesIds), traitSets, Split(draft.Variants),
            draft.RequiredSkeletonId);
        var authoring = new CreatureAuthoringCompositionProfile(
            sourceAuthoring.Id,
            sourceAuthoring.SchemaVersion,
            sourceAuthoring.Provenance,
            draft.AssetId,
            sourceAuthoring.PreviewTransform,
            draft.ArchetypeId,
            draft.RulesetId,
            draft.RigId,
            draft.FactionId,
            draft.EncounterRole,
            threat,
            draft.VisualKitId,
            Split(draft.CombatTags),
            species,
            sourceAuthoring.Appearance with { RigId = draft.RigId },
            sourceAuthoring.AnimationGraph,
            sourceAuthoring.Overrides);
        candidate = new CreatureFoundationProfile(
            source.Id,
            source.SchemaVersion,
            source.Provenance,
            draft.RequiredAtRuntime,
            identity,
            source.DefaultTransform,
            source.Resources,
            source.Movement,
            source.References,
            source.Spawned,
            source.Overrides,
            authoring,
            true,
            source.RulesComposition,
            source.RulesCompositionRequiredAtRuntime,
            source.InteractionComposition,
            source.InteractionCompositionRequiredAtRuntime,
            source.EventAiComposition,
            source.EventAiCompositionRequiredAtRuntime);
        var domainDiagnostics = CreatureFoundationValidator.Validate(candidate,
            $"blueprints/{blueprint.Id}/creatureFoundation");
        diagnostics = domainDiagnostics.Select(CreatureDiagnosticUx4D).ToArray();
        return diagnostics.Count == 0;
    }

    private static IReadOnlyList<CreatureSpeciesTraitSet> ParseTraitSetsUx4D(
        string value,
        ICollection<StudioCreatureFoundationDiagnosticUx4D> errors)
    {
        var result = new List<CreatureSpeciesTraitSet>();
        foreach (var line in value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf('=');
            if (separator <= 0 || separator == line.Length - 1)
            {
                errors.Add(new("studio.creature.trait_set_invalid", "creature/species/traitSets",
                    "Chaque ensemble doit utiliser la forme identifiant=trait,trait."));
                continue;
            }
            result.Add(new(line[..separator], Split(line[(separator + 1)..])));
        }
        if (result.Count == 0)
        {
            errors.Add(new("studio.creature.trait_set_required", "creature/species/traitSets",
                "Au moins un ensemble de traits est requis."));
        }
        return result;
    }

    private static CreatureFoundationProfile CreateCanonicalEvidenceFoundationUx4D(BlueprintDefinition blueprint)
    {
        var foundation = CreatureFoundationProfile.CreateDefault(
            StableCreatureIdUx4D(blueprint.Id.Value, "foundation"), blueprint.Slug, blueprint.DisplayName,
            "studio:ux4-d-evidence");
        var authoring = CreatureAuthoringCompositionProfile.CreateDefault(
            StableCreatureIdUx4D(blueprint.Id.Value, "authoring"), "studio:ux4-d-evidence");
        return new(foundation.Id, foundation.SchemaVersion, foundation.Provenance, true, foundation.Identity,
            foundation.DefaultTransform, foundation.Resources, foundation.Movement, foundation.References,
            foundation.Spawned, foundation.Overrides, authoring, true,
            foundation.RulesComposition, foundation.RulesCompositionRequiredAtRuntime,
            foundation.InteractionComposition, foundation.InteractionCompositionRequiredAtRuntime,
            foundation.EventAiComposition, foundation.EventAiCompositionRequiredAtRuntime);
    }

    private static ProjectDefinition WithCreatureFoundationUx4D(
        ProjectDefinition project,
        BlueprintId blueprintId,
        CreatureFoundationProfile foundation)
    {
        var blueprints = project.Blueprints.Select(item =>
            item.Id == blueprintId ? item.WithCreatureFoundation(foundation) : item).ToArray();
        return new(project.Id, project.Title, project.Module, blueprints, project.Areas, project.Dialogues,
            project.Quests);
    }

    private static bool ToggleCreatureRequiredUx4D(StudioSessionController session, BlueprintId blueprintId)
    {
        var project = session.Project;
        var history = session.History;
        var source = project?.Blueprints.SingleOrDefault(item => item.Id == blueprintId)?.CreatureFoundation;
        if (project is null || history is null || source is null) return false;
        var toggled = new CreatureFoundationProfile(
            source.Id, source.SchemaVersion, source.Provenance, !source.RequiredAtRuntime, source.Identity,
            source.DefaultTransform, source.Resources, source.Movement, source.References, source.Spawned,
            source.Overrides, source.AuthoringComposition, source.AuthoringCompositionRequiredAtRuntime,
            source.RulesComposition, source.RulesCompositionRequiredAtRuntime,
            source.InteractionComposition, source.InteractionCompositionRequiredAtRuntime,
            source.EventAiComposition, source.EventAiCompositionRequiredAtRuntime);
        var handler = new SetCreatureFoundationHandler(new SystemClock());
        var result = handler.Handle(history.State, new(CommandId.From(Guid.NewGuid()), history.State.Revision,
            blueprintId, toggled, DryRun: false));
        if (!result.Succeeded) return false;
        history.Record(result);
        session.InvalidateContentIndexes();
        return true;
    }

    private static StudioCreatureFoundationPreviewUx4D FailedCreaturePreviewUx4D(string message, string code) =>
        new(false, false, message, string.Empty, string.Empty,
            [new(code, "creature", message)], "Identité non évaluée.", "Assistant non évalué.",
            "Espèce non évaluée.", "Runtime non évalué.");

    private static IReadOnlyList<StudioCreatureFoundationDiagnosticUx4D> CreatureDiagnosticsUx4D(
        IReadOnlyList<CommandDiagnostic> diagnostics) => diagnostics.Select(item =>
    {
        var separator = item.Message.IndexOf(": ", StringComparison.Ordinal);
        var path = separator > 0 ? item.Message[..separator] : "creature";
        var message = separator > 0 ? item.Message[(separator + 2)..] : item.Message;
        return new StudioCreatureFoundationDiagnosticUx4D(item.Code, path, HumanCreatureMessageUx4D(item.Code, message));
    }).ToArray();

    private static StudioCreatureFoundationDiagnosticUx4D CreatureDiagnosticUx4D(
        CreatureFoundationDiagnostic item) =>
        new(item.Code, item.Path, HumanCreatureMessageUx4D(item.Code, item.Message));

    private static string HumanCreatureMessageUx4D(string code, string fallback) => code switch
    {
        "creature.foundation.identity_invalid" => "Renseignez un nom français et des pronoms.",
        "creature.foundation.localization_invalid" => "Chaque locale doit être canonique et chaque nom non vide.",
        "creature.authoring.wizard_invalid" => "Assistant : références, rôle, menace et tags doivent être valides.",
        "creature.authoring.species_invalid" => "Espèce : héritage, ensembles de traits, variantes et squelette sont invalides.",
        "creature.authoring.appearance_invalid" => "Le rig et le squelette doivent rester compatibles avec l’apparence.",
        "command.revision_conflict" => "Le projet a changé : rechargez la fiche.",
        _ => fallback,
    };

    private static string IdentitySummaryUx4D(StudioCreatureFoundationDraftUx4D draft) =>
        $"{draft.FrenchName} · {Split(draft.Aliases).Count} alias · {Split(draft.NarrativeTags).Count} tags narratifs";

    private static string WizardSummaryUx4D(StudioCreatureFoundationDraftUx4D draft) =>
        $"{draft.ArchetypeId} · {draft.EncounterRole} · menace {draft.ThreatRating} · {draft.FactionId}";

    private static string SpeciesSummaryUx4D(StudioCreatureFoundationDraftUx4D draft) =>
        $"{draft.SpeciesId} · {Split(draft.ParentSpeciesIds).Count} parent(s) · " +
        $"{draft.TraitSets.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length} ensemble(s) · " +
        $"{Split(draft.Variants).Count} variante(s)";

    private static StudioCreatureFoundationDraftUx4D CloneCreatureDraftUx4D(
        StudioCreatureFoundationDraftUx4D draft) => draft with { };

    private static string FingerprintProjectUx4D(ProjectDefinition project) =>
        HashUx4D(ProjectJsonSerializer.Serialize(project));

    private static string FingerprintCreatureCandidateUx4D(
        ProjectDefinition project,
        BlueprintId blueprintId,
        CreatureFoundationProfile candidate) =>
        FingerprintProjectUx4D(WithCreatureFoundationUx4D(project, blueprintId, candidate));

    private static string FingerprintCreatureDraftUx4D(StudioCreatureFoundationDraftUx4D draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(draft.BaseRevision.Value);
        WriteUx4D(writer, draft.SourceProjectFingerprint);
        WriteUx4D(writer, draft.BlueprintId.ToString());
        writer.Write(draft.RequiredAtRuntime);
        WriteUx4D(writer, draft.FrenchName); WriteUx4D(writer, draft.EnglishName);
        WriteUx4D(writer, draft.Aliases); WriteUx4D(writer, draft.Pronouns);
        WriteUx4D(writer, draft.FrenchBiography); WriteUx4D(writer, draft.EnglishBiography);
        WriteUx4D(writer, draft.AuthorNotes); WriteUx4D(writer, draft.NarrativeTags);
        WriteUx4D(writer, draft.AssetId); WriteUx4D(writer, draft.ArchetypeId);
        WriteUx4D(writer, draft.RulesetId); WriteUx4D(writer, draft.RigId);
        WriteUx4D(writer, draft.FactionId); writer.Write((int)draft.EncounterRole);
        WriteUx4D(writer, draft.ThreatRating); WriteUx4D(writer, draft.VisualKitId);
        WriteUx4D(writer, draft.CombatTags); WriteUx4D(writer, draft.SpeciesId);
        WriteUx4D(writer, draft.ParentSpeciesIds); WriteUx4D(writer, draft.TraitSets);
        WriteUx4D(writer, draft.Variants); WriteUx4D(writer, draft.RequiredSkeletonId);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteUx4D(BinaryWriter writer, string? value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string HashUx4D(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static Guid StableCreatureIdUx4D(Guid basis, string segment)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{basis:D}:ux4-d:{segment}"));
        Span<byte> id = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(id);
        id[7] = (byte)((id[7] & 0x0F) | 0x40);
        id[8] = (byte)((id[8] & 0x3F) | 0x80);
        return new Guid(id);
    }

    private static IReadOnlyList<string> Split(string value) => value
        .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.Ordinal).ToArray();

    private static string Join(IEnumerable<string> values) => string.Join(", ", values);

    private static string Text(IReadOnlyDictionary<string, string>? values, string locale, string fallback) =>
        values is not null && values.TryGetValue(locale, out var value) ? value : fallback;

    private static string FormatTraitSetsUx4D(IEnumerable<CreatureSpeciesTraitSet> values) => string.Join("\n",
        values.Select(item => $"{item.Id}={string.Join(',', item.Traits)}"));

    private static bool DeleteUx4DSmokeRoot(string root)
    {
        try
        {
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4d-smoke-", StringComparison.Ordinal))
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
