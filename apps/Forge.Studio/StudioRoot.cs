using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Core.Accessibility;
using Forge.Studio.Core.Navigation;
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
    private bool _distributionSmokeRequested;
    private string? _capturePath;
    private string? _capturePageMatrixPath;
    private Vector2I? _captureSize;
    private string? _requestedPage;
    private string? _evidenceMode;
    private double? _requestedUiScale;
    private double _appliedUiScale = 1;

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
        _distributionSmokeRequested = arguments.Contains(
            "--studio-distribution-smoke-test", StringComparer.Ordinal);
        _smokeRequested = _distributionSmokeRequested ||
                          arguments.Contains("--studio-smoke-test", StringComparer.Ordinal);
        _capturePath = ReadArgument(arguments, "--capture");
        _capturePageMatrixPath = ReadArgument(arguments, "--capture-page-matrix");
        _captureSize = ParseCaptureSize(ReadArgument(arguments, "--capture-size"));
        _requestedPage = ReadArgument(arguments, "--page");
        _evidenceMode = ReadArgument(arguments, "--evidence");
        if (!StudioDisplayScale.TryParseOverride(ReadArgument(arguments, "--ui-scale"), out _requestedUiScale))
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UI_SCALE_INVALID min={StudioDisplayScale.Minimum:0.##} " +
                $"max={StudioDisplayScale.Maximum:0.##}");
            GetTree().Quit(2);
            return;
        }
        ApplyDisplayScale(StudioDisplayScale.Resolve(_requestedUiScale, 1));
        var requestedWorkspace = ReadArgument(arguments, "--workspace");

        BuildHub();
        if (IsNewProjectEvidence())
        {
            ShowNewProjectWizard();
            RunNewProjectEvidence();
        }
        else if (!string.IsNullOrWhiteSpace(requestedWorkspace) || _smokeRequested || _capturePath is not null ||
                 _capturePageMatrixPath is not null)
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
            if (_smokeRequested || _capturePath is not null || _capturePageMatrixPath is not null)
            {
                GetTree().Quit(1);
            }
            return;
        }

        ApplyDisplayScale(StudioDisplayScale.Resolve(_requestedUiScale, _session.WorkspaceProfile.UiScale));

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
        if (_smokeRequested || _capturePath is not null || _capturePageMatrixPath is not null ||
            !string.IsNullOrWhiteSpace(_evidenceMode))
        {
            RunAutomatedEvidence();
        }
    }

    private void ApplyDisplayScale(double scale)
    {
        _appliedUiScale = scale;
        GetWindow().ContentScaleFactor = (float)scale;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_DISPLAY_SCALE scale={scale:0.##} " +
            $"source={(_requestedUiScale is null ? "profile" : "override")}");
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
        if (_distributionSmokeRequested)
        {
            await RunDistributionSmoke();
            return;
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
        var ux3AValid = true;
        var ux3BValid = true;
        var ux3CValid = true;
        var ux3DValid = true;
        var ux3EValid = true;
        var ux3FValid = true;
        var ux3GValid = true;
        var ux3HValid = true;
        var ux3IValid = true;
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
        var ux4OValid = true;
        var ux4PValid = true;
        var ux4QValid = true;
        var ux4RValid = true;
        var ux4SValid = true;
        var ux4TValid = true;
        var ux4UValid = true;
        var ux5AValid = true;
        var ux5BValid = true;
        var ux6AValid = true;
        var ux6BValid = true;
        var ux6CValid = true;
        var ux6DValid = true;
        var ux6EValid = true;
        var ux6FValid = true;
        var ux6GValid = true;
        var ux6HValid = true;
        var ux6IValid = true;
        var ux6JValid = true;
        var ux6KValid = true;
        var ux6LValid = true;
        var ux6MValid = true;
        var ux6NValid = true;
        var ux6OValid = true;
        var ux6PValid = true;
        var ux6QValid = true;
        var ux6RValid = true;
        var ux6SValid = true;
        var ux6TValid = true;
        var ux6UValid = true;
        var ux6VValid = true;
        var ux6WValid = true;
        var ux6XValid = true;
        var ux6YValid = true;
        var ux6ZValid = true;
        var ux7AValid = true;
        var ux7BValid = true;
        var ux7CValid = true;
        var ux7DValid = true;
        var ux7EValid = true;
        var ux7FValid = true;
        var ux7GValid = true;
        var ux7HValid = true;
        var ux7IValid = true;
        var ux8AValid = true;
        var ux8BValid = true;
        var ux8CValid = true;
        var ux8DValid = true;
        var ux8EValid = true;
        var ux8FValid = true;
        var ux8GValid = true;
        var ux8HValid = true;
        var ux8IValid = true;
        var ux8JValid = true;
        var ux8KValid = true;
        var ux8LValid = true;
        var ux8MValid = true;
        var ux8NValid = true;
        var ux9AValid = true;
        var ux9BValid = true;
        var ux9CValid = true;
        var ux9DValid = true;
        var ux9EValid = true;
        var ux9FValid = true;
        var ux9GValid = true;
        var ux9HValid = true;
        var ux9IValid = true;
        var ux9JValid = true;
        var ux9KValid = true;
        var ux9LValid = true;
        var ux9MValid = true;
        var ux9NValid = true;
        var ux9OValid = true;
        var ux10Valid = true;
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
            var ux3AApproval = _session.CaptureBlueprintApprovalUx3A();
            var ux3BApproval = _session.CaptureBlueprintLibraryApprovalUx3B();
            var ux3CApproval = _session.CaptureBlueprintDiscoveryApprovalUx3C();
            var ux3DApproval = _session.CaptureBlueprintGovernanceApprovalUx3D();
            var ux3EApproval = _session.CaptureAssetQualityApprovalUx3E();
            var ux3FApproval = _session.CaptureAssetAuthoringApprovalUx3F();
            var ux3GApproval = _session.CaptureManifestApprovalUx3G();
            var ux3HApproval = _session.CaptureContentApprovalUx3H();
            var ux3IGovernanceApproval = _session.CaptureGovernanceApprovalUx3I();
            var ux3IMediaApproval = _session.CaptureMediaApprovalUx3I();
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
            var ux4OApproval = _session.CaptureCreatureDamageApprovalUx4O();
            var ux4PApproval = _session.CaptureCreatureEventAiApprovalUx4P();
            var ux4QApproval = _session.CaptureAreaVoiceApprovalUx4Q();
            var ux4RApproval = _session.CaptureBossApprovalUx4R();
            var ux4SApproval = _session.CaptureItemApprovalUx4S();
            var ux4TApproval = _session.CaptureMerchantApprovalUx4T();
            var ux4UApproval = _session.CaptureCompanionApprovalUx4U();
            var ux5AApproval = _session.CaptureDialogueApprovalUx5A();
            var ux5BApproval = _session.CaptureNarrativeApprovalUx5B();
            var ux6AApproval = _session.CaptureRulesWorldApprovalUx6A();
            var ux6BApproval = _session.CaptureGameplayFoundationApprovalUx6B();
            var ux6CApproval = _session.CaptureGameplayCapabilityApprovalUx6C();
            var ux6DApproval = _session.CaptureGameplaySimulationApprovalUx6D();
            var ux6EApproval = _session.CaptureGameplayProductionApprovalUx6E();
            var ux6FApproval = _session.CaptureScriptApprovalUx6F();
            var ux6JApproval = _session.CaptureExecutionPolicyApprovalUx6J();
            var ux6KApproval = _session.CaptureModulePermissionApprovalUx6K();
            var ux6MApproval = _session.CaptureScriptAutomationApprovalUx6M();
            var ux6OApproval = _session.CaptureScriptQualityApprovalUx6O();
            var ux6PApproval = _session.CaptureScriptInteropApprovalUx6P();
            var ux6QApproval = _session.CaptureCompilerAssistantApprovalUx6Q();
            var ux6RApproval = _session.CaptureRuntimeDataApprovalUx6R();
            var ux6SApproval = _session.CaptureCreatorPolicyApprovalUx6S();
            var ux6TApproval = _session.CaptureModernToolingApprovalUx6T();
            var ux6UApproval = _session.CaptureBlackboardApprovalUx6U();
            var ux6VApproval = _session.CapturePathfindingApprovalUx6V();
            var ux6WApproval = _session.CaptureRuntimeTransformApprovalUx6W();
            var ux6XApproval = _session.CaptureRuntimeUiApprovalUx6X();
            var ux6YApproval = _session.CaptureRuntimeOperationsApprovalUx6Y();
            var ux6ZApproval = _session.CaptureAbilityApprovalUx6Z();
            var ux7AApproval = _session.CaptureValidationApprovalUx7A();
            var ux7BApproval = _session.CaptureQualityFoundationApprovalUx7B();
            var ux7IApproval = _session.CaptureObservabilityApprovalUx7I();
            var ux8CApproval = _session.CaptureGovernanceApprovalUx8C();
            var ux8HApproval = _session.CaptureDistributionApprovalUx8H();
            var ux8IApproval = _session.CaptureCompatibilityApprovalUx8I();
            var ux8JApproval = _session.CaptureSecurityApprovalUx8J();
            var ux8KApproval = _session.CaptureBulkApprovalUx8K();
            var ux8LApproval = _session.CaptureSyncApprovalUx8L();
            var ux8MApproval = _session.CaptureNetworkExperienceApprovalUx8M();
            var ux8NApproval = _session.CapturePackageLifecycleApprovalUx8N();
            var ux9AApproval = _session.CaptureNetworkFoundationApprovalUx9A();
            var ux9BApproval = _session.CaptureNetworkOperationsApprovalUx9B();
            var ux9CApproval = _session.CaptureWorldAdministrationApprovalUx9C();
            var ux9DApproval = _session.CapturePersistenceObservabilityApprovalUx9D();
            var ux9EApproval = _session.CaptureResilienceCapacityApprovalUx9E();
            var ux9FApproval = _session.CaptureLiveDirectorApprovalUx9F();
            var ux9GApproval = _session.CaptureLiveActionApprovalUx9G();
            var ux9HApproval = _session.CaptureLiveAuthorityApprovalUx9H();
            var ux9IApproval = _session.CaptureLiveGroupApprovalUx9I();
            var ux9JApproval = _session.CaptureLiveOperationsApprovalUx9J();
            var ux9KApproval = _session.CaptureLiveNarrativeApprovalUx9K();
            var ux9LApproval = _session.CaptureLiveAccessApprovalUx9L();
            var ux9MApproval = _session.CaptureLiveProductionApprovalUx9M();
            var ux9NApproval = _session.CaptureNetworkProductionApprovalUx9N();
            var ux9OApproval = _session.CaptureNetworkClosureApprovalUx9O();
            var ux10Approval = _session.CaptureAssistantApprovalUx10();
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
                ux3AValid = _shell.RunUx3ASmoke();
                ux3BValid = _shell.RunUx3BSmoke();
                ux3CValid = _shell.RunUx3CSmoke();
                ux3DValid = _shell.RunUx3DSmoke();
                ux3EValid = _shell.RunUx3ESmoke();
                ux3FValid = _shell.RunUx3FSmoke();
                ux3GValid = _shell.RunUx3GSmoke();
                ux3HValid = _shell.RunUx3HSmoke();
                ux3IValid = _shell.RunUx3ISmoke();
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
                ux4OValid = _shell.RunUx4OSmoke();
                ux4PValid = _shell.RunUx4PSmoke();
                ux4QValid = _shell.RunUx4QSmoke();
                ux4RValid = _shell.RunUx4RSmoke();
                ux4SValid = _shell.RunUx4SSmoke();
                ux4TValid = _shell.RunUx4TSmoke();
                ux4UValid = _shell.RunUx4USmoke();
                ux5AValid = _shell.RunUx5ASmoke();
                ux5BValid = _shell.RunUx5BSmoke();
                ux6AValid = _shell.RunUx6ASmoke();
                ux6BValid = _shell.RunUx6BSmoke();
                ux6CValid = _shell.RunUx6CSmoke();
                ux6DValid = _shell.RunUx6DSmoke();
                ux6EValid = _shell.RunUx6ESmoke();
                ux6FValid = _shell.RunUx6FSmoke();
                ux6GValid = _shell.RunUx6GSmoke();
                ux6HValid = _shell.RunUx6HSmoke();
                ux6IValid = _shell.RunUx6ISmoke();
                ux6JValid = _shell.RunUx6JSmoke();
                ux6KValid = _shell.RunUx6KSmoke();
                ux6LValid = _shell.RunUx6LSmoke();
                ux6MValid = _shell.RunUx6MSmoke();
                ux6NValid = _shell.RunUx6NSmoke();
                ux6OValid = _shell.RunUx6OSmoke();
                ux6PValid = _shell.RunUx6PSmoke();
                ux6QValid = _shell.RunUx6QSmoke();
                ux6RValid = _shell.RunUx6RSmoke();
                ux6SValid = _shell.RunUx6SSmoke();
                ux6TValid = _shell.RunUx6TSmoke();
                ux6UValid = _shell.RunUx6USmoke();
                ux6VValid = _shell.RunUx6VSmoke();
                ux6WValid = _shell.RunUx6WSmoke();
                ux6XValid = _shell.RunUx6XSmoke();
                ux6YValid = _shell.RunUx6YSmoke();
                ux6ZValid = _shell.RunUx6ZSmoke();
                ux7AValid = _shell.RunUx7ASmoke();
                ux7BValid = _shell.RunUx7BSmoke();
                ux7CValid = _shell.RunUx7CSmoke();
                ux7DValid = _shell.RunUx7DSmoke();
                ux7EValid = _shell.RunUx7ESmoke();
                ux7FValid = _shell.RunUx7FSmoke();
                ux7GValid = _shell.RunUx7GSmoke();
                ux7HValid = _shell.RunUx7HSmoke();
                ux7IValid = _shell.RunUx7ISmoke();
                ux8AValid = _shell.RunUx8ASmoke();
                ux8BValid = _shell.RunUx8BSmoke();
                ux8CValid = _shell.RunUx8CSmoke();
                ux8DValid = _shell.RunUx8DSmoke();
                ux8EValid = _shell.RunUx8ESmoke();
                ux8FValid = _shell.RunUx8FSmoke();
                ux8GValid = _shell.RunUx8GSmoke();
                ux8HValid = _shell.RunUx8HSmoke();
                ux8IValid = _shell.RunUx8ISmoke();
                ux8JValid = _shell.RunUx8JSmoke();
                ux8KValid = _shell.RunUx8KSmoke();
                ux8LValid = _shell.RunUx8LSmoke();
                ux8MValid = _shell.RunUx8MSmoke();
                ux8NValid = _shell.RunUx8NSmoke();
                ux9AValid = _shell.RunUx9ASmoke();
                ux9BValid = _shell.RunUx9BSmoke();
                ux9CValid = _shell.RunUx9CSmoke();
                ux9DValid = _shell.RunUx9DSmoke();
                ux9EValid = _shell.RunUx9ESmoke();
                ux9FValid = _shell.RunUx9FSmoke();
                ux9GValid = _shell.RunUx9GSmoke();
                ux9HValid = _shell.RunUx9HSmoke();
                ux9IValid = _shell.RunUx9ISmoke();
                ux9JValid = _shell.RunUx9JSmoke();
                ux9KValid = _shell.RunUx9KSmoke();
                ux9LValid = _shell.RunUx9LSmoke();
                ux9MValid = _shell.RunUx9MSmoke();
                ux9NValid = _shell.RunUx9NSmoke();
                ux9OValid = _shell.RunUx9OSmoke();
                ux10Valid = _shell.RunUx10Smoke();
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
                _session.RestoreBlueprintApprovalUx3A(ux3AApproval);
                _session.RestoreBlueprintLibraryApprovalUx3B(ux3BApproval);
                _session.RestoreBlueprintDiscoveryApprovalUx3C(ux3CApproval);
                _session.RestoreBlueprintGovernanceApprovalUx3D(ux3DApproval);
                _session.RestoreAssetQualityApprovalUx3E(ux3EApproval);
                _session.RestoreAssetAuthoringApprovalUx3F(ux3FApproval);
                _session.RestoreManifestApprovalUx3G(ux3GApproval);
                _session.RestoreContentApprovalUx3H(ux3HApproval);
                _session.RestoreGovernanceApprovalUx3I(ux3IGovernanceApproval);
                _session.RestoreMediaApprovalUx3I(ux3IMediaApproval);
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
                _session.RestoreCreatureDamageApprovalUx4O(ux4OApproval);
                _session.RestoreCreatureEventAiApprovalUx4P(ux4PApproval);
                _session.RestoreAreaVoiceApprovalUx4Q(ux4QApproval);
                _session.RestoreBossApprovalUx4R(ux4RApproval);
                _session.RestoreItemApprovalUx4S(ux4SApproval);
                _session.RestoreMerchantApprovalUx4T(ux4TApproval);
                _session.RestoreCompanionApprovalUx4U(ux4UApproval);
                _session.RestoreDialogueApprovalUx5A(ux5AApproval);
                _session.RestoreNarrativeApprovalUx5B(ux5BApproval);
                _session.RestoreRulesWorldApprovalUx6A(ux6AApproval);
                _session.RestoreGameplayFoundationApprovalUx6B(ux6BApproval);
                _session.RestoreGameplayCapabilityApprovalUx6C(ux6CApproval);
                _session.RestoreGameplaySimulationApprovalUx6D(ux6DApproval);
                _session.RestoreGameplayProductionApprovalUx6E(ux6EApproval);
                _session.RestoreScriptApprovalUx6F(ux6FApproval);
                _session.RestoreExecutionPolicyApprovalUx6J(ux6JApproval);
                _session.RestoreModulePermissionApprovalUx6K(ux6KApproval);
                _session.RestoreScriptAutomationApprovalUx6M(ux6MApproval);
                _session.RestoreScriptQualityApprovalUx6O(ux6OApproval);
                _session.RestoreScriptInteropApprovalUx6P(ux6PApproval);
                _session.RestoreCompilerAssistantApprovalUx6Q(ux6QApproval);
                _session.RestoreRuntimeDataApprovalUx6R(ux6RApproval);
                _session.RestoreCreatorPolicyApprovalUx6S(ux6SApproval);
                _session.RestoreModernToolingApprovalUx6T(ux6TApproval);
                _session.RestoreBlackboardApprovalUx6U(ux6UApproval);
                _session.RestorePathfindingApprovalUx6V(ux6VApproval);
                _session.RestoreRuntimeTransformApprovalUx6W(ux6WApproval);
                _session.RestoreRuntimeUiApprovalUx6X(ux6XApproval);
                _session.RestoreRuntimeOperationsApprovalUx6Y(ux6YApproval);
                _session.RestoreAbilityApprovalUx6Z(ux6ZApproval);
                _session.RestoreValidationApprovalUx7A(ux7AApproval);
                _session.RestoreQualityFoundationApprovalUx7B(ux7BApproval);
                _session.RestoreObservabilityApprovalUx7I(ux7IApproval);
                _session.RestoreGovernanceApprovalUx8C(ux8CApproval);
                _session.RestoreDistributionApprovalUx8H(ux8HApproval);
                _session.RestoreCompatibilityApprovalUx8I(ux8IApproval);
                _session.RestoreSecurityApprovalUx8J(ux8JApproval);
                _session.RestoreBulkApprovalUx8K(ux8KApproval);
                _session.RestoreSyncApprovalUx8L(ux8LApproval);
                _session.RestoreNetworkExperienceApprovalUx8M(ux8MApproval);
                _session.RestorePackageLifecycleApprovalUx8N(ux8NApproval);
                _session.RestoreNetworkFoundationApprovalUx9A(ux9AApproval);
                _session.RestoreNetworkOperationsApprovalUx9B(ux9BApproval);
                _session.RestoreWorldAdministrationApprovalUx9C(ux9CApproval);
                _session.RestorePersistenceObservabilityApprovalUx9D(ux9DApproval);
                _session.RestoreResilienceCapacityApprovalUx9E(ux9EApproval);
                _session.RestoreLiveDirectorApprovalUx9F(ux9FApproval);
                _session.RestoreLiveActionApprovalUx9G(ux9GApproval);
                _session.RestoreLiveAuthorityApprovalUx9H(ux9HApproval);
                _session.RestoreLiveGroupApprovalUx9I(ux9IApproval);
                _session.RestoreLiveOperationsApprovalUx9J(ux9JApproval);
                _session.RestoreLiveNarrativeApprovalUx9K(ux9KApproval);
                _session.RestoreLiveAccessApprovalUx9L(ux9LApproval);
                _session.RestoreLiveProductionApprovalUx9M(ux9MApproval);
                _session.RestoreNetworkProductionApprovalUx9N(ux9NApproval);
                _session.RestoreNetworkClosureApprovalUx9O(ux9OApproval);
                _session.RestoreAssistantApprovalUx10(ux10Approval);
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
                Equals(_session.CaptureBlueprintApprovalUx3A(), ux3AApproval) &&
                Equals(_session.CaptureBlueprintLibraryApprovalUx3B(), ux3BApproval) &&
                Equals(_session.CaptureBlueprintDiscoveryApprovalUx3C(), ux3CApproval) &&
                Equals(_session.CaptureBlueprintGovernanceApprovalUx3D(), ux3DApproval) &&
                Equals(_session.CaptureAssetQualityApprovalUx3E(), ux3EApproval) &&
                Equals(_session.CaptureAssetAuthoringApprovalUx3F(), ux3FApproval) &&
                Equals(_session.CaptureManifestApprovalUx3G(), ux3GApproval) &&
                Equals(_session.CaptureContentApprovalUx3H(), ux3HApproval) &&
                Equals(_session.CaptureGovernanceApprovalUx3I(), ux3IGovernanceApproval) &&
                Equals(_session.CaptureMediaApprovalUx3I(), ux3IMediaApproval) &&
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
                Equals(_session.CaptureCreatureDamageApprovalUx4O(), ux4OApproval) &&
                Equals(_session.CaptureCreatureEventAiApprovalUx4P(), ux4PApproval) &&
                Equals(_session.CaptureAreaVoiceApprovalUx4Q(), ux4QApproval) &&
                Equals(_session.CaptureBossApprovalUx4R(), ux4RApproval) &&
                Equals(_session.CaptureItemApprovalUx4S(), ux4SApproval) &&
                Equals(_session.CaptureMerchantApprovalUx4T(), ux4TApproval) &&
                Equals(_session.CaptureCompanionApprovalUx4U(), ux4UApproval) &&
                Equals(_session.CaptureDialogueApprovalUx5A(), ux5AApproval) &&
                Equals(_session.CaptureNarrativeApprovalUx5B(), ux5BApproval) &&
                Equals(_session.CaptureRulesWorldApprovalUx6A(), ux6AApproval) &&
                Equals(_session.CaptureGameplayFoundationApprovalUx6B(), ux6BApproval) &&
                Equals(_session.CaptureGameplayCapabilityApprovalUx6C(), ux6CApproval) &&
                Equals(_session.CaptureGameplaySimulationApprovalUx6D(), ux6DApproval) &&
                Equals(_session.CaptureGameplayProductionApprovalUx6E(), ux6EApproval) &&
                Equals(_session.CaptureScriptApprovalUx6F(), ux6FApproval) &&
                Equals(_session.CaptureExecutionPolicyApprovalUx6J(), ux6JApproval) &&
                Equals(_session.CaptureModulePermissionApprovalUx6K(), ux6KApproval) &&
                Equals(_session.CaptureScriptAutomationApprovalUx6M(), ux6MApproval) &&
                Equals(_session.CaptureScriptQualityApprovalUx6O(), ux6OApproval);
            candidateApprovalsValid = candidateApprovalsValid &&
                Equals(_session.CaptureScriptInteropApprovalUx6P(), ux6PApproval) &&
                Equals(_session.CaptureCompilerAssistantApprovalUx6Q(), ux6QApproval) &&
                Equals(_session.CaptureRuntimeDataApprovalUx6R(), ux6RApproval) &&
                Equals(_session.CaptureCreatorPolicyApprovalUx6S(), ux6SApproval) &&
                Equals(_session.CaptureModernToolingApprovalUx6T(), ux6TApproval) &&
                Equals(_session.CaptureBlackboardApprovalUx6U(), ux6UApproval) &&
                Equals(_session.CapturePathfindingApprovalUx6V(), ux6VApproval) &&
                Equals(_session.CaptureRuntimeTransformApprovalUx6W(), ux6WApproval) &&
                Equals(_session.CaptureRuntimeUiApprovalUx6X(), ux6XApproval);
            candidateApprovalsValid = candidateApprovalsValid &&
                Equals(_session.CaptureRuntimeOperationsApprovalUx6Y(), ux6YApproval) &&
                Equals(_session.CaptureAbilityApprovalUx6Z(), ux6ZApproval) &&
                Equals(_session.CaptureValidationApprovalUx7A(), ux7AApproval) &&
                Equals(_session.CaptureQualityFoundationApprovalUx7B(), ux7BApproval) &&
                Equals(_session.CaptureObservabilityApprovalUx7I(), ux7IApproval);
            candidateApprovalsValid = candidateApprovalsValid &&
                Equals(_session.CaptureGovernanceApprovalUx8C(), ux8CApproval) &&
                Equals(_session.CaptureDistributionApprovalUx8H(), ux8HApproval) &&
                Equals(_session.CaptureCompatibilityApprovalUx8I(), ux8IApproval) &&
                Equals(_session.CaptureSecurityApprovalUx8J(), ux8JApproval) &&
                Equals(_session.CaptureBulkApprovalUx8K(), ux8KApproval) &&
                Equals(_session.CaptureSyncApprovalUx8L(), ux8LApproval) &&
                Equals(_session.CaptureNetworkExperienceApprovalUx8M(), ux8MApproval) &&
                Equals(_session.CapturePackageLifecycleApprovalUx8N(), ux8NApproval) &&
                Equals(_session.CaptureNetworkFoundationApprovalUx9A(), ux9AApproval) &&
                Equals(_session.CaptureNetworkOperationsApprovalUx9B(), ux9BApproval) &&
                Equals(_session.CaptureWorldAdministrationApprovalUx9C(), ux9CApproval) &&
                Equals(_session.CapturePersistenceObservabilityApprovalUx9D(), ux9DApproval) &&
                Equals(_session.CaptureResilienceCapacityApprovalUx9E(), ux9EApproval) &&
                Equals(_session.CaptureLiveDirectorApprovalUx9F(), ux9FApproval) &&
                Equals(_session.CaptureLiveActionApprovalUx9G(), ux9GApproval) &&
                Equals(_session.CaptureLiveAuthorityApprovalUx9H(), ux9HApproval) &&
                Equals(_session.CaptureLiveGroupApprovalUx9I(), ux9IApproval) &&
                Equals(_session.CaptureLiveOperationsApprovalUx9J(), ux9JApproval) &&
                Equals(_session.CaptureLiveNarrativeApprovalUx9K(), ux9KApproval) &&
                Equals(_session.CaptureLiveAccessApprovalUx9L(), ux9LApproval) &&
                Equals(_session.CaptureLiveProductionApprovalUx9M(), ux9MApproval) &&
                Equals(_session.CaptureNetworkProductionApprovalUx9N(), ux9NApproval) &&
                Equals(_session.CaptureNetworkClosureApprovalUx9O(), ux9OApproval) &&
                Equals(_session.CaptureAssistantApprovalUx10(), ux10Approval);
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
        if (!string.IsNullOrWhiteSpace(_capturePath) || !string.IsNullOrWhiteSpace(_capturePageMatrixPath))
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
            !ux3AValid || !ux3BValid || !ux3CValid || !ux3DValid || !ux3EValid || !ux3FValid || !ux3GValid || !ux3HValid || !ux3IValid ||
            !ux4AValid || !ux4BValid || !ux4CValid || !ux4DValid || !ux4EValid || !ux4FValid || !ux4GValid ||
            !ux4HValid || !ux4IValid || !ux4JValid || !ux4KValid || !ux4LValid || !ux4MValid || !ux4NValid ||
            !ux4OValid || !ux4PValid || !ux4QValid || !ux4RValid || !ux4SValid || !ux4TValid || !ux4UValid ||
            !ux5AValid || !ux5BValid || !ux6AValid || !ux6BValid || !ux6CValid || !ux6DValid ||
            !ux6EValid || !ux6FValid || !ux6GValid || !ux6HValid || !ux6IValid || !ux6JValid || !ux6KValid ||
            !ux6LValid || !ux6MValid || !ux6NValid || !ux6OValid || !ux6PValid || !ux6QValid || !ux6RValid ||
            !ux6SValid || !ux6TValid || !ux6UValid || !ux6VValid || !ux6WValid || !ux6XValid || !ux6YValid ||
            !ux6ZValid || !ux7AValid || !ux7BValid || !ux7CValid || !ux7DValid || !ux7EValid || !ux7FValid ||
            !ux7GValid || !ux7HValid || !ux7IValid || !ux8AValid || !ux8BValid || !ux8CValid || !ux8DValid || !ux8EValid || !ux8FValid || !ux8GValid || !ux8HValid || !ux8IValid || !ux8JValid || !ux8KValid || !ux8LValid || !ux8MValid || !ux8NValid || !ux9AValid || !ux9BValid || !ux9CValid || !ux9DValid || !ux9EValid || !ux9FValid || !ux9GValid || !ux9HValid || !ux9IValid || !ux9JValid || !ux9KValid || !ux9LValid || !ux9MValid || !ux9NValid || !ux9OValid || !ux10Valid ||
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
                $"ux2navm={ux2NavMValid} ux3a={ux3AValid} ux3b={ux3BValid} ux3c={ux3CValid} ux3d={ux3DValid} ux3e={ux3EValid} ux3f={ux3FValid} ux3g={ux3GValid} ux3h={ux3HValid} ux3i={ux3IValid} " +
                $"ux4a={ux4AValid} ux4b={ux4BValid} ux4c={ux4CValid} ux4d={ux4DValid} ux4e={ux4EValid} ux4f={ux4FValid} ux4g={ux4GValid} ux4h={ux4HValid} ux4i={ux4IValid} ux4j={ux4JValid} ux4k={ux4KValid} ux4l={ux4LValid} ux4m={ux4MValid} ux4n={ux4NValid} ux4o={ux4OValid} ux4p={ux4PValid} ux4q={ux4QValid} ux4r={ux4RValid} ux4s={ux4SValid} ux4t={ux4TValid} ux4u={ux4UValid} ux5a={ux5AValid} ux5b={ux5BValid} ux6a={ux6AValid} ux6b={ux6BValid} ux6c={ux6CValid} ux6d={ux6DValid} ux6e={ux6EValid} ux6f={ux6FValid} ux6g={ux6GValid} ux6h={ux6HValid} ux6i={ux6IValid} ux6j={ux6JValid} ux6k={ux6KValid} ux6l={ux6LValid} ux6m={ux6MValid} ux6n={ux6NValid} ux6o={ux6OValid} ux6p={ux6PValid} ux6q={ux6QValid} ux6r={ux6RValid} ux6s={ux6SValid} ux6t={ux6TValid} ux6u={ux6UValid} ux6v={ux6VValid} ux6w={ux6WValid} ux6x={ux6XValid} ux6y={ux6YValid} ux6z={ux6ZValid} ux7a={ux7AValid} ux7b={ux7BValid} ux7c={ux7CValid} ux7d={ux7DValid} ux7e={ux7EValid} ux7f={ux7FValid} ux7g={ux7GValid} ux7h={ux7HValid} ux7i={ux7IValid} " +
                $"ux8a={ux8AValid} ux8b={ux8BValid} ux8c={ux8CValid} ux8d={ux8DValid} ux8e={ux8EValid} ux8f={ux8FValid} ux8g={ux8GValid} ux8h={ux8HValid} ux8i={ux8IValid} ux8j={ux8JValid} ux8k={ux8KValid} ux8l={ux8LValid} ux8m={ux8MValid} ux8n={ux8NValid} ux9a={ux9AValid} ux9b={ux9BValid} ux9c={ux9CValid} ux9d={ux9DValid} ux9e={ux9EValid} ux9f={ux9FValid} ux9g={ux9GValid} ux9h={ux9HValid} ux9i={ux9IValid} ux9j={ux9JValid} ux9k={ux9KValid} ux9l={ux9LValid} ux9m={ux9MValid} ux9n={ux9NValid} ux9o={ux9OValid} ux10={ux10Valid} candidate_approvals={candidateApprovalsValid} " +
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

        if (!string.IsNullOrWhiteSpace(_capturePageMatrixPath))
        {
            if (!await CapturePageMatrix())
            {
                GetTree().Quit(1);
                return;
            }
        }

        GD.Print(
            $"OPENRPG_FORGE_STUDIO_SHELL_PASS sections={_shell.SectionCount} pages={_shell.PageCount} " +
            $"windows=1 module={_session.Project.Module.Slug} areas={_session.Project.Areas.Count} " +
            $"entities={_shell.WorldViewport.EntityCount} assets={_shell.WorldViewport.AssetModelInstanceCount}");

        // Automated evidence can leave temporary workspaces and native controls alive until the
        // SceneTree teardown. Release them while the tree is still processing so Godot does not
        // race C# disposal against its native shutdown sequence.
        _shell.ReleaseUx4RAutomatedEvidence();
        _shell.ReleaseUx4SAutomatedEvidence();
        _shell.ReleaseUx4TAutomatedEvidence();
        _shell.ReleaseUx4UAutomatedEvidence();
        _shell.ReleaseUx3BAutomatedEvidence();
        _shell.ReleaseUx3CAutomatedEvidence();
        _shell.ReleaseUx3DAutomatedEvidence();
        _shell.ReleaseUx3EAutomatedEvidence();
        _shell.ReleaseUx3FAutomatedEvidence();
        _shell.ReleaseUx3GAutomatedEvidence();
        _shell.ReleaseUx3HAutomatedEvidence();
        _shell.ReleaseUx3IAutomatedEvidence();
        _shell.ReleaseUx7AAutomatedEvidence();
        _shell.ReleaseUx7BAutomatedEvidence();
        _shell.ReleaseUx7CAutomatedEvidence();
        _shell.ReleaseUx7DAutomatedEvidence();
        _shell.ReleaseUx7EAutomatedEvidence();
        _shell.ReleaseUx7FAutomatedEvidence();
        _shell.ReleaseUx7GAutomatedEvidence();
        _shell.ReleaseUx7HAutomatedEvidence();
        _shell.ReleaseUx7IAutomatedEvidence();
        _shell.ReleaseUx8AAutomatedEvidence();
        _shell.ReleaseUx8BAutomatedEvidence();
        _shell.ReleaseUx8CAutomatedEvidence();
        _shell.ReleaseUx8DAutomatedEvidence();
        _shell.ReleaseUx8EAutomatedEvidence();
        _shell.ReleaseUx8FAutomatedEvidence();
        _shell.ReleaseUx8GAutomatedEvidence();
        _shell.ReleaseUx8HAutomatedEvidence();
        _shell.ReleaseUx8IAutomatedEvidence();
        _shell.ReleaseUx8JAutomatedEvidence();
        _shell.ReleaseUx8KAutomatedEvidence();
        _shell.ReleaseUx8LAutomatedEvidence();
        _shell.ReleaseUx8MAutomatedEvidence();
        _shell.ReleaseUx8NAutomatedEvidence();
        _shell.ReleaseUx9AAutomatedEvidence();
        _shell.ReleaseUx9BAutomatedEvidence();
        _shell.ReleaseUx9CAutomatedEvidence();
        _shell.ReleaseUx9DAutomatedEvidence();
        _shell.ReleaseUx9EAutomatedEvidence();
        _shell.ReleaseUx9FAutomatedEvidence();
        _shell.ReleaseUx9GAutomatedEvidence();
        _shell.ReleaseUx9HAutomatedEvidence();
        _shell.ReleaseUx9IAutomatedEvidence();
        _shell.ReleaseUx9JAutomatedEvidence();
        _shell.ReleaseUx9KAutomatedEvidence();
        _shell.ReleaseUx9LAutomatedEvidence();
        _shell.ReleaseUx9MAutomatedEvidence();
        _shell.ReleaseUx9NAutomatedEvidence();
        _shell.ReleaseUx9OAutomatedEvidence();
        _shell.ReleaseUx10AutomatedEvidence();
        _shell.QueueFree();
        _shell = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _session.Close();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit();
    }

    private async Task RunDistributionSmoke()
    {
        if (_shell is null || _session.Project is null || _session.History is null)
        {
            GD.PrintErr("OPENRPG_FORGE_STUDIO_DISTRIBUTION_SMOKE_FAILED reason=session-unavailable");
            GetTree().Quit(1);
            return;
        }

        var shell = _shell;
        var originalProject = _session.Project;
        var originalProjectJson = ProjectJsonSerializer.Serialize(originalProject);
        var originalProjectFile = _session.Session!.ProjectFilePath;
        var originalProjectBytes = File.ReadAllBytes(originalProjectFile);
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var assetApproval = _session.CaptureAssetAuthoringApprovalUx3F();
        var dialogueApproval = _session.CaptureDialogueApprovalUx5A();
        var narrativeApproval = _session.CaptureNarrativeApprovalUx5B();
        var editValid = false;
        var ux0IValid = false;
        var ux2LwCValid = false;
        var ux3FValid = false;
        var ux5AValid = false;
        var ux5BValid = false;
        var failure = "none";

        try
        {
            editValid = shell.RunEditingSmoke();
            ux0IValid = shell.RunUx0ISmoke();
            ux2LwCValid = shell.RunUx2LwCSmoke();
            ux3FValid = shell.RunUx3FSmoke();
            ux5AValid = shell.RunUx5ASmoke();
            ux5BValid = shell.RunUx5BSmoke();
        }
        catch (Exception exception)
        {
            failure = exception.GetType().Name;
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_DISTRIBUTION_SMOKE_EXCEPTION type={failure}");
        }
        finally
        {
            _session.RestoreAssetAuthoringApprovalUx3F(assetApproval);
            _session.RestoreDialogueApprovalUx5A(dialogueApproval);
            _session.RestoreNarrativeApprovalUx5B(narrativeApproval);
            _ = _session.SelectEntities(originalSelection, originalSelected);
        }

        var projectReferenceUnchanged = ReferenceEquals(originalProject, _session.Project);
        var projectContentUnchanged = string.Equals(
            ProjectJsonSerializer.Serialize(_session.Project), originalProjectJson, StringComparison.Ordinal);
        var projectFileUnchanged = File.ReadAllBytes(originalProjectFile).SequenceEqual(originalProjectBytes);
        var contextRestored = _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected;
        var approvalsRestored = Equals(_session.CaptureAssetAuthoringApprovalUx3F(), assetApproval) &&
                                Equals(_session.CaptureDialogueApprovalUx5A(), dialogueApproval) &&
                                Equals(_session.CaptureNarrativeApprovalUx5B(), narrativeApproval);
        var sourceUnchanged = projectReferenceUnchanged && projectContentUnchanged && projectFileUnchanged &&
                              _session.IsDirty == originalDirty && contextRestored && approvalsRestored;
        var shellReady = shell.SectionCount == StudioPageCatalog.Sections.Count &&
                         shell.PageCount == StudioPageCatalog.Pages.Count;
        var valid = editValid && ux0IValid && ux2LwCValid && ux3FValid && ux5AValid && ux5BValid &&
                    sourceUnchanged && shellReady;
        var marker = valid
            ? "OPENRPG_FORGE_STUDIO_DISTRIBUTION_SMOKE_PASS"
            : "OPENRPG_FORGE_STUDIO_DISTRIBUTION_SMOKE_FAILED";
        GD.Print(
            $"{marker} sections={shell.SectionCount} pages={shell.PageCount} windows=1 " +
            $"module={_session.Project.Module.Slug} edit={editValid.ToString().ToLowerInvariant()} " +
            $"ux0i={ux0IValid.ToString().ToLowerInvariant()} " +
            $"ux2lwc={ux2LwCValid.ToString().ToLowerInvariant()} " +
            $"ux3f={ux3FValid.ToString().ToLowerInvariant()} " +
            $"ux5a={ux5AValid.ToString().ToLowerInvariant()} " +
            $"ux5b={ux5BValid.ToString().ToLowerInvariant()} " +
            $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} " +
            $"project_reference={projectReferenceUnchanged.ToString().ToLowerInvariant()} " +
            $"project_content={projectContentUnchanged.ToString().ToLowerInvariant()} " +
            $"project_file={projectFileUnchanged.ToString().ToLowerInvariant()} " +
            $"context_restored={contextRestored.ToString().ToLowerInvariant()} " +
            $"approvals_restored={approvalsRestored.ToString().ToLowerInvariant()} failure={failure}");

        shell.ReleaseUx3FAutomatedEvidence();
        shell.QueueFree();
        _shell = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _session.Close();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(valid ? 0 : 1);
    }

    private async Task<bool> CapturePageMatrix()
    {
        var directory = Path.GetFullPath(_capturePageMatrixPath!);
        Directory.CreateDirectory(directory);

        _ = await CaptureEvidenceImage(_shell!);
        var viewport = _shell!.GetViewport();
        var captured = 0;
        foreach (var page in StudioPageCatalog.Pages)
        {
            _shell.OpenPageForCapture(page.Id);
            var readinessFrames = 0;
            while (!_shell.IsPageReadyForCapture && readinessFrames < 600)
            {
                readinessFrames++;
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            if (!_shell.IsPageReadyForCapture)
            {
                GD.PrintErr($"OPENRPG_FORGE_STUDIO_PAGE_MATRIX_FAILED page={page.Id} reason=not-ready");
                return false;
            }
            for (var frame = 0; frame < 3; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            RenderingServer.ForceSync();

            var image = viewport.GetTexture().GetImage();
            var path = Path.Combine(directory, $"{captured + 1:00}-{page.Id}.png");
            if (image.SavePng(path) != Error.Ok)
            {
                GD.PrintErr($"OPENRPG_FORGE_STUDIO_PAGE_MATRIX_FAILED page={page.Id} path={path}");
                return false;
            }
            captured++;
            GD.Print(
                $"OPENRPG_FORGE_STUDIO_PAGE_CAPTURE_PASS page={page.Id} path={path} " +
                $"size={image.GetWidth()}x{image.GetHeight()}");
        }

        GD.Print(
            $"OPENRPG_FORGE_STUDIO_PAGE_MATRIX_PASS pages={captured} directory={directory} " +
            $"scale={_appliedUiScale:0.##}");
        return captured == StudioPageCatalog.Pages.Count;
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
            Size2DOverride = new Vector2I(
                Math.Max(2, (int)Math.Round(requested.X / _appliedUiScale, MidpointRounding.AwayFromZero)),
                Math.Max(2, (int)Math.Round(requested.Y / _appliedUiScale, MidpointRounding.AwayFromZero))),
            Size2DOverrideStretch = true,
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
