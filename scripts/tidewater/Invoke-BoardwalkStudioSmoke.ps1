#Requires -Version 7.2
# Isolated local proof: source boardwalk -> accepted placements -> edited pose -> reopen -> Playtest.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$CandidateExecutable,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-fA-F]{64}$')][string]$ExpectedSha256,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$FixtureSourceRoot = 'E:\OpenRPG_Forge\.tidewater-integration\studio-tidewater-review-20260927-001',
    [guid]$ZoneUuid = 'e620dd19-ead9-5913-a6d6-0f93109148c0',
    [ValidateRange(30,180)][int]$InteractionTimeoutSeconds = 120,
    [switch]$ConvertSourceStand,
    [switch]$ConvertSourcePier,
    [switch]$ConvertSourcePlaza,
    [switch]$TraverseSourcePier,
    [switch]$TraverseSourcePierHead,
    [switch]$WalkWithoutJump
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$boardwalkInputs = @{
    CandidateExecutable=$CandidateExecutable; ExpectedSha256=$ExpectedSha256;
    OutputDirectory=$OutputDirectory; FixtureSourceRoot=$FixtureSourceRoot;
    ZoneUuid=$ZoneUuid; InteractionTimeoutSeconds=$InteractionTimeoutSeconds;
    ConvertSourceStand=$ConvertSourceStand
}
. (Join-Path $PSScriptRoot 'Invoke-FishStandStudioSmoke.ps1') -SelfTest | Out-Null
foreach ($name in $boardwalkInputs.Keys) { Set-Variable -Name $name -Value $boardwalkInputs[$name] }

$sourcePin = '50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720'
$boardwalkPin = '2d8270d51c0666b256618740a68069e79f6c1dda6c254062194717c937c734e6'
$standPin = 'c739c737914261991fd9aba21c4372e7e9cafa31527f7a4139001a4db8e9b335'
$pierPin = 'f3d89e2a8b92d7f99044b40500d28525a223955b28c1deffe0e9f1deeb8c5e73'
$plazaPin = 'c0b3ce259e9de735204e5ea03e2cce5b6bdf0a332c3ffaca9fab59052712a23a'
if ($TraverseSourcePier -and -not $ConvertSourcePier) { throw 'Pier traversal requires the source pier conversion.' }
if ($TraverseSourcePierHead -and -not $TraverseSourcePier) { throw 'Pier head traversal requires the pier entrance traversal.' }
if ($WalkWithoutJump -and -not $TraverseSourcePier) { throw 'Walking the entrance requires the pier traversal.' }
$expectedPlacements = 26 + [int][bool]$ConvertSourceStand + [int][bool]$ConvertSourcePier + 5 * [int][bool]$ConvertSourcePlaza
$candidate = [IO.Path]::GetFullPath($CandidateExecutable)
$source = [IO.Path]::GetFullPath($FixtureSourceRoot)
$output = [IO.Path]::GetFullPath($OutputDirectory)
foreach ($entry in @(@($candidate,'Candidate'),@($source,'Fixture source'),@($output,'Output'))) {
    Assert-DriveEPath -LiteralPath $entry[0] -Description $entry[1]
    Assert-NoExistingReparseAncestor -LiteralPath $entry[0] -Description $entry[1]
}
if (Test-Path -LiteralPath $output) { throw "OutputDirectory must be fresh: $output" }
if ((Get-FileSha256 -LiteralPath $candidate) -cne (Get-NormalizedSha256 -Value $ExpectedSha256)) {
    throw 'Candidate executable SHA-256 mismatch.'
}
$zoneId = $ZoneUuid.ToString('D').ToLowerInvariant()
$sourceArchive = Join-Path (Join-Path $source $zoneId) 'authoring.json'
$sourceBefore = Get-ArchiveSnapshot -LiteralPath $sourceArchive
if ($sourceBefore.Sha256 -cne $sourcePin -or @(Get-StandArchivePlacements -Archive $sourceArchive).Count -ne 23) {
    throw 'Tidewater fixture is not the reviewed 23-placement source.'
}
$web = Join-Path (Split-Path -Parent $candidate) 'web'
$imports = Join-Path $web 'forge-imports'
$library = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText((Join-Path $imports 'buildings.json')))
if ([string]$library.boardwalk_sha256 -cne $boardwalkPin -or
    (Get-FileSha256 -LiteralPath (Join-Path (Join-Path $imports $boardwalkPin) 'package.json')) -cne $boardwalkPin) {
    throw 'Candidate does not contain the pinned editable boardwalk.'
}
if ($ConvertSourcePier -and ([string]$library.pier_sha256 -cne $pierPin -or
    (Get-FileSha256 -LiteralPath (Join-Path (Join-Path $imports $pierPin) 'package.json')) -cne $pierPin)) {
    throw 'Candidate does not contain the pinned editable pier.'
}
if ($ConvertSourcePlaza -and ([string]$library.plaza_sha256 -cne $plazaPin -or
    (Get-FileSha256 -LiteralPath (Join-Path (Join-Path $imports $plazaPin) 'package.json')) -cne $plazaPin)) {
    throw 'Candidate does not contain the pinned editable plaza.'
}

[void](New-Item -ItemType Directory -Path $output)
$fixture = Join-Path $output 'authoring-root'
$archive = Copy-IsolatedFixture -SourceRoot $source -DestinationRoot $fixture -ZoneId $zoneId
$workspaceArchive = Join-Path $fixture 'workspace-v1\authoring.json'
$objectRoot = Join-Path $fixture 'workspace-v1\objects'
$originalObject = Join-Path $objectRoot "$sourcePin.json"
$workspaceBefore = Get-ArchiveSnapshot -LiteralPath $workspaceArchive
$objectBefore = Get-ArchiveSnapshot -LiteralPath $originalObject
$profile = Join-Path $output 'webview2-user-data'
[void](New-Item -ItemType Directory -Path $profile)
$savedEnvironment = @{}
foreach ($name in @('FORGE_AUTHORING_ROOT','FORGE_AUTHOR_SUBJECT','WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS','WEBVIEW2_USER_DATA_FOLDER')) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$process = $null
$socket = $null
$reservation = $null
$failure = $null
$playtest = $null
$movement = $null
$pierTraversal = $null
$pierHeadTraversal = $null
$savedObjectPath = $null
$initialX = $null
$movedX = $null
$placed = $null
$pierPlaced = $null
$plazaPlaced = @()
$screenshots = [System.Collections.Generic.List[object]]::new()
try {
    $reservation = New-LoopbackPortReservation
    $port = $reservation.Port
    [Environment]::SetEnvironmentVariable('FORGE_AUTHORING_ROOT', $fixture, 'Process')
    [Environment]::SetEnvironmentVariable('FORGE_AUTHOR_SUBJECT', 'tidewater-boardwalk-cdp-smoke', 'Process')
    [Environment]::SetEnvironmentVariable('WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', "--remote-debugging-address=127.0.0.1 --remote-debugging-port=$port", 'Process')
    [Environment]::SetEnvironmentVariable('WEBVIEW2_USER_DATA_FOLDER', $profile, 'Process')
    $reservation.Listener.Stop()
    $process = Start-Process -FilePath $candidate -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $output 'candidate.stdout.log') `
        -RedirectStandardError (Join-Path $output 'candidate.stderr.log')
    [void](Wait-LaunchedExecutableIdentity -Process $process -Candidate $candidate -ExpectedHash (Get-NormalizedSha256 -Value $ExpectedSha256))
    $target = Wait-CdpTarget -Port $port -TimeoutSeconds 60 -Process $process
    $socket = Connect-CdpSocket -WebSocketUrl ([string]$target.webSocketDebuggerUrl)
    [void](Invoke-CdpCommand -Socket $socket -Method 'Runtime.enable')
    [void](Invoke-CdpCommand -Socket $socket -Method 'Page.enable')
    [void](Invoke-CdpCommand -Socket $socket -Method 'Page.bringToFront')
    [void](Wait-CdpReady -Socket $socket -Description 'Studio shell' -TimeoutSeconds $InteractionTimeoutSeconds -Expression @'
(() => ({ready:document.readyState==='complete' && !!document.querySelector('.authoring-shell'),reason:'authoring shell pending'}))()
'@)
    [void](Wait-StandLibrary -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds)
    [void](Open-ManagedTidewaterZone -Socket $socket -ZoneId $zoneId -TimeoutSeconds $InteractionTimeoutSeconds -OpenWorkspace)
    [void](Install-WorldAudit -Socket $socket)
    $initial = Get-WorldState -Socket $socket
    if (@($initial.placement_options).Count -ne 23) { throw 'Expected 23 unedited source placements.' }
    [void](Invoke-UiClick -Socket $socket -Text 'Objets' -Selector '[data-authoring-tab-button]')
    [void](Invoke-UiClick -Socket $socket -Text 'Placer / manipuler')
    [void](Open-DetailsBySummary -Socket $socket -Summary 'Ajouter un objet depuis le catalogue')
    if ($ConvertSourceStand) {
        # The source stand snaps to the imported heightmap, whose verified bytes
        # can still be loading after the zone's controls first appear.
        Start-Sleep -Seconds 15
        [void](Wait-CdpReady -Socket $socket -Description 'source FishStand conversion action' -TimeoutSeconds 30 -Expression @'
(() => { const b=[...document.querySelectorAll('button')].find(x=>x.textContent?.trim()==='Rendre l’échoppe Tidewater source éditable'); return {ready:!!b&&!b.disabled,reason:b?'button disabled':'button absent'}; })()
'@)
        [void](Invoke-UiClick -Socket $socket -Text "Rendre l’échoppe Tidewater source éditable")
        [void](Wait-NewPreview -Socket $socket -PreviousCount ([int]$initial.audit_counts.preview_zone) -TimeoutSeconds $InteractionTimeoutSeconds)
        $standEntry = Get-RequiredAuditedEntry -Snapshot (Get-WorldAuditSnapshot -Socket $socket) -RequestKind 'preview_zone'
        $standProposal = @($standEntry.wire.request.request.action.corrections.placements)
        $sourceStand = @($standProposal | Where-Object { $initial.placement_options.value -notcontains [string]$_.id })
        if ($standProposal.Count -ne 24 -or $sourceStand.Count -ne 1 -or
            [string]$sourceStand[0].asset_hash -cne $standPin -or
            [math]::Abs([double]$sourceStand[0].transform.translation_m.x - 49.9) -gt 0.001 -or
            [math]::Abs([double]$sourceStand[0].transform.translation_m.z + 74.6) -gt 0.001) {
            throw 'Source FishStand conversion did not preserve the pinned pose and bundle.'
        }
        $beforeStandApply = Get-WorldState -Socket $socket
        [void](Invoke-UiClick -Socket $socket -Text 'Appliquer' -Selector '.authoring-apply')
        [void](Wait-WorldState -Socket $socket -Description 'source FishStand accepted' -TimeoutSeconds $InteractionTimeoutSeconds -Predicate {
            param($state)
            [int]$state.audit_counts.apply_zone -eq ([int]$beforeStandApply.audit_counts.apply_zone + 1) -and
                [int]$state.audit_pending -eq 0 -and @($state.placement_options).Count -eq 24
        })
        $initial = Get-WorldState -Socket $socket
    }
    if ($ConvertSourcePier) {
        [void](Wait-CdpReady -Socket $socket -Description 'pier import action' -TimeoutSeconds 45 -Expression @'
(() => { const b=[...document.querySelectorAll('button')].find(x=>x.textContent?.trim()==='Rendre le quai Tidewater source éditable'); return {ready:!!b&&!b.disabled,reason:b?'button disabled':'button absent'}; })()
'@)
        [void](Invoke-UiClick -Socket $socket -Text 'Rendre le quai Tidewater source éditable')
        [void](Wait-NewPreview -Socket $socket -PreviousCount ([int]$initial.audit_counts.preview_zone) -TimeoutSeconds $InteractionTimeoutSeconds)
        $pierEntry = Get-RequiredAuditedEntry -Snapshot (Get-WorldAuditSnapshot -Socket $socket) -RequestKind 'preview_zone'
        $pierProposal = @($pierEntry.wire.request.request.action.corrections.placements)
        $pierPlaced = @($pierProposal | Where-Object { $initial.placement_options.value -notcontains [string]$_.id })
        if ($pierProposal.Count -ne (24 + [int][bool]$ConvertSourceStand) -or $pierPlaced.Count -ne 1 -or
            [string]$pierPlaced[0].asset_hash -cne $pierPin -or
            [math]::Abs([double]$pierPlaced[0].transform.translation_m.x - 55.0) -gt 0.001 -or
            [math]::Abs([double]$pierPlaced[0].transform.translation_m.z + 64.0) -gt 0.001) {
            throw 'Source pier conversion did not preserve the pinned pose and package.'
        }
        $beforePierApply = Get-WorldState -Socket $socket
        [void](Invoke-UiClick -Socket $socket -Text 'Appliquer' -Selector '.authoring-apply')
        [void](Wait-WorldState -Socket $socket -Description 'source pier accepted' -TimeoutSeconds $InteractionTimeoutSeconds -Predicate {
            param($state)
            [int]$state.audit_counts.apply_zone -eq ([int]$beforePierApply.audit_counts.apply_zone + 1) -and
                [int]$state.audit_pending -eq 0 -and @($state.placement_options).Count -eq (24 + [int][bool]$ConvertSourceStand)
        })
        $initial = Get-WorldState -Socket $socket
    }
    if ($ConvertSourcePlaza) {
        [void](Wait-CdpReady -Socket $socket -Description 'plaza import action' -TimeoutSeconds 45 -Expression @'
(() => { const b=[...document.querySelectorAll('button')].find(x=>x.textContent?.trim()==='Rendre les cinq accessoires de la place Tidewater éditables'); return {ready:!!b&&!b.disabled,reason:b?'button disabled':'button absent'}; })()
'@)
        [void](Invoke-UiClick -Socket $socket -Text 'Rendre les cinq accessoires de la place Tidewater éditables')
        [void](Wait-NewPreview -Socket $socket -PreviousCount ([int]$initial.audit_counts.preview_zone) -TimeoutSeconds $InteractionTimeoutSeconds)
        $plazaEntry = Get-RequiredAuditedEntry -Snapshot (Get-WorldAuditSnapshot -Socket $socket) -RequestKind 'preview_zone'
        $plazaProposal = @($plazaEntry.wire.request.request.action.corrections.placements)
        $plazaPlaced = @($plazaProposal | Where-Object { $initial.placement_options.value -notcontains [string]$_.id })
        if ($plazaProposal.Count -ne (28 + [int][bool]$ConvertSourceStand + [int][bool]$ConvertSourcePier) -or
            $plazaPlaced.Count -ne 5 -or
            @($plazaPlaced | Where-Object { [string]$_.asset_hash -cne $plazaPin }).Count -ne 0) {
            throw 'Source plaza conversion did not preserve five distinct pinned placements.'
        }
        $expectedPlaza = @{
            'Banc Tidewater 1' = @(36.2, -108.2)
            'Banc Tidewater 2' = @(45.8, -108.6)
            'Lampadaire Tidewater' = @(44.3, -106.2)
            'Tonneau Tidewater 1' = @(37.4, -115.6)
            'Tonneau Tidewater 2' = @(38.1, -116.1)
        }
        if (@($plazaPlaced | Select-Object -ExpandProperty name -Unique).Count -ne 5) {
            throw 'Source plaza identities are not distinct.'
        }
        foreach ($entry in $plazaPlaced) {
            $position = $expectedPlaza[[string]$entry.name]
            if ($null -eq $position -or
                [math]::Abs([double]$entry.transform.translation_m.x - $position[0]) -gt 0.001 -or
                [math]::Abs([double]$entry.transform.translation_m.z - $position[1]) -gt 0.001) {
                throw 'Source plaza placement differs from its pinned position.'
            }
        }
        $beforePlazaApply = Get-WorldState -Socket $socket
        [void](Invoke-UiClick -Socket $socket -Text 'Appliquer' -Selector '.authoring-apply')
        [void](Wait-WorldState -Socket $socket -Description 'source plaza accepted' -TimeoutSeconds $InteractionTimeoutSeconds -Predicate {
            param($state)
            [int]$state.audit_counts.apply_zone -eq ([int]$beforePlazaApply.audit_counts.apply_zone + 1) -and
                [int]$state.audit_pending -eq 0 -and @($state.placement_options).Count -eq (28 + [int][bool]$ConvertSourceStand + [int][bool]$ConvertSourcePier)
        })
        $initial = Get-WorldState -Socket $socket
    }
    [void](Wait-CdpReady -Socket $socket -Description 'boardwalk import action' -TimeoutSeconds 30 -Expression @'
(() => { const b=[...document.querySelectorAll('button')].find(x=>x.textContent?.trim()==='Rendre les trois passerelles Tidewater éditables'); return {ready:!!b&&!b.disabled,reason:b?'button disabled':'button absent'}; })()
'@)
    [void](Invoke-UiClick -Socket $socket -Text 'Rendre les trois passerelles Tidewater éditables')
    [void](Wait-NewPreview -Socket $socket -PreviousCount ([int]$initial.audit_counts.preview_zone) -TimeoutSeconds $InteractionTimeoutSeconds)
    $importEntry = Get-RequiredAuditedEntry -Snapshot (Get-WorldAuditSnapshot -Socket $socket) -RequestKind 'preview_zone'
    $proposal = @($importEntry.wire.request.request.action.corrections.placements)
    $placed = @($proposal | Where-Object { $initial.placement_options.value -notcontains [string]$_.id } | Sort-Object name)
    if ($proposal.Count -ne $expectedPlacements -or $placed.Count -ne 3 -or @($placed | Where-Object { [string]$_.asset_hash -cne $boardwalkPin }).Count -ne 0) {
        throw 'Boardwalk Preview did not add the three pinned source placements.'
    }
    if ((Get-ArchiveSnapshot -LiteralPath $workspaceArchive).Sha256 -cne $workspaceBefore.Sha256) {
        throw 'Boardwalk Preview modified the saved workspace.'
    }
    $beforeApply = Get-WorldState -Socket $socket
    [void](Invoke-UiClick -Socket $socket -Text 'Appliquer' -Selector '.authoring-apply')
    [void](Wait-WorldState -Socket $socket -Description 'boardwalk import accepted' -TimeoutSeconds $InteractionTimeoutSeconds -Predicate {
        param($state)
        [int]$state.audit_counts.apply_zone -eq ([int]$beforeApply.audit_counts.apply_zone + 1) -and
            [int]$state.audit_pending -eq 0 -and @($state.placement_options).Count -eq $expectedPlacements
    })
    [void](Open-DetailsBySummary -Socket $socket -Summary 'Sélection et chargement')
    [void](Select-UiOptionByLabel -Socket $socket -Label 'Objet placé à inspecter' -Value ([string]$placed[0].id))
    [void](Invoke-UiClick -Socket $socket -Text 'Placer / manipuler')
    [void](Invoke-UiClick -Socket $socket -Text 'Réglages avancés →')
    [void](Open-DetailsBySummary -Socket $socket -Summary 'Position précise et duplication')
    $change = Invoke-CdpExpression -Socket $socket -Expression @'
(() => {
  const summary=[...document.querySelectorAll('summary')].find(x=>x.textContent?.trim()==='Position précise et duplication');
  const detail=summary?.closest('details');
  const label=[...(detail?.querySelectorAll('label')??[])].find(x=>x.textContent?.trim().startsWith('X (m)'));
  const input=label?.querySelector('input'); if(!input) return null;
  const before=Number(input.value); input.value=String(before+2);
  input.dispatchEvent(new Event('input',{bubbles:true}));
  return {before,after:before+2};
})()
'@
    if ($null -eq $change -or -not [double]::IsFinite([double]$change.before)) { throw 'Precise boardwalk X editor absent.' }
    $initialX = [double]$change.before
    $movedX = [double]$change.after
    $beforeMove = Get-WorldState -Socket $socket
    [void](Invoke-UiClick -Socket $socket -Text 'Prévisualiser le déplacement')
    [void](Wait-NewPreview -Socket $socket -PreviousCount ([int]$beforeMove.audit_counts.preview_zone) -TimeoutSeconds $InteractionTimeoutSeconds)
    $moveEntry = Get-RequiredAuditedEntry -Snapshot (Get-WorldAuditSnapshot -Socket $socket) -RequestKind 'preview_zone'
    $movedProposal = @($moveEntry.wire.request.request.action.corrections.placements)
    $moved = @($movedProposal | Where-Object { [string]$_.id -ceq [string]$placed[0].id })
    if ($moved.Count -ne 1 -or [math]::Abs([double]$moved[0].transform.translation_m.x - $movedX) -gt 0.001) {
        throw 'Boardwalk Preview did not carry the edited X coordinate.'
    }
    [void](Invoke-UiClick -Socket $socket -Text 'Appliquer' -Selector '.authoring-apply')
    [void](Wait-WorldState -Socket $socket -Description 'moved boardwalk accepted' -TimeoutSeconds $InteractionTimeoutSeconds -Predicate {
        param($state)
        [int]$state.audit_pending -eq 0 -and $state.diff_hidden -and @($state.placement_options).Count -eq $expectedPlacements
    })
    [void](Invoke-UiClick -Socket $socket -Text 'Cadrer cet objet dans la carte')
    Start-Sleep -Seconds 2
    $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output 'boardwalk-edited.png')))
    if ($ConvertSourcePier) {
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Objet placé à inspecter' -Value ([string]$pierPlaced[0].id))
        [void](Invoke-UiClick -Socket $socket -Text 'Placer / manipuler')
        [void](Open-DetailsBySummary -Socket $socket -Summary 'Sélection et chargement')
        [void](Invoke-UiClick -Socket $socket -Text 'Cadrer cet objet dans la carte')
        Start-Sleep -Seconds 12
        $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output 'pier-authored.png')))
    }
    if ($ConvertSourcePlaza) {
        $lamp = @($plazaPlaced | Where-Object { [string]$_.name -ceq 'Lampadaire Tidewater' })
        if ($lamp.Count -ne 1) { throw 'Pinned source plaza lantern placement absent.' }
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Objet placé à inspecter' -Value ([string]$lamp[0].id))
        [void](Invoke-UiClick -Socket $socket -Text 'Placer / manipuler')
        [void](Open-DetailsBySummary -Socket $socket -Summary 'Sélection et chargement')
        [void](Invoke-UiClick -Socket $socket -Text 'Cadrer cet objet dans la carte')
        Start-Sleep -Seconds 4
        $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output 'plaza-authored.png')))
    }
    $beforeSave = Get-WorldState -Socket $socket
    [void](Invoke-UiClick -Socket $socket -Text 'Sauvegarder')
    [void](Wait-WorldState -Socket $socket -Description 'boardwalk saved' -TimeoutSeconds $InteractionTimeoutSeconds -Predicate {
        param($state)
        [int]$state.audit_counts.save -eq ([int]$beforeSave.audit_counts.save + 1) -and
            [int]$state.audit_pending -eq 0 -and [string]$state.command_status -like '*Session synchronisée*'
    })
    $reference = Get-StandWorkspaceReference -Archive $workspaceArchive -ZoneId $zoneId
    $savedObjectPath = Join-Path $objectRoot "$($reference.archive_hash).json"
    $savedPlacements = @(Get-StandArchivePlacements -Archive $savedObjectPath)
    $savedMoved = @($savedPlacements | Where-Object { [string]$_.id -ceq [string]$placed[0].id })
    if ($savedPlacements.Count -ne $expectedPlacements -or $savedMoved.Count -ne 1 -or
        [math]::Abs([double]$savedMoved[0].transform.translation_m.x - $movedX) -gt 0.001) {
        throw 'Saved boardwalk position differs from accepted Preview.'
    }
    if ($ConvertSourcePier -and @($savedPlacements | Where-Object { [string]$_.id -ceq [string]$pierPlaced[0].id -and [string]$_.asset_hash -ceq $pierPin }).Count -ne 1) {
        throw 'Saved source pier placement differs from accepted Preview.'
    }
    if ($ConvertSourcePlaza -and @($savedPlacements | Where-Object { [string]$_.asset_hash -ceq $plazaPin }).Count -ne 5) {
        throw 'Saved source plaza placements differ from accepted Preview.'
    }
    [void](Invoke-UiClick -Socket $socket -Text 'Projet' -Selector '[data-authoring-tab-button]')
    [void](Open-DetailsBySummary -Socket $socket -Summary 'Créer ou modifier la zone')
    [void](Invoke-UiClick -Socket $socket -Text 'Fermer la zone')
    [void](Install-WorldAuditAfterOwnedReload -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds)
    [void](Wait-StandLibrary -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds)
    [void](Open-ManagedTidewaterZone -Socket $socket -ZoneId $zoneId -TimeoutSeconds $InteractionTimeoutSeconds)
    [void](Wait-WorldState -Socket $socket -Description 'edited boardwalk reopened' -TimeoutSeconds $InteractionTimeoutSeconds -Predicate {
        param($state)
        [int]$state.audit_pending -eq 0 -and @($state.placement_options).Count -eq $expectedPlacements
    })
    $reopened = Get-StandArchivePlacements -Archive $savedObjectPath
    $reopenedMoved = @($reopened | Where-Object { [string]$_.id -ceq [string]$placed[0].id })
    if ($reopenedMoved.Count -ne 1 -or [math]::Abs([double]$reopenedMoved[0].transform.translation_m.x - $movedX) -gt 0.001) {
        throw 'Reopening lost the boardwalk correction.'
    }
    if ($ConvertSourcePier) {
        $reopenedPier = @($reopened | Where-Object {
            [string]$_.id -ceq [string]$pierPlaced[0].id -and
            [string]$_.asset_hash -ceq $pierPin -and
            [math]::Abs([double]$_.transform.translation_m.x - 55.0) -le 0.001 -and
            [math]::Abs([double]$_.transform.translation_m.z + 64.0) -le 0.001
        })
        if ($reopenedPier.Count -ne 1) { throw 'Reopening lost the pinned source pier placement or pose.' }
    }
    if ($ConvertSourcePlaza -and @($reopened | Where-Object { [string]$_.asset_hash -ceq $plazaPin }).Count -ne 5) {
        throw 'Reopening lost the five source plaza placements.'
    }
    [void](Invoke-UiClick -Socket $socket -Text 'Tester & suivi' -Selector '[data-authoring-tab-button]')
    if ($TraverseSourcePier) {
        [void](Open-DetailsBySummary -Socket $socket -Summary 'Choisir le départ du playtest')
        $startSet = Invoke-CdpExpression -Socket $socket -Expression @'
(() => {
  const details=[...document.querySelectorAll('details')].find(d=>d.querySelector('summary')?.textContent?.trim()==='Choisir le départ du playtest');
  if (!details) return false;
  for (const [labelText,value] of [['Départ X (m)','55'],['Départ Z (m)','-66']]) {
    const label=[...details.querySelectorAll('label')].find(x=>x.textContent?.trim().startsWith(labelText));
    const input=label?.querySelector('input'); if (!input) return false;
    input.value=value; input.dispatchEvent(new Event('input',{bubbles:true}));
  }
  return true;
})()
'@
        if (-not $startSet) { throw 'Temporary pier traversal start controls absent.' }
        [void](Invoke-UiClick -Socket $socket -Text 'Jouer à cet endroit')
    } else {
        [void](Invoke-UiClick -Socket $socket -Text 'Jouer')
    }
    $view = Wait-CdpReady -Socket $socket -Description 'edited boardwalk playtest ready' -TimeoutSeconds $InteractionTimeoutSeconds -Expression @'
(() => { const text=document.querySelector('output#authoring-playtest-status')?.textContent??'';
  return {ready:text.startsWith('Simulation active') && text.includes('simulation prête true'),reason:text||'playtest status pending',playtest_status:text}; })()
'@
    $playtest = [pscustomobject]@{ playtest_status = [string]$view.playtest_status }
    $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output 'boardwalk-playtest.png')))
    $canvasPoint = Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const r=document.getElementById('forge-authoring-canvas')?.getBoundingClientRect();
  if (!r || r.width < 1 || r.height < 1) return null;
  return {x:r.left+r.width/2,y:r.top+r.height/2}; })()
'@
    if ($null -eq $canvasPoint) { throw 'Playtest canvas has no clickable bounds.' }
    Send-CdpMouseClick -Socket $socket -X ([double]$canvasPoint.x) -Y ([double]$canvasPoint.y)
    Start-Sleep -Milliseconds 300
    if (-not (Invoke-CdpExpression -Socket $socket -Expression "document.pointerLockElement?.id==='forge-authoring-canvas'")) {
        Send-CdpMouseClick -Socket $socket -X ([double]$canvasPoint.x) -Y ([double]$canvasPoint.y)
    }
    [void](Wait-CdpReady -Socket $socket -Description 'boardwalk playtest input capture' -TimeoutSeconds 15 -Expression @'
(() => { const locked=document.pointerLockElement?.id==='forge-authoring-canvas';
  return {ready:locked,reason:document.querySelector('output#authoring-playtest-status')?.textContent??'no status'}; })()
'@)
    if ($TraverseSourcePier) {
        if (-not $WalkWithoutJump) {
            Send-CdpKeyStroke -Socket $socket -Key ' ' -Code 'Space' -VirtualKeyCode 32
        }
        Send-CdpKeyHold -Socket $socket -Key 's' -Code 'KeyS' -VirtualKeyCode 83 -Milliseconds 5200
    } else {
        Send-CdpKeyHold -Socket $socket -Key 'w' -Code 'KeyW' -VirtualKeyCode 87 -Milliseconds 900
    }
    $movement = Wait-WorldState -Socket $socket -Description 'boardwalk resolved player movement' -TimeoutSeconds 15 -Predicate {
        param($state)
        [string]$state.playtest_status -match 'dernier mouvement observé tick' -and
            [string]$state.playtest_status -match 'entrée active true' -and
            [string]$state.playtest_status -notmatch 'résolu \(0\.0000, 0\.0000\)'
    }
    $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output 'boardwalk-movement.png')))
    if ($TraverseSourcePier) {
        $pierTraversal = [string]$movement.playtest_status
        $position = [regex]::Match($pierTraversal, 'position \(m\) (?<x>-?\d+\.\d+), (?<y>-?\d+\.\d+), (?<z>-?\d+\.\d+)')
        if (-not $position.Success -or $pierTraversal -notmatch 'au sol true' -or
            [double]::Parse($position.Groups['z'].Value, [Globalization.CultureInfo]::InvariantCulture) -le -55.0) {
            throw "Player did not cross the pier entrance on its walkable surface: $pierTraversal"
        }
        $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output 'pier-traversal.png')))
        if ($TraverseSourcePierHead) {
            Send-CdpKeyHold -Socket $socket -Key 's' -Code 'KeyS' -VirtualKeyCode 83 -Milliseconds 10000
            Send-CdpKeyHold -Socket $socket -Key 's' -Code 'KeyS' -VirtualKeyCode 83 -Milliseconds 6000
            $pierHeadTraversal = [string](Get-WorldState -Socket $socket).playtest_status
            $headPosition = [regex]::Match($pierHeadTraversal, 'position \(m\) (?<x>-?\d+\.\d+), (?<y>-?\d+\.\d+), (?<z>-?\d+\.\d+)')
            if (-not $headPosition.Success -or $pierHeadTraversal -notmatch 'au sol true' -or
                [double]::Parse($headPosition.Groups['z'].Value, [Globalization.CultureInfo]::InvariantCulture) -le 20.0) {
                throw "Player did not reach the seaward pier head on its walkable surface: $pierHeadTraversal"
            }
            $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output 'pier-head.png')))
        }
    }
    Send-CdpMouseEvent -Socket $socket -Type mouseMoved -X ([double]$canvasPoint.x + 1500.0) -Y ([double]$canvasPoint.y)
    Start-Sleep -Seconds 1
    $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output 'boardwalk-source-stand.png')))
} catch {
    $failure = $_.Exception.Message
    if ($null -ne $socket -and $socket.State -eq [Net.WebSockets.WebSocketState]::Open) {
        try { $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output 'boardwalk-failure.png'))) } catch { Write-Verbose $_.Exception.Message }
    }
} finally {
    if ($null -ne $socket) {
        if ($socket.State -eq [Net.WebSockets.WebSocketState]::Open) {
            try { [void](Restore-WorldAudit -Socket $socket) } catch { Write-Verbose $_.Exception.Message }
            try {
                $closing = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(2))
                try { Wait-NonGenericTask -Task ($socket.CloseAsync([Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'Boardwalk smoke complete', $closing.Token)) }
                finally { $closing.Dispose() }
            } catch { Write-Verbose $_.Exception.Message }
        }
        $socket.Dispose()
    }
    if ($null -ne $process -and -not $process.HasExited) {
        try { [void]$process.CloseMainWindow() } catch { Write-Verbose $_.Exception.Message }
        if (-not $process.WaitForExit(5000)) { Stop-Process -Id $process.Id -Force; [void]$process.WaitForExit(5000) }
    }
    if ($null -ne $reservation) { $reservation.Listener.Stop() }
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
}
$sourcePreserved = (Get-ArchiveSnapshot -LiteralPath $sourceArchive).Sha256 -ceq $sourceBefore.Sha256 -and
    (Get-ArchiveSnapshot -LiteralPath $originalObject).Sha256 -ceq $objectBefore.Sha256
$ledger = [ordered]@{
    schema='forge.tidewater.boardwalk-studio-smoke'; version=1;
    status=if($null -eq $failure -and $sourcePreserved -and $null -ne $playtest){'passed'}else{'failed'};
    failure=$failure; candidate=$candidate; candidate_sha256=(Get-FileSha256 -LiteralPath $candidate);
    boardwalk_package_sha256=$boardwalkPin; source_preserved=$sourcePreserved;
    initial_placements=23; saved_placements=$expectedPlacements; source_stand_converted=[bool]$ConvertSourceStand;
    source_pier_converted=[bool]$ConvertSourcePier; pier_package_sha256=if($ConvertSourcePier){$pierPin}else{$null};
    pier_placement=if($null -ne $pierPlaced -and @($pierPlaced).Count -gt 0){$pierPlaced[0]}else{$null};
    source_plaza_converted=[bool]$ConvertSourcePlaza; plaza_package_sha256=if($ConvertSourcePlaza){$plazaPin}else{$null};
    plaza_placements=$plazaPlaced;
    moved_placement=if($null -ne $placed -and @($placed).Count -gt 0){$placed[0]}else{$null};
    initial_x_m=$initialX; accepted_x_m=$movedX; saved_object=$savedObjectPath;
    playtest_status=if($null -ne $playtest){$playtest.playtest_status}else{$null};
    movement_status=if($null -ne $movement){$movement.playtest_status}else{$null};
    pier_traversal_status=$pierTraversal; pier_head_traversal_status=$pierHeadTraversal;
    walk_without_jump=[bool]$WalkWithoutJump;
    screenshots=@($screenshots)
}
$ledgerPath = Join-Path $output 'boardwalk-studio-smoke-ledger.json'
[IO.File]::WriteAllText($ledgerPath, ($ledger | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))
if ($ledger.status -cne 'passed') { throw "Boardwalk Studio smoke failed: $failure. Ledger: $ledgerPath" }
Write-Output "TIDEWATER_BOARDWALK_STUDIO_SMOKE_PASS ledger=$ledgerPath"
