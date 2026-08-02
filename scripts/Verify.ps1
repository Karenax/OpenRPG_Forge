[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'Common.ps1')

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot 'OpenRPGForge.sln'
$artifactRoot = Join-Path $repositoryRoot 'artifacts\verification'
$enginePath = Get-ForgeEnginePath

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null

function Invoke-CheckedDotnet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Invoke-CheckedPython {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & python @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "python $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Test-ForgeBranding {
    $projects = @(
        (Join-Path $repositoryRoot 'apps\Forge.Runtime'),
        (Join-Path $repositoryRoot 'apps\Forge.Toolset')
    )
    $expectedSignature = '137,80,78,71,13,10,26,10'

    foreach ($project in $projects) {
        $settingsPath = Join-Path $project 'project.godot'
        $imagePath = Join-Path $project 'branding\openrpg_forge_splash.png'
        $settings = Get-Content -LiteralPath $settingsPath -Raw
        if ($settings -notmatch 'boot_splash/image="res://branding/openrpg_forge_splash.png"') {
            throw "Forge branding is not configured in $settingsPath."
        }

        if (-not (Test-Path -LiteralPath $imagePath)) {
            throw "Forge splash image is missing: $imagePath."
        }

        $bytes = [System.IO.File]::ReadAllBytes($imagePath)
        if ($bytes.Length -lt 8 -or ($bytes[0..7] -join ',') -ne $expectedSignature) {
            throw "Forge splash image is not a valid PNG: $imagePath."
        }
    }

    Write-Output 'branding passed.'
}

function Invoke-ForgeEngineMarkerTest {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$ProjectPath,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string[]]$Markers,
        [Parameter(Mandatory)][bool]$StopAfterMarkers
    )

    $logPath = Join-Path $artifactRoot "$Name.log"
    if (Test-Path -LiteralPath $logPath) {
        Clear-Content -LiteralPath $logPath
    }

    $process = Start-Process `
        -FilePath $enginePath `
        -ArgumentList $Arguments `
        -WindowStyle Hidden `
        -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    $passed = $false

    do {
        Start-Sleep -Milliseconds 500
        if (Test-Path -LiteralPath $logPath) {
            $log = Get-Content -LiteralPath $logPath -Raw -ErrorAction SilentlyContinue
            $passed = @($Markers | Where-Object { $log -notmatch [regex]::Escape($_) }).Count -eq 0
            if ($passed -or $log -match 'OPENRPG_FORGE_.*_(FAILED|INVALID)') {
                break
            }
        }

        if ($process.HasExited -and -not $passed) {
            Start-Sleep -Milliseconds 500
        }
    } while ([DateTime]::UtcNow -lt $deadline)

    if ($StopAfterMarkers -or -not $process.HasExited) {
        $matchingProcesses = Get-CimInstance Win32_Process | Where-Object {
            $_.Name -like 'Godot_v4.7.1-stable_mono_win64*' -and
            $_.CommandLine -like "*$ProjectPath*"
        }
        foreach ($matchingProcess in $matchingProcesses) {
            Stop-Process -Id $matchingProcess.ProcessId -Force -ErrorAction SilentlyContinue
        }
    }

    $log = if (Test-Path -LiteralPath $logPath) {
        Get-Content -LiteralPath $logPath -Raw
    }
    else {
        ''
    }

    if (-not $passed) {
        throw "$Name did not emit all expected markers. See $logPath."
    }

    if ($log -match '(?m)^ERROR:|OPENRPG_FORGE_.*_(FAILED|INVALID)') {
        throw "$Name emitted an engine or startup error. See $logPath."
    }

    Write-Output "$Name passed."
}

Push-Location $repositoryRoot
try {
    Invoke-CheckedDotnet @('restore', $solutionPath, '--locked-mode')
    Invoke-CheckedDotnet @('format', $solutionPath, '--verify-no-changes', '--no-restore', '--verbosity', 'minimal')
    Invoke-CheckedDotnet @('build', $solutionPath, '--configuration', 'Release', '--no-restore')
    Invoke-CheckedDotnet @('test', $solutionPath, '--configuration', 'Release', '--no-build', '--logger', 'console;verbosity=minimal')
    & (Join-Path $repositoryRoot 'scripts\Update-AuroraParityTracker.ps1') -Check
    if ($LASTEXITCODE -ne 0) {
        throw "Aurora parity tracker freshness check failed with exit code $LASTEXITCODE."
    }
    Invoke-CheckedPython @('-m', 'unittest', 'discover', '-s', 'tests', '-p', 'test_*.py')
    Invoke-CheckedPython @('scripts/architecture_graph.py', 'check')
    Test-ForgeBranding

    $toolsetPath = Join-Path $repositoryRoot 'apps\Forge.Toolset'
    $toolsetLog = Join-Path $artifactRoot 'toolset.log'
    $prototypeDataPath = Join-Path $repositoryRoot 'apps\Forge.Runtime\content\crypt-of-ashes\project.json'
    $prototypeHashBefore = (Get-FileHash -LiteralPath $prototypeDataPath -Algorithm SHA256).Hash
    Invoke-ForgeEngineMarkerTest `
        -Name 'toolset' `
        -ProjectPath $toolsetPath `
        -Arguments @('--headless', '--editor', '--path', $toolsetPath, '--log-file', $toolsetLog, '--', '--workspace-smoke-test') `
        -Markers @('OPENRPG_FORGE_PLUGIN_LOADED', 'OPENRPG_FORGE_WINDOW_TITLE_PASS title=OpenRPG_Forge', 'OPENRPG_FORGE_TOOLSET_PROJECT_VALID entities=16', 'OPENRPG_FORGE_TOOLSET_PREVIEW_REBUILD_PASS entities=16', 'OPENRPG_FORGE_TOOLSET_G702_SHELL_PASS workspaces=9', 'OPENRPG_FORGE_TOOLSET_G703_BLUEPRINT_PASS catalog=10 derived=1 references=1 undo=restored', 'OPENRPG_FORGE_TOOLSET_G704_DIRECT_PASS drag=placed gizmo=moved invalid=blocked history=restored', 'OPENRPG_FORGE_TOOLSET_G705_AREA_PASS areas=2 documents=2 transition=valid active=switched history=restored', 'OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_PASS entities=semantic-copy area=full-clone uuids=remapped preview=available history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G707_SEARCH_PASS search=global', 'OPENRPG_FORGE_TOOLSET_G708_MODULE_PASS metadata=localized variables=typed', 'OPENRPG_FORGE_TOOLSET_G709_PALETTE_PASS standard=readonly project=editable generated=provenance favorites=filtered temporary=isolated format=0.9.0 history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G710_CAMERA_PASS orbit=mouse pan=mouse zoom=adaptive focus=selection views=orthographic pilot=wasd bookmarks=persisted history=dedicated project=unchanged', 'OPENRPG_FORGE_TOOLSET_G712_BUILD_PASS graph=versioned scripts=2 cache=source-api-compiler dependencies=automatic incremental=reused persistence=restored', 'OPENRPG_FORGE_TOOLSET_G713_DISTRIBUTION_PASS profiles=5 shipping=blocked licenses=diagnostic registry=versioned notices=generated history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G714_NAVIGATION_PASS agents=4 geometry=valid reachability=spawn-object doors=states offmesh=linked flight=profiled incremental=partial overlay=visible history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_T12_SMOKE_PASS format=0.9.0 areas=1 documents=1 entities=2', 'OPENRPG_FORGE_TOOLSET_T13_OVERRIDES_PASS schema=1 properties=3', 'OPENRPG_FORGE_TOOLSET_T42_DIALOGUE_PASS nodes=2 choices=3', 'OPENRPG_FORGE_TOOLSET_T43_QUEST_PASS states=3 transitions=2', 'OPENRPG_FORGE_TOOLSET_T23_SMOKE_PASS entities=16', 'OPENRPG_FORGE_TOOLSET_PROJECT_SAVED', 'OPENRPG_FORGE_TOOLSET_T32_SMOKE_PASS spawn=20000000-0000-4000-8000-000000000010 snapshot=deleted logs=captured') `
        -StopAfterMarkers $true
    $prototypeHashAfter = (Get-FileHash -LiteralPath $prototypeDataPath -Algorithm SHA256).Hash
    if ($prototypeHashBefore -ne $prototypeHashAfter) {
        throw 'The toolset smoke test did not restore the prototype project content.'
    }

    Write-Output 'toolset sample restored.'

    $forestMapLog = Join-Path $artifactRoot 'forest-map.log'
    Invoke-ForgeEngineMarkerTest `
        -Name 'forest-map' `
        -ProjectPath $toolsetPath `
        -Arguments @('--headless', '--path', $toolsetPath, '--log-file', $forestMapLog, 'res://addons/forge_forest_map/scenes/forge_forest_map_demo.tscn', '--', '--forest-map-smoke-test') `
        -Markers @('OPENRPG_FORGE_FOREST_MAP_SMOKE_PASS') `
        -StopAfterMarkers $false

    $moduleArtifact = Join-Path $artifactRoot 'crypt-of-ashes.orpmod'
    $moduleWorkspace = Join-Path $repositoryRoot 'apps\Forge.Runtime\content\crypt-of-ashes'
    $cliProject = Join-Path $repositoryRoot 'tools\Forge.Cli\Forge.Cli.csproj'
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'package', 'export', $moduleWorkspace, $moduleArtifact)
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'package', 'validate', $moduleArtifact)
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'package', 'analyze', $moduleWorkspace)

    $runtimePath = Join-Path $repositoryRoot 'apps\Forge.Runtime'
    $starterWorkspace = Join-Path $runtimePath 'content\broken-isles-starter'
    $starterArtifact = Join-Path $artifactRoot 'broken-isles-starter.orpmod'
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'package', 'export', $starterWorkspace, $starterArtifact)
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'package', 'validate', $starterArtifact)
    $starterLog = Join-Path $artifactRoot 'generic-starter.log'
    Invoke-ForgeEngineMarkerTest `
        -Name 'generic-starter' `
        -ProjectPath $runtimePath `
        -Arguments @('--headless', '--path', $runtimePath, '--log-file', $starterLog, '--', '--module-package', $starterArtifact, '--generic-smoke-test') `
        -Markers @('OPENRPG_FORGE_RUNTIME_MODULE_LOADED slug=les-iles-brisees', 'OPENRPG_FORGE_RUNTIME_LIFECYCLE_PASS events=3 order=ProjectLoaded>AreaEntered>WorldStarted authority=SinglePlayer', 'OPENRPG_FORGE_RUNTIME_PROJECT_LOADED entities=2 collisions=1', 'OPENRPG_FORGE_RUNTIME_GENERIC_SMOKE_PASS slug=les-iles-brisees entities=2 collisions=1 combat=false interaction=empty') `
        -StopAfterMarkers $false

    $mcpHostProject = Join-Path $repositoryRoot 'apps\Forge.McpHost\Forge.McpHost.csproj'
    Invoke-CheckedDotnet @(
        'run', '--project', $mcpHostProject, '--configuration', 'Release', '--no-build', '--',
        '--workspace', $moduleWorkspace, '--self-test')
    $mcpScenarioLog = Join-Path $artifactRoot 'mcp-scenario.log'
    Invoke-CheckedDotnet @(
        'run', '--project', $mcpHostProject, '--configuration', 'Release', '--no-build', '--',
        '--workspace', $moduleWorkspace, '--scenario-test', '--engine', $enginePath,
        '--runtime', $runtimePath, '--module', $moduleArtifact, '--artifact', $mcpScenarioLog)

    $runtimeSave = Join-Path $artifactRoot 'crypt-of-ashes.orpsave'
    $saveCreateLog = Join-Path $artifactRoot 'save-create.log'
    Invoke-ForgeEngineMarkerTest `
        -Name 'save-create' `
        -ProjectPath $runtimePath `
        -Arguments @('--headless', '--path', $runtimePath, '--log-file', $saveCreateLog, '--', '--module-package', $moduleArtifact, '--save-file', $runtimeSave, '--save-smoke-create') `
        -Markers @('OPENRPG_FORGE_FORGESCRIPT_CACHE status=', 'OPENRPG_FORGE_RUNTIME_SAVE_WRITTEN format=1.0.0', 'OPENRPG_FORGE_RUNTIME_SAVE_PASS') `
        -StopAfterMarkers $false
    $saveResumeLog = Join-Path $artifactRoot 'save-resume.log'
    Invoke-ForgeEngineMarkerTest `
        -Name 'save-resume' `
        -ProjectPath $runtimePath `
        -Arguments @('--headless', '--path', $runtimePath, '--log-file', $saveResumeLog, '--', '--module-package', $moduleArtifact, '--load-save', $runtimeSave, '--save-smoke-resume') `
        -Markers @('OPENRPG_FORGE_RUNTIME_SAVE_LOADED format=1.0.0', 'OPENRPG_FORGE_RUNTIME_SAVE_RESUME_PASS') `
        -StopAfterMarkers $false
}
finally {
    Pop-Location
}

Write-Output 'OPENRPG_FORGE_VERIFICATION=PASS'
