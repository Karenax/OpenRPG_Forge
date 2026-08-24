using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Forge.Application.Commands;
using Forge.Application.Dialogues;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux5AEvidenceProvenance = "studio:ux5-a:evidence";
    private static readonly Regex Ux5AGuidPattern = new(
        @"(?<![0-9a-fA-F])(?:[0-9a-fA-F]{32}|[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12})(?![0-9a-fA-F])",
        RegexOptions.CultureInvariant);
    private readonly SetDialogueHandler _setDialogueUx5A = new(new SystemClock());
    private DialogueDefinition? _approvedDialogueCandidateUx5A;
    private ProjectRevision? _approvedDialogueRevisionUx5A;
    private string? _approvedDialogueSourceFingerprintUx5A;
    private string? _approvedDialogueCandidateFingerprintUx5A;

    internal IReadOnlyList<StudioDialogueChoiceUx5A> DialogueChoicesUx5A
    {
        get
        {
            var project = Project;
            if (project is null)
            {
                return [];
            }

            var duplicateNames = project.Dialogues
                .GroupBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
            return project.Dialogues
                .Select(dialogue => new StudioDialogueChoiceUx5A(
                    dialogue.Id,
                    duplicateNames.Contains(dialogue.DisplayName)
                        ? $"{dialogue.DisplayName} · {dialogue.Slug}"
                        : dialogue.DisplayName,
                    dialogue.Nodes.Count,
                    dialogue.Nodes.Sum(node => node.Choices.Count)))
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioDialogueSpeakerChoiceUx5A> DialogueSpeakerChoicesUx5A
    {
        get
        {
            var project = Project;
            if (project is null)
            {
                return [];
            }

            var referencedSpeakerIds = project.Dialogues
                .SelectMany(dialogue => dialogue.Nodes)
                .Where(node => node.SpeakerBlueprintId is not null)
                .Select(node => node.SpeakerBlueprintId!)
                .ToHashSet();
            var candidates = project.Blueprints
                .Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn ||
                               referencedSpeakerIds.Contains(item.Id))
                .ToArray();
            var duplicateNames = candidates
                .GroupBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
            return new[] { new StudioDialogueSpeakerChoiceUx5A(null, "Narration / système", false) }
                .Concat(candidates.Select(blueprint =>
                {
                    var protectedSpeaker = blueprint.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn);
                    var label = duplicateNames.Contains(blueprint.DisplayName)
                        ? $"{blueprint.DisplayName} · {blueprint.Slug}"
                        : blueprint.DisplayName;
                    if (protectedSpeaker)
                    {
                        label += " · intervenant hérité protégé";
                    }
                    return new StudioDialogueSpeakerChoiceUx5A(blueprint.Id, label, protectedSpeaker);
                }))
                .OrderBy(item => item.Id is null ? 0 : 1)
                .ThenBy(item => item.Protected)
                .ThenBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal StudioDialogueDraftUx5A? CreateDialogueDraftUx5A(DialogueId? requestedDialogueId = null)
    {
        var history = History;
        var project = Project;
        if (history is null || project is null || requestedDialogueId is null)
        {
            return null;
        }

        var matches = project.Dialogues.Where(item => item.Id == requestedDialogueId).ToArray();
        return matches.Length == 1
            ? CreateDialogueDraftUx5A(matches[0], history.State.Revision, ProjectFingerprintUx5A(project), false)
            : null;
    }

    internal StudioDialogueDraftUx5A? CreateNewDialogueDraftUx5A()
    {
        var history = History;
        var project = Project;
        if (history is null || project is null)
        {
            return null;
        }

        var suffix = 1;
        string slug;
        do
        {
            slug = $"nouveau-dialogue-{suffix++}";
        } while (project.Dialogues.Any(item => string.Equals(item.Slug, slug, StringComparison.Ordinal)));

        var dialogueId = DialogueId.From(Guid.NewGuid());
        var entryId = DialogueNodeId.From(Guid.NewGuid());
        var narrator = DialogueParticipant.FromSpeaker(null);
        var entry = new DialogueNode(
            entryId,
            null,
            new LocalizedText($"{slug}.entry", "Nouvelle réplique", provenance: "author:studio-ux5-a"),
            role: DialogueNodeRole.End);
        var source = new DialogueDefinition(
            dialogueId,
            slug,
            "Nouveau dialogue",
            entryId,
            [entry],
            schemaVersion: DialogueDefinition.CurrentSchemaVersion,
            provenance: "author:studio-ux5-a",
            metadata: new Dictionary<string, string> { ["kind"] = "conversation" },
            participants: [narrator],
            entryPoints:
            [
                new DialogueEntryPoint(
                    Guid.NewGuid(),
                    DialogueEntryPoint.CurrentSchemaVersion,
                    "default",
                    entryId,
                    0,
                    "author:studio-ux5-a"),
            ]);
        return CreateDialogueDraftUx5A(
            source,
            history.State.Revision,
            ProjectFingerprintUx5A(project),
            true);
    }

    internal StudioDialoguePreviewUx5A PreviewDialogueUx5A(StudioDialogueDraftUx5A draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelDialogueUx5A();
        var history = History;
        var project = Project;
        if (history is null || project is null)
        {
            return FailedDialoguePreviewUx5A("Aucun module n’est ouvert.");
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            return FailedDialoguePreviewUx5A(
                "Le projet a changé depuis l’ouverture du dialogue. Rechargez-le avant le dry-run.");
        }

        string currentFingerprint;
        try
        {
            currentFingerprint = ProjectFingerprintUx5A(project);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ProjectValidationException)
        {
            return FailedDialoguePreviewUx5A(
                $"Le projet courant ne peut pas être empreinté sans ambiguïté ({exception.GetType().Name}).");
        }
        if (!string.Equals(draft.SourceProjectFingerprint, currentFingerprint, StringComparison.Ordinal))
        {
            return FailedDialoguePreviewUx5A(
                "Le contenu du projet ne correspond plus à la fiche ouverte. Rechargez le dialogue.");
        }

        if (!TryBuildDialogueUx5A(draft, out var candidate, out var candidateProject, out var failure))
        {
            return FailedDialoguePreviewUx5A(failure);
        }
        if (candidate is null || candidateProject is null)
        {
            return FailedDialoguePreviewUx5A(
                "Le builder canonique n’a produit aucun candidat applicable.");
        }

        var stateBefore = history.State;
        var revisionBefore = history.State.Revision;
        var journalBefore = history.Journal.ToArray();
        var command = new SetDialogueCommand(
            CommandId.From(Guid.NewGuid()),
            draft.BaseRevision,
            candidate,
            DryRun: true);
        var result = _setDialogueUx5A.Handle(history.State, command);
        if (!result.Succeeded)
        {
            var diagnostics = result.Diagnostics.Select(HumanizeDialogueDiagnosticUx5A).ToArray();
            return new StudioDialoguePreviewUx5A(
                false,
                diagnostics.FirstOrDefault()?.Message ??
                "Le graphe canonique a refusé ce brouillon sans modifier le projet.",
                BuildDialogueDiffUx5A(draft.Source, candidate, draft.IsNew),
                false,
                candidate.Nodes.Count,
                candidate.Nodes.Sum(node => node.Choices.Count),
                candidate.Edges.Count(edge => edge.Kind == DialogueEdgeKind.Reference),
                diagnostics);
        }
        if (!ReferenceEquals(stateBefore, result.State) || result.Audit.Applied ||
            history.State.Revision != revisionBefore || !history.Journal.SequenceEqual(journalBefore))
        {
            return FailedDialoguePreviewUx5A(
                "Le dry-run canonique n’a pas respecté son contrat de non-mutation.");
        }

        var candidateFingerprint = ProjectFingerprintUx5A(candidateProject);
        _approvedDialogueCandidateUx5A = candidate;
        _approvedDialogueRevisionUx5A = revisionBefore;
        _approvedDialogueSourceFingerprintUx5A = currentFingerprint;
        _approvedDialogueCandidateFingerprintUx5A = candidateFingerprint;
        return new StudioDialoguePreviewUx5A(
            true,
            "Aperçu validé · aucun document n’a été modifié.",
            BuildDialogueDiffUx5A(draft.Source, candidate, draft.IsNew),
            true,
            candidate.Nodes.Count,
            candidate.Nodes.Sum(node => node.Choices.Count),
            candidate.Edges.Count(edge => edge.Kind == DialogueEdgeKind.Reference),
            []);
    }

    internal StudioDialogueActionResultUx5A ApplyApprovedDialogueUx5A()
    {
        var history = History;
        var project = Project;
        var candidate = _approvedDialogueCandidateUx5A;
        if (history is null || project is null || candidate is null ||
            _approvedDialogueRevisionUx5A != history.State.Revision)
        {
            CancelDialogueUx5A();
            return new(false, "Prévisualisez de nouveau le dialogue à la révision courante avant de l’appliquer.");
        }

        try
        {
            var sourceFingerprint = ProjectFingerprintUx5A(project);
            var candidateProject = ReplaceDialogueUx5A(project, candidate);
            var candidateFingerprint = ProjectFingerprintUx5A(candidateProject);
            if (!string.Equals(
                    _approvedDialogueSourceFingerprintUx5A,
                    sourceFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    _approvedDialogueCandidateFingerprintUx5A,
                    candidateFingerprint,
                    StringComparison.Ordinal))
            {
                CancelDialogueUx5A();
                return new(false, "L’aperçu ne correspond plus exactement au projet ou au brouillon courant.");
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or ProjectValidationException)
        {
            CancelDialogueUx5A();
            return new(false,
                $"L’empreinte canonique ne peut pas être vérifiée ({exception.GetType().Name}). Aucune écriture effectuée.");
        }

        var command = new SetDialogueCommand(
            CommandId.From(Guid.NewGuid()),
            history.State.Revision,
            candidate,
            DryRun: false);
        var result = _setDialogueUx5A.Handle(history.State, command);
        CancelDialogueUx5A();
        if (!result.Succeeded)
        {
            return new(false,
                result.Diagnostics.Select(HumanizeDialogueDiagnosticUx5A).FirstOrDefault()?.Message ??
                "Le dialogue canonique a été refusé. Le projet reste inchangé.");
        }

        history.Record(result);
        InvalidateContentIndexes();
        return new(true, "Dialogue appliqué · Ctrl+Z pour annuler, Ctrl+Y pour rétablir.");
    }

    internal void CancelDialogueUx5A()
    {
        _approvedDialogueCandidateUx5A = null;
        _approvedDialogueRevisionUx5A = null;
        _approvedDialogueSourceFingerprintUx5A = null;
        _approvedDialogueCandidateFingerprintUx5A = null;
    }

    internal void CloseUx5A() => CancelDialogueUx5A();

    internal StudioDialogueApprovalSnapshotUx5A CaptureDialogueApprovalUx5A() => new(
        _approvedDialogueCandidateUx5A,
        _approvedDialogueRevisionUx5A,
        _approvedDialogueSourceFingerprintUx5A,
        _approvedDialogueCandidateFingerprintUx5A);

    internal void RestoreDialogueApprovalUx5A(StudioDialogueApprovalSnapshotUx5A snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedDialogueCandidateUx5A = snapshot.Candidate;
        _approvedDialogueRevisionUx5A = snapshot.Revision;
        _approvedDialogueSourceFingerprintUx5A = snapshot.SourceFingerprint;
        _approvedDialogueCandidateFingerprintUx5A = snapshot.CandidateFingerprint;
    }

    internal StudioDialogueEvidenceWorkspaceUx5A? CreateUx5AEvidenceWorkspace()
    {
        var evidenceProject = CreateUx5AEvidenceProject();
        if (evidenceProject is null)
        {
            return null;
        }

        var rootPath = StudioDialogueEvidenceWorkspaceUx5A.CreateRootPath();
        var evidenceSession = new StudioSessionController();
        var workspace = new StudioDialogueEvidenceWorkspaceUx5A(evidenceSession, rootPath);
        try
        {
            var created = ProjectSession.Create(rootPath, evidenceProject);
            if (!created.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX5A_EVIDENCE_CREATE_FAILED diagnostics=" +
                    string.Join("|", created.Diagnostics.Select(item => item.Code)));
                workspace.Dispose();
                return null;
            }

            var opened = evidenceSession.Open(rootPath);
            if (!opened.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX5A_EVIDENCE_OPEN_FAILED diagnostics=" +
                    string.Join("|", opened.Diagnostics.Select(item => item.Code)));
                workspace.Dispose();
                return null;
            }
            return workspace;
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX5A_EVIDENCE_EXCEPTION type={exception.GetType().Name}");
            workspace.Dispose();
            return null;
        }
    }

    internal StudioUx5ASmokeResult RunUx5AContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        if (sourceProject is null || sourceHistory is null)
        {
            return FailedUx5ASmoke("Aucune session source n’est ouverte pour la preuve isolée.");
        }

        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceCanUndo = sourceHistory.CanUndo;
        var sourceCanRedo = sourceHistory.CanRedo;
        var sourceDirty = IsDirty;
        var sourceAreaId = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceApproval = CaptureDialogueApprovalUx5A();
        var workspace = CreateUx5AEvidenceWorkspace();
        if (workspace is null)
        {
            return FailedUx5ASmoke("Le workspace temporaire UX5-A n’a pas pu être créé.");
        }

        StudioUx5ASmokeResult result;
        StudioSessionController? reopened = null;
        try
        {
            var editing = workspace.Session;
            var explicitSelection = editing.CreateDialogueDraftUx5A() is null;
            var evidenceChoice = editing.DialogueChoicesUx5A.Single(item =>
                item.Label.Contains("Branche témoin", StringComparison.Ordinal));
            var draft = editing.CreateDialogueDraftUx5A(evidenceChoice.Id);
            if (draft is null)
            {
                throw new InvalidOperationException("The isolated UX5-A dialogue draft is unavailable.");
            }
            var protectedSpeakerId = draft.Lines[0].SpeakerBlueprintId;
            var protectedSpeakerSurfaced = protectedSpeakerId is not null &&
                                            editing.DialogueSpeakerChoicesUx5A.Any(item =>
                                                item.Id == protectedSpeakerId && item.Protected &&
                                                item.Label.Contains("hérité protégé", StringComparison.OrdinalIgnoreCase) &&
                                                !Ux5AGuidPattern.IsMatch(item.Label));
            var protectedSubgraphLine = draft.Lines.SingleOrDefault(line =>
                line.Role == DialogueNodeRole.Subgraph);
            var subgraphDraftProtected = protectedSubgraphLine is not null &&
                                         !string.IsNullOrWhiteSpace(protectedSubgraphLine.Original.SubgraphKey);
            var branchesUnlimited = draft.Lines.Count > 25 && draft.Lines[0].Choices.Count > 25;
            var multipleEntries = draft.Source.EntryPoints.Count > 1 &&
                                  draft.Source.EntryPoints.Select(item => item.Key).Distinct().Count() ==
                                  draft.Source.EntryPoints.Count;

            var referencePreview = editing.PreviewDialogueUx5A(draft);
            var referenceCycleAccepted = referencePreview.Succeeded && referencePreview.ReferenceCount > 0;
            var structuralChoiceDiff = referencePreview.Succeeded &&
                                       referencePreview.Summary.Contains(
                                           "0 ligne(s) modifiée(s)",
                                           StringComparison.Ordinal);

            var identityDisplayName = "Branche témoin renommée";
            var identitySlug = draft.Source.Slug + "-renommee";
            var identityCandidate = CopyDialogueUx5A(
                draft.Source,
                identityDisplayName,
                identitySlug,
                draft.Source.Nodes[1].Id);
            var identityDiff = BuildDialogueDiffUx5A(draft.Source, identityCandidate, isNew: false);
            var identityDiffHuman = identityDiff.Contains(identityDisplayName, StringComparison.Ordinal) &&
                                    identityDiff.Contains(identitySlug, StringComparison.Ordinal) &&
                                    identityDiff.Contains("entrée", StringComparison.OrdinalIgnoreCase) &&
                                    identityDiff.Contains("ligne 02", StringComparison.Ordinal) &&
                                    !Ux5AGuidPattern.IsMatch(identityDiff);

            var referenceLineIndex = draft.Lines.ToList().FindIndex(line =>
                line.Choices.Any(choice => choice.EdgeKind == DialogueEdgeKind.Reference));
            var referenceChoiceIndex = draft.Lines[referenceLineIndex].Choices.ToList().FindIndex(choice =>
                choice.EdgeKind == DialogueEdgeKind.Reference);
            var flowCycleDraft = ReplaceChoiceDraftUx5A(
                draft,
                referenceLineIndex,
                referenceChoiceIndex,
                draft.Lines[referenceLineIndex].Choices[referenceChoiceIndex] with
                {
                    EdgeKind = DialogueEdgeKind.Flow,
                });
            var flowPreview = editing.PreviewDialogueUx5A(flowCycleDraft);
            var flowCycleBlocked = !flowPreview.Succeeded &&
                                   flowPreview.Diagnostics.Any(item =>
                                       item.Code == DialogueValidator.AccidentalCycleCode);

            var missingTargetDraft = ReplaceChoiceDraftUx5A(
                draft,
                0,
                0,
                draft.Lines[0].Choices[0] with
                {
                    TargetNodeId = DialogueNodeId.From(Guid.Parse("d5a00000-0000-4000-8000-00000000ffff")),
                });
            var missingTargetPreview = editing.PreviewDialogueUx5A(missingTargetDraft);
            var missingTargetBlocked = !missingTargetPreview.Succeeded &&
                                       missingTargetPreview.Diagnostics.Any(item =>
                                           item.Code == DialogueValidator.MissingTargetNodeCode);

            var negativePriorityDraft = ReplaceChoiceDraftUx5A(
                draft,
                0,
                0,
                draft.Lines[0].Choices[0] with { PriorityText = "-1" });
            var negativePriorityPreview = editing.PreviewDialogueUx5A(negativePriorityDraft);
            var negativePriorityBlocked = !negativePriorityPreview.Succeeded &&
                                          negativePriorityPreview.Diagnostics.Any(item =>
                                              item.Code == DialogueValidator.EdgeInvalidCode);

            var reordered = MoveChoiceDraftUx5A(draft, 0, 0, 1);
            var history = editing.History!;
            var beforeProject = editing.Project;
            var beforeRevision = history.State.Revision;
            var beforeJournal = history.Journal.ToArray();
            var preview = editing.PreviewDialogueUx5A(reordered);
            var dryRunPure = preview.Succeeded && ReferenceEquals(beforeProject, editing.Project) &&
                             history.State.Revision == beforeRevision &&
                             history.Journal.SequenceEqual(beforeJournal);
            var reorderDiffHuman = preview.Summary.Contains("Réponses réordonnées", StringComparison.Ordinal) &&
                                   preview.Summary.Contains(
                                       reordered.Lines[0].Choices[0].Text,
                                       StringComparison.CurrentCulture) &&
                                   !Ux5AGuidPattern.IsMatch(preview.Summary);

            var approvedCandidate = editing._approvedDialogueCandidateUx5A!;
            editing._approvedDialogueCandidateUx5A = CopyDialogueUx5A(
                approvedCandidate,
                displayName: approvedCandidate.DisplayName + " altéré après aperçu");
            var fingerprintGuard = !editing.ApplyApprovedDialogueUx5A().Succeeded;

            preview = editing.PreviewDialogueUx5A(reordered);
            var applied = editing.ApplyApprovedDialogueUx5A();
            var appliedDialogue = editing.Project!.Dialogues.Single(item => item.Id == draft.DialogueId);
            var canonicalCommand = applied.Succeeded &&
                                   history.Journal.LastOrDefault()?.CommandName == "set_dialogue" &&
                                   appliedDialogue.Nodes[0].Choices[0].Id ==
                                   reordered.Lines[0].Choices[0].Original.Id;
            var expectedEdges = appliedDialogue.Nodes.SelectMany(node => node.Choices
                    .Select((choice, index) => (choice, index))
                    .Where(item => item.choice.TargetNodeId is not null)
                    .Select(item => new DialogueGraphEdge(
                        item.choice.Id,
                        node.Id,
                        item.choice.TargetNodeId!,
                        item.choice.EdgeKind,
                        item.index,
                        item.choice.Priority)))
                .OrderBy(item => item.SourceNodeId.Value)
                .ThenBy(item => item.Order)
                .ToArray();
            var edgesCanonical = appliedDialogue.Edges.SequenceEqual(expectedEdges);
            var structuralPreservation = applied.Succeeded &&
                                         PreservesDialogueNeighborsUx5A(draft.Source, appliedDialogue);
            var protectedSpeakerPreserved = protectedSpeakerSurfaced && applied.Succeeded &&
                                            appliedDialogue.Nodes[0].SpeakerBlueprintId == protectedSpeakerId;
            var appliedSubgraph = protectedSubgraphLine is null
                ? null
                : appliedDialogue.Nodes.SingleOrDefault(node => node.Id == protectedSubgraphLine.Original.Id);
            var subgraphProtected = subgraphDraftProtected && appliedSubgraph is not null &&
                                    appliedSubgraph.Role == DialogueNodeRole.Subgraph &&
                                    string.Equals(
                                        appliedSubgraph.SubgraphKey,
                                        protectedSubgraphLine!.Original.SubgraphKey,
                                        StringComparison.Ordinal);

            var stalePreview = editing.PreviewDialogueUx5A(draft);
            var staleRevisionBlocked = !stalePreview.Succeeded;
            var undo = editing.Undo();
            var undone = editing.Project!.Dialogues.Single(item => item.Id == draft.DialogueId)
                .Nodes[0].Choices[0].Id == draft.Lines[0].Choices[0].Original.Id;
            var redo = editing.Redo();
            var redone = editing.Project!.Dialogues.Single(item => item.Id == draft.DialogueId)
                .Nodes[0].Choices[0].Id == reordered.Lines[0].Choices[0].Original.Id;
            var undoRedo = undo.Succeeded && undone && redo.Succeeded && redone;

            var newDialogue = false;
            var newDraft = editing.CreateNewDialogueDraftUx5A();
            if (newDraft is not null)
            {
                newDraft = newDraft with
                {
                    DisplayName = "Nouveau dialogue témoin",
                    Slug = "nouveau-dialogue-temoin-ux5-a",
                };
                var newPreview = editing.PreviewDialogueUx5A(newDraft);
                var newApplied = newPreview.Succeeded
                    ? editing.ApplyApprovedDialogueUx5A()
                    : new StudioDialogueActionResultUx5A(false, "Aperçu du dialogue neuf refusé.");
                newDialogue = newPreview.Succeeded && newApplied.Succeeded &&
                              newPreview.Summary.Contains("Dialogue neuf", StringComparison.Ordinal) &&
                              newPreview.Summary.Contains(newDraft.DisplayName, StringComparison.Ordinal) &&
                              newPreview.Summary.Contains(newDraft.Slug, StringComparison.Ordinal) &&
                              newPreview.Summary.Contains("entrée ligne 01", StringComparison.OrdinalIgnoreCase) &&
                              !Ux5AGuidPattern.IsMatch(newPreview.Summary) &&
                              editing.Project!.Dialogues.Any(dialogue =>
                                  dialogue.Id == newDraft.DialogueId && dialogue.DisplayName == newDraft.DisplayName &&
                                  dialogue.Slug == newDraft.Slug);
                canonicalCommand &= newApplied.Succeeded &&
                                    history.Journal.LastOrDefault()?.CommandName == "set_dialogue";
            }

            var serialized = ProjectJsonSerializer.Serialize(editing.Project!);
            var serializerRoundTrip = string.Equals(
                ProjectJsonSerializer.Serialize(ProjectJsonSerializer.Deserialize(serialized)),
                serialized,
                StringComparison.Ordinal);
            var saved = editing.Save().Succeeded;
            reopened = new StudioSessionController();
            var opened = reopened.Open(workspace.RootPath);
            var persisted = opened.Succeeded && string.Equals(
                ProjectJsonSerializer.Serialize(reopened.Project!),
                serialized,
                StringComparison.Ordinal);
            var roundTrip = serializerRoundTrip && saved && persisted;

            var currentSourceHistory = History;
            var sourceUntouched = ReferenceEquals(sourceProject, Project) &&
                                  ReferenceEquals(sourceHistory, currentSourceHistory) &&
                                  currentSourceHistory is not null &&
                                  currentSourceHistory.State.Revision == sourceRevision &&
                                  currentSourceHistory.Journal.SequenceEqual(sourceJournal) &&
                                  currentSourceHistory.CanUndo == sourceCanUndo &&
                                  currentSourceHistory.CanRedo == sourceCanRedo &&
                                  IsDirty == sourceDirty &&
                                  ActiveAreaId == sourceAreaId &&
                                  SelectedEntityIds.SequenceEqual(sourceSelection) &&
                                  SelectedEntityId == sourceSelected &&
                                  Equals(CaptureDialogueApprovalUx5A(), sourceApproval);
            var succeeded = explicitSelection && newDialogue && protectedSpeakerPreserved && subgraphProtected &&
                            structuralChoiceDiff && identityDiffHuman && branchesUnlimited && multipleEntries &&
                            referenceCycleAccepted && flowCycleBlocked && missingTargetBlocked &&
                            negativePriorityBlocked && dryRunPure && staleRevisionBlocked &&
                            fingerprintGuard && canonicalCommand && edgesCanonical && reorderDiffHuman && undoRedo &&
                            roundTrip && structuralPreservation && sourceUntouched;
            result = new StudioUx5ASmokeResult(
                succeeded,
                explicitSelection,
                newDialogue,
                protectedSpeakerPreserved,
                subgraphProtected,
                structuralChoiceDiff,
                identityDiffHuman,
                branchesUnlimited,
                multipleEntries,
                referenceCycleAccepted,
                flowCycleBlocked,
                missingTargetBlocked,
                negativePriorityBlocked,
                dryRunPure,
                staleRevisionBlocked,
                fingerprintGuard,
                canonicalCommand,
                edgesCanonical,
                reorderDiffHuman,
                undoRedo,
                roundTrip,
                structuralPreservation,
                sourceUntouched,
                ResourcesReleased: false,
                "Preuve contractuelle UX5-A exécutée dans un workspace temporaire borné.");
        }
        catch (Exception exception)
        {
            result = FailedUx5ASmoke(
                $"La preuve contractuelle UX5-A a échoué ({exception.GetType().Name}).");
        }
        finally
        {
            if (reopened is not null)
            {
                reopened.CloseUx5A();
                reopened.Close();
            }
            workspace.Dispose();
        }

        var finalSourceHistory = History;
        var sourceStillUntouched = ReferenceEquals(sourceProject, Project) &&
                                   ReferenceEquals(sourceHistory, finalSourceHistory) &&
                                   finalSourceHistory is not null &&
                                   finalSourceHistory.State.Revision == sourceRevision &&
                                   finalSourceHistory.Journal.SequenceEqual(sourceJournal) &&
                                   finalSourceHistory.CanUndo == sourceCanUndo &&
                                   finalSourceHistory.CanRedo == sourceCanRedo &&
                                   IsDirty == sourceDirty &&
                                   ActiveAreaId == sourceAreaId &&
                                   SelectedEntityIds.SequenceEqual(sourceSelection) &&
                                   SelectedEntityId == sourceSelected &&
                                   Equals(CaptureDialogueApprovalUx5A(), sourceApproval);
        return result with
        {
            Succeeded = result.Succeeded && sourceStillUntouched && workspace.ResourcesReleased,
            SourceUntouched = result.SourceUntouched && sourceStillUntouched,
            ResourcesReleased = workspace.ResourcesReleased,
        };
    }

    private static StudioDialogueDraftUx5A CreateDialogueDraftUx5A(
        DialogueDefinition dialogue,
        ProjectRevision revision,
        string sourceProjectFingerprint,
        bool isNew) => new(
        dialogue.Id,
        dialogue.DisplayName,
        dialogue.Slug,
        dialogue.EntryNodeId,
        dialogue.Nodes.Select(node => new StudioDialogueLineDraftUx5A(
            node,
            node.Text.Fallback,
            node.Role,
            node.SpeakerBlueprintId,
            node.ParticipantId,
            node.Choices.Select(choice => new StudioDialogueChoiceDraftUx5A(
                choice,
                choice.Text.Fallback,
                choice.TargetNodeId,
                choice.EdgeKind,
                choice.Priority.ToString(CultureInfo.InvariantCulture))).ToArray())).ToArray(),
        revision,
        sourceProjectFingerprint,
        dialogue,
        isNew);

    private bool TryBuildDialogueUx5A(
        StudioDialogueDraftUx5A draft,
        out DialogueDefinition? candidate,
        out ProjectDefinition? candidateProject,
        out string failure)
    {
        candidate = null;
        candidateProject = null;
        failure = string.Empty;
        var project = Project;
        var history = History;
        if (project is null || history is null)
        {
            failure = "Aucun module n’est ouvert.";
            return false;
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            failure = "Le projet a changé. Rechargez le dialogue avant de poursuivre.";
            return false;
        }
        if (draft.Lines.Count == 0)
        {
            failure = "Un dialogue doit conserver au moins une ligne.";
            return false;
        }
        if (draft.Lines.Select(item => item.Original.Id).Distinct().Count() != draft.Lines.Count)
        {
            failure = "Deux lignes du brouillon portent la même identité interne.";
            return false;
        }
        if (!draft.IsNew && project.Dialogues.Count(item => item.Id == draft.DialogueId) != 1)
        {
            failure = "Le dialogue sélectionné n’existe plus ou est ambigu. Rechargez la page.";
            return false;
        }
        if (draft.IsNew && project.Dialogues.Any(item => item.Id == draft.DialogueId))
        {
            failure = "Ce nouveau dialogue existe déjà. Actualisez la liste avant de continuer.";
            return false;
        }

        try
        {
            var nodes = new List<DialogueNode>(draft.Lines.Count);
            foreach (var line in draft.Lines)
            {
                if (string.IsNullOrWhiteSpace(line.Text))
                {
                    failure = "Chaque ligne doit contenir un texte lisible.";
                    return false;
                }
                var choices = new List<DialogueChoice>(line.Choices.Count);
                foreach (var choice in line.Choices)
                {
                    if (string.IsNullOrWhiteSpace(choice.Text))
                    {
                        failure = "Chaque réponse doit contenir un texte lisible.";
                        return false;
                    }
                    if (!int.TryParse(
                            choice.PriorityText,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var priority))
                    {
                        failure = "La priorité d’une réponse doit être un entier explicite.";
                        return false;
                    }
                    choices.Add(new DialogueChoice(
                        choice.Original.Id,
                        CopyLocalizedTextUx5A(choice.Original.Text, choice.Text),
                        choice.Original.Conditions,
                        choice.Original.Actions,
                        choice.TargetNodeId,
                        choice.EdgeKind,
                        priority,
                        choice.Original.ConditionCombination));
                }

                nodes.Add(new DialogueNode(
                    line.Original.Id,
                    line.SpeakerBlueprintId,
                    CopyLocalizedTextUx5A(line.Original.Text, line.Text),
                    line.Original.OnEnterActions,
                    choices,
                    line.Role,
                    line.ParticipantId,
                    line.Original.SubgraphKey));
            }

            var participants = draft.Source.Participants.ToList();
            foreach (var line in draft.Lines)
            {
                if (participants.Any(item => item.Id == line.ParticipantId))
                {
                    continue;
                }
                var derived = DialogueParticipant.FromSpeaker(line.SpeakerBlueprintId);
                if (derived.Id != line.ParticipantId || participants.Any(item =>
                        string.Equals(item.Role, derived.Role, StringComparison.Ordinal)))
                {
                    failure = "Le participant associé à une ligne ne peut pas être préservé sans ambiguïté.";
                    return false;
                }
                participants.Add(derived);
            }

            var entries = draft.Source.EntryPoints.ToArray();
            if (draft.EntryNodeId != draft.Source.EntryNodeId)
            {
                var canonicalEntryIndex = Array.FindIndex(entries, item =>
                    string.Equals(item.Key, "default", StringComparison.Ordinal));
                if (canonicalEntryIndex < 0)
                {
                    canonicalEntryIndex = Array.FindIndex(entries, item => item.NodeId == draft.Source.EntryNodeId);
                }
                if (canonicalEntryIndex < 0)
                {
                    failure = "L’entrée canonique existante ne peut pas être remplacée sans ambiguïté.";
                    return false;
                }
                entries[canonicalEntryIndex] = entries[canonicalEntryIndex] with { NodeId = draft.EntryNodeId };
            }

            candidate = new DialogueDefinition(
                draft.DialogueId,
                draft.Slug,
                draft.DisplayName,
                draft.EntryNodeId,
                nodes,
                draft.Source.Presentation,
                draft.Source.SchemaVersion,
                draft.Source.Provenance,
                draft.Source.Metadata,
                participants,
                entries,
                edges: null,
                authoring: draft.Source.Authoring,
                experience: draft.Source.Experience,
                production: draft.Source.Production);
            candidateProject = ReplaceDialogueUx5A(project, candidate);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            failure = $"Le brouillon est incomplet : {RedactTechnicalIdsUx5A(exception.Message)}";
            return false;
        }
    }

    private ProjectDefinition? CreateUx5AEvidenceProject()
    {
        var project = Project;
        if (project is null)
        {
            return null;
        }

        var occupied = new[] { project.Id.Value, project.Module.Id.Value }
            .Concat(project.Blueprints.Select(item => item.Id.Value))
            .Concat(project.Areas.Select(item => item.Id.Value))
            .Concat(project.Dialogues.Select(item => item.Id.Value))
            .ToHashSet();
        var salt = 0;
        Guid speakerGuid;
        Guid dialogueGuid;
        do
        {
            speakerGuid = StableUx5AId(project.Id.Value, $"speaker:{salt}");
            dialogueGuid = StableUx5AId(project.Id.Value, $"dialogue:{salt}");
            salt++;
        } while (speakerGuid == dialogueGuid || occupied.Contains(speakerGuid) || occupied.Contains(dialogueGuid));

        var speakerId = BlueprintId.From(speakerGuid);
        var speakerSlug = "temoin-dialogue-ux5-a";
        var slugSuffix = 1;
        while (project.Blueprints.Any(item => string.Equals(item.Slug, speakerSlug, StringComparison.Ordinal)))
        {
            speakerSlug = $"temoin-dialogue-ux5-a-{slugSuffix++}";
        }
        var speaker = new BlueprintDefinition(
            speakerId,
            speakerSlug,
            "Témoin des branches",
            BlueprintKind.Placeable);

        var dialogueSlug = "branche-temoin-ux5-a";
        slugSuffix = 1;
        while (project.Dialogues.Any(item => string.Equals(item.Slug, dialogueSlug, StringComparison.Ordinal)))
        {
            dialogueSlug = $"branche-temoin-ux5-a-{slugSuffix++}";
        }

        var entryId = DialogueNodeId.From(StableUx5AId(dialogueGuid, "node:entry"));
        var leafIds = Enumerable.Range(1, 27)
            .Select(index => DialogueNodeId.From(StableUx5AId(dialogueGuid, $"node:{index}")))
            .ToArray();
        var entryChoices = leafIds.Select((target, index) => new DialogueChoice(
            DialogueChoiceId.From(StableUx5AId(dialogueGuid, $"choice:entry:{index}")),
            new LocalizedText(
                StableUx5AId(dialogueGuid, $"text:choice:{index}"),
                LocalizedText.CurrentSchemaVersion,
                Ux5AEvidenceProvenance,
                $"dialogue.ux5a.choice-{index + 1}",
                $"Réponse témoin {index + 1:00}",
                index == 0
                    ? [LocalizedTextVariant.Create(
                        $"dialogue.ux5a.choice-{index + 1}",
                        "en",
                        "Evidence answer one",
                        provenance: Ux5AEvidenceProvenance)]
                    : []),
            conditions: index == 0
                ? [new DialogueCondition(DialogueConditionKind.HasItem, "cle-temoin", "true")]
                : [],
            actions: index == 0
                ? [new DialogueAction(DialogueActionKind.PlayAudio, "dialogue.temoin", "play")]
                : [],
            targetNodeId: target,
            priority: index)).ToArray();
        var narrator = DialogueParticipant.FromSpeaker(null);
        var speakingParticipant = DialogueParticipant.FromSpeaker(speakerId);
        var entry = new DialogueNode(
            entryId,
            speakerId,
            new LocalizedText(
                StableUx5AId(dialogueGuid, "text:entry"),
                LocalizedText.CurrentSchemaVersion,
                Ux5AEvidenceProvenance,
                "dialogue.ux5a.entry",
                "Quelle branche souhaitez-vous explorer ?",
                [LocalizedTextVariant.Create(
                    "dialogue.ux5a.entry",
                    "en",
                    "Which branch would you like to explore?",
                    provenance: Ux5AEvidenceProvenance)]),
            [new DialogueAction(DialogueActionKind.SetRelation, "temoin", "met")],
            entryChoices,
            DialogueNodeRole.SpeakerLine,
            speakingParticipant.Id,
            null);
        var leaves = leafIds.Select((nodeId, index) => index switch
        {
            0 => new DialogueNode(
                nodeId,
                null,
                new LocalizedText($"dialogue.ux5a.leaf-{index + 1}", $"Branche {index + 1:00}"),
                choices:
                [
                    new DialogueChoice(
                        DialogueChoiceId.From(StableUx5AId(dialogueGuid, "choice:reference")),
                        new LocalizedText("dialogue.ux5a.reference", "Revenir comme référence"),
                        targetNodeId: entryId,
                        edgeKind: DialogueEdgeKind.Reference,
                        priority: 1),
                    new DialogueChoice(
                        DialogueChoiceId.From(StableUx5AId(dialogueGuid, "choice:leave")),
                        new LocalizedText("dialogue.ux5a.leave", "Terminer ici"),
                        priority: 2),
                ],
                role: DialogueNodeRole.PlayerChoice,
                subgraphKey: null),
            1 => new DialogueNode(
                nodeId,
                null,
                new LocalizedText($"dialogue.ux5a.leaf-{index + 1}", $"Branche {index + 1:00}"),
                role: DialogueNodeRole.Subgraph,
                subgraphKey: "branche-secondaire-protegee"),
            _ => new DialogueNode(
                nodeId,
                null,
                new LocalizedText($"dialogue.ux5a.leaf-{index + 1}", $"Branche {index + 1:00}"),
                role: DialogueNodeRole.End),
        }).ToArray();
        var dialogue = new DialogueDefinition(
            DialogueId.From(dialogueGuid),
            dialogueSlug,
            "Branche témoin UX5-A",
            entryId,
            [entry, .. leaves],
            schemaVersion: DialogueDefinition.CurrentSchemaVersion,
            provenance: Ux5AEvidenceProvenance,
            metadata: new Dictionary<string, string>
            {
                ["kind"] = "conversation",
                ["evidence"] = "ux5-a",
            },
            participants: [speakingParticipant, narrator],
            entryPoints:
            [
                new DialogueEntryPoint(
                    StableUx5AId(dialogueGuid, "entry:default"),
                    DialogueEntryPoint.CurrentSchemaVersion,
                    "default",
                    entryId,
                    0,
                    Ux5AEvidenceProvenance),
                new DialogueEntryPoint(
                    StableUx5AId(dialogueGuid, "entry:alternate"),
                    DialogueEntryPoint.CurrentSchemaVersion,
                    "branche-secondaire",
                    leafIds[1],
                    1,
                    Ux5AEvidenceProvenance),
            ]);
        return new ProjectDefinition(
            project.Id,
            project.Title,
            project.Module,
            [.. project.Blueprints, speaker],
            project.Areas,
            [.. project.Dialogues, dialogue],
            project.Quests);
    }

    private static ProjectDefinition ReplaceDialogueUx5A(
        ProjectDefinition project,
        DialogueDefinition dialogue)
    {
        var exists = project.Dialogues.Any(item => item.Id == dialogue.Id);
        var dialogues = exists
            ? project.Dialogues.Select(item => item.Id == dialogue.Id ? dialogue : item).ToArray()
            : [.. project.Dialogues, dialogue];
        return new ProjectDefinition(
            project.Id,
            project.Title,
            project.Module,
            project.Blueprints,
            project.Areas,
            dialogues,
            project.Quests);
    }

    private static DialogueDefinition CopyDialogueUx5A(
        DialogueDefinition source,
        string? displayName = null,
        string? slug = null,
        DialogueNodeId? entryNodeId = null) => new(
        source.Id,
        slug ?? source.Slug,
        displayName ?? source.DisplayName,
        entryNodeId ?? source.EntryNodeId,
        source.Nodes,
        source.Presentation,
        source.SchemaVersion,
        source.Provenance,
        source.Metadata,
        source.Participants,
        source.EntryPoints,
        source.Edges,
        source.Authoring,
        source.Experience,
        source.Production);

    private static LocalizedText CopyLocalizedTextUx5A(LocalizedText source, string fallback) => new(
        source.Id,
        source.SchemaVersion,
        source.Provenance,
        source.Key,
        fallback,
        source.Variants);

    private static string ProjectFingerprintUx5A(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))))
            .ToLowerInvariant();

    private static StudioDialogueDiagnosticUx5A HumanizeDialogueDiagnosticUx5A(CommandDiagnostic diagnostic)
    {
        var section = diagnostic.Code switch
        {
            DialogueValidator.MissingEntryNodeCode or DialogueValidator.EntryPointInvalidCode => "Entrées",
            DialogueValidator.MissingTargetNodeCode => "Liens",
            DialogueValidator.UnreachableNodeCode => "Accessibilité",
            DialogueValidator.NodeRoleInvalidCode or DialogueValidator.ParticipantInvalidCode => "Intervenants",
            DialogueValidator.EdgeInvalidCode or DialogueValidator.AccidentalCycleCode => "Réponses",
            DialogueValidator.MissingTerminalNodeCode => "Fins",
            DialogueValidator.LocalizationInvalidCode => "Textes",
            DialogueValidator.ConditionInvalidCode or DialogueValidator.ContradictoryConditionCode => "Conditions préservées",
            DialogueValidator.ActionInvalidCode => "Actions préservées",
            ProjectValidator.MissingDialogueSpeakerBlueprintCode => "Intervenants",
            SetDialogueHandler.RevisionConflictCode => "Révision",
            _ => "Dialogue",
        };
        var message = diagnostic.Code switch
        {
            DialogueValidator.MissingEntryNodeCode => "L’entrée principale doit désigner une ligne existante.",
            DialogueValidator.EntryPointInvalidCode => "Les entrées doivent rester uniques, ordonnées et résolues.",
            DialogueValidator.MissingTargetNodeCode => "Une réponse vise une ligne qui n’existe plus.",
            DialogueValidator.UnreachableNodeCode => "Une ligne n’est accessible depuis aucune entrée du graphe.",
            DialogueValidator.NodeRoleInvalidCode => "Un rôle, un intervenant ou un sous-dialogue est incohérent.",
            DialogueValidator.ParticipantInvalidCode => "Les intervenants doivent conserver des rôles uniques et valides.",
            DialogueValidator.EdgeInvalidCode => "Une réponse requiert une cible compatible et une priorité entière positive ou nulle.",
            DialogueValidator.AccidentalCycleCode =>
                "Un cycle Flow est accidentel. Marquez un retour intentionnel comme Reference.",
            DialogueValidator.MissingTerminalNodeCode => "Au moins une fin accessible est requise.",
            DialogueValidator.LocalizationInvalidCode => "Un texte ou une variante localisée est incomplet.",
            DialogueValidator.ConditionInvalidCode => "Une condition préservée est invalide.",
            DialogueValidator.ContradictoryConditionCode => "Des conditions préservées se contredisent.",
            DialogueValidator.ActionInvalidCode => "Une action préservée est invalide.",
            DialogueValidator.DuplicateSemanticIdCode => "Deux éléments du dialogue partagent la même identité interne.",
            ProjectValidator.MissingDialogueSpeakerBlueprintCode => "Un intervenant sélectionné n’existe plus dans le projet.",
            SetDialogueHandler.RevisionConflictCode => "Le projet a changé. Rechargez puis recommencez le dry-run.",
            _ => "Le validateur canonique a refusé ce brouillon dans cette section.",
        };
        return new StudioDialogueDiagnosticUx5A(diagnostic.Code, section, message);
    }

    private static string BuildDialogueDiffUx5A(
        DialogueDefinition source,
        DialogueDefinition candidate,
        bool isNew)
    {
        var sourceNodes = source.Nodes.ToDictionary(item => item.Id);
        var candidateNodes = candidate.Nodes.ToDictionary(item => item.Id);
        var added = candidateNodes.Keys.Count(id => !sourceNodes.ContainsKey(id));
        var removed = sourceNodes.Keys.Count(id => !candidateNodes.ContainsKey(id));
        var modified = candidate.Nodes.Count(node =>
            sourceNodes.TryGetValue(node.Id, out var previous) &&
            !DialogueNodeStructurallyEqualUx5A(previous, node));
        var lines = new List<string>
        {
            $"{candidate.Nodes.Count} ligne(s) · {candidate.Nodes.Sum(item => item.Choices.Count)} réponse(s) · " +
            $"{candidate.Edges.Count(edge => edge.Kind == DialogueEdgeKind.Reference)} référence(s)",
        };
        if (isNew)
        {
            lines.Add(
                $"Dialogue neuf : « {HumanSnippetUx5A(candidate.DisplayName)} » · " +
                $"slug « {HumanSnippetUx5A(candidate.Slug)} » · " +
                $"entrée {HumanEntryUx5A(candidate, candidate.EntryNodeId)}");
        }
        else
        {
            var identity = new List<string>();
            if (!string.Equals(source.DisplayName, candidate.DisplayName, StringComparison.Ordinal))
            {
                identity.Add(
                    $"nom « {HumanSnippetUx5A(source.DisplayName)} » → « {HumanSnippetUx5A(candidate.DisplayName)} »");
            }
            if (!string.Equals(source.Slug, candidate.Slug, StringComparison.Ordinal))
            {
                identity.Add($"slug « {HumanSnippetUx5A(source.Slug)} » → « {HumanSnippetUx5A(candidate.Slug)} »");
            }
            if (source.EntryNodeId != candidate.EntryNodeId)
            {
                identity.Add(
                    $"entrée {HumanEntryUx5A(source, source.EntryNodeId)} → " +
                    HumanEntryUx5A(candidate, candidate.EntryNodeId));
            }
            if (identity.Count > 0)
            {
                lines.Add("Identité : " + string.Join(" · ", identity));
            }
        }
        lines.Add($"Diff : {added} ajout(s) · {removed} retrait(s) · {modified} ligne(s) modifiée(s)");
        foreach (var node in candidate.Nodes)
        {
            if (!sourceNodes.TryGetValue(node.Id, out var previous))
            {
                continue;
            }
            var previousIds = previous.Choices.Select(item => item.Id).ToArray();
            var currentIds = node.Choices.Select(item => item.Id).ToArray();
            if (previousIds.Length == currentIds.Length && previousIds.ToHashSet().SetEquals(currentIds) &&
                !previousIds.SequenceEqual(currentIds))
            {
                lines.Add(
                    $"Réponses réordonnées sur « {HumanSnippetUx5A(node.Text.Fallback)} » : " +
                    string.Join(" → ", node.Choices.Select((choice, index) =>
                        $"{index + 1}. {HumanSnippetUx5A(choice.Text.Fallback)}")));
            }
        }
        lines.Add("Dry-run : projet, historique et fichiers inchangés.");
        return string.Join('\n', lines);
    }

    private static bool DialogueNodeStructurallyEqualUx5A(DialogueNode left, DialogueNode right) =>
        left.Id == right.Id &&
        left.SpeakerBlueprintId == right.SpeakerBlueprintId &&
        left.Role == right.Role &&
        left.ParticipantId == right.ParticipantId &&
        string.Equals(left.SubgraphKey, right.SubgraphKey, StringComparison.Ordinal) &&
        LocalizedTextStructurallyEqualUx5A(left.Text, right.Text) &&
        left.OnEnterActions.SequenceEqual(right.OnEnterActions) &&
        left.Choices.Count == right.Choices.Count &&
        left.Choices.Zip(right.Choices).All(pair =>
            DialogueChoiceStructurallyEqualUx5A(pair.First, pair.Second));

    private static bool DialogueChoiceStructurallyEqualUx5A(DialogueChoice left, DialogueChoice right) =>
        left.Id == right.Id &&
        LocalizedTextStructurallyEqualUx5A(left.Text, right.Text) &&
        left.Conditions.SequenceEqual(right.Conditions) &&
        left.Actions.SequenceEqual(right.Actions) &&
        left.TargetNodeId == right.TargetNodeId &&
        left.EdgeKind == right.EdgeKind &&
        left.Priority == right.Priority &&
        left.ConditionCombination == right.ConditionCombination;

    private static bool LocalizedTextStructurallyEqualUx5A(LocalizedText left, LocalizedText right) =>
        left.Id == right.Id &&
        left.SchemaVersion == right.SchemaVersion &&
        string.Equals(left.Provenance, right.Provenance, StringComparison.Ordinal) &&
        string.Equals(left.Key, right.Key, StringComparison.Ordinal) &&
        string.Equals(left.Fallback, right.Fallback, StringComparison.Ordinal) &&
        left.Variants.SequenceEqual(right.Variants);

    private static string HumanEntryUx5A(DialogueDefinition dialogue, DialogueNodeId entryNodeId)
    {
        var index = dialogue.Nodes.ToList().FindIndex(node => node.Id == entryNodeId);
        return index >= 0 ? $"ligne {index + 1:00}" : "indisponible";
    }

    private static string HumanSnippetUx5A(string value)
    {
        var normalized = string.Join(' ', (value ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length <= 56 ? normalized : normalized[..53] + "…";
    }

    private static string RedactTechnicalIdsUx5A(string value) =>
        Ux5AGuidPattern.Replace(value ?? string.Empty, "référence interne");

    private static StudioDialoguePreviewUx5A FailedDialoguePreviewUx5A(string message) => new(
        false,
        message,
        "Aucun aperçu valide · projet et historique inchangés.",
        false,
        0,
        0,
        0,
        []);

    private static bool PreservesDialogueNeighborsUx5A(
        DialogueDefinition source,
        DialogueDefinition candidate)
    {
        if (source.Id != candidate.Id ||
            !string.Equals(source.Slug, candidate.Slug, StringComparison.Ordinal) ||
            !string.Equals(source.DisplayName, candidate.DisplayName, StringComparison.Ordinal) ||
            source.EntryNodeId != candidate.EntryNodeId ||
            source.Nodes.Count != candidate.Nodes.Count ||
            source.SchemaVersion != candidate.SchemaVersion ||
            !string.Equals(source.Provenance, candidate.Provenance, StringComparison.Ordinal) ||
            !source.Metadata.SequenceEqual(candidate.Metadata) ||
            !Equals(source.Presentation, candidate.Presentation) ||
            !source.Participants.SequenceEqual(candidate.Participants) ||
            !source.EntryPoints.SequenceEqual(candidate.EntryPoints) ||
            !Equals(source.Authoring, candidate.Authoring) ||
            !Equals(source.Experience, candidate.Experience) ||
            !Equals(source.Production, candidate.Production))
        {
            return false;
        }

        var candidates = candidate.Nodes.ToDictionary(item => item.Id);
        foreach (var sourceNode in source.Nodes)
        {
            if (!candidates.TryGetValue(sourceNode.Id, out var current) ||
                sourceNode.SpeakerBlueprintId != current.SpeakerBlueprintId ||
                sourceNode.Role != current.Role ||
                sourceNode.ParticipantId != current.ParticipantId ||
                sourceNode.Choices.Count != current.Choices.Count ||
                !sourceNode.OnEnterActions.SequenceEqual(current.OnEnterActions) ||
                sourceNode.Text.Id != current.Text.Id ||
                sourceNode.Text.SchemaVersion != current.Text.SchemaVersion ||
                !string.Equals(sourceNode.Text.Provenance, current.Text.Provenance, StringComparison.Ordinal) ||
                !string.Equals(sourceNode.Text.Key, current.Text.Key, StringComparison.Ordinal) ||
                !string.Equals(sourceNode.Text.Fallback, current.Text.Fallback, StringComparison.Ordinal) ||
                !sourceNode.Text.Variants.SequenceEqual(current.Text.Variants) ||
                !string.Equals(sourceNode.SubgraphKey, current.SubgraphKey, StringComparison.Ordinal))
            {
                return false;
            }

            var choices = current.Choices.ToDictionary(item => item.Id);
            foreach (var sourceChoice in sourceNode.Choices)
            {
                if (!choices.TryGetValue(sourceChoice.Id, out var choice) ||
                    sourceChoice.TargetNodeId != choice.TargetNodeId ||
                    sourceChoice.EdgeKind != choice.EdgeKind ||
                    sourceChoice.Priority != choice.Priority ||
                    !sourceChoice.Conditions.SequenceEqual(choice.Conditions) ||
                    !sourceChoice.Actions.SequenceEqual(choice.Actions) ||
                    sourceChoice.ConditionCombination != choice.ConditionCombination ||
                    sourceChoice.Text.Id != choice.Text.Id ||
                    sourceChoice.Text.SchemaVersion != choice.Text.SchemaVersion ||
                    !string.Equals(sourceChoice.Text.Provenance, choice.Text.Provenance, StringComparison.Ordinal) ||
                    !string.Equals(sourceChoice.Text.Key, choice.Text.Key, StringComparison.Ordinal) ||
                    !string.Equals(sourceChoice.Text.Fallback, choice.Text.Fallback, StringComparison.Ordinal) ||
                    !sourceChoice.Text.Variants.SequenceEqual(choice.Text.Variants))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static StudioDialogueDraftUx5A ReplaceChoiceDraftUx5A(
        StudioDialogueDraftUx5A draft,
        int lineIndex,
        int choiceIndex,
        StudioDialogueChoiceDraftUx5A replacement)
    {
        var lines = draft.Lines.ToArray();
        var choices = lines[lineIndex].Choices.ToArray();
        choices[choiceIndex] = replacement;
        lines[lineIndex] = lines[lineIndex] with { Choices = choices };
        return draft with { Lines = lines };
    }

    internal static StudioDialogueDraftUx5A MoveChoiceDraftUx5A(
        StudioDialogueDraftUx5A draft,
        int lineIndex,
        int sourceIndex,
        int targetIndex)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (lineIndex < 0 || lineIndex >= draft.Lines.Count)
        {
            return draft;
        }
        var choices = draft.Lines[lineIndex].Choices.ToList();
        if (sourceIndex < 0 || sourceIndex >= choices.Count ||
            targetIndex < 0 || targetIndex >= choices.Count || sourceIndex == targetIndex)
        {
            return draft;
        }
        var moved = choices[sourceIndex];
        choices.RemoveAt(sourceIndex);
        choices.Insert(targetIndex, moved);
        var lines = draft.Lines.ToArray();
        lines[lineIndex] = lines[lineIndex] with { Choices = choices.ToArray() };
        return draft with { Lines = lines };
    }

    private static Guid StableUx5AId(Guid seed, string segment)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"ux5-a:{seed:N}:{segment}"));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guid);
        guid[7] = (byte)((guid[7] & 0x0f) | 0x40);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }

    private static StudioUx5ASmokeResult FailedUx5ASmoke(string message) => new(
        Succeeded: false,
        ExplicitSelection: false,
        NewDialogue: false,
        ProtectedSpeakerPreserved: false,
        SubgraphProtected: false,
        StructuralChoiceDiff: false,
        IdentityDiffHuman: false,
        BranchesUnlimited: false,
        MultipleEntries: false,
        ReferenceCycleAccepted: false,
        FlowCycleBlocked: false,
        MissingTargetBlocked: false,
        NegativePriorityBlocked: false,
        DryRunPure: false,
        StaleRevisionBlocked: false,
        FingerprintGuard: false,
        CanonicalCommand: false,
        EdgesCanonical: false,
        ReorderDiffHuman: false,
        UndoRedo: false,
        RoundTrip: false,
        StructuralPreservation: false,
        SourceUntouched: false,
        ResourcesReleased: false,
        Message: message);
}
