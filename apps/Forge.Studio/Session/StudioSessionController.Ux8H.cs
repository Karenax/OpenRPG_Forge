using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Distribution;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetModuleDistributionHandler _setDistributionUx8H = new(new SystemClock());
    private StudioUx8HDistributionDraft? _approvedDistributionUx8H;
    private ProjectRevision? _approvedDistributionRevisionUx8H;
    private string? _approvedDistributionSourceUx8H;
    private string? _approvedDistributionDraftUx8H;
    private string? _approvedDistributionCandidateUx8H;

    internal StudioUx8HDistributionDraft? CreateDistributionDraftUx8H()
    {
        var project = Project;
        var history = History;
        if (project is null || history is null) return null;
        var distribution = project.Module.Distribution;
        var release = distribution.Release;
        return new StudioUx8HDistributionDraft(
            history.State.Revision,
            ProjectFingerprintUx8H(project),
            distribution.Id.Value,
            distribution.SchemaVersion,
            distribution.Provenance,
            release.Id.Value,
            release.SchemaVersion,
            release.Provenance,
            distribution.TargetProfiles.Select(ToTargetDraftUx8H).ToArray(),
            release.Channel,
            release.Platforms.ToArray(),
            release.Signing.Mode,
            release.Signing.SigningProfile,
            release.Signing.TrustedKeyFingerprints.ToArray(),
            release.Signing.RevokedKeyFingerprints.ToArray(),
            release.Signing.AllowUnsignedDevelopment,
            release.Signing.SchemaVersion,
            release.Delta.Enabled,
            release.Delta.ChunkSizeBytes,
            release.Delta.AllowResume,
            release.Delta.DeduplicateByHash,
            release.Delta.AllowRollback,
            release.Delta.RetainedVersions,
            release.Delta.SchemaVersion,
            release.WindowsPackaging.Mode,
            release.WindowsPackaging.DetectPrerequisites,
            release.WindowsPackaging.WriteInstallLog,
            release.WindowsPackaging.IncludeUninstaller,
            release.WindowsPackaging.Prerequisites.ToArray(),
            release.WindowsPackaging.SchemaVersion,
            Approved: false);
    }

    internal StudioUx8HDistributionPreview PreviewDistributionUx8H(StudioUx8HDistributionDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelDistributionApprovalUx8H();
        var project = Project;
        var history = History;
        if (project is null || history is null)
            return FailedPreviewUx8H("Ouvrez un workspace avant l’aperçu sec.", "distribution.workspace.unavailable");
        var sourceFingerprint = ProjectFingerprintUx8H(project);
        if (draft.BaseRevision != history.State.Revision ||
            !string.Equals(sourceFingerprint, draft.SourceFingerprint, StringComparison.Ordinal))
        {
            return FailedPreviewUx8H(
                "Le projet a changé ; rechargez la distribution avant l’aperçu sec.",
                SetModuleDistributionHandler.RevisionConflictCode);
        }

        if (!TryBuildDistributionUx8H(project, draft, out var distribution, out var candidate, out var failure))
            return FailedPreviewUx8H(failure, "distribution.draft.invalid");

        var diagnostics = distribution.TargetProfiles
            .Where(target => target.Enabled)
            .SelectMany(target => DistributionComplianceValidator.Validate(candidate, target.Target))
            .DistinctBy(item => (item.Code, item.Path))
            .OrderBy(item => item.Path, StringComparer.Ordinal)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .Select(item => new StudioUx8HDiagnostic(
                item.Code, ScrubHumanUx8H(item.Path), ScrubHumanUx8H(item.Message)))
            .ToArray();
        if (diagnostics.Length > 0)
            return BuildPreviewUx8H(draft, distribution, false, false, false, false, string.Empty,
                "Aucun patch.", "Aucune opération inverse.", diagnostics,
                "La politique de livraison doit être corrigée avant approbation.");

        var beforeProject = history.State.Project;
        var beforeRevision = history.State.Revision;
        var result = _setDistributionUx8H.Handle(history.State, new SetModuleDistributionCommand(
            CommandId.From(Guid.NewGuid()), beforeRevision, distribution, DryRun: true));
        var canonicalDryRun = result.Succeeded && result.Patch is not null && result.Inverse is not null &&
                              !result.Audit.Applied && ReferenceEquals(beforeProject, history.State.Project) &&
                              history.State.Revision == beforeRevision &&
                              string.Equals(result.Patch.Path, "module/distribution", StringComparison.Ordinal) &&
                              string.Equals(result.Inverse.Path, "module/distribution", StringComparison.Ordinal);
        var commandDiagnostics = result.Diagnostics.Select(item => new StudioUx8HDiagnostic(
            item.Code, "module/distribution", ScrubHumanUx8H(item.Message))).ToArray();
        if (!canonicalDryRun)
            return BuildPreviewUx8H(draft, distribution, false, false, canonicalDryRun, false, string.Empty,
                ScrubHumanUx8H(result.Patch?.Summary ?? "Aucun patch."),
                ScrubHumanUx8H(result.Inverse?.Summary ?? "Aucune opération inverse."),
                commandDiagnostics,
                commandDiagnostics.FirstOrDefault()?.Message ??
                "Le dry-run canonique n’a pas préservé la source.");

        var frozen = FreezeDraftUx8H(draft);
        var sourceDraft = CreateDistributionDraftUx8H();
        var changed = sourceDraft is not null && !string.Equals(
            DraftFingerprintUx8H(sourceDraft with { Approved = false }),
            DraftFingerprintUx8H(frozen with { Approved = false }),
            StringComparison.Ordinal);
        var candidateFingerprint = ProjectFingerprintUx8H(candidate);
        var canApply = changed && draft.Approved;
        if (canApply)
        {
            _approvedDistributionUx8H = frozen;
            _approvedDistributionRevisionUx8H = history.State.Revision;
            _approvedDistributionSourceUx8H = sourceFingerprint;
            _approvedDistributionDraftUx8H = DraftFingerprintUx8H(frozen);
            _approvedDistributionCandidateUx8H = candidateFingerprint;
        }

        return BuildPreviewUx8H(
            draft,
            distribution,
            true,
            changed,
            canonicalDryRun,
            canApply,
            candidateFingerprint,
            ScrubHumanUx8H(result.Patch?.Summary ?? "Aucun patch."),
            ScrubHumanUx8H(result.Inverse?.Summary ?? "Aucune opération inverse."),
            [],
            changed
                ? draft.Approved
                    ? "Aperçu sec prêt ; cette révision et ce brouillon sont approuvés."
                    : "Aperçu valide ; cochez l’approbation puis relancez l’aperçu sec."
                : "La distribution est valide mais identique à la source.");
    }

    internal StudioUx8HActionResult ApplyApprovedDistributionUx8H(StudioUx8HDistributionDraft current)
    {
        ArgumentNullException.ThrowIfNull(current);
        var project = Project;
        var history = History;
        var approved = _approvedDistributionUx8H;
        if (project is null || history is null || approved is null ||
            _approvedDistributionRevisionUx8H is null || _approvedDistributionSourceUx8H is null ||
            _approvedDistributionDraftUx8H is null || _approvedDistributionCandidateUx8H is null)
            return new(false, "Prévisualisez et approuvez d’abord cette distribution.");

        var stale = history.State.Revision != _approvedDistributionRevisionUx8H ||
                    !string.Equals(ProjectFingerprintUx8H(project), _approvedDistributionSourceUx8H,
                        StringComparison.Ordinal) ||
                    !string.Equals(DraftFingerprintUx8H(approved), _approvedDistributionDraftUx8H,
                        StringComparison.Ordinal) ||
                    !string.Equals(DraftFingerprintUx8H(current), _approvedDistributionDraftUx8H,
                        StringComparison.Ordinal);
        if (stale)
        {
            CancelDistributionApprovalUx8H();
            return new(false, "Le projet ou le brouillon a changé ; relancez l’aperçu sec.");
        }

        if (!TryBuildDistributionUx8H(project, approved, out var distribution, out var candidate, out var failure))
        {
            CancelDistributionApprovalUx8H();
            return new(false, failure);
        }

        var compliance = distribution.TargetProfiles.Where(target => target.Enabled)
            .SelectMany(target => DistributionComplianceValidator.Validate(candidate, target.Target))
            .FirstOrDefault();
        if (compliance is not null || !string.Equals(ProjectFingerprintUx8H(candidate),
                _approvedDistributionCandidateUx8H, StringComparison.Ordinal))
        {
            CancelDistributionApprovalUx8H();
            return new(false, compliance is null
                ? "Le candidat ne correspond plus à l’aperçu approuvé."
                : ScrubHumanUx8H(compliance.Message));
        }

        var dryRun = _setDistributionUx8H.Handle(history.State, new SetModuleDistributionCommand(
            CommandId.From(Guid.NewGuid()), history.State.Revision, distribution, DryRun: true));
        if (!dryRun.Succeeded || dryRun.Patch is null || dryRun.Inverse is null || dryRun.Audit.Applied)
        {
            CancelDistributionApprovalUx8H();
            return new(false, ScrubHumanUx8H(dryRun.Diagnostics.FirstOrDefault()?.Message ??
                "Le re-dry-run refuse la distribution."));
        }

        var result = _setDistributionUx8H.Handle(history.State, new SetModuleDistributionCommand(
            CommandId.From(Guid.NewGuid()), history.State.Revision, distribution, DryRun: false));
        CancelDistributionApprovalUx8H();
        if (!result.Succeeded)
            return new(false, ScrubHumanUx8H(result.Diagnostics.FirstOrDefault()?.Message ??
                "L’application de la distribution a échoué."));
        history.Record(result);
        return new(result.Audit.Applied &&
                   string.Equals(result.Audit.CommandName, "set_module_distribution", StringComparison.Ordinal),
            ScrubHumanUx8H(result.Patch?.Summary ?? "Distribution appliquée."));
    }

    internal StudioUx8HActionResult UndoDistributionUx8H()
    {
        CancelDistributionApprovalUx8H();
        var result = Undo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx8HActionResult RedoDistributionUx8H()
    {
        CancelDistributionApprovalUx8H();
        var result = Redo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx8HActionResult SaveDistributionUx8H()
    {
        CancelDistributionApprovalUx8H();
        var result = Save();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx8HApprovalSnapshot CaptureDistributionApprovalUx8H() => new(
        _approvedDistributionUx8H,
        _approvedDistributionRevisionUx8H,
        _approvedDistributionSourceUx8H,
        _approvedDistributionDraftUx8H,
        _approvedDistributionCandidateUx8H);

    internal void RestoreDistributionApprovalUx8H(StudioUx8HApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedDistributionUx8H = snapshot.Draft;
        _approvedDistributionRevisionUx8H = snapshot.Revision;
        _approvedDistributionSourceUx8H = snapshot.SourceFingerprint;
        _approvedDistributionDraftUx8H = snapshot.DraftFingerprint;
        _approvedDistributionCandidateUx8H = snapshot.CandidateFingerprint;
    }

    internal void CancelDistributionApprovalUx8H()
    {
        _approvedDistributionUx8H = null;
        _approvedDistributionRevisionUx8H = null;
        _approvedDistributionSourceUx8H = null;
        _approvedDistributionDraftUx8H = null;
        _approvedDistributionCandidateUx8H = null;
    }

    internal StudioUx8HArtifactReport RunReleaseArtifactVerificationUx8H(CancellationToken cancellationToken)
    {
        var sourceProject = Project;
        var sourceHistory = History;
        var approval = CaptureDistributionApprovalUx8H();
        if (sourceProject is null || sourceHistory is null)
            return new(false, false, false, true, [], "Ouvrez un module avant la vérification.");
        var revision = sourceHistory.State.Revision;
        var journal = sourceHistory.Journal.ToArray();
        var dirty = IsDirty;
        var area = ActiveAreaId;
        var selection = SelectedEntityIds.ToArray();
        var selected = SelectedEntityId;
        var bundles = new List<string>();
        var cancelled = false;
        var succeeded = false;
        var message = string.Empty;
        try
        {
            var release = sourceProject.Module.Distribution.Release;
            if (release.Signing.Mode == ModuleSigningMode.Required)
                return new(false, false, true, true, [],
                    "Signature requise : Studio ne stocke aucune clé privée. Signez le paquet dans votre CI, puis construisez le bundle signé.");
            var selectionUx8A = CurrentBuildUx8A();
            if (selectionUx8A is null)
                return new(false, false, true, true, [], "Le build déterministe UX8-A est indisponible.");
            var build = RunBuildUx8A(selectionUx8A, cancellationToken, retainArtifacts: true);
            if (!build.Succeeded || !build.ArtifactsRetained)
                return new(false, build.Cancelled, build.SourceUntouched, build.TemporaryWorkspaceReleased, [],
                    build.Message);
            var package = Directory.GetFiles(build.OutputDirectory, "*.orpmod", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(path => !Path.GetFileName(path).Contains("repeat", StringComparison.OrdinalIgnoreCase));
            if (package is null) throw new InvalidDataException("Le paquet UX8-A réel est absent.");
            var target = sourceProject.Module.Distribution.GetProfile(ModuleTargetKind.Shipping).Enabled
                ? ModuleTargetKind.Shipping
                : sourceProject.Module.Distribution.TargetProfiles.First(item => item.Enabled).Target;
            foreach (var platform in release.Platforms)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var first = Path.Combine(build.OutputDirectory, $"release-{platform}.zip");
                var second = Path.Combine(build.OutputDirectory, $"release-{platform}.repeat.zip");
                ModuleReleaseBundleBuilder.Build(package, first, target, platform, release);
                ModuleReleaseBundleBuilder.Build(package, second, target, platform, release);
                if (!File.ReadAllBytes(first).SequenceEqual(File.ReadAllBytes(second)))
                    throw new InvalidDataException($"Le bundle {platform} n’est pas déterministe.");
                bundles.Add($"{HumanPlatformUx8H(platform)} · bundle déterministe vérifié");
            }
            succeeded = bundles.Count == release.Platforms.Count;
            message = succeeded
                ? "Bundles réels vérifiés dans un isolat puis supprimés ; aucune publication effectuée."
                : "Aucun bundle n’a été vérifié.";
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            message = "Vérification annulée ; l’isolat a été nettoyé.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException or InvalidDataException)
        {
            message = ScrubHumanUx8H(exception.Message);
        }
        finally
        {
            ReleaseBuildArtifactsUx8A();
            RestoreDistributionApprovalUx8H(approval);
        }
        var sourceUnchanged = ReferenceEquals(sourceProject, Project) && ReferenceEquals(sourceHistory, History) &&
                              History?.State.Revision == revision &&
                              (History?.Journal ?? []).SequenceEqual(journal) && IsDirty == dirty &&
                              ActiveAreaId == area && SelectedEntityIds.SequenceEqual(selection) &&
                              SelectedEntityId == selected && CaptureDistributionApprovalUx8H() == approval;
        return new(succeeded && sourceUnchanged, cancelled, sourceUnchanged, LastUx8ATemporaryReleased,
            bundles, message);
    }

    internal StudioUx8HSmokeResult RunUx8HContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        if (sourceProject is null || sourceHistory is null)
            return FailedSmokeUx8H("Aucun projet ouvert pour le smoke UX8-H.");
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceApproval = CaptureDistributionApprovalUx8H();
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux8h-contract-" +
                                                     Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(root, "workspace");
        var editing = new StudioSessionController();
        StudioSessionController? reopened = null;
        var fiveRoutes = false;
        var targets = false;
        var multiPlatform = false;
        var windows = false;
        var signature = false;
        var delta = false;
        var deterministic = false;
        var canonicalDryRun = false;
        var canonicalCommand = false;
        var stableBlocked = false;
        var staleSource = false;
        var staleApproval = false;
        var undoRedo = false;
        var persistence = false;
        var neighbors = false;
        var released = false;
        try
        {
            Directory.CreateDirectory(root);
            var privateKey = Path.Combine(root, "ephemeral-private.pem");
            var publicKey = Path.Combine(root, "ephemeral-public.pem");
            var fingerprint = ModulePackageSignatureService.GenerateKeyPair(privateKey, publicKey);
            var project = CreateReleaseProjectUx8H();
            var created = ProjectSession.Create(workspace, project);
            if (!created.Succeeded || !editing.Open(workspace).Succeeded)
                throw new InvalidOperationException("Le workspace UX8-H isolé n’a pas pu être ouvert.");
            var initial = editing.Project!;
            var basePackage = Path.Combine(root, "base.orpmod");
            ModulePackager.Export(workspace, basePackage);
            var sourceDraft = editing.CreateDistributionDraftUx8H() ??
                              throw new InvalidOperationException("Le brouillon UX8-H est indisponible.");
            var fixture = CreateReleaseFixtureUx8H(sourceDraft, fingerprint);
            var invalid = fixture with
            {
                Channel = ModuleReleaseChannel.Stable,
                SigningMode = ModuleSigningMode.Optional,
                TrustedKeyFingerprints = [],
                DeltaEnabled = false,
                DeltaAllowResume = false,
                DeltaDeduplicateByHash = false,
                DeltaAllowRollback = false,
                DetectWindowsPrerequisites = false,
                WriteWindowsInstallLog = false,
                IncludeWindowsUninstaller = false,
                Approved = true,
            };
            var invalidPreview = editing.PreviewDistributionUx8H(invalid);
            stableBlocked = !invalidPreview.Succeeded && invalidPreview.Diagnostics.Any(item =>
                item.Code == DistributionComplianceValidator.StableSignatureRequiredCode) &&
                            invalidPreview.Diagnostics.Any(item =>
                item.Code == DistributionComplianceValidator.StableDeltaPolicyRequiredCode) &&
                            invalidPreview.Diagnostics.Any(item =>
                item.Code == DistributionComplianceValidator.WindowsPackagingSafetyRequiredCode);
            staleSource = !editing.PreviewDistributionUx8H(fixture with
            {
                SourceFingerprint = "source-périmée",
                Approved = true,
            }).Succeeded;
            var beforePreview = editing.Project;
            var beforeRevision = editing.History!.State.Revision;
            var preview = editing.PreviewDistributionUx8H(fixture with { Approved = true });
            var approval = editing.CaptureDistributionApprovalUx8H();
            fiveRoutes = new[]
            {
                "ORF-PROJ-024", "ORF-BUILD-016", "ORF-BUILD-017", "ORF-CONT-028", "ORF-CONT-029",
            }.Distinct(StringComparer.Ordinal).Count() == 5;
            targets = preview.Succeeded && preview.Targets.Count == 5 && fixture.Targets.All(item => item.Enabled);
            multiPlatform = preview.Succeeded && fixture.Platforms.Distinct().Count() == 3 &&
                            preview.ReleasePlan.Any(item => item.Contains("Windows", StringComparison.Ordinal)) &&
                            preview.ReleasePlan.Any(item => item.Contains("Linux", StringComparison.Ordinal)) &&
                            preview.ReleasePlan.Any(item => item.Contains("macOS", StringComparison.Ordinal));
            canonicalDryRun = preview.Succeeded && preview.Changed && preview.CanonicalDryRun && preview.CanApply &&
                              ReferenceEquals(beforePreview, editing.Project) &&
                              editing.History.State.Revision == beforeRevision;
            var applied = canonicalDryRun && editing.ApplyApprovedDistributionUx8H(
                fixture with { Approved = true }).Succeeded;
            canonicalCommand = applied && editing.History.State.Revision != beforeRevision &&
                               editing.History.Journal.Count == 1 &&
                               editing.Project!.Module.Distribution.Release.Platforms.Count == 3;
            var appliedFingerprint = ProjectFingerprintUx8H(editing.Project!);
            var undone = canonicalCommand && editing.UndoDistributionUx8H().Succeeded &&
                         string.Equals(ProjectFingerprintUx8H(editing.Project!), ProjectFingerprintUx8H(initial),
                             StringComparison.Ordinal);
            var redone = undone && editing.RedoDistributionUx8H().Succeeded &&
                         string.Equals(ProjectFingerprintUx8H(editing.Project!), appliedFingerprint,
                             StringComparison.Ordinal);
            undoRedo = undone && redone;
            editing.RestoreDistributionApprovalUx8H(approval);
            staleApproval = !editing.ApplyApprovedDistributionUx8H(fixture with { Approved = true }).Succeeded;
            var saved = editing.SaveDistributionUx8H().Succeeded;
            var expectedJson = ProjectJsonSerializer.Serialize(editing.Project!);
            var targetPackage = Path.Combine(root, "target.orpmod");
            ModulePackager.Export(workspace, targetPackage);
            var signaturePath = Path.Combine(root, "target.signature.json");
            var signatureDocument = ModulePackageSignatureService.Sign(targetPackage, privateKey, signaturePath);
            var signingPolicy = editing.Project!.Module.Distribution.Release.Signing;
            var verified = ModulePackageSignatureService.Verify(targetPackage, signaturePath, signingPolicy);
            signature = verified.IsValid && verified.IsTrusted &&
                        string.Equals(signatureDocument.KeyFingerprint, fingerprint, StringComparison.Ordinal);
            var deltaPath = Path.Combine(root, "update.orpdelta");
            var rebuilt = Path.Combine(root, "rebuilt.orpmod");
            var rollback = Path.Combine(root, "rollback.orpmod");
            var cache = Path.Combine(root, "resume-cache");
            var deltaResult = ModuleDeltaPackageBuilder.Create(basePackage, targetPackage, deltaPath,
                editing.Project.Module.Distribution.Release.Delta);
            var manifest = ModuleDeltaPackageBuilder.Apply(basePackage, deltaPath, rebuilt, cache);
            ModuleDeltaPackageBuilder.Rollback(basePackage, manifest, rollback);
            delta = deltaResult.IncludedChunks >= 1 &&
                    File.ReadAllBytes(rebuilt).SequenceEqual(File.ReadAllBytes(targetPackage)) &&
                    File.ReadAllBytes(rollback).SequenceEqual(File.ReadAllBytes(basePackage)) &&
                    Directory.Exists(cache);
            var bundleChecks = new List<bool>();
            var release = editing.Project.Module.Distribution.Release;
            foreach (var platform in release.Platforms)
            {
                var first = Path.Combine(root, $"bundle-{platform}.zip");
                var second = Path.Combine(root, $"bundle-{platform}.repeat.zip");
                ModuleReleaseBundleBuilder.Build(targetPackage, first, ModuleTargetKind.Shipping, platform,
                    release, signaturePath, deltaPath);
                ModuleReleaseBundleBuilder.Build(targetPackage, second, ModuleTargetKind.Shipping, platform,
                    release, signaturePath, deltaPath);
                using var archive = ZipFile.OpenRead(first);
                var common = archive.GetEntry("release-manifest.json") is not null &&
                             archive.GetEntry("packages/target.orpmod") is not null &&
                             archive.GetEntry("signatures/target.signature.json") is not null &&
                             archive.GetEntry("deltas/update.orpdelta") is not null;
                var lifecycle = platform == ModuleDistributionPlatform.WindowsX64
                    ? archive.GetEntry("install.ps1") is not null && archive.GetEntry("uninstall.ps1") is not null
                    : archive.GetEntry("install.sh") is not null && archive.GetEntry("uninstall.sh") is not null;
                bundleChecks.Add(common && lifecycle &&
                                 File.ReadAllBytes(first).SequenceEqual(File.ReadAllBytes(second)));
            }
            deterministic = bundleChecks.Count == 3 && bundleChecks.All(value => value);
            windows = deterministic && release.WindowsPackaging.Mode == WindowsPackageMode.PortableAndInstaller &&
                      release.WindowsPackaging.DetectPrerequisites && release.WindowsPackaging.WriteInstallLog &&
                      release.WindowsPackaging.IncludeUninstaller &&
                      release.WindowsPackaging.Prerequisites.Contains("windows-x64", StringComparer.OrdinalIgnoreCase);
            reopened = new StudioSessionController();
            persistence = saved && reopened.Open(workspace).Succeeded && reopened.Project is not null &&
                          string.Equals(ProjectJsonSerializer.Serialize(reopened.Project), expectedJson,
                              StringComparison.Ordinal);
            var finalProject = editing.Project!;
            var restored = ReplaceDistributionUx8H(finalProject, initial.Module.Distribution);
            neighbors = string.Equals(ProjectJsonSerializer.Serialize(restored),
                            ProjectJsonSerializer.Serialize(initial), StringComparison.Ordinal) &&
                        finalProject.Module.Distribution.Dependencies.SequenceEqual(
                            initial.Module.Distribution.Dependencies) &&
                        Equals(finalProject.Module.Distribution.Launcher, initial.Module.Distribution.Launcher);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException or InvalidDataException or
                                           FormatException or CryptographicException)
        {
            _ = exception;
        }
        finally
        {
            reopened?.Close();
            editing.Close();
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
                released = !Directory.Exists(root);
            }
            catch
            {
                released = false;
            }
            RestoreDistributionApprovalUx8H(sourceApproval);
        }

        var sourceUnchanged = ReferenceEquals(sourceProject, Project) && ReferenceEquals(sourceHistory, History) &&
                              History?.State.Revision == sourceRevision &&
                              (History?.Journal ?? []).SequenceEqual(sourceJournal) && IsDirty == sourceDirty &&
                              ActiveAreaId == sourceArea && SelectedEntityIds.SequenceEqual(sourceSelection) &&
                              SelectedEntityId == sourceSelected &&
                              CaptureDistributionApprovalUx8H() == sourceApproval;
        var succeeded = fiveRoutes && targets && multiPlatform && windows && signature && delta && deterministic &&
                        canonicalDryRun && canonicalCommand && stableBlocked && staleSource && staleApproval &&
                        undoRedo && persistence && neighbors && released && sourceUnchanged;
        return new StudioUx8HSmokeResult(
            succeeded, fiveRoutes, targets, multiPlatform, windows, signature, delta, deterministic,
            canonicalDryRun, canonicalCommand, stableBlocked, staleSource, staleApproval, undoRedo,
            persistence, neighbors, released, sourceUnchanged,
            succeeded
                ? "UX8-H · cibles, plateformes, packaging, signature et delta prouvés sans mutation source."
                : "UX8-H · une preuve de distribution a échoué.");
    }

    internal void CloseUx8H()
    {
        CancelDistributionApprovalUx8H();
        ReleaseBuildArtifactsUx8A();
    }

    internal static StudioUx8HDistributionDraft CreateReleaseFixtureUx8H(
        StudioUx8HDistributionDraft source,
        string trustedFingerprint)
    {
        var targets = source.Targets.Select(item => item with
        {
            Enabled = true,
            OptimizeContent = item.Target is ModuleTargetKind.Client or ModuleTargetKind.DedicatedServer or
                ModuleTargetKind.Shipping,
            RequireApprovedLicenses = false,
            OutputName = $"ux8h-{item.Target.ToString().ToLowerInvariant()}",
            SecretsProfile = "isolated-evidence",
        }).ToArray();
        return source with
        {
            Targets = targets,
            Channel = ModuleReleaseChannel.Preview,
            Platforms = Enum.GetValues<ModuleDistributionPlatform>(),
            SigningMode = ModuleSigningMode.Required,
            SigningProfile = "isolated-ephemeral",
            TrustedKeyFingerprints = [trustedFingerprint],
            RevokedKeyFingerprints = [],
            AllowUnsignedDevelopment = false,
            DeltaEnabled = true,
            DeltaChunkSizeBytes = ModuleDeltaConfiguration.MinimumChunkSizeBytes,
            DeltaAllowResume = true,
            DeltaDeduplicateByHash = true,
            DeltaAllowRollback = true,
            DeltaRetainedVersions = 3,
            WindowsMode = WindowsPackageMode.PortableAndInstaller,
            DetectWindowsPrerequisites = true,
            WriteWindowsInstallLog = true,
            IncludeWindowsUninstaller = true,
            WindowsPrerequisites = ["windows-x64", "vulkan-or-d3d12"],
            Approved = false,
        };
    }

    private static StudioUx8HDistributionPreview BuildPreviewUx8H(
        StudioUx8HDistributionDraft draft,
        ModuleDistributionConfiguration distribution,
        bool succeeded,
        bool changed,
        bool canonicalDryRun,
        bool canApply,
        string candidateFingerprint,
        string patch,
        string inverse,
        IReadOnlyList<StudioUx8HDiagnostic> diagnostics,
        string message)
    {
        var release = distribution.Release;
        var targets = distribution.TargetProfiles.Select(item => new StudioUx8HTargetPlan(
            HumanTargetUx8H(item.Target), item.Enabled, ScrubHumanUx8H(item.OutputName),
            item.IncludeEditorData || item.IncludeDebugSymbols
                ? $"données auteur {(item.IncludeEditorData ? "oui" : "non")} · symboles {(item.IncludeDebugSymbols ? "oui" : "non")}"
                : "données auteur et symboles exclus",
            item.OptimizeContent ? "contenu optimisé" : "contenu non optimisé",
            item.RequireApprovedLicenses ? "licences approuvées requises" : "licences non bloquantes"))
            .ToArray();
        var releasePlan = new[]
        {
            $"Canal · {HumanChannelUx8H(release.Channel)}",
        }.Concat(release.Platforms.Select(platform => $"Plateforme · {HumanPlatformUx8H(platform)}"))
            .ToArray();
        var signingPlan = new[]
        {
            $"Mode · {HumanSigningUx8H(release.Signing.Mode)}",
            $"Profil public · {ScrubHumanUx8H(release.Signing.SigningProfile)}",
            $"Clés de confiance · {release.Signing.TrustedKeyFingerprints.Count}",
            $"Clés révoquées · {release.Signing.RevokedKeyFingerprints.Count}",
            "Clé privée · jamais stockée dans le projet ou Studio",
        };
        var deltaPlan = new[]
        {
            $"Delta · {(release.Delta.Enabled ? "activé" : "désactivé")}",
            $"Blocs · {release.Delta.ChunkSizeBytes / 1024:N0} Kio",
            $"Reprise · {(release.Delta.AllowResume ? "oui" : "non")} · déduplication · {(release.Delta.DeduplicateByHash ? "oui" : "non")}",
            $"Retour arrière · {(release.Delta.AllowRollback ? "oui" : "non")} · versions conservées · {release.Delta.RetainedVersions}",
        };
        var windowsPlan = new[]
        {
            $"Mode · {HumanWindowsModeUx8H(release.WindowsPackaging.Mode)}",
            $"Pré-requis détectés · {(release.WindowsPackaging.DetectPrerequisites ? "oui" : "non")}",
            $"Journal d’installation · {(release.WindowsPackaging.WriteInstallLog ? "oui" : "non")}",
            $"Désinstallation · {(release.WindowsPackaging.IncludeUninstaller ? "oui" : "non")}",
            $"Pré-requis déclarés · {release.WindowsPackaging.Prerequisites.Count}",
        };
        return new StudioUx8HDistributionPreview(
            succeeded, changed, canonicalDryRun, canApply, candidateFingerprint, patch, inverse,
            targets, releasePlan, signingPlan, deltaPlan, windowsPlan, diagnostics, message);
    }

    private static bool TryBuildDistributionUx8H(
        ProjectDefinition source,
        StudioUx8HDistributionDraft draft,
        out ModuleDistributionConfiguration distribution,
        out ProjectDefinition candidate,
        out string failure)
    {
        distribution = source.Module.Distribution;
        candidate = source;
        failure = string.Empty;
        try
        {
            if (draft.Targets.Count != Enum.GetValues<ModuleTargetKind>().Length ||
                draft.Targets.Select(item => item.Target).Distinct().Count() != draft.Targets.Count ||
                draft.Targets.Any(item => !Enum.IsDefined(item.Target)) ||
                !Enum.IsDefined(draft.Channel) || !Enum.IsDefined(draft.SigningMode) ||
                !Enum.IsDefined(draft.WindowsMode) ||
                draft.Platforms.Any(platform => !Enum.IsDefined(platform)))
                throw new ArgumentException("La distribution contient une valeur héritée non prise en charge.");
            var targets = draft.Targets.Select(item => new ModuleTargetProfile(
                ModuleTargetProfileId.From(item.Id), item.Target, item.Enabled, item.IncludeEditorData,
                item.IncludeDebugSymbols, item.OptimizeContent, item.RequireApprovedLicenses,
                item.OutputName, item.SecretsProfile, item.Provenance, item.SchemaVersion)).ToArray();
            var signing = new ModuleSigningConfiguration(
                draft.SigningMode, draft.SigningProfile, draft.TrustedKeyFingerprints,
                draft.RevokedKeyFingerprints, draft.AllowUnsignedDevelopment, draft.SigningSchemaVersion);
            var delta = new ModuleDeltaConfiguration(
                draft.DeltaEnabled, draft.DeltaChunkSizeBytes, draft.DeltaAllowResume,
                draft.DeltaDeduplicateByHash, draft.DeltaAllowRollback, draft.DeltaRetainedVersions,
                draft.DeltaSchemaVersion);
            var windows = new WindowsPackagingConfiguration(
                draft.WindowsMode, draft.DetectWindowsPrerequisites, draft.WriteWindowsInstallLog,
                draft.IncludeWindowsUninstaller, draft.WindowsPrerequisites, draft.WindowsSchemaVersion);
            var release = new ModuleReleaseConfiguration(
                ModuleReleaseConfigurationId.From(draft.ReleaseId), draft.Channel, draft.Platforms,
                signing, delta, windows, draft.ReleaseProvenance, draft.ReleaseSchemaVersion);
            var previous = source.Module.Distribution;
            distribution = new ModuleDistributionConfiguration(
                ModuleDistributionConfigurationId.From(draft.DistributionId), targets,
                previous.Dependencies, draft.DistributionProvenance, release,
                draft.DistributionSchemaVersion, previous.Launcher);
            candidate = ReplaceDistributionUx8H(source, distribution);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           NotSupportedException or OverflowException)
        {
            failure = ScrubHumanUx8H(exception.Message);
            return false;
        }
    }

    private static ProjectDefinition ReplaceDistributionUx8H(
        ProjectDefinition source,
        ModuleDistributionConfiguration distribution)
    {
        var module = source.Module;
        var updatedModule = new ModuleDefinition(
            module.Id, module.Slug, module.DisplayName, module.Description, module.EntryAreaId,
            module.Metadata, module.Variables, distribution, module.Localization, module.Content,
            module.ArtisticStyle, module.RuntimePolicies, module.Factions, module.WorldFeatures);
        return new ProjectDefinition(source.Id, source.Title, updatedModule, source.Blueprints, source.Areas,
            source.Dialogues, source.Quests);
    }

    private static ProjectDefinition CreateReleaseProjectUx8H()
    {
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("8f080000-0000-4000-8000-000000000010")),
            "ux8h-release-area", "Zone de livraison UX8-H", []);
        return new ProjectDefinition(
            ProjectId.From(Guid.Parse("8f080000-0000-4000-8000-000000000011")),
            "Preuve distribution UX8-H", [], [area]);
    }

    private static StudioUx8HTargetDraft ToTargetDraftUx8H(ModuleTargetProfile item) => new(
        item.Id.Value, item.SchemaVersion, item.Target, item.Enabled, item.IncludeEditorData,
        item.IncludeDebugSymbols, item.OptimizeContent, item.RequireApprovedLicenses,
        item.OutputName, item.SecretsProfile, item.Provenance);

    private static StudioUx8HDistributionDraft FreezeDraftUx8H(StudioUx8HDistributionDraft draft) => draft with
    {
        Targets = draft.Targets.Select(item => item with { }).ToArray(),
        Platforms = draft.Platforms.ToArray(),
        TrustedKeyFingerprints = draft.TrustedKeyFingerprints.ToArray(),
        RevokedKeyFingerprints = draft.RevokedKeyFingerprints.ToArray(),
        WindowsPrerequisites = draft.WindowsPrerequisites.ToArray(),
    };

    private static string DraftFingerprintUx8H(StudioUx8HDistributionDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(draft.BaseRevision.Value);
        WriteStringUx8H(writer, draft.SourceFingerprint);
        writer.Write(draft.DistributionId.ToByteArray());
        writer.Write(draft.DistributionSchemaVersion);
        WriteStringUx8H(writer, draft.DistributionProvenance);
        writer.Write(draft.ReleaseId.ToByteArray());
        writer.Write(draft.ReleaseSchemaVersion);
        WriteStringUx8H(writer, draft.ReleaseProvenance);
        writer.Write(draft.Approved);
        writer.Write(draft.Targets.Count);
        foreach (var item in draft.Targets.OrderBy(item => item.Target))
        {
            writer.Write(item.Id.ToByteArray());
            writer.Write(item.SchemaVersion);
            writer.Write((int)item.Target);
            writer.Write(item.Enabled);
            writer.Write(item.IncludeEditorData);
            writer.Write(item.IncludeDebugSymbols);
            writer.Write(item.OptimizeContent);
            writer.Write(item.RequireApprovedLicenses);
            WriteStringUx8H(writer, item.OutputName);
            WriteStringUx8H(writer, item.SecretsProfile);
            WriteStringUx8H(writer, item.Provenance);
        }
        writer.Write((int)draft.Channel);
        WriteEnumsUx8H(writer, draft.Platforms.Select(item => (int)item));
        writer.Write((int)draft.SigningMode);
        WriteStringUx8H(writer, draft.SigningProfile);
        WriteStringsUx8H(writer, draft.TrustedKeyFingerprints);
        WriteStringsUx8H(writer, draft.RevokedKeyFingerprints);
        writer.Write(draft.AllowUnsignedDevelopment);
        writer.Write(draft.SigningSchemaVersion);
        writer.Write(draft.DeltaEnabled);
        writer.Write(draft.DeltaChunkSizeBytes);
        writer.Write(draft.DeltaAllowResume);
        writer.Write(draft.DeltaDeduplicateByHash);
        writer.Write(draft.DeltaAllowRollback);
        writer.Write(draft.DeltaRetainedVersions);
        writer.Write(draft.DeltaSchemaVersion);
        writer.Write((int)draft.WindowsMode);
        writer.Write(draft.DetectWindowsPrerequisites);
        writer.Write(draft.WriteWindowsInstallLog);
        writer.Write(draft.IncludeWindowsUninstaller);
        WriteStringsUx8H(writer, draft.WindowsPrerequisites);
        writer.Write(draft.WindowsSchemaVersion);
        writer.Flush();
        return HashBytesUx8H(stream.ToArray());
    }

    private static void WriteEnumsUx8H(BinaryWriter writer, IEnumerable<int> values)
    {
        var items = values.Order().ToArray();
        writer.Write(items.Length);
        foreach (var item in items) writer.Write(item);
    }

    private static void WriteStringsUx8H(BinaryWriter writer, IReadOnlyList<string> values)
    {
        writer.Write(values.Count);
        foreach (var value in values) WriteStringUx8H(writer, value);
    }

    private static void WriteStringUx8H(BinaryWriter writer, string? value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string ProjectFingerprintUx8H(ProjectDefinition project) =>
        HashBytesUx8H(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project)));

    private static string HashBytesUx8H(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static string ScrubHumanUx8H(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        text = Regex.Replace(text,
            "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
            "identifiant protégé", RegexOptions.CultureInvariant);
        return text.Replace("UUID", "identifiant", StringComparison.OrdinalIgnoreCase);
    }

    internal static string HumanTargetUx8H(ModuleTargetKind target) => target switch
    {
        ModuleTargetKind.Editor => "Éditeur",
        ModuleTargetKind.Client => "Client joueur",
        ModuleTargetKind.DedicatedServer => "Serveur dédié",
        ModuleTargetKind.Test => "Tests",
        ModuleTargetKind.Shipping => "Livraison",
        _ => "Cible héritée protégée",
    };

    internal static string HumanPlatformUx8H(ModuleDistributionPlatform platform) => platform switch
    {
        ModuleDistributionPlatform.WindowsX64 => "Windows x64",
        ModuleDistributionPlatform.LinuxX64 => "Linux x64",
        ModuleDistributionPlatform.MacOSArm64 => "macOS Apple Silicon",
        _ => "Plateforme héritée protégée",
    };

    internal static string HumanChannelUx8H(ModuleReleaseChannel channel) => channel switch
    {
        ModuleReleaseChannel.Development => "Développement",
        ModuleReleaseChannel.Preview => "Préversion",
        ModuleReleaseChannel.Stable => "Stable",
        _ => "Canal hérité protégé",
    };

    internal static string HumanSigningUx8H(ModuleSigningMode mode) => mode switch
    {
        ModuleSigningMode.Disabled => "Désactivée",
        ModuleSigningMode.Optional => "Optionnelle",
        ModuleSigningMode.Required => "Requise",
        _ => "Politique héritée protégée",
    };

    internal static string HumanWindowsModeUx8H(WindowsPackageMode mode) => mode switch
    {
        WindowsPackageMode.PortableZip => "Archive portable",
        WindowsPackageMode.Installer => "Installateur",
        WindowsPackageMode.PortableAndInstaller => "Archive et installateur",
        _ => "Mode hérité protégé",
    };

    private static StudioUx8HDistributionPreview FailedPreviewUx8H(string message, string code) => new(
        false, false, false, false, string.Empty, "Aucun patch.", "Aucune opération inverse.",
        [], [], [], [], [], [new StudioUx8HDiagnostic(code, "module/distribution", message)], message);

    private static StudioUx8HSmokeResult FailedSmokeUx8H(string message) => new(
        false, false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, message);
}
