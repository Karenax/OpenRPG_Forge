using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.RuntimePolicies;
using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux7IProvenance = "studio:ux7-i:observability";
    private const string Ux7IPath =
        "module/runtimePolicies/extensions/postLaunch/creatorExperience/observability";

    private readonly SetRuntimeExtensionsHandler _setRuntimeExtensionsUx7I = new(new SystemClock());
    private readonly WorkspaceObservabilityCaptureService _observabilityCaptureUx7I = new();
    private StudioUx7IDraft? _approvedObservabilityUx7I;
    private ProjectRevision? _approvedObservabilityRevisionUx7I;
    private string? _approvedObservabilityFingerprintUx7I;
    private bool _lastUx7ITemporaryReleased = true;

    internal bool LastUx7ITemporaryReleased => _lastUx7ITemporaryReleased;

    internal IReadOnlyList<StudioUx7IProfileChoice> ObservabilityChoicesUx7I
    {
        get
        {
            var project = Project;
            if (project is null) return [];
            var profile = project.Module.RuntimePolicies.Extensions.PostLaunch.CreatorExperience.Observability;
            return [new($"{HumanTextUx7I(project.Module.DisplayName)} · " +
                (profile.Enabled ? "observabilité activée" : "observabilité désactivée"), profile.Enabled)];
        }
    }

    internal StudioUx7IDraft? CreateObservabilityDraftUx7I(bool explicitlySelected = false)
    {
        var project = Project;
        var history = History;
        if (!explicitlySelected || project is null || history is null) return null;
        var profile = project.Module.RuntimePolicies.Extensions.PostLaunch.CreatorExperience.Observability;
        var backend = profile.OpenTelemetryCompatible
            ? ObservabilityCaptureBackend.OpenTelemetry
            : ObservabilityCaptureBackend.TracyNative;
        return new(history.State.Revision, ObservabilitySourceFingerprintUx7I(project), profile.Enabled,
            profile.TracyCompatible, profile.OpenTelemetryCompatible, profile.MaximumCaptureSeconds,
            profile.IncludeScripts, backend, Math.Min(30, profile.MaximumCaptureSeconds));
    }

    internal StudioUx7IPreview PreviewObservabilityUx7I(StudioUx7IDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelObservabilityApprovalUx7I();
        var project = Project;
        var history = History;
        if (project is null || history is null)
            return FailedPreviewUx7I("OBSERVABILITY_MODULE_MISSING", "module",
                "Ouvrez un module avant de configurer son observabilité.");
        if (draft.BaseRevision != history.State.Revision)
            return FailedPreviewUx7I("OBSERVABILITY_REVISION_CHANGED", Ux7IPath,
                "La révision du projet a changé; rechargez le profil.");
        if (!string.Equals(draft.SourceFingerprint, ObservabilitySourceFingerprintUx7I(project),
                StringComparison.Ordinal))
            return FailedPreviewUx7I("OBSERVABILITY_SOURCE_CHANGED", Ux7IPath,
                "La configuration canonique a changé; rechargez le profil.");
        if (draft.RequestedCaptureSeconds is < 1 || draft.RequestedCaptureSeconds > draft.MaximumCaptureSeconds)
            return FailedPreviewUx7I("OBSERVABILITY_DURATION_OUT_OF_RANGE",
                $"{Ux7IPath}/maximumCaptureSeconds",
                "La durée du run doit rester comprise entre 1 seconde et le maximum canonique.");
        if (!TryBuildObservabilityExtensionsUx7I(project, draft, out var candidate, out var candidateProject,
                out var diagnostic))
            return new(false, false, false, new([]), [diagnostic!], "Configuration refusée",
                "Aucune mutation n’a été produite.");

        var dryRun = _setRuntimeExtensionsUx7I.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, candidate!, DryRun: true));
        if (!dryRun.Succeeded)
        {
            var item = dryRun.Diagnostics.FirstOrDefault();
            return FailedPreviewUx7I(item?.Code ?? "command.validation_failed", Ux7IPath,
                item?.Message ?? "La commande canonique a refusé le profil.");
        }

        var sourceProfile = project.Module.RuntimePolicies.Extensions.PostLaunch.CreatorExperience.Observability;
        var targetProfile = candidateProject!.Module.RuntimePolicies.Extensions.PostLaunch.CreatorExperience.Observability;
        var diff = BuildObservabilityDiffUx7I(sourceProfile, targetProfile);
        var backendStatus = draft.Backend switch
        {
            ObservabilityCaptureBackend.OpenTelemetry when !draft.OpenTelemetryCompatible =>
                "OpenTelemetry est désactivé dans le profil canonique.",
            ObservabilityCaptureBackend.TracyNative when !draft.TracyCompatible =>
                "Tracy natif est désactivé dans le profil canonique.",
            ObservabilityCaptureBackend.TracyNative =>
                "Tracy natif exige un Godot instrumenté et un serveur de capture correspondant; indisponible ici.",
            _ => "Export OpenTelemetry-compatible disponible dans une copie isolée.",
        };
        var runnable = draft.Enabled && draft.Backend == ObservabilityCaptureBackend.OpenTelemetry &&
                       draft.OpenTelemetryCompatible;
        if (diff.HasChanges)
        {
            _approvedObservabilityUx7I = draft;
            _approvedObservabilityRevisionUx7I = history.State.Revision;
            _approvedObservabilityFingerprintUx7I = ObservabilityDraftFingerprintUx7I(draft);
        }
        return new(true, diff.HasChanges, true, diff, [], backendStatus,
            runnable
                ? "Aperçu sec validé · capture OpenTelemetry-compatible exécutable après application dans l’isolat."
                : "Aperçu sec validé · la configuration est persistable, mais le backend sélectionné ne peut pas être exécuté ici.");
    }

    internal StudioUx7IActionResult ApplyApprovedObservabilityUx7I()
    {
        var draft = _approvedObservabilityUx7I;
        var project = Project;
        var history = History;
        if (draft is null || project is null || history is null ||
            _approvedObservabilityRevisionUx7I != history.State.Revision ||
            draft.BaseRevision != history.State.Revision ||
            !string.Equals(_approvedObservabilityFingerprintUx7I,
                ObservabilityDraftFingerprintUx7I(draft), StringComparison.Ordinal) ||
            !string.Equals(draft.SourceFingerprint, ObservabilitySourceFingerprintUx7I(project),
                StringComparison.Ordinal))
        {
            CancelObservabilityApprovalUx7I();
            return new(false, "Relancez l’aperçu sec avant d’appliquer la configuration.");
        }
        if (!TryBuildObservabilityExtensionsUx7I(project, draft, out var candidate, out _, out var diagnostic))
        {
            CancelObservabilityApprovalUx7I();
            return new(false, diagnostic?.Message ?? "Le profil ne peut pas être reconstruit.");
        }
        var command = new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            candidate!, DryRun: true);
        var dryRun = _setRuntimeExtensionsUx7I.Handle(history.State, command);
        if (!dryRun.Succeeded)
        {
            CancelObservabilityApprovalUx7I();
            return new(false, dryRun.Diagnostics.FirstOrDefault()?.Message ??
                "La commande canonique a refusé le profil.");
        }
        var applied = _setRuntimeExtensionsUx7I.Handle(history.State, command with { DryRun = false });
        CancelObservabilityApprovalUx7I();
        if (!applied.Succeeded)
            return new(false, applied.Diagnostics.FirstOrDefault()?.Message ?? "L’application a échoué.");
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Configuration d’observabilité appliquée par une commande canonique annulable.");
    }

    internal void CancelObservabilityApprovalUx7I()
    {
        _approvedObservabilityUx7I = null;
        _approvedObservabilityRevisionUx7I = null;
        _approvedObservabilityFingerprintUx7I = null;
    }

    internal StudioUx7IApprovalSnapshot CaptureObservabilityApprovalUx7I() => new(
        _approvedObservabilityUx7I, _approvedObservabilityRevisionUx7I,
        _approvedObservabilityFingerprintUx7I);

    internal void RestoreObservabilityApprovalUx7I(StudioUx7IApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedObservabilityUx7I = snapshot.Draft;
        _approvedObservabilityRevisionUx7I = snapshot.Revision;
        _approvedObservabilityFingerprintUx7I = snapshot.Fingerprint;
    }

    internal StudioUx7ICaptureResult RunIsolatedObservabilityUx7I(
        StudioUx7IDraft draft,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (Project is null || History is null || Session is null)
            return FailedCaptureUx7I("Aucun module source n’est ouvert.");
        if (!TryBuildObservabilityExtensionsUx7I(Project, draft, out _, out var candidateProject,
                out var diagnostic))
            return FailedCaptureUx7I(diagnostic?.Message ?? "La configuration est invalide.",
                diagnostic is null ? [] : [diagnostic]);

        var sourceProject = Project;
        var sourceHistory = History;
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceFile = FileFingerprintUx7D(Session.ProjectFilePath);
        var sourceRoot = Session.RootDirectory;
        var root = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux7-i-" + Guid.NewGuid().ToString("N"));
        WorkspaceObservabilityCaptureReport? report = null;
        var isolated = false;
        var artifactConfined = false;
        var error = string.Empty;
        _lastUx7ITemporaryReleased = false;
        try
        {
            var created = ProjectSession.Create(root, candidateProject!);
            if (!created.Succeeded || created.Session is null)
                throw new InvalidOperationException("La copie temporaire n’a pas pu être créée.");
            if (!CopyWorkspaceFilesUx7D(sourceRoot, root))
                throw new IOException("Les ressources du module n’ont pas pu être copiées dans l’isolat.");
            isolated = !string.Equals(Path.GetFullPath(root), Path.GetFullPath(sourceRoot),
                StringComparison.OrdinalIgnoreCase);
            report = _observabilityCaptureUx7I.Run(root, created.Session.Project,
                new(draft.Backend, draft.RequestedCaptureSeconds, draft.IncludeScripts, WriteArtifact: true),
                cancellationToken);
            artifactConfined = report.Cancelled || report.ArtifactPath is null ||
                               ConfinedArtifactUx7I(root, report.ArtifactPath);
        }
        catch (OperationCanceledException)
        {
            report = new(false, true, draft.Backend, 0, [], [], string.Empty, null,
                [new("OBSERVABILITY_CAPTURE_CANCELLED", "runtime/observability/capture",
                    "Capture annulée avant production de l’artefact.")]);
            artifactConfined = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException or NotSupportedException)
        {
            error = exception.Message;
        }
        finally
        {
            _lastUx7ITemporaryReleased = DeleteUx7ITemporaryWorkspace(root);
        }

        var sourceUntouched = ReferenceEquals(sourceProject, Project) &&
                              ReferenceEquals(sourceHistory, History) && History?.State.Revision == sourceRevision &&
                              (History?.Journal ?? []).SequenceEqual(sourceJournal) && IsDirty == sourceDirty &&
                              ActiveAreaId == sourceArea && SelectedEntityIds.SequenceEqual(sourceSelection) &&
                              SelectedEntityId == sourceSelected &&
                              FileFingerprintUx7D(Session.ProjectFilePath) == sourceFile;
        if (report is null)
            return new(false, false, isolated, artifactConfined, sourceUntouched, _lastUx7ITemporaryReleased,
                draft.Backend, 0, [], [], string.Empty, string.Empty,
                [new("OBSERVABILITY_CAPTURE_FAILED", "runtime/observability/capture",
                    string.IsNullOrWhiteSpace(error) ? "Capture interrompue." : error)],
                string.IsNullOrWhiteSpace(error) ? "Capture interrompue." : error);
        var diagnostics = report.Diagnostics.Select(item => new StudioUx7IDiagnostic(item.Code, item.Path,
            HumanDiagnosticUx7I(item.Code, item.Message))).ToArray();
        var succeeded = report.Succeeded && isolated && artifactConfined && sourceUntouched &&
                        _lastUx7ITemporaryReleased;
        return new(succeeded, report.Cancelled, isolated, artifactConfined, sourceUntouched,
            _lastUx7ITemporaryReleased, report.Backend, report.SpanCount, report.Surfaces, report.Categories,
            report.Fingerprint, report.ArtifactPath is null ? "aucun artefact" : "trace OpenTelemetry confinée",
            diagnostics, report.Cancelled
                ? "Capture annulée proprement · aucun artefact n’a quitté l’isolat."
                : succeeded
                    ? "Capture OpenTelemetry-compatible terminée dans une copie temporaire."
                    : diagnostics.FirstOrDefault()?.Message ?? "Capture refusée.");
    }

    internal StudioUx7ISmokeResult RunUx7IContractSmoke()
    {
        if (Project is null || History is null || Session is null) return FailedSmokeUx7I();
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceRevision = History.State.Revision;
        var sourceJournal = History.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceFile = FileFingerprintUx7D(Session.ProjectFilePath);
        var root = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux7-i-contract-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        var explicitSelection = false;
        var canonicalCommand = false;
        var invalidBlocked = false;
        var staleBlocked = false;
        var undoRedo = false;
        var roundTrip = false;
        var preservation = false;
        var openTelemetry = false;
        var nativeDiagnostic = false;
        var cancellation = false;
        var resultNavigation = false;
        var isolated = false;
        var artifactConfined = false;
        try
        {
            var created = ProjectSession.Create(root, sourceProject);
            if (!created.Succeeded || !CopyWorkspaceFilesUx7D(Session.RootDirectory, root) ||
                !editing.Open(root).Succeeded)
                return FailedSmokeUx7I();
            isolated = !string.Equals(Path.GetFullPath(root), Path.GetFullPath(Session.RootDirectory),
                StringComparison.OrdinalIgnoreCase);
            var draft = editing.CreateObservabilityDraftUx7I(explicitlySelected: true);
            explicitSelection = draft is not null && editing.ObservabilityChoicesUx7I.Count == 1;
            if (draft is null) return FailedSmokeUx7I();
            draft = draft with
            {
                Enabled = true,
                TracyCompatible = true,
                OpenTelemetryCompatible = true,
                IncludeScripts = true,
                MaximumCaptureSeconds = draft.MaximumCaptureSeconds >= 3600
                    ? 3599
                    : Math.Max(30, draft.MaximumCaptureSeconds) + 1,
                RequestedCaptureSeconds = 30,
                Backend = ObservabilityCaptureBackend.OpenTelemetry,
            };
            invalidBlocked = !editing.PreviewObservabilityUx7I(draft with
            {
                TracyCompatible = false,
                OpenTelemetryCompatible = false,
            }).Succeeded;
            staleBlocked = !editing.PreviewObservabilityUx7I(draft with
            {
                BaseRevision = editing.History!.State.Revision.Next(),
            }).Succeeded;
            var originalExtensions = editing.Project!.Module.RuntimePolicies.Extensions;
            var preview = editing.PreviewObservabilityUx7I(draft);
            var applied = editing.ApplyApprovedObservabilityUx7I();
            canonicalCommand = preview is { Succeeded: true, CanApply: true, CanonicalDryRun: true } &&
                               applied.Succeeded && editing.History!.Journal.Last().CommandName ==
                               "set_runtime_extensions";
            var appliedJson = ProjectJsonSerializer.Serialize(editing.Project!);
            var appliedExtensions = editing.Project!.Module.RuntimePolicies.Extensions;
            preservation = ReferenceEquals(originalExtensions.ModernTooling, appliedExtensions.ModernTooling) &&
                           ReferenceEquals(originalExtensions.OperationalReadiness,
                               appliedExtensions.OperationalReadiness) &&
                           ReferenceEquals(originalExtensions.PostLaunch.Gameplay,
                               appliedExtensions.PostLaunch.Gameplay) &&
                           ReferenceEquals(originalExtensions.PostLaunch.LiveDirector,
                               appliedExtensions.PostLaunch.LiveDirector) &&
                           ReferenceEquals(originalExtensions.PostLaunch.Network,
                               appliedExtensions.PostLaunch.Network) &&
                           ReferenceEquals(originalExtensions.PostLaunch.Production,
                               appliedExtensions.PostLaunch.Production);
            var undo = editing.Undo();
            var redo = editing.Redo();
            undoRedo = undo.Succeeded && redo.Succeeded &&
                       string.Equals(appliedJson, ProjectJsonSerializer.Serialize(editing.Project!),
                           StringComparison.Ordinal);
            var saved = editing.Save();
            roundTrip = saved.Succeeded && reopened.Open(root).Succeeded && reopened.Project is not null &&
                        string.Equals(appliedJson, ProjectJsonSerializer.Serialize(reopened.Project),
                            StringComparison.Ordinal);
            var captureDraft = reopened.CreateObservabilityDraftUx7I(explicitlySelected: true)! with
            {
                Backend = ObservabilityCaptureBackend.OpenTelemetry,
                RequestedCaptureSeconds = 30,
            };
            var capture = reopened.RunIsolatedObservabilityUx7I(captureDraft, CancellationToken.None);
            var tracy = reopened.RunIsolatedObservabilityUx7I(captureDraft with
            {
                Backend = ObservabilityCaptureBackend.TracyNative,
            }, CancellationToken.None);
            using var cancelledSource = new CancellationTokenSource();
            cancelledSource.Cancel();
            var cancelled = reopened.RunIsolatedObservabilityUx7I(captureDraft, cancelledSource.Token);
            openTelemetry = capture.Succeeded && capture.SpanCount >= 5 && capture.Surfaces.Count == 3 &&
                            capture.Categories.Contains("script", StringComparer.Ordinal);
            nativeDiagnostic = !tracy.Succeeded && tracy.Diagnostics.Any(item =>
                item.Code == "OBSERVABILITY_TRACY_RUNTIME_MISSING");
            cancellation = cancelled.Cancelled && cancelled.TemporaryWorkspaceReleased;
            artifactConfined = capture.ArtifactConfined && tracy.ArtifactConfined && cancelled.ArtifactConfined;
            resultNavigation = capture.Surfaces.Count == 3 && capture.Categories.Count >= 4 &&
                               nativeDiagnostic && !string.IsNullOrWhiteSpace(capture.ArtifactLabel);
        }
        finally
        {
            reopened.Close();
            editing.Close();
            cleanup = DeleteUx7ITemporaryWorkspace(root);
        }
        var sourceUnchanged = ReferenceEquals(sourceProject, Project) &&
                              ReferenceEquals(sourceHistory, History) && History?.State.Revision == sourceRevision &&
                              (History?.Journal ?? []).SequenceEqual(sourceJournal) && IsDirty == sourceDirty &&
                              ActiveAreaId == sourceArea && SelectedEntityIds.SequenceEqual(sourceSelection) &&
                              SelectedEntityId == sourceSelected &&
                              FileFingerprintUx7D(Session.ProjectFilePath) == sourceFile;
        var succeeded = explicitSelection && canonicalCommand && invalidBlocked && staleBlocked && undoRedo &&
                        roundTrip && preservation && openTelemetry && nativeDiagnostic && cancellation &&
                        resultNavigation && isolated && artifactConfined && sourceUnchanged && cleanup;
        return new(succeeded, explicitSelection, canonicalCommand, invalidBlocked, staleBlocked, undoRedo,
            roundTrip, preservation, openTelemetry, nativeDiagnostic, cancellation, resultNavigation, isolated,
            artifactConfined, sourceUnchanged, cleanup);
    }

    internal void CloseUx7I()
    {
        CancelObservabilityApprovalUx7I();
        _lastUx7ITemporaryReleased = true;
    }

    private static bool TryBuildObservabilityExtensionsUx7I(
        ProjectDefinition project,
        StudioUx7IDraft draft,
        out ModuleRuntimeExtensionProfile? candidate,
        out ProjectDefinition? candidateProject,
        out StudioUx7IDiagnostic? diagnostic)
    {
        candidate = null;
        candidateProject = null;
        diagnostic = null;
        try
        {
            var policies = project.Module.RuntimePolicies;
            var extensions = policies.Extensions;
            var post = extensions.PostLaunch;
            var creator = post.CreatorExperience;
            var observability = new CreatorExperienceProductionProfile.ObservabilitySettings(
                creator.Observability.Id, draft.Enabled, draft.TracyCompatible,
                draft.OpenTelemetryCompatible, draft.MaximumCaptureSeconds, draft.IncludeScripts);
            var updatedCreator = new CreatorExperienceProductionProfile(creator.Id, creator.SchemaVersion,
                creator.Water, creator.DeviceQueries, creator.ResourceCommands, creator.DynamicAreas,
                creator.PartyControl, creator.ProgressionTracks, creator.RepositoryMetadata, observability,
                creator.Graphics, Ux7IProvenance);
            var updatedPost = new PostLaunchCapabilityProfile(post.Id, post.SchemaVersion, post.Gameplay,
                post.LiveDirector, post.Network, updatedCreator, post.Production, Ux7IProvenance);
            candidate = new ModuleRuntimeExtensionProfile(extensions.Id, extensions.SchemaVersion,
                extensions.RuleSchemas, extensions.UiDocuments, extensions.JsonDocuments,
                extensions.SnapshotPolicy, Ux7IProvenance, extensions.ModernTooling,
                extensions.OperationalReadiness, updatedPost);
            candidateProject = ProjectWithExtensionsUx7I(project, candidate);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            diagnostic = new("OBSERVABILITY_CONFIGURATION_INVALID", Ux7IPath,
                HumanDiagnosticUx7I("OBSERVABILITY_CONFIGURATION_INVALID", exception.Message));
            return false;
        }
    }

    private static ProjectDefinition ProjectWithExtensionsUx7I(ProjectDefinition project,
        ModuleRuntimeExtensionProfile extensions)
    {
        var current = project.Module.RuntimePolicies;
        var policies = new ModuleRuntimePolicyProfile(current.Id, current.SchemaVersion, current.Heartbeats,
            current.Rest, current.SequenceAbort, Ux7IProvenance, current.Cinematics, current.Gameplay,
            current.Rules, extensions, current.GameplayEvents, current.World, current.LifecycleEvents);
        return new ProjectDefinition(project.Id, project.Title, project.Module.WithRuntimePolicies(policies),
            project.Blueprints, project.Areas, project.Dialogues, project.Quests);
    }

    private static StudioUx7IDiff BuildObservabilityDiffUx7I(
        CreatorExperienceProductionProfile.ObservabilitySettings source,
        CreatorExperienceProductionProfile.ObservabilitySettings target)
    {
        var entries = new List<string>();
        Add(source.Enabled, target.Enabled, "Observabilité");
        Add(source.TracyCompatible, target.TracyCompatible, "Compatibilité Tracy");
        Add(source.OpenTelemetryCompatible, target.OpenTelemetryCompatible, "Compatibilité OpenTelemetry");
        Add(source.IncludeScripts, target.IncludeScripts, "Spans ForgeScript");
        if (source.MaximumCaptureSeconds != target.MaximumCaptureSeconds)
            entries.Add($"Durée maximale · {source.MaximumCaptureSeconds} s → {target.MaximumCaptureSeconds} s");
        return new(entries);

        void Add(bool before, bool after, string label)
        {
            if (before != after) entries.Add($"{label} · {(before ? "activé" : "désactivé")} → " +
                                              (after ? "activé" : "désactivé"));
        }
    }

    private static string ObservabilitySourceFingerprintUx7I(ProjectDefinition project) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));

    private static string ObservabilityDraftFingerprintUx7I(StudioUx7IDraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            WriteTextUx7I(writer, "ux7-i-draft-v1");
            writer.Write(draft.BaseRevision.Value);
            WriteTextUx7I(writer, draft.SourceFingerprint);
            writer.Write(draft.Enabled);
            writer.Write(draft.TracyCompatible);
            writer.Write(draft.OpenTelemetryCompatible);
            writer.Write(draft.MaximumCaptureSeconds);
            writer.Write(draft.IncludeScripts);
            writer.Write((int)draft.Backend);
            writer.Write(draft.RequestedCaptureSeconds);
        }
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteTextUx7I(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static bool ConfinedArtifactUx7I(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root);
        var output = Path.GetFullPath(Path.Combine(fullRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        return output.StartsWith(fullRoot + Path.DirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase) && File.Exists(output);
    }

    internal static bool DeleteUx7ITemporaryWorkspace(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return true;
        try
        {
            var root = Path.GetFullPath(directory);
            var temp = Path.GetFullPath(Path.GetTempPath());
            var prefix = temp.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                         Path.DirectorySeparatorChar;
            var leaf = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
            if (!root.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !leaf.StartsWith("openrpg-forge-studio-ux7-i-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            return !Directory.Exists(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static StudioUx7IPreview FailedPreviewUx7I(string code, string path, string message) => new(
        false, false, false, new([]), [new(code, path, HumanDiagnosticUx7I(code, message))],
        "Configuration refusée", "Aucune mutation n’a été produite.");

    private static StudioUx7ICaptureResult FailedCaptureUx7I(string message,
        IReadOnlyList<StudioUx7IDiagnostic>? diagnostics = null) => new(false, false, false, false, true, true,
        ObservabilityCaptureBackend.OpenTelemetry, 0, [], [], string.Empty, string.Empty,
        diagnostics ?? [new("OBSERVABILITY_CAPTURE_FAILED", "runtime/observability/capture", message)], message);

    private static StudioUx7ISmokeResult FailedSmokeUx7I() => new(false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false);

    private static string HumanDiagnosticUx7I(string code, string fallback) => code switch
    {
        "OBSERVABILITY_TRACY_RUNTIME_MISSING" =>
            "Tracy natif indisponible : utilisez un Godot instrumenté et un serveur de capture correspondant, " +
            "ou choisissez le fallback OpenTelemetry.",
        "OBSERVABILITY_TRACY_NOT_ALLOWED" => "La compatibilité Tracy n’est pas autorisée par le profil.",
        "OBSERVABILITY_OTEL_NOT_ALLOWED" => "La compatibilité OpenTelemetry n’est pas autorisée par le profil.",
        "OBSERVABILITY_DISABLED" => "Activez l’observabilité avant de lancer une capture.",
        "OBSERVABILITY_CAPTURE_CANCELLED" => "Capture annulée proprement.",
        "OBSERVABILITY_CONFIGURATION_INVALID" =>
            "Une observabilité active exige au moins un backend compatible Tracy ou OpenTelemetry.",
        _ => fallback,
    };

    private static string HumanTextUx7I(string value) => value.Replace('\\', '/');
}
