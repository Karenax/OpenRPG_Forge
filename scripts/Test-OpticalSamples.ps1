$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Compare-OpticalSamples.ps1"
function Assert-True($Value, $Message) { if (-not $Value) { throw $Message } }
$stable = @(@(10, 10, 10), @(11, 10, 10), @(10, 11, 10))
$contrastSamples = @(@(30, 30, 30), @(31, 30, 30), @(30, 31, 30))
$noisy = @(@(0, 0, 0), @(50, 50, 50), @(20, 20, 20))
Assert-True (Get-OpticalSeparation $stable $contrastSamples $stable).separated_and_restored 'Distinct restored effect rejected'
Assert-True (-not (Get-OpticalSeparation $stable $stable $stable).separated_and_restored) 'Unchanged state accepted'
Assert-True (-not (Get-OpticalSeparation $noisy $contrastSamples $noisy).separated_and_restored) 'Temporal noise accepted as effect'
Assert-True (-not (Get-OpticalSeparation $stable $contrastSamples $contrastSamples).separated_and_restored) 'Failed restoration accepted'
$rejected = $false
try { Get-OpticalSeparation @(@(1), @(2)) $contrastSamples $stable | Out-Null } catch { $rejected = $true }
Assert-True $rejected 'Two samples incorrectly accepted'
'5 synthetic optical-comparison checks PASS; no renderer qualification'
