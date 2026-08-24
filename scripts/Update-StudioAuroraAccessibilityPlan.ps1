[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [switch]$Check
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
}

$trackerPath = Join-Path $RepositoryRoot 'tracking\project\AURORA_PARITY_COVERAGE.csv'
$catalogPath = Join-Path $RepositoryRoot 'src\Forge.Studio.Core\Navigation\StudioPageCatalog.cs'
$proofPath = Join-Path $RepositoryRoot 'tracking\studio\AURORA_STUDIO_PROOFS.csv'
$outputPath = Join-Path $RepositoryRoot 'tracking\studio\AURORA_STUDIO_ACCESSIBILITY.csv'

foreach ($requiredPath in @($trackerPath, $catalogPath, $proofPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required Studio accessibility input is missing: $requiredPath"
    }
}

$tracker = @(Import-Csv -LiteralPath $trackerPath -Delimiter ';' -Encoding UTF8)
if ($tracker.Count -ne 717) {
    throw "Studio accessibility planning requires exactly 717 Aurora rows, found $($tracker.Count)."
}

$featureIds = @($tracker | ForEach-Object feature_id)
if (@($featureIds | Sort-Object -Unique).Count -ne 717) {
    throw 'Studio accessibility planning requires 717 unique feature IDs.'
}

$proofRows = @(Import-Csv -LiteralPath $proofPath -Delimiter ';' -Encoding UTF8)
$allowedImplementationStatuses = @('functional', 'partial', 'read-only', 'placeholder', 'blocked')
$proofsByFeatureId = @{}
foreach ($proof in $proofRows) {
    if ([string]::IsNullOrWhiteSpace($proof.feature_id) -or $proof.feature_id -notin $featureIds) {
        throw "Studio proof references unknown feature '$($proof.feature_id)'."
    }
    if ($proofsByFeatureId.ContainsKey($proof.feature_id)) {
        throw "Studio proof feature '$($proof.feature_id)' is duplicated."
    }
    if ($proof.implementation_status -notin $allowedImplementationStatuses) {
        throw "Studio proof '$($proof.feature_id)' has invalid status '$($proof.implementation_status)'."
    }
    if ([string]::IsNullOrWhiteSpace($proof.proof_reference) -or
        [string]::IsNullOrWhiteSpace($proof.validated_at)) {
        throw "Studio proof '$($proof.feature_id)' is incomplete."
    }
    $proofsByFeatureId[$proof.feature_id] = $proof
}

$pageDefinitions = @{
    zones = @{ Section = 'world'; Surface = 'spatial'; Wave = 'UX1' }
    terrain = @{ Section = 'world'; Surface = 'spatial'; Wave = 'UX2' }
    placement = @{ Section = 'world'; Surface = 'spatial'; Wave = 'UX1' }
    environment = @{ Section = 'world'; Surface = 'spatial'; Wave = 'UX2' }
    navigation = @{ Section = 'world'; Surface = 'spatial'; Wave = 'UX2' }
    'living-world' = @{ Section = 'world'; Surface = 'spatial'; Wave = 'UX2' }
    blueprints = @{ Section = 'content'; Surface = 'hybrid'; Wave = 'UX3' }
    creatures = @{ Section = 'content'; Surface = 'hybrid'; Wave = 'UX4' }
    items = @{ Section = 'content'; Surface = 'hybrid'; Wave = 'UX4' }
    assets = @{ Section = 'content'; Surface = 'hybrid'; Wave = 'UX3' }
    'social-content' = @{ Section = 'content'; Surface = 'document'; Wave = 'UX4' }
    dialogues = @{ Section = 'story'; Surface = 'document'; Wave = 'UX5' }
    quests = @{ Section = 'story'; Surface = 'document'; Wave = 'UX5' }
    cinematics = @{ Section = 'story'; Surface = 'hybrid'; Wave = 'UX5' }
    lore = @{ Section = 'story'; Surface = 'document'; Wave = 'UX5' }
    localization = @{ Section = 'story'; Surface = 'document'; Wave = 'UX5' }
    rules = @{ Section = 'gameplay'; Surface = 'document'; Wave = 'UX6' }
    abilities = @{ Section = 'gameplay'; Surface = 'hybrid'; Wave = 'UX6' }
    scripts = @{ Section = 'gameplay'; Surface = 'document'; Wave = 'UX6' }
    runtime = @{ Section = 'gameplay'; Surface = 'document'; Wave = 'UX6' }
    'network-director' = @{ Section = 'gameplay'; Surface = 'live'; Wave = 'UX9' }
    playtest = @{ Section = 'test'; Surface = 'spatial'; Wave = 'UX7' }
    validation = @{ Section = 'test'; Surface = 'document'; Wave = 'UX7' }
    quality = @{ Section = 'test'; Surface = 'document'; Wave = 'UX7' }
    history = @{ Section = 'test'; Surface = 'document'; Wave = 'UX7' }
    build = @{ Section = 'publish'; Surface = 'document'; Wave = 'UX8' }
    packages = @{ Section = 'publish'; Surface = 'document'; Wave = 'UX8' }
    export = @{ Section = 'publish'; Surface = 'document'; Wave = 'UX8' }
    launcher = @{ Section = 'publish'; Surface = 'document'; Wave = 'UX8' }
    module = @{ Section = 'project'; Surface = 'document'; Wave = 'UX0' }
    workspace = @{ Section = 'project'; Surface = 'global'; Wave = 'UX0' }
    security = @{ Section = 'project'; Surface = 'document'; Wave = 'UX8' }
    production = @{ Section = 'project'; Surface = 'document'; Wave = 'UX8' }
    contextual = @{ Section = 'assistant'; Surface = 'contextual'; Wave = 'UX10' }
    plans = @{ Section = 'assistant'; Surface = 'document'; Wave = 'UX10' }
    'assisted-3d' = @{ Section = 'assistant'; Surface = 'hybrid'; Wave = 'UX10' }
}

$catalogText = Get-Content -LiteralPath $catalogPath -Raw -Encoding UTF8
$catalogPageIds = @([regex]::Matches($catalogText, 'Page\("(?<id>[^"]+)"') |
    ForEach-Object { $_.Groups['id'].Value } | Sort-Object -Unique)
$plannedPageIds = @($pageDefinitions.Keys | Sort-Object)
if ($catalogPageIds.Count -ne 36 -or $plannedPageIds.Count -ne 36 -or
    @($catalogPageIds | Where-Object { $_ -notin $plannedPageIds }).Count -gt 0 -or
    @($plannedPageIds | Where-Object { $_ -notin $catalogPageIds }).Count -gt 0) {
    throw 'Studio page catalog and accessibility page definitions are not an exact 36-page match.'
}

function Get-NormalizedText {
    param([AllowEmptyString()][string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return ''
    }

    $characters = foreach ($character in $Value.Normalize([Text.NormalizationForm]::FormD).ToCharArray()) {
        if ([Globalization.CharUnicodeInfo]::GetUnicodeCategory($character) -ne
            [Globalization.UnicodeCategory]::NonSpacingMark) {
            $character
        }
    }
    return (-join $characters).ToLowerInvariant()
}

function New-StudioRoute {
    param(
        [Parameter(Mandatory)][string]$Page,
        [Parameter(Mandatory)][string]$Interaction,
        [string]$SecondaryPage = '',
        [string]$Surface = '',
        [string]$Wave = ''
    )

    if (-not $pageDefinitions.ContainsKey($Page)) {
        throw "Unknown Studio accessibility page '$Page'."
    }
    if (-not [string]::IsNullOrWhiteSpace($SecondaryPage) -and
        -not $pageDefinitions.ContainsKey($SecondaryPage)) {
        throw "Unknown secondary Studio accessibility page '$SecondaryPage'."
    }

    $definition = $pageDefinitions[$Page]
    return [pscustomobject][ordered]@{
        Page = $Page
        SecondaryPage = $SecondaryPage
        Surface = $(if ([string]::IsNullOrWhiteSpace($Surface)) { $definition.Surface } else { $Surface })
        Interaction = $Interaction
        Wave = $(if ([string]::IsNullOrWhiteSpace($Wave)) { $definition.Wave } else { $Wave })
    }
}

function Resolve-StudioRoute {
    param([Parameter(Mandatory)]$Feature)

    $title = Get-NormalizedText $Feature.title
    switch ($Feature.domain) {
        'SHELL' {
            if ($title -match 'build du module') { return New-StudioRoute build 'status-action' }
            if ($title -match 'apercu et mode test') { return New-StudioRoute playtest 'run-inspect' }
            if ($title -match 'sauvegarde automatique|recuperation') { return New-StudioRoute history 'timeline-restore' }
            if ($title -match 'palette') { return New-StudioRoute blueprints 'search-place' -Surface global -Wave UX0 }
            if ($title -match 'vue 3d|selection et inspection|navigation camera|placement invalide|transformation avec gizmos') {
                return New-StudioRoute zones 'direct-manipulation' -Surface global -Wave UX0
            }
            if ($title -match 'copier|couper|coller|dupliquer') {
                return New-StudioRoute placement 'context-actions' -Surface global -Wave UX0
            }
            if ($title -match 'messages|erreurs|statut') {
                return New-StudioRoute validation 'issue-navigator' -Surface global -Wave UX0
            }
            return New-StudioRoute workspace 'preferences-command' -Surface global -Wave UX0
        }
        'PROJECT' {
            if ($title -match 'contenu personnalise ordonne') { return New-StudioRoute packages 'dependency-list' }
            if ($title -match 'film|sequence de demarrage') { return New-StudioRoute cinematics 'timeline-editor' -SecondaryPage module }
            if ($title -match 'difficulte') { return New-StudioRoute rules 'named-form' -SecondaryPage module }
            if ($title -match 'parametres par cible') { return New-StudioRoute export 'checklist-form' -SecondaryPage module }
            if ($title -match 'compatibilite|migration') { return New-StudioRoute production 'status-action' -SecondaryPage module }
            if ($title -match 'evenement|heartbeat|scripts mis en cache') {
                return New-StudioRoute module 'event-cards' -SecondaryPage scripts
            }
            return New-StudioRoute module 'named-form'
        }
        'AREA' {
            if ($title -match 'validation') { return New-StudioRoute validation 'issue-navigator' -SecondaryPage zones }
            if ($title -match 'play from here') { return New-StudioRoute playtest 'run-inspect' -SecondaryPage zones }
            if ($title -match 'terrain|tuile|tileset|effaceur|remplacement|ajustement aleatoire|proprietes de tuile') {
                return New-StudioRoute terrain 'brush-direct-manipulation'
            }
            if ($title -match 'jour/nuit|lumiere|brouillard|vent|meteo|skybox|ciel|eau|audio de zone|interieures|exterieures') {
                return New-StudioRoute environment 'profile-live-preview'
            }
            if ($title -match 'transition|navigation|carte|guerre|occlusion|visibilite|lod') {
                return New-StudioRoute navigation 'route-overlay-editor'
            }
            if ($title -match 'entree/sortie|heartbeat|utilisateur|perception|repos|pvp') {
                return New-StudioRoute 'living-world' 'event-cards'
            }
            return New-StudioRoute zones 'direct-manipulation'
        }
        'AUDIO' {
            if ($title -match 'voix de creature') { return New-StudioRoute creatures 'audio-preview' -SecondaryPage environment }
            if ($title -match 'sous-titres') { return New-StudioRoute localization 'translation-table' -SecondaryPage environment }
            if ($title -match 'validation') { return New-StudioRoute validation 'issue-navigator' -SecondaryPage environment }
            return New-StudioRoute environment 'audio-preview'
        }
        'BUILD' {
            if ($title -match 'dependency approval|third-party|notice') { return New-StudioRoute packages 'dependency-list' }
            if ($title -match 'packaging windows|multi-plateforme|import/export') { return New-StudioRoute export 'checklist-form' }
            if ($title -match 'depot git|structure de projet|versioning|adr|documentation|task manifests|release channels|crash artifacts') {
                return New-StudioRoute production 'truthful-status'
            }
            return New-StudioRoute build 'status-action'
        }
        'COMPANION' { return New-StudioRoute 'social-content' 'list-inspector' -SecondaryPage creatures }
        'CONTENT' {
            if ($title -match 'localisation|truetype|ui scalable') { return New-StudioRoute localization 'translation-table' }
            if ($title -match 'package|ordre de priorite|dependance|compilation|namespace|synchronisation|packs curates|migration|desinstallation|signature|delta') {
                return New-StudioRoute packages 'dependency-list' -SecondaryPage assets
            }
            if ($title -match 'credits|notices|ip boundary') { return New-StudioRoute production 'truthful-status' -SecondaryPage packages }
            if ($title -match 'compatibilite de plateforme') { return New-StudioRoute production 'status-action' -SecondaryPage assets }
            return New-StudioRoute assets 'asset-browser-preview'
        }
        'CREATURE' { return New-StudioRoute creatures 'list-inspector' }
        'DIALOGUE' { return New-StudioRoute dialogues 'line-choice-editor' }
        'DM' { return New-StudioRoute 'network-director' 'live-console' }
        'DOOR' {
            if ($title -match 'transition|navigation') { return New-StudioRoute navigation 'route-overlay-editor' -SecondaryPage placement }
            return New-StudioRoute placement 'direct-manipulation'
        }
        'EE' {
            if ($title -match 'build module') { return New-StudioRoute build 'status-action' }
            if ($title -match 'plusieurs onglets de zone') { return New-StudioRoute workspace 'preferences-command' -SecondaryPage zones }
            if ($title -match 'mise a l.echelle dans le toolset|creation dynamique de zone|copie dynamique') {
                return New-StudioRoute zones 'direct-manipulation'
            }
            if ($title -match 'pbr|tone mapping|hdr|eclairage|lumiere|eau|lod|msaa|filtrage anisotrope') {
                return New-StudioRoute environment 'profile-live-preview' -SecondaryPage quality
            }
            if ($title -match 'profiler tracy') { return New-StudioRoute quality 'run-inspect' }
            if ($title -match 'nwsync') { return New-StudioRoute packages 'dependency-list' }
            if ($title -match 'compilateur') { return New-StudioRoute scripts 'code-editor' }
            return New-StudioRoute runtime 'named-form'
        }
        'ENCOUNTER' { return New-StudioRoute 'living-world' 'scenario-composer' -SecondaryPage placement }
        'FACTION' { return New-StudioRoute 'social-content' 'relationship-matrix' }
        'GAMEPLAY' {
            if ($title -match 'regles plugin|difficulte|mutateurs|temps de monde') { return New-StudioRoute rules 'named-form' }
            if ($title -match 'cutscene|cinematics and dialogue') { return New-StudioRoute cinematics 'timeline-editor' }
            if ($title -match 'sauvegarde|checkpoint') { return New-StudioRoute history 'timeline-restore' -SecondaryPage runtime }
            if ($title -match 'replay deterministic test') { return New-StudioRoute playtest 'run-inspect' }
            if ($title -match 'achievements|milestones') { return New-StudioRoute abilities 'list-inspector' -SecondaryPage rules }
            return New-StudioRoute runtime 'named-form'
        }
        'ITEM' { return New-StudioRoute items 'list-inspector' }
        'MERCHANT' { return New-StudioRoute 'social-content' 'list-inspector' }
        'MULTI' {
            if ($title -match 'tests reseau') { return New-StudioRoute quality 'run-inspect' -SecondaryPage 'network-director' }
            if ($title -match 'decouverte et connexion|lobby|selection de personnage|creation de personnage|personnages locaux ou serveur') {
                return New-StudioRoute launcher 'form-live-preview' -SecondaryPage 'network-director'
            }
            return New-StudioRoute 'network-director' 'live-console' -SecondaryPage launcher
        }
        'PALETTE' {
            if ($title -match 'provenance|licence|compatibilite artistique|budget d.asset') {
                return New-StudioRoute assets 'asset-browser-preview' -SecondaryPage blueprints
            }
            return New-StudioRoute blueprints 'search-place'
        }
        'PLACEABLE' { return New-StudioRoute placement 'direct-manipulation' -SecondaryPage blueprints }
        'QA' {
            if ($title -match 'crash recovery') { return New-StudioRoute history 'timeline-restore' }
            if ($title -match 'validation|diagnostics cliquables') { return New-StudioRoute validation 'issue-navigator' }
            return New-StudioRoute quality 'run-inspect'
        }
        'QUEST' { return New-StudioRoute quests 'objective-state-editor' }
        'SCRIPT' { return New-StudioRoute scripts 'code-editor' }
        'TRIGGER' {
            if ($title -match 'transition') { return New-StudioRoute navigation 'route-overlay-editor' -SecondaryPage placement }
            if ($title -match 'repos sur|pistes|interjection') { return New-StudioRoute 'living-world' 'event-cards' -SecondaryPage placement }
            return New-StudioRoute placement 'event-cards'
        }
        'WAYPOINT' { return New-StudioRoute navigation 'route-overlay-editor' }
        'AI' {
            if ($title -match 'index de lore|graph de references') { return New-StudioRoute lore 'reference-index' -SecondaryPage contextual }
            if ($title -match 'plan de zone|construction de zone') { return New-StudioRoute zones 'plan-review' -SecondaryPage plans }
            if ($title -match 'dialogue') { return New-StudioRoute dialogues 'plan-review' -SecondaryPage plans }
            if ($title -match 'quete') { return New-StudioRoute quests 'plan-review' -SecondaryPage plans }
            if ($title -match 'forgescript') { return New-StudioRoute scripts 'plan-review' -SecondaryPage plans }
            if ($title -match 'boss') { return New-StudioRoute creatures 'plan-review' -SecondaryPage plans }
            if ($title -match 'capacite') { return New-StudioRoute abilities 'plan-review' -SecondaryPage plans }
            if ($title -match '3d|blender|text/image-to-3d') { return New-StudioRoute 'assisted-3d' 'variant-comparison' -SecondaryPage plans }
            if ($title -match 'vfx|audio|images') { return New-StudioRoute assets 'plan-review' -SecondaryPage plans }
            if ($title -match 'agent de test|simulation de combat') { return New-StudioRoute playtest 'plan-review' -SecondaryPage plans }
            if ($title -match 'plan avant mutation|dry-run|transaction|diff|autorisation|audit ia|prompts versionnes|resultats structures') {
                return New-StudioRoute plans 'plan-review'
            }
            if ($title -match 'pas d.execution python|prompt injection') { return New-StudioRoute security 'permission-review' -SecondaryPage contextual }
            return New-StudioRoute contextual 'plan-review' -SecondaryPage plans
        }
        default { throw "No Studio accessibility route exists for domain '$($Feature.domain)'." }
    }
}

function Get-DisclosureLevel {
    param([Parameter(Mandatory)]$Feature)

    $title = Get-NormalizedText $Feature.title
    if ($title -match 'depot git|build cli|build deterministe|cache jetable|ci$|adr|documentation generee|task manifests|third-party|release channels|crash artifacts|resource manager|interest management|sharding|bus d.evenements serveur|metriques|health checks|logs structures|profiler|anti-cheat|sandbox|handles opaques|budget d.instructions|budget memoire|determinisme|version d.api|analyse statique|security fuzzing|malicious module|soak tests|coverage aurora|acceptance gates|signatures|ip boundary') {
        return 'automatic'
    }
    if ($title -match 'schema|hash|permission|api |bytecode|compilateur|interop|profilage|migration|compatibilite|evenement|heartbeat|onuserdefined|onphysicalattacked|onspellcastat|ondamaged|ondeath|onopen|onclose|onlock|onunlock|onenter|onexit|script') {
        return 'advanced'
    }
    if ($title -match 'assistant|creation|identite|texte|vue 3d|selection|palette contextuelle|undo|apercu|terrain de base|dialogue|quete|blueprint|objet sonore|deplacement au sol|sauvegarde et chargement') {
        return 'beginner'
    }
    return 'standard'
}

function Get-AcceptanceContract {
    param([Parameter(Mandatory)][string]$Interaction)

    switch -Regex ($Interaction) {
        'direct-manipulation|brush|context-actions|search-place|named-form|list-inspector|event-cards|editor|matrix|audio-preview|profile-live-preview|form-live-preview|route-overlay|scenario-composer|asset-browser|dependency-list|checklist-form|preferences-command' {
            return 'command+dry-run+undo+save+preservation+ui+smoke+human'
        }
        'plan-review|variant-comparison' { return 'plan+dry-run+approval+test+revert+audit+ui+human' }
        'live-console' { return 'permission+confirmation+audit+disconnect+ui+smoke+human' }
        'run-inspect' { return 'isolated-run+cancel+result+navigation+ui+smoke+human' }
        'issue-navigator' { return 'diagnostic+deep-link+preview-fix+undo+ui+human' }
        'timeline-restore' { return 'checkpoint+impact+restore-copy+reopen+ui+smoke+human' }
        'truthful-status|status-action' { return 'truthful-state+logs-on-demand+cancel+ui+human' }
        'reference-index' { return 'search+deep-link+back-navigation+ui+human' }
        'permission-review' { return 'scope+consent+deny+audit+ui+human' }
        default { return 'named-access+state+ui+smoke+human' }
    }
}

function ConvertTo-DeterministicCsv {
    param([Parameter(Mandatory)][object[]]$Rows)

    $lines = @($Rows | ConvertTo-Csv -Delimiter ';' -NoTypeInformation)
    return [string]::Join("`n", $lines) + "`n"
}

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Content
    )

    [IO.File]::WriteAllText($Path, $Content, [Text.UTF8Encoding]::new($false))
}

$rows = foreach ($feature in $tracker) {
    $route = Resolve-StudioRoute -Feature $feature
    $definition = $pageDefinitions[$route.Page]
    $proof = $proofsByFeatureId[$feature.feature_id]
    [pscustomobject][ordered]@{
        feature_id = $feature.feature_id
        domain = $feature.domain
        title = $feature.title
        priority = $feature.priority
        section_id = $definition.Section
        page_id = $route.Page
        secondary_page_id = $route.SecondaryPage
        surface = $route.Surface
        interaction = $route.Interaction
        disclosure = Get-DisclosureLevel -Feature $feature
        delivery_wave = $route.Wave
        acceptance_contract = Get-AcceptanceContract -Interaction $route.Interaction
        implementation_status = $(if ($null -eq $proof) { 'planned' } else { $proof.implementation_status })
        proof_reference = $(if ($null -eq $proof) { '' } else { $proof.proof_reference })
        validated_at = $(if ($null -eq $proof) { '' } else { $proof.validated_at })
    }
}

if ($rows.Count -ne 717 -or @($rows.feature_id | Sort-Object -Unique).Count -ne 717) {
    throw 'Generated Studio accessibility matrix must cover every Aurora feature exactly once.'
}

foreach ($row in $rows) {
    if (-not $pageDefinitions.ContainsKey($row.page_id)) {
        throw "Generated row '$($row.feature_id)' uses unknown page '$($row.page_id)'."
    }
    if ([string]::IsNullOrWhiteSpace($row.interaction) -or
        [string]::IsNullOrWhiteSpace($row.disclosure) -or
        [string]::IsNullOrWhiteSpace($row.delivery_wave) -or
        [string]::IsNullOrWhiteSpace($row.acceptance_contract)) {
        throw "Generated row '$($row.feature_id)' has an incomplete human access contract."
    }
    if ($row.implementation_status -ne 'planned' -and
        ([string]::IsNullOrWhiteSpace($row.proof_reference) -or
         [string]::IsNullOrWhiteSpace($row.validated_at))) {
        throw "Generated row '$($row.feature_id)' has an unproven implementation status."
    }
}

$content = ConvertTo-DeterministicCsv -Rows $rows
if ($Check) {
    if (-not (Test-Path -LiteralPath $outputPath -PathType Leaf)) {
        throw "Studio accessibility matrix is missing: $outputPath"
    }
    $actual = [IO.File]::ReadAllText($outputPath, [Text.Encoding]::UTF8).Replace("`r`n", "`n")
    if ($actual -ne $content) {
        throw 'Studio accessibility matrix is stale. Run scripts/Update-StudioAuroraAccessibilityPlan.ps1.'
    }
}
else {
    [IO.Directory]::CreateDirectory((Split-Path -Parent $outputPath)) | Out-Null
    Write-Utf8NoBom -Path $outputPath -Content $content
}

$waveSummary = @($rows | Group-Object delivery_wave | Sort-Object Name |
    ForEach-Object { "$($_.Name)=$($_.Count)" }) -join ' '
$statusSummary = @($rows | Group-Object implementation_status | Sort-Object Name |
    ForEach-Object { "$($_.Name)=$($_.Count)" }) -join ' '
Write-Output "STUDIO_AURORA_ACCESSIBILITY=PASS rows=$($rows.Count) pages=$(@($rows.page_id | Sort-Object -Unique).Count) $waveSummary $statusSummary"
