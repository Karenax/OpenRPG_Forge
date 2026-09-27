[CmdletBinding(DefaultParameterSetName = 'Proof')]
param(
    [Parameter(ParameterSetName = 'Proof')]
    [switch]$Proof,

    [Parameter(ParameterSetName = 'Proof')]
    [ValidateNotNullOrEmpty()]
    [string]$RepoRoot,

    [Parameter(ParameterSetName = 'Proof', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputDirectory,

    [Parameter(ParameterSetName = 'Proof', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$CargoHome,

    [Parameter(ParameterSetName = 'Proof')]
    [ValidateRange(60, 7200)]
    [int]$CargoTestTimeoutSeconds = 1800,

    [Parameter(ParameterSetName = 'SelfTest', Mandatory)]
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:CiStatus = 'NOT RUN ' + [char]0x2014 + ' infrastructure billing'
$script:Utf8NoBom = [System.Text.UTF8Encoding]::new($false)

function Write-Utf8File {
    param(
        [Parameter(Mandatory)][string]$LiteralPath,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Content
    )
    [System.IO.File]::WriteAllText($LiteralPath, $Content, $script:Utf8NoBom)
}

function Get-FileSha256 {
    param([Parameter(Mandatory)][string]$LiteralPath)
    return (Get-FileHash -LiteralPath $LiteralPath -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Test-IsDriveEPath {
    param([Parameter(Mandatory)][string]$LiteralPath)
    $full = [System.IO.Path]::GetFullPath($LiteralPath)
    return [System.IO.Path]::GetPathRoot($full).TrimEnd('\') -ieq 'E:'
}

function Test-PathWithinDirectory {
    param(
        [Parameter(Mandatory)][string]$LiteralPath,
        [Parameter(Mandatory)][string]$Directory
    )
    $candidate = [System.IO.Path]::GetFullPath($LiteralPath).TrimEnd('\')
    $root = [System.IO.Path]::GetFullPath($Directory).TrimEnd('\')
    return $candidate.Equals($root, [StringComparison]::OrdinalIgnoreCase) -or
        $candidate.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)
}

function Assert-NoExistingReparseAncestor {
    param(
        [Parameter(Mandatory)][string]$LiteralPath,
        [Parameter(Mandatory)][string]$Description
    )
    $current = [System.IO.Path]::GetFullPath($LiteralPath)
    while (-not (Test-Path -LiteralPath $current)) {
        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $current) {
            throw "$Description has no existing parent directory: $LiteralPath"
        }
        $current = $parent
    }
    while (-not [string]::IsNullOrWhiteSpace($current)) {
        $item = Get-Item -LiteralPath $current -Force
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "$Description traverses reparse point '$current'; physical E: storage is required."
        }
        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $current) {
            break
        }
        $current = $parent
    }
}

function Get-TestManifest {
    return @(
        [pscustomobject]@{
            Id = 'core-process-exit'
            Package = 'forge_core'
            IntegrationTarget = $null
            Test = 'document_store::tests::process_exit_preserves_versions_and_releases_os_lock'
            Source = 'crates/forge_core/src/document_store/tests.rs'
            Criteria = @('R2-038', 'R2-039')
            Coverage = @('process_crash_after_file_fsync_before_replace', 'old_or_new_never_partial', 'concurrent_lock', 'stale_choice', 'promotion_preserves_versions')
            Markers = @('std::process::exit(77)', 'assert_eq!(status.code(), Some(77))', 'assert_eq!(pending.bytes, b"candidate")', 'store.recover(Some(saved), pending.hash, true).unwrap()')
        }
        [pscustomobject]@{
            Id = 'core-truncated-keep'
            Package = 'forge_core'
            IntegrationTarget = $null
            Test = 'document_store::tests::keep_saved_archives_truncated_candidate_and_allows_next_save'
            Source = 'crates/forge_core/src/document_store/tests.rs'
            Criteria = @('R2-039')
            Coverage = @('invalid_or_truncated_candidate', 'promotion_preserves_versions', 'exact_reopen')
            Markers = @('fs::write(root.join("authoring.pending"), b"{")', 'store.recover(Some(saved), pending.hash, false).unwrap()', 'store.save(Some(saved), b"next").unwrap()')
        }
        [pscustomobject]@{
            Id = 'core-replace-reopen'
            Package = 'forge_core'
            IntegrationTarget = $null
            Test = 'document_store::tests::replacement_reopen_conflict_and_lock_release'
            Source = 'crates/forge_core/src/document_store/tests.rs'
            Criteria = @('R2-038')
            Coverage = @('conflict', 'concurrent_lock', 'old_or_new_never_partial', 'exact_reopen')
            Markers = @('Err(StoreError::Busy)', 'Err(StoreError::Conflict)', 'assert_eq!(stored.bytes, b"second")')
        }
        [pscustomobject]@{
            Id = 'core-before-replace-failure'
            Package = 'forge_core'
            IntegrationTarget = $null
            Test = 'document_store::tests::failure_before_replace_and_external_edit_preserve_committed_bytes'
            Source = 'crates/forge_core/src/document_store/tests.rs'
            Criteria = @('R2-038')
            Coverage = @('conflict', 'old_or_new_never_partial')
            Markers = @('"injected interruption"', 'assert!(!root.join("authoring.pending").exists())', 'b"external"', 'Err(StoreError::Conflict)')
        }
        [pscustomobject]@{
            Id = 'core-abandoned-stage-invalid'
            Package = 'forge_core'
            IntegrationTarget = $null
            Test = 'document_store::tests::interrupted_staging_and_invalid_entries_are_not_overwritten'
            Source = 'crates/forge_core/src/document_store/tests.rs'
            Criteria = @('R2-038', 'R2-039')
            Coverage = @('abandoned_staging', 'oversize', 'invalid_or_truncated_candidate', 'old_or_new_never_partial')
            Markers = @('b"interrupted"', 'MAX_BYTES + 1', 'Err(StoreError::TooLarge)', 'Err(StoreError::InvalidEntry)')
        }
        [pscustomobject]@{
            Id = 'workspace-atomic-reopen'
            Package = 'forge_project'
            IntegrationTarget = 'workspace'
            Test = 'atomic_project_save_reopens_with_history_and_preserves_legacy'
            Source = 'crates/forge_project/tests/workspace.rs'
            Criteria = @('R2-038')
            Coverage = @('concurrent_lock', 'promotion_preserves_versions', 'exact_reopen')
            Markers = @('assert!(FileWorkspaceSession::open(&root).is_err())', 'let mut reopened = FileWorkspaceSession::open(&root).unwrap()', 'b"untouched legacy bytes"')
        }
        [pscustomobject]@{
            Id = 'workspace-recovery'
            Package = 'forge_project'
            IntegrationTarget = 'workspace_recovery'
            Test = 'interrupted_workspace_recovery_is_explicit_hash_bound_and_preserves_versions'
            Source = 'crates/forge_project/tests/workspace_recovery.rs'
            Criteria = @('R2-039')
            Coverage = @('invalid_or_truncated_candidate', 'missing_object', 'source_lock', 'stale_choice', 'promotion_preserves_versions', 'exact_reopen')
            Markers = @('review.pending.head.is_none()', 'ZoneObjectStore::open(&authority)', 'let source_lock = DocumentStore::open(&source).unwrap()', 'assert_eq!(opened.head, Some(head))')
        }
        [pscustomobject]@{
            Id = 'zone-recovery-bindings'
            Package = 'forge_project'
            IntegrationTarget = 'authoring_recovery'
            Test = 'recovery_binds_both_versions_archives_them_and_requires_closed_session'
            Source = 'crates/forge_project/tests/authoring_recovery.rs'
            Criteria = @('R2-039')
            Coverage = @('invalid_or_truncated_candidate', 'stale_choice', 'promotion_preserves_versions', 'exact_reopen')
            Markers = @('host.execute(Request::Close).unwrap()', 'std::fs::write(dir.join("authoring.pending"), b"corrupt")', 'std::fs::write(dir.join("authoring.json"), b"changed saved bytes")', 'Some(candidate)')
        }
        [pscustomobject]@{
            Id = 'zone-recovery-identity'
            Package = 'forge_project'
            IntegrationTarget = 'authoring_recovery'
            Test = 'first_save_recovery_and_foreign_identity_rejection'
            Source = 'crates/forge_project/tests/authoring_recovery.rs'
            Criteria = @('R2-039')
            Coverage = @('foreign_identity', 'promotion_preserves_versions', 'exact_reopen')
            Markers = @('assert!(review.saved.is_none())', 'foreign.project_id = forge_core::ProjectId::deterministic("different")', 'assert!(review.pending.document.is_none())')
        }
        [pscustomobject]@{
            Id = 'zone-catalog-failures'
            Package = 'forge_project'
            IntegrationTarget = 'authoring_catalog'
            Test = 'catalog_explains_interrupted_missing_mismatched_and_oversized_archives'
            Source = 'crates/forge_project/tests/authoring_catalog.rs'
            Criteria = @('R2-039')
            Coverage = @('abandoned_staging', 'missing_source_archive', 'foreign_identity', 'oversize', 'invalid_or_truncated_candidate')
            Markers = @('Reason::InterruptedSave', 'Reason::MissingArchive', 'Reason::IdentityMismatch', 'Reason::TooLarge', 'Reason::UnsafePath')
        }
        [pscustomobject]@{
            Id = 'workspace-object-publication'
            Package = 'forge_project'
            IntegrationTarget = 'workspace_objects'
            Test = 'immutable_object_publication_preserves_source_and_rejects_corruption'
            Source = 'crates/forge_project/tests/workspace_objects.rs'
            Criteria = @('R2-038', 'R2-039')
            Coverage = @('missing_object', 'foreign_identity', 'old_or_new_never_partial', 'promotion_preserves_versions')
            Markers = @('.verify(reviewed.project_id(), reviewed.reference())', 'store.publish(&reviewed).unwrap()', 'std::fs::write(&object, b"corrupt")', 'assert_eq!(bytes, std::fs::read(&source).unwrap())')
        }
        [pscustomobject]@{
            Id = 'workspace-object-review'
            Package = 'forge_project'
            IntegrationTarget = 'workspace_objects'
            Test = 'review_requires_exact_bytes_and_valid_archive'
            Source = 'crates/forge_project/tests/workspace_objects.rs'
            Criteria = @('R2-039')
            Coverage = @('oversize', 'invalid_or_truncated_candidate')
            Markers = @('ReviewedZone::from_bytes(b"{}", bytes_sha256(b"other"))', '4 * 1024 * 1024 + 1')
        }
        [pscustomobject]@{
            Id = 'workspace-adoption-lock'
            Package = 'forge_project'
            IntegrationTarget = 'workspace_adoption'
            Test = 'adoption_locks_source_publishes_exact_bytes_and_blocks_legacy_even_after_undo'
            Source = 'crates/forge_project/tests/workspace_adoption.rs'
            Criteria = @('R2-038', 'R2-039')
            Coverage = @('source_lock', 'stale_choice', 'promotion_preserves_versions', 'exact_reopen')
            Markers = @('assert!(file.adopt(&adopt).is_err()); // source still open', 'file.adopt(&adopt).unwrap()', 'assert_eq!(bytes, std::fs::read(&source).unwrap())', 'FileWorkspaceSession::open(&root).unwrap()')
        }
    )
}

function Get-SourceContracts {
    return @(
        [pscustomobject]@{
            Source = 'crates/forge_core/src/document_store.rs'
            Markers = @('file.sync_all()?;', 'before_replace()?;', 'fs::rename(&staging, self.root.join("authoring.json"))?;', 'try_lock()')
        }
        [pscustomobject]@{
            Source = 'crates/forge_project/src/workspace/file_session/recovery.rs'
            Markers = @('Recovery versions changed: inspect them again', 'Invalid workspace candidate or zone objects cannot be promoted', 'fn lock_sources')
        }
        [pscustomobject]@{
            Source = 'crates/forge_project/src/workspace/objects.rs'
            Markers = @('file.sync_all().map_err', 'fs::hard_link(&stage, &final_path)', 'self.verify(reviewed.project_id, &reviewed.reference)')
        }
    )
}

function Assert-ManifestContracts {
    param(
        [Parameter(Mandatory)][string]$ResolvedRepo,
        [Parameter(Mandatory)][object[]]$Manifest
    )
    $ids = @{}
    foreach ($entry in $Manifest) {
        if ($ids.ContainsKey($entry.Id)) {
            throw "Duplicate test manifest id '$($entry.Id)'."
        }
        $ids[$entry.Id] = $true
        $path = Join-Path $ResolvedRepo $entry.Source
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Pinned test source is missing: $($entry.Source)"
        }
        $text = [System.IO.File]::ReadAllText($path)
        $leafName = $entry.Test.Substring($entry.Test.LastIndexOf(':') + 1)
        $matches = [regex]::Matches($text, '(?m)^\s*fn\s+' + [regex]::Escape($leafName) + '\s*\(')
        if ($matches.Count -ne 1) {
            throw "Pinned test '$($entry.Test)' must have exactly one definition in $($entry.Source); found $($matches.Count)."
        }
        foreach ($marker in $entry.Markers) {
            if ($text.IndexOf($marker, [StringComparison]::Ordinal) -lt 0) {
                throw "Pinned semantic marker is absent for $($entry.Test): $marker"
            }
        }
    }
    foreach ($contract in Get-SourceContracts) {
        $path = Join-Path $ResolvedRepo $contract.Source
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Pinned implementation source is missing: $($contract.Source)"
        }
        $text = [System.IO.File]::ReadAllText($path)
        foreach ($marker in $contract.Markers) {
            if ($text.IndexOf($marker, [StringComparison]::Ordinal) -lt 0) {
                throw "Pinned implementation marker is absent in $($contract.Source): $marker"
            }
        }
    }
}

function Get-PinnedRelativePaths {
    param(
        [Parameter(Mandatory)][string]$ResolvedRepo,
        [Parameter(Mandatory)][object[]]$Manifest
    )
    $paths = @(
        'Cargo.toml'
        'Cargo.lock'
        'rust-toolchain.toml'
        '.cargo/config.toml'
        'crates/forge_core/Cargo.toml'
        'crates/forge_project/Cargo.toml'
        'scripts/Invoke-Gate2PersistenceFailureProof.ps1'
    )
    foreach ($sourceRoot in @('crates/forge_core/src', 'crates/forge_project/src')) {
        $absoluteRoot = Join-Path $ResolvedRepo $sourceRoot
        if (-not (Test-Path -LiteralPath $absoluteRoot -PathType Container)) {
            throw "Direct source closure root is missing: $sourceRoot"
        }
        $paths += @(Get-ChildItem -LiteralPath $absoluteRoot -File -Filter '*.rs' -Recurse | ForEach-Object {
            $_.FullName.Substring($ResolvedRepo.Length).TrimStart('\').Replace('\', '/')
        })
    }
    $paths += @($Manifest | ForEach-Object { $_.Source })
    $paths += @(Get-SourceContracts | ForEach-Object { $_.Source })
    return @($paths | Sort-Object -Unique)
}

function Get-GitWorktreeSnapshot {
    param([Parameter(Mandatory)][string]$ResolvedRepo)
    $paths = @(& git -c core.quotepath=false -C $ResolvedRepo ls-files --cached --others --exclude-standard 2>&1)
    $code = $LASTEXITCODE
    if ($code -ne 0) {
        throw "git ls-files failed with exit code ${code}: $($paths -join [Environment]::NewLine)"
    }
    $entries = @()
    foreach ($relative in @($paths | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)) {
        $full = Join-Path $ResolvedRepo $relative
        if (Test-Path -LiteralPath $full -PathType Leaf) {
            $item = Get-Item -LiteralPath $full -Force
            $entries += [pscustomobject]@{
                path = $relative.Replace('\', '/')
                state = 'file'
                sha256 = Get-FileSha256 -LiteralPath $item.FullName
                bytes = [int64]$item.Length
            }
        }
        elseif (Test-Path -LiteralPath $full -PathType Container) {
            $entries += [pscustomobject]@{
                path = $relative.Replace('\', '/')
                state = 'directory'
                sha256 = $null
                bytes = $null
            }
        }
        else {
            $entries += [pscustomobject]@{
                path = $relative.Replace('\', '/')
                state = 'missing'
                sha256 = $null
                bytes = $null
            }
        }
    }
    return $entries
}

function Compare-GitWorktreeSnapshots {
    param(
        [Parameter(Mandatory)][object[]]$Before,
        [Parameter(Mandatory)][object[]]$After
    )
    $beforeMap = @{}
    $afterMap = @{}
    foreach ($entry in $Before) { $beforeMap[$entry.path] = $entry }
    foreach ($entry in $After) { $afterMap[$entry.path] = $entry }
    $allPaths = @($beforeMap.Keys + $afterMap.Keys | Sort-Object -Unique)
    $changes = @()
    foreach ($path in $allPaths) {
        if (-not $beforeMap.ContainsKey($path)) {
            $changes += "${path}: added"
            continue
        }
        if (-not $afterMap.ContainsKey($path)) {
            $changes += "${path}: removed"
            continue
        }
        $left = $beforeMap[$path]
        $right = $afterMap[$path]
        if ($left.state -cne $right.state -or $left.sha256 -cne $right.sha256 -or $left.bytes -ne $right.bytes) {
            $changes += "${path}: $($left.state)/$($left.sha256) -> $($right.state)/$($right.sha256)"
        }
    }
    return $changes
}

function Get-PinnedSnapshot {
    param(
        [Parameter(Mandatory)][string]$ResolvedRepo,
        [Parameter(Mandatory)][string[]]$RelativePaths
    )
    $snapshot = @()
    foreach ($relative in $RelativePaths) {
        $path = Join-Path $ResolvedRepo $relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Pinned source disappeared: $relative"
        }
        $item = Get-Item -LiteralPath $path -Force
        $snapshot += [pscustomobject]@{
            path = $relative.Replace('\', '/')
            sha256 = Get-FileSha256 -LiteralPath $item.FullName
            bytes = [int64]$item.Length
            last_write_utc = ([DateTimeOffset]$item.LastWriteTimeUtc).ToString('o')
        }
    }
    return $snapshot
}

function Compare-PinnedSnapshots {
    param(
        [Parameter(Mandatory)][object[]]$Before,
        [Parameter(Mandatory)][object[]]$After
    )
    $changes = @()
    $afterByPath = @{}
    foreach ($entry in $After) { $afterByPath[$entry.path] = $entry }
    foreach ($entry in $Before) {
        if (-not $afterByPath.ContainsKey($entry.path)) {
            $changes += "$($entry.path): missing"
            continue
        }
        $other = $afterByPath[$entry.path]
        if ($entry.sha256 -cne $other.sha256 -or [int64]$entry.bytes -ne [int64]$other.bytes) {
            $changes += "$($entry.path): $($entry.sha256) -> $($other.sha256)"
        }
    }
    return $changes
}

function Get-GitOutput {
    param(
        [Parameter(Mandatory)][string]$ResolvedRepo,
        [Parameter(Mandatory)][string[]]$Arguments
    )
    $output = @(& git -C $ResolvedRepo @Arguments 2>&1)
    $code = $LASTEXITCODE
    if ($code -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code ${code}: $($output -join [Environment]::NewLine)"
    }
    return ($output -join [Environment]::NewLine).Trim()
}

function Get-CargoArguments {
    param([Parameter(Mandatory)][object]$TestEntry)
    $arguments = @('test', '--locked', '--offline', '-j', '1', '-p', $TestEntry.Package)
    if (-not [string]::IsNullOrWhiteSpace([string]$TestEntry.IntegrationTarget)) {
        $arguments += @('--test', $TestEntry.IntegrationTarget)
    }
    $arguments += @($TestEntry.Test, '--', '--exact', '--nocapture', '--test-threads=1')
    return $arguments
}

function Stop-OwnedProcessTree {
    param([Parameter(Mandatory)][int]$ProcessId)
    $receipt = [ordered]@{
        owned_root_pid = $ProcessId
        termination_requested = $false
        method = 'taskkill /PID <owned> /T /F'
        taskkill_exit_code = $null
        taskkill_output = @()
        root_exited = $false
        error = $null
        process_disposed = $false
    }
    try {
        $existing = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
        if ($null -ne $existing) {
            $receipt.termination_requested = $true
            $taskkill = Join-Path $env:SystemRoot 'System32\taskkill.exe'
            $output = @(& $taskkill /PID $ProcessId /T /F 2>&1)
            $receipt.taskkill_exit_code = $LASTEXITCODE
            $receipt.taskkill_output = @($output | ForEach-Object { [string]$_ })
        }
        $receipt.root_exited = $null -eq (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)
    }
    catch {
        $receipt.error = $_.Exception.Message
        $receipt.root_exited = $null -eq (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)
    }
    return [pscustomobject]$receipt
}

function Get-ExactTestObservation {
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Output,
        [Parameter(Mandatory)][string]$ExpectedName
    )
    $pattern = '(?m)^\s*test\s+' + [regex]::Escape($ExpectedName) + '\s+\.\.\.\s+(ok|FAILED|ignored)\s*$'
    $matches = [regex]::Matches($Output, $pattern)
    return [pscustomobject]@{
        count = $matches.Count
        status = if ($matches.Count -eq 1) { $matches[0].Groups[1].Value } else { $null }
    }
}

function Invoke-CargoTestLogged {
    param(
        [Parameter(Mandatory)][string]$CargoPath,
        [Parameter(Mandatory)][string]$ResolvedRepo,
        [Parameter(Mandatory)][object]$TestEntry,
        [Parameter(Mandatory)][int]$Ordinal,
        [Parameter(Mandatory)][string]$CommandsDirectory,
        [Parameter(Mandatory)][string]$LogsDirectory,
        [Parameter(Mandatory)][ValidateRange(60, 7200)][int]$TimeoutSeconds
    )
    $arguments = @(Get-CargoArguments -TestEntry $TestEntry)
    foreach ($argument in $arguments) {
        if ([string]$argument -match '[\s"]') {
            throw "Unexpected whitespace or quote in fixed cargo argument '$argument'."
        }
    }
    $prefix = '{0:D2}-{1}' -f $Ordinal, $TestEntry.Id
    $commandPath = Join-Path $CommandsDirectory ($prefix + '.command.txt')
    $stdoutPath = Join-Path $LogsDirectory ($prefix + '.stdout.log')
    $stderrPath = Join-Path $LogsDirectory ($prefix + '.stderr.log')
    $display = '"' + $CargoPath + '" ' + ($arguments -join ' ')
    Write-Utf8File -LiteralPath $commandPath -Content ($display + [Environment]::NewLine)

    $start = [DateTimeOffset]::UtcNow
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $exitCode = -1
    $stdout = ''
    $stderr = ''
    $failure = $null
    $process = $null
    $processStarted = $false
    $ownedPid = $null
    $stdoutTask = $null
    $stderrTask = $null
    $timedOut = $false
    $disposed = $false
    $cleanup = [pscustomobject]@{
        owned_root_pid = $null
        termination_requested = $false
        method = 'none'
        taskkill_exit_code = $null
        taskkill_output = @()
        root_exited = $true
        error = $null
        process_disposed = $false
    }
    try {
        $info = [Diagnostics.ProcessStartInfo]::new()
        $info.FileName = $CargoPath
        $info.Arguments = $arguments -join ' '
        $info.WorkingDirectory = $ResolvedRepo
        $info.UseShellExecute = $false
        $info.CreateNoWindow = $true
        $info.RedirectStandardOutput = $true
        $info.RedirectStandardError = $true
        $process = [Diagnostics.Process]::new()
        $process.StartInfo = $info
        if (-not $process.Start()) {
            throw 'cargo process did not start.'
        }
        $processStarted = $true
        $ownedPid = $process.Id
        $cleanup.owned_root_pid = $ownedPid
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $timedOut = $true
            $failure = "Cargo test timed out after $TimeoutSeconds seconds."
            $cleanup = Stop-OwnedProcessTree -ProcessId $ownedPid
            if (-not $process.WaitForExit(10000)) {
                throw "Owned Cargo root PID $ownedPid did not exit after bounded tree termination."
            }
            $cleanup.root_exited = $true
        }
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        $exitCode = $process.ExitCode
    }
    catch {
        if ([string]::IsNullOrWhiteSpace($failure)) {
            $failure = $_.Exception.Message
        }
        else {
            $failure += ' | ' + $_.Exception.Message
        }
        $stderr = $stderr + [Environment]::NewLine + $failure
    }
    finally {
        if ($null -ne $process) {
            try {
                if ($processStarted -and -not $process.HasExited) {
                    $cleanup = Stop-OwnedProcessTree -ProcessId $ownedPid
                    if ($process.WaitForExit(10000)) {
                        $cleanup.root_exited = $true
                    }
                }
            }
            catch {
                if ([string]::IsNullOrWhiteSpace($cleanup.error)) {
                    $cleanup.error = $_.Exception.Message
                }
            }
            finally {
                $process.Dispose()
                $disposed = $true
                $cleanup.process_disposed = $true
            }
        }
        $watch.Stop()
        Write-Utf8File -LiteralPath $stdoutPath -Content $stdout
        Write-Utf8File -LiteralPath $stderrPath -Content $stderr
    }
    $observedName = if ([string]::IsNullOrWhiteSpace([string]$TestEntry.IntegrationTarget)) {
        [string]$TestEntry.Test
    }
    else {
        ([string]$TestEntry.Test).Substring(([string]$TestEntry.Test).LastIndexOf(':') + 1)
    }
    $combinedOutput = $stdout + [Environment]::NewLine + $stderr
    $observation = Get-ExactTestObservation -Output $combinedOutput -ExpectedName $observedName
    if ($exitCode -eq 0 -and ($observation.count -ne 1 -or $observation.status -cne 'ok')) {
        $observationFailure = "Cargo returned code 0 but exact test '$observedName' was observed $($observation.count) time(s) with status '$($observation.status)'; exactly one ok result is required."
        if ([string]::IsNullOrWhiteSpace($failure)) { $failure = $observationFailure } else { $failure += ' | ' + $observationFailure }
    }
    $passed = $exitCode -eq 0 -and $observation.count -eq 1 -and $observation.status -ceq 'ok' -and
        -not $timedOut -and [string]::IsNullOrWhiteSpace($failure) -and $disposed -and [bool]$cleanup.root_exited
    return [pscustomobject]@{
        id = $TestEntry.Id
        criteria = @($TestEntry.Criteria)
        coverage = @($TestEntry.Coverage)
        package = $TestEntry.Package
        integration_target = $TestEntry.IntegrationTarget
        exact_test = $TestEntry.Test
        command = $display
        command_file = $commandPath
        command_sha256 = Get-FileSha256 -LiteralPath $commandPath
        stdout_log = $stdoutPath
        stdout_sha256 = Get-FileSha256 -LiteralPath $stdoutPath
        stderr_log = $stderrPath
        stderr_sha256 = Get-FileSha256 -LiteralPath $stderrPath
        started_at_utc = $start.ToString('o')
        completed_at_utc = ([DateTimeOffset]::UtcNow).ToString('o')
        elapsed_ms = [int64]$watch.ElapsedMilliseconds
        timeout_seconds = $TimeoutSeconds
        timed_out = $timedOut
        exit_code = $exitCode
        observed_test_name = $observedName
        observed_named_test_count = $observation.count
        observed_named_test_status = $observation.status
        cleanup = $cleanup
        passed = $passed
        failure = $failure
    }
}

function Test-CriterionPassed {
    param(
        [Parameter(Mandatory)][object[]]$Results,
        [Parameter(Mandatory)][string]$Criterion,
        [Parameter(Mandatory)][int]$ExpectedCount,
        [Parameter(Mandatory)][bool]$SourcesUnchanged,
        [Parameter(Mandatory)][bool]$RepositoryIdentityStable
    )
    $selected = @($Results | Where-Object { @($_.criteria) -contains $Criterion })
    return $SourcesUnchanged -and $RepositoryIdentityStable -and
        $selected.Count -eq $ExpectedCount -and
        @($selected | Where-Object { -not $_.passed }).Count -eq 0
}

function Write-ArtifactHashManifest {
    param([Parameter(Mandatory)][string]$ResolvedOutput)
    $manifestPath = Join-Path $ResolvedOutput 'ARTIFACTS.sha256'
    $files = @()
    if (Test-Path -LiteralPath $ResolvedOutput -PathType Container) {
        $files += @(Get-ChildItem -LiteralPath $ResolvedOutput -File | Where-Object {
            -not $_.FullName.Equals($manifestPath, [StringComparison]::OrdinalIgnoreCase)
        })
        foreach ($directoryName in @('commands', 'logs')) {
            $directory = Join-Path $ResolvedOutput $directoryName
            if (Test-Path -LiteralPath $directory -PathType Container) {
                $files += @(Get-ChildItem -LiteralPath $directory -File -Recurse)
            }
        }
    }
    $files = @($files | Sort-Object FullName -Unique)
    $hashLines = @($files | ForEach-Object {
        $relative = $_.FullName.Substring($ResolvedOutput.Length).TrimStart('\').Replace('\', '/')
        "$(Get-FileSha256 -LiteralPath $_.FullName) *$relative"
    })
    Write-Utf8File -LiteralPath $manifestPath -Content (($hashLines -join [Environment]::NewLine) + [Environment]::NewLine)
    return [pscustomobject]@{
        path = $manifestPath
        artifact_count = $files.Count
        relative_paths = @($files | ForEach-Object {
            $_.FullName.Substring($ResolvedOutput.Length).TrimStart('\').Replace('\', '/')
        })
    }
}

function Write-AbortEvidence {
    param(
        [Parameter(Mandatory)][string]$ResolvedOutput,
        [Parameter(Mandatory)][string]$Phase,
        [Parameter(Mandatory)][string]$Failure,
        [object]$Cleanup
    )
    $recorded = [DateTimeOffset]::UtcNow.ToString('o')
    $ledgerPath = Join-Path $ResolvedOutput 'R2-038-R2-039-PERSISTENCE-FAILURE-PROOF.json'
    $reportPath = Join-Path $ResolvedOutput 'R2-038-R2-039-PERSISTENCE-FAILURE-PROOF.md'
    $abortPath = Join-Path $ResolvedOutput 'ABORT.json'
    $abort = [ordered]@{
        schema = 'forge.gate2.persistence-failure-proof.abort'
        schema_version = 1
        recorded_at_utc = $recorded
        phase = $Phase
        failure = $Failure
        gate_qualified = $false
        human_review_performed = $false
        ci = [ordered]@{ status = $script:CiStatus }
        cleanup = $Cleanup
    }
    $ledger = [ordered]@{
        schema = 'forge.gate2.persistence-failure-proof'
        schema_version = 1
        recorded_at_utc = $recorded
        gate = 'Gate 2'
        gate_qualified = $false
        human_review_performed = $false
        ci = [ordered]@{ status = $script:CiStatus }
        campaign = [ordered]@{ passed = $false; status = 'failed_closed'; phase = $Phase; failure = $Failure }
        r2_038 = [ordered]@{ programmatic_acquired = $false; status = 'not_acquired' }
        r2_039 = [ordered]@{ programmatic_coverage_acquired = $false; gui_recovery_workflow_reviewed = $false; criterion_qualified = $false; status = 'not_acquired' }
        cleanup = $Cleanup
        abort_manifest = $abortPath
    }
    try {
        Write-Utf8File -LiteralPath $abortPath -Content ($abort | ConvertTo-Json -Depth 100)
        Write-Utf8File -LiteralPath $ledgerPath -Content ($ledger | ConvertTo-Json -Depth 100)
        $report = @(
            '# R2-038 / R2-039 - ABORT fail-closed'
            ''
            '- `gate_qualified=false`'
            '- `human_review_performed=false`'
            ('- CI: **{0}**' -f $script:CiStatus)
            ('- Phase: `{0}`' -f $Phase)
            ('- Failure: `{0}`' -f $Failure)
            ('- Abort manifest: `{0}`' -f $abortPath)
        ) -join [Environment]::NewLine
        Write-Utf8File -LiteralPath $reportPath -Content ($report + [Environment]::NewLine)
    }
    finally {
        [void](Write-ArtifactHashManifest -ResolvedOutput $ResolvedOutput)
    }
}

function Invoke-SelfTest {
    $repo = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
    $manifest = @(Get-TestManifest)
    Assert-ManifestContracts -ResolvedRepo $repo -Manifest $manifest
    if ($manifest.Count -ne 13) {
        throw "Self-test expected 13 exact tests, found $($manifest.Count)."
    }
    $requiredCoverage = @(
        'process_crash_after_file_fsync_before_replace', 'abandoned_staging',
        'old_or_new_never_partial', 'conflict', 'concurrent_lock', 'oversize',
        'invalid_or_truncated_candidate', 'foreign_identity', 'missing_source_archive',
        'missing_object', 'source_lock', 'stale_choice', 'promotion_preserves_versions',
        'exact_reopen'
    )
    $coverage = @($manifest | ForEach-Object { $_.Coverage } | Sort-Object -Unique)
    foreach ($required in $requiredCoverage) {
        if ($coverage -notcontains $required) {
            throw "Self-test coverage map is missing '$required'."
        }
    }
    $r2038Count = @($manifest | Where-Object { @($_.Criteria) -contains 'R2-038' }).Count
    $r2039Count = @($manifest | Where-Object { @($_.Criteria) -contains 'R2-039' }).Count
    $fake = @()
    foreach ($entry in $manifest) {
        $fake += [pscustomobject]@{ criteria = @($entry.Criteria); passed = $true }
    }
    if (-not (Test-CriterionPassed -Results $fake -Criterion 'R2-038' -ExpectedCount $r2038Count -SourcesUnchanged $true -RepositoryIdentityStable $true)) {
        throw 'Self-test could not acquire the R2-038 programmatic result from a complete passing set.'
    }
    $fake[0].passed = $false
    if (Test-CriterionPassed -Results $fake -Criterion 'R2-038' -ExpectedCount $r2038Count -SourcesUnchanged $true -RepositoryIdentityStable $true) {
        throw 'Self-test criterion aggregation did not fail closed.'
    }
    if (-not (Test-IsDriveEPath -LiteralPath 'E:\gate2-self-test')) {
        throw 'Self-test E: path validation failed.'
    }
    if (Test-IsDriveEPath -LiteralPath 'C:\gate2-self-test') {
        throw 'Self-test accepted a non-E: path.'
    }
    if ($r2038Count -lt 1 -or $r2039Count -lt 1) {
        throw 'Self-test criterion selection is empty.'
    }
    $ciExpected = 'NOT RUN ' + [char]0x2014 + ' infrastructure billing'
    if (-not $script:CiStatus.Equals($ciExpected, [StringComparison]::Ordinal) -or [int][char]$script:CiStatus[8] -ne 0x2014) {
        throw 'Self-test CI status is not the exact required Unicode string.'
    }
    $ciJson = [ordered]@{ ci = [ordered]@{ status = $script:CiStatus } } | ConvertTo-Json -Compress
    $ciRoundTrip = $script:Utf8NoBom.GetString($script:Utf8NoBom.GetBytes($ciJson)) | ConvertFrom-Json
    if (-not ([string]$ciRoundTrip.ci.status).Equals($ciExpected, [StringComparison]::Ordinal)) {
        throw 'Self-test CI status did not survive the exact UTF-8 artifact round-trip.'
    }
    $cargoArguments = @(Get-CargoArguments -TestEntry $manifest[0])
    if ($cargoArguments -notcontains '--offline' -or $cargoArguments -notcontains '--locked') {
        throw 'Self-test Cargo arguments are not locked and offline.'
    }
    $single = Get-ExactTestObservation -Output 'test exact_name ... ok' -ExpectedName 'exact_name'
    $missing = Get-ExactTestObservation -Output 'test result: ok. 0 passed; 0 failed' -ExpectedName 'exact_name'
    $duplicate = Get-ExactTestObservation -Output ("test exact_name ... ok`ntest exact_name ... ok") -ExpectedName 'exact_name'
    if ($single.count -ne 1 -or $single.status -cne 'ok' -or $missing.count -ne 0 -or $duplicate.count -ne 2) {
        throw 'Self-test exact named-test observation is not fail-closed.'
    }
    $pinned = @(Get-PinnedRelativePaths -ResolvedRepo $repo -Manifest $manifest)
    foreach ($requiredPath in @(
        'rust-toolchain.toml', '.cargo/config.toml',
        'crates/forge_core/src/lib.rs', 'crates/forge_project/src/lib.rs',
        'crates/forge_project/src/workspace/file_session.rs',
        'crates/forge_project/src/workspace/session.rs',
        'crates/forge_project/src/authoring/host/recovery.rs'
    )) {
        if ($pinned -notcontains $requiredPath) {
            throw "Self-test source closure is missing '$requiredPath'."
        }
    }
    $sameBefore = @([pscustomobject]@{ path = 'dirty.rs'; state = 'file'; sha256 = 'aa'; bytes = 2 })
    $sameAfter = @([pscustomobject]@{ path = 'dirty.rs'; state = 'file'; sha256 = 'aa'; bytes = 2 })
    $changedAfter = @([pscustomobject]@{ path = 'dirty.rs'; state = 'file'; sha256 = 'bb'; bytes = 2 })
    if (@(Compare-GitWorktreeSnapshots -Before $sameBefore -After $sameAfter).Count -ne 0 -or
        @(Compare-GitWorktreeSnapshots -Before $sameBefore -After $changedAfter).Count -ne 1) {
        throw 'Self-test full Git snapshot comparison failed to protect an already-dirty path.'
    }
    $parseTokens = $null
    $parseErrors = $null
    $selfAst = [System.Management.Automation.Language.Parser]::ParseFile(
        $PSCommandPath,
        [ref]$parseTokens,
        [ref]$parseErrors
    )
    if ($parseErrors.Count -ne 0) {
        throw 'Self-test could not parse the harness while checking CargoHome semantics.'
    }
    $cargoHomeParameters = @($selfAst.ParamBlock.Parameters | Where-Object {
        $_.Name.VariablePath.UserPath -ceq 'CargoHome'
    })
    if ($cargoHomeParameters.Count -ne 1 -or $null -ne $cargoHomeParameters[0].DefaultValue) {
        throw 'Self-test requires CargoHome to be caller-supplied with no assumed path.'
    }
    $cargoHomeMandatory = @($cargoHomeParameters[0].Attributes | Where-Object {
        $_.TypeName.Name -ceq 'Parameter' -and
        @($_.NamedArguments | Where-Object { $_.ArgumentName -ceq 'Mandatory' }).Count -eq 1
    }).Count -eq 1
    if (-not $cargoHomeMandatory) {
        throw 'Self-test requires CargoHome to be mandatory for Proof.'
    }
    $selfTestParent = [System.IO.Path]::GetFullPath((Split-Path -Parent $repo))
    if (-not (Test-IsDriveEPath -LiteralPath $selfTestParent)) {
        throw 'Self-test abort evidence root must be on physical E:.'
    }
    $selfTestLeaf = '.gate2-persistence-selftest-' + [Guid]::NewGuid().ToString('N')
    $selfTestOutput = [System.IO.Path]::GetFullPath((Join-Path $selfTestParent $selfTestLeaf))
    if (-not (Test-PathWithinDirectory -LiteralPath $selfTestOutput -Directory $selfTestParent) -or
        -not ([System.IO.Path]::GetFileName($selfTestOutput)).StartsWith('.gate2-persistence-selftest-', [StringComparison]::Ordinal)) {
        throw 'Self-test temporary output target failed its deletion boundary check.'
    }
    try {
        [void](New-Item -ItemType Directory -Path $selfTestOutput)
        Write-Utf8File -LiteralPath (Join-Path $selfTestOutput 'git-status-before.txt') -Content "self-test snapshot`n"
        Write-AbortEvidence -ResolvedOutput $selfTestOutput -Phase 'selftest-preflight' -Failure 'expected self-test abort' -Cleanup ([ordered]@{
            environment_restored = $true
            owned_processes = @()
            all_owned_roots_exited = $true
            all_processes_disposed = $true
        })
        $artifactManifestPath = Join-Path $selfTestOutput 'ARTIFACTS.sha256'
        $requiredArtifacts = @(
            'ABORT.json',
            'R2-038-R2-039-PERSISTENCE-FAILURE-PROOF.json',
            'R2-038-R2-039-PERSISTENCE-FAILURE-PROOF.md',
            'git-status-before.txt'
        )
        if (-not (Test-Path -LiteralPath $artifactManifestPath -PathType Leaf)) {
            throw 'Self-test abort evidence did not produce ARTIFACTS.sha256.'
        }
        $observedArtifacts = @{}
        foreach ($hashLine in @(Get-Content -LiteralPath $artifactManifestPath)) {
            if ($hashLine -cnotmatch '^([0-9a-f]{64}) \*(.+)$') {
                throw "Self-test artifact manifest has an invalid line: $hashLine"
            }
            $relativeArtifact = $Matches[2]
            if ($observedArtifacts.ContainsKey($relativeArtifact)) {
                throw "Self-test artifact manifest has a duplicate path: $relativeArtifact"
            }
            $artifactPath = Join-Path $selfTestOutput ($relativeArtifact.Replace('/', '\'))
            if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
                throw "Self-test artifact manifest names a missing file: $relativeArtifact"
            }
            $actualHash = Get-FileSha256 -LiteralPath $artifactPath
            if ($actualHash -cne $Matches[1]) {
                throw "Self-test artifact hash mismatch: $relativeArtifact"
            }
            $observedArtifacts[$relativeArtifact] = $actualHash
        }
        foreach ($requiredArtifact in $requiredArtifacts) {
            if (-not $observedArtifacts.ContainsKey($requiredArtifact)) {
                throw "Self-test artifact manifest does not cover required abort evidence: $requiredArtifact"
            }
        }
    }
    finally {
        if (Test-Path -LiteralPath $selfTestOutput) {
            if (-not (Test-PathWithinDirectory -LiteralPath $selfTestOutput -Directory $selfTestParent) -or
                -not ([System.IO.Path]::GetFileName($selfTestOutput)).StartsWith('.gate2-persistence-selftest-', [StringComparison]::Ordinal)) {
                throw 'Self-test refused to remove an output outside its exact bounded target.'
            }
            Remove-Item -LiteralPath $selfTestOutput -Recurse -Force
        }
    }
    $selfSource = [System.IO.File]::ReadAllText($PSCommandPath)
    foreach ($requiredMarker in @(
        'WaitForExit($TimeoutSeconds * 1000)',
        'Stop-OwnedProcessTree -ProcessId $ownedPid',
        '$process.Dispose()',
        'observed_named_test_count',
        'Write-AbortEvidence',
        'Write-ArtifactHashManifest',
        'CARGO_HOME = $resolvedCargoHome',
        "'--offline'"
    )) {
        if ($selfSource.IndexOf($requiredMarker, [StringComparison]::Ordinal) -lt 0) {
            throw "Self-test harness safety marker is missing: $requiredMarker"
        }
    }
    Write-Output "GATE2_PERSISTENCE_FAILURE_PROOF_SELF_TEST_PASS tests=$($manifest.Count) r2_038=$r2038Count r2_039=$r2039Count ci_exact=true ci_codepoint=U+2014 offline=true timeout_bounded=true abort_manifest_verified=true cargo_home_assumed=false cargo_not_run=true"
}

if ($SelfTest) {
    Invoke-SelfTest
    return
}

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Split-Path -Parent $PSScriptRoot
}
$resolvedRepo = [System.IO.Path]::GetFullPath($RepoRoot)
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
$resolvedCargoHome = [System.IO.Path]::GetFullPath($CargoHome)
if (-not (Test-IsDriveEPath -LiteralPath $resolvedOutput)) {
    throw "OutputDirectory must be an absolute path on physical E:. Received '$resolvedOutput'."
}
Assert-NoExistingReparseAncestor -LiteralPath $resolvedOutput -Description 'OutputDirectory'
if (Test-Path -LiteralPath $resolvedOutput) {
    throw "OutputDirectory must be fresh and absent: $resolvedOutput"
}
if (Test-PathWithinDirectory -LiteralPath $resolvedOutput -Directory $resolvedRepo) {
    throw 'OutputDirectory must be outside RepoRoot so qualification artifacts cannot alter the dirty worktree.'
}

[void](New-Item -ItemType Directory -Path $resolvedOutput)
$script:CurrentPhase = 'preflight'
$script:EvidenceFinalized = $false
$script:TopCleanupReceipt = [ordered]@{
    environment_restored = $true
    owned_processes = @()
    all_owned_roots_exited = $true
    all_processes_disposed = $true
}
trap {
    $trapFailure = $_.Exception.Message
    if (-not $script:EvidenceFinalized) {
        try {
            Write-AbortEvidence -ResolvedOutput $resolvedOutput -Phase $script:CurrentPhase -Failure $trapFailure -Cleanup $script:TopCleanupReceipt
        }
        catch {
            [Console]::Error.WriteLine('Unable to write fail-closed abort evidence: ' + $_.Exception.Message)
        }
    }
    exit 1
}
$commandsDirectory = Join-Path $resolvedOutput 'commands'
$logsDirectory = Join-Path $resolvedOutput 'logs'
$cargoTargetDirectory = Join-Path $resolvedOutput 'cargo-target'
$tempDirectory = Join-Path $resolvedOutput 'temp'
foreach ($directory in @($commandsDirectory, $logsDirectory, $cargoTargetDirectory, $tempDirectory)) {
    [void](New-Item -ItemType Directory -Path $directory)
}

if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'This local persistence proof harness requires Windows.'
}
if (-not (Test-Path -LiteralPath $resolvedRepo -PathType Container)) {
    throw "RepoRoot does not exist: $resolvedRepo"
}
if (-not (Test-IsDriveEPath -LiteralPath $resolvedRepo)) {
    throw "RepoRoot must be on physical E:. Received '$resolvedRepo'."
}
if (-not (Test-Path -LiteralPath $resolvedCargoHome -PathType Container)) {
    throw "CargoHome must be an existing offline cache directory: $resolvedCargoHome"
}
if (-not (Test-IsDriveEPath -LiteralPath $resolvedCargoHome)) {
    throw "CargoHome must be on physical E:. Received '$resolvedCargoHome'."
}
Assert-NoExistingReparseAncestor -LiteralPath $resolvedRepo -Description 'RepoRoot'
Assert-NoExistingReparseAncestor -LiteralPath $resolvedCargoHome -Description 'CargoHome'
if ((Test-PathWithinDirectory -LiteralPath $resolvedCargoHome -Directory $resolvedRepo) -or
    (Test-PathWithinDirectory -LiteralPath $resolvedRepo -Directory $resolvedCargoHome) -or
    (Test-PathWithinDirectory -LiteralPath $resolvedOutput -Directory $resolvedCargoHome) -or
    (Test-PathWithinDirectory -LiteralPath $resolvedCargoHome -Directory $resolvedOutput)) {
    throw 'CargoHome, RepoRoot, and OutputDirectory must be disjoint.'
}

$gitRoot = Get-GitOutput -ResolvedRepo $resolvedRepo -Arguments @('rev-parse', '--show-toplevel')
$gitRoot = [System.IO.Path]::GetFullPath($gitRoot)
if (-not $gitRoot.Equals($resolvedRepo, [StringComparison]::OrdinalIgnoreCase)) {
    throw "RepoRoot must be the Git worktree root. Git reported '$gitRoot'."
}
$branchBefore = Get-GitOutput -ResolvedRepo $resolvedRepo -Arguments @('rev-parse', '--abbrev-ref', 'HEAD')
if ($branchBefore -cne 'Rust') {
    throw "Persistence proof is restricted to branch Rust; current branch is '$branchBefore'."
}
$headBefore = Get-GitOutput -ResolvedRepo $resolvedRepo -Arguments @('rev-parse', 'HEAD')
$manifest = @(Get-TestManifest)
Assert-ManifestContracts -ResolvedRepo $resolvedRepo -Manifest $manifest
$pinnedPaths = @(Get-PinnedRelativePaths -ResolvedRepo $resolvedRepo -Manifest $manifest)
$snapshotBefore = @(Get-PinnedSnapshot -ResolvedRepo $resolvedRepo -RelativePaths $pinnedPaths)
$gitWorktreeBefore = @(Get-GitWorktreeSnapshot -ResolvedRepo $resolvedRepo)
$statusBefore = Get-GitOutput -ResolvedRepo $resolvedRepo -Arguments @('status', '--porcelain=v2', '--branch', '--untracked-files=all')
$indexBefore = Get-GitOutput -ResolvedRepo $resolvedRepo -Arguments @('ls-files', '--stage')
Write-Utf8File -LiteralPath (Join-Path $resolvedOutput 'git-status-before.txt') -Content ($statusBefore + [Environment]::NewLine)
Write-Utf8File -LiteralPath (Join-Path $resolvedOutput 'git-index-before.txt') -Content ($indexBefore + [Environment]::NewLine)
Write-Utf8File -LiteralPath (Join-Path $resolvedOutput 'git-worktree-before.json') -Content ($gitWorktreeBefore | ConvertTo-Json -Depth 20)

$cargo = Get-Command cargo -CommandType Application -ErrorAction Stop | Select-Object -First 1
$cargoPath = $cargo.Source
if ([string]::IsNullOrWhiteSpace($cargoPath)) { $cargoPath = $cargo.Path }
$cargoPath = [System.IO.Path]::GetFullPath($cargoPath)
$rustc = Get-Command rustc -CommandType Application -ErrorAction Stop | Select-Object -First 1
$rustcPath = $rustc.Source
if ([string]::IsNullOrWhiteSpace($rustcPath)) { $rustcPath = $rustc.Path }
$rustcPath = [System.IO.Path]::GetFullPath($rustcPath)
$cargoVersion = @(& $cargoPath --version 2>&1) -join [Environment]::NewLine
if ($LASTEXITCODE -ne 0) { throw "cargo --version failed: $cargoVersion" }
$rustcVersion = @(& $rustcPath --version --verbose 2>&1) -join [Environment]::NewLine
if ($LASTEXITCODE -ne 0) { throw "rustc --version --verbose failed: $rustcVersion" }
$environmentNames = @(
    'CARGO_HOME', 'CARGO_TARGET_DIR', 'CARGO_NET_OFFLINE', 'CARGO_TERM_COLOR',
    'CARGO_INCREMENTAL', 'TEMP', 'TMP', 'TMPDIR', 'RUSTC_WRAPPER', 'RUSTC_WORKSPACE_WRAPPER'
)
$environmentBefore = @{}
foreach ($name in $environmentNames) {
    $environmentBefore[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$environmentApplied = [ordered]@{
    CARGO_HOME = $resolvedCargoHome
    CARGO_TARGET_DIR = $cargoTargetDirectory
    CARGO_NET_OFFLINE = 'true'
    CARGO_TERM_COLOR = 'never'
    CARGO_INCREMENTAL = '0'
    TEMP = $tempDirectory
    TMP = $tempDirectory
    TMPDIR = $tempDirectory
    RUSTC_WRAPPER = ''
    RUSTC_WORKSPACE_WRAPPER = ''
}
$campaignStoragePaths = @($resolvedOutput, $resolvedCargoHome, $cargoTargetDirectory, $tempDirectory)
$allCampaignStorageOnE = @($campaignStoragePaths | Where-Object {
    -not (Test-IsDriveEPath -LiteralPath $_)
}).Count -eq 0
if (-not $allCampaignStorageOnE) {
    throw 'All mutable campaign storage must be on physical E:.'
}

$startedAt = [DateTimeOffset]::UtcNow
$results = [System.Collections.Generic.List[object]]::new()
$campaignFailure = $null
$failurePhase = $null
$script:CurrentPhase = 'campaign'
try {
    $script:TopCleanupReceipt.environment_restored = $false
    foreach ($name in $environmentApplied.Keys) {
        [Environment]::SetEnvironmentVariable($name, [string]$environmentApplied[$name], 'Process')
    }
    for ($index = 0; $index -lt $manifest.Count; $index += 1) {
        $result = Invoke-CargoTestLogged -CargoPath $cargoPath -ResolvedRepo $resolvedRepo -TestEntry $manifest[$index] -Ordinal ($index + 1) -CommandsDirectory $commandsDirectory -LogsDirectory $logsDirectory -TimeoutSeconds $CargoTestTimeoutSeconds
        $results.Add($result)
        $script:TopCleanupReceipt.owned_processes = @($results | ForEach-Object { $_.cleanup })
        $script:TopCleanupReceipt.all_owned_roots_exited = @($results | Where-Object { -not [bool]$_.cleanup.root_exited }).Count -eq 0
        $script:TopCleanupReceipt.all_processes_disposed = @($results | Where-Object { -not [bool]$_.cleanup.process_disposed }).Count -eq 0
        if (-not $result.passed) {
            $campaignFailure = "Exact test failed: $($result.exact_test), exit code $($result.exit_code). $($result.failure)"
            $failurePhase = 'campaign'
            break
        }
        $intermediate = @(Get-PinnedSnapshot -ResolvedRepo $resolvedRepo -RelativePaths $pinnedPaths)
        $changes = @(Compare-PinnedSnapshots -Before $snapshotBefore -After $intermediate)
        if ($changes.Count -ne 0) {
            $campaignFailure = 'Pinned source mutation detected during the campaign: ' + ($changes -join '; ')
            $failurePhase = 'campaign'
            break
        }
        $currentBranch = Get-GitOutput -ResolvedRepo $resolvedRepo -Arguments @('rev-parse', '--abbrev-ref', 'HEAD')
        $currentHead = Get-GitOutput -ResolvedRepo $resolvedRepo -Arguments @('rev-parse', 'HEAD')
        if ($currentBranch -cne 'Rust' -or $currentHead -cne $headBefore) {
            $campaignFailure = "Repository identity changed during the campaign: branch=$currentBranch head=$currentHead."
            $failurePhase = 'campaign'
            break
        }
    }
}
catch {
    $campaignFailure = $_.Exception.Message
    $failurePhase = 'campaign'
}
finally {
    foreach ($name in $environmentNames) {
        [Environment]::SetEnvironmentVariable($name, $environmentBefore[$name], 'Process')
    }
    $script:TopCleanupReceipt.environment_restored = $true
}

$script:CurrentPhase = 'postflight'
$completedAt = [DateTimeOffset]::UtcNow
$snapshotAfter = @(Get-PinnedSnapshot -ResolvedRepo $resolvedRepo -RelativePaths $pinnedPaths)
$sourceChanges = @(Compare-PinnedSnapshots -Before $snapshotBefore -After $snapshotAfter)
$sourcesUnchanged = $sourceChanges.Count -eq 0
$branchAfter = Get-GitOutput -ResolvedRepo $resolvedRepo -Arguments @('rev-parse', '--abbrev-ref', 'HEAD')
$headAfter = Get-GitOutput -ResolvedRepo $resolvedRepo -Arguments @('rev-parse', 'HEAD')
$gitWorktreeAfter = @(Get-GitWorktreeSnapshot -ResolvedRepo $resolvedRepo)
$gitWorktreeChanges = @(Compare-GitWorktreeSnapshots -Before $gitWorktreeBefore -After $gitWorktreeAfter)
$statusAfter = Get-GitOutput -ResolvedRepo $resolvedRepo -Arguments @('status', '--porcelain=v2', '--branch', '--untracked-files=all')
$indexAfter = Get-GitOutput -ResolvedRepo $resolvedRepo -Arguments @('ls-files', '--stage')
$gitWorktreeStable = $gitWorktreeChanges.Count -eq 0 -and $statusBefore -ceq $statusAfter -and $indexBefore -ceq $indexAfter
$repositoryIdentityStable = $branchAfter -ceq 'Rust' -and $headAfter -ceq $headBefore -and $gitWorktreeStable
Write-Utf8File -LiteralPath (Join-Path $resolvedOutput 'git-status-after.txt') -Content ($statusAfter + [Environment]::NewLine)
Write-Utf8File -LiteralPath (Join-Path $resolvedOutput 'git-index-after.txt') -Content ($indexAfter + [Environment]::NewLine)
Write-Utf8File -LiteralPath (Join-Path $resolvedOutput 'git-worktree-after.json') -Content ($gitWorktreeAfter | ConvertTo-Json -Depth 20)

if (-not $sourcesUnchanged) {
    $message = 'Pinned source mutation detected: ' + ($sourceChanges -join '; ')
    if ($null -eq $failurePhase) { $failurePhase = 'postflight' }
    if ([string]::IsNullOrWhiteSpace($campaignFailure)) { $campaignFailure = $message } else { $campaignFailure += ' | ' + $message }
}
if (-not $repositoryIdentityStable) {
    $message = "Complete Git snapshot changed: branch=$branchAfter head=$headAfter worktree_changes=$($gitWorktreeChanges.Count) status_equal=$($statusBefore -ceq $statusAfter) index_equal=$($indexBefore -ceq $indexAfter)."
    if ($null -eq $failurePhase) { $failurePhase = 'postflight' }
    if ([string]::IsNullOrWhiteSpace($campaignFailure)) { $campaignFailure = $message } else { $campaignFailure += ' | ' + $message }
}
if ($results.Count -ne $manifest.Count) {
    $message = "Only $($results.Count) of $($manifest.Count) exact tests ran."
    if ($null -eq $failurePhase) { $failurePhase = 'campaign' }
    if ([string]::IsNullOrWhiteSpace($campaignFailure)) { $campaignFailure = $message } else { $campaignFailure += ' | ' + $message }
}
if (-not $script:TopCleanupReceipt.environment_restored -or
    -not $script:TopCleanupReceipt.all_owned_roots_exited -or
    -not $script:TopCleanupReceipt.all_processes_disposed) {
    $message = 'Cleanup receipt is incomplete; environment, owned process exit, or Dispose verification failed.'
    if ($null -eq $failurePhase) { $failurePhase = 'postflight' }
    if ([string]::IsNullOrWhiteSpace($campaignFailure)) { $campaignFailure = $message } else { $campaignFailure += ' | ' + $message }
}

$r2038Expected = @($manifest | Where-Object { @($_.Criteria) -contains 'R2-038' }).Count
$r2039Expected = @($manifest | Where-Object { @($_.Criteria) -contains 'R2-039' }).Count
$r2038Acquired = Test-CriterionPassed -Results @($results) -Criterion 'R2-038' -ExpectedCount $r2038Expected -SourcesUnchanged $sourcesUnchanged -RepositoryIdentityStable $repositoryIdentityStable
$r2039Programmatic = Test-CriterionPassed -Results @($results) -Criterion 'R2-039' -ExpectedCount $r2039Expected -SourcesUnchanged $sourcesUnchanged -RepositoryIdentityStable $repositoryIdentityStable
$campaignPassed = [string]::IsNullOrWhiteSpace($campaignFailure) -and $r2038Acquired -and $r2039Programmatic

$ledgerPath = Join-Path $resolvedOutput 'R2-038-R2-039-PERSISTENCE-FAILURE-PROOF.json'
$reportPath = Join-Path $resolvedOutput 'R2-038-R2-039-PERSISTENCE-FAILURE-PROOF.md'
$ledger = [ordered]@{
    schema = 'forge.gate2.persistence-failure-proof'
    schema_version = 1
    recorded_at_utc = $completedAt.ToString('o')
    gate = 'Gate 2'
    gate_qualified = $false
    human_review_performed = $false
    ci = [ordered]@{ status = $script:CiStatus }
    campaign = [ordered]@{
        passed = $campaignPassed
        status = if ($campaignPassed) { 'local_programmatic_proof_acquired' } else { 'failed_closed' }
        failure_phase = $failurePhase
        failure = $campaignFailure
        started_at_utc = $startedAt.ToString('o')
        completed_at_utc = $completedAt.ToString('o')
        exact_tests_expected = $manifest.Count
        exact_tests_executed = $results.Count
        sequential = $true
        cargo_jobs = 1
        rust_test_threads = 1
        cargo_test_timeout_seconds = $CargoTestTimeoutSeconds
        cargo_offline = $true
        exact_named_test_required_once = $true
    }
    r2_038 = [ordered]@{
        label = 'atomic save'
        programmatic_acquired = $r2038Acquired
        status = if ($r2038Acquired) { 'programmatic_acquired' } else { 'not_acquired' }
        expected_exact_tests = $r2038Expected
        power_loss_directory_durability_proven = $false
    }
    r2_039 = [ordered]@{
        label = 'interruption recovery'
        programmatic_coverage_acquired = $r2039Programmatic
        gui_recovery_workflow_reviewed = $false
        human_review_performed = $false
        criterion_qualified = $false
        status = if ($r2039Programmatic) { 'programmatic_acquired_gui_open' } else { 'not_acquired' }
        expected_exact_tests = $r2039Expected
    }
    repository = [ordered]@{
        root = $resolvedRepo
        branch_before = $branchBefore
        branch_after = $branchAfter
        head_before = $headBefore
        head_after = $headAfter
        identity_stable = $repositoryIdentityStable
        dirty_tree_was_preserved = $gitWorktreeStable
        full_status_before_file = (Join-Path $resolvedOutput 'git-status-before.txt')
        full_status_after_file = (Join-Path $resolvedOutput 'git-status-after.txt')
        full_index_before_file = (Join-Path $resolvedOutput 'git-index-before.txt')
        full_index_after_file = (Join-Path $resolvedOutput 'git-index-after.txt')
        full_worktree_before_file = (Join-Path $resolvedOutput 'git-worktree-before.json')
        full_worktree_after_file = (Join-Path $resolvedOutput 'git-worktree-after.json')
        full_worktree_entry_count_before = $gitWorktreeBefore.Count
        full_worktree_entry_count_after = $gitWorktreeAfter.Count
        full_worktree_changes = @($gitWorktreeChanges)
        status_exact = $statusBefore -ceq $statusAfter
        index_exact = $indexBefore -ceq $indexAfter
        pinned_source_closure_count = $pinnedPaths.Count
        pinned_sources_unchanged = $sourcesUnchanged
        pinned_source_changes = @($sourceChanges)
        pinned_sources_before = @($snapshotBefore)
        pinned_sources_after = @($snapshotAfter)
    }
    environment = [ordered]@{
        cargo_path = $cargoPath
        cargo_version = $cargoVersion
        rustc_path = $rustcPath
        rustc_version_verbose = $rustcVersion
        CARGO_HOME = $resolvedCargoHome
        CARGO_TARGET_DIR = $cargoTargetDirectory
        CARGO_NET_OFFLINE = 'true'
        CARGO_TERM_COLOR = 'never'
        TEMP = $tempDirectory
        TMP = $tempDirectory
        TMPDIR = $tempDirectory
        CARGO_INCREMENTAL = '0'
        mutable_storage_paths = @($campaignStoragePaths)
        all_campaign_storage_on_e = $allCampaignStorageOnE
        restored_after_campaign = $script:TopCleanupReceipt.environment_restored
    }
    cleanup = $script:TopCleanupReceipt
    native_candidate = [ordered]@{
        used = $false
        path = $null
        hash = $null
    }
    tests = @($results)
    not_run = @($manifest | Select-Object -Skip $results.Count | ForEach-Object {
        [ordered]@{ id = $_.Id; exact_test = $_.Test; reason = 'campaign stopped fail-closed before this test' }
    })
    limitations = @(
        'R2-038 is programmatic process-interruption evidence only; DocumentStore explicitly does not claim directory durability after sudden power loss on every filesystem.',
        'R2-039 has programmatic recovery coverage only. No Studio GUI recovery gesture or human review is performed by this harness.',
        'The existing tests cover a missing authoring archive and a missing immutable zone object. They do not delete an already-reviewed legacy source in the narrow interval immediately before workspace promotion.',
        'The adapter coordinates cooperating writers with an OS lock and hashes; it is not a sandbox against a hostile process replacing directories concurrently.',
        'No native candidate is built or launched. No Gate 3 work is performed.',
        'gate_qualified remains false regardless of a passing automated campaign.',
        $script:CiStatus
    )
}
Write-Utf8File -LiteralPath $ledgerPath -Content ($ledger | ConvertTo-Json -Depth 100)
$ledgerHash = Get-FileSha256 -LiteralPath $ledgerPath

$lines = [System.Collections.Generic.List[string]]::new()
$campaignLabel = if ($campaignPassed) { 'PASS programmatique local' } else { 'FAIL ferme' }
$r2038Label = if ($r2038Acquired) { 'acquis programmatique' } else { 'non acquis' }
$r2039Label = if ($r2039Programmatic) { 'couverture programmatique acquise, parcours GUI encore ouvert' } else { 'non acquise' }
$lines.Add('# R2-038 / R2-039 - preuve locale de persistance en echec')
$lines.Add('')
$lines.Add(('- Verdict du harness : **{0}**' -f $campaignLabel))
$lines.Add('- `gate_qualified=false`')
$lines.Add('- `human_review_performed=false`')
$lines.Add(('- CI distante : **{0}**' -f $script:CiStatus))
$lines.Add(('- Branche : `{0}` ; HEAD : `{1}`' -f $branchBefore, $headBefore))
$lines.Add('- Candidat natif : **aucun**')
$lines.Add('')
$lines.Add('## Verdicts bornes')
$lines.Add('')
$lines.Add(('- R2-038 sauvegarde atomique : **{0}**.' -f $r2038Label))
$lines.Add(('- R2-039 recuperation : **{0}**.' -f $r2039Label))
$lines.Add('- Gate 2 reste non qualifiee ; aucune Gate 3 n''est ouverte.')
$lines.Add('')
$lines.Add('## Tests exacts')
$lines.Add('')
foreach ($entry in $manifest) {
    $result = @($results | Where-Object { $_.id -ceq $entry.Id })
    $status = if ($result.Count -eq 1) { if ($result[0].passed) { 'PASS' } else { "FAIL (code $($result[0].exit_code))" } } else { 'NOT RUN' }
    $lines.Add(('- **{0}** - `{1}` / `{2}` - {3}.' -f $status, $entry.Package, $entry.Test, ($entry.Coverage -join ', ')))
}
$lines.Add('')
$lines.Add('## Integrite et artefacts')
$lines.Add('')
$lines.Add(('- Sources epinglees inchangees : **{0}**.' -f $sourcesUnchanged))
$lines.Add(('- Snapshot Git complet, index, branche et HEAD stables : **{0}**.' -f $repositoryIdentityStable))
$lines.Add(('- `CARGO_HOME={0}`, `CARGO_TARGET_DIR={1}`, `TEMP={2}`, `TMP={2}` ; `--offline` ; stockage mutable sur E: **{3}**.' -f $resolvedCargoHome, $cargoTargetDirectory, $tempDirectory, $allCampaignStorageOnE))
$lines.Add(('- Timeout par test : **{0} s** ; arret borne du seul arbre Cargo possede ; tous les Process disposes : **{1}**.' -f $CargoTestTimeoutSeconds, $script:TopCleanupReceipt.all_processes_disposed))
$lines.Add('- Un code 0 est refuse si le test exact nomme n''apparait pas exactement une fois avec le statut `ok`.')
$lines.Add(('- Ledger : `{0}` (SHA-256 `{1}`).' -f $ledgerPath, $ledgerHash))
$lines.Add(('- Commandes : `{0}` ; logs stdout/stderr et codes dans le ledger : `{1}`.' -f $commandsDirectory, $logsDirectory))
$lines.Add('')
$lines.Add('## Limites')
$lines.Add('')
foreach ($limitation in $ledger.limitations) { $lines.Add(('- {0}' -f $limitation)) }
if (-not [string]::IsNullOrWhiteSpace($campaignFailure)) {
    $lines.Add('')
    $lines.Add(('Erreur bloquante : `{0}`' -f $campaignFailure))
}
Write-Utf8File -LiteralPath $reportPath -Content (($lines -join [Environment]::NewLine) + [Environment]::NewLine)

$abortPath = $null
if (-not $campaignPassed) {
    $abortPath = Join-Path $resolvedOutput 'ABORT.json'
    $abort = [ordered]@{
        schema = 'forge.gate2.persistence-failure-proof.abort'
        schema_version = 1
        recorded_at_utc = ([DateTimeOffset]::UtcNow).ToString('o')
        phase = if ($null -ne $failurePhase) { $failurePhase } else { $script:CurrentPhase }
        failure = $campaignFailure
        gate_qualified = $false
        human_review_performed = $false
        ci = [ordered]@{ status = $script:CiStatus }
        cleanup = $script:TopCleanupReceipt
        ledger = $ledgerPath
        report = $reportPath
    }
    Write-Utf8File -LiteralPath $abortPath -Content ($abort | ConvertTo-Json -Depth 100)
}

[void](Write-ArtifactHashManifest -ResolvedOutput $resolvedOutput)

$script:EvidenceFinalized = $true
if (-not $campaignPassed) {
    [Console]::Error.WriteLine("Gate 2 persistence failure proof failed closed. Ledger: $ledgerPath. Failure: $campaignFailure")
    exit 1
}

Write-Output "GATE2_PERSISTENCE_FAILURE_PROOF_PASS r2_038=programmatic_acquired r2_039=programmatic_acquired_gui_open gate_qualified=false human_review_performed=false ledger=$ledgerPath report=$reportPath"
