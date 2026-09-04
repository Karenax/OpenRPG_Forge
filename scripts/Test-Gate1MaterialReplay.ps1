#requires -Version 7.0
param([string]$RepoRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$tokens = $null
$parseErrors = $null
$launcherPath = Join-Path $RepoRoot 'scripts/Invoke-Gate1MaterialReplay.ps1'
$ast = [Management.Automation.Language.Parser]::ParseFile($launcherPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw ($parseErrors | Out-String) }
# Test only standalone helpers; the launcher body and GPU process are never invoked.
foreach ($function in $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] }, $false)) {
    . ([scriptblock]::Create($function.Extent.Text))
}
$checks = 0
function Confirm-Check {
    param([bool]$Condition, [string]$Label)
    if (-not $Condition) { throw "Self-test failed: $Label" }
    $script:checks++
}
function Confirm-Rejected {
    param([scriptblock]$Action, [string]$Label)
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    Confirm-Check $rejected $Label
}
$temporaryParent = [IO.Path]::GetTempPath().TrimEnd('\', '/')
$temporary = Join-Path $temporaryParent ('forge-material-selftest-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $temporary)
try {
    Confirm-Check ((Get-ReplayProfileFingerprint (Join-Path $temporary 'absent')) -ceq 'absent') 'Absent profile sentinel'
    Confirm-Check (Test-ReplayInside $temporary (Join-Path $temporary 'child')) 'Contained path'
    Confirm-Check (-not (Test-ReplayInside $temporary ($temporary + '-sibling'))) 'Sibling-prefix rejection'
    Confirm-Rejected { Remove-ReplayOwnedTree $temporary $temporary 'forge-material-' } 'Cannot remove owned root'
    Confirm-Rejected { Remove-ReplayOwnedTree $temporaryParent $temporary 'wrong-prefix-' } 'Wrong cleanup prefix'
    $receipt = [pscustomobject]@{
        schema = 'forge-gate1-renderdoc-validation/v1'; status = 'PASS'; backend = 'Vulkan'; results = @()
    }
    foreach ($name in @('clear-01', 'clear-02', 'rain-01', 'rain-02')) {
        [void](New-Item -ItemType Directory -Path (Join-Path $temporary $name))
        $file = Join-Path $temporary "$name/frame.rdc"
        Write-ReplayJson $file @{ synthetic = $name }
        $receipt.results += [pscustomobject]@{ run = $name; capture_file = "$name/frame.rdc"; capture_sha256 = Get-ReplayHash $file }
    }
    Confirm-Check (@(Get-ReplayCaptures $temporary $receipt).Count -eq 4) 'Four exact captures'
    $receipt.results[0].capture_file = '../frame.rdc'
    Confirm-Rejected { Get-ReplayCaptures $temporary $receipt } 'Capture traversal'
    $receipt.results[0].capture_file = 'clear-01/frame.rdc'
    $expectedHash = $receipt.results[0].capture_sha256
    $receipt.results[0].capture_sha256 = '0' * 64
    Confirm-Rejected { Get-ReplayCaptures $temporary $receipt } 'Altered capture hash'
    $receipt.results[0].capture_sha256 = $expectedHash
    $receipt.results[0].run = 'clear-02'
    Confirm-Rejected { Get-ReplayCaptures $temporary $receipt } 'Duplicate/missing run'
    $receipt.results[0].run = 'clear-01'
    $receipt.status = 'FAIL'
    Confirm-Rejected { Get-ReplayCaptures $temporary $receipt } 'Failed parent receipt'
    $receipt.status = 'PASS'
    $receipt.results += $receipt.results[0]
    Confirm-Rejected { Get-ReplayCaptures $temporary $receipt } 'Extra parent run'
    Confirm-Rejected { Assert-ReplayBundle $temporary @([pscustomobject]@{ path = 'extract/RenderDoc_1.46_64/../escape'; size = 0; sha256 = '0' * 64 }) } 'Bundle traversal'
    $jsonPath = Join-Path $temporary 'exclusive.json'
    Write-ReplayJson $jsonPath @{ proof = 1 }
    Confirm-Rejected { Write-ReplayJson $jsonPath @{ proof = 2 } } 'Never overwrite proof'
    $fingerprint = Get-ReplayProfileFingerprint $temporary
    Write-ReplayJson (Join-Path $temporary 'change.json') @{ altered = $true }
    Confirm-Check ((Get-ReplayProfileFingerprint $temporary) -cne $fingerprint) 'Profile contents fingerprint'
    $source = $ast.Extent.Text
    foreach ($contract in @('WaitForExit(180000)', 'CreateNoWindow = $true', 'ProcessWindowStyle]::Hidden', 'FORGE_MATERIAL_PARENT_RECEIPT', 'failure.txt', 'extracted_pending_bound_state_validation', 'parent_receipt_sha256 -ceq $parentHash', 'execution-bundle-inventory-on-failure.json')) {
        Confirm-Check ($source.Contains($contract)) "Launcher static contract $contract"
    }
    Confirm-Check (-not $source.Contains('[scriptblock]::Create')) 'No runtime AST import'
    Write-Output "Gate 1 material replay static/synthetic checks PASS: $checks (no GPU/process launch)"
}
finally { Remove-ReplayOwnedTree $temporaryParent $temporary 'forge-material-selftest-' }
