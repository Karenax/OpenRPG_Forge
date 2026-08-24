using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Build;
using Forge.Application.Commands;
using Forge.Domain.Build;
using Forge.Domain.Content;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private StudioUx8CGovernanceDraft? _approvedGovernanceUx8C;
    private WorkspaceBuildProfileRevision? _approvedGovernanceRevisionUx8C;
    private string? _approvedGovernanceSourceUx8C;
    private string? _approvedGovernanceDraftFingerprintUx8C;

    internal StudioUx8CGovernanceDraft? CurrentGovernanceDraftUx8C()
    {
        if (Project is null || Session is null || _buildProfileHistory is null) return null;
        var profile = _buildProfileHistory.State.Profile;
        var governance = profile.Governance;
        return new StudioUx8CGovernanceDraft(
            Project.Id.Value,
            Project.Module.DisplayName,
            _buildProfileHistory.State.Revision,
            BuildProfileFingerprintUx8C(profile),
            governance.Worktrees.BranchPrefix,
            governance.Worktrees.OwnershipManifestPath,
            governance.Worktrees.IntegrationChecks.ToArray(),
            governance.ContinuousIntegration.WorkflowPath,
            governance.ContinuousIntegration.ValidateSchemas,
            governance.ContinuousIntegration.BuildManagedCode,
            governance.ContinuousIntegration.BuildNativeCode,
            governance.ContinuousIntegration.CompileForgeScript,
            governance.ContinuousIntegration.RunTests,
            governance.ContinuousIntegration.PackReferenceModule,
            governance.ContinuousIntegration.VerifyLicenses,
            governance.ReferenceFixtures.CatalogPath,
            governance.ReferenceFixtures.Fixtures.ToArray());
    }

    internal StudioUx8CGovernancePreview PreviewGovernanceUx8C(StudioUx8CGovernanceDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelGovernanceApprovalUx8C();
        if (!TryValidateGovernanceSourceUx8C(draft, out var sourceError))
            return FailedGovernancePreviewUx8C(sourceError);

        WorkspaceBuildProfile candidate;
        try
        {
            candidate = BuildGovernanceCandidateUx8C(draft);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return FailedGovernancePreviewUx8C(exception.Message);
        }

        var command = new SetWorkspaceBuildProfileCommand(
            CommandId.From(Guid.NewGuid()), draft.BaseRevision, candidate, DryRun: true);
        var preview = _setWorkspaceBuildProfile.Handle(_buildProfileHistory!.State, command);
        var domainDiagnostics = WorkspaceBuildGovernanceValidator.Validate(candidate.Governance);
        if (!preview.Succeeded || domainDiagnostics.Count > 0)
        {
            var diagnostics = preview.Diagnostics.Select(item => HumanGovernanceDiagnosticUx8C(item.Code))
                .Concat(domainDiagnostics.Select(item => HumanGovernanceDiagnosticUx8C(item.Code)))
                .Distinct(StringComparer.Ordinal).ToArray();
            return new StudioUx8CGovernancePreview(false, false,
                diagnostics.FirstOrDefault() ?? "Le profil de gouvernance n’est pas valide.", string.Empty,
                diagnostics, false, false, false, false, "Preuve dépôt non exécutée.");
        }

        var evidence = EvaluateRepositoryGovernanceUx8C(candidate.Governance);
        var worktreeReady = !domainDiagnostics.Any(item => item.Code == WorkspaceBuildGovernanceValidator.WorktreeInvalidCode) &&
                            !evidence.Diagnostics.Any(item => item.Code ==
                                WorkspaceBuildGovernanceEvidenceValidator.WorktreeEvidenceMissingCode);
        var ciReady = !domainDiagnostics.Any(item => item.Code == WorkspaceBuildGovernanceValidator.CiInvalidCode) &&
                      !evidence.Diagnostics.Any(item => item.Code ==
                          WorkspaceBuildGovernanceEvidenceValidator.CiEvidenceMissingCode);
        var fixturesReady = !domainDiagnostics.Any(item => item.Code == WorkspaceBuildGovernanceValidator.FixturesInvalidCode) &&
                            !evidence.Diagnostics.Any(item => item.Code ==
                                WorkspaceBuildGovernanceEvidenceValidator.FixtureEvidenceMissingCode);

        _approvedGovernanceUx8C = FreezeGovernanceDraftUx8C(draft);
        _approvedGovernanceRevisionUx8C = draft.BaseRevision;
        _approvedGovernanceSourceUx8C = draft.SourceFingerprint;
        _approvedGovernanceDraftFingerprintUx8C = GovernanceDraftFingerprintUx8C(_approvedGovernanceUx8C);
        return new StudioUx8CGovernancePreview(
            true, true,
            "Aperçu sec validé · le profil n’a pas été écrit.",
            preview.Patch?.Summary ?? "Remplacer le profil de gouvernance du build.",
            evidence.Diagnostics.Select(item => HumanEvidenceDiagnosticUx8C(item.Code)).Distinct().ToArray(),
            worktreeReady, ciReady, fixturesReady, evidence.Available,
            evidence.Available
                ? $"Dépôt local inspecté · worktrees {(worktreeReady ? "conformes" : "à corriger")} · CI {(ciReady ? "conforme" : "à corriger")} · cinq fixtures {(fixturesReady ? "résolues" : "à corriger")}"
                : "Le module est hors d’un dépôt source : configuration validée, preuves CI et fixtures non exécutées.");
    }

    internal StudioUx8CActionResult ApplyApprovedGovernanceUx8C(StudioUx8CGovernanceDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (_buildProfileHistory is null || _approvedGovernanceUx8C is null ||
            _approvedGovernanceRevisionUx8C is null || _approvedGovernanceSourceUx8C is null ||
            _approvedGovernanceDraftFingerprintUx8C is null ||
            _buildProfileHistory.State.Revision != _approvedGovernanceRevisionUx8C ||
            !string.Equals(BuildProfileFingerprintUx8C(_buildProfileHistory.State.Profile),
                _approvedGovernanceSourceUx8C, StringComparison.Ordinal) ||
            !string.Equals(GovernanceDraftFingerprintUx8C(_approvedGovernanceUx8C),
                _approvedGovernanceDraftFingerprintUx8C, StringComparison.Ordinal) ||
            !string.Equals(GovernanceDraftFingerprintUx8C(draft),
                _approvedGovernanceDraftFingerprintUx8C, StringComparison.Ordinal))
        {
            CancelGovernanceApprovalUx8C();
            return new(false, "Application refusée · le profil ou le brouillon a changé depuis l’aperçu.");
        }

        var candidate = BuildGovernanceCandidateUx8C(_approvedGovernanceUx8C);
        var command = new SetWorkspaceBuildProfileCommand(
            CommandId.From(Guid.NewGuid()), _buildProfileHistory.State.Revision, candidate, DryRun: true);
        var dryRun = _setWorkspaceBuildProfile.Handle(_buildProfileHistory.State, command);
        if (!dryRun.Succeeded)
        {
            CancelGovernanceApprovalUx8C();
            return new(false, HumanGovernanceDiagnosticUx8C(dryRun.Diagnostics.FirstOrDefault()?.Code));
        }

        var previousFileMissing = _buildProfileFileMissing;
        var applied = _setWorkspaceBuildProfile.Handle(_buildProfileHistory.State, command with { DryRun = false });
        if (!applied.Succeeded)
        {
            CancelGovernanceApprovalUx8C();
            return new(false, HumanGovernanceDiagnosticUx8C(applied.Diagnostics.FirstOrDefault()?.Code));
        }

        _buildProfileHistory.Record(applied);
        _buildProfileUndoFileStates.Push(previousFileMissing);
        _buildProfileRedoFileStates.Clear();
        if (!TryPersistBuildProfile(applied.State.Profile, fileMissing: false, out var error))
        {
            _buildProfileHistory.Undo();
            _buildProfileUndoFileStates.Pop();
            CancelGovernanceApprovalUx8C();
            return new(false, error);
        }

        _persistedBuildProfile = applied.State.Profile;
        _buildProfileFileMissing = false;
        CancelGovernanceApprovalUx8C();
        return new(true, "Gouvernance enregistrée atomiquement · annulation et rétablissement disponibles.");
    }

    internal StudioUx8CActionResult UndoGovernanceUx8C()
    {
        CancelGovernanceApprovalUx8C();
        var result = UndoScriptCacheProfile();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx8CActionResult RedoGovernanceUx8C()
    {
        CancelGovernanceApprovalUx8C();
        var result = RedoScriptCacheProfile();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx8CActionResult SaveGovernanceUx8C()
    {
        CancelGovernanceApprovalUx8C();
        if (_buildProfileHistory is null)
            return new(false, "Aucun profil de build n’est ouvert.");
        return TryPersistBuildProfile(_buildProfileHistory.State.Profile, fileMissing: false, out var error)
            ? new(true, "Profil de gouvernance enregistré.")
            : new(false, error);
    }

    internal StudioUx8CMergePreview PreviewAssistedMergeUx8C(
        string oursLabel,
        string theirsLabel,
        ContentMergePolicy policy)
    {
        if (Project is null)
            return FailedMergeUx8C(policy, "Ouvrez un module avant de préparer un merge.");
        if (string.IsNullOrWhiteSpace(oursLabel) || string.IsNullOrWhiteSpace(theirsLabel) ||
            string.Equals(oursLabel.Trim(), theirsLabel.Trim(), StringComparison.Ordinal))
            return FailedMergeUx8C(policy, "Les variantes doivent porter deux valeurs humaines différentes.");

        try
        {
            var project = Project;
            var area = project.Areas.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).First();
            var tableId = DataTableId.From(project.Id.Value);
            var rowId = DataRowId.From(area.Id.Value);
            var column = new DataColumnDefinition(DataColumnId.From(StableUx8CGuid("column:label")),
                "label", "Libellé", DataColumnKind.Text, required: true);
            DataTableDefinition Table(string label, string provenance) => new(
                tableId, "module-labels", "Libellés du module", [column],
                [new DataRowDefinition(rowId,
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["label"] = label }, provenance)],
                provenance: provenance);
            var tableMerge = DataTableThreeWayMerger.Merge(
                Table(area.DisplayName, "source"), Table(oursLabel.Trim(), "ours"),
                Table(theirsLabel.Trim(), "theirs"), policy);

            var rootId = project.Module.Id.Value;
            var targetId = area.Id.Value;
            var edgeId = StableUx8CGuid($"edge:{rootId}:{targetId}");
            UuidGraphDocument Graph(string label, string provenance) => new(
                project.Id.Value, 1, provenance,
                [
                    new UuidGraphNode(rootId, "module",
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["label"] = project.Module.DisplayName,
                        }, "project"),
                    new UuidGraphNode(targetId, "area",
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["label"] = label,
                            ["slug"] = area.Slug,
                        }, provenance),
                ],
                [new UuidGraphEdge(edgeId, rootId, targetId, "contains",
                    new Dictionary<string, string>(StringComparer.Ordinal), "project")]);
            var graphMerge = UuidGraphThreeWayMerger.Merge(
                Graph(area.DisplayName, "source"), Graph(oursLabel.Trim(), "ours"),
                Graph(theirsLabel.Trim(), "theirs"), policy);
            var conflicts = tableMerge.Conflicts.Select(_ =>
                    $"Liste · « {area.DisplayName} » diffère dans les deux variantes.")
                .Concat(graphMerge.Conflicts.Select(_ =>
                    $"Graphe · la zone « {area.DisplayName} » diffère dans les deux variantes."))
                .ToArray();
            var labels = tableMerge.Merged.Rows.Select(row => row.Values["label"]).ToArray();
            var preservedBoth = policy == ContentMergePolicy.PreserveBoth && labels.Length == 2 &&
                                graphMerge.Merged.Nodes.Count == 3 && graphMerge.Merged.Edges.Count == 2;
            return new StudioUx8CMergePreview(
                true, policy, project.Module.DisplayName, area.DisplayName,
                tableMerge.Conflicts.Count, graphMerge.Conflicts.Count,
                tableMerge.Merged.Rows.Count, graphMerge.Merged.Nodes.Count, graphMerge.Merged.Edges.Count,
                preservedBoth, conflicts,
                [
                    $"Résolution · {HumanMergePolicyUx8C(policy)}",
                    $"Liste fusionnée · {string.Join(" + ", labels.Select(label => $"« {label} »"))}",
                    $"Graphe fusionné · {graphMerge.Merged.Nodes.Count} nœud(s) · {graphMerge.Merged.Edges.Count} relation(s)",
                    policy == ContentMergePolicy.PreserveBoth
                        ? "Les deux variantes reçoivent des identités déterministes et les relations sont remappées."
                        : "La variante choisie reste explicite ; aucune écriture n’est effectuée.",
                ],
                "Merge calculé en mémoire · le module ouvert est inchangé.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return FailedMergeUx8C(policy, "Merge refusé · " + exception.Message);
        }
    }

    internal StudioUx8CApprovalSnapshot CaptureGovernanceApprovalUx8C() => new(
        _approvedGovernanceUx8C,
        _approvedGovernanceRevisionUx8C,
        _approvedGovernanceSourceUx8C,
        _approvedGovernanceDraftFingerprintUx8C);

    internal void RestoreGovernanceApprovalUx8C(StudioUx8CApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedGovernanceUx8C = snapshot.Draft;
        _approvedGovernanceRevisionUx8C = snapshot.Revision;
        _approvedGovernanceSourceUx8C = snapshot.SourceFingerprint;
        _approvedGovernanceDraftFingerprintUx8C = snapshot.DraftFingerprint;
    }

    internal void CloseUx8C() => CancelGovernanceApprovalUx8C();

    internal StudioUx8CSmokeResult RunUx8CContractSmoke()
    {
        if (Project is null || History is null || Session is null)
            return FailedSmokeUx8C("Aucun module source n’est ouvert.");
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceApproval = CaptureGovernanceApprovalUx8C();
        var sourceRoot = Session.RootDirectory;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux8-c", Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var released = false;
        try
        {
            var created = ProjectSession.Create(root, sourceProject);
            if (!created.Succeeded || created.Session is null || !CopyWorkspaceFilesUx7D(sourceRoot, root))
                return FailedSmokeUx8C("La copie de preuve n’a pas pu être créée.");
            var sourceProfile = WorkspaceBuildProfileStore.Load(sourceRoot);
            if (!sourceProfile.Succeeded || !WorkspaceBuildProfileStore.Save(root, sourceProfile.Profile).Succeeded ||
                !editing.Open(root).Succeeded)
                return FailedSmokeUx8C("Le profil de preuve n’a pas pu être ouvert.");

            var mergeManual = editing.PreviewAssistedMergeUx8C(
                "Clairière · équipe locale", "Clairière · contribution reçue", ContentMergePolicy.Manual);
            var mergeBoth = editing.PreviewAssistedMergeUx8C(
                "Clairière · équipe locale", "Clairière · contribution reçue", ContentMergePolicy.PreserveBoth);
            var mergeConflict = mergeManual.Succeeded && mergeManual.TableConflicts == 1 &&
                                mergeManual.GraphConflicts == 1;
            var preserveBoth = mergeBoth.Succeeded && mergeBoth.PreservedBoth &&
                               mergeBoth.MergedRows == 2 && mergeBoth.MergedNodes == 3 &&
                               mergeBoth.MergedEdges == 2;

            var originalProfile = editing.BuildProfile;
            var neighborsBefore = BuildProfileNeighborsFingerprintUx8C(originalProfile);
            var draft = editing.CurrentGovernanceDraftUx8C();
            if (draft is null) return FailedSmokeUx8C("Le brouillon de gouvernance est indisponible.");
            var preview = editing.PreviewGovernanceUx8C(draft);
            var worktree = preview.Succeeded && preview.WorktreeReady &&
                           draft.IntegrationChecks.Order(StringComparer.Ordinal).SequenceEqual(
                               new[] { "backlog", "graph", "tracker", "verify" }, StringComparer.Ordinal);
            var ci = preview.Succeeded && preview.CiReady && draft.ValidateSchemas && draft.BuildManagedCode &&
                     draft.BuildNativeCode && draft.CompileForgeScript && draft.RunTests &&
                     draft.PackReferenceModule && draft.VerifyLicenses;
            var fixtures = preview.Succeeded && preview.FixturesReady && draft.Fixtures.Count == 5 &&
                           draft.Fixtures.Select(item => item.Kind).Distinct().Count() ==
                           Enum.GetValues<ReferenceFixtureKind>().Length;
            var dryRun = preview.CanApply && editing.BuildProfile == originalProfile;
            var staleGuard = !editing.ApplyApprovedGovernanceUx8C(draft with
            {
                BranchPrefix = draft.BranchPrefix + "changed/",
            }).Succeeded;
            preview = editing.PreviewGovernanceUx8C(draft);
            var applied = preview.CanApply && editing.ApplyApprovedGovernanceUx8C(draft).Succeeded;
            var undo = applied && editing.UndoGovernanceUx8C().Succeeded;
            var redo = undo && editing.RedoGovernanceUx8C().Succeeded;
            var saved = redo && editing.SaveGovernanceUx8C().Succeeded;
            var neighborsPreserved = saved &&
                                     BuildProfileNeighborsFingerprintUx8C(editing.BuildProfile) == neighborsBefore;
            editing.Close();
            var opened = reopened.Open(root);
            var persistence = saved && opened.Succeeded &&
                              GovernanceFingerprintUx8C(reopened.BuildProfile.Governance) ==
                              GovernanceFingerprintUx8C(originalProfile.Governance) &&
                              File.Exists(Path.Combine(root, WorkspaceBuildProfileStore.RelativePath));
            var sourceUnchanged = ReferenceEquals(sourceProject, Project) && ReferenceEquals(sourceHistory, History) &&
                                  History?.State.Revision == sourceRevision &&
                                  (History?.Journal ?? []).SequenceEqual(sourceJournal) && IsDirty == sourceDirty &&
                                  ActiveAreaId == sourceArea && SelectedEntityIds.SequenceEqual(sourceSelection) &&
                                  CaptureGovernanceApprovalUx8C() == sourceApproval;
            var fourRoutes = mergeConflict && worktree && ci && fixtures;
            var succeeded = fourRoutes && preserveBoth && dryRun && staleGuard && applied && undo && redo &&
                            persistence && neighborsPreserved && sourceUnchanged;
            return new StudioUx8CSmokeResult(
                succeeded, fourRoutes, mergeConflict, preserveBoth, worktree, ci, fixtures, dryRun,
                staleGuard, applied && undo && redo, persistence, neighborsPreserved, sourceUnchanged, true,
                succeeded
                    ? "UX8-C · merge et gouvernance validés dans une copie temporaire."
                    : "UX8-C · une preuve de merge ou de gouvernance a échoué.");
        }
        finally
        {
            editing.Close();
            reopened.Close();
            if (Directory.Exists(root))
            {
                try
                {
                    Directory.Delete(root, recursive: true);
                    released = true;
                }
                catch (IOException)
                {
                    released = false;
                }
                catch (UnauthorizedAccessException)
                {
                    released = false;
                }
            }
            else
            {
                released = true;
            }
            _lastUx8CTemporaryReleased = released;
        }
    }

    private bool _lastUx8CTemporaryReleased = true;
    internal bool LastUx8CTemporaryReleased => _lastUx8CTemporaryReleased;

    private bool TryValidateGovernanceSourceUx8C(StudioUx8CGovernanceDraft draft, out string error)
    {
        if (Project is null || _buildProfileHistory is null || draft.ProjectId != Project.Id.Value ||
            draft.BaseRevision != _buildProfileHistory.State.Revision ||
            !string.Equals(draft.SourceFingerprint,
                BuildProfileFingerprintUx8C(_buildProfileHistory.State.Profile), StringComparison.Ordinal))
        {
            error = "Le profil a changé depuis l’ouverture du formulaire. Rechargez ses valeurs.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private WorkspaceBuildProfile BuildGovernanceCandidateUx8C(StudioUx8CGovernanceDraft draft)
    {
        var source = _buildProfileHistory?.State.Profile ??
                     throw new InvalidOperationException("Aucun profil de build n’est ouvert.");
        var current = source.Governance;
        var governance = new WorkspaceBuildGovernanceProfile(
            current.Id,
            current.SchemaVersion,
            current.Provenance,
            new WorktreeAgentPolicy(
                true, true, true, draft.BranchPrefix.Trim(), draft.OwnershipManifestPath.Trim(),
                draft.IntegrationChecks.Select(item => item.Trim()).ToArray()),
            new ContinuousIntegrationPolicy(
                draft.WorkflowPath.Trim(), draft.ValidateSchemas, draft.BuildManagedCode,
                draft.BuildNativeCode, draft.CompileForgeScript, draft.RunTests,
                draft.PackReferenceModule, draft.VerifyLicenses),
            new ReferenceFixtureCatalogPolicy(
                draft.FixtureCatalogPath.Trim(), draft.Fixtures.ToArray()),
            current.Versioning,
            current.ArchitectureDecisions);
        return new WorkspaceBuildProfile(
            source.Id, source.SchemaVersion, "studio:ux8-c-build-governance",
            source.RequireGitRepository, source.TextDocuments, source.ExplicitGeneratedFiles,
            source.ReproducibleAssets, source.Layout, source.CliOperations, source.StableOrdering,
            source.NormalizedTimestamps, source.LockedDependencies, source.ContentHashes,
            source.VerifyReproducibility, source.IncrementalBuild, source.CachePath,
            source.RebuildAffectedOnly, governance, source.Collaboration, source.Compatibility);
    }

    private (bool Available, IReadOnlyList<DomainDiagnostic> Diagnostics) EvaluateRepositoryGovernanceUx8C(
        WorkspaceBuildGovernanceProfile governance)
    {
        var root = FindRepositoryRootUx8C();
        return root is null
            ? (false, Array.Empty<DomainDiagnostic>())
            : (true, WorkspaceBuildGovernanceEvidenceValidator.Validate(root, governance));
    }

    private string? FindRepositoryRootUx8C()
    {
        var starts = new[]
        {
            Session?.RootDirectory,
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
        }.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var start in starts)
        {
            var directory = new DirectoryInfo(Path.GetFullPath(start!));
            while (directory is not null)
            {
                if ((Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
                     File.Exists(Path.Combine(directory.FullName, ".git"))) &&
                    File.Exists(Path.Combine(directory.FullName, "tests", "fixtures", "build",
                        "reference-fixtures.json")))
                    return directory.FullName;
                directory = directory.Parent;
            }
        }
        return null;
    }

    private static StudioUx8CGovernanceDraft FreezeGovernanceDraftUx8C(StudioUx8CGovernanceDraft draft) =>
        draft with
        {
            IntegrationChecks = draft.IntegrationChecks.ToArray(),
            Fixtures = draft.Fixtures.ToArray(),
        };

    private static string BuildProfileFingerprintUx8C(WorkspaceBuildProfile profile) =>
        HashUx8C(JsonSerializer.Serialize(profile));

    private static string GovernanceFingerprintUx8C(WorkspaceBuildGovernanceProfile governance) =>
        HashUx8C(JsonSerializer.Serialize(governance));

    private static string BuildProfileNeighborsFingerprintUx8C(WorkspaceBuildProfile profile) => HashUx8C(
        JsonSerializer.Serialize(new
        {
            profile.Id,
            profile.SchemaVersion,
            profile.RequireGitRepository,
            profile.TextDocuments,
            profile.ExplicitGeneratedFiles,
            profile.ReproducibleAssets,
            profile.Layout,
            profile.CliOperations,
            profile.StableOrdering,
            profile.NormalizedTimestamps,
            profile.LockedDependencies,
            profile.ContentHashes,
            profile.VerifyReproducibility,
            profile.IncrementalBuild,
            profile.CachePath,
            profile.RebuildAffectedOnly,
            profile.Collaboration,
            profile.Compatibility,
            profile.Governance.Versioning,
            profile.Governance.ArchitectureDecisions,
        }));

    internal static string GovernanceDraftFingerprintUx8C(StudioUx8CGovernanceDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(draft.ProjectId.ToByteArray());
        writer.Write(draft.BaseRevision.Value);
        WriteUx8CString(writer, draft.SourceFingerprint);
        WriteUx8CString(writer, draft.BranchPrefix);
        WriteUx8CString(writer, draft.OwnershipManifestPath);
        WriteUx8CStringList(writer, draft.IntegrationChecks);
        WriteUx8CString(writer, draft.WorkflowPath);
        writer.Write(draft.ValidateSchemas);
        writer.Write(draft.BuildManagedCode);
        writer.Write(draft.BuildNativeCode);
        writer.Write(draft.CompileForgeScript);
        writer.Write(draft.RunTests);
        writer.Write(draft.PackReferenceModule);
        writer.Write(draft.VerifyLicenses);
        WriteUx8CString(writer, draft.FixtureCatalogPath);
        writer.Write(draft.Fixtures.Count);
        foreach (var fixture in draft.Fixtures.OrderBy(item => item.Kind))
        {
            writer.Write(fixture.Id.ToByteArray());
            writer.Write((int)fixture.Kind);
            WriteUx8CString(writer, fixture.Name);
            WriteUx8CString(writer, fixture.RelativePath);
            WriteUx8CString(writer, fixture.ExpectedOutcome);
            WriteUx8CString(writer, fixture.Provenance);
        }
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void WriteUx8CString(BinaryWriter writer, string? value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static void WriteUx8CStringList(BinaryWriter writer, IReadOnlyList<string> values)
    {
        writer.Write(values.Count);
        foreach (var value in values) WriteUx8CString(writer, value);
    }

    private static string HashUx8C(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static Guid StableUx8CGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("ux8-c:" + value));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guid);
        guid[6] = (byte)((guid[6] & 0x0f) | 0x40);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }

    private void CancelGovernanceApprovalUx8C()
    {
        _approvedGovernanceUx8C = null;
        _approvedGovernanceRevisionUx8C = null;
        _approvedGovernanceSourceUx8C = null;
        _approvedGovernanceDraftFingerprintUx8C = null;
    }

    private static StudioUx8CGovernancePreview FailedGovernancePreviewUx8C(string message) => new(
        false, false, message, string.Empty, [message], false, false, false, false,
        "Preuve dépôt non exécutée.");

    private static StudioUx8CMergePreview FailedMergeUx8C(ContentMergePolicy policy, string message) => new(
        false, policy, string.Empty, string.Empty, 0, 0, 0, 0, 0, false, [], [], message);

    private static StudioUx8CSmokeResult FailedSmokeUx8C(string message) => new(
        false, false, false, false, false, false, false, false, false, false, false, false, false,
        false, message);

    private static string HumanMergePolicyUx8C(ContentMergePolicy policy) => policy switch
    {
        ContentMergePolicy.Manual => "Conserver notre variante et signaler le conflit",
        ContentMergePolicy.PreferOurs => "Préférer notre variante",
        ContentMergePolicy.PreferTheirs => "Préférer la variante reçue",
        ContentMergePolicy.PreserveBoth => "Préserver les deux variantes",
        _ => "Politique inconnue",
    };

    private static string HumanGovernanceDiagnosticUx8C(string? code) => code switch
    {
        WorkspaceBuildGovernanceValidator.WorktreeInvalidCode =>
            "La politique worktree exige une branche dédiée, une propriété exclusive, des périmètres déclarés et les quatre contrôles d’intégration.",
        WorkspaceBuildGovernanceValidator.CiInvalidCode =>
            "La CI doit couvrir schémas, code managé, natif, ForgeScript, tests, module de référence et licences.",
        WorkspaceBuildGovernanceValidator.FixturesInvalidCode =>
            "Le catalogue doit contenir exactement les cinq fixtures Tiny, Vertical, Stress, Malicious et Migration.",
        SetWorkspaceBuildProfileHandler.RevisionConflictCode =>
            "Le profil a changé depuis l’aperçu. Rechargez ses valeurs.",
        _ => "Le profil de gouvernance est invalide.",
    };

    private static string HumanEvidenceDiagnosticUx8C(string code) => code switch
    {
        WorkspaceBuildGovernanceEvidenceValidator.WorktreeEvidenceMissingCode =>
            "La politique worktree du dépôt ne correspond pas au profil.",
        WorkspaceBuildGovernanceEvidenceValidator.CiEvidenceMissingCode =>
            "Le workflow CI configuré est absent ou incomplet.",
        WorkspaceBuildGovernanceEvidenceValidator.FixtureEvidenceMissingCode =>
            "Le catalogue de fixtures est absent ou une référence ne se résout pas.",
        WorkspaceBuildGovernanceEvidenceValidator.VersioningEvidenceMissingCode =>
            "La preuve de versionnement voisine est à corriger hors de ce lot.",
        WorkspaceBuildGovernanceEvidenceValidator.AdrEvidenceMissingCode =>
            "La preuve ADR voisine est à corriger hors de ce lot.",
        _ => "Une preuve de dépôt n’est pas conforme.",
    };
}
