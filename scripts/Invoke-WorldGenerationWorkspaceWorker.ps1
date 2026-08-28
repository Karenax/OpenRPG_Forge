[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$WorkspaceRoot,

    [ValidateSet('godot-flow', 'gf-pcgodot')]
    [string]$GraphProvider = 'godot-flow',

    [string]$GodotExecutable,
    [switch]$BuildPcgFromSource,
    [switch]$RefreshDependencies
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$WorkspaceRoot = [System.IO.Path]::GetFullPath($WorkspaceRoot)
if (-not [System.IO.Path]::IsPathRooted($WorkspaceRoot) -or
    -not (Test-Path -LiteralPath (Join-Path $WorkspaceRoot 'project.json') -PathType Leaf)) {
    throw "Workspace '$WorkspaceRoot' is not an absolute OpenRPG workspace."
}

$candidateRepositoryRoot = Split-Path -Parent $PSScriptRoot
$sourceManifest = Join-Path $candidateRepositoryRoot 'third_party/world-generation.dependencies.json'
if (Test-Path -LiteralPath $sourceManifest -PathType Leaf) {
    $bundleRoot = $candidateRepositoryRoot
    $manifestPath = $sourceManifest
    $installer = Join-Path $candidateRepositoryRoot 'scripts/Install-WorldGenerationDependencies.ps1'
    $workerTemplate = Join-Path $candidateRepositoryRoot 'integrations/godot/world_generation_worker'
    $integrationSource = Join-Path $candidateRepositoryRoot 'integrations/godot/addons/openrpg_world_generation'
    $artifactBase = Join-Path $candidateRepositoryRoot 'artifacts/world-generation'
}
else {
    $bundleRoot = $PSScriptRoot
    $manifestPath = Join-Path $bundleRoot 'world-generation.dependencies.json'
    $installer = Join-Path $bundleRoot 'Install-WorldGenerationDependencies.ps1'
    $workerTemplate = Join-Path $bundleRoot 'worker-project'
    $integrationSource = Join-Path $bundleRoot 'addons/openrpg_world_generation'
    $artifactBase = Join-Path $bundleRoot 'artifacts'
}

foreach ($required in @($manifestPath, $installer, $workerTemplate, $integrationSource)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "World-generation worker component '$required' was not found."
    }
}

$dependencyManifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$terrainDependency = @($dependencyManifest.dependencies | Where-Object providerKey -eq 'terrain3d')
$graphDependency = @($dependencyManifest.dependencies | Where-Object providerKey -eq $GraphProvider)
if ($dependencyManifest.schemaVersion -ne 2 -or $terrainDependency.Count -ne 1 -or $graphDependency.Count -ne 1) {
    throw "Pinned dependency manifest does not define Terrain3D and '$GraphProvider' uniquely."
}

if ([string]::IsNullOrWhiteSpace($GodotExecutable)) {
    $engine = Get-Command 'Godot_v4.7.1-stable_mono_win64_console.exe' -ErrorAction SilentlyContinue
    if ($null -eq $engine) { $engine = Get-Command 'godot' -ErrorAction SilentlyContinue }
    if ($null -eq $engine) { $engine = Get-Command 'godot4' -ErrorAction SilentlyContinue }
    if ($null -eq $engine) {
        throw 'Godot 4.7.1 was not found. Pass -GodotExecutable explicitly.'
    }
    $GodotExecutable = $engine.Source
}
else {
    $GodotExecutable = [System.IO.Path]::GetFullPath($GodotExecutable)
}
if (-not (Test-Path -LiteralPath $GodotExecutable -PathType Leaf)) {
    throw "Godot executable '$GodotExecutable' was not found."
}
$versionOutput = @(& $GodotExecutable --version 2>&1 | ForEach-Object { [string]$_ })
if ($LASTEXITCODE -ne 0 -or ($versionOutput -join "`n") -notmatch '^4\.7\.1\.stable') {
    throw "World-generation worker requires Godot 4.7.1 stable; received '$($versionOutput -join ' ')'."
}

function Copy-Tree {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )
    [System.IO.Directory]::CreateDirectory($Destination) | Out-Null
    foreach ($entry in Get-ChildItem -LiteralPath $Source -Force) {
        Copy-Item -LiteralPath $entry.FullName -Destination $Destination -Recurse -Force
    }
}

function Test-ProviderCache {
    param([Parameter(Mandatory = $true)][string]$ProjectRoot)
    $receiptPath = Join-Path $ProjectRoot '.openrpg/world-generation/provider-installation.json'
    if (-not (Test-Path -LiteralPath $receiptPath -PathType Leaf)) { return $false }
    try {
        $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
        if ($receipt.schemaVersion -ne 1) { return $false }
        foreach ($dependency in @($terrainDependency[0], $graphDependency[0])) {
            $installed = @($receipt.providers | Where-Object providerKey -eq ([string]$dependency.providerKey))
            if ($installed.Count -ne 1) { return $false }
            $expectedRevision = if ($null -ne $dependency.PSObject.Properties['revision']) {
                [string]$dependency.revision
            }
            else { [string]$dependency.version }
            if ([string]$installed[0].versionOrRevision -ne $expectedRevision) { return $false }
            foreach ($evidence in @($installed[0].evidence)) {
                $relative = [string]$evidence.relativePath
                if ([System.IO.Path]::IsPathRooted($relative) -or $relative -match '(^|[\\/])\.\.([\\/]|$)') {
                    return $false
                }
                $path = [System.IO.Path]::GetFullPath((Join-Path $ProjectRoot $relative))
                $prefix = $ProjectRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
                    [System.IO.Path]::DirectorySeparatorChar
                if (-not $path.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase) -or
                    -not (Test-Path -LiteralPath $path -PathType Leaf)) {
                    return $false
                }
                $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
                if ($actual -ne [string]$evidence.sha256) { return $false }
            }
        }
        return $true
    }
    catch { return $false }
}

function Get-DurableJobs {
    param([Parameter(Mandatory = $true)][string]$Root)
    $jobsDirectory = Join-Path $Root 'world/generation/jobs'
    if (-not (Test-Path -LiteralPath $jobsDirectory -PathType Container)) { return @() }
    return @(
        Get-ChildItem -LiteralPath $jobsDirectory -Filter '*.json' -File |
            Sort-Object Name |
            ForEach-Object {
                $wrapper = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
                if ($null -ne $wrapper.job) {
                    [pscustomobject]@{ Path = $_.FullName; Job = $wrapper.job }
                }
            }
    )
}

function Invoke-GodotWorkerStep {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$LogPath
    )
    $output = @(& $GodotExecutable @Arguments 2>&1 | ForEach-Object { [string]$_ })
    $exitCode = $LASTEXITCODE
    [System.IO.File]::WriteAllLines($LogPath, $output)
    if ($exitCode -ne 0) {
        throw "Godot worker step '$Name' failed with exit code $exitCode. See '$LogPath'."
    }
    if (($output -join "`n") -match '(?m)^SCRIPT ERROR:') {
        throw "Godot worker step '$Name' emitted a script error. See '$LogPath'."
    }
    return ($output -join "`n")
}

$initialJobs = @(Get-DurableJobs -Root $WorkspaceRoot | Where-Object {
        [string]$_.Job.status -in @('queued', 'running')
    })
if ($initialJobs.Count -eq 0) {
    Write-Output "OPENRPG_FORGE_WORLD_GENERATION_WORKER_PASS provider=$GraphProvider jobs=0 workspace=$WorkspaceRoot"
    return
}

$cacheRoot = [System.IO.Path]::GetFullPath((Join-Path $artifactBase "worker-cache/$GraphProvider"))
$runId = [DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmssZ') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
$runRoot = [System.IO.Path]::GetFullPath((Join-Path $artifactBase "workers/$GraphProvider/$runId"))
[System.IO.Directory]::CreateDirectory($cacheRoot) | Out-Null
[System.IO.Directory]::CreateDirectory($runRoot) | Out-Null

$lockPath = Join-Path $cacheRoot '.worker.lock'
if (Test-Path -LiteralPath $lockPath -PathType Leaf) {
    $stalePid = 0
    try { $stalePid = [int](Get-Content -LiteralPath $lockPath -Raw) } catch { $stalePid = 0 }
    $activeOwner = if ($stalePid -gt 0) { Get-Process -Id $stalePid -ErrorAction SilentlyContinue } else { $null }
    if ($null -ne $activeOwner) {
        throw "World-generation worker cache is already owned by process $stalePid."
    }
    Remove-Item -LiteralPath $lockPath -Force
}
$lockStream = [System.IO.File]::Open($lockPath, [System.IO.FileMode]::CreateNew,
    [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
$lockWriter = [System.IO.StreamWriter]::new($lockStream)
$lockWriter.Write([System.Environment]::ProcessId)
$lockWriter.Flush()

$previousWorkspaceEnvironment = $env:OPENRPG_WORLD_GENERATION_WORKSPACE
try {
    Copy-Tree -Source $workerTemplate -Destination $cacheRoot
    Copy-Tree -Source $integrationSource -Destination (Join-Path $cacheRoot 'addons/openrpg_world_generation')

    $cacheValid = Test-ProviderCache -ProjectRoot $cacheRoot
    if ($RefreshDependencies -or -not $cacheValid) {
        $installerArguments = @{
            GodotProjectPath = $cacheRoot
            GraphProvider = $GraphProvider
            Force = $true
            BuildPcgFromSource = $BuildPcgFromSource
        }
        $installerOutput = @(& $installer @installerArguments 2>&1 | ForEach-Object { [string]$_ })
        [System.IO.File]::WriteAllLines((Join-Path $runRoot 'dependency-install.log'), $installerOutput)
        if (-not $?) {
            throw "Pinned provider installation failed. See '$(Join-Path $runRoot 'dependency-install.log')'."
        }
        if (-not (Test-ProviderCache -ProjectRoot $cacheRoot)) {
            throw 'Pinned provider receipt or evidence did not validate after installation.'
        }
    }

    $env:OPENRPG_WORLD_GENERATION_WORKSPACE = $WorkspaceRoot
    $importOutput = Invoke-GodotWorkerStep -Name 'editor-import' -LogPath (Join-Path $runRoot 'editor-import.log') `
        -Arguments @('--headless', '--editor', '--path', $cacheRoot, '--quit-after', '180')
    $workerOutput = Invoke-GodotWorkerStep -Name 'process-jobs' -LogPath (Join-Path $runRoot 'process-jobs.log') `
        -Arguments @('--editor', '--path', $cacheRoot,
            '--script', 'res://process_workspace_jobs.gd')
    if ($workerOutput -notmatch 'OPENRPG_FORGE_WORLD_GENERATION_GODOT_WORKER_PASS') {
        throw "Godot worker did not emit its success marker. See '$(Join-Path $runRoot 'process-jobs.log')'."
    }
    if ($workerOutput -notmatch 'Using Device:\s*.+') {
        throw "Godot worker did not initialize a real rendering device. Provider MultiMesh transforms cannot be qualified with the dummy headless renderer."
    }

    $finalJobs = @(Get-DurableJobs -Root $WorkspaceRoot)
    $proofs = @()
    foreach ($initial in $initialJobs) {
        $planId = [string]$initial.Job.planId
        $final = @($finalJobs | Where-Object { [string]$_.Job.planId -eq $planId })
        if ($final.Count -ne 1 -or [string]$final[0].Job.status -ne 'succeeded' -or
            @($final[0].Job.diagnostics).Count -ne 0) {
            throw "Durable world-generation job '$planId' did not finish successfully."
        }
        $requiresProviderCandidate = @($initial.Job.plan.steps | Where-Object {
                [string]$_.backend -in @('terrain3D', 'Terrain3D', 'pcgGodot', 'PcgGodot')
            }).Count -gt 0
        $candidateValidated = $false
        if ($requiresProviderCandidate) {
            $compactPlanId = $planId.Replace('-', '').ToLowerInvariant()
            $candidateRoot = Join-Path $WorkspaceRoot "world/generation/artifacts/$compactPlanId"
            $scenePath = Join-Path $candidateRoot 'derived-world.tscn'
            $reportPath = Join-Path $candidateRoot 'validation-report.json'
            if (-not (Test-Path -LiteralPath $scenePath -PathType Leaf) -or
                -not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
                throw "Job '$planId' produced no canonical-promotion candidate."
            }
            $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
            $providerKeys = @($report.providers | ForEach-Object { [string]$_.providerKey })
            $artisticRoles = @($report.artisticRoles.PSObject.Properties.Name)
            $candidateValidated = $report.validationSucceeded -eq $true -and
                $report.collisionValidated -eq $true -and $report.navigationValidated -eq $true -and
                [int]$report.artisticInstances -gt 0 -and
                @(@('biomes', 'roads', 'settlements', 'scatter') |
                    Where-Object { $_ -notin $artisticRoles }).Count -eq 0 -and
                $providerKeys -contains 'terrain3d' -and $providerKeys -contains $GraphProvider -and
                [string]$report.derivedSceneSha256 -eq
                    (Get-FileHash -LiteralPath $scenePath -Algorithm SHA256).Hash.ToLowerInvariant()
            if (-not $candidateValidated) {
                throw "Job '$planId' promotion candidate failed provider, collision, navigation, or SHA validation."
            }
            $sceneText = Get-Content -LiteralPath $scenePath -Raw
            $serializedMultiMeshes = @([regex]::Matches(
                    $sceneText,
                    '(?ms)^\[sub_resource type="MultiMesh".*?(?=^\[(?:sub_resource|node)|\z)'))
            $serializedBuffers = @($serializedMultiMeshes | Where-Object {
                    $_.Value -match '(?m)^instance_count\s*=\s*[1-9][0-9]*\s*$' -and
                    $_.Value -match '(?m)^buffer\s*=\s*PackedFloat32Array\('
                })
            if ($serializedMultiMeshes.Count -lt 4 -or $serializedBuffers.Count -ne $serializedMultiMeshes.Count) {
                throw "Job '$planId' did not serialize distributed transforms for every provider-neutral MultiMesh."
            }

            $reviewRoot = Join-Path $cacheRoot "candidate-review/$compactPlanId"
            $reviewCaptureRoot = Join-Path $reviewRoot 'captures'
            [System.IO.Directory]::CreateDirectory($reviewRoot) | Out-Null
            Copy-Item -LiteralPath $scenePath -Destination (Join-Path $reviewRoot 'derived-world.tscn') -Force
            $previousCaptureScene = $env:OPENRPG_WORLD_GENERATION_CAPTURE_SCENE
            $previousCaptureOutput = $env:OPENRPG_WORLD_GENERATION_CAPTURE_OUTPUT
            try {
                $env:OPENRPG_WORLD_GENERATION_CAPTURE_SCENE =
                    "res://candidate-review/$compactPlanId/derived-world.tscn"
                $env:OPENRPG_WORLD_GENERATION_CAPTURE_OUTPUT =
                    "res://candidate-review/$compactPlanId/captures"
                $captureOutput = Invoke-GodotWorkerStep -Name "capture-candidate-$compactPlanId" `
                    -LogPath (Join-Path $runRoot "capture-candidate-$compactPlanId.log") `
                    -Arguments @('--path', $cacheRoot, '--script', 'res://capture_candidate_scene.gd')
                if ($captureOutput -notmatch 'OPENRPG_FORGE_WORLD_GENERATION_CANDIDATE_CAPTURE_PASS' -or
                    $captureOutput -notmatch 'Using Device:\s*.+') {
                    throw "Job '$planId' did not produce a real-device candidate capture proof."
                }
            }
            finally {
                if ($null -eq $previousCaptureScene) {
                    Remove-Item Env:OPENRPG_WORLD_GENERATION_CAPTURE_SCENE -ErrorAction SilentlyContinue
                }
                else { $env:OPENRPG_WORLD_GENERATION_CAPTURE_SCENE = $previousCaptureScene }
                if ($null -eq $previousCaptureOutput) {
                    Remove-Item Env:OPENRPG_WORLD_GENERATION_CAPTURE_OUTPUT -ErrorAction SilentlyContinue
                }
                else { $env:OPENRPG_WORLD_GENERATION_CAPTURE_OUTPUT = $previousCaptureOutput }
            }
            $captureFiles = @(Get-ChildItem -LiteralPath $reviewCaptureRoot -Filter '*.png' -File |
                Sort-Object Name)
            if ($captureFiles.Count -ne 5) {
                throw "Job '$planId' candidate review did not emit five captures."
            }
            $proofCaptureRoot = Join-Path $runRoot "captures/$compactPlanId"
            Copy-Tree -Source $reviewCaptureRoot -Destination $proofCaptureRoot
        }
        $proofs += [ordered]@{
            planId = $planId
            status = 'succeeded'
            providerCandidateValidated = $candidateValidated
            captures = if ($requiresProviderCandidate) {
                @($captureFiles | ForEach-Object { "captures/$compactPlanId/$($_.Name)" })
            }
            else { @() }
        }
    }

    $result = [ordered]@{
        schemaVersion = 1
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        workspaceRoot = $WorkspaceRoot
        graphProvider = $GraphProvider
        godotVersion = ($versionOutput | Select-Object -First 1).Trim()
        providerCacheReused = $cacheValid -and -not $RefreshDependencies
        jobs = $proofs
        logs = [ordered]@{
            editorImport = 'editor-import.log'
            processJobs = 'process-jobs.log'
        }
    }
    $resultPath = Join-Path $runRoot 'workspace-worker-result.json'
    [System.IO.File]::WriteAllText(
        $resultPath,
        ($result | ConvertTo-Json -Depth 10),
        [System.Text.UTF8Encoding]::new($false))
    Write-Output "OPENRPG_FORGE_WORLD_GENERATION_WORKER_PASS provider=$GraphProvider jobs=$($proofs.Count) result=$resultPath"
}
finally {
    if ($null -eq $previousWorkspaceEnvironment) {
        Remove-Item Env:OPENRPG_WORLD_GENERATION_WORKSPACE -ErrorAction SilentlyContinue
    }
    else { $env:OPENRPG_WORLD_GENERATION_WORKSPACE = $previousWorkspaceEnvironment }
    $lockWriter.Dispose()
    $lockStream.Dispose()
    if (Test-Path -LiteralPath $lockPath -PathType Leaf) {
        Remove-Item -LiteralPath $lockPath -Force
    }
}
