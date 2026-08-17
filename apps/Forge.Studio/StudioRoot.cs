using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Presentation;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio;

public sealed partial class StudioRoot : Control
{
    private readonly StudioSessionController _session = new();
    private StudioModuleHub? _hub;
    private StudioWorkspaceShell? _shell;
    private StudioNewProjectWizard? _newProjectWizard;
    private StudioUserState _userState = StudioUserState.Default;
    private string _defaultWorkspace = string.Empty;
    private bool _smokeRequested;
    private string? _capturePath;
    private Vector2I? _captureSize;
    private string? _requestedPage;
    private string? _evidenceMode;

    public override void _Ready()
    {
        Name = "OpenRpgForgeStudio";
        GetWindow().Title = "OpenRPG Forge Studio";
        GetWindow().MinSize = new Vector2I(1152, 720);
        RenderingServer.SetDefaultClearColor(StudioStyle.Background);

        _defaultWorkspace = Path.GetFullPath(ProjectSettings.GlobalizePath(
            "res://../Forge.Runtime/content/forgotten-forest"));
        if (!StudioUserStateStore.TryLoad(out _userState, out var preferenceError))
        {
            GD.PushWarning($"OPENRPG_FORGE_STUDIO_PREFERENCES_INVALID {preferenceError}");
        }

        var arguments = OS.GetCmdlineUserArgs();
        _smokeRequested = arguments.Contains("--studio-smoke-test", StringComparer.Ordinal);
        _capturePath = ReadArgument(arguments, "--capture");
        _captureSize = ParseCaptureSize(ReadArgument(arguments, "--capture-size"));
        _requestedPage = ReadArgument(arguments, "--page");
        _evidenceMode = ReadArgument(arguments, "--evidence");
        var requestedWorkspace = ReadArgument(arguments, "--workspace");

        BuildHub();
        if (IsNewProjectEvidence())
        {
            ShowNewProjectWizard();
            RunNewProjectEvidence();
        }
        else if (!string.IsNullOrWhiteSpace(requestedWorkspace) || _smokeRequested || _capturePath is not null)
        {
            OpenWorkspace(requestedWorkspace ?? _defaultWorkspace, fromHub: false);
        }
    }

    private void BuildHub()
    {
        _hub = new StudioModuleHub();
        _hub.ContinueRequested += () => OpenWorkspace(PreferredWorkspace(), fromHub: true);
        _hub.BrowseRequested += BrowseForWorkspace;
        _hub.CreateRequested += ShowNewProjectWizard;
        AddChild(_hub);

        var summary = ProjectSession.Open(PreferredWorkspace());
        if (summary.Succeeded && summary.Session is not null)
        {
            var project = summary.Session.Project;
            _hub.SetDefaultModule(
                project.Module.DisplayName,
                $"{project.Areas.Count} zones · {project.Blueprints.Count} blueprints · " +
                $"Dernière zone restaurée automatiquement",
                available: true);
        }
        else
        {
            _hub.SetDefaultModule("Forgotten Forest", "Le workspace fourni n'est pas disponible.", available: false);
        }
    }

    private string PreferredWorkspace()
    {
        if (!string.IsNullOrWhiteSpace(_userState.LastWorkspace) &&
            Directory.Exists(_userState.LastWorkspace))
        {
            return _userState.LastWorkspace;
        }

        return _defaultWorkspace;
    }

    private void BrowseForWorkspace()
    {
        var dialog = new FileDialog
        {
            Title = "Choisir un module OpenRPG Forge",
            FileMode = FileDialog.FileModeEnum.OpenDir,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = true,
        };
        dialog.DirSelected += path =>
        {
            OpenWorkspace(path, fromHub: true);
            dialog.QueueFree();
        };
        dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog);
        dialog.PopupCenteredRatio(0.75f);
    }

    private void OpenWorkspace(string workspace, bool fromHub)
    {
        var result = _session.Open(workspace, _userState.AreaId);
        if (!result.Succeeded)
        {
            var message = string.Join(
                "\n",
                result.Diagnostics.Select(diagnostic => $"{diagnostic.Code} · {diagnostic.Message}"));
            _hub?.ShowStatus(message, error: true);
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_PROJECT_INVALID {result.Diagnostics.FirstOrDefault()?.Code}");
            if (_smokeRequested || _capturePath is not null)
            {
                GetTree().Quit(1);
            }
            return;
        }

        _newProjectWizard?.QueueFree();
        _newProjectWizard = null;
        _shell?.QueueFree();
        _shell = new StudioWorkspaceShell(
            _session,
            ResolveInitialSection(),
            ResolveInitialPage());
        _shell.BackToHubRequested += ReturnToHub;
        _shell.NavigationChanged += SaveNavigationState;
        AddChild(_shell);
        _hub!.Visible = false;

        _userState = _userState with
        {
            LastWorkspace = _session.Session!.RootDirectory,
            SectionId = _shell.SectionId,
            PageId = _shell.PageId,
            AreaId = _session.ActiveAreaId?.ToString(),
        };
        SaveUserState();

        GD.Print(
            $"OPENRPG_FORGE_STUDIO_PROJECT_OPEN title={_session.Project!.Module.DisplayName} " +
            $"areas={_session.Project.Areas.Count} source={(fromHub ? "hub" : "arguments")}");
        if (_smokeRequested || _capturePath is not null || !string.IsNullOrWhiteSpace(_evidenceMode))
        {
            RunAutomatedEvidence();
        }
    }

    private void ShowNewProjectWizard()
    {
        _shell?.QueueFree();
        _shell = null;
        _newProjectWizard?.QueueFree();
        _newProjectWizard = new StudioNewProjectWizard(_session);
        _newProjectWizard.Cancelled += () =>
        {
            _newProjectWizard?.QueueFree();
            _newProjectWizard = null;
            _hub!.Visible = true;
            _hub.ShowStatus("Création annulée · aucun projet n’a été écrit.", error: false);
        };
        _newProjectWizard.ProjectCreated += workspace => OpenWorkspace(workspace, fromHub: true);
        AddChild(_newProjectWizard);
        _hub!.Visible = false;
    }

    private void ReturnToHub()
    {
        SaveNavigationState();
        _shell?.QueueFree();
        _shell = null;
        _session.Close();
        _hub!.Visible = true;
        _hub.ShowStatus("Module fermé proprement. Choisissez le prochain workspace.", error: false);
    }

    private void SaveNavigationState()
    {
        if (_shell is null)
        {
            return;
        }

        _userState = _userState with
        {
            LastWorkspace = _session.Session?.RootDirectory,
            SectionId = _shell.SectionId,
            PageId = _shell.PageId,
            AreaId = _session.ActiveAreaId?.ToString(),
        };
        SaveUserState();
    }

    private void SaveUserState()
    {
        if (!StudioUserStateStore.TrySave(_userState, out var error))
        {
            GD.PushWarning($"OPENRPG_FORGE_STUDIO_PREFERENCES_SAVE_FAILED {error}");
        }
    }

    private string ResolveInitialSection()
    {
        if (!string.IsNullOrWhiteSpace(_requestedPage))
        {
            try
            {
                return Forge.Studio.Core.Navigation.StudioPageCatalog.SectionForPage(_requestedPage).Id;
            }
            catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException)
            {
                GD.PushWarning($"OPENRPG_FORGE_STUDIO_PAGE_UNKNOWN {_requestedPage}");
            }
        }

        return _userState.SectionId;
    }

    private string ResolveInitialPage() => _requestedPage ?? _userState.PageId;

    private async void RunAutomatedEvidence()
    {
        if (_shell is null || _session.Project is null)
        {
            GetTree().Quit(1);
            return;
        }

        for (var frame = 0; frame < 5; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        GD.Print("OPENRPG_FORGE_STUDIO_EVIDENCE_BEGIN");
        if (string.Equals(_requestedPage, "assets", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(_evidenceMode, "assets", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(_evidenceMode, "asset-install", StringComparison.OrdinalIgnoreCase))
        {
            var assetWait = System.Diagnostics.Stopwatch.StartNew();
            while (!_shell.AssetLibraryLoaded && assetWait.Elapsed < TimeSpan.FromSeconds(8))
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            GD.Print($"OPENRPG_FORGE_STUDIO_ASSET_WAIT_DONE loaded={_shell.AssetLibraryLoaded.ToString().ToLowerInvariant()} " +
                     $"elapsed={assetWait.Elapsed.TotalSeconds:0.###}");
        }
        var editValid = !_smokeRequested || _shell.RunEditingSmoke();
        var ux0AValid = !_smokeRequested || _shell.RunUx0ASmoke();
        var ux0BValid = !_smokeRequested || _shell.RunUx0BSmoke();
        var ux0CValid = !_smokeRequested || _shell.RunUx0CSmoke();
        var ux0DValid = !_smokeRequested || _shell.RunUx0DSmoke();
        var ux0EValid = !_smokeRequested || _shell.RunUx0ESmoke();
        var ux0FValid = !_smokeRequested || _shell.RunUx0FSmoke();
        var ux0GValid = !_smokeRequested || _shell.RunUx0GSmoke();
        var ux0HValid = !_smokeRequested || _shell.RunUx0HSmoke();
        var ux0IValid = !_smokeRequested || _shell.RunUx0ISmoke();
        var ux0JValid = !_smokeRequested || _shell.RunUx0JSmoke();
        var ux1AValid = !_smokeRequested || _shell.RunUx1ASmoke();
        var ux1BValid = !_smokeRequested || _shell.RunUx1BSmoke();
        var ux1CValid = !_smokeRequested || _shell.RunUx1CSmoke();
        var ux1DValid = !_smokeRequested || _shell.RunUx1DSmoke();
        var ux1EValid = !_smokeRequested || _shell.RunUx1ESmoke();
        var ux1FValid = !_smokeRequested || _shell.RunUx1FSmoke();
        var ux1GValid = !_smokeRequested || _shell.RunUx1GSmoke();
        var ux1HValid = !_smokeRequested || _shell.RunUx1HSmoke();
        var ux1IValid = !_smokeRequested || _shell.RunUx1ISmoke();
        var ux1JValid = !_smokeRequested || _shell.RunUx1JSmoke();
        var ux1KValid = !_smokeRequested || _shell.RunUx1KSmoke();
        var ux1LValid = !_smokeRequested || _shell.RunUx1LSmoke();
        var ux1MValid = !_smokeRequested || _shell.RunUx1MSmoke();
        var ux1NValid = !_smokeRequested || _shell.RunUx1NSmoke();
        var ux1OValid = !_smokeRequested || _shell.RunUx1OSmoke();
        var ux1PValid = !_smokeRequested || _shell.RunUx1PSmoke();
        var ux1QValid = !_smokeRequested || _shell.RunUx1QSmoke();
        var ux1RValid = !_smokeRequested || _shell.RunUx1RSmoke();
        var ux1SValid = !_smokeRequested || _shell.RunUx1SSmoke();
        var ux1TValid = !_smokeRequested || _shell.RunUx1TSmoke();
        var ux1UValid = !_smokeRequested || _shell.RunUx1USmoke();
        var ux1VValid = !_smokeRequested || _shell.RunUx1VSmoke();
        var ux1WValid = !_smokeRequested || _shell.RunUx1WSmoke();
        var ux1XValid = !_smokeRequested || _shell.RunUx1XSmoke();
        var ux1YValid = !_smokeRequested || _shell.RunUx1YSmoke();
        var ux1ZValid = !_smokeRequested || _shell.RunUx1ZSmoke();
        var ux1AAValid = !_smokeRequested || _shell.RunUx1AASmoke();
        var ux1ABValid = !_smokeRequested || _shell.RunUx1ABSmoke();
        var ux1ACValid = !_smokeRequested || _shell.RunUx1ACSmoke();
        var ux1ADValid = !_smokeRequested || _shell.RunUx1ADSmoke();
        var ux1AEValid = !_smokeRequested || _shell.RunUx1AESmoke();
        var ux1AFValid = !_smokeRequested || _shell.RunUx1AFSmoke();
        var ux1AGValid = !_smokeRequested || _shell.RunUx1AGSmoke();
        var ux1AHValid = !_smokeRequested || _shell.RunUx1AHSmoke();
        var ux1AIValid = !_smokeRequested || _shell.RunUx1AISmoke();
        var ux1AJValid = !_smokeRequested || _shell.RunUx1AJSmoke();
        var ux2AValid = true;
        var ux2LwBValid = true;
        var ux2LwCValid = true;
        var ux2LwDValid = true;
        var ux2LwEValid = true;
        var ux2LwFValid = true;
        var ux2LwGValid = true;
        var ux2LwHValid = true;
        var ux2LwIValid = true;
        var ux2LwJValid = true;
        var ux2LwKValid = true;
        var ux2LwLValid = true;
        var ux2BValid = true;
        var ux2CValid = true;
        var ux2EnvBValid = true;
        var ux2EnvCValid = true;
        var ux2EnvDValid = true;
        var ux2EnvEValid = true;
        var ux2EnvFValid = true;
        var ux2EnvGValid = true;
        var ux2EnvHValid = true;
        var ux2EnvIValid = true;
        var ux2EnvJValid = true;
        var ux2EnvKValid = true;
        var ux2NavBValid = true;
        var ux2NavCValid = true;
        var ux2NavDValid = true;
        var ux2NavEValid = true;
        var ux2NavFValid = true;
        var ux2NavGValid = true;
        var ux2NavHValid = true;
        var ux2NavIValid = true;
        var ux2NavJValid = true;
        var ux2NavKValid = true;
        var ux2NavLValid = true;
        var ux2NavMValid = true;
        var ux4AValid = true;
        var ux4BValid = true;
        var ux4CValid = true;
        var ux4DValid = true;
        var ux4EValid = true;
        var ux4FValid = true;
        var ux4GValid = true;
        var ux4HValid = true;
        var ux4IValid = true;
        var ux4JValid = true;
        var ux4KValid = true;
        var ux4LValid = true;
        var ux4MValid = true;
        var ux4NValid = true;
        var ux5AValid = true;
        var candidateApprovalsValid = true;
        if (_smokeRequested)
        {
            var ux2AApproval = _session.CaptureAreaPolicyApprovalUx2A();
            var ux2LwBApproval = _session.CaptureAreaLifecycleApprovalUx2LwB();
            var ux2LwCApproval = _session.CaptureAreaUserEventApprovalUx2LwC();
            var ux2LwDApproval = _session.CaptureAreaCombatApprovalUx2LwD();
            var ux2LwEApproval = _session.CaptureLivingWorldTriggerApprovalUx2LwE();
            var ux2LwFApproval = _session.CaptureEncounterApprovalUx2LwF();
            var ux2LwGApproval = _session.CaptureEncounterPolicyApprovalUx2LwG();
            var ux2LwHApproval = _session.CaptureEncounterScenarioApprovalUx2LwH();
            var ux2LwIApproval = _session.CaptureEncounterLifecycleApprovalUx2LwI();
            var ux2LwJApproval = _session.CaptureEncounterTacticalApprovalUx2LwJ();
            var ux2LwKApproval = _session.CaptureEncounterStatisticsApprovalUx2LwK();
            var ux2LwLApproval = _session.CaptureEncounterValidationApprovalUx2LwL();
            var ux2BApproval = _session.CaptureTerrainApprovalUx2B();
            var ux2CApproval = _session.CaptureTerrainSurfaceApprovalUx2C();
            var ux2EnvBApproval = _session.CaptureAreaEnvironmentApprovalUx2EnvB();
            var ux2EnvCApproval = _session.CaptureClimateApprovalUx2EnvC();
            var ux2EnvDApproval = _session.CaptureRenderingApprovalUx2EnvD();
            var ux2EnvEApproval = _session.CaptureAudioApprovalUx2EnvE();
            var ux2EnvFApproval = _session.CaptureAudioEmittersApprovalUx2EnvF();
            var ux2EnvGApproval = _session.CaptureAudioWorkbenchApprovalUx2EnvG();
            var ux2EnvHApproval = _session.CaptureAudioRangeApprovalUx2EnvH();
            var ux2EnvIApproval = _session.CaptureAudioScatterApprovalUx2EnvI();
            var ux2EnvJApproval = _session.CaptureMapLodApprovalUx2EnvJ();
            var ux2EnvKApproval = _session.CaptureWaterGraphicsApprovalUx2EnvK();
            var ux2NavBApproval = _session.CaptureNavigationApprovalUx2NavB();
            var ux2NavCApproval = _session.CaptureWaypointApprovalUx2NavC();
            var ux2NavDApproval = _session.CapturePatrolApprovalUx2NavD();
            var ux2NavEApproval = _session.CaptureLocatorApprovalUx2NavE();
            var ux2NavFApproval = _session.CapturePortalApprovalUx2NavF();
            var ux2NavGApproval = _session.CaptureDoorNavigationApprovalUx2NavG();
            var ux2NavHApproval = _session.CaptureTriggerPortalApprovalUx2NavH();
            var ux2NavIApproval = _session.CaptureAerialApprovalUx2NavI();
            var ux2NavJApproval = _session.CaptureVisibilityApprovalUx2NavJ();
            var ux2NavKApproval = _session.CaptureCrossAreaPatrolApprovalUx2NavK();
            var ux2NavLApproval = _session.CaptureVehicleRouteApprovalUx2NavL();
            var ux2NavMApproval = _session.CaptureCameraAnchorApprovalUx2NavM();
            var ux4AApproval = _session.CaptureFactionApprovalUx4A();
            var ux4BApproval = _session.CaptureFactionRuntimeApprovalUx4B();
            var ux4CApproval = _session.CaptureFactionMatrixApprovalUx4C();
            var ux4DApproval = _session.CaptureCreatureFoundationApprovalUx4D();
            var ux4EApproval = _session.CaptureCreaturePresentationApprovalUx4E();
            var ux4FApproval = _session.CaptureCreatureRulesApprovalUx4F();
            var ux4GApproval = _session.CaptureCreatureProgressionApprovalUx4G();
            var ux4HApproval = _session.CaptureCreatureCapabilityApprovalUx4H();
            var ux4IApproval = _session.CaptureCreatureLootApprovalUx4I();
            var ux4JApproval = _session.CaptureCreatureInteractionApprovalUx4J();
            var ux4KApproval = _session.CaptureCreatureMovementApprovalUx4K();
            var ux4LApproval = _session.CaptureCreatureVoiceApprovalUx4L();
            var ux4MApproval = _session.CaptureCreatureRoleBehaviorApprovalUx4M();
            var ux4NApproval = _session.CaptureCreatureEventApprovalUx4N();
            var ux5AApproval = _session.CaptureDialogueApprovalUx5A();
            try
            {
                ux2AValid = _shell.RunUx2ASmoke();
                ux2LwBValid = _shell.RunUx2LwBSmoke();
                ux2LwCValid = _shell.RunUx2LwCSmoke();
                ux2LwDValid = _shell.RunUx2LwDSmoke();
                ux2LwEValid = _shell.RunUx2LwESmoke();
                ux2LwFValid = _shell.RunUx2LwFSmoke();
                ux2LwGValid = _shell.RunUx2LwGSmoke();
                ux2LwHValid = _shell.RunUx2LwHSmoke();
                ux2LwIValid = _shell.RunUx2LwISmoke();
                ux2LwJValid = _shell.RunUx2LwJSmoke();
                ux2LwKValid = _shell.RunUx2LwKSmoke();
                ux2LwLValid = _shell.RunUx2LwLSmoke();
                ux2BValid = _shell.RunUx2BSmoke();
                ux2CValid = _shell.RunUx2CSmoke();
                ux2EnvBValid = _shell.RunUx2EnvBSmoke();
                ux2EnvCValid = _shell.RunUx2EnvCSmoke();
                ux2EnvDValid = _shell.RunUx2EnvDSmoke();
                ux2EnvEValid = _shell.RunUx2EnvESmoke();
                ux2EnvFValid = _shell.RunUx2EnvFSmoke();
                ux2EnvGValid = _shell.RunUx2EnvGSmoke();
                ux2EnvHValid = _shell.RunUx2EnvHSmoke();
                ux2EnvIValid = _shell.RunUx2EnvISmoke();
                ux2EnvJValid = _shell.RunUx2EnvJSmoke();
                ux2EnvKValid = _shell.RunUx2EnvKSmoke();
                ux2NavBValid = _shell.RunUx2NavBSmoke();
                ux2NavCValid = _shell.RunUx2NavCSmoke();
                ux2NavDValid = _shell.RunUx2NavDSmoke();
                ux2NavEValid = _shell.RunUx2NavESmoke();
                ux2NavFValid = _shell.RunUx2NavFSmoke();
                ux2NavGValid = _shell.RunUx2NavGSmoke();
                ux2NavHValid = _shell.RunUx2NavHSmoke();
                ux2NavIValid = _shell.RunUx2NavISmoke();
                ux2NavJValid = _shell.RunUx2NavJSmoke();
                ux2NavKValid = _shell.RunUx2NavKSmoke();
                ux2NavLValid = _shell.RunUx2NavLSmoke();
                ux2NavMValid = _shell.RunUx2NavMSmoke();
                ux4AValid = _shell.RunUx4ASmoke();
                ux4BValid = _shell.RunUx4BSmoke();
                ux4CValid = _shell.RunUx4CSmoke();
                ux4DValid = _shell.RunUx4DSmoke();
                ux4EValid = _shell.RunUx4ESmoke();
                ux4FValid = _shell.RunUx4FSmoke();
                ux4GValid = _shell.RunUx4GSmoke();
                ux4HValid = _shell.RunUx4HSmoke();
                ux4IValid = _shell.RunUx4ISmoke();
                ux4JValid = _shell.RunUx4JSmoke();
                ux4KValid = _shell.RunUx4KSmoke();
                ux4LValid = _shell.RunUx4LSmoke();
                ux4MValid = _shell.RunUx4MSmoke();
                ux4NValid = _shell.RunUx4NSmoke();
                ux5AValid = _shell.RunUx5ASmoke();
            }
            finally
            {
                _session.RestoreAreaPolicyApprovalUx2A(ux2AApproval);
                _session.RestoreAreaLifecycleApprovalUx2LwB(ux2LwBApproval);
                _session.RestoreAreaUserEventApprovalUx2LwC(ux2LwCApproval);
                _session.RestoreAreaCombatApprovalUx2LwD(ux2LwDApproval);
                _session.RestoreLivingWorldTriggerApprovalUx2LwE(ux2LwEApproval);
                _session.RestoreEncounterApprovalUx2LwF(ux2LwFApproval);
                _session.RestoreEncounterPolicyApprovalUx2LwG(ux2LwGApproval);
                _session.RestoreEncounterScenarioApprovalUx2LwH(ux2LwHApproval);
                _session.RestoreEncounterLifecycleApprovalUx2LwI(ux2LwIApproval);
                _session.RestoreEncounterTacticalApprovalUx2LwJ(ux2LwJApproval);
                _session.RestoreEncounterStatisticsApprovalUx2LwK(ux2LwKApproval);
                _session.RestoreEncounterValidationApprovalUx2LwL(ux2LwLApproval);
                _session.RestoreTerrainApprovalUx2B(ux2BApproval);
                _session.RestoreTerrainSurfaceApprovalUx2C(ux2CApproval);
                _session.RestoreAreaEnvironmentApprovalUx2EnvB(ux2EnvBApproval);
                _session.RestoreClimateApprovalUx2EnvC(ux2EnvCApproval);
                _session.RestoreRenderingApprovalUx2EnvD(ux2EnvDApproval);
                _session.RestoreAudioApprovalUx2EnvE(ux2EnvEApproval);
                _session.RestoreAudioEmittersApprovalUx2EnvF(ux2EnvFApproval);
                _session.RestoreAudioWorkbenchApprovalUx2EnvG(ux2EnvGApproval);
                _session.RestoreAudioRangeApprovalUx2EnvH(ux2EnvHApproval);
                _session.RestoreAudioScatterApprovalUx2EnvI(ux2EnvIApproval);
                _session.RestoreMapLodApprovalUx2EnvJ(ux2EnvJApproval);
                _session.RestoreWaterGraphicsApprovalUx2EnvK(ux2EnvKApproval);
                _session.RestoreNavigationApprovalUx2NavB(ux2NavBApproval);
                _session.RestoreWaypointApprovalUx2NavC(ux2NavCApproval);
                _session.RestorePatrolApprovalUx2NavD(ux2NavDApproval);
                _session.RestoreLocatorApprovalUx2NavE(ux2NavEApproval);
                _session.RestorePortalApprovalUx2NavF(ux2NavFApproval);
                _session.RestoreDoorNavigationApprovalUx2NavG(ux2NavGApproval);
                _session.RestoreTriggerPortalApprovalUx2NavH(ux2NavHApproval);
                _session.RestoreAerialApprovalUx2NavI(ux2NavIApproval);
                _session.RestoreVisibilityApprovalUx2NavJ(ux2NavJApproval);
                _session.RestoreCrossAreaPatrolApprovalUx2NavK(ux2NavKApproval);
                _session.RestoreVehicleRouteApprovalUx2NavL(ux2NavLApproval);
                _session.RestoreCameraAnchorApprovalUx2NavM(ux2NavMApproval);
                _session.RestoreFactionApprovalUx4A(ux4AApproval);
                _session.RestoreFactionRuntimeApprovalUx4B(ux4BApproval);
                _session.RestoreFactionMatrixApprovalUx4C(ux4CApproval);
                _session.RestoreCreatureFoundationApprovalUx4D(ux4DApproval);
                _session.RestoreCreaturePresentationApprovalUx4E(ux4EApproval);
                _session.RestoreCreatureRulesApprovalUx4F(ux4FApproval);
                _session.RestoreCreatureProgressionApprovalUx4G(ux4GApproval);
                _session.RestoreCreatureCapabilityApprovalUx4H(ux4HApproval);
                _session.RestoreCreatureLootApprovalUx4I(ux4IApproval);
                _session.RestoreCreatureInteractionApprovalUx4J(ux4JApproval);
                _session.RestoreCreatureMovementApprovalUx4K(ux4KApproval);
                _session.RestoreCreatureVoiceApprovalUx4L(ux4LApproval);
                _session.RestoreCreatureRoleBehaviorApprovalUx4M(ux4MApproval);
                _session.RestoreCreatureEventApprovalUx4N(ux4NApproval);
                _session.RestoreDialogueApprovalUx5A(ux5AApproval);
            }
            candidateApprovalsValid =
                Equals(_session.CaptureAreaPolicyApprovalUx2A(), ux2AApproval) &&
                Equals(_session.CaptureAreaLifecycleApprovalUx2LwB(), ux2LwBApproval) &&
                Equals(_session.CaptureAreaUserEventApprovalUx2LwC(), ux2LwCApproval) &&
                Equals(_session.CaptureAreaCombatApprovalUx2LwD(), ux2LwDApproval) &&
                Equals(_session.CaptureLivingWorldTriggerApprovalUx2LwE(), ux2LwEApproval) &&
                Equals(_session.CaptureEncounterApprovalUx2LwF(), ux2LwFApproval) &&
                Equals(_session.CaptureEncounterPolicyApprovalUx2LwG(), ux2LwGApproval) &&
                Equals(_session.CaptureEncounterScenarioApprovalUx2LwH(), ux2LwHApproval) &&
                Equals(_session.CaptureEncounterLifecycleApprovalUx2LwI(), ux2LwIApproval) &&
                Equals(_session.CaptureEncounterTacticalApprovalUx2LwJ(), ux2LwJApproval) &&
                Equals(_session.CaptureEncounterStatisticsApprovalUx2LwK(), ux2LwKApproval) &&
                Equals(_session.CaptureEncounterValidationApprovalUx2LwL(), ux2LwLApproval) &&
                Equals(_session.CaptureTerrainApprovalUx2B(), ux2BApproval) &&
                Equals(_session.CaptureTerrainSurfaceApprovalUx2C(), ux2CApproval) &&
                Equals(_session.CaptureAreaEnvironmentApprovalUx2EnvB(), ux2EnvBApproval) &&
                Equals(_session.CaptureClimateApprovalUx2EnvC(), ux2EnvCApproval) &&
                Equals(_session.CaptureRenderingApprovalUx2EnvD(), ux2EnvDApproval) &&
                Equals(_session.CaptureAudioApprovalUx2EnvE(), ux2EnvEApproval) &&
                Equals(_session.CaptureAudioEmittersApprovalUx2EnvF(), ux2EnvFApproval) &&
                Equals(_session.CaptureAudioWorkbenchApprovalUx2EnvG(), ux2EnvGApproval) &&
                Equals(_session.CaptureAudioRangeApprovalUx2EnvH(), ux2EnvHApproval) &&
                Equals(_session.CaptureAudioScatterApprovalUx2EnvI(), ux2EnvIApproval) &&
                Equals(_session.CaptureMapLodApprovalUx2EnvJ(), ux2EnvJApproval) &&
                Equals(_session.CaptureWaterGraphicsApprovalUx2EnvK(), ux2EnvKApproval) &&
                Equals(_session.CaptureNavigationApprovalUx2NavB(), ux2NavBApproval) &&
                Equals(_session.CaptureWaypointApprovalUx2NavC(), ux2NavCApproval) &&
                Equals(_session.CapturePatrolApprovalUx2NavD(), ux2NavDApproval) &&
                Equals(_session.CaptureLocatorApprovalUx2NavE(), ux2NavEApproval) &&
                Equals(_session.CapturePortalApprovalUx2NavF(), ux2NavFApproval) &&
                Equals(_session.CaptureDoorNavigationApprovalUx2NavG(), ux2NavGApproval) &&
                Equals(_session.CaptureTriggerPortalApprovalUx2NavH(), ux2NavHApproval) &&
                Equals(_session.CaptureAerialApprovalUx2NavI(), ux2NavIApproval) &&
                Equals(_session.CaptureVisibilityApprovalUx2NavJ(), ux2NavJApproval) &&
                Equals(_session.CaptureCrossAreaPatrolApprovalUx2NavK(), ux2NavKApproval) &&
                Equals(_session.CaptureVehicleRouteApprovalUx2NavL(), ux2NavLApproval) &&
                Equals(_session.CaptureCameraAnchorApprovalUx2NavM(), ux2NavMApproval) &&
                Equals(_session.CaptureFactionApprovalUx4A(), ux4AApproval) &&
                Equals(_session.CaptureFactionRuntimeApprovalUx4B(), ux4BApproval) &&
                Equals(_session.CaptureFactionMatrixApprovalUx4C(), ux4CApproval) &&
                Equals(_session.CaptureCreatureFoundationApprovalUx4D(), ux4DApproval) &&
                Equals(_session.CaptureCreaturePresentationApprovalUx4E(), ux4EApproval) &&
                Equals(_session.CaptureCreatureRulesApprovalUx4F(), ux4FApproval) &&
                Equals(_session.CaptureCreatureProgressionApprovalUx4G(), ux4GApproval) &&
                Equals(_session.CaptureCreatureCapabilityApprovalUx4H(), ux4HApproval) &&
                Equals(_session.CaptureCreatureLootApprovalUx4I(), ux4IApproval) &&
                Equals(_session.CaptureCreatureInteractionApprovalUx4J(), ux4JApproval) &&
                Equals(_session.CaptureCreatureMovementApprovalUx4K(), ux4KApproval) &&
                Equals(_session.CaptureCreatureVoiceApprovalUx4L(), ux4LApproval) &&
                Equals(_session.CaptureCreatureRoleBehaviorApprovalUx4M(), ux4MApproval) &&
                Equals(_session.CaptureCreatureEventApprovalUx4N(), ux4NApproval) &&
                Equals(_session.CaptureDialogueApprovalUx5A(), ux5AApproval);
            GD.Print(
                $"OPENRPG_FORGE_STUDIO_CANDIDATE_APPROVALS_{(candidateApprovalsValid ? "PASS" : "FAILED")} " +
                $"preserved={candidateApprovalsValid.ToString().ToLowerInvariant()}");
        }
        var evidenceValid = _shell.PrepareEditingEvidence(_evidenceMode);
        GD.Print($"OPENRPG_FORGE_STUDIO_EVIDENCE_PREPARED valid={evidenceValid.ToString().ToLowerInvariant()}");
        for (var frame = 0; frame < 2; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _shell.FinalizeEditingEvidenceLayout();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!string.IsNullOrWhiteSpace(_capturePath))
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            RenderingServer.ForceSync();
        }

        var nativeWindows = CountAdditionalWindows(GetTree().Root);
        var shellValid = _shell.SectionCount == 8 && _shell.PageCount == 36 && nativeWindows == 0;
        var navigationEvidence = !string.IsNullOrWhiteSpace(_evidenceMode) &&
                                 _evidenceMode.StartsWith("ux2-nav-", StringComparison.OrdinalIgnoreCase);
        var worldValid = (_shell.WorldViewport.EntityCount > 0 || navigationEvidence) &&
                         _session.Project.Areas.Count > 0;
        if (!shellValid || !worldValid || !editValid || !ux0AValid || !ux0BValid || !ux0CValid || !ux0DValid ||
            !ux0EValid || !ux0FValid || !ux0GValid || !ux0HValid || !ux0IValid || !ux0JValid || !ux1AValid ||
            !ux1BValid || !ux1CValid || !ux1DValid || !ux1EValid || !ux1FValid || !ux1GValid || !ux1HValid ||
            !ux1IValid || !ux1JValid || !ux1KValid || !ux1LValid || !ux1MValid || !ux1NValid || !ux1OValid ||
            !ux1PValid || !ux1QValid || !ux1RValid || !ux1SValid || !ux1TValid || !ux1UValid || !ux1VValid ||
            !ux1WValid || !ux1XValid || !ux1YValid || !ux1ZValid || !ux1AAValid || !ux1ABValid || !ux1ACValid ||
            !ux1ADValid || !ux1AEValid || !ux1AFValid || !ux1AGValid || !ux1AHValid || !ux1AIValid || !ux1AJValid ||
            !ux2AValid || !ux2LwBValid || !ux2LwCValid || !ux2LwDValid || !ux2LwEValid || !ux2LwFValid ||
            !ux2LwGValid || !ux2LwHValid || !ux2LwIValid || !ux2LwJValid || !ux2LwKValid || !ux2LwLValid ||
            !ux2BValid || !ux2CValid ||
            !ux2EnvBValid || !ux2EnvCValid || !ux2EnvDValid || !ux2EnvEValid || !ux2EnvFValid || !ux2EnvGValid ||
            !ux2EnvHValid || !ux2EnvIValid || !ux2EnvJValid || !ux2EnvKValid ||
            !ux2NavBValid || !ux2NavCValid || !ux2NavDValid || !ux2NavEValid || !ux2NavFValid || !ux2NavGValid ||
            !ux2NavHValid || !ux2NavIValid || !ux2NavJValid || !ux2NavKValid || !ux2NavLValid || !ux2NavMValid ||
            !ux4AValid || !ux4BValid || !ux4CValid || !ux4DValid || !ux4EValid || !ux4FValid || !ux4GValid ||
            !ux4HValid || !ux4IValid || !ux4JValid || !ux4KValid || !ux4LValid || !ux4MValid || !ux4NValid || !ux5AValid ||
            !candidateApprovalsValid || !evidenceValid)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_SHELL_FAILED sections={_shell.SectionCount} pages={_shell.PageCount} " +
                $"windows={nativeWindows + 1} entities={_shell.WorldViewport.EntityCount} " +
                $"edit={editValid} ux0a={ux0AValid} ux0b={ux0BValid} ux0c={ux0CValid} ux0d={ux0DValid} " +
                $"ux0e={ux0EValid} ux0f={ux0FValid} ux0g={ux0GValid} ux0h={ux0HValid} ux0i={ux0IValid} " +
                $"ux0j={ux0JValid} ux1a={ux1AValid} ux1b={ux1BValid} ux1c={ux1CValid} ux1d={ux1DValid} " +
                $"ux1e={ux1EValid} ux1f={ux1FValid} ux1g={ux1GValid} ux1h={ux1HValid} ux1i={ux1IValid} " +
                $"ux1j={ux1JValid} ux1k={ux1KValid} ux1l={ux1LValid} ux1m={ux1MValid} ux1n={ux1NValid} " +
                $"ux1o={ux1OValid} ux1p={ux1PValid} ux1q={ux1QValid} ux1r={ux1RValid} ux1s={ux1SValid} " +
                $"ux1t={ux1TValid} ux1u={ux1UValid} ux1v={ux1VValid} ux1w={ux1WValid} " +
                $"ux1x={ux1XValid} ux1y={ux1YValid} ux1z={ux1ZValid} ux1aa={ux1AAValid} " +
                $"ux1ab={ux1ABValid} ux1ac={ux1ACValid} ux1ad={ux1ADValid} ux1ae={ux1AEValid} " +
                $"ux1af={ux1AFValid} ux1ag={ux1AGValid} ux1ah={ux1AHValid} ux1ai={ux1AIValid} " +
                $"ux1aj={ux1AJValid} ux2a={ux2AValid} ux2lwb={ux2LwBValid} ux2lwc={ux2LwCValid} " +
                $"ux2lwd={ux2LwDValid} ux2lwe={ux2LwEValid} ux2lwf={ux2LwFValid} ux2lwg={ux2LwGValid} " +
                $"ux2lwh={ux2LwHValid} ux2lwi={ux2LwIValid} ux2lwj={ux2LwJValid} ux2lwk={ux2LwKValid} " +
                $"ux2lwl={ux2LwLValid} ux2b={ux2BValid} " +
                $"ux2c={ux2CValid} ux2envb={ux2EnvBValid} ux2envc={ux2EnvCValid} ux2envd={ux2EnvDValid} " +
                $"ux2enve={ux2EnvEValid} ux2envf={ux2EnvFValid} ux2envg={ux2EnvGValid} " +
                $"ux2envh={ux2EnvHValid} ux2envi={ux2EnvIValid} ux2envj={ux2EnvJValid} ux2envk={ux2EnvKValid} " +
                $"ux2navb={ux2NavBValid} ux2navc={ux2NavCValid} ux2navd={ux2NavDValid} " +
                $"ux2nave={ux2NavEValid} ux2navf={ux2NavFValid} ux2navg={ux2NavGValid} ux2navh={ux2NavHValid} " +
                $"ux2navi={ux2NavIValid} ux2navj={ux2NavJValid} ux2navk={ux2NavKValid} ux2navl={ux2NavLValid} " +
                $"ux2navm={ux2NavMValid} " +
                $"ux4a={ux4AValid} ux4b={ux4BValid} ux4c={ux4CValid} ux4d={ux4DValid} ux4e={ux4EValid} ux4f={ux4FValid} ux4g={ux4GValid} ux4h={ux4HValid} ux4i={ux4IValid} ux4j={ux4JValid} ux4k={ux4KValid} ux4l={ux4LValid} ux4m={ux4MValid} ux4n={ux4NValid} ux5a={ux5AValid} " +
                $"candidate_approvals={candidateApprovalsValid} " +
                $"evidence={evidenceValid}");
            GetTree().Quit(1);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_capturePath))
        {
            var path = Path.GetFullPath(_capturePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var image = await CaptureEvidenceImage(_shell);
            if (image.SavePng(path) != Error.Ok)
            {
                GD.PrintErr($"OPENRPG_FORGE_STUDIO_CAPTURE_FAILED path={path}");
                GetTree().Quit(1);
                return;
            }
            GD.Print($"OPENRPG_FORGE_STUDIO_CAPTURE_PASS path={path} size={image.GetWidth()}x{image.GetHeight()}");
        }

        GD.Print(
            $"OPENRPG_FORGE_STUDIO_SHELL_PASS sections={_shell.SectionCount} pages={_shell.PageCount} " +
            $"windows=1 module={_session.Project.Module.Slug} areas={_session.Project.Areas.Count} " +
            $"entities={_shell.WorldViewport.EntityCount} assets={_shell.WorldViewport.AssetModelInstanceCount}");
        GetTree().Quit();
    }

    private async void RunNewProjectEvidence()
    {
        if (_newProjectWizard is null)
        {
            GetTree().Quit(1);
            return;
        }
        for (var frame = 0; frame < 5; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var prepared = _newProjectWizard.PrepareEvidence(_evidenceMode ?? "ux0-e-wizard");
        for (var frame = 0; frame < 2; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        if (!string.IsNullOrWhiteSpace(_capturePath))
        {
            var path = Path.GetFullPath(_capturePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var image = await CaptureEvidenceImage(_newProjectWizard);
            if (image.SavePng(path) != Error.Ok)
            {
                GD.PrintErr($"OPENRPG_FORGE_STUDIO_CAPTURE_FAILED path={path}");
                GetTree().Quit(1);
                return;
            }
            GD.Print($"OPENRPG_FORGE_STUDIO_CAPTURE_PASS path={path} size={image.GetWidth()}x{image.GetHeight()}");
        }
        var windows = CountAdditionalWindows(GetTree().Root) + 1;
        var valid = prepared && windows == 1 && _newProjectWizard.StepCount == 5 &&
                    _newProjectWizard.ShowsHumanSurface;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX0E_WIZARD_{(valid ? "PASS" : "FAILED")} " +
            $"steps={_newProjectWizard.StepCount} windows={windows} human_no_uuid={_newProjectWizard.ShowsHumanSurface.ToString().ToLowerInvariant()} " +
            "preview_no_write=true");
        GetTree().Quit(valid ? 0 : 1);
    }

    private bool IsNewProjectEvidence() =>
        string.Equals(_evidenceMode, "ux0-e-wizard", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(_evidenceMode, "ux0-e-summary", StringComparison.OrdinalIgnoreCase);

    private async Task<Image> CaptureEvidenceImage(Control content)
    {
        if (_captureSize is not { } requested || GetWindow().Size == requested)
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            RenderingServer.ForceSync();
            return GetViewport().GetTexture().GetImage();
        }

        var viewport = new SubViewport
        {
            Name = "StudioEvidenceViewport",
            Size = requested,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            RenderTargetClearMode = SubViewport.ClearMode.Always,
        };
        AddChild(viewport);
        content.Reparent(viewport, keepGlobalTransform: false);
        content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        for (var frame = 0; frame < 4; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        if (ReferenceEquals(content, _shell))
        {
            _shell!.PrepareEditingEvidence(_evidenceMode);
        }
        else if (ReferenceEquals(content, _newProjectWizard))
        {
            _newProjectWizard!.PrepareEvidence(_evidenceMode ?? "ux0-e-wizard");
        }
        for (var frame = 0; frame < 2; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        if (ReferenceEquals(content, _shell))
        {
            _shell.FinalizeEditingEvidenceLayout();
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        RenderingServer.ForceSync();
        return viewport.GetTexture().GetImage();
    }

    private static int CountAdditionalWindows(Node node)
    {
        var count = 0;
        foreach (Node child in node.GetChildren())
        {
            if (child is Window)
            {
                count++;
            }
            count += CountAdditionalWindows(child);
        }
        return count;
    }

    private static string? ReadArgument(string[] arguments, string name)
    {
        var index = Array.IndexOf(arguments, name);
        return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
    }

    private static Vector2I? ParseCaptureSize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var components = value.Split('x', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (components.Length != 2 || !int.TryParse(components[0], out var width) ||
            !int.TryParse(components[1], out var height) || width < 320 || height < 240 ||
            width > 16_384 || height > 16_384)
        {
            throw new ArgumentException($"Invalid Studio capture size '{value}'.", nameof(value));
        }
        return new Vector2I(width, height);
    }
}
