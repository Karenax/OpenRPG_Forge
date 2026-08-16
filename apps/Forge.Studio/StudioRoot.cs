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
        var worldValid = _shell.WorldViewport.EntityCount > 0 && _session.Project.Areas.Count > 0;
        if (!shellValid || !worldValid || !editValid || !ux0AValid || !ux0BValid || !ux0CValid || !ux0DValid ||
            !ux0EValid || !ux0FValid || !ux0GValid || !ux0HValid || !ux0IValid || !ux0JValid || !ux1AValid ||
            !ux1BValid || !ux1CValid || !ux1DValid || !ux1EValid || !ux1FValid || !ux1GValid || !ux1HValid ||
            !ux1IValid || !ux1JValid || !ux1KValid || !ux1LValid || !ux1MValid || !ux1NValid || !ux1OValid ||
            !ux1PValid || !ux1QValid || !ux1RValid || !ux1SValid || !ux1TValid || !ux1UValid || !ux1VValid ||
            !ux1WValid || !ux1XValid || !ux1YValid || !ux1ZValid || !ux1AAValid || !ux1ABValid || !ux1ACValid ||
            !ux1ADValid || !ux1AEValid || !ux1AFValid ||
            !evidenceValid)
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
                $"ux1af={ux1AFValid} " +
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
