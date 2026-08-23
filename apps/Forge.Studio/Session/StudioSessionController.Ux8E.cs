using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Modules;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Diagnostics;
using Forge.ModuleFormat.Distribution;
using Forge.ModuleFormat.Documentation;
using Forge.ModuleFormat.Serialization;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux8ETemporaryPrefix = "openrpg-forge-studio-ux8e-";

    internal StudioUx8ESelection? CurrentProductionEvidenceUx8E()
    {
        if (Project is null || Session is null) return null;
        var repositoryRoot = FindRepositoryRootUx8E();
        return new StudioUx8ESelection(
            Project.Id.Value,
            Project.Module.DisplayName,
            repositoryRoot is null ? "Dépôt source indisponible" : new DirectoryInfo(repositoryRoot).Name,
            repositoryRoot is not null);
    }

    internal StudioUx8EReport RunProductionEvidenceUx8E(
        StudioUx8ESelection selection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (Project is null || Session is null || selection.ProjectId != Project.Id.Value)
            return FailedUx8E(selection, "La sélection ne correspond plus au module ouvert.");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var repositoryRoot = FindRepositoryRootUx8E();
            var repositoryName = repositoryRoot is null
                ? "Dépôt source indisponible"
                : new DirectoryInfo(repositoryRoot).Name;
            var stages = new List<StudioUx8EStage>(4)
            {
                InspectGeneratedDocumentationUx8E(repositoryRoot),
            };
            cancellationToken.ThrowIfCancellationRequested();
            stages.Add(InspectTaskManifestsUx8E(Project));
            cancellationToken.ThrowIfCancellationRequested();
            stages.Add(InspectReleaseChannelsUx8E(Project));
            cancellationToken.ThrowIfCancellationRequested();
            var crash = InspectCrashArtifactsUx8E(Project);
            stages.Add(crash.Stage);
            cancellationToken.ThrowIfCancellationRequested();

            return new StudioUx8EReport(
                true,
                false,
                crash.TemporaryWorkspaceReleased,
                selection.ModuleName,
                repositoryName,
                stages,
                $"Inspection terminée · {stages.Count(stage => stage.Ready)}/4 états prêts · aucune source modifiée.");
        }
        catch (OperationCanceledException)
        {
            return new StudioUx8EReport(
                true,
                true,
                true,
                selection.ModuleName,
                selection.RepositoryName,
                [],
                "Inspection annulée · aucune génération, publication ou collecte native n’a été lancée.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or InvalidOperationException or InvalidDataException)
        {
            return FailedUx8E(selection, "Inspection interrompue · " + exception.Message);
        }
    }

    internal StudioUx8ESmokeResult RunUx8EContractSmoke()
    {
        if (Project is null || History is null || Session is null)
            return FailedSmokeUx8E("Aucun module source n’est ouvert.");
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var tempBefore = CountUx8ETemporaryDirectories();
        var selection = CurrentProductionEvidenceUx8E();
        if (selection is null) return FailedSmokeUx8E("La sélection UX8-E est indisponible.");

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = RunProductionEvidenceUx8E(selection, cancellation.Token);
        var report = RunProductionEvidenceUx8E(selection, CancellationToken.None);
        var fourRoutes = report.Succeeded && !report.Cancelled && report.Stages.Count == 4 &&
                         report.Stages.Select(stage => stage.Route).SequenceEqual(
                             new[] { "ORF-BUILD-020", "ORF-BUILD-021", "ORF-BUILD-024", "ORF-BUILD-025" },
                             StringComparer.Ordinal);
        StudioUx8EStage Stage(string route) => report.Stages.Single(stage => stage.Route == route);
        var documentation = fourRoutes && Stage("ORF-BUILD-020").Evaluated &&
                            Stage("ORF-BUILD-020").Details.Any(detail =>
                                detail.Contains("six", StringComparison.OrdinalIgnoreCase));
        var tasks = fourRoutes && Stage("ORF-BUILD-021").Evaluated &&
                    Stage("ORF-BUILD-021").Details.Any(detail =>
                        detail.Contains("manifeste", StringComparison.OrdinalIgnoreCase));
        var releases = fourRoutes && Stage("ORF-BUILD-024").Evaluated &&
                       Stage("ORF-BUILD-024").Log.Any(line =>
                           line.StartsWith("publication=", StringComparison.Ordinal));
        var crash = fourRoutes && Stage("ORF-BUILD-025") is { Evaluated: true, Ready: true } &&
                    Stage("ORF-BUILD-025").Log.Contains("sanitization=passed", StringComparer.Ordinal);
        var cancellationReady = cancelled.Succeeded && cancelled.Cancelled && cancelled.Stages.Count == 0;
        var logs = report.Stages.All(stage => stage.Log.Count > 0) &&
                   report.Stages.SelectMany(stage => stage.Log).All(line => !Path.IsPathRooted(line));
        var temporaryReleased = report.TemporaryWorkspaceReleased &&
                                CountUx8ETemporaryDirectories() == tempBefore;
        var sourceUnchanged = ReferenceEquals(sourceProject, Project) && ReferenceEquals(sourceHistory, History) &&
                              History?.State.Revision == sourceRevision &&
                              (History?.Journal ?? []).SequenceEqual(sourceJournal) && IsDirty == sourceDirty &&
                              ActiveAreaId == sourceArea && SelectedEntityIds.SequenceEqual(sourceSelection) &&
                              SelectedEntityId == sourceSelected;
        var succeeded = fourRoutes && documentation && tasks && releases && crash && cancellationReady && logs &&
                        temporaryReleased && sourceUnchanged;
        return new StudioUx8ESmokeResult(
            succeeded,
            fourRoutes,
            documentation,
            tasks,
            releases,
            crash,
            cancellationReady,
            logs,
            temporaryReleased,
            sourceUnchanged,
            succeeded
                ? "UX8-E · documentation, tâches, canaux et crash inspectés sans mutation source."
                : "UX8-E · une preuve de préparation produit a échoué.");
    }

    internal void CloseUx8E()
    {
        // UX8-E ne conserve aucune approbation ni aucun état mutable dans la session.
    }

    private static StudioUx8EStage InspectGeneratedDocumentationUx8E(string? repositoryRoot)
    {
        if (repositoryRoot is null)
        {
            return new StudioUx8EStage(
                "ORF-BUILD-020", "Documentation générée", false, false, "SOURCE INDISPONIBLE",
                "Le dépôt source n’est pas résolu ; aucun fichier n’a été écrit.",
                ["Six catalogues attendus · état non évalué"],
                ["mode=check-only", "repository=unavailable", "mutation=none"]);
        }

        var result = ForgeDocumentationGenerator.Generate(repositoryRoot, "docs/generated", checkOnly: true);
        var details = new List<string>
        {
            $"Six catalogues déterministes · {result.Files.Count}/6 construits en mémoire",
            result.Current
                ? "Documentation suivie · aucun écart détecté"
                : $"Écart détecté · {result.ChangedFiles.Count} fichier(s) à régénérer explicitement",
        };
        details.AddRange(result.Files.Select(file =>
            $"{file.RelativePath} · {file.Content.Count(character => character == '\n')} ligne(s)"));
        if (!result.Current)
            details.AddRange(result.ChangedFiles.Select(file => $"À régénérer · {file}"));
        return new StudioUx8EStage(
            "ORF-BUILD-020", "Documentation générée", true, result.Current,
            result.Current ? "À JOUR" : "DRIFT DÉTECTÉ",
            "Le générateur canonique a été exécuté en mode contrôle strictement non mutant.",
            details,
            [
                "mode=check-only",
                $"catalogs={result.Files.Count}",
                $"changed={result.ChangedFiles.Count}",
                "output=docs/generated",
                "mutation=none",
            ]);
    }

    private static StudioUx8EStage InspectTaskManifestsUx8E(Forge.Domain.Projects.ProjectDefinition project)
    {
        var profile = project.Module.RuntimePolicies.Extensions.OperationalReadiness;
        var diagnostics = OperationalReadinessValidator.Validate(profile);
        var ready = profile.TaskManifests.Count > 0 && diagnostics.Count == 0;
        var details = new List<string>
        {
            $"Manifestes bornés · {profile.TaskManifests.Count} · schéma {AgentTaskManifest.CurrentSchemaVersion}",
            $"Diagnostics · {diagnostics.Count}",
        };
        foreach (var task in profile.TaskManifests)
        {
            details.Add($"{task.Key} · {task.Goal}");
            details.Add($"Responsable · {task.Owner} · portée {task.ScopePaths.Count} · interdits {task.ForbiddenPaths.Count} · acceptations {task.AcceptanceTests.Count} · dépendances {task.Dependencies.Count}");
        }
        details.AddRange(diagnostics.Select(diagnostic => $"{diagnostic.Code} · {diagnostic.Message}"));
        return new StudioUx8EStage(
            "ORF-BUILD-021", "Task manifests", true, ready,
            ready ? "VALIDES" : "À CORRIGER",
            "Les tâches machine lisibles sont validées sans lancer d’agent ni modifier leur portée.",
            details,
            [
                $"task_manifests={profile.TaskManifests.Count}",
                $"diagnostics={diagnostics.Count}",
                "agent_dispatch=none",
                "mutation=none",
            ]);
    }

    private static StudioUx8EStage InspectReleaseChannelsUx8E(Forge.Domain.Projects.ProjectDefinition project)
    {
        var distribution = project.Module.Distribution;
        var release = distribution.Release;
        var targets = distribution.TargetProfiles.Where(profile => profile.Enabled)
            .Select(profile => profile.Target).Order().ToArray();
        var compliance = targets.SelectMany(target => DistributionComplianceValidator.Validate(project, target)).ToArray();
        ModuleReleaseChannel? next = release.Channel switch
        {
            ModuleReleaseChannel.Development => ModuleReleaseChannel.Preview,
            ModuleReleaseChannel.Preview => ModuleReleaseChannel.Stable,
            _ => null,
        };
        ModuleReleasePromotionResult? preview = next is null ? null : ModuleReleasePromotionService.Preview(
            new ModuleReleasePromotionRequest(
                release.Id,
                release.Channel,
                next.Value,
                targets,
                release.Platforms,
                release.Signing.Mode == ModuleSigningMode.Required && release.Signing.TrustedKeyFingerprints.Count > 0,
                RemotePublisherConfigured: false,
                CompatibilityEvidence: []));
        var configured = Enum.IsDefined(release.Channel) && release.Platforms.Count > 0;
        var verdict = preview is null
            ? "CANAL STABLE · AUCUNE ÉTAPE SUPÉRIEURE"
            : preview.Allowed ? "PRÊT À PROMOUVOIR" : "CONFIGURÉ · PROMOTION BLOQUÉE";
        var details = new List<string>
        {
            $"Canal courant · {release.Channel} · étape suivante · {(next?.ToString() ?? "aucune")}",
            $"Cibles actives · {string.Join(" · ", targets)}",
            $"Plateformes · {string.Join(" · ", release.Platforms)}",
            $"Signature · {release.Signing.Mode} · clés fiables {release.Signing.TrustedKeyFingerprints.Count}",
            $"Delta · reprise {YesNoUx8E(release.Delta.AllowResume)} · déduplication {YesNoUx8E(release.Delta.DeduplicateByHash)} · rollback {YesNoUx8E(release.Delta.AllowRollback)}",
            "Adaptateur de publication distante · non représenté dans le projet · aucune publication tentée",
            preview is null
                ? "Prévisualisation · canal final atteint"
                : $"Prévisualisation · {preview.DiagnosticCode} · {preview.Message}",
            $"Diagnostics de conformité des cibles · {compliance.Length}",
        };
        return new StudioUx8EStage(
            "ORF-BUILD-024", "Release channels", true, configured, verdict,
            "La transition Development → Preview → Stable est évaluée sans bundle ni publication distante.",
            details,
            [
                $"channel={release.Channel}",
                $"next={next?.ToString() ?? "none"}",
                $"promotion_preview={preview?.DiagnosticCode ?? "final-channel"}",
                $"compliance_diagnostics={compliance.Length}",
                "publication=none",
            ]);
    }

    private static (StudioUx8EStage Stage, bool TemporaryWorkspaceReleased) InspectCrashArtifactsUx8E(
        Forge.Domain.Projects.ProjectDefinition project)
    {
        var root = Path.Combine(Path.GetTempPath(), Ux8ETemporaryPrefix + Guid.NewGuid().ToString("N"));
        StudioUx8EStage stage;
        var released = false;
        try
        {
            var policy = project.Module.RuntimePolicies.Extensions.OperationalReadiness.CrashArtifacts;
            var builder = new CrashArtifactBuilder(policy);
            var fingerprint = Convert.ToHexStringLower(SHA256.HashData(
                Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));
            const string secret = "ux8e-secret-value";
            var result = builder.Capture(new CrashArtifactRequest(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                ProjectJsonSerializer.CurrentFormatVersion,
                fingerprint,
                ["studio inspection started", $"token={secret}", "studio inspection completed"],
                root));
            using var archive = ZipFile.OpenRead(result.ArtifactPath);
            var logEntry = archive.GetEntry("sanitized.log");
            string sanitized;
            if (logEntry is null)
            {
                sanitized = string.Empty;
            }
            else
            {
                using var reader = new StreamReader(logEntry.Open());
                sanitized = reader.ReadToEnd();
            }
            var redacted = !sanitized.Contains(secret, StringComparison.Ordinal) &&
                           sanitized.Contains("[REDACTED]", StringComparison.Ordinal);
            var bounded = result.Bytes <= policy.MaxArtifactBytes && result.SanitizedLogLines <= policy.MaxLogLines;
            var ready = redacted && bounded && !result.DumpIncluded;
            stage = new StudioUx8EStage(
                "ORF-BUILD-025", "Crash artifacts", true, ready,
                ready ? "ARCHIVE TEMPORAIRE VALIDÉE" : "PREUVE INCOMPLÈTE",
                "Une archive bornée a été réellement créée, inspectée puis supprimée ; aucun dump natif n’a été demandé.",
                [
                    $"Logs assainis · {result.SanitizedLogLines} · secret remplacé · {YesNoUx8E(redacted)}",
                    $"Taille · {result.Bytes} octets / quota {policy.MaxArtifactBytes}",
                    $"Version et empreinte package incluses · {YesNoUx8E(policy.IncludeVersion && policy.IncludePackageFingerprint)}",
                    $"Adaptateur demandé · {policy.DumpAdapter} · adaptateur injecté · aucun · dump inclus · non",
                    "Archive de preuve · temporaire uniquement · aucune collecte de processus ou mémoire native",
                ],
                [
                    $"artifact={Path.GetFileName(result.ArtifactPath)}",
                    $"bytes={result.Bytes}",
                    "sanitization=passed",
                    "dump=none",
                    "source_mutation=none",
                ]);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or InvalidOperationException or InvalidDataException)
        {
            stage = new StudioUx8EStage(
                "ORF-BUILD-025", "Crash artifacts", true, false, "PREUVE ÉCHOUÉE",
                "La preuve temporaire a échoué ; aucun artefact n’est présenté comme valide.",
                [exception.Message],
                ["artifact=failed", "dump=none", "source_mutation=none"]);
        }
        finally
        {
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
                released = !Directory.Exists(root);
            }
            catch
            {
                released = false;
            }
        }
        if (!released)
        {
            stage = stage with
            {
                Ready = false,
                Verdict = "NETTOYAGE TEMPORAIRE ÉCHOUÉ",
                Details = stage.Details.Append("Le dossier temporaire n’a pas pu être supprimé.").ToArray(),
                Log = stage.Log.Append("temporary_workspace=released:false").ToArray(),
            };
        }
        else
        {
            stage = stage with { Log = stage.Log.Append("temporary_workspace=released:true").ToArray() };
        }
        return (stage, released);
    }

    private string? FindRepositoryRootUx8E()
    {
        var starts = new[] { Session?.RootDirectory, Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            .Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var start in starts)
        {
            var directory = new DirectoryInfo(Path.GetFullPath(start!));
            while (directory is not null)
            {
                if ((Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
                     File.Exists(Path.Combine(directory.FullName, ".git"))) &&
                    File.Exists(Path.Combine(directory.FullName, "README.md")))
                    return directory.FullName;
                directory = directory.Parent;
            }
        }
        return null;
    }

    private static int CountUx8ETemporaryDirectories() =>
        Directory.EnumerateDirectories(Path.GetTempPath(), Ux8ETemporaryPrefix + "*", SearchOption.TopDirectoryOnly)
            .Count(path => Guid.TryParseExact(Path.GetFileName(path)[Ux8ETemporaryPrefix.Length..], "N", out _));

    private static string YesNoUx8E(bool value) => value ? "oui" : "non";

    private static StudioUx8EReport FailedUx8E(StudioUx8ESelection selection, string message) =>
        new(false, false, true, selection.ModuleName, selection.RepositoryName, [], message);

    private static StudioUx8ESmokeResult FailedSmokeUx8E(string message) =>
        new(false, false, false, false, false, false, false, false, false, false, message);
}
