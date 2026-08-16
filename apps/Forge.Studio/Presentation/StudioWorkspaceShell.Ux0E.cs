using Forge.Domain.Projects;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    internal bool PrepareUx0EEvidence(string mode)
    {
        SwitchPage("module", notify: false);
        return _projectPage.PrepareEvidence(mode);
    }

    internal bool RunUx0ESmoke()
    {
        var originalProject = _session.Project;
        var uiReady = PrepareUx0EEvidence("ux0-e-module") &&
                      _projectPage.LanguageCount > 0 &&
                      _projectPage.ShowsHumanIdentity;
        var uiTransaction = _projectPage.RunTransactionEvidence();
        var result = _session.RunUx0EContractSmoke();
        var coreShellWithoutArea = VerifyCoreShellWithoutArea();
        var customChoicesPreserved = VerifyCustomChoicesPreserved();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) && !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && coreShellWithoutArea &&
                    customChoicesPreserved && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX0E_{(valid ? "PASS" : "FAILED")} " +
            $"wizard_preview={result.WizardPreview.ToString().ToLowerInvariant()} " +
            $"invalid_rpg_blocked={result.InvalidRpgBlocked.ToString().ToLowerInvariant()} " +
            $"core_without_area={result.CoreWithoutArea.ToString().ToLowerInvariant()} " +
            $"core_shell={coreShellWithoutArea.ToString().ToLowerInvariant()} " +
            $"localized_metadata={result.LocalizedMetadata.ToString().ToLowerInvariant()} " +
            $"stable_identity={result.StableIdentity.ToString().ToLowerInvariant()} " +
            $"player_start={result.PlayerStart.ToString().ToLowerInvariant()} " +
            $"optional_waypoint={result.OptionalWaypoint.ToString().ToLowerInvariant()} " +
            $"custom_choices={customChoicesPreserved.ToString().ToLowerInvariant()} " +
            $"atomic_undo_redo={result.AtomicUndoRedo.ToString().ToLowerInvariant()} " +
            $"persistence_roundtrip={result.PersistenceRoundTrip.ToString().ToLowerInvariant()} " +
            $"human_no_uuid={result.HumanDiagnostics.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"project_restored={currentProjectUntouched.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    private bool VerifyCustomChoicesPreserved()
    {
        const string rulesProvider = "studio.custom.rules";
        const string gameMode = "survival";
        const string entryClass = "spellcaster";
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-custom-choices-" + Guid.NewGuid().ToString("N"));
        StudioWorkspaceShell? shell = null;
        var controller = new StudioSessionController();
        try
        {
            var draft = StudioNewProjectDraft.CreateDefault(root) with
            {
                DraftId = Guid.Parse("d0250000-0000-4000-8000-000000000300"),
            };
            var created = _session.CreateNewProject(draft);
            if (!created.Succeeded || created.WorkspaceDirectory is null ||
                !controller.Open(created.WorkspaceDirectory).Succeeded)
            {
                return false;
            }
            var custom = controller.CaptureProjectConfiguration() with
            {
                RulesProvider = rulesProvider,
                GameMode = gameMode,
                EntryClass = entryClass,
            };
            if (!controller.ApplyProjectConfiguration(custom).Succeeded)
            {
                return false;
            }
            shell = new StudioWorkspaceShell(controller, "project", "module") { Visible = false };
            AddChild(shell);
            var valid = shell._projectPage.PreservesSemanticChoices(rulesProvider, gameMode, entryClass);
            RemoveChild(shell);
            shell.Free();
            shell = null;
            return valid;
        }
        finally
        {
            if (shell is not null && GodotObject.IsInstanceValid(shell))
            {
                if (shell.GetParent() is not null) shell.GetParent().RemoveChild(shell);
                shell.Free();
            }
            controller.Close();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private bool VerifyCoreShellWithoutArea()
    {
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-core-shell-" + Guid.NewGuid().ToString("N"));
        StudioWorkspaceShell? shell = null;
        var controller = new StudioSessionController();
        try
        {
            var draft = StudioNewProjectDraft.CreateDefault(root) with
            {
                DraftId = Guid.Parse("d0250000-0000-4000-8000-000000000200"),
                Template = ProjectTemplateKind.CoreMenu,
                CreateInitialArea = false,
                AreaDisplayName = string.Empty,
                Kits = ["forge-core"],
                RulesProvider = "forge.core",
            };
            var created = _session.CreateNewProject(draft);
            if (!created.Succeeded || created.WorkspaceDirectory is null ||
                !controller.Open(created.WorkspaceDirectory).Succeeded)
            {
                return false;
            }
            shell = new StudioWorkspaceShell(controller, "project", "module") { Visible = false };
            AddChild(shell);
            var valid = controller.ActiveAreaId is null && controller.Project?.Areas.Count == 0 &&
                        shell.PageId == "module" && shell.WorldViewport.ViewAreaId is null &&
                        shell._projectPage.ShowsHumanIdentity;
            RemoveChild(shell);
            shell.Free();
            shell = null;
            return valid;
        }
        finally
        {
            if (shell is not null && GodotObject.IsInstanceValid(shell))
            {
                if (shell.GetParent() is not null) shell.GetParent().RemoveChild(shell);
                shell.Free();
            }
            controller.Close();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
