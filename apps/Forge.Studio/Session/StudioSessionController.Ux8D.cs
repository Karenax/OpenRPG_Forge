using Forge.Domain.Build;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private static readonly string[] StandardLayoutKeysUx8D =
        ["assets", "cache", "data", "docs", "localization", "packages", "scripts", "tests"];

    internal StudioUx8DSelection? CurrentRepositoryReadinessUx8D()
    {
        if (Project is null || Session is null || _buildProfileHistory is null) return null;
        var repositoryRoot = FindRepositoryRootUx8D();
        return new StudioUx8DSelection(
            Project.Id.Value,
            Project.Module.DisplayName,
            repositoryRoot is null ? "Dépôt source indisponible" : new DirectoryInfo(repositoryRoot).Name,
            repositoryRoot is not null,
            _buildProfileHistory.State.Profile.Provenance);
    }

    internal StudioUx8DReport RunRepositoryReadinessUx8D(
        StudioUx8DSelection selection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (Project is null || Session is null || _buildProfileHistory is null ||
            selection.ProjectId != Project.Id.Value)
            return FailedUx8D(selection, "La sélection ne correspond plus au module ouvert.");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var profile = _buildProfileHistory.State.Profile;
            var repositoryRoot = FindRepositoryRootUx8D();
            var repositoryName = repositoryRoot is null
                ? "Dépôt source indisponible"
                : new DirectoryInfo(repositoryRoot).Name;
            var stages = new List<StudioUx8DStage>(4);

            stages.Add(InspectCanonicalRepositoryUx8D(profile, repositoryRoot));
            cancellationToken.ThrowIfCancellationRequested();
            stages.Add(InspectStandardLayoutUx8D(profile, repositoryRoot));
            cancellationToken.ThrowIfCancellationRequested();
            stages.Add(InspectVersioningUx8D(profile, repositoryRoot));
            cancellationToken.ThrowIfCancellationRequested();
            stages.Add(InspectArchitectureDecisionsUx8D(profile, repositoryRoot));
            cancellationToken.ThrowIfCancellationRequested();

            var configured = stages.Count(stage => stage.Configured);
            return new StudioUx8DReport(
                true,
                false,
                selection.ModuleName,
                repositoryName,
                stages,
                $"Inspection terminée · {configured}/4 contrats configurés · source et Git inchangés.");
        }
        catch (OperationCanceledException)
        {
            return new StudioUx8DReport(
                true,
                true,
                selection.ModuleName,
                selection.RepositoryName,
                [],
                "Inspection annulée · aucune écriture n’a été effectuée.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or InvalidOperationException)
        {
            return FailedUx8D(selection, "Inspection interrompue · " + exception.Message);
        }
    }

    internal StudioUx8DSmokeResult RunUx8DContractSmoke()
    {
        if (Project is null || History is null || Session is null)
            return FailedSmokeUx8D("Aucun module source n’est ouvert.");
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var selection = CurrentRepositoryReadinessUx8D();
        if (selection is null) return FailedSmokeUx8D("La sélection UX8-D est indisponible.");

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = RunRepositoryReadinessUx8D(selection, cancellation.Token);
        var report = RunRepositoryReadinessUx8D(selection, CancellationToken.None);
        var fourRoutes = report.Succeeded && !report.Cancelled && report.Stages.Count == 4 &&
                         report.Stages.Select(stage => stage.Route).SequenceEqual(
                             new[] { "ORF-BUILD-001", "ORF-BUILD-002", "ORF-BUILD-018", "ORF-BUILD-019" },
                             StringComparer.Ordinal);
        bool Ready(string route) => report.Stages.Single(stage => stage.Route == route).Configured;
        var canonicalRepository = fourRoutes && Ready("ORF-BUILD-001");
        var standardLayout = fourRoutes && Ready("ORF-BUILD-002") &&
                             report.Stages[1].Details.Any(detail => detail.Contains("huit", StringComparison.OrdinalIgnoreCase));
        var semanticVersioning = fourRoutes && Ready("ORF-BUILD-018") &&
                                 report.Stages[2].Details.Any(detail => detail.Contains("schéma", StringComparison.OrdinalIgnoreCase));
        var architectureDecisions = fourRoutes && Ready("ORF-BUILD-019") &&
                                    report.Stages[3].Details.Any(detail => detail.Contains("décision", StringComparison.OrdinalIgnoreCase));
        var cancellationReady = cancelled.Succeeded && cancelled.Cancelled && cancelled.Stages.Count == 0;
        var logs = report.Stages.All(stage => stage.Log.Count > 0) &&
                   report.Stages.SelectMany(stage => stage.Log).All(line => !Path.IsPathRooted(line));
        var sourceUnchanged = ReferenceEquals(sourceProject, Project) && ReferenceEquals(sourceHistory, History) &&
                              History?.State.Revision == sourceRevision &&
                              (History?.Journal ?? []).SequenceEqual(sourceJournal) && IsDirty == sourceDirty &&
                              ActiveAreaId == sourceArea && SelectedEntityIds.SequenceEqual(sourceSelection) &&
                              SelectedEntityId == sourceSelected;
        var succeeded = fourRoutes && canonicalRepository && standardLayout && semanticVersioning &&
                        architectureDecisions && cancellationReady && logs && sourceUnchanged;
        return new StudioUx8DSmokeResult(
            succeeded,
            fourRoutes,
            canonicalRepository,
            standardLayout,
            semanticVersioning,
            architectureDecisions,
            cancellationReady,
            logs,
            sourceUnchanged,
            succeeded
                ? "UX8-D · dépôt, structure, versions et ADR inspectés sans mutation."
                : "UX8-D · une preuve de préparation du dépôt a échoué.");
    }

    internal void CloseUx8D()
    {
        // UX8-D est strictement en lecture seule et ne conserve aucune approbation.
    }

    private StudioUx8DStage InspectCanonicalRepositoryUx8D(
        WorkspaceBuildProfile profile,
        string? repositoryRoot)
    {
        var repositoryAvailable = repositoryRoot is not null;
        var git = repositoryAvailable && (Directory.Exists(Path.Combine(repositoryRoot!, ".git")) ||
                                          File.Exists(Path.Combine(repositoryRoot!, ".git")));
        var ignore = repositoryAvailable && File.Exists(Path.Combine(repositoryRoot!, ".gitignore"));
        var readme = repositoryAvailable && File.Exists(Path.Combine(repositoryRoot!, "README.md"));
        var configured = profile.RequireGitRepository && profile.TextDocuments &&
                         profile.ExplicitGeneratedFiles && profile.ReproducibleAssets;
        return new StudioUx8DStage(
            "ORF-BUILD-001",
            "Dépôt Git canonique",
            configured,
            repositoryAvailable,
            configured && git && ignore && readme ? "CONFORME" : configured ? "CONFIGURÉ · PREUVE PARTIELLE" : "À CORRIGER",
            repositoryAvailable
                ? "Dépôt local inspecté sans commande Git ni écriture."
                : "Profil inspecté ; dépôt source indisponible depuis ce workspace.",
            [
                $"Git requis · {YesNoUx8D(profile.RequireGitRepository)} · textes versionnables · {YesNoUx8D(profile.TextDocuments)}",
                $"Générés explicites · {YesNoUx8D(profile.ExplicitGeneratedFiles)} · assets reproductibles · {YesNoUx8D(profile.ReproducibleAssets)}",
                $"Preuves locales · dépôt {PresentUx8D(git)} · ignore {PresentUx8D(ignore)} · guide {PresentUx8D(readme)}",
            ],
            [
                "inspection=.git",
                "inspection=.gitignore",
                "inspection=README.md",
                "mutation_git=aucune",
            ]);
    }

    private StudioUx8DStage InspectStandardLayoutUx8D(
        WorkspaceBuildProfile profile,
        string? repositoryRoot)
    {
        var keys = profile.Layout.Keys.Order(StringComparer.Ordinal).ToArray();
        var configured = keys.SequenceEqual(StandardLayoutKeysUx8D, StringComparer.Ordinal) &&
                         profile.Layout.Values.All(IsSafeRelativeUx8D) &&
                         profile.Layout.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() == profile.Layout.Count;
        var existing = repositoryRoot is null
            ? 0
            : profile.Layout.Values.Count(path => Directory.Exists(Path.Combine(repositoryRoot, path)));
        var details = profile.Layout.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => $"{HumanLayoutUx8D(item.Key)} · {item.Value} · " +
                            (repositoryRoot is not null && Directory.Exists(Path.Combine(repositoryRoot, item.Value))
                                ? "présent dans le checkout"
                                : "emplacement configuré"))
            .Prepend($"Huit emplacements standards uniques · {profile.Layout.Count}/8 configurés · {existing} matérialisés ici")
            .ToArray();
        return new StudioUx8DStage(
            "ORF-BUILD-002",
            "Structure de projet standard",
            configured,
            repositoryRoot is not null,
            configured ? "CONFORME" : "À CORRIGER",
            "La structure configurée est affichée sans inventer les dossiers absents.",
            details,
            profile.Layout.OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => $"layout.{item.Key}={item.Value}").ToArray());
    }

    private StudioUx8DStage InspectVersioningUx8D(
        WorkspaceBuildProfile profile,
        string? repositoryRoot)
    {
        var versioning = profile.Governance.Versioning;
        var diagnostics = WorkspaceBuildGovernanceValidator.Validate(profile.Governance);
        var configured = diagnostics.All(item => item.Code != WorkspaceBuildGovernanceValidator.VersioningInvalidCode);
        var changelog = repositoryRoot is not null &&
                        File.Exists(Path.Combine(repositoryRoot, versioning.ChangelogPath));
        return new StudioUx8DStage(
            "ORF-BUILD-018",
            "Versioning sémantique",
            configured,
            repositoryRoot is not null,
            configured && changelog ? "CONFORME" : configured ? "CONFIGURÉ · PREUVE PARTIELLE" : "À CORRIGER",
            "Core, kits, packages et schémas conservent des versions distinctes et inspectables.",
            [
                $"Core · {versioning.CoreVersion} · package · {versioning.PackageVersion}",
                $"Kits · {string.Join(" · ", versioning.KitVersions.OrderBy(item => item.Key).Select(item => $"{item.Key} {item.Value}"))}",
                $"Versions de schéma · {string.Join(" · ", versioning.SchemaVersions.OrderBy(item => item.Key).Select(item => $"{item.Key} {item.Value}"))}",
                $"Compatibilité · {string.Join(" · ", versioning.CompatibilityRules)}",
                $"Changelog · {versioning.ChangelogPath} · {PresentUx8D(changelog)}",
            ],
            [
                $"core={versioning.CoreVersion}",
                $"package={versioning.PackageVersion}",
                $"schemas={versioning.SchemaVersions.Count}",
                $"compatibility_rules={versioning.CompatibilityRules.Count}",
                $"changelog={versioning.ChangelogPath}",
            ]);
    }

    private StudioUx8DStage InspectArchitectureDecisionsUx8D(
        WorkspaceBuildProfile profile,
        string? repositoryRoot)
    {
        var decisions = profile.Governance.ArchitectureDecisions;
        var diagnostics = WorkspaceBuildGovernanceValidator.Validate(profile.Governance);
        var configured = diagnostics.All(item => item.Code != WorkspaceBuildGovernanceValidator.AdrInvalidCode);
        var directory = repositoryRoot is not null && Directory.Exists(Path.Combine(repositoryRoot, decisions.DirectoryPath));
        var template = repositoryRoot is not null && File.Exists(Path.Combine(repositoryRoot, decisions.TemplatePath));
        var count = directory
            ? Directory.EnumerateFiles(Path.Combine(repositoryRoot!, decisions.DirectoryPath), "*.md",
                    SearchOption.TopDirectoryOnly).Count()
            : 0;
        return new StudioUx8DStage(
            "ORF-BUILD-019",
            "Décisions d’architecture",
            configured,
            repositoryRoot is not null,
            configured && directory && template ? "CONFORME" : configured ? "CONFIGURÉ · PREUVE PARTIELLE" : "À CORRIGER",
            "Les ADR sont inventoriées avec statuts formels et règles de preuve, sans modifier la documentation.",
            [
                $"Dossier · {decisions.DirectoryPath} · {PresentUx8D(directory)} · {count} décision(s) Markdown",
                $"Modèle · {decisions.TemplatePath} · {PresentUx8D(template)}",
                $"Statuts · {string.Join(" · ", decisions.AllowedStatuses)}",
                $"Tests requis · {YesNoUx8D(decisions.RequireTestLinks)} · benchmarks performance · {YesNoUx8D(decisions.RequireBenchmarksForPerformanceDecisions)}",
                $"Remplacement par décision supersédante · {YesNoUx8D(decisions.ReplaceBySupersedingDecision)}",
            ],
            [
                $"adr_directory={decisions.DirectoryPath}",
                $"adr_template={decisions.TemplatePath}",
                $"adr_statuses={string.Join(',', decisions.AllowedStatuses)}",
                $"adr_documents={count}",
                "mutation_docs=aucune",
            ]);
    }

    private string? FindRepositoryRootUx8D()
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

    private static bool IsSafeRelativeUx8D(string path) =>
        !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) &&
        path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)
            .All(segment => segment is not "." and not "..");

    private static string HumanLayoutUx8D(string key) => key switch
    {
        "assets" => "Assets",
        "cache" => "Cache",
        "data" => "Données",
        "docs" => "Documentation",
        "localization" => "Localisation",
        "packages" => "Packages",
        "scripts" => "Scripts",
        "tests" => "Tests",
        _ => key,
    };

    private static string YesNoUx8D(bool value) => value ? "oui" : "non";
    private static string PresentUx8D(bool value) => value ? "présent" : "absent";

    private static StudioUx8DReport FailedUx8D(StudioUx8DSelection selection, string message) =>
        new(false, false, selection.ModuleName, selection.RepositoryName, [], message);

    private static StudioUx8DSmokeResult FailedSmokeUx8D(string message) =>
        new(false, false, false, false, false, false, false, false, false, message);
}
