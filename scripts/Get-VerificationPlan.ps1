[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string[]]$ChangedPath,
    [string[]]$FeatureId,
    [ValidateSet('Development', 'Lot', 'Milestone', 'Publication')]
    [string]$Stage = 'Development',
    [ValidateSet('Auto', 'Core', 'Studio', 'Runtime', 'Assets', 'Network', 'Distribution', 'Full')]
    [string]$Profile = 'Auto',
    [string]$Page = 'zones',
    [string]$Evidence = 'studio-increment',
    [ValidateSet('Route', 'Wave', 'Full')]
    [string]$StudioSmokeScope = 'Route',
    [ValidateSet('Studio', 'Runtime', 'All')]
    [string]$DistributionProduct = 'All',
    [string[]]$DistributionDirectory,
    [switch]$NoGraph,
    [ValidateSet('Json', 'Object')]
    [string]$OutputFormat = 'Json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}
$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)

if (-not (Test-Path -LiteralPath (Join-Path $RepositoryRoot 'OpenRPGForge.sln') -PathType Leaf)) {
    throw "Repository root does not contain OpenRPGForge.sln: $RepositoryRoot"
}

$testProjects = [ordered]@{
    Application = 'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
    ArchitectureGraph = 'tests/Forge.ArchitectureGraph.Tests/Forge.ArchitectureGraph.Tests.csproj'
    Domain = 'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
    EngineAdapter = 'tests/Forge.EngineAdapter.Tests/Forge.EngineAdapter.Tests.csproj'
    LiveDirector = 'tests/Forge.LiveDirector.Tests/Forge.LiveDirector.Tests.csproj'
    Mcp = 'tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj'
    ModuleFormat = 'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
    Network = 'tests/Forge.Network.Tests/Forge.Network.Tests.csproj'
    RpgKit = 'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
    Scripting = 'tests/Forge.Scripting.Tests/Forge.Scripting.Tests.csproj'
    Studio = 'tests/Forge.Studio.Core.Tests/Forge.Studio.Core.Tests.csproj'
}

$steps = [System.Collections.Generic.List[object]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$classifications = [System.Collections.Generic.SortedSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
$stepKeys = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
$testSelections = @{}

function ConvertTo-RepositoryPath {
    param([Parameter(Mandatory)][string]$Path)

    $candidate = if ([System.IO.Path]::IsPathRooted($Path)) {
        [System.IO.Path]::GetFullPath($Path)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $Path))
    }
    $prefix = $RepositoryRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (-not $candidate.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase) -and
        -not $candidate.Equals($RepositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Changed path escapes the repository: $Path"
    }
    return [System.IO.Path]::GetRelativePath($RepositoryRoot, $candidate).Replace('\', '/')
}

function Get-WorkspaceFingerprint {
    param(
        [string[]]$Paths,
        [Parameter(Mandatory)][string]$Revision
    )

    $entries = [System.Collections.Generic.List[string]]::new()
    $entries.Add("HEAD=$Revision")
    foreach ($path in @($Paths | Sort-Object -Unique)) {
        $fullPath = Join-Path $RepositoryRoot $path
        if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
            $entries.Add("$path=$((Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant())")
        }
        elseif (Test-Path -LiteralPath $fullPath -PathType Container) {
            foreach ($file in Get-ChildItem -LiteralPath $fullPath -File -Recurse | Sort-Object FullName) {
                $relative = [System.IO.Path]::GetRelativePath($RepositoryRoot, $file.FullName).Replace('\', '/')
                $entries.Add("$relative=$((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant())")
            }
        }
        else {
            $entries.Add("$path=<missing>")
        }
    }
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($entries -join "`n")
    return [System.Convert]::ToHexString(
        [System.Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}

function New-VerificationStep {
    param(
        [Parameter(Mandatory)][string]$Id,
        [Parameter(Mandatory)][string]$Kind,
        [Parameter(Mandatory)][string]$Target,
        [string]$Configuration = '',
        [string]$Filter = '',
        [string[]]$Arguments = @(),
        [Parameter(Mandatory)][string]$Reason,
        [ValidateSet('low', 'medium', 'high')][string]$EstimatedCost = 'low',
        [string]$SideEffects = 'none',
        [string]$ExpectedMarker = '',
        [int]$TimeoutSeconds = 0
    )

    $key = "$Kind|$Target|$Configuration|$Filter|$($Arguments -join [char]31)"
    if (-not $stepKeys.Add($key)) {
        return
    }
    $steps.Add([pscustomobject][ordered]@{
        id = $Id
        kind = $Kind
        target = $Target.Replace('\', '/')
        configuration = $Configuration
        filter = $Filter
        arguments = @($Arguments)
        reason = $Reason
        estimated_cost = $EstimatedCost
        side_effects = $SideEffects
        expected_marker = $ExpectedMarker
        timeout_seconds = $TimeoutSeconds
    })
}

function Add-TestSelection {
    param(
        [Parameter(Mandatory)][string]$Project,
        [Parameter(Mandatory)][string]$Reason,
        [string[]]$TestFiles = @(),
        [switch]$WholeProject,
        [string]$Configuration = 'Debug'
    )

    if (-not $testSelections.ContainsKey($Project)) {
        $testSelections[$Project] = [ordered]@{
            reasons = [System.Collections.Generic.SortedSet[string]]::new([System.StringComparer]::Ordinal)
            classes = [System.Collections.Generic.SortedSet[string]]::new([System.StringComparer]::Ordinal)
            whole_project = $false
            configuration = $Configuration
        }
    }
    $selection = $testSelections[$Project]
    $null = $selection.reasons.Add($Reason)
    if ($WholeProject) {
        $selection.whole_project = $true
    }
    foreach ($testFile in $TestFiles) {
        if ($testFile -match '(?i)Tests?\.cs$') {
            $null = $selection.classes.Add([System.IO.Path]::GetFileNameWithoutExtension($testFile))
        }
    }
}

function Find-TestProjectForFile {
    param([Parameter(Mandatory)][string]$TestFile)

    $current = Split-Path -Parent (Join-Path $RepositoryRoot $TestFile)
    while (-not [string]::IsNullOrWhiteSpace($current) -and
        $current.StartsWith($RepositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        $project = Get-ChildItem -LiteralPath $current -Filter '*.csproj' -File -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($null -ne $project) {
            return [System.IO.Path]::GetRelativePath($RepositoryRoot, $project.FullName).Replace('\', '/')
        }
        $parent = Split-Path -Parent $current
        if ($parent -eq $current) { break }
        $current = $parent
    }
    return $null
}

function Find-RelatedTestFiles {
    param([string[]]$Paths, [string[]]$Features)

    $result = [System.Collections.Generic.SortedSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($path in $Paths) {
        if ($path -match '^tests/.+Tests?\.cs$') {
            $null = $result.Add($path)
        }
    }

    foreach ($feature in @($Features | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
        Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'tests') -Filter '*.cs' -File -Recurse |
            Select-String -SimpleMatch $feature -List |
            ForEach-Object {
                $null = $result.Add(
                    [System.IO.Path]::GetRelativePath($RepositoryRoot, $_.Path).Replace('\', '/'))
            }
    }

    if ($NoGraph) {
        return @($result | Sort-Object)
    }

    $graphProject = Join-Path $RepositoryRoot 'tools/Forge.ArchitectureGraph/Forge.ArchitectureGraph.csproj'
    if (-not (Test-Path -LiteralPath $graphProject -PathType Leaf)) {
        $warnings.Add('Architecture graph unavailable; project-level fallback was used.')
        return @($result | Sort-Object)
    }

    $symbols = @($Paths |
        Where-Object { $_ -match '\.cs$' -and $_ -notmatch '^tests/' } |
        ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_) } |
        Sort-Object -Unique)
    if ($symbols.Count -gt 12) {
        $warnings.Add('More than 12 production symbols changed; graph queries were skipped in favor of project suites.')
        return @($result | Sort-Object)
    }

    foreach ($symbol in $symbols) {
        $graphArguments = @(
            'run', '--no-build', '--project', $graphProject, '--',
            'query', $symbol, '--mode', 'tests', '--format', 'paths', '--max-nodes', '80')
        $graphOutput = @(& dotnet @graphArguments 2>$null)
        if ($LASTEXITCODE -ne 0) {
            $warnings.Add("Architecture graph query failed for '$symbol'; project-level fallback was used.")
            continue
        }
        foreach ($line in $graphOutput) {
            $candidate = ([string]$line).Trim().Replace('\', '/')
            if ($candidate -match '^tests/.+Tests?\.cs$') {
                $null = $result.Add($candidate)
            }
        }
    }
    return @($result | Sort-Object)
}

function Add-DotnetProjectSuite {
    param([string]$Project, [string]$Reason, [string]$Configuration = 'Debug')
    Add-TestSelection -Project $Project -Reason $Reason -WholeProject -Configuration $Configuration
}

function Add-ProfileSteps {
    param([Parameter(Mandatory)][string]$SelectedProfile)

    switch ($SelectedProfile) {
        'Core' {
            $null = $classifications.Add('core')
            New-VerificationStep -Id 'build-solution-release' -Kind 'dotnet-build' `
                -Target 'OpenRPGForge.sln' -Configuration 'Release' `
                -Reason 'Core profile compiles every managed boundary without engine smoke tests.' `
                -EstimatedCost high
            foreach ($project in $testProjects.Values) {
                Add-DotnetProjectSuite -Project $project -Reason 'Core profile managed regression suite.' `
                    -Configuration 'Release'
            }
            New-VerificationStep -Id 'python-unit' -Kind 'python-unittest' -Target 'tests' `
                -Arguments @('test_*.py') -Reason 'Core profile validates repository Python tooling.' `
                -EstimatedCost medium
            New-VerificationStep -Id 'python-asset-unit' -Kind 'python-unittest' `
                -Target 'tests/asset_pipeline' -Arguments @('test_*.py') `
                -Reason 'Asset pipeline discovery is separate in the canonical full gate.' -EstimatedCost medium
            foreach ($check in @(
                    @{ id = 'aurora-tracker'; path = 'scripts/Update-AuroraParityTracker.ps1'; args = @('-Check') },
                    @{ id = 'aurora-evidence'; path = 'scripts/Test-AuroraEvidencePaths.ps1'; args = @() },
                    @{ id = 'function-backlog'; path = 'scripts/Update-CompleteFunctionBacklog.ps1'; args = @('-Check') },
                    @{ id = 'studio-matrix'; path = 'scripts/Update-StudioAuroraAccessibilityPlan.ps1'; args = @('-Check') },
                    @{ id = 'targeted-verification-planner'; path = 'scripts/Test-TargetedVerificationPlanner.ps1'; args = @() })) {
                New-VerificationStep -Id $check.id -Kind 'script' -Target $check.path `
                    -Arguments $check.args -Reason 'Canonical generated planning evidence must remain fresh.'
            }
            New-VerificationStep -Id 'graph-full' -Kind 'graph-check' `
                -Target 'tools/Forge.ArchitectureGraph/Forge.ArchitectureGraph.csproj' `
                -Arguments @('--full') -Reason 'Core milestone checks the complete architecture contract.' `
                -EstimatedCost medium
        }
        'Studio' {
            $null = $classifications.Add('studio')
            New-VerificationStep -Id 'studio-debug-build' -Kind 'dotnet-build' `
                -Target 'apps/Forge.Studio/Forge.Studio.csproj' -Configuration 'Debug' `
                -Reason 'Studio smoke tests execute the Debug managed cache.' -EstimatedCost medium
            Add-DotnetProjectSuite -Project $testProjects.Studio `
                -Reason 'Studio profile validates engine-neutral presentation contracts.'
            New-VerificationStep -Id 'studio-scoped-smoke' -Kind 'studio-smoke' `
                -Target 'scripts/Open-Studio.ps1' `
                -Arguments @('-Page', $Page, '-Evidence', $Evidence, '-SmokeScope', $StudioSmokeScope) `
                -Reason "Studio $StudioSmokeScope smoke validates only the requested route or wave." `
                -EstimatedCost ($(if ($StudioSmokeScope -eq 'Full') { 'high' } elseif ($StudioSmokeScope -eq 'Wave') { 'medium' } else { 'low' })) `
                -SideEffects 'writes ignored Studio verification logs; source workspace is restored'
            New-VerificationStep -Id 'studio-matrix' -Kind 'script' `
                -Target 'scripts/Update-StudioAuroraAccessibilityPlan.ps1' -Arguments @('-Check') `
                -Reason 'Studio route metadata and accessibility matrix must remain fresh.'
        }
        'Runtime' {
            $null = $classifications.Add('runtime')
            New-VerificationStep -Id 'runtime-debug-build' -Kind 'dotnet-build' `
                -Target 'apps/Forge.Runtime/Forge.Runtime.csproj' -Configuration 'Debug' `
                -Reason 'Runtime smoke executes the Debug managed cache.' -EstimatedCost medium
            foreach ($project in @($testProjects.Domain, $testProjects.Application,
                    $testProjects.EngineAdapter, $testProjects.ModuleFormat, $testProjects.RpgKit,
                    $testProjects.Scripting)) {
                Add-DotnetProjectSuite -Project $project -Reason 'Runtime profile contract regression suite.'
            }
            New-VerificationStep -Id 'runtime-headless-smoke' -Kind 'engine-smoke' `
                -Target 'apps/Forge.Runtime' `
                -Arguments @('--headless', '--', '--smoke-test') `
                -Reason 'Runtime profile validates a real Godot startup and deterministic scenario.' `
                -EstimatedCost medium -SideEffects 'writes an ignored runtime verification log' `
                -ExpectedMarker 'OPENRPG_FORGE_RUNTIME_SMOKE_PASS' -TimeoutSeconds 120
        }
        'Assets' {
            $null = $classifications.Add('assets')
            foreach ($project in @($testProjects.Domain, $testProjects.Application, $testProjects.ModuleFormat)) {
                Add-DotnetProjectSuite -Project $project -Reason 'Asset contracts cross domain, application, and module serialization.'
            }
            New-VerificationStep -Id 'python-asset-unit' -Kind 'python-unittest' `
                -Target 'tests/asset_pipeline' -Arguments @('test_*.py') `
                -Reason 'Asset profile validates deterministic import and worker tooling.' -EstimatedCost medium
        }
        'Network' {
            $null = $classifications.Add('network')
            New-VerificationStep -Id 'server-release-build' -Kind 'dotnet-build' `
                -Target 'apps/Forge.Server/Forge.Server.csproj' -Configuration 'Release' `
                -Reason 'Network smoke runs the Release server host.' -EstimatedCost medium
            foreach ($project in @($testProjects.Network, $testProjects.LiveDirector, $testProjects.Mcp)) {
                Add-DotnetProjectSuite -Project $project -Reason 'Network profile contract regression suite.' `
                    -Configuration 'Release'
            }
            New-VerificationStep -Id 'network-beta-smoke' -Kind 'dotnet-run' `
                -Target 'apps/Forge.Server/Forge.Server.csproj' -Configuration 'Release' `
                -Arguments @('--network-beta-smoke-test') `
                -Reason 'Network profile validates the real server transport scenario.' -EstimatedCost medium `
                -ExpectedMarker 'OPENRPG_FORGE_NETWORK_BETA=PASS' -TimeoutSeconds 120
        }
        'Distribution' {
            $null = $classifications.Add('distribution')
            $directories = @($DistributionDirectory | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
            if ($directories.Count -eq 0) {
                if ($DistributionProduct -in @('Studio', 'All')) {
                    $directories += 'artifacts/distribution/studio-windows-x64'
                }
                if ($DistributionProduct -in @('Runtime', 'All')) {
                    $directories += 'artifacts/distribution/runtime-windows-x64'
                }
            }
            foreach ($directory in $directories) {
                $resolvedDirectory = if ([System.IO.Path]::IsPathRooted($directory)) {
                    [System.IO.Path]::GetFullPath($directory)
                }
                else {
                    [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $directory))
                }
                $manifest = Join-Path $resolvedDirectory 'distribution-manifest.json'
                if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) {
                    throw "Distribution profile requires an existing manifest: $manifest"
                }
                $repositoryPrefix = $RepositoryRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
                $directoryArgument = if ($resolvedDirectory.StartsWith(
                        $repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                    [System.IO.Path]::GetRelativePath($RepositoryRoot, $resolvedDirectory).Replace('\', '/')
                }
                else { $resolvedDirectory }
                $product = (Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json).product
                $script = switch ($product) {
                    'OpenRPG Forge Studio' { 'scripts/Verify-StudioDistribution.ps1' }
                    'OpenRPG Forge Runtime' { 'scripts/Verify-RuntimeDistribution.ps1' }
                    default { throw "Unsupported distribution product '$product' in $manifest" }
                }
                New-VerificationStep -Id "distribution-$([System.IO.Path]::GetFileName($resolvedDirectory))" `
                    -Kind 'script' -Target $script -Arguments @('-DistributionDirectory', $directoryArgument) `
                    -Reason 'Distribution profile verifies manifest, hashes, required payload, and compliance.' `
                    -EstimatedCost medium
            }
        }
        'Full' {
            $null = $classifications.Add('full')
            New-VerificationStep -Id 'full-gate' -Kind 'full-gate' -Target 'scripts/Verify.ps1' `
                -Reason 'Publication or explicit Full profile requires the canonical transversal gate.' `
                -EstimatedCost high -SideEffects 'writes verification artifacts and exercises every product surface'
        }
        default { throw "Unknown verification profile: $SelectedProfile" }
    }
}

function Add-ChangedPathMapping {
    param([Parameter(Mandatory)][string]$Path, [string[]]$RelatedTests)

    $wholeProject = $Stage -ne 'Development'
    $matchingTests = @($RelatedTests |
        Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) } |
        Where-Object {
            $project = Find-TestProjectForFile $_
            $null -ne $project
        })

    if ($Path -match '^tests/.+Tests?\.cs$') {
        $project = Find-TestProjectForFile $Path
        if ($null -ne $project) {
            Add-TestSelection -Project $project -Reason "Changed test file: $Path" `
                -TestFiles @($Path) -WholeProject:$wholeProject
        }
        return
    }

    $mappings = @(
        @{ pattern = '^src/Forge\.Domain/'; key = 'Domain'; class = 'domain' },
        @{ pattern = '^src/Forge\.Application/'; key = 'Application'; class = 'application' },
        @{ pattern = '^src/Forge\.ModuleFormat/'; key = 'ModuleFormat'; class = 'module-format' },
        @{ pattern = '^src/Forge\.EngineAdapter/'; key = 'EngineAdapter'; class = 'engine-adapter' },
        @{ pattern = '^src/Forge\.Scripting/|^native/'; key = 'Scripting'; class = 'scripting' },
        @{ pattern = '^src/Forge\.Network/'; key = 'Network'; class = 'network' },
        @{ pattern = '^src/Forge\.LiveDirector/'; key = 'LiveDirector'; class = 'live-director' },
        @{ pattern = '^src/Forge\.Mcp/|^apps/Forge\.McpHost/'; key = 'Mcp'; class = 'mcp' },
        @{ pattern = '^src/Forge\.RpgKit'; key = 'RpgKit'; class = 'rpg-kit' },
        @{ pattern = '^src/Forge\.Studio\.Core/|^apps/Forge\.Studio/'; key = 'Studio'; class = 'studio' },
        @{ pattern = '^tools/Forge\.ArchitectureGraph/'; key = 'ArchitectureGraph'; class = 'architecture-graph' })

    foreach ($mapping in $mappings) {
        if ($Path -match $mapping.pattern) {
            $null = $classifications.Add($mapping.class)
            $project = $testProjects[$mapping.key]
            $projectTests = @($matchingTests | Where-Object { (Find-TestProjectForFile $_) -eq $project })
            Add-TestSelection -Project $project -Reason "Changed production boundary: $Path" `
                -TestFiles $projectTests -WholeProject:$wholeProject
        }
    }

    if ($Path -match '^apps/Forge\.Runtime/') {
        $null = $classifications.Add('runtime')
        foreach ($key in @('Domain', 'Application', 'EngineAdapter')) {
            $project = $testProjects[$key]
            $projectTests = @($matchingTests | Where-Object { (Find-TestProjectForFile $_) -eq $project })
            Add-TestSelection -Project $project -Reason "Runtime host changed: $Path" `
                -TestFiles $projectTests -WholeProject:$wholeProject
        }
        New-VerificationStep -Id 'runtime-debug-build' -Kind 'dotnet-build' `
            -Target 'apps/Forge.Runtime/Forge.Runtime.csproj' -Configuration 'Debug' `
            -Reason 'Runtime host changes require a fresh Godot managed cache.' -EstimatedCost medium
    }
    if ($Path -match '^apps/Forge\.Server/') {
        $null = $classifications.Add('network')
        foreach ($key in @('Network', 'LiveDirector')) {
            Add-TestSelection -Project $testProjects[$key] -Reason "Server host changed: $Path" `
                -WholeProject:$wholeProject -TestFiles $matchingTests
        }
        New-VerificationStep -Id 'server-release-build' -Kind 'dotnet-build' `
            -Target 'apps/Forge.Server/Forge.Server.csproj' -Configuration 'Release' `
            -Reason 'Server host changes require a real host build.' -EstimatedCost medium
    }
    if ($Path -match '^apps/Forge\.Toolset/') {
        $null = $classifications.Add('toolset')
        foreach ($key in @('Application', 'EngineAdapter', 'ModuleFormat')) {
            Add-TestSelection -Project $testProjects[$key] -Reason "Legacy Creator host changed: $Path" `
                -WholeProject:$wholeProject -TestFiles $matchingTests
        }
        New-VerificationStep -Id 'toolset-debug-build' -Kind 'dotnet-build' `
            -Target 'apps/Forge.Toolset/Forge.Toolset.csproj' -Configuration 'Debug' `
            -Reason 'Toolset host changes require a fresh Godot managed cache.' -EstimatedCost medium
    }

    if ($Path -match '^apps/Forge\.Studio/') {
        New-VerificationStep -Id 'studio-debug-build' -Kind 'dotnet-build' `
            -Target 'apps/Forge.Studio/Forge.Studio.csproj' -Configuration 'Debug' `
            -Reason 'Studio host changes require a fresh Godot managed cache.' -EstimatedCost medium
        New-VerificationStep -Id 'studio-route-smoke' -Kind 'studio-smoke' `
            -Target 'scripts/Open-Studio.ps1' `
            -Arguments @('-Page', $Page, '-Evidence', $Evidence, '-SmokeScope', $StudioSmokeScope) `
            -Reason 'Studio host changes require the closest route or wave smoke.' `
            -EstimatedCost ($(if ($StudioSmokeScope -eq 'Route') { 'low' } else { 'medium' })) `
            -SideEffects 'writes ignored Studio verification logs; source workspace is restored'
    }
    if ($Path -match '^tools/asset_pipeline/|^tests/asset_pipeline/|^asset_manifests/') {
        $null = $classifications.Add('assets')
        New-VerificationStep -Id 'python-asset-unit' -Kind 'python-unittest' `
            -Target 'tests/asset_pipeline' -Arguments @('test_*.py') `
            -Reason "Asset pipeline path changed: $Path" -EstimatedCost medium
    }
    if ($Path -match '\.py$' -and $Path -notmatch '^tests/asset_pipeline/') {
        $null = $classifications.Add('python')
        New-VerificationStep -Id 'python-unit' -Kind 'python-unittest' -Target 'tests' `
            -Arguments @('test_*.py') -Reason "Python path changed: $Path" -EstimatedCost medium
    }
    if ($Path -match '\.ps1$') {
        $null = $classifications.Add('powershell')
        New-VerificationStep -Id "syntax-$([System.IO.Path]::GetFileNameWithoutExtension($Path))" `
            -Kind 'powershell-syntax' -Target $Path -Reason 'Changed PowerShell must parse before execution.'
    }
    if ($Path -match '^scripts/(Get-VerificationPlan|Invoke-TargetedVerification|Test-TargetedVerificationPlanner|Verify)\.ps1$') {
        $null = $classifications.Add('verification-infrastructure')
        New-VerificationStep -Id 'targeted-verification-planner' -Kind 'script' `
            -Target 'scripts/Test-TargetedVerificationPlanner.ps1' `
            -Reason 'Verification infrastructure changes must preserve deterministic routing and fail-closed policies.'
    }
    if ($Path -match '^scripts/(Open-Studio|Verify-StudioIncrement)\.ps1$') {
        $null = $classifications.Add('studio')
        Add-TestSelection -Project $testProjects.Studio `
            -Reason "Studio verification entry point changed: $Path" -WholeProject:($Stage -ne 'Development')
        New-VerificationStep -Id 'studio-debug-build' -Kind 'dotnet-build' `
            -Target 'apps/Forge.Studio/Forge.Studio.csproj' -Configuration 'Debug' `
            -Reason 'Studio verification entry points require a fresh managed cache.' -EstimatedCost medium
        New-VerificationStep -Id 'studio-route-smoke' -Kind 'studio-smoke' `
            -Target 'scripts/Open-Studio.ps1' `
            -Arguments @('-Page', $Page, '-Evidence', $Evidence, '-SmokeScope', $StudioSmokeScope) `
            -Reason 'Studio verification entry point changes require a real scoped smoke.' `
            -EstimatedCost low -SideEffects 'writes ignored Studio verification logs; source workspace is restored'
    }
    if ($Path -match '^tracking/project/AURORA_PARITY_COVERAGE\.csv$') {
        New-VerificationStep -Id 'aurora-tracker' -Kind 'script' `
            -Target 'scripts/Update-AuroraParityTracker.ps1' -Arguments @('-Check') `
            -Reason 'Aurora tracker edits must match generated coverage.'
    }
    if ($Path -match '^tracking/studio/|^apps/Forge\.Studio/') {
        New-VerificationStep -Id 'studio-matrix' -Kind 'script' `
            -Target 'scripts/Update-StudioAuroraAccessibilityPlan.ps1' -Arguments @('-Check') `
            -Reason 'Studio tracking and route metadata must remain synchronized.'
    }
    if ($Path -match '^docs/planning/COMPLETE_FUNCTION_BACKLOG\.md$|^tracking/project/AURORA_FEATURE_IMPLEMENTATION_AUDIT\.csv$') {
        New-VerificationStep -Id 'function-backlog' -Kind 'script' `
            -Target 'scripts/Update-CompleteFunctionBacklog.ps1' -Arguments @('-Check') `
            -Reason 'Canonical function backlog must remain generated from the tracker.'
    }
    if ($Path -match '^(OpenRPGForge\.sln|Directory\.Build\.props|global\.json|.+\.csproj|.+packages\.lock\.json)$') {
        $null = $classifications.Add('build-system')
        New-VerificationStep -Id 'build-solution-release' -Kind 'dotnet-build' `
            -Target 'OpenRPGForge.sln' -Configuration 'Release' `
            -Reason "Build topology changed: $Path" -EstimatedCost high
        if ($Stage -eq 'Development') {
            $warnings.Add('Build topology changed; use -Stage Lot or -Profile Core before closing the lot.')
        }
    }
}

if ($Stage -eq 'Publication') {
    $Profile = 'Full'
}

if ($Profile -ne 'Auto') {
    Add-ProfileSteps -SelectedProfile $Profile
    $normalizedPaths = @($ChangedPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object { ConvertTo-RepositoryPath $_ } | Sort-Object -Unique)
}
else {
    if ($null -eq $ChangedPath -or $ChangedPath.Count -eq 0) {
        Push-Location $RepositoryRoot
        try {
            $ChangedPath = @(
                @(& git diff --name-only --relative HEAD) +
                @(& git ls-files --others --exclude-standard)
            ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        }
        finally {
            Pop-Location
        }
    }
    $normalizedPaths = @($ChangedPath | ForEach-Object { ConvertTo-RepositoryPath $_ } | Sort-Object -Unique)
    if ($normalizedPaths.Count -eq 0) {
        throw 'Auto planning requires changed paths. Pass -ChangedPath or select an explicit -Profile.'
    }
    $relatedTests = @(Find-RelatedTestFiles -Paths $normalizedPaths -Features $FeatureId)
    foreach ($path in $normalizedPaths) {
        Add-ChangedPathMapping -Path $path -RelatedTests $relatedTests
    }
    foreach ($testFile in $relatedTests) {
        $project = Find-TestProjectForFile $testFile
        if ($null -ne $project) {
            Add-TestSelection -Project $project -Reason 'Architecture graph or feature identifier selected this test.' `
                -TestFiles @($testFile) -WholeProject:($Stage -ne 'Development')
        }
    }

    if ($Stage -eq 'Milestone') {
        $profiles = [System.Collections.Generic.SortedSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        if ($classifications.Contains('studio')) { $null = $profiles.Add('Studio') }
        if ($classifications.Contains('runtime') -or $classifications.Contains('engine-adapter')) { $null = $profiles.Add('Runtime') }
        if ($classifications.Contains('assets')) { $null = $profiles.Add('Assets') }
        if ($classifications.Contains('network') -or $classifications.Contains('live-director')) { $null = $profiles.Add('Network') }
        if ($profiles.Count -eq 0) { $null = $profiles.Add('Core') }
        foreach ($selected in $profiles) { Add-ProfileSteps -SelectedProfile $selected }
        New-VerificationStep -Id 'graph-generate' -Kind 'graph-generate' `
            -Target 'tools/Forge.ArchitectureGraph/Forge.ArchitectureGraph.csproj' `
            -Reason 'Milestone source changes refresh generated architecture evidence.' `
            -EstimatedCost medium -SideEffects 'updates generated docs/architecture artifacts'
        New-VerificationStep -Id 'graph-full' -Kind 'graph-check' `
            -Target 'tools/Forge.ArchitectureGraph/Forge.ArchitectureGraph.csproj' `
            -Arguments @('--full') -Reason 'Milestone closes with the full architecture invariant set.' `
            -EstimatedCost medium
    }
    elseif (-not $NoGraph -and @($normalizedPaths | Where-Object { $_ -match '\.(cs|csproj|sln)$' }).Count -gt 0) {
        New-VerificationStep -Id 'graph-fast' -Kind 'graph-check' `
            -Target 'tools/Forge.ArchitectureGraph/Forge.ArchitectureGraph.csproj' `
            -Reason 'Managed changes require a fast graph freshness and invariant check.'
    }

    $textPaths = @($normalizedPaths | Where-Object {
            $_ -match '\.(md|txt|csv|json|ya?ml)$' -or
            $_ -match '(^|/)(README|STATUS|TASKS_MVP|PROJECT_CONTEXT)$'
        })
    if ($textPaths.Count -gt 0) {
        New-VerificationStep -Id 'text-files' -Kind 'text-check' -Target '.' `
            -Arguments $textPaths -Reason 'Changed text evidence must be UTF-8 readable and free of whitespace or merge-marker defects.'
    }
}

foreach ($project in @($testSelections.Keys | Sort-Object)) {
    $selection = $testSelections[$project]
    $filter = ''
    if (-not $selection.whole_project -and $selection.classes.Count -gt 0) {
        $filter = @($selection.classes | ForEach-Object { "FullyQualifiedName~$_" }) -join '|'
    }
    $name = [System.IO.Path]::GetFileNameWithoutExtension($project).Replace('.', '-').ToLowerInvariant()
    New-VerificationStep -Id "tests-$name" -Kind 'dotnet-test' -Target $project `
        -Configuration $selection.configuration -Filter $filter `
        -Reason (@($selection.reasons) -join ' ') `
        -EstimatedCost ($(if ([string]::IsNullOrWhiteSpace($filter)) { 'medium' } else { 'low' }))
}

if ($steps.Count -eq 0) {
    throw 'No verification step was selected. Add an explicit mapping or choose a profile; zero-work plans are forbidden.'
}

Push-Location $RepositoryRoot
try {
    $revision = (& git rev-parse HEAD).Trim()
}
finally {
    Pop-Location
}
$workspaceFingerprint = Get-WorkspaceFingerprint -Paths $normalizedPaths -Revision $revision

$phaseOrder = @{
    'powershell-syntax' = 10
    'text-check' = 10
    'dotnet-build' = 20
    'dotnet-test' = 30
    'python-unittest' = 30
    'script' = 40
    'studio-smoke' = 50
    'engine-smoke' = 50
    'dotnet-run' = 50
    'graph-generate' = 60
    'graph-check' = 70
    'full-gate' = 80
}
$orderedSteps = @($steps | Sort-Object `
        @{ Expression = { $phaseOrder[[string]$_.kind] } },
        @{ Expression = { [string]$_.id } })

$plan = [pscustomobject][ordered]@{
    schema_version = 1
    repository = $RepositoryRoot
    revision = $revision
    workspace_fingerprint = $workspaceFingerprint
    stage = $Stage
    profile = $Profile
    changed_paths = @($normalizedPaths)
    feature_ids = @($FeatureId | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
    classifications = @($classifications)
    warnings = @($warnings | Sort-Object -Unique)
    policy = [pscustomobject][ordered]@{
        sequential = $true
        fail_on_zero_tests = $true
        fail_fast = $true
        full_gate = $Profile -eq 'Full'
    }
    steps = $orderedSteps
}

if ($OutputFormat -eq 'Object') {
    Write-Output $plan
}
else {
    $plan | ConvertTo-Json -Depth 10
}
