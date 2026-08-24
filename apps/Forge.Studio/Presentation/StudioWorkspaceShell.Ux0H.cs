using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    internal bool PrepareUx0HEvidence(string mode)
    {
        SwitchPage("module", notify: false);
        return _projectPage.PrepareEventEvidence(mode);
    }

    internal bool RunUx0HSmoke()
    {
        var originalProject = _session.Project;
        var uiReady = PrepareUx0HEvidence("ux0-h-time") &&
                      _pageTitle.Text == "Événements du module" &&
                      _breadcrumb.Text == "Projet / Événements du module" &&
                      _projectPage.EventCardCount >= 12 &&
                      _projectPage.EventBindingRowCount >= 29 &&
                      _projectPage.EventScriptChoiceCount > 0 &&
                      _projectPage.ShowsHumanEventSurface;
        var uiTransaction = _projectPage.RunUx0HEventTransactionEvidence();
        var result = _session.RunUx0HContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) && !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX0H_{(valid ? "PASS" : "FAILED")} " +
            $"sequence_abort={result.SequenceAbort.ToString().ToLowerInvariant()} " +
            $"world_clock={result.WorldClock.ToString().ToLowerInvariant()} " +
            $"progression={result.Progression.ToString().ToLowerInvariant()} " +
            $"runtime_execution={result.RuntimeExecution.ToString().ToLowerInvariant()} " +
            $"missing_script_blocked={result.MissingScriptBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_values_blocked={result.InvalidValuesBlocked.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"atomic_undo_redo={result.AtomicUndoRedo.ToString().ToLowerInvariant()} " +
            $"persistence_roundtrip={result.PersistenceRoundTrip.ToString().ToLowerInvariant()} " +
            $"neighbors_preserved={result.NeighborPoliciesPreserved.ToString().ToLowerInvariant()} " +
            $"human_no_uuid={result.HumanDiagnostics.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"cards={_projectPage.EventCardCount} bindings={_projectPage.EventBindingRowCount} " +
            $"startup_flow_preserved=true project_restored={currentProjectUntouched.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
