using System.Security.Cryptography;
using System.Text;
using Forge.Application.Combat;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux6TProvenance = "studio:ux6-t:aoe-nui";
    private readonly SetRuntimeExtensionsHandler _setRuntimeExtensionsUx6T = new(new SystemClock());
    private StudioUx6TDraft? _approvedModernToolingUx6T;
    private ProjectRevision? _approvedModernToolingRevisionUx6T;
    private string? _approvedModernToolingFingerprintUx6T;

    internal IReadOnlyList<StudioUx6TProfileChoice> ModernToolingChoicesUx6T()
    {
        var project = Project;
        if (project is null) return [];
        var extensions = project.Module.RuntimePolicies.Extensions;
        var modern = extensions.ModernTooling;
        return [new($"{HumanUx6T(project.Module.DisplayName)} · " +
                    $"{modern.AreaEffectIndicator.Shapes.Count} forme(s) AOE · " +
                    $"NUI {modern.UiToolkit.ToolkitVersion} · " +
                    $"{extensions.UiDocuments.Count} document(s) UI")];
    }

    internal StudioUx6TDraft? CreateModernToolingDraftUx6T(bool explicitlySelected)
    {
        var project = Project;
        var history = History;
        if (!explicitlySelected || project is null || history is null) return null;
        var modern = project.Module.RuntimePolicies.Extensions.ModernTooling;
        var aoe = modern.AreaEffectIndicator;
        var nui = modern.UiToolkit;
        return new(
            history.State.Revision,
            SourceFingerprintUx6T(project),
            aoe.Shapes.ToArray(),
            aoe.MaxRange,
            aoe.MaxHeightDelta,
            aoe.EvaluateCover,
            aoe.RequireServerConfirmation,
            aoe.ColorMode,
            aoe.ValidColor,
            aoe.InvalidColor,
            nui.ToolkitVersion,
            nui.ResponsiveLayout,
            nui.Forms,
            nui.Lists,
            nui.Canvas,
            nui.Accessibility,
            nui.Localization,
            nui.DeveloperInspector,
            aoe.Shapes[0],
            Math.Min(12f, aoe.MaxRange),
            Math.Min(6f, aoe.MaxRange),
            Math.Min(4f, aoe.MaxRange),
            90f,
            true);
    }

    internal StudioUx6TPreview PreviewModernToolingUx6T(StudioUx6TDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelModernToolingApprovalUx6T();
        var project = Project;
        var history = History;
        if (project is null || history is null)
            return FailedPreviewUx6T("Ouvrez un module et sélectionnez son profil runtime moderne.",
                "studio.modern.selection_required", "module/runtimePolicies/extensions/modernTooling");
        if (draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, SourceFingerprintUx6T(project), StringComparison.Ordinal))
            return FailedPreviewUx6T("Le projet a changé · rechargez le profil runtime moderne.",
                "studio.modern.source_changed", "module/runtimePolicies/extensions/modernTooling");
        if (!TryBuildModernToolingCandidateUx6T(project, draft, out var candidate, out var diagnostic))
            return FailedPreviewUx6T(diagnostic!.Message, diagnostic.Code, diagnostic.Path);

        var command = new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            candidate!, DryRun: true);
        var dryRun = _setRuntimeExtensionsUx6T.Handle(history.State, command);
        if (!dryRun.Succeeded)
            return FailedPreviewUx6T(HumanCommandFailureUx6T(dryRun),
                dryRun.Diagnostics.FirstOrDefault()?.Code ?? "studio.modern.command_refused",
                "module/runtimePolicies/extensions/modernTooling");

        var modern = candidate!.ModernTooling;
        var aoeResult = BuildAreaEffectPreviewUx6T(modern.AreaEffectIndicator, draft);
        var areaEffectResolved = aoeResult.IsValid &&
                                 aoeResult.Code == AreaEffectPreviewService.SuccessCode &&
                                 draft.Shapes.All(shape => BuildAreaEffectPreviewUx6T(
                                     modern.AreaEffectIndicator, draft with { PreviewShape = shape }).IsValid);
        var nui = modern.UiToolkit;
        var nuiResolved = nui.ToolkitVersion == ScriptableUiToolkitProfile.CurrentToolkitVersion &&
                          nui.ToolkitVersion == draft.ToolkitVersion &&
                          nui.ResponsiveLayout == draft.ResponsiveLayout && nui.Forms == draft.Forms &&
                          nui.Lists == draft.Lists && nui.Canvas == draft.Canvas &&
                          nui.Accessibility == draft.Accessibility && nui.Localization == draft.Localization &&
                          nui.DeveloperInspector == draft.DeveloperInspector;
        var diff = BuildDiffUx6T(project.Module.RuntimePolicies.Extensions.ModernTooling, modern);
        var runtime = BuildRuntimePreviewUx6T(candidate, draft, aoeResult);
        var changed = diff.Count > 0;
        if (changed)
        {
            _approvedModernToolingUx6T = draft;
            _approvedModernToolingRevisionUx6T = history.State.Revision;
            _approvedModernToolingFingerprintUx6T = CandidateFingerprintUx6T(project, draft);
        }

        return new(true, changed, true,
            changed
                ? "Aperçu sec canonique validé · aucune politique runtime n’a encore été appliquée."
                : "Aperçu sec validé · le profil AOE/NUI est déjà identique.",
            diff, [], runtime, areaEffectResolved, nuiResolved, true);
    }

    internal StudioUx6TActionResult ApplyApprovedModernToolingUx6T()
    {
        var draft = _approvedModernToolingUx6T;
        var project = Project;
        var history = History;
        if (draft is null || project is null || history is null ||
            _approvedModernToolingRevisionUx6T != history.State.Revision ||
            draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, SourceFingerprintUx6T(project), StringComparison.Ordinal) ||
            !string.Equals(_approvedModernToolingFingerprintUx6T,
                CandidateFingerprintUx6T(project, draft), StringComparison.Ordinal) ||
            !TryBuildModernToolingCandidateUx6T(project, draft, out var candidate, out _))
        {
            CancelModernToolingApprovalUx6T();
            return new(false, "Le projet ou le brouillon a changé · relancez l’aperçu sec.");
        }

        var command = new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            candidate!, DryRun: true);
        var dryRun = _setRuntimeExtensionsUx6T.Handle(history.State, command);
        if (!dryRun.Succeeded)
        {
            CancelModernToolingApprovalUx6T();
            return new(false, HumanCommandFailureUx6T(dryRun));
        }
        var applied = _setRuntimeExtensionsUx6T.Handle(history.State, command with { DryRun = false });
        CancelModernToolingApprovalUx6T();
        if (!applied.Succeeded) return new(false, HumanCommandFailureUx6T(applied));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Profil AOE/NUI appliqué par une commande canonique annulable.");
    }

    internal StudioUx6TActionResult UndoModernToolingUx6T()
    {
        CancelModernToolingApprovalUx6T();
        var result = Undo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx6TActionResult RedoModernToolingUx6T()
    {
        CancelModernToolingApprovalUx6T();
        var result = Redo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx6TActionResult SaveModernToolingUx6T()
    {
        var result = Save();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx6TApprovalSnapshot CaptureModernToolingApprovalUx6T() =>
        new(_approvedModernToolingUx6T, _approvedModernToolingRevisionUx6T,
            _approvedModernToolingFingerprintUx6T);

    internal void RestoreModernToolingApprovalUx6T(StudioUx6TApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedModernToolingUx6T = snapshot.Draft;
        _approvedModernToolingRevisionUx6T = snapshot.Revision;
        _approvedModernToolingFingerprintUx6T = snapshot.Fingerprint;
    }

    internal void CancelModernToolingApprovalUx6T()
    {
        _approvedModernToolingUx6T = null;
        _approvedModernToolingRevisionUx6T = null;
        _approvedModernToolingFingerprintUx6T = null;
    }

    internal void CloseUx6T() => CancelModernToolingApprovalUx6T();

    internal static StudioUx6TDraft ChangedModernToolingDraftUx6T(StudioUx6TDraft source)
    {
        var range = source.MaxRange >= 249f ? source.MaxRange - 1f : source.MaxRange + 1f;
        var height = source.MaxHeightDelta >= 99f ? source.MaxHeightDelta - 1f : source.MaxHeightDelta + 1f;
        var valid = string.Equals(source.ValidColor, "3fb8ffff", StringComparison.OrdinalIgnoreCase)
            ? "4dd6a8a0" : "3fb8ffff";
        var invalid = string.Equals(source.InvalidColor, "ff5c5cff", StringComparison.OrdinalIgnoreCase)
            ? "ff8a5ba0" : "ff5c5cff";
        var color = source.ColorMode == TargetIndicatorColorMode.HighContrast
            ? TargetIndicatorColorMode.Deuteranopia
            : TargetIndicatorColorMode.HighContrast;
        return source with
        {
            MaxRange = range,
            MaxHeightDelta = height,
            EvaluateCover = true,
            RequireServerConfirmation = true,
            ColorMode = color,
            ValidColor = valid,
            InvalidColor = invalid,
            DeveloperInspector = !source.DeveloperInspector,
            PreviewShape = source.Shapes.Contains(AreaEffectShape.Cone)
                ? AreaEffectShape.Cone : source.Shapes[0],
            PreviewRange = Math.Min(12f, range),
            PreviewRadius = Math.Min(6f, range),
            PreviewWidth = Math.Min(4f, range),
            PreviewAngleDegrees = 90f,
            PreviewServerConfirmed = true,
        };
    }

    internal static ProjectDefinition CreateEvidenceProjectUx6T(ProjectDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source;
    }

    internal StudioUx6TSmokeResult RunUx6TContractSmoke()
    {
        var real = Project;
        if (real is null) return FailedSmokeUx6T();
        var realExtensions = real.Module.RuntimePolicies.Extensions;
        var realModern = realExtensions.ModernTooling;
        var realReadable = realModern.Id != Guid.Empty && realModern.AreaEffectIndicator.Id != Guid.Empty &&
                           realModern.UiToolkit.Id != Guid.Empty &&
                           realModern.UiToolkit.ToolkitVersion == ScriptableUiToolkitProfile.CurrentToolkitVersion;
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-t-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        try
        {
            var created = ProjectSession.Create(root, CreateEvidenceProjectUx6T(real));
            if (!created.Succeeded || !editing.Open(root).Succeeded) return FailedSmokeUx6T();
            var explicitSelection = editing.CreateModernToolingDraftUx6T(false) is null &&
                                    editing.CreateModernToolingDraftUx6T(true) is not null;
            var baseline = editing.CreateModernToolingDraftUx6T(true)!;
            var shapeValidation = !editing.PreviewModernToolingUx6T(baseline with { Shapes = [] }).Succeeded;
            var rangeValidation = !editing.PreviewModernToolingUx6T(baseline with { MaxRange = 0.25f }).Succeeded;
            var colorValidation = !editing.PreviewModernToolingUx6T(baseline with
            {
                ValidColor = "not-a-color",
            }).Succeeded;
            var toolkitValidation = !editing.PreviewModernToolingUx6T(baseline with
            {
                ToolkitVersion = "3.0",
            }).Succeeded;

            var history = editing.History!;
            var beforeProject = editing.Project;
            var beforeRevision = history.State.Revision;
            var beforeJournal = history.Journal.ToArray();
            var changed = ChangedModernToolingDraftUx6T(baseline);
            var preview = editing.PreviewModernToolingUx6T(changed);
            var canonicalDryRun = preview is { Succeeded: true, CanApply: true, CanonicalDryRun: true } &&
                                  ReferenceEquals(beforeProject, editing.Project) &&
                                  history.State.Revision == beforeRevision &&
                                  history.Journal.SequenceEqual(beforeJournal) && !editing.IsDirty;

            var externalSource = editing.Project!;
            var externalExtensions = externalSource.Module.RuntimePolicies.Extensions;
            var externalCandidate = new ModuleRuntimeExtensionProfile(
                externalExtensions.Id, externalExtensions.SchemaVersion, externalExtensions.RuleSchemas,
                externalExtensions.UiDocuments, externalExtensions.JsonDocuments, externalExtensions.SnapshotPolicy,
                Ux6TProvenance + ":external", externalExtensions.ModernTooling,
                externalExtensions.OperationalReadiness, externalExtensions.PostLaunch);
            var external = editing._setRuntimeExtensionsUx6T.Handle(history.State,
                new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                    externalCandidate, DryRun: false));
            if (!external.Succeeded) return FailedSmokeUx6T();
            history.Record(external);
            var staleSource = editing.ApplyApprovedModernToolingUx6T();
            var staleSourceBlocked = !staleSource.Succeeded && editing.Undo().Succeeded;

            var tamperDraft = ChangedModernToolingDraftUx6T(editing.CreateModernToolingDraftUx6T(true)!);
            var tamperPreview = editing.PreviewModernToolingUx6T(tamperDraft);
            var approval = editing.CaptureModernToolingApprovalUx6T();
            editing.RestoreModernToolingApprovalUx6T(approval with
            {
                Draft = approval.Draft! with { PreviewAngleDegrees = approval.Draft.PreviewAngleDegrees + 1f },
            });
            var staleApproval = editing.ApplyApprovedModernToolingUx6T();
            var staleApprovalBlocked = tamperPreview.Succeeded && !staleApproval.Succeeded;

            var sourceProject = editing.Project!;
            var sourceExtensions = sourceProject.Module.RuntimePolicies.Extensions;
            var sourceModern = sourceExtensions.ModernTooling;
            var fresh = ChangedModernToolingDraftUx6T(editing.CreateModernToolingDraftUx6T(true)!);
            if (!TryBuildModernToolingCandidateUx6T(sourceProject, fresh, out var expectedExtensions, out _) ||
                expectedExtensions is null)
                return FailedSmokeUx6T();
            var freshPreview = editing.PreviewModernToolingUx6T(fresh);
            var applied = editing.ApplyApprovedModernToolingUx6T();
            var finalProject = editing.Project!;
            var finalExtensions = finalProject.Module.RuntimePolicies.Extensions;
            var finalModern = finalExtensions.ModernTooling;
            var canonicalCommand = freshPreview.Succeeded && applied.Succeeded &&
                                   editing.History!.Journal.Last().CommandName == "set_runtime_extensions";
            var modernNeighbors = ReferenceEquals(sourceModern.RuntimeCopy, finalModern.RuntimeCopy) &&
                                  ReferenceEquals(sourceModern.Compiler, finalModern.Compiler) &&
                                  ReferenceEquals(sourceModern.Documentation, finalModern.Documentation) &&
                                  ReferenceEquals(sourceModern.QualityAssurance, finalModern.QualityAssurance) &&
                                  sourceModern.Id == finalModern.Id;
            var extensionNeighbors = sourceExtensions.RuleSchemas.SequenceEqual(finalExtensions.RuleSchemas) &&
                                     sourceExtensions.UiDocuments.SequenceEqual(finalExtensions.UiDocuments) &&
                                     sourceExtensions.JsonDocuments.SequenceEqual(finalExtensions.JsonDocuments) &&
                                     ReferenceEquals(sourceExtensions.SnapshotPolicy, finalExtensions.SnapshotPolicy) &&
                                     ReferenceEquals(sourceExtensions.OperationalReadiness,
                                         finalExtensions.OperationalReadiness) &&
                                     ReferenceEquals(sourceExtensions.PostLaunch, finalExtensions.PostLaunch) &&
                                     sourceExtensions.Id == finalExtensions.Id;
            var expectedProject = BuildProjectUx6T(sourceProject, expectedExtensions);
            var projectNeighbors = string.Equals(ProjectJsonSerializer.Serialize(expectedProject),
                ProjectJsonSerializer.Serialize(finalProject), StringComparison.Ordinal);
            var finalJson = ProjectJsonSerializer.Serialize(finalProject);
            var undo = editing.Undo();
            var undone = undo.Succeeded && ReferenceEquals(sourceProject, editing.Project);
            var redo = editing.Redo();
            var redone = redo.Succeeded && ReferenceEquals(finalProject, editing.Project);
            var persistence = editing.Save().Succeeded && reopened.Open(root).Succeeded &&
                              reopened.Project is not null && string.Equals(finalJson,
                                  ProjectJsonSerializer.Serialize(reopened.Project), StringComparison.Ordinal);
            var human = !ContainsUuidUx6T(string.Join(' ', editing.ModernToolingChoicesUx6T()
                                .Select(item => item.Label)) + " " + string.Join(' ', freshPreview.Diff) + " " +
                            string.Join(' ', freshPreview.RuntimePreview));
            var bounded = freshPreview.RuntimeClaimsBounded && freshPreview.RuntimePreview.Any(item =>
                item.Contains("Aucun mesh", StringComparison.Ordinal));

            editing.Close();
            reopened.Close();
            cleanup = DeleteTemporaryUx6T(root);
            var succeeded = explicitSelection && realReadable && shapeValidation && rangeValidation &&
                            colorValidation && toolkitValidation && canonicalDryRun &&
                            freshPreview.AreaEffectResolved && freshPreview.NuiResolved && staleSourceBlocked &&
                            staleApprovalBlocked && canonicalCommand && undone && redone && persistence &&
                            modernNeighbors && extensionNeighbors && projectNeighbors && human && bounded && cleanup;
            return new(succeeded, explicitSelection, realReadable, shapeValidation, rangeValidation,
                colorValidation, toolkitValidation, canonicalDryRun, freshPreview.AreaEffectResolved,
                freshPreview.NuiResolved, staleSourceBlocked, staleApprovalBlocked, canonicalCommand,
                undone && redone, persistence, modernNeighbors, extensionNeighbors, projectNeighbors,
                human, bounded, cleanup);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_T_CONTRACT_FAILED " +
                              $"type={exception.GetType().Name} message={exception.Message}");
            return FailedSmokeUx6T();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            cleanup = DeleteTemporaryUx6T(root);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_T_CLEANUP_FAILED");
        }
    }

    private static bool TryBuildModernToolingCandidateUx6T(ProjectDefinition project, StudioUx6TDraft draft,
        out ModuleRuntimeExtensionProfile? candidate, out StudioUx6TDiagnostic? diagnostic)
    {
        candidate = null;
        diagnostic = null;
        var path = "module/runtimePolicies/extensions/modernTooling";
        if (draft.Shapes.Count == 0 || draft.Shapes.Any(shape => !Enum.IsDefined(shape)))
        {
            diagnostic = new("studio.aoe.shapes_invalid", path + "/areaEffectIndicator/shapes",
                "Choisissez au moins une forme AOE reconnue.");
            return false;
        }
        if (!float.IsFinite(draft.MaxRange) || draft.MaxRange is < 0.5f or > 250f ||
            !float.IsFinite(draft.MaxHeightDelta) || draft.MaxHeightDelta is < 0f or > 100f)
        {
            diagnostic = new("studio.aoe.bounds_invalid", path + "/areaEffectIndicator",
                "La portée doit rester entre 0,5 et 250 m et l’écart de hauteur entre 0 et 100 m.");
            return false;
        }
        if (!IsColorUx6T(draft.ValidColor) || !IsColorUx6T(draft.InvalidColor))
        {
            diagnostic = new("studio.aoe.color_invalid", path + "/areaEffectIndicator/colors",
                "Les couleurs doivent être hexadécimales RGB ou RGBA.");
            return false;
        }
        if (!Enum.IsDefined(draft.ColorMode) || !Enum.IsDefined(draft.PreviewShape) ||
            !draft.Shapes.Contains(draft.PreviewShape))
        {
            diagnostic = new("studio.aoe.preview_shape_invalid", path + "/areaEffectIndicator/preview/shape",
                "La forme d’aperçu doit être activée dans le profil.");
            return false;
        }
        if (!float.IsFinite(draft.PreviewRange) || draft.PreviewRange is <= 0 ||
            draft.PreviewRange > draft.MaxRange || !float.IsFinite(draft.PreviewRadius) ||
            draft.PreviewRadius < 0 || draft.PreviewRadius > draft.MaxRange ||
            !float.IsFinite(draft.PreviewWidth) || draft.PreviewWidth < 0 ||
            draft.PreviewWidth > draft.MaxRange * 2f || !float.IsFinite(draft.PreviewAngleDegrees) ||
            draft.PreviewAngleDegrees is <= 0 or > 360)
        {
            diagnostic = new("studio.aoe.preview_bounds_invalid", path + "/areaEffectIndicator/preview",
                "Les dimensions de l’aperçu doivent être finies et rester dans les limites du profil.");
            return false;
        }
        if (!string.Equals(draft.ToolkitVersion, ScriptableUiToolkitProfile.CurrentToolkitVersion,
                StringComparison.Ordinal))
        {
            diagnostic = new("studio.nui.toolkit_version_invalid", path + "/uiToolkit/toolkitVersion",
                "Le toolkit runtime pris en charge est NUI 2.0.");
            return false;
        }

        try
        {
            var extensions = project.Module.RuntimePolicies.Extensions;
            var modern = extensions.ModernTooling;
            var aoe = new AreaEffectIndicatorProfile(modern.AreaEffectIndicator.Id,
                modern.AreaEffectIndicator.SchemaVersion, draft.Shapes, draft.MaxRange, draft.MaxHeightDelta,
                draft.EvaluateCover, draft.RequireServerConfirmation, draft.ColorMode,
                draft.ValidColor, draft.InvalidColor, Ux6TProvenance);
            var nui = new ScriptableUiToolkitProfile(modern.UiToolkit.Id, modern.UiToolkit.SchemaVersion,
                draft.ToolkitVersion, draft.ResponsiveLayout, draft.Forms, draft.Lists, draft.Canvas,
                draft.Accessibility, draft.Localization, draft.DeveloperInspector, Ux6TProvenance);
            var updatedModern = new ModernRuntimeToolingProfile(modern.Id, modern.SchemaVersion,
                modern.RuntimeCopy, aoe, nui, modern.Compiler, modern.Documentation, Ux6TProvenance,
                modern.QualityAssurance);
            candidate = new ModuleRuntimeExtensionProfile(extensions.Id, extensions.SchemaVersion,
                extensions.RuleSchemas, extensions.UiDocuments, extensions.JsonDocuments,
                extensions.SnapshotPolicy, Ux6TProvenance, updatedModern,
                extensions.OperationalReadiness, extensions.PostLaunch);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            var parameter = exception is ArgumentException argument && !string.IsNullOrWhiteSpace(argument.ParamName)
                ? argument.ParamName
                : "contrat";
            diagnostic = new("studio.modern.profile_invalid", path,
                $"Le profil runtime moderne est refusé ({parameter}).");
            return false;
        }
    }

    private static AreaEffectPreviewResult BuildAreaEffectPreviewUx6T(AreaEffectIndicatorProfile profile,
        StudioUx6TDraft draft)
    {
        var candidates = new AreaEffectTargetCandidate[]
        {
            new(EntityId.From(Guid.Parse("10000000-0000-4000-8000-000000000001")),
                new Vector3Value(0, Math.Min(1f, profile.MaxHeightDelta), 4), 0f, true),
            new(EntityId.From(Guid.Parse("10000000-0000-4000-8000-000000000002")),
                new Vector3Value(0.5f, 0, 3), .9f, true),
            new(EntityId.From(Guid.Parse("10000000-0000-4000-8000-000000000003")),
                new Vector3Value(0, profile.MaxHeightDelta + 1f, 2), 0f, true),
            new(EntityId.From(Guid.Parse("10000000-0000-4000-8000-000000000004")),
                new Vector3Value(0, 0, 1), 0f, false),
        };
        return new AreaEffectPreviewService(profile).Preview(new AreaEffectPreviewRequest(
            draft.PreviewShape, Vector3Value.Zero, new Vector3Value(0, 0, 1), draft.PreviewRange,
            draft.PreviewRadius, draft.PreviewWidth, draft.PreviewAngleDegrees,
            draft.PreviewServerConfirmed, candidates));
    }

    private static IReadOnlyList<string> BuildDiffUx6T(ModernRuntimeToolingProfile source,
        ModernRuntimeToolingProfile candidate)
    {
        var diff = new List<string>();
        AddList(source.AreaEffectIndicator.Shapes, candidate.AreaEffectIndicator.Shapes, "Formes AOE");
        Add(source.AreaEffectIndicator.MaxRange, candidate.AreaEffectIndicator.MaxRange, "Portée AOE maximale");
        Add(source.AreaEffectIndicator.MaxHeightDelta, candidate.AreaEffectIndicator.MaxHeightDelta,
            "Écart de hauteur AOE");
        Add(source.AreaEffectIndicator.EvaluateCover, candidate.AreaEffectIndicator.EvaluateCover,
            "Évaluation de couverture");
        Add(source.AreaEffectIndicator.RequireServerConfirmation,
            candidate.AreaEffectIndicator.RequireServerConfirmation, "Confirmation serveur");
        Add(source.AreaEffectIndicator.ColorMode, candidate.AreaEffectIndicator.ColorMode, "Palette accessible");
        Add(source.AreaEffectIndicator.ValidColor, candidate.AreaEffectIndicator.ValidColor, "Couleur valide");
        Add(source.AreaEffectIndicator.InvalidColor, candidate.AreaEffectIndicator.InvalidColor, "Couleur refusée");
        Add(source.UiToolkit.ResponsiveLayout, candidate.UiToolkit.ResponsiveLayout, "Layout responsive");
        Add(source.UiToolkit.Forms, candidate.UiToolkit.Forms, "Formulaires NUI");
        Add(source.UiToolkit.Lists, candidate.UiToolkit.Lists, "Listes NUI");
        Add(source.UiToolkit.Canvas, candidate.UiToolkit.Canvas, "Canvas NUI");
        Add(source.UiToolkit.Accessibility, candidate.UiToolkit.Accessibility, "Accessibilité NUI");
        Add(source.UiToolkit.Localization, candidate.UiToolkit.Localization, "Localisation NUI");
        Add(source.UiToolkit.DeveloperInspector, candidate.UiToolkit.DeveloperInspector,
            "Inspecteur développeur");
        return diff;

        void Add<T>(T before, T after, string label) where T : notnull
        {
            if (!EqualityComparer<T>.Default.Equals(before, after)) diff.Add($"{label} · {before} → {after}");
        }

        void AddList<T>(IReadOnlyList<T> before, IReadOnlyList<T> after, string label)
        {
            if (!before.SequenceEqual(after)) diff.Add($"{label} · {before.Count} → {after.Count}");
        }
    }

    private static IReadOnlyList<string> BuildRuntimePreviewUx6T(ModuleRuntimeExtensionProfile extensions,
        StudioUx6TDraft draft, AreaEffectPreviewResult aoe)
    {
        var nui = extensions.ModernTooling.UiToolkit;
        var enabled = new[]
        {
            (nui.ResponsiveLayout, "responsive"), (nui.Forms, "formulaires"), (nui.Lists, "listes"),
            (nui.Canvas, "canvas"), (nui.Accessibility, "accessibilité"),
            (nui.Localization, "localisation"), (nui.DeveloperInspector, "inspecteur"),
        }.Where(item => item.Item1).Select(item => item.Item2).ToArray();
        var widgets = extensions.UiDocuments.Sum(document => document.Widgets.Count);
        return
        [
            $"Zone d’effet · {HumanShapeUx6T(draft.PreviewShape)} · " +
            $"{aoe.ValidTargets.Count} cible(s) valide(s), {aoe.BlockedTargets.Count} bloquée(s) · {HumanCodeUx6T(aoe.Code)}",
            $"Palette · {HumanColorModeUx6T(extensions.ModernTooling.AreaEffectIndicator.ColorMode)} · " +
            $"valide #{extensions.ModernTooling.AreaEffectIndicator.ValidColor} · refus #{extensions.ModernTooling.AreaEffectIndicator.InvalidColor}",
            $"NUI {nui.ToolkitVersion} · {enabled.Length}/7 capacités déclarées · " +
            (enabled.Length == 0 ? "aucune" : string.Join(", ", enabled)),
            $"Documents runtime inchangés · {extensions.UiDocuments.Count} document(s), {widgets} widget(s)",
            "Aucun mesh Godot, ciblage serveur, document NUI ou événement runtime n’est exécuté dans Studio.",
        ];
    }

    private static StudioUx6TPreview FailedPreviewUx6T(string message, string code, string path) =>
        new(false, false, false, message, [], [new(code, path, message)], [], false, false, true);

    private static string HumanCommandFailureUx6T(CommandResult result)
    {
        var diagnostic = result.Diagnostics.FirstOrDefault();
        return diagnostic is null
            ? "La commande canonique a refusé le profil runtime moderne."
            : $"La commande canonique a refusé le profil ({diagnostic.Code}).";
    }

    private static ProjectDefinition BuildProjectUx6T(ProjectDefinition source,
        ModuleRuntimeExtensionProfile extensions)
    {
        var current = source.Module.RuntimePolicies;
        var policies = new ModuleRuntimePolicyProfile(current.Id, current.SchemaVersion, current.Heartbeats,
            current.Rest, current.SequenceAbort, "runtime-extension-command", current.Cinematics,
            current.Gameplay, current.Rules, extensions, current.GameplayEvents, current.World,
            current.LifecycleEvents);
        return new ProjectDefinition(source.Id, source.Title, source.Module.WithRuntimePolicies(policies),
            source.Blueprints, source.Areas, source.Dialogues, source.Quests);
    }

    private static string SourceFingerprintUx6T(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));

    private static string CandidateFingerprintUx6T(ProjectDefinition project, StudioUx6TDraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            WriteTextUx6T(writer, "ux6-t-draft-v1");
            writer.Write(draft.BaseRevision.Value);
            WriteTextUx6T(writer, draft.SourceFingerprint);
            writer.Write(draft.Shapes.Count);
            foreach (var shape in draft.Shapes) writer.Write((int)shape);
            writer.Write(draft.MaxRange);
            writer.Write(draft.MaxHeightDelta);
            writer.Write(draft.EvaluateCover);
            writer.Write(draft.RequireServerConfirmation);
            writer.Write((int)draft.ColorMode);
            WriteTextUx6T(writer, draft.ValidColor);
            WriteTextUx6T(writer, draft.InvalidColor);
            WriteTextUx6T(writer, draft.ToolkitVersion);
            writer.Write(draft.ResponsiveLayout);
            writer.Write(draft.Forms);
            writer.Write(draft.Lists);
            writer.Write(draft.Canvas);
            writer.Write(draft.Accessibility);
            writer.Write(draft.Localization);
            writer.Write(draft.DeveloperInspector);
            writer.Write((int)draft.PreviewShape);
            writer.Write(draft.PreviewRange);
            writer.Write(draft.PreviewRadius);
            writer.Write(draft.PreviewWidth);
            writer.Write(draft.PreviewAngleDegrees);
            writer.Write(draft.PreviewServerConfirmed);
            WriteTextUx6T(writer, ProjectJsonSerializer.Serialize(project));
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteTextUx6T(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static bool IsColorUx6T(string value)
    {
        var normalized = (value ?? string.Empty).Trim().TrimStart('#');
        return normalized.Length is 6 or 8 && normalized.All(Uri.IsHexDigit);
    }

    private static string HumanShapeUx6T(AreaEffectShape shape) => shape switch
    {
        AreaEffectShape.Circle => "cercle",
        AreaEffectShape.Cone => "cône",
        AreaEffectShape.Rectangle => "rectangle",
        AreaEffectShape.Line => "ligne",
        _ => "forme inconnue",
    };

    private static string HumanColorModeUx6T(TargetIndicatorColorMode mode) => mode switch
    {
        TargetIndicatorColorMode.Standard => "standard",
        TargetIndicatorColorMode.Deuteranopia => "deutéranopie",
        TargetIndicatorColorMode.Tritanopia => "tritanopie",
        TargetIndicatorColorMode.HighContrast => "contraste élevé",
        _ => "palette inconnue",
    };

    private static string HumanCodeUx6T(string code) => code switch
    {
        AreaEffectPreviewService.SuccessCode => "aperçu accepté",
        AreaEffectPreviewService.UnsupportedShapeCode => "forme non activée",
        AreaEffectPreviewService.OutOfRangeCode => "dimensions hors limites",
        AreaEffectPreviewService.ConfirmationRequiredCode => "confirmation serveur requise",
        _ => "décision inconnue",
    };

    private static string HumanUx6T(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "aucun";
        if (Guid.TryParse(value, out _)) return "identité protégée";
        return string.Join(' ', value.Trim().Replace('_', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool ContainsUuidUx6T(string value) =>
        value.Split([' ', '\n', '\r', '\t', ',', ';', '(', ')', '[', ']', '{', '}'],
                StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim('.', ':', '«', '»'))
            .Any(item => Guid.TryParse(item, out _));

    internal static bool DeleteTemporaryUx6T(string root)
    {
        try
        {
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            var leaf = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar));
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !leaf.StartsWith("openrpg-forge-studio-ux6-t-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static StudioUx6TSmokeResult FailedSmokeUx6T() =>
        new(false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false, false);
}
