using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Sequences;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux6WProvenance = "studio:ux6-w:runtime-transforms";
    private const string Ux6WEvidenceProvenance = "studio:ux6-w:evidence";
    private const string Ux6WBoundary =
        "Aucun nœud Godot, matériau, état d’acteur, scène, serveur ou transport réseau n’est modifié · runtimes copiés seulement.";

    private readonly SetCinematicProfilesHandler _setRuntimeTransformsUx6W = new(new SystemClock());
    private StudioUx6WDraft? _approvedRuntimeTransformsUx6W;
    private ProjectRevision? _approvedRuntimeTransformsRevisionUx6W;
    private string? _approvedRuntimeTransformsFingerprintUx6W;

    internal IReadOnlyList<StudioUx6WAreaChoice> RuntimeTransformAreaChoicesUx6W()
    {
        var project = Project;
        if (project is null) return [];
        return project.Areas.Where(item => item.Entities.Count > 0)
            .Select(item => new StudioUx6WAreaChoice(item.Id,
                $"{HumanUx6W(item.DisplayName, "Zone")} · {item.Entities.Count} objet(s)"))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal IReadOnlyList<StudioUx6WEntityChoice> RuntimeTransformEntityChoicesUx6W(AreaId? areaId)
    {
        var project = Project;
        var area = areaId is null ? null : project?.Areas.FirstOrDefault(item => item.Id == areaId);
        if (project is null || area is null) return [];
        var blueprints = project.Blueprints.ToDictionary(item => item.Id);
        return area.Entities.Select((entity, index) =>
        {
            var label = blueprints.TryGetValue(entity.BlueprintId, out var blueprint)
                ? HumanUx6W(blueprint.DisplayName, "Objet")
                : "Objet hérité protégé";
            return new StudioUx6WEntityChoice(area.Id, entity.Id, $"{label} · instance {index + 1}");
        }).OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    internal IReadOnlyList<StudioUx6WItemChoice> RuntimeOverrideChoicesUx6W(StudioUx6WDraft? draft)
    {
        if (draft is null) return [];
        return draft.CandidateProfile.TransformOverrides.Select((item, index) => new StudioUx6WItemChoice(item.Id,
            $"Override {index + 1} · {EntityLabelUx6W(item.EntityId)} · {item.DurationMilliseconds} ms"))
            .ToArray();
    }

    internal IReadOnlyList<StudioUx6WEntityChoice> RuntimeTransformAllEntityChoicesUx6W()
    {
        var project = Project;
        if (project is null) return [];
        return project.Areas.SelectMany(area => RuntimeTransformEntityChoicesUx6W(area.Id)
                .Select(item => item with { Label = $"{item.Label} · {HumanUx6W(area.DisplayName, "Zone")}" }))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal IReadOnlyList<StudioUx6WItemChoice> RuntimeTweenChoicesUx6W(StudioUx6WDraft? draft)
    {
        if (draft is null) return [];
        return draft.CandidateProfile.Tweens.Select((item, index) => new StudioUx6WItemChoice(item.Id,
            $"Tween {index + 1} · {HumanChannelUx6W(item.Channel)} · {EntityLabelUx6W(item.EntityId)}"))
            .ToArray();
    }

    internal StudioUx6WDraft? CreateRuntimeTransformDraftUx6W(bool explicitlySelected, AreaId? areaId)
    {
        var project = Project;
        var history = History;
        var area = areaId is null ? null : project?.Areas.FirstOrDefault(item => item.Id == areaId);
        if (!explicitlySelected || project is null || history is null || area is null || area.Entities.Count == 0)
            return null;
        var profile = project.Module.RuntimePolicies.Cinematics;
        return new(history.State.Revision, ProjectFingerprintUx6W(project), area.Id, profile, profile);
    }

    internal StudioUx6WDraft UpsertRuntimeOverrideUx6W(StudioUx6WDraft draft, StudioUx6WOverrideInput input)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(input);
        var id = input.Id ?? Guid.NewGuid();
        var value = new TransformOverrideComponent(id, TransformOverrideComponent.CurrentSchemaVersion,
            input.EntityId, input.ParentEntityId, input.From, input.To, input.DurationMilliseconds, input.Easing,
            input.Authority, input.SaveBehavior, input.Replicate, Ux6WProvenance);
        var values = draft.CandidateProfile.TransformOverrides.Where(item => item.Id != id).Append(value).ToArray();
        return draft with
        {
            CandidateProfile = RebuildRuntimeTransformProfileUx6W(draft.CandidateProfile, values,
            draft.CandidateProfile.Tweens)
        };
    }

    internal StudioUx6WDraft RemoveRuntimeOverrideUx6W(StudioUx6WDraft draft, Guid id)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return draft with
        {
            CandidateProfile = RebuildRuntimeTransformProfileUx6W(draft.CandidateProfile,
                draft.CandidateProfile.TransformOverrides.Where(item => item.Id != id).ToArray(),
                draft.CandidateProfile.Tweens),
        };
    }

    internal StudioUx6WDraft UpsertRuntimeTweenUx6W(StudioUx6WDraft draft, StudioUx6WTweenInput input)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(input);
        var id = input.Id ?? Guid.NewGuid();
        var value = new AnimationTweenDefinition(id, AnimationTweenDefinition.CurrentSchemaVersion,
            input.EntityId, input.Channel, input.Property, input.FromTransform, input.ToTransform,
            input.FromValue, input.ToValue, input.DurationMilliseconds, input.Easing, input.Authority,
            input.SaveBehavior, input.Replicate, Ux6WProvenance);
        var values = draft.CandidateProfile.Tweens.Where(item => item.Id != id).Append(value).ToArray();
        return draft with
        {
            CandidateProfile = RebuildRuntimeTransformProfileUx6W(draft.CandidateProfile,
            draft.CandidateProfile.TransformOverrides, values)
        };
    }

    internal StudioUx6WDraft RemoveRuntimeTweenUx6W(StudioUx6WDraft draft, Guid id)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return draft with
        {
            CandidateProfile = RebuildRuntimeTransformProfileUx6W(draft.CandidateProfile,
                draft.CandidateProfile.TransformOverrides,
                draft.CandidateProfile.Tweens.Where(item => item.Id != id).ToArray()),
        };
    }

    internal StudioUx6WPreview PreviewRuntimeTransformsUx6W(StudioUx6WDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelRuntimeTransformApprovalUx6W();
        if (!TryBuildRuntimeTransformCandidateUx6W(draft, true, out var command, out var expected,
                out var diagnostic)) return FailedRuntimeTransformPreviewUx6W(diagnostic!);
        var history = History!;
        var dryRun = _setRuntimeTransformsUx6W.Handle(history.State, command!);
        if (!dryRun.Succeeded)
        {
            var value = dryRun.Diagnostics.FirstOrDefault();
            return FailedRuntimeTransformPreviewUx6W(new StudioUx6WDiagnostic(
                value?.Code ?? "studio.runtime_transform.command_refused",
                "module/runtimePolicies/cinematics", HumanCommandUx6W(value?.Code, value?.Message)));
        }

        var profile = draft.CandidateProfile;
        var overrideVerified = VerifyOverridesUx6W(profile.TransformOverrides, out var overrideText,
            out var authorityOverride, out var cancelOverride);
        var transformVerified = VerifyTransformTweensUx6W(profile.Tweens, out var transformText,
            out var authorityTween, out var cancelTween);
        var materialVerified = VerifyMaterialTweensUx6W(profile.Tweens, out var materialText);
        var stateVerified = VerifyStateTweensUx6W(profile.Tweens, out var stateText);
        var authorityVerified = authorityOverride && authorityTween;
        var cancellationVerified = cancelOverride && cancelTween;
        var diff = BuildRuntimeTransformDiffUx6W(draft.SourceProfile, profile);
        if (diff.Count > 0)
        {
            _approvedRuntimeTransformsUx6W = draft;
            _approvedRuntimeTransformsRevisionUx6W = history.State.Revision;
            _approvedRuntimeTransformsFingerprintUx6W = ProjectFingerprintUx6W(expected!);
        }
        return new(true, diff.Count > 0, true,
            diff.Count > 0
                ? "Aperçu sec canonique validé · les transforms du projet source restent inchangés."
                : "État vide valide · ajoutez explicitement un override ou un tween pour appliquer.",
            diff, [], [overrideText, transformText, materialText, stateText, Ux6WBoundary],
            overrideVerified, transformVerified, materialVerified, stateVerified, authorityVerified,
            cancellationVerified, true);
    }

    internal StudioUx6WActionResult ApplyApprovedRuntimeTransformsUx6W()
    {
        var draft = _approvedRuntimeTransformsUx6W;
        var history = History;
        var project = Project;
        if (draft is null || history is null || project is null ||
            _approvedRuntimeTransformsRevisionUx6W != history.State.Revision ||
            draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, ProjectFingerprintUx6W(project), StringComparison.Ordinal) ||
            !TryBuildRuntimeTransformCandidateUx6W(draft, false, out var command, out var expected, out _) ||
            !string.Equals(_approvedRuntimeTransformsFingerprintUx6W, ProjectFingerprintUx6W(expected!),
                StringComparison.Ordinal))
        {
            CancelRuntimeTransformApprovalUx6W();
            return new(false, "Le projet ou le brouillon a changé · relancez l’aperçu sec canonique.");
        }
        var dryRun = _setRuntimeTransformsUx6W.Handle(history.State, command! with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            CancelRuntimeTransformApprovalUx6W();
            return new(false, HumanCommandUx6W(dryRun.Diagnostics.FirstOrDefault()?.Code,
                dryRun.Diagnostics.FirstOrDefault()?.Message));
        }
        var result = _setRuntimeTransformsUx6W.Handle(history.State, command!);
        CancelRuntimeTransformApprovalUx6W();
        if (!result.Succeeded)
            return new(false, HumanCommandUx6W(result.Diagnostics.FirstOrDefault()?.Code,
                result.Diagnostics.FirstOrDefault()?.Message));
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, "Transforms runtime appliqués · l’aperçu copié n’a modifié aucun nœud Godot.");
    }

    internal StudioUx6WActionResult UndoRuntimeTransformsUx6W()
    {
        CancelRuntimeTransformApprovalUx6W();
        var result = Undo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx6WActionResult RedoRuntimeTransformsUx6W()
    {
        CancelRuntimeTransformApprovalUx6W();
        var result = Redo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx6WActionResult SaveRuntimeTransformsUx6W()
    {
        var result = Save();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx6WApprovalSnapshot CaptureRuntimeTransformApprovalUx6W() =>
        new(_approvedRuntimeTransformsUx6W, _approvedRuntimeTransformsRevisionUx6W,
            _approvedRuntimeTransformsFingerprintUx6W);

    internal void RestoreRuntimeTransformApprovalUx6W(StudioUx6WApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedRuntimeTransformsUx6W = snapshot.Draft;
        _approvedRuntimeTransformsRevisionUx6W = snapshot.Revision;
        _approvedRuntimeTransformsFingerprintUx6W = snapshot.CandidateFingerprint;
    }

    internal void CancelRuntimeTransformApprovalUx6W()
    {
        _approvedRuntimeTransformsUx6W = null;
        _approvedRuntimeTransformsRevisionUx6W = null;
        _approvedRuntimeTransformsFingerprintUx6W = null;
    }

    internal void CloseUx6W() => CancelRuntimeTransformApprovalUx6W();

    internal StudioUx6WSmokeResult RunUx6WContractSmoke()
    {
        var real = Project;
        if (real is null) return FailedSmokeUx6W();
        var realCinematics = real.Module.RuntimePolicies.Cinematics;
        var realReadable = real.Areas.Any(item => item.Entities.Count > 0) && realCinematics is not null;
        var emptyStateHandled = realCinematics is not null && realCinematics.TransformOverrides.Count == 0 &&
                                realCinematics.Tweens.Count == 0;
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-w-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        try
        {
            var evidence = CreateRuntimeTransformEvidenceProjectUx6W(real);
            if (!ProjectSession.Create(root, evidence).Succeeded || !editing.Open(root).Succeeded)
                return FailedSmokeUx6W();
            var area = editing.Project!.Areas.First(item => item.Entities.Count >= 2);
            var explicitSelection = editing.CreateRuntimeTransformDraftUx6W(false, area.Id) is null &&
                                    editing.CreateRuntimeTransformDraftUx6W(true, null) is null &&
                                    editing.CreateRuntimeTransformDraftUx6W(true, area.Id) is not null;
            var sourceDraft = editing.CreateRuntimeTransformDraftUx6W(true, area.Id)!;
            var invalidOverride = sourceDraft.CandidateProfile.TransformOverrides[0];
            var invalid = new TransformOverrideComponent(invalidOverride.Id, 1, invalidOverride.EntityId,
                invalidOverride.ParentEntityId, invalidOverride.From, invalidOverride.To,
                invalidOverride.DurationMilliseconds, (SequenceEasing)999, invalidOverride.Authority,
                invalidOverride.SaveBehavior, invalidOverride.Replicate, invalidOverride.Provenance);
            var invalidProfile = RebuildRuntimeTransformProfileUx6W(sourceDraft.CandidateProfile, [invalid],
                sourceDraft.CandidateProfile.Tweens);
            var invalidConfiguration = !editing.PreviewRuntimeTransformsUx6W(sourceDraft with
            {
                CandidateProfile = invalidProfile,
            }).Succeeded;

            var changed = ChangedRuntimeTransformDraftUx6W(sourceDraft);
            var history = editing.History!;
            var beforeProject = editing.Project;
            var beforeRevision = history.State.Revision;
            var beforeJournal = history.Journal.ToArray();
            var preview = editing.PreviewRuntimeTransformsUx6W(changed);
            var canonicalDryRun = preview is { Succeeded: true, CanApply: true, CanonicalDryRun: true } &&
                                  ReferenceEquals(beforeProject, editing.Project) &&
                                  history.State.Revision == beforeRevision &&
                                  history.Journal.SequenceEqual(beforeJournal) && !editing.IsDirty;

            var currentArea = editing.Project!.Areas.Single(item => item.Id == area.Id);
            var external = editing._setRuntimeTransformsUx6W.Handle(history.State,
                new SetCinematicProfilesCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                    editing.Project.Module.RuntimePolicies.Cinematics, area.Id,
                    currentArea.RuntimePolicies.Cinematics, false));
            if (!external.Succeeded) return FailedSmokeUx6W();
            history.Record(external);
            var staleSourceBlocked = !editing.ApplyApprovedRuntimeTransformsUx6W().Succeeded &&
                                     editing.Undo().Succeeded;

            var refreshed = editing.CreateRuntimeTransformDraftUx6W(true, area.Id)!;
            var tamper = ChangedRuntimeTransformDraftUx6W(refreshed);
            var tamperPreview = editing.PreviewRuntimeTransformsUx6W(tamper);
            var approval = editing.CaptureRuntimeTransformApprovalUx6W();
            editing.RestoreRuntimeTransformApprovalUx6W(approval with
            {
                Draft = approval.Draft! with { CandidateProfile = approval.Draft.SourceProfile },
            });
            var staleApprovalBlocked = tamperPreview.Succeeded &&
                                       !editing.ApplyApprovedRuntimeTransformsUx6W().Succeeded;

            var freshSource = editing.Project!;
            var freshDraft = ChangedRuntimeTransformDraftUx6W(
                editing.CreateRuntimeTransformDraftUx6W(true, area.Id)!);
            if (!editing.TryBuildRuntimeTransformCandidateUx6W(freshDraft, false, out _, out var expected, out _))
                return FailedSmokeUx6W();
            var freshPreview = editing.PreviewRuntimeTransformsUx6W(freshDraft);
            var applied = editing.ApplyApprovedRuntimeTransformsUx6W();
            var finalProject = editing.Project!;
            var canonicalCommand = freshPreview.Succeeded && applied.Succeeded &&
                                   history.Journal.Last().CommandName == "set_cinematic_profiles";
            var sourceCinematics = freshSource.Module.RuntimePolicies.Cinematics;
            var finalCinematics = finalProject.Module.RuntimePolicies.Cinematics;
            var neighbors = ReferenceEquals(sourceCinematics.Startup, finalCinematics.Startup) &&
                            sourceCinematics.Sequences.Count == finalCinematics.Sequences.Count &&
                            sourceCinematics.Sequences.Zip(finalCinematics.Sequences)
                                .All(pair => ReferenceEquals(pair.First, pair.Second)) &&
                            ReferenceEquals(freshSource.Areas.Single(item => item.Id == area.Id)
                                .RuntimePolicies.Cinematics,
                                finalProject.Areas.Single(item => item.Id == area.Id).RuntimePolicies.Cinematics);
            var projectNeighbors = expected is not null && string.Equals(ProjectJsonSerializer.Serialize(expected),
                ProjectJsonSerializer.Serialize(finalProject), StringComparison.Ordinal);
            var finalJson = ProjectJsonSerializer.Serialize(finalProject);
            var undo = editing.Undo();
            var undone = undo.Succeeded && ReferenceEquals(freshSource, editing.Project);
            var redo = editing.Redo();
            var redone = redo.Succeeded && ReferenceEquals(finalProject, editing.Project);
            var persistence = editing.Save().Succeeded && reopened.Open(root).Succeeded &&
                              reopened.Project is not null && string.Equals(finalJson,
                                  ProjectJsonSerializer.Serialize(reopened.Project), StringComparison.Ordinal);
            var humanText = !ContainsUuidUx6W(string.Join(' ', editing.RuntimeTransformAreaChoicesUx6W()
                                .Select(item => item.Label)) + " " +
                            string.Join(' ', editing.RuntimeTransformEntityChoicesUx6W(area.Id)
                                .Select(item => item.Label)) + " " + string.Join(' ', freshPreview.Results));
            var bounded = freshPreview.RuntimeClaimsBounded && freshPreview.Results.Contains(Ux6WBoundary);
            editing.Close();
            reopened.Close();
            cleanup = DeleteTemporaryUx6W(root);
            var succeeded = explicitSelection && realReadable && emptyStateHandled &&
                            invalidConfiguration && canonicalDryRun && freshPreview.OverrideRuntimeVerified &&
                            freshPreview.TransformTweenVerified && freshPreview.MaterialTweenVerified &&
                            freshPreview.StateTweenVerified && freshPreview.AuthorityVerified &&
                            freshPreview.CancellationVerified && staleSourceBlocked && staleApprovalBlocked &&
                            canonicalCommand && undone && redone && persistence && neighbors && projectNeighbors &&
                            humanText && bounded && cleanup;
            return new(succeeded, explicitSelection, realReadable, emptyStateHandled,
                invalidConfiguration, canonicalDryRun, freshPreview.OverrideRuntimeVerified,
                freshPreview.TransformTweenVerified, freshPreview.MaterialTweenVerified,
                freshPreview.StateTweenVerified, freshPreview.AuthorityVerified,
                freshPreview.CancellationVerified, staleSourceBlocked, staleApprovalBlocked, canonicalCommand,
                undone && redone, persistence, neighbors, projectNeighbors, humanText, bounded, cleanup);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_W_CONTRACT_FAILED type={exception.GetType().Name} " +
                              $"message={exception.Message}");
            return FailedSmokeUx6W();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            cleanup = DeleteTemporaryUx6W(root);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_W_CLEANUP_FAILED");
        }
    }

    internal static ProjectDefinition CreateRuntimeTransformEvidenceProjectUx6W(ProjectDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var project = source;
        var area = project.Areas.FirstOrDefault(item => item.Entities.Count >= 2);
        if (area is null)
        {
            var blueprint = project.Blueprints.First();
            var areaId = AreaId.From(StableIdUx6W(project.Id.Value, "area"));
            var first = new EntityInstance(EntityId.From(StableIdUx6W(project.Id.Value, "entity:first")),
                blueprint.Id, TransformValue.Identity);
            var second = new EntityInstance(EntityId.From(StableIdUx6W(project.Id.Value, "entity:second")),
                blueprint.Id, new TransformValue(new Vector3Value(3, 0, 0), QuaternionValue.Identity,
                    Vector3Value.One));
            area = new AreaDefinition(areaId, "runtime-transform-lab", "Laboratoire de transforms", [first, second]);
            project = new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
                project.Areas.Append(area).ToArray(), project.Dialogues, project.Quests);
        }
        var target = area.Entities[0];
        var parent = area.Entities[1];
        var from = target.Transform;
        var to = OffsetUx6W(from, 2, 1, -1, 1.15f);
        var transformOverride = new TransformOverrideComponent(StableIdUx6W(project.Id.Value, "override"), 1,
            target.Id, parent.Id, from, to, 1_200, SequenceEasing.SmoothStep, TransformAuthority.Server,
            TransformSaveBehavior.CommitFinal, true, Ux6WEvidenceProvenance);
        var transformTween = new AnimationTweenDefinition(StableIdUx6W(project.Id.Value, "tween:transform"), 1,
            target.Id, AnimationChannelKind.Transform, string.Empty, from, OffsetUx6W(from, 4, 0, 0, 1),
            AnimationTweenValue.Number(0), AnimationTweenValue.Number(0), 900, SequenceEasing.EaseInOut,
            TransformAuthority.Server, TransformSaveBehavior.Resume, true, Ux6WEvidenceProvenance);
        var materialTween = new AnimationTweenDefinition(StableIdUx6W(project.Id.Value, "tween:material"), 1,
            target.Id, AnimationChannelKind.MaterialParameter, "emission", null, null,
            AnimationTweenValue.Color(0, 0, 0, 1), AnimationTweenValue.Color(1, .5f, .2f, 1), 700,
            SequenceEasing.Linear, TransformAuthority.Owner, TransformSaveBehavior.Transient, false,
            Ux6WEvidenceProvenance);
        var stateTween = new AnimationTweenDefinition(StableIdUx6W(project.Id.Value, "tween:state"), 1,
            target.Id, AnimationChannelKind.State, "stance", null, null,
            AnimationTweenValue.State("idle"), AnimationTweenValue.State("alert"), 500,
            SequenceEasing.EaseOut, TransformAuthority.Owner, TransformSaveBehavior.CommitFinal, false,
            Ux6WEvidenceProvenance);
        var sourceProfile = project.Module.RuntimePolicies.Cinematics;
        var profile = RebuildRuntimeTransformProfileUx6W(sourceProfile, [transformOverride],
            [transformTween, materialTween, stateTween], Ux6WEvidenceProvenance);
        return ReplaceRuntimeTransformsUx6W(project, area.Id, profile);
    }

    internal static StudioUx6WDraft ChangedRuntimeTransformDraftUx6W(StudioUx6WDraft draft)
    {
        var value = draft.CandidateProfile.TransformOverrides.First();
        var changed = new TransformOverrideComponent(value.Id, value.SchemaVersion, value.EntityId,
            value.ParentEntityId, value.From, OffsetUx6W(value.To, .5f, .25f, 0, 1.05f),
            value.DurationMilliseconds + 125, SequenceEasing.EaseInOut, value.Authority, value.SaveBehavior,
            value.Replicate, Ux6WProvenance);
        var material = draft.CandidateProfile.Tweens.First(item => item.Channel == AnimationChannelKind.MaterialParameter);
        var changedMaterial = new AnimationTweenDefinition(material.Id, material.SchemaVersion, material.EntityId,
            material.Channel, material.Property, null, null, material.FromValue,
            AnimationTweenValue.Color(.8f, .4f, .15f, 1), material.DurationMilliseconds + 75,
            material.Easing, material.Authority, material.SaveBehavior, material.Replicate, Ux6WProvenance);
        var tweens = draft.CandidateProfile.Tweens.Select(item => item.Id == material.Id ? changedMaterial : item)
            .ToArray();
        return draft with
        {
            CandidateProfile = RebuildRuntimeTransformProfileUx6W(draft.CandidateProfile, [changed], tweens),
        };
    }

    private bool TryBuildRuntimeTransformCandidateUx6W(StudioUx6WDraft draft, bool dryRun,
        out SetCinematicProfilesCommand? command, out ProjectDefinition? expected,
        out StudioUx6WDiagnostic? diagnostic)
    {
        command = null;
        expected = null;
        diagnostic = null;
        var project = Project;
        var history = History;
        if (project is null || history is null)
        {
            diagnostic = new("studio.runtime_transform.project_required", "project",
                "Ouvrez un module avant de configurer les transforms runtime.");
            return false;
        }
        if (draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, ProjectFingerprintUx6W(project), StringComparison.Ordinal))
        {
            diagnostic = new("studio.runtime_transform.source_changed", "project",
                "Le projet a changé · rechargez l’atelier.");
            return false;
        }
        var area = project.Areas.FirstOrDefault(item => item.Id == draft.CommandAreaId);
        if (area is null)
        {
            diagnostic = new("studio.runtime_transform.area_missing", $"areas/{draft.CommandAreaId}",
                "La zone de contexte n’existe plus.");
            return false;
        }
        diagnostic = ValidateRuntimeTransformProfileUx6W(draft.CandidateProfile);
        if (diagnostic is not null) return false;
        expected = ReplaceRuntimeTransformsUx6W(project, area.Id, draft.CandidateProfile);
        var domainDiagnostic = ProjectValidator.Validate(expected).FirstOrDefault();
        if (domainDiagnostic is not null)
        {
            diagnostic = new(domainDiagnostic.Code, domainDiagnostic.Path ?? "module/runtimePolicies/cinematics",
                HumanDomainUx6W(domainDiagnostic.Code, domainDiagnostic.Message));
            return false;
        }
        command = new SetCinematicProfilesCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            draft.CandidateProfile, area.Id, area.RuntimePolicies.Cinematics, dryRun);
        return true;
    }

    private static StudioUx6WDiagnostic? ValidateRuntimeTransformProfileUx6W(ModuleCinematicProfile profile)
    {
        foreach (var item in profile.TransformOverrides)
        {
            var path = $"module/runtimePolicies/cinematics/transformOverrides/{item.Id:D}";
            if (!Enum.IsDefined(item.Easing) || !Enum.IsDefined(item.Authority) ||
                !Enum.IsDefined(item.SaveBehavior))
                return new("studio.runtime_transform.enum_invalid", path,
                    "L’interpolation, l’autorité ou la sauvegarde n’est pas reconnue.");
            if (item.Replicate && item.Authority == TransformAuthority.Local)
                return new("studio.runtime_transform.local_replication", path,
                    "Un override local ne peut pas être répliqué.");
        }
        foreach (var item in profile.Tweens)
        {
            var path = $"module/runtimePolicies/cinematics/tweens/{item.Id:D}";
            if (!Enum.IsDefined(item.Channel) || !Enum.IsDefined(item.Easing) ||
                !Enum.IsDefined(item.Authority) || !Enum.IsDefined(item.SaveBehavior))
                return new("studio.runtime_transform.enum_invalid", path,
                    "Le canal, l’interpolation, l’autorité ou la sauvegarde n’est pas reconnu.");
            if (item.Replicate && item.Authority == TransformAuthority.Local)
                return new("studio.runtime_transform.local_replication", path,
                    "Un tween local ne peut pas être répliqué.");
        }
        return null;
    }

    private static ProjectDefinition ReplaceRuntimeTransformsUx6W(ProjectDefinition project, AreaId areaId,
        ModuleCinematicProfile profile)
    {
        var module = project.Module.RuntimePolicies;
        var policies = new ModuleRuntimePolicyProfile(module.Id, module.SchemaVersion, module.Heartbeats,
            module.Rest, module.SequenceAbort, "cinematic-command", profile, module.Gameplay, module.Rules,
            module.Extensions, module.GameplayEvents, module.World, module.LifecycleEvents);
        var area = project.Areas.Single(item => item.Id == areaId);
        var areaPolicies = area.RuntimePolicies;
        var replacedAreaPolicies = new AreaRuntimePolicyProfile(areaPolicies.Id, areaPolicies.SchemaVersion,
            areaPolicies.Heartbeats, areaPolicies.Rest, "cinematic-command", areaPolicies.Cinematics,
            areaPolicies.Rules, areaPolicies.Experience);
        return new ProjectDefinition(project.Id, project.Title, project.Module.WithRuntimePolicies(policies),
            project.Blueprints, project.Areas.Select(item => item.Id == areaId
                ? item.WithRuntimePolicies(replacedAreaPolicies) : item).ToArray(), project.Dialogues, project.Quests);
    }

    private static ModuleCinematicProfile RebuildRuntimeTransformProfileUx6W(ModuleCinematicProfile source,
        IReadOnlyList<TransformOverrideComponent> overrides, IReadOnlyList<AnimationTweenDefinition> tweens,
        string provenance = Ux6WProvenance) => new(source.Id, source.SchemaVersion, source.Startup,
        source.Sequences, provenance, overrides, tweens);

    private static bool VerifyOverridesUx6W(IReadOnlyList<TransformOverrideComponent> values, out string text,
        out bool authority, out bool cancellation)
    {
        authority = true;
        cancellation = true;
        foreach (var item in values)
        {
            var started = TransformOverrideRuntime.Start(item);
            if (item.Authority == TransformAuthority.Server)
                authority &= TransformOverrideRuntime.Advance(item, started,
                    Math.Max(1, item.DurationMilliseconds / 2), false) == started;
            var advanced = TransformOverrideRuntime.Advance(item, started,
                Math.Max(1, item.DurationMilliseconds / 2), true);
            cancellation &= TransformOverrideRuntime.Cancel(advanced).Cancelled;
        }
        text = values.Count == 0
            ? "Overrides · état vide explicite"
            : $"Overrides · {values.Count} · interpolation, hiérarchie, sauvegarde et réplication évaluées";
        return values.Count == 0 || cancellation;
    }

    private static bool VerifyTransformTweensUx6W(IReadOnlyList<AnimationTweenDefinition> values, out string text,
        out bool authority, out bool cancellation)
    {
        var matching = values.Where(item => item.Channel == AnimationChannelKind.Transform).ToArray();
        authority = true;
        cancellation = true;
        foreach (var item in matching)
        {
            var started = AnimationTweenRuntime.Start(item);
            if (item.Authority == TransformAuthority.Server)
                authority &= AnimationTweenRuntime.Advance(item, started,
                    Math.Max(1, item.DurationMilliseconds / 2), false) == started;
            var advanced = AnimationTweenRuntime.Advance(item, started,
                Math.Max(1, item.DurationMilliseconds / 2), true);
            cancellation &= advanced.Transform is not null && AnimationTweenRuntime.Cancel(advanced).Cancelled;
        }
        text = matching.Length == 0 ? "Tween transform · aucun" :
            $"Tween transform · {matching.Length} · position, quaternion et échelle interpolés";
        return matching.Length == 0 || cancellation;
    }

    private static bool VerifyMaterialTweensUx6W(IReadOnlyList<AnimationTweenDefinition> values, out string text)
    {
        var matching = values.Where(item => item.Channel == AnimationChannelKind.MaterialParameter).ToArray();
        var valid = matching.All(item => AnimationTweenRuntime.Advance(item, AnimationTweenRuntime.Start(item),
            Math.Max(1, item.DurationMilliseconds / 2), true).Value != item.FromValue);
        text = matching.Length == 0 ? "Tween matériau · aucun" :
            $"Tween matériau · {matching.Length} · paramètres vectoriels interpolés";
        return valid;
    }

    private static bool VerifyStateTweensUx6W(IReadOnlyList<AnimationTweenDefinition> values, out string text)
    {
        var matching = values.Where(item => item.Channel == AnimationChannelKind.State).ToArray();
        var valid = matching.All(item => AnimationTweenRuntime.Advance(item, AnimationTweenRuntime.Start(item),
            item.DurationMilliseconds, true).Value.Text == item.ToValue.Text);
        text = matching.Length == 0 ? "Tween état · aucun" :
            $"Tween état · {matching.Length} · état final déterministe";
        return valid;
    }

    private static IReadOnlyList<string> BuildRuntimeTransformDiffUx6W(ModuleCinematicProfile source,
        ModuleCinematicProfile candidate)
    {
        var values = new List<string>();
        if (!source.TransformOverrides.SequenceEqual(candidate.TransformOverrides))
            values.Add($"Overrides · {source.TransformOverrides.Count} → {candidate.TransformOverrides.Count}");
        if (!source.Tweens.SequenceEqual(candidate.Tweens))
            values.Add($"Tweens · {source.Tweens.Count} → {candidate.Tweens.Count}");
        return values;
    }

    private string EntityLabelUx6W(EntityId id)
    {
        var project = Project;
        if (project is null) return "Objet protégé";
        foreach (var area in project.Areas)
        {
            var index = area.Entities.ToList().FindIndex(item => item.Id == id);
            if (index < 0) continue;
            var entity = area.Entities[index];
            var blueprint = project.Blueprints.FirstOrDefault(item => item.Id == entity.BlueprintId);
            return $"{HumanUx6W(blueprint?.DisplayName, "Objet")} · {HumanUx6W(area.DisplayName, "Zone")} · instance {index + 1}";
        }
        return "Objet hérité protégé";
    }

    private static TransformValue OffsetUx6W(TransformValue source, float x, float y, float z, float scale) =>
        new(new Vector3Value(source.Position.X + x, source.Position.Y + y, source.Position.Z + z),
            source.Rotation, new Vector3Value(source.Scale.X * scale, source.Scale.Y * scale, source.Scale.Z * scale));

    private static string ProjectFingerprintUx6W(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));

    private static Guid StableIdUx6W(Guid owner, string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{owner:D}|ux6-w|{key}"));
        Span<byte> value = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(value);
        value[6] = (byte)((value[6] & 0x0f) | 0x40);
        value[8] = (byte)((value[8] & 0x3f) | 0x80);
        return new Guid(value);
    }

    private static StudioUx6WPreview FailedRuntimeTransformPreviewUx6W(StudioUx6WDiagnostic diagnostic) =>
        new(false, false, false, diagnostic.Message, [], [diagnostic], [], false, false, false, false, false,
            false, true);

    private static string HumanCommandUx6W(string? code, string? message) => code switch
    {
        "command.revision_conflict" => "Le projet a changé · relancez l’aperçu.",
        "cinematic.area_missing" => "La zone de contexte n’existe plus.",
        _ => HumanUx6W(message, "La commande canonique a refusé les transforms runtime."),
    };

    private static string HumanDomainUx6W(string code, string message) => code switch
    {
        CinematicValidator.MissingTransformEntityCode => "Un override référence un objet absent.",
        CinematicValidator.TransformHierarchyCode => "La hiérarchie des overrides contient un cycle.",
        CinematicValidator.MissingTweenEntityCode => "Un tween référence un objet absent.",
        _ => HumanUx6W(message, "La validation cinématique a refusé le profil."),
    };

    private static string HumanChannelUx6W(AnimationChannelKind value) => value switch
    {
        AnimationChannelKind.Transform => "Transform",
        AnimationChannelKind.MaterialParameter => "Matériau",
        AnimationChannelKind.State => "État",
        _ => "Canal protégé",
    };

    private static string HumanUx6W(string? value, string fallback)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text) || ContainsUuidUx6W(text)) return fallback;
        return text;
    }

    private static bool ContainsUuidUx6W(string value) => Regex.IsMatch(value,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");

    internal static bool DeleteTemporaryUx6W(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) return true;
        try
        {
            var full = Path.GetFullPath(root);
            var temporary = Path.GetFullPath(Path.GetTempPath());
            if (!full.StartsWith(temporary, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux6-w-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, true);
            return !Directory.Exists(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static StudioUx6WSmokeResult FailedSmokeUx6W() => new(false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false,
        false);
}
