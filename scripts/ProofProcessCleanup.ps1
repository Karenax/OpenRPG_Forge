# Only pass a process handle obtained by the caller for its own proof run.
function Stop-OwnedProofProcess {
    param([Parameter(Mandatory)][Diagnostics.Process]$Process,
          [Parameter(Mandatory)][string]$ExpectedExecutable)
    if ($Process.HasExited) { return 'already_exited' }
    if ([IO.Path]::GetFullPath($Process.Path) -ne [IO.Path]::GetFullPath($ExpectedExecutable)) {
        throw 'Proof process identity mismatch; termination refused'
    }
    if ($Process.WaitForExit(1000)) { return 'exited' }
    $Process.Kill()
    if (-not $Process.WaitForExit(5000)) { throw 'Owned proof process did not terminate' }
    return 'terminated'
}

function Test-ProofRuntimeOwnership {
    param([string]$Executable, [string]$ExpectedExecutable,
          [string]$CommandLine, [string]$OutputRoot)
    if (-not $Executable -or -not $CommandLine) { return $false }
    $captureArgument = '"' + (Join-Path $OutputRoot 'readiness.png') + '"'
    $bridgeArgument = '"--bridge-frames" "' + (Join-Path $OutputRoot 'frames') + '"'
    $normalizedCommand = $CommandLine.Replace('/', '\')
    return ([IO.Path]::GetFullPath($Executable) -eq [IO.Path]::GetFullPath($ExpectedExecutable) -and
            ($normalizedCommand.Contains($captureArgument.Replace('/', '\')) -or
             $normalizedCommand.Contains($bridgeArgument.Replace('/', '\'))))
}
