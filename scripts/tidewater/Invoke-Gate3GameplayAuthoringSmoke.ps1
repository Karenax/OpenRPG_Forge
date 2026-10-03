#Requires -Version 7.2
# Local Gate 3 authoring smoke. This is an isolated UI proof, not a Gate verdict.
[CmdletBinding(DefaultParameterSetName = 'Run')]
param(
    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [Alias('CandidateExecutable')]
    [ValidateNotNullOrEmpty()]
    [string]$Candidate,

    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [Alias('FixtureSourceRoot')]
    [ValidateNotNullOrEmpty()]
    [string]$FreshIsolatedFixture,

    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string]$SourceOriginalHashPinned,

    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$EvidenceRoot,

    [Parameter(ParameterSetName = 'Run')]
    [guid]$ZoneUuid = 'e620dd19-ead9-5913-a6d6-0f93109148c0',

    [Parameter(ParameterSetName = 'Run')]
    [ValidateRange(30, 180)]
    [int]$InteractionTimeoutSeconds = 120,

    [Parameter(ParameterSetName = 'Run')]
    [Nullable[double]]$MeasuredFloorY,

    [Parameter(ParameterSetName = 'Run')]
    [ValidateRange(-2000, 2000)]
    [double]$RoomOriginX = 52,

    [Parameter(ParameterSetName = 'Run')]
    [Parameter(ParameterSetName = 'SelfTest')]
    [switch]$IncludeImpactApproach,

    [Parameter(ParameterSetName = 'SelfTest', Mandatory)]
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Keep the caller's parameters because the dot-sourced helper has parameters
# with some of the same names. It only defines CDP/fixture helpers in -SelfTest.
$requestedSelfTest = [bool]$SelfTest
$requestedCandidate = $Candidate
$requestedFixture = $FreshIsolatedFixture
$requestedSourcePin = $SourceOriginalHashPinned
$requestedEvidenceRoot = $EvidenceRoot
$requestedZoneUuid = $ZoneUuid
$requestedTimeout = $InteractionTimeoutSeconds
$requestedMeasuredFloorY = $MeasuredFloorY
$requestedRoomOriginX = $RoomOriginX
$requestedIncludeImpactApproach = [bool]$IncludeImpactApproach
. (Join-Path $PSScriptRoot '..\Invoke-Gate2WorldEditingProof.ps1') -SelfTest | Out-Null
# In the SelfTest parameter set the Run-only variables are unbound.  Assigning
# null back to a validated parameter after dot-sourcing the helper re-runs its
# validation and aborts before the local self-test.  The Run set is the only
# path that needs restoration because the helper owns a same-named timeout.
if (-not $requestedSelfTest) {
    $Candidate = $requestedCandidate
    $FreshIsolatedFixture = $requestedFixture
    $SourceOriginalHashPinned = $requestedSourcePin
    $EvidenceRoot = $requestedEvidenceRoot
    $ZoneUuid = $requestedZoneUuid
    $InteractionTimeoutSeconds = $requestedTimeout
    $MeasuredFloorY = $requestedMeasuredFloorY
    $RoomOriginX = $requestedRoomOriginX
    $IncludeImpactApproach = $requestedIncludeImpactApproach
}

$zoneId = $ZoneUuid.ToString('D').ToLowerInvariant()
$editorSummary = 'Personnages, objets et objectifs'
$impactApproachName = 'Bloc de contrepoids'
$impactApproachCenterY = 58.817839403
$impactTerrainBlobName = 'be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3.bin'
$impactTerrainBlobSha256 = 'be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3'
$script:fireControlsMode = $null
$script:fireControlsObservation = $null
$sourceCommitPin = '1438b1abfcaee3267092b75573014f4d9b4a983c'
$expectedTerrainPin = '5e5e88f902df94fec5e40e3ce0f0e279245d5dca1aebfe1be224bae7bb4ee258'
$expectedEnvironmentPins = @(
    '6d278d406a34eccf06fae4cd6d4a68647d5988f620761524ed03c41a97e795d1',
    'dddf01a25b409c01992bf372117646583b1f72f1f173046151ca0b33753bd097'
)
$expectedPlantsPin = '1fec8a063214ad05ceac19f277dbd17e131e04d2714378bae9239efd12224f83'

function Open-Gate3TidewaterZone {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$ZoneId,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )

    [void](Invoke-UiClick -Socket $Socket -Text 'Projet' -Selector '[data-authoring-tab-button]')
    [void](Open-DetailsBySummary -Socket $Socket -Summary 'Projets et modules')
    [void](Invoke-UiClickInDetailsBySummary -Socket $Socket -Summary 'Projets et modules' -Text 'Ouvrir le workspace')
    $zoneLiteral = ConvertTo-CompactJson -Value $ZoneId
    [void](Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'Gate 3 managed Tidewater zone button' -Expression @"
(() => {
  const zone=$zoneLiteral;
  const button=[...document.querySelectorAll('button[title]')].find(node=>node.title===zone);
  const status=[...document.querySelectorAll('details.scene-navigation p[role=status]')].map(node=>node.textContent??'').join(' | ');
  return {ready:!!button&&!button.disabled,reason:button?'managed zone button disabled':status||'managed zone button pending'};
})()
"@)

    $buttonState = Invoke-CdpExpression -Socket $Socket -Expression @"
(async () => {
  const zone=$zoneLiteral, norm=v=>(v??'').replace(/\s+/g,' ').trim();
  const buttons=[...document.querySelectorAll('button[title]')].filter(node=>node.title===zone);
  if(buttons.length!==1) return {ready:false,terminal:true,reason:'expected one managed zone button, got '+buttons.length};
  const node=buttons[0]; node.scrollIntoView({block:'center',inline:'center'});
  await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));
  const rect=node.getBoundingClientRect(), style=getComputedStyle(node), hit=document.elementFromPoint(rect.left+rect.width/2,rect.top+rect.height/2);
  const visible=rect.width>0&&rect.height>0&&style.visibility!=='hidden'&&style.display!=='none';
  const unobscured=hit===node||!!hit&&node.contains(hit);
  return {ready:visible&&!node.disabled&&unobscured,terminal:!visible,reason:!visible?'zone button hidden':unobscured?'zone button ready':'zone button obscured',x:rect.left+rect.width/2,y:rect.top+rect.height/2};
})()
"@
    if ($null -eq $buttonState -or $buttonState.ready -ne $true) {
        $buttonReason = if ($null -eq $buttonState) { 'state absent' } else { [string]$buttonState.reason }
        throw "Managed Tidewater zone button was not physically clickable: $buttonReason"
    }
    Send-CdpMouseClick -Socket $Socket -X $buttonState.x -Y $buttonState.y

    [void](Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'Gate 3 managed Tidewater edit viewport' -Expression @"
(() => {
  const norm=v=>(v??'').replace(/\s+/g,' ').trim(), zone=$zoneLiteral;
  const canvas=document.getElementById('forge-authoring-canvas');
  const term=[...document.querySelectorAll('.authoring-zone-information dt')].find(node=>norm(node.textContent)==='Zone');
  const shown=norm(term?.nextElementSibling?.textContent).toLowerCase();
  const mode=[...document.querySelectorAll('output')].map(node=>norm(node.textContent)).find(text=>text.startsWith('Mode '))??'';
  return {ready:shown===zone&&mode.startsWith('Mode édition')&&canvas?.clientWidth>0&&canvas?.clientHeight>0,reason:'zone='+shown+'; mode='+mode};
})()
"@)
}

function Wait-GameplayEditor {
    param([Parameter(Mandatory)]$Socket, [Parameter(Mandatory)][int]$TimeoutSeconds)
    return Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'Gameplay editor controls' -Expression @"
(() => {
  const summary=[...document.querySelectorAll('summary')].find(node=>(node.textContent??'').replace(/\s+/g,' ').trim()==='$editorSummary');
  const editor=document.querySelector('details.authoring-gameplay-editor');
  return {ready:!!summary&&!!editor,reason:editor?'gameplay editor ready':'gameplay editor pending'};
})()
"@
}

function Save-GameplayUiDiagnostic {
    param([Parameter(Mandatory)]$Socket, [Parameter(Mandatory)][string]$OutputRoot, [Parameter(Mandatory)][string]$Name)
    $observed = Invoke-CdpExpression -Socket $Socket -Expression @'
(() => {
  const norm=v=>(v??'').replace(/\s+/g,' ').trim();
  const editor=document.querySelector('details.authoring-gameplay-editor');
  return {
    captured_at:new Date().toISOString(),
    context:norm(document.querySelector('.authoring-context')?.textContent),
    status:norm(document.querySelector('.authoring-command-status')?.textContent),
    toolbar_busy:document.querySelector('.authoring-toolbar')?.getAttribute('aria-busy'),
    gameplay_fieldset_disabled:editor?.querySelector('fieldset')?.disabled??null,
    summaries:[...document.querySelectorAll('summary')].map(n=>({text:norm(n.textContent),parent:n.closest('details')?.className,open:n.closest('details')?.open})),
    editor_html:editor?.outerHTML??null,
    labels:[...document.querySelectorAll('label')].map(n=>({text:norm(n.textContent),details:n.closest('details')?.className,summary:norm(n.closest('details')?.querySelector(':scope > summary')?.textContent)}))
  };
})()
'@
    [IO.File]::WriteAllText((Join-Path $OutputRoot "$Name-ui.json"), ($observed | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
    $capture = Invoke-CdpCommand -Socket $Socket -Method 'Page.captureScreenshot' -Parameters @{format='png';captureBeyondViewport=$false}
    [IO.File]::WriteAllBytes((Join-Path $OutputRoot "$Name.png"), [Convert]::FromBase64String([string]$capture.data))
}

function Wait-GameplayForm {
    param([Parameter(Mandatory)]$Socket, [Parameter(Mandatory)][int]$TimeoutSeconds)
    return Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'Activated gameplay form' -Expression @'
(() => {
  const norm=v=>(v??'').replace(/\s+/g,' ').trim();
  const editor=document.querySelector('details.authoring-gameplay-editor');
  const labels=editor?[...editor.querySelectorAll('label')].filter(n=>norm(n.textContent).startsWith('Type')):[];
  const control=labels.length===1?labels[0].querySelector('select'):null;
  return {ready:!!control&&!control.disabled&&!control.closest('fieldset:disabled'),terminal:labels.length>1,reason:'gameplay Type labels='+labels.length+'; status='+norm(document.querySelector('.authoring-command-status')?.textContent)};
})()
'@
}

function Wait-GameplayPreview {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [Parameter(Mandatory)][string]$Description
    )
    return Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description $Description -Expression @'
(() => {
  const norm=v=>(v??'').replace(/\s+/g,' ').trim();
  const apply=document.querySelector('button.authoring-apply');
  const diff=document.querySelector('.authoring-command-diff');
  const status=norm(document.querySelector('.authoring-command-status')?.textContent);
  const terminal=/refusée|refusé|erreur|invalide|impossible|absent/i.test(status);
  return {ready:!!apply&&!apply.disabled&&!!diff&&!diff.hidden&&!terminal,terminal,reason:status||'preview pending',status};
})()
'@
}

function Wait-GameplayApply {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [Parameter(Mandatory)][string]$Description
    )
    return Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description $Description -Expression @'
(() => {
  const norm=v=>(v??'').replace(/\s+/g,' ').trim();
  const apply=document.querySelector('button.authoring-apply');
  const diff=document.querySelector('.authoring-command-diff');
  const status=norm(document.querySelector('.authoring-command-status')?.textContent);
  const saveState=norm(document.querySelector('.authoring-command-bar .gold-save-state')?.textContent);
  const busy=document.querySelector('.authoring-toolbar')?.getAttribute('aria-busy');
  const terminal=/refusée|refusé|erreur|invalide|impossible|absent/i.test(status);
  return {ready:!!apply&&apply.disabled&&(!diff||diff.hidden)&&status==='Retouches non sauvegardées'&&saveState==='Non enregistré'&&busy==='false'&&!terminal,
    terminal,reason:'status='+status+'; save='+saveState+'; busy='+busy,status};
})()
'@
}

function Invoke-GameplayPreviewApply {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$PreviewButton,
        [Parameter(Mandatory)][string]$Description,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    [void](Invoke-UiClickInDetailsBySummary -Socket $Socket -Summary $editorSummary -Text $PreviewButton)
    # Terrain transport is asynchronous. A click made while it is loading is
    # refused without creating a preview; retry that public click only for
    # this exact transient status, and retain the overall interaction bound.
    $loadingDeadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $nextLoadingRetry = [DateTimeOffset]::UtcNow.AddMilliseconds(750)
    while ([DateTimeOffset]::UtcNow -lt $loadingDeadline) {
        $transient = Invoke-CdpExpression -Socket $Socket -Expression @'
(() => {
  const status=(document.querySelector('.authoring-command-status')?.textContent??'').replace(/\s+/g,' ').trim();
  const apply=document.querySelector('button.authoring-apply');
  const diff=document.querySelector('.authoring-command-diff');
  const loading=status==='Chargement du relief importé en cours.';
  return {loading,preview_ready:!!apply&&!apply.disabled&&!!diff&&!diff.hidden,terminal:!loading&&/refusée|refusé|erreur|invalide|impossible|absent/i.test(status),status};
})()
'@
        if ($null -ne $transient -and $transient.preview_ready) { break }
        if ($null -ne $transient -and $transient.terminal) { throw "$Description preview refused: $($transient.status)" }
        if ($null -ne $transient -and $transient.loading -and [DateTimeOffset]::UtcNow -ge $nextLoadingRetry) {
            [void](Invoke-UiClickInDetailsBySummary -Socket $Socket -Summary $editorSummary -Text $PreviewButton)
            $nextLoadingRetry = [DateTimeOffset]::UtcNow.AddMilliseconds(750)
        }
        Start-Sleep -Milliseconds 150
    }
    $remaining = [Math]::Max(1, [Math]::Ceiling(($loadingDeadline - [DateTimeOffset]::UtcNow).TotalSeconds))
    $preview = Wait-GameplayPreview -Socket $Socket -TimeoutSeconds $remaining -Description "$Description preview"
    [void](Invoke-UiClick -Socket $Socket -Text 'Appliquer' -Selector '.authoring-apply')
    $applied = Wait-GameplayApply -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description "$Description apply"
    return [ordered]@{ description=$Description; preview_status=$preview.status; apply_status=$applied.status }
}

function Set-Gate3UiTextByLabel {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$Label,
        [AllowEmptyString()]
        [Parameter(Mandatory)][string]$Text,
        [string]$DetailsSummary = ''
    )
    $labelLiteral = ConvertTo-CompactJson -Value $Label
    $summaryLiteral = ConvertTo-CompactJson -Value $DetailsSummary
    $textLiteral = ConvertTo-CompactJson -Value $Text
    $focus = @"
(() => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim(), expected=$labelLiteral, summaryText=$summaryLiteral;
  let scope=document;
  if (summaryText) {
    const summaries=[...document.querySelectorAll('summary')].filter(node=>norm(node.textContent)===summaryText);
    if (summaries.length!==1) throw new Error('Expected one details summary ' + summaryText + ', got ' + summaries.length);
    scope=summaries[0].closest('details'); scope.open=true;
  }
  const labels=[...scope.querySelectorAll('label')].filter(node=>norm(node.textContent).startsWith(expected));
  if (labels.length!==1) throw new Error('Expected one label ' + expected + ', got ' + labels.length);
  const control=labels[0].querySelector('input,textarea');
  if (!control || control.disabled || control.closest('fieldset:disabled')) throw new Error('Writable text control absent for ' + expected);
  control.scrollIntoView({block:'center',inline:'center'}); control.focus();
  return { focused:document.activeElement===control, before:String(control.value) };
})()
"@
    $prepared = Invoke-CdpExpression -Socket $Socket -Expression $focus
    if (-not $prepared.focused) { throw "Keyboard input '$Label' did not obtain exact focus." }
    # Use browser keyboard events for clearing so an empty value follows the
    # same public input/change path as a human edit; no application state is injected.
    Send-CdpKeyStroke -Socket $Socket -Key 'a' -Code 'KeyA' -VirtualKeyCode 65 -Modifiers 2
    if ([string]::IsNullOrEmpty($Text)) {
        Send-CdpKeyStroke -Socket $Socket -Key 'Backspace' -Code 'Backspace' -VirtualKeyCode 8
    } else {
        [void](Invoke-CdpCommand -Socket $Socket -Method 'Input.insertText' -Parameters @{ text = $Text })
    }
    Send-CdpKeyStroke -Socket $Socket -Key 'Tab' -Code 'Tab' -VirtualKeyCode 9
    $verify = @"
(() => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim(), expected=$labelLiteral, summaryText=$summaryLiteral, wanted=$textLiteral;
  let scope=document;
  if(summaryText){const summaries=[...document.querySelectorAll('summary')].filter(node=>norm(node.textContent)===summaryText); if(summaries.length!==1) return {ready:false,terminal:true,reason:'summary cardinality changed'}; scope=summaries[0].closest('details');}
  const labels=[...scope.querySelectorAll('label')].filter(node=>norm(node.textContent).startsWith(expected));
  if(labels.length!==1) return {ready:false,terminal:true,reason:'label cardinality changed'};
  const control=labels[0].querySelector('input,textarea');
  const value=String(control?.value ?? '');
  return {ready:!!control && value===wanted && document.activeElement!==control,terminal:false,reason:'value=' + value,value};
})()
"@
    return Wait-CdpReady -Socket $Socket -Expression $verify -Description "Keyboard-entered UI control '$Label'" -TimeoutSeconds 10
}

function Set-GameplayForm {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$Kind,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$X,
        [Parameter(Mandatory)][string]$Z,
        [AllowEmptyString()]
        [Parameter(Mandatory)][string]$Reply,
        [AllowEmptyString()]
        [string]$CenterY = '',
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    [void](Set-UiControlByLabel -Socket $Socket -Label 'Type' -Value $Kind -DetailsSummary $editorSummary)
    [void](Set-UiControlByLabelWithKeyboard -Socket $Socket -Label 'Nom' -Text $Name -DetailsSummary $editorSummary)
    [void](Set-UiControlByLabelWithKeyboard -Socket $Socket -Label 'X (m)' -Text $X -DetailsSummary $editorSummary)
    [void](Set-UiControlByLabelWithKeyboard -Socket $Socket -Label 'Z (m)' -Text $Z -DetailsSummary $editorSummary)
    # An empty center Y deliberately asks the editor to sample canonical terrain.
    [void](Set-Gate3UiTextByLabel -Socket $Socket -Label 'Hauteur du centre (m, vide = terrain)' -Text $CenterY -DetailsSummary $editorSummary)
    [void](Set-Gate3UiTextByLabel -Socket $Socket -Label 'Réplique' -Text $Reply -DetailsSummary $editorSummary)
}

function Set-GameplayPhysicsForm {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$Mass,
        [Parameter(Mandatory)][string]$Width,
        [Parameter(Mandatory)][string]$Height,
        [Parameter(Mandatory)][string]$Depth,
        [Parameter(Mandatory)][bool]$Pushable,
        [Parameter(Mandatory)][bool]$Throwable,
        [AllowEmptyString()]
        [Parameter(Mandatory)][string]$BreakEnergy
    )
    Open-DetailsBySummary -Socket $Socket -Summary 'Propriétés physiques' | Out-Null
    [void](Set-UiControlByLabel -Socket $Socket -Label 'Masse (kg)' -Value $Mass -DetailsSummary 'Propriétés physiques')
    [void](Set-UiControlByLabel -Socket $Socket -Label 'Largeur (m)' -Value $Width -DetailsSummary 'Propriétés physiques')
    [void](Set-UiControlByLabel -Socket $Socket -Label 'Hauteur (m)' -Value $Height -DetailsSummary 'Propriétés physiques')
    [void](Set-UiControlByLabel -Socket $Socket -Label 'Profondeur (m)' -Value $Depth -DetailsSummary 'Propriétés physiques')
    [void](Set-UiControlByLabel -Socket $Socket -Label 'Poussable' -Value $Pushable -Kind checked -DetailsSummary 'Propriétés physiques')
    [void](Set-UiControlByLabel -Socket $Socket -Label 'Lançable' -Value $Throwable -Kind checked -DetailsSummary 'Propriétés physiques')
    $summaryLiteral = ConvertTo-CompactJson -Value 'Propriétés physiques'
    $flammableLiteral = ConvertTo-CompactJson -Value 'Inflammable'
    $heatDamageLiteral = ConvertTo-CompactJson -Value 'Dégâts de chaleur par tick (0 = aucun dégât)'
    $fireControls = Invoke-CdpExpression -Socket $Socket -Expression @"
(() => {
  const norm = value => (value ?? '').replace(/\s+/g, ' ').trim();
  const summaryText = $summaryLiteral;
  const flammablePrefix = $flammableLiteral;
  const heatDamagePrefix = $heatDamageLiteral;
  const summaries = [...document.querySelectorAll('summary')].filter(node => norm(node.textContent) === summaryText);
  if (summaries.length !== 1) throw new Error('Expected one details summary ' + summaryText + ', got ' + summaries.length);
  const details = summaries[0].closest('details');
  if (!details) throw new Error('Details parent absent for ' + summaryText);
  const matches = prefix => [...details.querySelectorAll('label')].filter(node => norm(node.textContent).startsWith(prefix));
  const controls = labels => labels.filter(node => !!node.querySelector('input,select,textarea'));
  const flammableLabels = matches(flammablePrefix);
  const heatDamageLabels = matches(heatDamagePrefix);
  return {
    flammable_label_count: flammableLabels.length,
    flammable_control_count: controls(flammableLabels).length,
    heat_damage_label_count: heatDamageLabels.length,
    heat_damage_control_count: controls(heatDamageLabels).length
  };
})()
"@
    $flammableLabelCount = [int]$fireControls.flammable_label_count
    $flammableControlCount = [int]$fireControls.flammable_control_count
    $heatDamageLabelCount = [int]$fireControls.heat_damage_label_count
    $heatDamageControlCount = [int]$fireControls.heat_damage_control_count
    if ($flammableLabelCount -ne $flammableControlCount -or $heatDamageLabelCount -ne $heatDamageControlCount) {
        throw "Fire control labels must each expose one public control; found flammable labels=$flammableLabelCount controls=$flammableControlCount, heat labels=$heatDamageLabelCount controls=$heatDamageControlCount."
    }
    $fireMode = if ($flammableLabelCount -eq 0 -and $heatDamageLabelCount -eq 0) {
        'absent'
    } elseif ($flammableLabelCount -eq 1 -and $heatDamageLabelCount -eq 1) {
        'present'
    } elseif ($flammableLabelCount -eq 1 -and $heatDamageLabelCount -eq 0) {
        # The pinned V1 candidate exposes flammability but predates the heat
        # rate control. Set the available public control and verify the zero
        # heat default in the saved archive; this does not author a fire.
        'legacy_flammable_only'
    } else {
        throw "Unknown fire control availability; found flammable=$flammableLabelCount, heat=$heatDamageLabelCount."
    }
    if ($null -ne $script:fireControlsMode -and [string]$script:fireControlsMode -cne $fireMode) {
        throw "Fire control availability changed during one authoring session: '$script:fireControlsMode' then '$fireMode'."
    }
    $script:fireControlsMode = $fireMode
    $script:fireControlsObservation = [ordered]@{
        mode=$fireMode
        flammable_label_count=$flammableLabelCount
        flammable_control_count=$flammableControlCount
        heat_damage_label_count=$heatDamageLabelCount
        heat_damage_control_count=$heatDamageControlCount
        defaults_flammable=$false
        defaults_heat_damage_per_tick=0
        authoring_claim='no fire authoring claimed'
    }
    if ($fireMode -in @('present', 'legacy_flammable_only')) {
        [void](Set-UiControlByLabel -Socket $Socket -Label 'Inflammable' -Value $false -Kind checked -DetailsSummary 'Propriétés physiques')
    }
    if ($fireMode -eq 'present') {
        [void](Set-UiControlByLabel -Socket $Socket -Label 'Dégâts de chaleur par tick (0 = aucun dégât)' -Value '0' -DetailsSummary 'Propriétés physiques')
    }
    [void](Set-UiControlByLabel -Socket $Socket -Label 'Énergie de rupture (J, vide = solide)' -Value $BreakEnergy -DetailsSummary 'Propriétés physiques')
}

function Invoke-Gate3BuildingRoomPreview {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][double]$MeasuredFloorY,
        [Parameter(Mandatory)][double]$RoomOriginX,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    # The measured terrain Y is deliberately an input. This helper never
    # invents an altitude; its room X is an explicit reviewed scenario input.
    [void](Invoke-UiClick -Socket $Socket -Text 'Construire' -Selector '[data-authoring-tab-button]')
    $summary = 'Nouvelle pièce · 8 × 6 m · hauteur 3 m'
    [void](Open-DetailsBySummary -Socket $Socket -Summary $summary)
    [void](Set-UiControlByLabelWithKeyboard -Socket $Socket -Label 'X (m)' -Text $RoomOriginX.ToString([Globalization.CultureInfo]::InvariantCulture) -DetailsSummary $summary)
    [void](Set-UiControlByLabelWithKeyboard -Socket $Socket -Label 'Y · sol (m)' -Text $MeasuredFloorY.ToString([Globalization.CultureInfo]::InvariantCulture) -DetailsSummary $summary)
    [void](Set-UiControlByLabelWithKeyboard -Socket $Socket -Label 'Z (m)' -Text '-145' -DetailsSummary $summary)
    [void](Invoke-UiClickInDetailsBySummary -Socket $Socket -Summary $summary -Text 'Prévisualiser une nouvelle pièce')
    $preview = Wait-GameplayPreview -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'building room preview'
    [void](Invoke-UiClick -Socket $Socket -Text 'Appliquer' -Selector '.authoring-apply')
    $applied = Wait-GameplayApply -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'building room apply'
    return [ordered]@{ preview_status=$preview.status; apply_status=$applied.status; x=$RoomOriginX; y=$MeasuredFloorY; z=-145 }
}

function Get-GameplayOptionValue {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$Label,
        [Parameter(Mandatory)][string]$Text,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    $labelLiteral = ConvertTo-CompactJson -Value $Label
    $textLiteral = ConvertTo-CompactJson -Value $Text
    $state = Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description "Gameplay option '$Text' under '$Label'" -Expression @"
(() => {
  const norm=v=>(v??'').replace(/\s+/g,' ').trim(), labelText=$labelLiteral, expected=$textLiteral;
  const editors=[...document.querySelectorAll('details.authoring-gameplay-editor')];
  if(editors.length!==1) return {ready:false,terminal:editors.length>1,reason:'gameplay editor cardinality '+editors.length};
  const labels=[...editors[0].querySelectorAll('label')].filter(node=>norm(node.textContent).startsWith(labelText));
  if(labels.length!==1) return {ready:false,terminal:labels.length>1,reason:'label cardinality '+labels.length};
  const select=labels[0].querySelector('select');
  if(!select) return {ready:false,terminal:true,reason:'select absent'};
  const option=[...select.options].find(item=>norm(item.textContent)===expected);
  if(!option) return {ready:false,terminal:false,reason:'option pending: '+expected};
  return {ready:true,terminal:false,reason:'option ready',value:option.value,text:norm(option.textContent)};
})()
"@
    return [string]$state.value
}

function Select-GameplayOptionByText {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$Label,
        [Parameter(Mandatory)][string]$Text,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    $value = Get-GameplayOptionValue -Socket $Socket -Label $Label -Text $Text -TimeoutSeconds $TimeoutSeconds
    [void](Set-UiControlByLabel -Socket $Socket -Label $Label -Value $value -DetailsSummary $editorSummary)
}

function Get-Gate3WorkspaceReference {
    param([Parameter(Mandatory)][string]$Archive, [Parameter(Mandatory)][string]$ZoneId)
    $document = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($Archive))
    $matches = @(
        foreach ($project in $document.head.projects) {
            foreach ($module in $project.modules) {
                foreach ($zone in $module.zones) {
                    if ([string]$zone.area_id -ceq $ZoneId) { $zone }
                }
            }
        }
    )
    if ($matches.Count -ne 1) { throw 'Exactly one managed Tidewater zone is required in the workspace.' }
    return $matches[0]
}

function Get-Gate3ArchivePlacements {
    param([Parameter(Mandatory)][string]$Archive)
    $document = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($Archive))
    return @($document.head.corrections.placements)
}

function Test-Gate3PositionXZ {
    param(
        [AllowNull()][object]$Position,
        [Parameter(Mandatory)][double]$X,
        [Parameter(Mandatory)][double]$Z
    )
    if ($null -eq $Position) { return $false }
    try {
        return [Math]::Abs([double]$Position.x - $X) -lt 0.0001 -and
            [Math]::Abs([double]$Position.z - $Z) -lt 0.0001
    }
    catch {
        return $false
    }
}

function Get-Gate3GameplayReport {
    param([Parameter(Mandatory)][string]$Archive)
    $document = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($Archive))
    $corrections = $document.head.corrections
    $gameplay = if ($null -ne $corrections -and $corrections.PSObject.Properties.Name -contains 'gameplay') {
        $corrections.gameplay
    } else {
        $null
    }
    if ($null -eq $gameplay) {
        return [ordered]@{ present=$false; archive_sha256=(Get-FileSha256 -LiteralPath $Archive); receipts=@($document.receipts).Count }
    }
    $actors = @($gameplay.actors)
    $items = @($gameplay.items)
    $objects = @($gameplay.objects)
    $places = @($gameplay.places)
    $dialogues = @($gameplay.dialogues)
    $quests = @($gameplay.quests)
    $byName = @{}
    foreach ($entry in @($actors + $items + $objects + $places)) { $byName[[string]$entry.name] = $entry }
    $ledger = @(
        foreach ($receipt in @($document.receipts)) {
            $gameplayRequest = $false
            if ($receipt.PSObject.Properties.Name -contains 'request' -and $null -ne $receipt.request -and
                $receipt.request.PSObject.Properties.Name -contains 'action' -and $null -ne $receipt.request.action -and
                $receipt.request.action.PSObject.Properties.Name -contains 'corrections' -and $null -ne $receipt.request.action.corrections) {
                $gameplayRequest = $receipt.request.action.corrections.PSObject.Properties.Name -contains 'gameplay' -and
                    $null -ne $receipt.request.action.corrections.gameplay
            }
            [ordered]@{
                command_id = [string]$receipt.request.command_id
                expected_hash = [string]$receipt.request.expected_hash
                resulting_hash = [string]$receipt.resulting_hash
                resulting_revision = $receipt.resulting_revision
                gameplay_request = $gameplayRequest
            }
        }
    )
    $linkNames = @{}
    foreach ($place in $places) {
        $linkNames[[string]$place.name] = @(
            foreach ($target in @($place.links)) {
                $resolved = @($places | Where-Object { [string]$_.id -ceq [string]$target })
                if ($resolved.Count -eq 1) { [string]$resolved[0].name } else { [string]$target }
            }
        )
    }
    $positionedEntries = @($actors + $objects + $places)
    $finiteY = @($positionedEntries | Where-Object { $_.position -and [double]::IsFinite([double]$_.position.y) }).Count
    return [ordered]@{
        present=$true
        archive_sha256=(Get-FileSha256 -LiteralPath $Archive)
        schema=[string]$gameplay.schema
        version=$gameplay.version
        player=[string]$gameplay.player
        actors=@($actors | ForEach-Object { [ordered]@{ id=[string]$_.id; name=[string]$_.name; role=[string]$_.role; position=$_.position } })
        items=@($items | ForEach-Object { [ordered]@{ id=[string]$_.id; name=[string]$_.name } })
        objects=@($objects | ForEach-Object { [ordered]@{
                    id=[string]$_.id
                    name=[string]$_.name
                    kind=$_.kind
                    position=$_.position
                    physics=[ordered]@{
                        mass_kg=$_.physics.mass_kg
                        pushable=[bool]$_.physics.pushable
                         throwable=[bool]$_.physics.throwable
                         flammable=[bool]$_.physics.flammable
                         heat_damage_per_tick=$_.physics.heat_damage_per_tick
                         break_energy_j=$_.physics.break_energy_j
                        body_half_extents_m=$_.physics.body_half_extents_m
                        supports=@($_.physics.supports | ForEach-Object { [string]$_ })
                    }
                } })
        places=@($places | ForEach-Object { [ordered]@{ id=[string]$_.id; name=[string]$_.name; position=$_.position; links=@($_.links | ForEach-Object { [string]$_ }) } })
        dialogues=@($dialogues | ForEach-Object { [ordered]@{ id=[string]$_.id; nodes=@($_.nodes) } })
        quests=@($quests | ForEach-Object { [ordered]@{ id=[string]$_.id; name=[string]$_.name; giver=[string]$_.giver; required_item=[string]$_.required_item; reward_item=$_.reward_item; reward_quantity=$_.reward_quantity } })
        names=$byName
        links_by_name=$linkNames
        finite_y_count=$finiteY
        receipt_ledger=$ledger
    }
}

function Assert-Gate3CandidateInputs {
    param([Parameter(Mandatory)][string]$CandidateExecutable)
    $candidateRoot = Split-Path -Parent $CandidateExecutable
    $web = Join-Path $candidateRoot 'web'
    Assert-TreeHasNoReparsePoints -LiteralPath $web -Description 'Gate 3 candidate frontend'
    $kitBuildPath = Join-Path $web 'forge-kit\build.json'
    $importsManifestPath = Join-Path $web 'forge-imports\buildings.json'
    if (-not (Test-Path -LiteralPath $kitBuildPath -PathType Leaf)) { throw 'Candidate governed kit build.json is missing.' }
    if (-not (Test-Path -LiteralPath $importsManifestPath -PathType Leaf)) { throw 'Candidate Tidewater imports manifest is missing.' }
    $kit = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($kitBuildPath))
    if ([string]$kit.schema -cne 'forge.blender-asset-kit-build' -or
        $kit.PSObject.Properties.Name -notcontains 'qualification' -or
        $null -eq $kit.qualification -or
        $kit.qualification.PSObject.Properties.Name -notcontains 'technical_export_validated' -or
        -not [bool]$kit.qualification.technical_export_validated) {
        throw 'Candidate kit is not technically validated.'
    }
    $humanArtReview = if ($kit.qualification.PSObject.Properties.Name -contains 'human_art_review') {
        [string]$kit.qualification.human_art_review
    } else {
        $null
    }
    $publicationApproved = if ($kit.qualification.PSObject.Properties.Name -contains 'publication_approved') {
        [bool]$kit.qualification.publication_approved
    } else {
        $false
    }
    $library = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($importsManifestPath))
    if ([string]$library.schema -cne 'forge.imported-building-library' -or [int]$library.version -ne 1) {
        throw 'Candidate imports manifest schema/version is not supported.'
    }
    if ([string]$library.terrain_sha256 -cne $expectedTerrainPin -or [string]$library.plants_sha256 -cne $expectedPlantsPin) {
        throw 'Candidate Tidewater terrain/plants pins differ from the reviewed source.'
    }
    if ($expectedEnvironmentPins -notcontains [string]$library.environment_sha256) {
        throw 'Candidate Tidewater environment pin is not one of the reviewed source packages.'
    }
    $packagePins = @(
        [pscustomobject]@{ Name='terrain'; Hash=[string]$library.terrain_sha256 },
        [pscustomobject]@{ Name='plants'; Hash=[string]$library.plants_sha256 },
        [pscustomobject]@{ Name='environment'; Hash=[string]$library.environment_sha256 },
        [pscustomobject]@{ Name='buildings'; Hash=[string]$library.package_sha256 }
    )
    if ($library.PSObject.Properties.Name -contains 'ocean_sha256' -and -not [string]::IsNullOrWhiteSpace([string]$library.ocean_sha256)) {
        $packagePins += [pscustomobject]@{ Name='ocean'; Hash=[string]$library.ocean_sha256 }
    }
    foreach ($package in $packagePins) {
        if ([string]$package.Hash -cnotmatch '^[0-9a-f]{64}$') { throw "Candidate $($package.Name) package hash is malformed." }
        $manifest = Join-Path (Join-Path $web 'forge-imports') (Join-Path $package.Hash 'package.json')
        if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) { throw "Candidate $($package.Name) package manifest is missing." }
        $packageDocument = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($manifest))
        if ([string]$packageDocument.source_commit -cne $sourceCommitPin) {
            throw "Candidate $($package.Name) package source commit differs from Tidewater source."
        }
    }
    return [ordered]@{
        kit_asset_kit_id=[string]$kit.asset_kit_id
        kit_asset_kit_revision=$kit.asset_kit_revision
        kit_technical_export_validated=[bool]$kit.qualification.technical_export_validated
        kit_human_art_review=$humanArtReview
        kit_publication_approved=$publicationApproved
        imports_schema=[string]$library.schema
        imports_source_commit=$sourceCommitPin
        imports_manifest_sha256=(Get-FileSha256 -LiteralPath $importsManifestPath)
        terrain_sha256=[string]$library.terrain_sha256
        environment_sha256=[string]$library.environment_sha256
        plants_sha256=[string]$library.plants_sha256
        ocean_sha256=if($library.PSObject.Properties.Name -contains 'ocean_sha256'){[string]$library.ocean_sha256}else{$null}
    }
}

if ($requestedSelfTest) {
    foreach ($name in @(
        'ConvertTo-CompactJson', 'ConvertFrom-ExactJson', 'Invoke-CdpCommand',
        'Invoke-CdpExpression', 'Wait-CdpReady', 'Wait-CdpTarget',
        'Connect-CdpSocket', 'New-LoopbackPortReservation', 'Wait-LaunchedExecutableIdentity',
        'Wait-NonGenericTask', 'Send-CdpMouseClick', 'Invoke-UiClick', 'Invoke-UiClickInDetailsBySummary',
        'Set-UiControlByLabel', 'Get-UiControlByLabel', 'Set-UiControlByLabelWithKeyboard', 'Select-UiOptionByLabel',
        'Copy-IsolatedFixture', 'Get-ArchiveSnapshot', 'Test-ArchiveSnapshotsEqual',
        'Get-FileSha256', 'Get-NormalizedSha256', 'Assert-DriveEPath',
        'Assert-NoExistingReparseAncestor', 'Assert-TreeHasNoReparsePoints',
        'Test-PathWithinDirectory', 'Test-IsWindowsPlatform', 'Get-Gate3ArchivePlacements',
        'Set-Gate3UiTextByLabel', 'Set-GameplayPhysicsForm', 'Invoke-Gate3BuildingRoomPreview'
    )) {
        [void](Get-Command $name -ErrorAction Stop)
    }
    foreach ($binding in @(
        [ordered]@{ command='Set-Gate3UiTextByLabel'; parameter='Text' },
        [ordered]@{ command='Set-GameplayForm'; parameter='Reply' },
        [ordered]@{ command='Set-GameplayForm'; parameter='CenterY' },
        [ordered]@{ command='Set-GameplayPhysicsForm'; parameter='BreakEnergy' }
    )) {
        $metadata = (Get-Command $binding.command -ErrorAction Stop).Parameters[$binding.parameter]
        $allowsEmpty = @($metadata.Attributes | Where-Object { $_.GetType().Name -eq 'AllowEmptyStringAttribute' }).Count -gt 0
        if (-not $allowsEmpty) { throw "Public helper binding $($binding.command).$($binding.parameter) does not allow an empty string." }
    }
    if ($zoneId -cnotmatch '^[0-9a-f-]{36}$' -or $requestedSourcePin -and $requestedSourcePin -notmatch '^[0-9A-Fa-f]{64}$') {
        throw 'Gate 3 source/zone selector constants are malformed.'
    }
    $raw = [IO.File]::ReadAllText($PSCommandPath)
    foreach ($needle in @(
        'Activer les règles de jeu dans cette zone', "Examiner l’ajout",
        'Examiner la quête et ses réponses', 'Examiner la liaison entre les lieux',
        'Examiner le nom, la position et la réplique', 'Sauvegarder',
        'Examiner le support et sa charge', 'Propriétés physiques',
        'Personnages, objets et objectifs', 'Construire',
        'Nouvelle pièce · 8 × 6 m · hauteur 3 m', 'Y · sol (m)',
        'Prévisualiser une nouvelle pièce', 'Appliquer', 'Set-Gate3UiTextByLabel', 'Get-UiControlByLabel',
        'Backspace', 'Input.insertText', 'AllowEmptyString', 'MeasuredFloorY',
        'IncludeImpactApproach', 'Bloc de contrepoids', '58.817839403',
        'be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3',
        '58.926544189453125', '59.446544189453125', 'supportTerrainSourceHash',
        'support_terrain_source_sha256', 'support_hash', 'supportTerrainGapOk',
        'flammable_label_count', 'heat_damage_label_count', 'fireControlsObservation',
        'fireDefaultsOk'
    )) {
        if ($raw.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) { throw "Public selector missing: $needle" }
    }
    Write-Output 'TIDEWATER_GATE3_GAMEPLAY_AUTHORING_SMOKE_SELF_TEST_PASS'
    return
}

if (-not (Test-IsWindowsPlatform)) { throw 'Windows/WebView2 is required.' }
$candidate = [IO.Path]::GetFullPath($Candidate)
$source = [IO.Path]::GetFullPath($FreshIsolatedFixture)
$output = [IO.Path]::GetFullPath($EvidenceRoot)
$sourcePin = Get-NormalizedSha256 -Value $SourceOriginalHashPinned
foreach ($entry in @(@($candidate, 'Candidate'), @($source, 'Fresh isolated fixture'), @($output, 'Evidence root'))) {
    Assert-DriveEPath -LiteralPath $entry[0] -Description $entry[1]
    Assert-NoExistingReparseAncestor -LiteralPath $entry[0] -Description $entry[1]
}
if (Test-Path -LiteralPath $output) { throw "EvidenceRoot must be fresh: $output" }
if ((Test-PathWithinDirectory -LiteralPath $output -Directory $source) -or
    (Test-PathWithinDirectory -LiteralPath $source -Directory $output)) {
    throw 'EvidenceRoot and fixture source must remain separate.'
}
if ((Test-PathWithinDirectory -LiteralPath $output -Directory (Split-Path -Parent $candidate)) -or
    (Test-PathWithinDirectory -LiteralPath (Split-Path -Parent $candidate) -Directory $output)) {
    throw 'EvidenceRoot and candidate directory must remain separate.'
}
if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { throw "Candidate absent: $candidate" }
Assert-TreeHasNoReparsePoints -LiteralPath $source -Description 'Fresh Tidewater fixture'
$candidateSha256 = Get-FileSha256 -LiteralPath $candidate
$proofPath = Join-Path (Split-Path -Parent $candidate) 'build-proof.json'
$candidateProof = $null
if (Test-Path -LiteralPath $proofPath -PathType Leaf) {
    $candidateProof = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($proofPath))
    if ($candidateProof.PSObject.Properties.Name -contains 'executable_sha256' -and
        (Get-NormalizedSha256 -Value ([string]$candidateProof.executable_sha256)) -cne $candidateSha256) {
        throw 'Candidate executable differs from build-proof.json.'
    }
}
$kitAndImports = Assert-Gate3CandidateInputs -CandidateExecutable $candidate
$sourceArchive = Join-Path (Join-Path $source $zoneId) 'authoring.json'
$sourceWorkspaceArchive = Join-Path $source 'workspace-v1\authoring.json'
$sourceObject = Join-Path (Join-Path $source 'workspace-v1\objects') "$sourcePin.json"
$sourceBefore = Get-ArchiveSnapshot -LiteralPath $sourceArchive
$sourceWorkspaceBefore = Get-ArchiveSnapshot -LiteralPath $sourceWorkspaceArchive
$sourceObjectBefore = Get-ArchiveSnapshot -LiteralPath $sourceObject
if ($sourceBefore.Sha256 -cne $sourcePin) { throw 'Fresh fixture zone archive does not match SourceOriginalHashPinned.' }
if (@(Get-Gate3ArchivePlacements -Archive $sourceArchive).Count -ne 23) { throw 'Fresh fixture must contain exactly 23 source placements.' }
$sourceReference = Get-Gate3WorkspaceReference -Archive $sourceWorkspaceArchive -ZoneId $zoneId
if ([string]$sourceReference.archive_hash -cne $sourcePin) { throw 'Workspace does not reference SourceOriginalHashPinned.' }
if ($sourceObjectBefore.Sha256 -cne $sourcePin -or @(Get-Gate3ArchivePlacements -Archive $sourceObject).Count -ne 23) {
    throw 'Source managed object is not the pinned 23-placement archive.'
}
$impactTerrainSource = Join-Path $source (Join-Path "$zoneId\source-blobs" $impactTerrainBlobName)
$impactTerrainSourceHash = $null
if ($IncludeImpactApproach) {
    if (-not (Test-Path -LiteralPath $impactTerrainSource -PathType Leaf)) {
        throw "Impact approach terrain source is missing: $impactTerrainSource"
    }
    $impactTerrainSourceHash = Get-FileSha256 -LiteralPath $impactTerrainSource
    if ($impactTerrainSourceHash -cne $impactTerrainBlobSha256) {
        throw "Impact approach terrain source hash differs from the pinned raster: $impactTerrainSource"
    }
}

[void](New-Item -ItemType Directory -Path $output)
$fixture = Join-Path $output 'authoring-root'
$archive = Copy-IsolatedFixture -SourceRoot $source -DestinationRoot $fixture -ZoneId $zoneId
$cloneBefore = Get-ArchiveSnapshot -LiteralPath $archive
$workspaceArchive = Join-Path $fixture 'workspace-v1\authoring.json'
$workspaceBefore = Get-ArchiveSnapshot -LiteralPath $workspaceArchive
$objectRoot = Join-Path $fixture 'workspace-v1\objects'
$originalObject = Join-Path $objectRoot "$sourcePin.json"
$originalObjectBefore = Get-ArchiveSnapshot -LiteralPath $originalObject
if ($cloneBefore.Sha256 -cne $sourceBefore.Sha256 -or $workspaceBefore.Sha256 -cne $sourceWorkspaceBefore.Sha256 -or
    $originalObjectBefore.Sha256 -cne $sourceObjectBefore.Sha256) {
    throw 'Isolated fixture copy does not preserve the pinned source.'
}
$profile = Join-Path $output 'webview2-user-data'
[void](New-Item -ItemType Directory -Path $profile)
$savedEnvironment = @{}
foreach ($name in @('FORGE_AUTHORING_ROOT','FORGE_AUTHOR_SUBJECT','WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS','WEBVIEW2_USER_DATA_FOLDER')) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$process = $null
$socket = $null
$reservation = $null
$port = 0
$failure = $null
$forcedProcessStop = $false
$nativeCloseConfirmed = $false
$uiClosed = $false
$savedWorkspace = $null
$savedReference = $null
$savedObjectPath = $null
$savedObject = $null
$gameplayReport = $null
$roomResult = $null
$roomApplied = $false
$supportYObserved = $null
$chargeYDerived = $null
$supportFootprintMaxY = 58.926544189453125
$supportCenterYExpected = 59.446544189453125
$supportTerrainSource = $null
$supportTerrainSourceHash = $null
$supportYPolicyOk = $false
$supportTerrainGapOk = $false
$doorYDerived = $null
$impactTerrainSource = $null
$impactTerrainSourceHash = $null
$fireDefaultsOk = $false
$steps = [System.Collections.Generic.List[object]]::new()
$supportTerrainSource = Join-Path $source (Join-Path "$zoneId\source-blobs" $impactTerrainBlobName)
if (-not (Test-Path -LiteralPath $supportTerrainSource -PathType Leaf)) {
    throw "Support terrain source is missing: $supportTerrainSource"
}
$supportTerrainSourceHash = Get-FileSha256 -LiteralPath $supportTerrainSource
if ($supportTerrainSourceHash -cne $impactTerrainBlobSha256) {
    throw "Support terrain source hash differs from the pinned raster: $supportTerrainSource"
}

try {
    $reservation = New-LoopbackPortReservation
    $port = $reservation.Port
    [Environment]::SetEnvironmentVariable('FORGE_AUTHORING_ROOT', $fixture, 'Process')
    [Environment]::SetEnvironmentVariable('FORGE_AUTHOR_SUBJECT', 'tidewater-gate3-gameplay-authoring-smoke', 'Process')
    [Environment]::SetEnvironmentVariable('WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', "--remote-debugging-address=127.0.0.1 --remote-debugging-port=$port", 'Process')
    [Environment]::SetEnvironmentVariable('WEBVIEW2_USER_DATA_FOLDER', $profile, 'Process')
    $reservation.Listener.Stop()
    $process = Start-Process -FilePath $candidate -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $output 'candidate.stdout.log') `
        -RedirectStandardError (Join-Path $output 'candidate.stderr.log')
    [void](Wait-LaunchedExecutableIdentity -Process $process -Candidate $candidate -ExpectedHash $candidateSha256)
    $target = Wait-CdpTarget -Port $port -TimeoutSeconds 60 -Process $process
    $socket = Connect-CdpSocket -WebSocketUrl ([string]$target.webSocketDebuggerUrl)
    [void](Invoke-CdpCommand -Socket $socket -Method 'Runtime.enable')
    [void](Invoke-CdpCommand -Socket $socket -Method 'Page.enable')
    [void](Invoke-CdpCommand -Socket $socket -Method 'Page.bringToFront')
    [void](Wait-CdpReady -Socket $socket -Description 'Studio authoring shell' -TimeoutSeconds $InteractionTimeoutSeconds -Expression @'
(() => ({ready:document.readyState==='complete'&&!!document.querySelector('.authoring-shell'),reason:'authoring shell pending'}))()
'@)
    Open-Gate3TidewaterZone -Socket $socket -ZoneId $zoneId -TimeoutSeconds $InteractionTimeoutSeconds
    [void](Invoke-UiClick -Socket $socket -Text 'Tester & suivi' -Selector '[data-authoring-tab-button]')
    [void](Open-DetailsBySummary -Socket $socket -Summary 'Choisir le départ du playtest')
    [void](Set-UiControlByLabelWithKeyboard -Socket $socket -Label 'Départ X (m)' -Text '40' -DetailsSummary 'Choisir le départ du playtest')
    [void](Set-UiControlByLabelWithKeyboard -Socket $socket -Label 'Départ Z (m)' -Text '-115' -DetailsSummary 'Choisir le départ du playtest')
    [void](Set-UiControlByLabelWithKeyboard -Socket $socket -Label 'Cap du départ enregistré (°)' -Text '0' -DetailsSummary 'Choisir le départ du playtest')
    [void](Invoke-UiClickInDetailsBySummary -Socket $socket -Summary 'Choisir le départ du playtest' -Text 'Examiner ce départ pour la zone')
    [void](Wait-GameplayPreview -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'accepted village start preview')
    [void](Invoke-UiClick -Socket $socket -Text 'Appliquer' -Selector 'button.authoring-apply')
    [void](Wait-GameplayApply -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'accepted village start apply')
    $steps.Add([ordered]@{action='set_zone_start';x=40;z=-115;facing_degrees=0;y_policy='canonical terrain at runtime';temporary=$false})
    if ($null -ne $MeasuredFloorY) {
        $roomResult = Invoke-Gate3BuildingRoomPreview -Socket $socket -MeasuredFloorY ([double]$MeasuredFloorY) -RoomOriginX $RoomOriginX -TimeoutSeconds $InteractionTimeoutSeconds
        $roomApplied = $true
        $steps.Add([ordered]@{
            action='add_building_room'
            name='Pièce'
            x=$RoomOriginX
            y=[double]$MeasuredFloorY
            z=-145
            y_policy='measured_floor_y_parameter'
            preview_status=$roomResult.preview_status
            apply_status=$roomResult.apply_status
        })
    }
    [void](Invoke-UiClick -Socket $socket -Text 'Objets' -Selector '[data-authoring-tab-button]')
    [void](Wait-GameplayEditor -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds)
    [void](Open-DetailsBySummary -Socket $socket -Summary $editorSummary)

    [void](Invoke-GameplayPreviewApply -Socket $socket -PreviewButton 'Activer les règles de jeu dans cette zone' -Description 'activate gameplay' -TimeoutSeconds $InteractionTimeoutSeconds)
    $steps.Add([ordered]@{ action='activate_gameplay'; passed=$true })
    Save-GameplayUiDiagnostic -Socket $socket -OutputRoot $output -Name 'after-activation'
    [void](Wait-GameplayForm -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds)

    Set-GameplayForm -Socket $socket -Kind 'friendly' -Name 'Intendant du village' -X '40' -Z '-118' -Reply 'Le sceau est au-delà de la forêt. Acceptez-vous cette mission ?' -TimeoutSeconds $InteractionTimeoutSeconds
    [void](Invoke-GameplayPreviewApply -Socket $socket -PreviewButton "Examiner l’ajout" -Description 'add village quest giver' -TimeoutSeconds $InteractionTimeoutSeconds)
    $steps.Add([ordered]@{ action='add_friendly'; name='Intendant du village'; x=40; z=-118; y='canonical' })

    Set-GameplayForm -Socket $socket -Kind 'enemy' -Name 'Gardien de la forêt' -X '16' -Z '-270' -Reply '' -TimeoutSeconds $InteractionTimeoutSeconds
    [void](Invoke-GameplayPreviewApply -Socket $socket -PreviewButton "Examiner l’ajout" -Description 'add forest enemy' -TimeoutSeconds $InteractionTimeoutSeconds)
    $steps.Add([ordered]@{ action='add_enemy'; name='Gardien de la forêt'; x=16; z=-270; y='canonical' })

    Set-GameplayForm -Socket $socket -Kind 'pickup' -Name 'Sceau' -X '18' -Z '-268' -Reply '' -TimeoutSeconds $InteractionTimeoutSeconds
    [void](Invoke-GameplayPreviewApply -Socket $socket -PreviewButton "Examiner l’ajout" -Description 'add quest seal pickup' -TimeoutSeconds $InteractionTimeoutSeconds)
    $steps.Add([ordered]@{ action='add_pickup'; name='Sceau'; x=18; z=-268; y='canonical' })

    $supportCenterYText = $supportCenterYExpected.ToString('R', [Globalization.CultureInfo]::InvariantCulture)
    Set-GameplayForm -Socket $socket -Kind 'prop' -Name 'Support de charge' -X '22' -Z '-268' -CenterY $supportCenterYText -Reply '' -TimeoutSeconds $InteractionTimeoutSeconds
    Set-GameplayPhysicsForm -Socket $socket -Mass '0' -Width '2' -Height '1' -Depth '1' -Pushable $false -Throwable $false -BreakEnergy '5'
    [void](Invoke-GameplayPreviewApply -Socket $socket -PreviewButton "Examiner l’ajout" -Description 'add physical charge support' -TimeoutSeconds $InteractionTimeoutSeconds)
    $steps.Add([ordered]@{
        action='add_physical_prop'
        name='Support de charge'
        x=22
        z=-268
        y=[double]$supportCenterYExpected
        y_policy='explicit center Y = pinned raster footprint max Y + half-height 0.5 + clearance 0.02'
        terrain_source=$impactTerrainBlobName
        terrain_source_sha256=$supportTerrainSourceHash
        dimensions_m=[ordered]@{ width=2.0; height=1.0; depth=1.0 }
        pushable=$false
        throwable=$false
        mass_kg=0
        break_energy_j=5
    })

    Select-GameplayOptionByText -Socket $socket -Label 'Élément à retoucher' -Text 'Support de charge' -TimeoutSeconds $InteractionTimeoutSeconds
    $supportYControl = $null
    $supportYText = ''
    for ($attempt = 0; $attempt -lt 20; $attempt += 1) {
        $supportYControl = Get-UiControlByLabel -Socket $socket -Label 'Hauteur du centre (m, vide = terrain)' -DetailsSummary $editorSummary
        $supportYText = [string]$supportYControl.value
        if (-not [string]::IsNullOrWhiteSpace($supportYText)) { break }
        Start-Sleep -Milliseconds 100
    }
    if ([string]::IsNullOrWhiteSpace($supportYText)) { throw 'Support Y was not exposed as a measured public UI value.' }
    [double]$supportYValue = 0
    if (-not [double]::TryParse($supportYText, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$supportYValue)) {
        throw "Support Y is not an invariant numeric public UI value: '$supportYText'"
    }
    $supportYObserved = $supportYValue
    if ([Math]::Abs($supportYObserved - $supportCenterYExpected) -ge 0.0001) {
        throw "Support Y differs from the pinned raster placement: expected $supportCenterYExpected, observed $supportYObserved"
    }
    $chargeYDerived = $supportYValue + 0.5 + 0.25 + 0.02
    $chargeYText = $chargeYDerived.ToString('R', [Globalization.CultureInfo]::InvariantCulture)
    Set-GameplayForm -Socket $socket -Kind 'prop' -Name 'Charge portée' -X '22' -Z '-268' -CenterY $chargeYText -Reply '' -TimeoutSeconds $InteractionTimeoutSeconds
    Set-GameplayPhysicsForm -Socket $socket -Mass '2' -Width '0.5' -Height '0.5' -Depth '0.5' -Pushable $false -Throwable $true -BreakEnergy ''
    [void](Invoke-GameplayPreviewApply -Socket $socket -PreviewButton "Examiner l’ajout" -Description 'add throwable charge' -TimeoutSeconds $InteractionTimeoutSeconds)
    $steps.Add([ordered]@{
        action='add_physical_prop'
        name='Charge portée'
        x=22
        z=-268
        y=[double]$chargeYDerived
        y_policy='support_public_y_plus_support_half_height_0.5_plus_charge_half_height_0.25_plus_gap_0.02'
        support_y_observed=[double]$supportYObserved
        throwable=$true
        mass_kg=2
    })

    if ($IncludeImpactApproach) {
        Set-GameplayForm -Socket $socket -Kind 'prop' -Name $impactApproachName -X '17.5' -Z '-270' -CenterY $impactApproachCenterY.ToString('R', [Globalization.CultureInfo]::InvariantCulture) -Reply '' -TimeoutSeconds $InteractionTimeoutSeconds
        Set-GameplayPhysicsForm -Socket $socket -Mass '100' -Width '0.5' -Height '1' -Depth '0.5' -Pushable $true -Throwable $false -BreakEnergy ''
        [void](Invoke-GameplayPreviewApply -Socket $socket -PreviewButton "Examiner l’ajout" -Description 'add bounded impact counterweight' -TimeoutSeconds $InteractionTimeoutSeconds)
        $steps.Add([ordered]@{
            action='add_impact_approach_prop'
            name=$impactApproachName
            x=17.5
            z=-270
            y=[double]$impactApproachCenterY
            y_policy='pinned_raster_footprint_max_58.297839403_plus_half_height_0.5_plus_clearance_0.02'
            terrain_source=$impactTerrainBlobName
            terrain_source_sha256=$impactTerrainBlobSha256
            dimensions_m=[ordered]@{ width=0.5; height=1.0; depth=0.5 }
            mass_kg=100
            pushable=$true
            throwable=$false
            flammable=$false
            break_energy_j=$null
            runtime_claim='authoring_only; impact/neutralization requires a separate playtest observation'
        })
    }

    if ($roomApplied) {
        $doorYDerived = ([double]$MeasuredFloorY) + 1.0
        $doorYText = $doorYDerived.ToString('R', [Globalization.CultureInfo]::InvariantCulture)
        Set-GameplayForm -Socket $socket -Kind 'door' -Name 'Porte intérieure' -X $RoomOriginX.ToString([Globalization.CultureInfo]::InvariantCulture) -Z '-148' -CenterY $doorYText -Reply '' -TimeoutSeconds $InteractionTimeoutSeconds
        Set-GameplayPhysicsForm -Socket $socket -Mass '0' -Width '1.2' -Height '2' -Depth '0.2' -Pushable $false -Throwable $false -BreakEnergy ''
        [void](Invoke-GameplayPreviewApply -Socket $socket -PreviewButton "Examiner l’ajout" -Description 'add gameplay door' -TimeoutSeconds $InteractionTimeoutSeconds)
        $steps.Add([ordered]@{
            action='add_door'
            name='Porte intérieure'
            x=$RoomOriginX
            z=-148
            y=[double]$doorYDerived
            y_policy='measured_floor_y_plus_door_half_height_1.0'
            initially_open=$false
        })
    } else {
        $steps.Add([ordered]@{ action='skip_door'; reason='MeasuredFloorY omitted; no room or explicit door Y was authored.' })
    }

    Select-GameplayOptionByText -Socket $socket -Label 'Élément à retoucher' -Text 'Support de charge' -TimeoutSeconds $InteractionTimeoutSeconds
    [void](Open-DetailsBySummary -Socket $socket -Summary 'Liaisons et mécanismes')
    Select-GameplayOptionByText -Socket $socket -Label 'Charge portée' -Text 'Charge portée' -TimeoutSeconds $InteractionTimeoutSeconds
    [void](Invoke-GameplayPreviewApply -Socket $socket -PreviewButton 'Examiner le support et sa charge' -Description 'attach charge to support' -TimeoutSeconds $InteractionTimeoutSeconds)
    $steps.Add([ordered]@{ action='support_charge'; support='Support de charge'; charge='Charge portée' })

    foreach ($place in @(
        [ordered]@{ Name='Village'; X='40'; Z='-118' },
        [ordered]@{ Name='Forêt'; X='16'; Z='-270' },
        [ordered]@{ Name='Intérieur'; X=$RoomOriginX.ToString([Globalization.CultureInfo]::InvariantCulture); Z='-145' }
    )) {
        Set-GameplayForm -Socket $socket -Kind 'place' -Name $place.Name -X $place.X -Z $place.Z -Reply '' -TimeoutSeconds $InteractionTimeoutSeconds
        [void](Invoke-GameplayPreviewApply -Socket $socket -PreviewButton "Examiner l’ajout" -Description "add place $($place.Name)" -TimeoutSeconds $InteractionTimeoutSeconds)
        $steps.Add([ordered]@{ action='add_place'; name=$place.Name; x=[double]$place.X; z=[double]$place.Z; y='canonical' })
    }

    [void](Set-UiControlByLabelWithKeyboard -Socket $socket -Label 'Nom' -Text 'Rapporter le Sceau' -DetailsSummary $editorSummary)
    [void](Open-DetailsBySummary -Socket $socket -Summary 'Quête de récupération')
    Select-GameplayOptionByText -Socket $socket -Label 'PNJ donneur' -Text 'Intendant du village' -TimeoutSeconds $InteractionTimeoutSeconds
    Select-GameplayOptionByText -Socket $socket -Label 'Objet demandé' -Text 'Sceau' -TimeoutSeconds $InteractionTimeoutSeconds
    [void](Invoke-GameplayPreviewApply -Socket $socket -PreviewButton 'Examiner la quête et ses réponses' -Description 'add seal recovery quest' -TimeoutSeconds $InteractionTimeoutSeconds)
    $steps.Add([ordered]@{ action='add_quest'; name='Rapporter le Sceau'; reward='ui_absent_recorded' })

    Select-GameplayOptionByText -Socket $socket -Label 'Élément à retoucher' -Text 'Intendant du village' -TimeoutSeconds $InteractionTimeoutSeconds
    [void](Set-UiControlByLabelWithKeyboard -Socket $socket -Label 'Nom' -Text 'Intendant du village' -DetailsSummary $editorSummary)
    [void](Set-UiControlByLabelWithKeyboard -Socket $socket -Label 'X (m)' -Text '40' -DetailsSummary $editorSummary)
    [void](Set-UiControlByLabelWithKeyboard -Socket $socket -Label 'Z (m)' -Text '-118' -DetailsSummary $editorSummary)
    [void](Set-Gate3UiTextByLabel -Socket $socket -Label 'Hauteur du centre (m, vide = terrain)' -Text '' -DetailsSummary $editorSummary)
    [void](Set-Gate3UiTextByLabel -Socket $socket -Label 'Réplique' -Text 'Le sceau est au-delà de la forêt. Revenez me voir avec le sceau.' -DetailsSummary $editorSummary)
    [void](Invoke-GameplayPreviewApply -Socket $socket -PreviewButton 'Examiner le nom, la position et la réplique' -Description 'retouch quest giver dialogue' -TimeoutSeconds $InteractionTimeoutSeconds)
    $steps.Add([ordered]@{ action='retouch_quest_giver'; name='Intendant du village'; y='canonical' })

    foreach ($link in @(
        [ordered]@{ Source='Village'; Destination='Forêt' },
        [ordered]@{ Source='Forêt'; Destination='Intérieur' },
        [ordered]@{ Source='Intérieur'; Destination='Village' }
    )) {
        [void](Open-DetailsBySummary -Socket $socket -Summary 'Liaisons et mécanismes')
        Select-GameplayOptionByText -Socket $socket -Label 'Élément à retoucher' -Text $link.Source -TimeoutSeconds $InteractionTimeoutSeconds
        Select-GameplayOptionByText -Socket $socket -Label 'Lieu de destination' -Text $link.Destination -TimeoutSeconds $InteractionTimeoutSeconds
        [void](Invoke-GameplayPreviewApply -Socket $socket -PreviewButton 'Examiner la liaison entre les lieux' -Description "link $($link.Source) to $($link.Destination)" -TimeoutSeconds $InteractionTimeoutSeconds)
        $steps.Add([ordered]@{ action='link_place'; source=$link.Source; destination=$link.Destination })
    }

    $beforeSaveWorkspace = Get-ArchiveSnapshot -LiteralPath $workspaceArchive
    [void](Invoke-UiClick -Socket $socket -Text 'Sauvegarder')
    [void](Wait-CdpReady -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'Gameplay zone save' -Expression @'
(() => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim();
  const status=norm(document.querySelector('.authoring-command-status')?.textContent);
  const saveState=norm(document.querySelector('.authoring-command-bar .gold-save-state')?.textContent);
  const context=norm(document.querySelector('.authoring-context')?.textContent);
  const busy=document.querySelector('.authoring-toolbar')?.getAttribute('aria-busy');
  const terminal=/refusée|erreur|invalide/i.test(status);
  return {ready:status==='Session synchronisée'&&saveState===''&&context!==''&&context!=='Aucune zone ouverte'&&busy==='false'&&!terminal,
    terminal,reason:'status='+status+'; save='+saveState+'; context='+context+'; busy='+busy};
})()
'@)
    $savedWorkspace = Get-ArchiveSnapshot -LiteralPath $workspaceArchive
    if (Test-ArchiveSnapshotsEqual -Before $beforeSaveWorkspace -After $savedWorkspace) { throw 'Studio Save did not update the managed workspace checkpoint.' }
    $savedReference = Get-Gate3WorkspaceReference -Archive $workspaceArchive -ZoneId $zoneId
    if ([string]$savedReference.archive_hash -ceq $sourcePin) { throw 'Studio Save did not create a new immutable gameplay object.' }
    if ([string]$savedReference.archive_hash -cnotmatch '^[0-9a-f]{64}$') { throw 'Studio Save returned an invalid managed object hash.' }
    $savedObjectPath = Join-Path $objectRoot "$($savedReference.archive_hash).json"
    $savedObject = Get-ArchiveSnapshot -LiteralPath $savedObjectPath
    if ($savedObject.Sha256 -cne [string]$savedReference.archive_hash) { throw 'Saved gameplay object hash does not match workspace ledger.' }
    if (@(Get-Gate3ArchivePlacements -Archive $savedObjectPath).Count -ne 23) { throw 'Saved gameplay object did not preserve the 23 source placements.' }
    if (-not (Test-ArchiveSnapshotsEqual -Before $cloneBefore -After (Get-ArchiveSnapshot -LiteralPath $archive)) -or
        -not (Test-ArchiveSnapshotsEqual -Before $originalObjectBefore -After (Get-ArchiveSnapshot -LiteralPath $originalObject))) {
        throw 'Gameplay Save altered the legacy source archive or original managed object.'
    }
    $gameplayReport = Get-Gate3GameplayReport -Archive $savedObjectPath
    if (-not $gameplayReport.present) { throw 'Saved archive has no gameplay correction.' }
    $actorNames = @($gameplayReport.actors | ForEach-Object { [string]$_.name })
    $itemNames = @($gameplayReport.items | ForEach-Object { [string]$_.name })
    $objectNames = @($gameplayReport.objects | ForEach-Object { [string]$_.name })
    $placeNames = @($gameplayReport.places | ForEach-Object { [string]$_.name })
    $playerActors = @($gameplayReport.actors | Where-Object { [string]$_.role -ceq 'player' })
    if ($playerActors.Count -ne 1 -or -not (Test-Gate3PositionXZ -Position $playerActors[0].position -X 40 -Z -115)) {
        throw 'The gameplay player initial pose does not use the publicly accepted village start.'
    }
    $friendlyActors = @($gameplayReport.actors | Where-Object { [string]$_.role -ceq 'friendly' })
    $enemyActors = @($gameplayReport.actors | Where-Object { [string]$_.role -ceq 'enemy' })
    $pickupObjects = @($gameplayReport.objects | Where-Object { [string]$_.kind.kind -ceq 'pickup' })
    $propObjects = @($gameplayReport.objects | Where-Object { [string]$_.kind.kind -ceq 'prop' })
    $doorObjects = @($gameplayReport.objects | Where-Object { [string]$_.kind.kind -ceq 'door' })
    $intendantPosition = @($gameplayReport.actors | Where-Object { [string]$_.name -ceq 'Intendant du village' })
    $enemyPosition = @($gameplayReport.actors | Where-Object { [string]$_.name -ceq 'Gardien de la forêt' })
    $sealPosition = @($gameplayReport.objects | Where-Object { [string]$_.name -ceq 'Sceau' })
    $supportEntry = @($gameplayReport.objects | Where-Object { [string]$_.name -ceq 'Support de charge' })
    $chargeEntry = @($gameplayReport.objects | Where-Object { [string]$_.name -ceq 'Charge portée' })
    $impactEntry = @($gameplayReport.objects | Where-Object { [string]$_.name -ceq $impactApproachName })
    $doorEntry = @($gameplayReport.objects | Where-Object { [string]$_.name -ceq 'Porte intérieure' })
    $villagePosition = @($gameplayReport.places | Where-Object { [string]$_.name -ceq 'Village' })
    $forestPosition = @($gameplayReport.places | Where-Object { [string]$_.name -ceq 'Forêt' })
    $interiorPosition = @($gameplayReport.places | Where-Object { [string]$_.name -ceq 'Intérieur' })
    $dialogueChoices = @(
        $gameplayReport.dialogues |
            ForEach-Object { $_.nodes } |
            ForEach-Object { $_.choices } |
            ForEach-Object { [string]$_.text }
    )
    $expectedObjectCount = if ($roomApplied) { 4 } else { 3 }
    $expectedFiniteYCount = if ($roomApplied) { 10 } else { 9 }
    $expectedPropCount = 2
    if ($IncludeImpactApproach) {
        $expectedObjectCount++
        $expectedFiniteYCount++
        $expectedPropCount++
    }
    $doorCardinalityOk = if ($roomApplied) {
        $doorObjects.Count -eq 1 -and $doorEntry.Count -eq 1
    } else {
        $doorObjects.Count -eq 0 -and $doorEntry.Count -eq 0
    }
    if ($gameplayReport.actors.Count -ne 3 -or $playerActors.Count -ne 1 -or $friendlyActors.Count -ne 1 -or $enemyActors.Count -ne 1 -or
        'Intendant du village' -notin $actorNames -or 'Gardien de la forêt' -notin $actorNames) {
        throw 'Saved gameplay actors do not match the first tranche.'
    }
    if ($gameplayReport.items.Count -ne 1 -or 'Sceau' -notin $itemNames -or $gameplayReport.objects.Count -ne $expectedObjectCount -or
        $pickupObjects.Count -ne 1 -or $propObjects.Count -ne $expectedPropCount -or -not $doorCardinalityOk -or
        'Sceau' -notin $objectNames -or $supportEntry.Count -ne 1 -or $chargeEntry.Count -ne 1) {
        throw 'Saved gameplay pickup, physical props or door do not match the first tranche.'
    }
    $doorPositionOk = $true
    if ($roomApplied) {
        $doorPositionOk = Test-Gate3PositionXZ -Position $doorEntry[0].position -X $RoomOriginX -Z -148
    }
    if ($intendantPosition.Count -ne 1 -or -not (Test-Gate3PositionXZ -Position $intendantPosition[0].position -X 40 -Z -118) -or
        $enemyPosition.Count -ne 1 -or -not (Test-Gate3PositionXZ -Position $enemyPosition[0].position -X 16 -Z -270) -or
        $sealPosition.Count -ne 1 -or -not (Test-Gate3PositionXZ -Position $sealPosition[0].position -X 18 -Z -268) -or
        -not (Test-Gate3PositionXZ -Position $supportEntry[0].position -X 22 -Z -268) -or
        -not (Test-Gate3PositionXZ -Position $chargeEntry[0].position -X 22 -Z -268) -or
        -not $doorPositionOk -or
        $villagePosition.Count -ne 1 -or -not (Test-Gate3PositionXZ -Position $villagePosition[0].position -X 40 -Z -118) -or
        $forestPosition.Count -ne 1 -or -not (Test-Gate3PositionXZ -Position $forestPosition[0].position -X 16 -Z -270) -or
        $interiorPosition.Count -ne 1 -or -not (Test-Gate3PositionXZ -Position $interiorPosition[0].position -X $RoomOriginX -Z -145)) {
        throw 'Saved gameplay positions do not match the first tranche coordinates.'
    }
    $supportChargeId = [string]$chargeEntry[0].id
    $supportPhysics = $supportEntry[0].physics
    $chargePhysics = $chargeEntry[0].physics
    $impactPositionOk = -not $IncludeImpactApproach
    $impactPhysicsOk = -not $IncludeImpactApproach
    $impactDimensionsOk = -not $IncludeImpactApproach
    $impactYOk = -not $IncludeImpactApproach
    if ($IncludeImpactApproach) {
        if ($impactEntry.Count -eq 1) {
            $impactPositionOk = Test-Gate3PositionXZ -Position $impactEntry[0].position -X 17.5 -Z -270
            $impactPhysics = $impactEntry[0].physics
            $impactPhysicsOk = [double]$impactPhysics.mass_kg -eq 100 -and
                [bool]$impactPhysics.pushable -and -not [bool]$impactPhysics.throwable -and
                -not [bool]$impactPhysics.flammable -and $null -eq $impactPhysics.break_energy_j -and
                @($impactPhysics.supports).Count -eq 0
            $impactDimensionsOk =
                [Math]::Abs([double]$impactPhysics.body_half_extents_m.x - 0.25) -lt 0.0001 -and
                [Math]::Abs([double]$impactPhysics.body_half_extents_m.y - 0.5) -lt 0.0001 -and
                [Math]::Abs([double]$impactPhysics.body_half_extents_m.z - 0.25) -lt 0.0001
            $impactYOk = [Math]::Abs([double]$impactEntry[0].position.y - $impactApproachCenterY) -lt 0.0001
        }
    }
    $supportDimensionsOk =
        [Math]::Abs([double]$supportPhysics.body_half_extents_m.x - 1.0) -lt 0.0001 -and
        [Math]::Abs([double]$supportPhysics.body_half_extents_m.y - 0.5) -lt 0.0001 -and
        [Math]::Abs([double]$supportPhysics.body_half_extents_m.z - 0.5) -lt 0.0001
    $supportYPolicyOk = $null -ne $supportYObserved -and
        [Math]::Abs([double]$supportYObserved - [double]$supportCenterYExpected) -lt 0.0001 -and
        [Math]::Abs([double]$supportEntry[0].position.y - [double]$supportCenterYExpected) -lt 0.0001
    $supportTerrainGapOk =
        [Math]::Abs(([double]$supportEntry[0].position.y - [double]$supportPhysics.body_half_extents_m.y) -
            [double]$supportFootprintMaxY - 0.02) -lt 0.0001
    $chargeDimensionsOk =
        [Math]::Abs([double]$chargePhysics.body_half_extents_m.x - 0.25) -lt 0.0001 -and
        [Math]::Abs([double]$chargePhysics.body_half_extents_m.y - 0.25) -lt 0.0001 -and
        [Math]::Abs([double]$chargePhysics.body_half_extents_m.z - 0.25) -lt 0.0001
    $doorPhysicsOk = $true
    $doorDimensionsOk = $true
    $doorYPolicyOk = $true
    if ($roomApplied) {
        $doorPhysics = $doorEntry[0].physics
        $doorDimensionsOk =
            [Math]::Abs([double]$doorPhysics.body_half_extents_m.x - 0.6) -lt 0.0001 -and
            [Math]::Abs([double]$doorPhysics.body_half_extents_m.y - 1.0) -lt 0.0001 -and
            [Math]::Abs([double]$doorPhysics.body_half_extents_m.z - 0.1) -lt 0.0001
        $doorPhysicsOk = [double]$doorPhysics.mass_kg -eq 0 -and
            -not [bool]$doorPhysics.pushable -and -not [bool]$doorPhysics.throwable
        $doorYPolicyOk = $null -ne $doorYDerived -and
            [Math]::Abs([double]$doorEntry[0].position.y - [double]$doorYDerived) -lt 0.0001
    }
    $chargeYPolicyOk = $null -ne $supportYObserved -and $null -ne $chargeYDerived -and
        [Math]::Abs([double]$supportEntry[0].position.y - [double]$supportYObserved) -lt 0.0001 -and
        [Math]::Abs([double]$chargeEntry[0].position.y - [double]$chargeYDerived) -lt 0.0001 -and
        [Math]::Abs([double]$chargeEntry[0].position.y - ([double]$supportCenterYExpected + 0.5 + 0.25 + 0.02)) -lt 0.0001 -and
        [double]$chargeEntry[0].position.y -gt [double]$supportEntry[0].position.y
    if ($script:fireControlsMode -notin @('absent', 'present', 'legacy_flammable_only')) {
        throw 'The public fire-control availability was not observed before archive verification.'
    }
    $fireDefaultViolations = @(
        foreach ($entry in @($gameplayReport.objects)) {
            $physics = $entry.physics
            if ($null -eq $physics) {
                [string]$entry.name
                continue
            }
            if (-not $physics.Contains('flammable') -or -not $physics.Contains('heat_damage_per_tick') -or
                $null -eq $physics.flammable -or $null -eq $physics.heat_damage_per_tick -or
                [bool]$physics.flammable -or [double]$physics.heat_damage_per_tick -ne 0) {
                [string]$entry.name
            }
        }
    )
    $fireDefaultsOk = $fireDefaultViolations.Count -eq 0
    if (-not $fireDefaultsOk) {
        throw "Saved archive fire defaults are not false/0 for: $($fireDefaultViolations -join ', ')."
    }
    if ([double]$supportPhysics.mass_kg -ne 0 -or [bool]$supportPhysics.pushable -or [bool]$supportPhysics.throwable -or
        [double]$chargePhysics.mass_kg -ne 2 -or [bool]$chargePhysics.pushable -or -not [bool]$chargePhysics.throwable -or
        $supportChargeId -notin @($supportPhysics.supports) -or -not $doorPhysicsOk -or
        $supportPhysics.break_energy_j -ne 5 -or -not $supportDimensionsOk -or -not $chargeDimensionsOk -or
        -not $supportYPolicyOk -or -not $supportTerrainGapOk -or -not $doorDimensionsOk -or
        -not $chargeYPolicyOk -or -not $doorYPolicyOk -or
        -not $impactPositionOk -or -not $impactPhysicsOk -or -not $impactDimensionsOk -or -not $impactYOk) {
        throw 'Saved gameplay physics or support/charge relation does not match the public controls.'
    }
    if ($gameplayReport.places.Count -ne 3 -or @('Village','Forêt','Intérieur' | Where-Object { $_ -notin $placeNames }).Count -ne 0) { throw 'Saved gameplay places do not match the first tranche.' }
    if ($gameplayReport.dialogues.Count -lt 1 -or $gameplayReport.quests.Count -ne 1 -or $gameplayReport.finite_y_count -ne $expectedFiniteYCount) { throw 'Saved gameplay dialogue, quest or canonical-Y counts are incomplete.' }
    if ("J’accepte." -notin $dialogueChoices -or "Voici l’objet demandé." -notin $dialogueChoices) { throw 'Saved quest dialogue does not contain accept and return choices.' }
    if (@($gameplayReport.receipt_ledger | Where-Object { [bool]$_.gameplay_request }).Count -lt 1) { throw 'Saved archive ledger has no gameplay correction receipt.' }
    foreach ($link in @(
        [ordered]@{ Source='Village'; Destination='Forêt' },
        [ordered]@{ Source='Forêt'; Destination='Intérieur' },
        [ordered]@{ Source='Intérieur'; Destination='Village' }
    )) {
        if ($link.Destination -notin @($gameplayReport.links_by_name[$link.Source])) { throw "Saved place link missing: $($link.Source) -> $($link.Destination)." }
    }
    $quest = @($gameplayReport.quests | Where-Object { [string]$_.name -ceq 'Rapporter le Sceau' })
    $intendant = @($gameplayReport.actors | Where-Object { [string]$_.name -ceq 'Intendant du village' })
    $sealItem = @($gameplayReport.items | Where-Object { [string]$_.name -ceq 'Sceau' })
    if ($quest.Count -ne 1 -or $intendant.Count -ne 1 -or $sealItem.Count -ne 1 -or
        [string]$quest[0].giver -cne [string]$intendant[0].id -or
        [string]$quest[0].required_item -cne [string]$sealItem[0].id -or
        $null -ne $quest[0].reward_item -or [int]$quest[0].reward_quantity -ne 0) {
        throw 'Quest giver, required item or reward state differs from the UI capability.'
    }
    $dialogueText = @($gameplayReport.dialogues[0].nodes | ForEach-Object { [string]$_.text }) -join ' | '
    if ($dialogueText -notlike '*Revenez me voir avec le sceau*') { throw 'Quest-giver dialogue retouch was not persisted.' }

    [void](Invoke-UiClick -Socket $socket -Text 'Projet' -Selector '[data-authoring-tab-button]')
    [void](Open-DetailsBySummary -Socket $socket -Summary 'Créer ou modifier la zone')
    [void](Invoke-UiClick -Socket $socket -Text 'Fermer la zone')
    [void](Wait-CdpReady -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'Close authored Tidewater zone' -Expression @'
(() => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim();
  const context=norm(document.querySelector('.authoring-context')?.textContent);
  const command=norm(document.querySelector('.authoring-command-status')?.textContent);
  const open=document.documentElement?.getAttribute('data-forge-zone-open');
  return {ready:context==='Aucune zone ouverte'&&open==='false',terminal:false,reason:'context='+context+'; open='+open+'; command='+command};
})()
'@)
    $uiClosed = $true
}
catch {
    $failure = $_.Exception.Message
    if ($null -ne $socket -and $socket.State -eq [Net.WebSockets.WebSocketState]::Open) {
        try { Save-GameplayUiDiagnostic -Socket $socket -OutputRoot $output -Name 'failure' }
        catch { Write-Verbose "Failure UI capture: $($_.Exception.Message)" }
    }
}
finally {
    # Authoring windows intercept WM_CLOSE and require the public confirmation
    # dialog even after a zone has been saved and closed. Keep CDP available
    # until that confirmation has been clicked; never inject CloseWindow.
    if ($null -ne $process -and -not $process.HasExited -and $null -ne $socket -and
        $socket.State -eq [Net.WebSockets.WebSocketState]::Open) {
        try {
            $process.Refresh()
            if (-not $process.CloseMainWindow()) { throw 'Owned Studio window did not accept the public close request.' }
            [void](Wait-CdpReady -Socket $socket -TimeoutSeconds 10 -Description 'Native Studio close confirmation' -Expression @'
(() => ({ready:!!document.querySelector('dialog.gold-close-dialog[open]'),terminal:false,reason:'native close dialog pending'}))()
'@)
            try { [void](Invoke-UiClick -Socket $socket -Text 'Quitter sans enregistrer' -ScopeSelector 'dialog.gold-close-dialog[open]') }
            catch { if (-not $process.WaitForExit(3000)) { throw } }
            $nativeCloseConfirmed = $process.WaitForExit(10000)
        } catch { Write-Verbose "Native close confirmation: $($_.Exception.Message)" }
    }
    if ($null -ne $socket) {
        if ($socket.State -eq [Net.WebSockets.WebSocketState]::Open) {
            try {
                $closing = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(2))
                try { Wait-NonGenericTask -Task ($socket.CloseAsync([Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'Gate 3 gameplay smoke complete', $closing.Token)) }
                finally { $closing.Dispose() }
            } catch { Write-Verbose "CDP close: $($_.Exception.Message)" }
        }
        $socket.Dispose()
    }
    if ($null -ne $process -and -not $process.HasExited) {
        try { [void]$process.CloseMainWindow() } catch { Write-Verbose "CloseMainWindow: $($_.Exception.Message)" }
        if (-not $process.WaitForExit(5000)) {
            Stop-Process -Id $process.Id -Force
            [void]$process.WaitForExit(5000)
            $forcedProcessStop = $true
        }
    }
    if ($null -ne $reservation) { $reservation.Listener.Stop() }
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process')
    }
}

$sourceAfter = Get-ArchiveSnapshot -LiteralPath $sourceArchive
$sourceWorkspaceAfter = Get-ArchiveSnapshot -LiteralPath $sourceWorkspaceArchive
$sourceObjectAfter = Get-ArchiveSnapshot -LiteralPath $sourceObject
$sourcePreserved = (Test-ArchiveSnapshotsEqual -Before $sourceBefore -After $sourceAfter) -and
    (Test-ArchiveSnapshotsEqual -Before $sourceWorkspaceBefore -After $sourceWorkspaceAfter) -and
    (Test-ArchiveSnapshotsEqual -Before $sourceObjectBefore -After $sourceObjectAfter)
if (-not $sourcePreserved) { $failure = "Source Tidewater fixture changed. $failure" }
$processClosed = $null -eq $process -or $process.HasExited
if (-not $processClosed) { $failure = "Owned Studio process remained alive. $failure" }
if ($forcedProcessStop) { $failure = "Studio required a forced stop. $failure" }
$impactLimitation = if ($IncludeImpactApproach) {
    'The optional 100 kg counterweight at (17.5,-270) is a bounded authoring fixture measured from the pinned Tidewater raster; runtime push, impact damage and guardian neutralization remain unclaimed until playtest evidence.'
} else {
    'The support and 2 kg charge at (22,-268) are separate from the forest guardian at (16,-270); this smoke does not claim an approach, trap or defeat.'
}
$ledger = [ordered]@{
    schema='forge.tidewater.gate3-gameplay-authoring-smoke'
    version=1
    status=if ([string]::IsNullOrWhiteSpace($failure)) { 'passed' } else { 'failed_closed' }
    failure=$failure
    gate_qualified=$false
    human_visual_review=$false
    authoring_scope='first_tranche_only'
    candidate=$candidate
    candidate_sha256=$candidateSha256
    candidate_build_proof=$candidateProof
    source_fixture=$source
    source_original_hash_pinned=$sourcePin
    source_archive_before=$sourceBefore
    source_archive_after=$sourceAfter
    source_workspace_before=$sourceWorkspaceBefore
    source_workspace_after=$sourceWorkspaceAfter
    source_object_before=$sourceObjectBefore
    source_object_after=$sourceObjectAfter
    source_preserved=$sourcePreserved
    include_impact_approach=[bool]$IncludeImpactApproach
    fire_controls_ui=if ($null -ne $script:fireControlsObservation) {
        $script:fireControlsObservation
    } else {
        [ordered]@{
            mode='not_observed'
            authoring_claim='no fire authoring claimed'
        }
    }
    fire_defaults_verified=[bool]$fireDefaultsOk
    support_charge_recipe=if ($null -ne $gameplayReport) {
        $savedSupport = @($gameplayReport.objects | Where-Object { [string]$_.name -ceq 'Support de charge' })
        $savedCharge = @($gameplayReport.objects | Where-Object { [string]$_.name -ceq 'Charge portée' })
        [ordered]@{
            support=[ordered]@{
                name='Support de charge'
                expected_position_x=22
                expected_position_z=-268
                expected_center_y=[double]$supportCenterYExpected
                observed_center_y=$supportYObserved
                saved_entry=if ($savedSupport.Count -eq 1) { $savedSupport[0] } else { $null }
                terrain_source=$impactTerrainBlobName
                terrain_source_path=$supportTerrainSource
                terrain_source_sha256=$supportTerrainSourceHash
                support_hash=$supportTerrainSourceHash
                measured_footprint_max_y=[double]$supportFootprintMaxY
                top_clearance_m=0.02
                y_policy='explicit center Y = pinned raster footprint max Y + half-height 0.5 + clearance 0.02'
                dimensions_m=[ordered]@{ width=2.0; height=1.0; depth=1.0 }
                mass_kg=0
                pushable=$false
                throwable=$false
                break_energy_j=5
                y_policy_verified=[bool]$supportYPolicyOk
                terrain_gap_verified=[bool]$supportTerrainGapOk
                runtime_claim='authoring_only; fixed controls and raster placement are persisted checks; runtime stability/contact is unclaimed'
            }
            charge=[ordered]@{
                name='Charge portée'
                expected_position_x=22
                expected_position_z=-268
                expected_center_y=$chargeYDerived
                saved_entry=if ($savedCharge.Count -eq 1) { $savedCharge[0] } else { $null }
                dimensions_m=[ordered]@{ width=0.5; height=0.5; depth=0.5 }
                mass_kg=2
                pushable=$false
                throwable=$true
                supported_by='Support de charge'
                y_policy='support center Y + support half-height 0.5 + charge half-height 0.25 + gap 0.02'
            }
            relation='logical support link persisted through the public UI; no runtime contact or release claim'
        }
    } else {
        $null
    }
    impact_approach=if ($IncludeImpactApproach -and $null -ne $gameplayReport) {
        $savedImpact = @($gameplayReport.objects | Where-Object { [string]$_.name -ceq $impactApproachName })
        [ordered]@{
            name=$impactApproachName
            enabled=$true
            expected_position_x=17.5
            expected_position_z=-270
            expected_center_y=[double]$impactApproachCenterY
            saved_entry=if ($savedImpact.Count -eq 1) { $savedImpact[0] } else { $null }
            terrain_source=$impactTerrainSource
            terrain_source_sha256=$impactTerrainSourceHash
            measured_footprint_max_y=58.297839403
            y_policy='explicit center Y = pinned raster footprint max Y + half-height 0.5 + clearance 0.02'
            runtime_claim='authoring_only; this smoke does not claim that pushing the block caused an impact or neutralized the guardian'
        }
    } else {
        $null
    }
    isolated_fixture=$fixture
    isolated_archive_before=$cloneBefore
    isolated_workspace_before=$workspaceBefore
    isolated_workspace_after=if(Test-Path -LiteralPath $workspaceArchive){Get-ArchiveSnapshot -LiteralPath $workspaceArchive}else{$null}
    saved_workspace=$savedWorkspace
    saved_reference=$savedReference
    saved_object=$savedObject
    saved_object_path=$savedObjectPath
    gameplay=$gameplayReport
    candidate_kit_and_imports=$kitAndImports
    steps=@($steps)
    zone_uuid=$zoneId
    measured_floor_y=$MeasuredFloorY
    room_origin_x=$RoomOriginX
    room_applied=$roomApplied
    room_result=$roomResult
    support_center_y_expected=$supportCenterYExpected
    support_y_observed=$supportYObserved
    support_footprint_max_y=$supportFootprintMaxY
    support_top_clearance_m=0.02
    support_terrain_source=$supportTerrainSource
    support_terrain_source_sha256=$supportTerrainSourceHash
    charge_y_derived=$chargeYDerived
    door_y_derived=$doorYDerived
    canonical_y_policy='Actor/place/support Y fields were cleared through the public UI and saved from canonical terrain; charge Y is derived from the observed support Y plus reviewed half-heights and gap; room floor Y is the optional measured parameter and door Y is derived from it.'
    ui_closed=$uiClosed
    owned_process_closed=$processClosed
    forced_process_stop=$forcedProcessStop
    native_close_confirmed=$nativeCloseConfirmed
    limitations=@(
        'This smoke authorizes only GameplayDocument authoring, persistence and UI retouch; it does not claim a playable room, collision, navigation or P05 playtest.',
        'The GameplayEditor path does not author room slab/walls/doorway geometry or expose DialogueEffect::SetDoor. When MeasuredFloorY is supplied, the separate Studio Construire panel applies the reviewed generic 8 × 6 m room at (RoomOriginX,-145), then the gameplay door is authored at the reviewed front (RoomOriginX,-148); without that parameter the smoke records no room or door.',
        'The generic quest form has no reward field; reward_item remains absent and is recorded as a UI limitation.',
        'Fire/flammability is outside this tranche; no prop-fire proof is claimed.',
        $impactLimitation,
        'A suspended-beam proof above an actor would require staged additional props and a 20 kg charge; it is outside this tranche.',
        'The kit is technical input only; human art review and publication qualification are not inferred.'
    )
}
$ledgerPath = Join-Path $output 'gate3-gameplay-authoring-smoke-ledger.json'
[IO.File]::WriteAllText($ledgerPath, ($ledger | ConvertTo-Json -Depth 100), [Text.UTF8Encoding]::new($false))
if ($ledger.status -cne 'passed') { throw "Gate 3 gameplay authoring smoke failed closed: $failure. Ledger: $ledgerPath" }
Write-Output "TIDEWATER_GATE3_GAMEPLAY_AUTHORING_SMOKE_PASS ledger=$ledgerPath"
