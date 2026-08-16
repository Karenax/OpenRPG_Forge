using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    internal bool PrepareUx0FEvidence(string mode)
    {
        SwitchPage("module", notify: false);
        return _projectPage.PrepareEventEvidence(mode);
    }

    internal bool RunUx0FSmoke()
    {
        var originalProject = _session.Project;
        var uiReady = PrepareUx0FEvidence("ux0-f-lifecycle") &&
                      _pageTitle.Text == "Événements du module" &&
                      _breadcrumb.Text == "Projet / Événements du module" &&
                      _projectPage.Ux0FEventCardCount == 5 &&
                      _projectPage.Ux0FEventBindingRowCount == 20 &&
                      _projectPage.EventScriptChoiceCount > 0 &&
                      _projectPage.ShowsHumanEventSurface;
        var uiTransaction = _projectPage.RunEventTransactionEvidence();
        var result = _session.RunUx0FContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) && !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX0F_{(valid ? "PASS" : "FAILED")} " +
            $"script_discovery={result.ScriptDiscovery.ToString().ToLowerInvariant()} " +
            $"missing_script_blocked={result.MissingScriptBlocked.ToString().ToLowerInvariant()} " +
            $"lifecycle_contexts={result.LifecycleContexts.ToString().ToLowerInvariant()} " +
            $"gameplay_groups={result.GameplayGroups.ToString().ToLowerInvariant()} " +
            $"server_authority={result.ServerAuthority.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"atomic_undo_redo={result.AtomicUndoRedo.ToString().ToLowerInvariant()} " +
            $"persistence_roundtrip={result.PersistenceRoundTrip.ToString().ToLowerInvariant()} " +
            $"neighbors_preserved={result.NeighborPoliciesPreserved.ToString().ToLowerInvariant()} " +
            $"human_no_uuid={result.HumanDiagnostics.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"cards={_projectPage.Ux0FEventCardCount} bindings={_projectPage.Ux0FEventBindingRowCount} " +
            $"project_restored={currentProjectUntouched.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
