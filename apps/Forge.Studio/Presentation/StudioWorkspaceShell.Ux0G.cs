using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    internal bool PrepareUx0GEvidence(string mode)
    {
        SwitchPage("module", notify: false);
        return _projectPage.PrepareEventEvidence(mode);
    }

    internal bool RunUx0GSmoke()
    {
        var originalProject = _session.Project;
        var uiReady = PrepareUx0GEvidence("ux0-g-heartbeat") &&
                      _pageTitle.Text == "Événements du module" &&
                      _breadcrumb.Text == "Projet / Événements du module" &&
                      _projectPage.EventCardCount >= 9 &&
                      _projectPage.EventBindingRowCount >= 26 &&
                      _projectPage.EventScriptChoiceCount > 0 &&
                      _projectPage.ShowsHumanEventSurface;
        var uiTransaction = _projectPage.RunUx0GEventTransactionEvidence();
        var result = _session.RunUx0GContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) && !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX0G_{(valid ? "PASS" : "FAILED")} " +
            $"equipment_progression={result.EquipmentAndProgression.ToString().ToLowerInvariant()} " +
            $"rest={result.RestLifecycle.ToString().ToLowerInvariant()} " +
            $"heartbeat={result.GlobalHeartbeat.ToString().ToLowerInvariant()} " +
            $"user_event={result.UserEvent.ToString().ToLowerInvariant()} " +
            $"runtime_dispatch={result.RuntimeDispatch.ToString().ToLowerInvariant()} " +
            $"missing_script_blocked={result.MissingScriptBlocked.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"atomic_undo_redo={result.AtomicUndoRedo.ToString().ToLowerInvariant()} " +
            $"persistence_roundtrip={result.PersistenceRoundTrip.ToString().ToLowerInvariant()} " +
            $"neighbors_preserved={result.NeighborPoliciesPreserved.ToString().ToLowerInvariant()} " +
            $"human_no_uuid={result.HumanDiagnostics.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"cards={_projectPage.EventCardCount} bindings={_projectPage.EventBindingRowCount} " +
            $"project_restored={currentProjectUntouched.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
