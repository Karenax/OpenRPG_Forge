using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    internal bool PrepareUx0IEvidence(string mode)
    {
        SwitchPage("module", notify: false);
        return _projectPage.PrepareGlobalsEvidence(mode);
    }

    internal bool RunUx0ISmoke()
    {
        var originalProject = _session.Project;
        var originalCacheProfile = _session.BuildProfile;
        var uiReady = PrepareUx0IEvidence("ux0-i-variables") &&
                      _pageTitle.Text == "Données et guide joueur" &&
                      _breadcrumb.Text == "Projet / Données et guide joueur" &&
                      _projectPage.Ux0IVariableCount > 0 &&
                      _projectPage.Ux0IGuidePageCount == 8 &&
                      _projectPage.Ux0IScriptCount > 0 &&
                      _projectPage.ShowsHumanGlobalsSurface;
        var uiTransaction = _projectPage.RunUx0IGlobalsTransactionEvidence();
        var result = _session.RunUx0IContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      ReferenceEquals(originalCacheProfile, _session.BuildProfile) &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX0I_{(valid ? "PASS" : "FAILED")} " +
            $"variables={result.VariableScopes.ToString().ToLowerInvariant()} " +
            $"invalid_variable_blocked={result.InvalidVariableBlocked.ToString().ToLowerInvariant()} " +
            $"player_guide={result.PlayerGuide.ToString().ToLowerInvariant()} " +
            $"script_cache={result.ScriptCache.ToString().ToLowerInvariant()} " +
            $"cache_refresh={result.CacheInvalidation.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"atomic_undo_redo={result.AtomicUndoRedo.ToString().ToLowerInvariant()} " +
            $"persistence_roundtrip={result.PersistenceRoundTrip.ToString().ToLowerInvariant()} " +
            $"neighbors_preserved={result.NeighborDataPreserved.ToString().ToLowerInvariant()} " +
            $"human_no_uuid={result.HumanDiagnostics.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"guide_pages={_projectPage.Ux0IGuidePageCount} scripts={_projectPage.Ux0IScriptCount} " +
            $"project_restored={currentProjectUntouched.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
