#requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$RepoRoot,
    [Parameter(Mandatory)][string]$CaptureRoot,
    [Parameter(Mandatory)][string]$OutputRoot,
    [Parameter(Mandatory)][string]$RenderDocRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-Replay {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Assert-PlainPath {
    param([string]$Path)
    $cursor = [IO.Path]::GetFullPath($Path)
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            $item = Get-Item -LiteralPath $cursor -Force
            Assert-Replay (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) "Reparse point forbidden: $cursor"
        }
        $parent = [IO.Directory]::GetParent($cursor)
        $cursor = if ($parent) { $parent.FullName } else { $null }
    }
}

function Assert-PlainTree {
    param([string]$Path)
    Assert-PlainPath $Path
    foreach ($item in @(Get-ChildItem -LiteralPath $Path -Force -Recurse)) {
        Assert-Replay (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) "Reparse point forbidden: $($item.FullName)"
    }
}

function Get-ReplayHash {
    param([string]$Path)
    Assert-PlainPath $Path
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-ReplayTextHash {
    param([string]$Value)
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Value))).ToLowerInvariant()
}

function Test-ReplayInside {
    param([string]$Root, [string]$Path)
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    return [IO.Path]::GetFullPath($Path).StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)
}

function Write-ReplayJson {
    param([string]$Path, [object]$Value)
    # Every output is owned by this invocation; never replace an existing proof.
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes(($Value | ConvertTo-Json -Depth 20))
        $stream.Write($bytes, 0, $bytes.Length)
    }
    finally { $stream.Dispose() }
}

function Get-ReplayProfileFingerprint {
    param([string]$Path)
    Assert-PlainPath $Path
    if (-not (Test-Path -LiteralPath $Path)) { return 'absent' }
    Assert-Replay ([IO.Directory]::Exists($Path)) "Profile is not a directory: $Path"
    Assert-PlainTree $Path
    $records = @('D|.') + @(
        Get-ChildItem -LiteralPath $Path -Force -Recurse | Sort-Object FullName | ForEach-Object {
            $relative = [IO.Path]::GetRelativePath($Path, $_.FullName).Replace('\', '/')
            if ($_.PSIsContainer) { "D|$relative" }
            else { "F|$relative|$($_.Length)|$(Get-ReplayHash $_.FullName)" }
        }
    )
    return Get-ReplayTextHash ($records -join "`n")
}

function Remove-ReplayOwnedTree {
    param([string]$Root, [string]$Path, [string]$Prefix)
    $absolute = [IO.Path]::GetFullPath($Path)
    $parent = [IO.Directory]::GetParent($absolute)
    Assert-Replay ($null -ne $parent -and $parent.FullName -ieq [IO.Path]::GetFullPath($Root)) "Cleanup target is not an immediate child: $absolute"
    Assert-Replay ((Test-ReplayInside $Root $absolute) -and [IO.Path]::GetFileName($absolute).StartsWith($Prefix, [StringComparison]::Ordinal)) "Cleanup target outside owned scope: $absolute"
    if (Test-Path -LiteralPath $absolute) {
        Assert-Replay ([IO.Directory]::Exists($absolute)) "Cleanup target is not a directory: $absolute"
        Assert-PlainTree $absolute
        Remove-Item -LiteralPath $absolute -Recurse -Force
    }
    Assert-Replay (-not (Test-Path -LiteralPath $absolute)) "Cleanup incomplete: $absolute"
}

function Assert-ReplayBundle {
    param([string]$Root, [object[]]$Inventory, [string]$ExtraExecutable = '')
    Assert-PlainTree $Root
    $expected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    [long]$total = 0
    foreach ($entry in $Inventory) {
        Assert-Replay ($entry.path -cmatch '^extract/RenderDoc_1\.46_64/([^:]+)$') 'Unexpected bundle inventory path'
        $relative = $Matches[1]
        Assert-Replay ($relative -notmatch '(^|[/\\])\.{1,2}([/\\]|$)' -and -not [IO.Path]::IsPathRooted($relative)) 'Unsafe bundle inventory path'
        Assert-Replay ($expected.Add($relative.Replace('\', '/'))) 'Duplicate bundle inventory path'
        $file = Join-Path $Root $relative
        Assert-Replay ((Test-ReplayInside $Root $file) -and [IO.File]::Exists($file)) "Missing bundle file: $relative"
        Assert-Replay ((Get-Item -LiteralPath $file).Length -eq $entry.size -and (Get-ReplayHash $file) -ceq ([string]$entry.sha256).ToLowerInvariant()) "Bundle content mismatch: $relative"
        $total += $entry.size
    }
    Assert-Replay ($Inventory.Count -eq 72 -and $total -eq 210237254) 'Pinned bundle count/size mismatch'
    if ($ExtraExecutable) { Assert-Replay ($expected.Add($ExtraExecutable)) 'Duplicate execution filename' }
    $files = @(Get-ChildItem -LiteralPath $Root -File -Force -Recurse)
    Assert-Replay ($files.Count -eq $expected.Count) 'Unexpected bundle file count'
    foreach ($file in $files) {
        Assert-Replay ($expected.Contains([IO.Path]::GetRelativePath($Root, $file.FullName).Replace('\', '/'))) 'Unpinned file in execution bundle'
    }
}

function Get-ReplayCaptures {
    param([string]$Root, [object]$Receipt)
    Assert-Replay ($Receipt.schema -ceq 'forge-gate1-renderdoc-validation/v1' -and $Receipt.status -ceq 'PASS' -and $Receipt.backend -ceq 'Vulkan') 'Parent R1-037 receipt is not a Vulkan PASS'
    $names = @('clear-01', 'clear-02', 'rain-01', 'rain-02')
    Assert-Replay (@($Receipt.results).Count -eq 4) 'Parent receipt must contain exactly four runs'
    foreach ($name in $names) {
        $matching = @($Receipt.results | Where-Object { $_.run -ceq $name })
        Assert-Replay ($matching.Count -eq 1) "Missing or duplicate parent run: $name"
        $run = $matching[0]
        Assert-Replay ($run.capture_file -cmatch ('^' + [regex]::Escape($name) + '/[A-Za-z0-9_-]+\.rdc$')) "Unsafe capture path: $name"
        Assert-Replay ($run.capture_sha256 -cmatch '^[0-9a-f]{64}$') "Invalid capture SHA-256: $name"
        $path = Join-Path $Root $run.capture_file
        Assert-Replay ((Test-ReplayInside $Root $path) -and [IO.File]::Exists($path)) "Missing capture: $name"
        Assert-Replay ((Get-ReplayHash $path) -ceq $run.capture_sha256) "Capture SHA-256 mismatch: $name"
        [pscustomobject]@{ run = $name; path = $path; sha256 = $run.capture_sha256 }
    }
}

function Get-ReplayGitState {
    param([string]$Root)
    $head = @(& git -C $Root rev-parse HEAD)
    Assert-Replay ($LASTEXITCODE -eq 0) 'Cannot read Git HEAD'
    $branch = @(& git -C $Root branch --show-current)
    Assert-Replay ($LASTEXITCODE -eq 0 -and ($branch -join '') -ceq 'Rust') 'Material replay requires the Rust branch'
    $status = @(& git -C $Root status --porcelain=v1 --untracked-files=all)
    Assert-Replay ($LASTEXITCODE -eq 0) 'Cannot read Git status'
    return ($head + $branch + $status) -join "`n"
}

function Invoke-ReplayProcess {
    param([string]$Executable, [string]$Script, [string]$WorkingDirectory, [hashtable]$Environment, [string]$LogPath)
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $Executable
    $start.WorkingDirectory = $WorkingDirectory
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.ArgumentList.Add('--python')
    $start.ArgumentList.Add($Script)
    foreach ($key in $Environment.Keys) { $start.Environment[$key] = $Environment[$key] }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    $started = $false
    try {
        $started = $process.Start()
        Assert-Replay $started 'QRenderDoc did not start'
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $finished = $process.WaitForExit(180000)
        if (-not $finished) {
            if (-not $process.HasExited) { $process.Kill($true) }
            Assert-Replay ($process.WaitForExit(10000)) 'Owned QRenderDoc did not terminate after timeout'
        }
        [IO.File]::WriteAllText($LogPath, ($stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()))
        Assert-Replay $finished 'QRenderDoc exceeded the 180-second replay limit'
        Assert-Replay ($process.ExitCode -eq 0) "QRenderDoc failed with exit code $($process.ExitCode)"
        return $process.Id
    }
    finally {
        if ($started -and -not $process.HasExited) {
            $process.Kill($true)
            Assert-Replay ($process.WaitForExit(10000)) 'Owned QRenderDoc survived cleanup'
        }
        $process.Dispose()
    }
}

$repository = [IO.Path]::GetFullPath($RepoRoot).TrimEnd('\', '/')
$captureDirectory = [IO.Path]::GetFullPath($CaptureRoot).TrimEnd('\', '/')
$output = [IO.Path]::GetFullPath($OutputRoot).TrimEnd('\', '/')
$bundle = [IO.Path]::GetFullPath($RenderDocRoot).TrimEnd('\', '/')
foreach ($path in @($repository, $captureDirectory, $output, $bundle)) { Assert-PlainPath $path }
Assert-Replay ($IsWindows) 'The pinned replay bundle requires Windows'
Assert-Replay ([IO.Directory]::Exists([IO.Directory]::GetParent($output).FullName)) 'Output parent directory must already exist'
$gitBefore = Get-ReplayGitState $repository
$worktrees = @(& git -C $repository worktree list --porcelain)
Assert-Replay ($LASTEXITCODE -eq 0) 'Cannot enumerate protected worktrees'
$protected = @($captureDirectory, $bundle) + @($worktrees | Where-Object { $_.StartsWith('worktree ') } | ForEach-Object { $_.Substring(9) })
foreach ($root in $protected) {
    Assert-Replay ($output -ine $root -and -not (Test-ReplayInside $root $output) -and -not (Test-ReplayInside $output $root)) "Output overlaps an input or worktree: $root"
}
Assert-Replay (-not [IO.File]::Exists($output)) 'Output is a file'
Assert-Replay (-not [IO.Directory]::Exists($output) -or @(Get-ChildItem -LiteralPath $output -Force).Count -eq 0) 'Output must be a new empty directory'

$script = Join-Path $repository 'scripts/renderdoc/gate1_material_replay.py'
$launcher = $PSCommandPath
$manifestPath = Join-Path $repository 'scripts/gate1-renderdoc.v1.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
Assert-Replay ($manifest.schema -ceq 'forge-gate1-renderdoc-lock/v1' -and $manifest.tool.release -ceq '1.46') 'Wrong RenderDoc pin'
Assert-Replay ($manifest.tool.bundle_inventory.relative_path -ceq 'evidence/extracted-file-manifest.json' -and $manifest.tool.bundle_inventory.bundle_relative_path -ceq 'extract/RenderDoc_1.46_64') 'Unexpected pinned acquisition paths'
$acquisitionRoot = [IO.Directory]::GetParent([IO.Directory]::GetParent($bundle).FullName).FullName
Assert-Replay ([IO.Path]::GetRelativePath($acquisitionRoot, $bundle).Replace('\', '/') -ceq $manifest.tool.bundle_inventory.bundle_relative_path) 'Bundle is outside its pinned acquisition layout'
$inventoryPath = Join-Path $acquisitionRoot $manifest.tool.bundle_inventory.relative_path
Assert-Replay ((Get-ReplayHash $inventoryPath) -ceq ([string]$manifest.tool.bundle_inventory.sha256).ToLowerInvariant()) 'Bundle inventory SHA-256 differs from pin'
$inventory = @(Get-Content -LiteralPath $inventoryPath -Raw | ConvertFrom-Json)
Assert-Replay ($manifest.tool.bundle_inventory.file_count -eq 72 -and $manifest.tool.bundle_inventory.total_bytes -eq 210237254) 'Unexpected bundle inventory contract'
Assert-Replay ([IO.File]::Exists($script)) 'Material replay Python script is absent'
Assert-Replay ((Get-ReplayHash (Join-Path $bundle 'qrenderdoc.exe')) -ceq ([string]$manifest.tool.sha256.qrenderdoc_exe).ToLowerInvariant()) 'QRenderDoc executable differs from pin'
$signature = Get-AuthenticodeSignature -LiteralPath (Join-Path $bundle 'qrenderdoc.exe')
Assert-Replay ($signature.Status -eq 'Valid' -and $null -ne $signature.SignerCertificate -and $signature.SignerCertificate.Thumbprint -ceq $manifest.tool.authenticode_signer_thumbprint) 'QRenderDoc signature differs from pin'
Assert-ReplayBundle $bundle $inventory
$parentPath = Join-Path $captureDirectory 'gate1-renderdoc.validation.json'
$parentHash = Get-ReplayHash $parentPath
$parentReceipt = Get-Content -LiteralPath $parentPath -Raw | ConvertFrom-Json
$captures = @(Get-ReplayCaptures $captureDirectory $parentReceipt)
$scriptHash = Get-ReplayHash $script
$launcherHash = Get-ReplayHash $launcher
$manifestHash = Get-ReplayHash $manifestPath
$nonce = [Guid]::NewGuid().ToString('N')
$applicationName = "forge-gate1-qrenderdoc-$nonce"
$roaming = [Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)
$profile = Join-Path $roaming $applicationName
$realProfile = Join-Path $roaming 'qrenderdoc'
$mirror = Join-Path $output ".qrenderdoc-execution-$nonce"
$mutex = [Threading.Mutex]::new($false, 'Global\OpenRPGForge-Gate1-QRenderDoc-Profile-v1')
$acquired = $false
$mirrorOwned = $false
$profileOwned = $false
$outputReady = $false
$profileBefore = $null
$profileAfter = $null
$errors = [Collections.Generic.List[string]]::new()
$results = [Collections.Generic.List[object]]::new()
$generatedCaches = [Collections.Generic.List[object]]::new()
$activeRun = $null
try {
    $acquired = $mutex.WaitOne(0)
    Assert-Replay $acquired 'Another RenderDoc task owns the profile mutex'
    $profileBefore = Get-ReplayProfileFingerprint $realProfile
    Assert-Replay (-not (Test-Path -LiteralPath $profile)) 'Disposable profile already exists'
    if (-not [IO.Directory]::Exists($output)) { [void](New-Item -ItemType Directory -Path $output) }
    $outputReady = $true
    [void](New-Item -ItemType Directory -Path $mirror)
    $mirrorOwned = $true
    foreach ($entry in $inventory) {
        $relative = $entry.path.Substring('extract/RenderDoc_1.46_64/'.Length)
        $destination = Join-Path $mirror $relative
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
        Copy-Item -LiteralPath (Join-Path $bundle $relative) -Destination $destination
    }
    Assert-ReplayBundle $mirror $inventory
    $executable = Join-Path $mirror "$applicationName.exe"
    Copy-Item -LiteralPath (Join-Path $mirror 'qrenderdoc.exe') -Destination $executable
    Assert-Replay ((Get-ReplayHash $executable) -ceq ([string]$manifest.tool.sha256.qrenderdoc_exe).ToLowerInvariant()) 'Renamed executable differs from pin'
    $signature = Get-AuthenticodeSignature -LiteralPath $executable
    Assert-Replay ($signature.Status -eq 'Valid' -and $null -ne $signature.SignerCertificate -and $signature.SignerCertificate.Thumbprint -ceq $manifest.tool.authenticode_signer_thumbprint) 'Renamed executable signature differs from pin'
    foreach ($capture in $captures) {
        $activeRun = Join-Path $output $capture.run
        [void](New-Item -ItemType Directory -Path $activeRun)
        [void](New-Item -ItemType Directory -Path $profile)
        $profileOwned = $true
        $config = Join-Path $profile 'UI.config'
        Write-ReplayJson $config @{ rdocConfigData = 1; Analytics_TotalOptOut = $true }
        $seedHash = Get-ReplayHash $config
        $processId = Invoke-ReplayProcess $executable $script $repository @{
            FORGE_GATE1_RENDERDOC_PROFILE_CONFIG = $config
            FORGE_MATERIAL_CAPTURE = $capture.path
            FORGE_MATERIAL_OUTPUT = $activeRun
            FORGE_MATERIAL_PARENT_RECEIPT = $parentPath
            PYTHONDONTWRITEBYTECODE = '1'
        } (Join-Path $activeRun 'qrenderdoc.log')
        Assert-Replay (-not (Test-Path -LiteralPath (Join-Path $activeRun 'failure.txt'))) "Python replay failed: $($capture.run)/failure.txt"
        $receiptPath = Join-Path $activeRun 'material-replay.json'
        Assert-Replay ([IO.File]::Exists($receiptPath)) "Python replay receipt absent: $($capture.run)"
        $pythonReceipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
        Assert-Replay ($null -ne $pythonReceipt -and $pythonReceipt -is [pscustomobject]) 'Python replay receipt is not a JSON object'
        Assert-Replay ($pythonReceipt.schema -ceq 'forge-gate1-material-replay/v1' -and $pythonReceipt.status -ceq 'extracted_pending_bound_state_validation') 'Python replay receipt identity/status mismatch'
        Assert-Replay ($pythonReceipt.capture.sha256 -ceq $capture.sha256 -and [IO.Path]::GetFullPath($pythonReceipt.capture.file) -ieq $capture.path -and $pythonReceipt.capture.run -ceq $capture.run -and $pythonReceipt.capture.parent_receipt_sha256 -ceq $parentHash) 'Python receipt identifies another capture or parent receipt'
        $finalConfig = Get-Content -LiteralPath $config -Raw | ConvertFrom-Json
        Assert-Replay ($finalConfig.rdocConfigData -eq 1 -and $finalConfig.Analytics_TotalOptOut -is [bool] -and $finalConfig.Analytics_TotalOptOut) 'Disposable profile lost its analytics opt-out'
        Copy-Item -LiteralPath $config -Destination (Join-Path $activeRun 'profile-final.json')
        $results.Add([ordered]@{
            run = $capture.run; process_id = $processId; capture_path = $capture.path
            capture_sha256 = $capture.sha256; python_script_sha256 = $scriptHash
            receipt_sha256 = Get-ReplayHash $receiptPath
            log_sha256 = Get-ReplayHash (Join-Path $activeRun 'qrenderdoc.log')
            profile_seed_sha256 = $seedHash; profile_final_sha256 = Get-ReplayHash $config
        })
        Remove-ReplayOwnedTree $roaming $profile 'forge-gate1-qrenderdoc-'
        $profileOwned = $false
    }
}
catch { $errors.Add($_.Exception.Message) }
finally {
    if ($profileOwned) {
        try {
            $config = Join-Path $profile 'UI.config'
            $preserved = Join-Path $activeRun 'profile-final.json'
            if ([IO.File]::Exists($config) -and -not (Test-Path -LiteralPath $preserved)) { Copy-Item -LiteralPath $config -Destination $preserved }
            Remove-ReplayOwnedTree $roaming $profile 'forge-gate1-qrenderdoc-'
        }
        catch { $errors.Add($_.Exception.Message) }
    }
    if ($mirrorOwned) {
        try {
            # QRenderDoc initializes these two Python packages before --python.
            # Its embedded interpreter ignores PYTHONDONTWRITEBYTECODE then.
            # Preserve their bytes outside the mirror, and remove only these
            # exact generated files before the unchanged 72-file pin check.
            foreach ($relative in @('PySide2/__pycache__/__init__.cpython-38.pyc', 'shiboken2/__pycache__/__init__.cpython-38.pyc')) {
                $cache = Join-Path $mirror $relative
                if (Test-Path -LiteralPath $cache) {
                    Assert-Replay ((Test-ReplayInside $mirror $cache) -and [IO.File]::Exists($cache)) 'Generated cache escaped the owned mirror'
                    Assert-PlainPath $cache
                    $cacheHash = Get-ReplayHash $cache
                    $preservedName = ($relative.Split('/')[0]) + '-startup.cpython-38.pyc'
                    $preservedCache = Join-Path $output $preservedName
                    Assert-Replay (-not (Test-Path -LiteralPath $preservedCache)) 'Preserved cache already exists'
                    Copy-Item -LiteralPath $cache -Destination $preservedCache
                    Assert-Replay ((Get-ReplayHash $preservedCache) -ceq $cacheHash) 'Preserved cache hash mismatch'
                    $generatedCaches.Add(@{ relative_path = $relative; preserved_file = $preservedName; sha256 = $cacheHash })
                    Remove-Item -LiteralPath $cache
                }
            }
        }
        catch { $errors.Add($_.Exception.Message) }
        try { Assert-ReplayBundle $mirror $inventory "$applicationName.exe" }
        catch {
            $bundleError = $_.Exception.Message
            $errors.Add($bundleError)
            try {
                # Preserve only metadata, never tool files or capture contents. Keep the
                # failing bundle check strict; this inventory explains unexpected files.
                $observedFiles = @(
                    Get-ChildItem -LiteralPath $mirror -File -Force -Recurse | Sort-Object FullName | ForEach-Object {
                        $bundleFile = $_
                        $hash = $null
                        $hashError = $null
                        try { $hash = Get-ReplayHash $bundleFile.FullName }
                        catch { $hashError = $_.Exception.Message }
                        [ordered]@{
                            path = [IO.Path]::GetRelativePath($mirror, $bundleFile.FullName).Replace('\', '/')
                            size = $bundleFile.Length; sha256 = $hash; hash_error = $hashError
                        }
                    }
                )
                Write-ReplayJson (Join-Path $output 'execution-bundle-inventory-on-failure.json') ([ordered]@{
                    schema = 'forge-gate1-material-replay-bundle-failure/v1'
                    error = $bundleError; expected_file_count = $inventory.Count + 1
                    actual_file_count = $observedFiles.Count; files = $observedFiles
                })
            }
            catch { $errors.Add("Bundle failure inventory could not be preserved: $($_.Exception.Message)") }
        }
        try { Remove-ReplayOwnedTree $output $mirror '.qrenderdoc-execution-' }
        catch { $errors.Add($_.Exception.Message) }
    }
    if ($acquired) {
        try {
            $profileAfter = Get-ReplayProfileFingerprint $realProfile
            Assert-Replay ($profileBefore -ceq $profileAfter) 'Real QRenderDoc user profile changed'
        }
        catch { $errors.Add($_.Exception.Message) }
        $mutex.ReleaseMutex()
    }
    $mutex.Dispose()
    $captureIntegrity = $true
    foreach ($capture in $captures) {
        try { Assert-Replay ((Get-ReplayHash $capture.path) -ceq $capture.sha256) "Capture modified: $($capture.run)" }
        catch { $captureIntegrity = $false; $errors.Add($_.Exception.Message) }
    }
    $gitUnchanged = $false
    $inputsUnchanged = $false
    try {
        $gitUnchanged = (Get-ReplayGitState $repository) -ceq $gitBefore
        Assert-Replay $gitUnchanged 'Git HEAD, branch or status changed during replay'
        $inputsUnchanged = (Get-ReplayHash $parentPath) -ceq $parentHash -and (Get-ReplayHash $script) -ceq $scriptHash -and (Get-ReplayHash $launcher) -ceq $launcherHash -and (Get-ReplayHash $manifestPath) -ceq $manifestHash
        Assert-Replay $inputsUnchanged 'Replay input receipt, scripts or manifest changed'
        Assert-ReplayBundle $bundle $inventory
    }
    catch { $errors.Add($_.Exception.Message) }
    if ($outputReady) {
        Write-ReplayJson (Join-Path $output 'material-replay-launcher.json') ([ordered]@{
            schema = 'forge-gate1-material-replay-launcher/v1'
            status = if ($errors.Count -eq 0 -and $results.Count -eq 4) { 'PASS' } else { 'FAIL' }
            completed_at_utc = [DateTime]::UtcNow.ToString('o')
            parent_receipt_sha256 = $parentHash; launcher_sha256 = $launcherHash
            python_script_sha256 = $scriptHash; renderdoc_manifest_sha256 = $manifestHash
            inventory_sha256 = Get-ReplayHash $inventoryPath
            qrenderdoc_sha256 = ([string]$manifest.tool.sha256.qrenderdoc_exe).ToLowerInvariant()
            git_state_sha256 = Get-ReplayTextHash $gitBefore
            safety = [ordered]@{
                git_unchanged = $gitUnchanged; captures_unchanged = $captureIntegrity
                input_receipt_scripts_manifest_unchanged = $inputsUnchanged
                real_profile_before = $profileBefore; real_profile_after = $profileAfter
                real_profile_unchanged = $acquired -and $profileBefore -ceq $profileAfter
                disposable_profile_removed = -not (Test-Path -LiteralPath $profile)
                execution_mirror_removed = -not (Test-Path -LiteralPath $mirror)
                host_registry_modified = $false; runtime_launched = $false; build_performed = $false
            }
            results = @($results.ToArray()); errors = @($errors.ToArray())
            generated_startup_caches = @($generatedCaches.ToArray())
            gate1_routes_qualified = $false
            dynamic_shader_execution_qualified = $false
        })
    }
}
if ($errors.Count) { throw ($errors -join '; ') }
Assert-Replay ($results.Count -eq 4) 'Replay matrix incomplete'
Write-Output "Gate 1 material replay acquisition PASS: $output"
