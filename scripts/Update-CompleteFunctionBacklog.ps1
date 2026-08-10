[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [switch]$Check
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
}

$catalogPath = Join-Path $RepositoryRoot 'Divers\OpenRPG_Forge_Aurora_Feature_Dossier\feature_catalog.csv'
$trackerPath = Join-Path $RepositoryRoot 'tracking\project\AURORA_PARITY_COVERAGE.csv'
$outputPath = Join-Path $RepositoryRoot 'docs\planning\COMPLETE_FUNCTION_BACKLOG.md'

foreach ($requiredPath in @($catalogPath, $trackerPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required complete-backlog input is missing: $requiredPath"
    }
}

$catalog = @(Import-Csv -LiteralPath $catalogPath -Delimiter ';' -Encoding UTF8)
$tracker = @(Import-Csv -LiteralPath $trackerPath -Delimiter ';' -Encoding UTF8)

if ($catalog.Count -ne 717 -or $tracker.Count -ne 717) {
    throw 'Complete backlog requires exactly 717 source and project tracker rows.'
}

$catalogById = @{}
foreach ($feature in $catalog) {
    if ([string]::IsNullOrWhiteSpace($feature.id) -or $catalogById.ContainsKey($feature.id)) {
        throw "Aurora source catalog contains a missing or duplicate feature ID '$($feature.id)'."
    }

    $catalogById[$feature.id] = $feature
}

$trackerById = @{}
foreach ($feature in $tracker) {
    if (-not $catalogById.ContainsKey($feature.feature_id)) {
        throw "Project tracker contains unknown feature ID '$($feature.feature_id)'."
    }

    if ($trackerById.ContainsKey($feature.feature_id)) {
        throw "Project tracker contains duplicate feature ID '$($feature.feature_id)'."
    }

    if ($feature.status -notin @('NotStarted', 'Researched', 'Designed', 'Implemented', 'Tested')) {
        throw "Project tracker contains unsupported status '$($feature.status)' for '$($feature.feature_id)'."
    }

    if ($feature.status -eq 'Tested' -and [string]::IsNullOrWhiteSpace($feature.test_evidence)) {
        throw "Tested feature '$($feature.feature_id)' must provide test evidence."
    }

    $trackerById[$feature.feature_id] = $feature
}

foreach ($featureId in $catalogById.Keys) {
    if (-not $trackerById.ContainsKey($featureId)) {
        throw "Source feature '$featureId' is missing from the project tracker."
    }
}

function Escape-MarkdownCell {
    param([AllowEmptyString()][string]$Value)

    if ($null -eq $Value) {
        return ''
    }

    return $Value.Replace('|', '\|').Replace("`r", ' ').Replace("`n", ' ').Trim()
}

function Get-LotState {
    param([Parameter(Mandatory)][object[]]$Features)

    if (@($Features | Where-Object status -ne 'Tested').Count -eq 0) {
        return 'Clos'
    }

    if (@($Features | Where-Object status -in @('NotStarted', 'Researched', 'Designed')).Count -gt 0) {
        return 'À livrer'
    }

    return 'À compléter et prouver'
}

function Get-ProofContract {
    param([Parameter(Mandatory)][string]$State)

    switch ($State) {
        'Clos' { return 'MAINTAIN' }
        'À compléter et prouver' { return 'COMPLETE' }
        default { return 'DELIVER' }
    }
}

$priorityOrder = @('P0', 'P1', 'P2', 'P3', 'P4')
$gateOrder = @{
    G0 = 0; G1 = 1; G2 = 2; G3 = 3; G4 = 4; G5 = 5; G6 = 6
    G7 = 7; G8 = 8; G9 = 9; G10 = 10; G11 = 11; G12 = 12; G13 = 13
}
$journeyByDomain = @{
    BUILD = 'Fondations'; QA = 'Fondations'; CONTENT = 'Fondations'; PROJECT = 'Fondations'
    AREA = 'Creator'; PALETTE = 'Creator'; SHELL = 'Creator'
    DIALOGUE = 'Writer'; QUEST = 'Writer'; AUDIO = 'Writer'; WAYPOINT = 'Writer'
    CREATURE = 'RPG'; FACTION = 'RPG'; ITEM = 'RPG'; PLACEABLE = 'RPG'; DOOR = 'RPG'
    TRIGGER = 'RPG'; ENCOUNTER = 'RPG'; GAMEPLAY = 'RPG'; MERCHANT = 'RPG'; COMPANION = 'RPG'
    SCRIPT = 'Scripter'; AI = 'Agent'; EE = 'Runtime'; MULTI = 'Network'; DM = 'GM'
}
$journeyOrder = @{
    Fondations = 0; Creator = 1; Writer = 2; RPG = 3; Scripter = 4
    Runtime = 5; Agent = 6; Network = 7; GM = 8
}
$lots = @()

foreach ($domainGroup in @($tracker | Group-Object domain | Sort-Object Name)) {
    foreach ($priority in $priorityOrder) {
        $features = @($domainGroup.Group | Where-Object priority -eq $priority | Sort-Object feature_id)
        if ($features.Count -eq 0) {
            continue
        }

        $lotNumber = 0
        for ($start = 0; $start -lt $features.Count; $start += 5) {
            $lotNumber++
            $end = [Math]::Min($start + 4, $features.Count - 1)
            $batch = @($features[$start..$end])
            if ($batch.Count -lt 1 -or $batch.Count -gt 5) {
                throw "Generated lot for '$($domainGroup.Name)' '$priority' is outside the 1-5 feature boundary."
            }

            $state = Get-LotState -Features $batch
            $dependencies = @($batch | ForEach-Object {
                    $sourceFeature = $catalogById[$_.feature_id]
                    if (-not [string]::IsNullOrWhiteSpace($sourceFeature.dependencies)) {
                        $sourceFeature.dependencies -split ','
                    }
                } | ForEach-Object { $_.Trim() } |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and $_ -ne $domainGroup.Name } |
                Sort-Object -Unique)
            $targetGates = @($batch.target_epic | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
                Sort-Object { if ($gateOrder.ContainsKey($_)) { $gateOrder[$_] } else { 99 } } -Unique)
            $firstGateOrder = if ($targetGates.Count -gt 0 -and $gateOrder.ContainsKey($targetGates[0])) {
                $gateOrder[$targetGates[0]]
            }
            else {
                99
            }
            $lots += [pscustomobject]@{
                Id = 'FC-{0}-{1}-{2:D2}' -f $domainGroup.Name, $priority, $lotNumber
                Domain = $domainGroup.Name
                Priority = $priority
                State = $state
                Contract = Get-ProofContract -State $state
                Features = $batch
                Tested = @($batch | Where-Object status -eq 'Tested').Count
                Dependencies = $dependencies
                TargetGates = $targetGates
                FirstGateOrder = $firstGateOrder
                Journey = $journeyByDomain[$domainGroup.Name]
                ExecutionRank = $null
            }
        }
    }
}

$coveredIds = @($lots | ForEach-Object Features | ForEach-Object feature_id)
if ($coveredIds.Count -ne 717 -or @($coveredIds | Sort-Object -Unique).Count -ne 717) {
    throw 'Generated complete backlog must cover every feature exactly once.'
}

$orderedLots = @($lots | Sort-Object `
        @{ Expression = { [Array]::IndexOf($priorityOrder, $_.Priority) } },
        @{ Expression = { $_.FirstGateOrder } },
        @{ Expression = { $journeyOrder[$_.Journey] } },
        Domain,
        Id)

for ($index = 0; $index -lt $orderedLots.Count; $index++) {
    $orderedLots[$index].ExecutionRank = $index + 1
}

$openLots = @($orderedLots | Where-Object State -ne 'Clos')

$statusCounts = @{}
foreach ($status in @('NotStarted', 'Researched', 'Designed', 'Implemented', 'Tested')) {
    $statusCounts[$status] = @($tracker | Where-Object status -eq $status).Count
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('# Plan canonique de complétude fonctionnelle')
$lines.Add('')
$lines.Add('> Fichier généré par `scripts/Update-CompleteFunctionBacklog.ps1`. Ne pas modifier à la main.')
$lines.Add('')
$lines.Add('## Portée et interprétation')
$lines.Add('')
$lines.Add('Ce plan couvre exactement les 717 fonctions du catalogue Aurora clean-room immuable. Il ne')
$lines.Add('déclare pas que les 717 fonctions sont terminées : un gate vertical fermé prouve un parcours')
$lines.Add("utilisateur, tandis qu'une fonction n'est close ici que lorsque le tracker projet la classe")
$lines.Add('`Tested` avec une preuve automatisée ou une procédure reproductible liée.')
$lines.Add('')
$lines.Add('Les identifiants `FC-<DOMAINE>-<PRIORITÉ>-<NN>` sont stables tant que le catalogue source reste')
$lines.Add("immuable. Chaque lot contient de une à cinq fonctions d'un même domaine et d'une même priorité.")
$lines.Add("La présence d'un lot dans ce plan ne l'ouvre pas automatiquement : une décision explicite choisit")
$lines.Add('un seul lot actif après vérification de ses dépendances.')
$lines.Add('')
$lines.Add('## État de couverture')
$lines.Add('')
$lines.Add('| Mesure | Valeur |')
$lines.Add('|---|---:|')
$lines.Add("| Fonctions source et tracker | $($tracker.Count) |")
$lines.Add("| Lots bornés générés | $($lots.Count) |")
$lines.Add("| Lots clos | $(@($lots | Where-Object State -eq 'Clos').Count) |")
$lines.Add("| Lots à compléter et prouver | $(@($lots | Where-Object State -eq 'À compléter et prouver').Count) |")
$lines.Add("| Lots à livrer | $(@($lots | Where-Object State -eq 'À livrer').Count) |")
$lines.Add("| Lots P0 ouverts | $(@($openLots | Where-Object Priority -eq 'P0').Count) |")
$lines.Add("| Fonctions P0 non closes | $(@($tracker | Where-Object { $_.priority -eq 'P0' -and $_.status -ne 'Tested' }).Count) |")
$lines.Add("| `Tested` | $($statusCounts['Tested']) |")
$lines.Add("| `Implemented` partiel | $($statusCounts['Implemented']) |")
$lines.Add("| `Designed` | $($statusCounts['Designed']) |")
$lines.Add("| `Researched` | $($statusCounts['Researched']) |")
$lines.Add("| `NotStarted` | $($statusCounts['NotStarted']) |")
$lines.Add('')
$lines.Add('## Contrats de sortie hérités par chaque lot')
$lines.Add('')
$lines.Add('- `DELIVER` : relire les critères source, définir le contrat canonique, implémenter le parcours')
$lines.Add('  humain sans JSON, ajouter les tests de domaine/application et le smoke Toolset/runtime/MCP')
$lines.Add('  applicable, puis lier les preuves avant tout passage à `Tested`.')
$lines.Add('- `COMPLETE` : auditer la note `Partial`, fermer chaque comportement manquant, ajouter ou renforcer')
$lines.Add('  les tests ciblés et la preuve visible/headless applicable, puis seulement passer à `Tested`.')
$lines.Add('- `MAINTAIN` : conserver les preuves existantes et ne rouvrir le lot qu''en cas de régression ou')
$lines.Add('  de critère source explicitement non satisfait.')
$lines.Add('')
$lines.Add('Avant implémentation, une fiche détaillée est créée sous `docs/aurora-parity/tasks/` avec les')
$lines.Add('fichiers probables, dépendances, critères utilisateur et commandes de test exactes. Le présent')
$lines.Add('registre garantit la couverture et la taille ; il ne remplace pas cette conception bornée.')
$lines.Add('')
$lines.Add("## Ordre d’exécution recommandé")
$lines.Add('')
$lines.Add('Le rang `R###` est une file de travail, pas un nouvel identifiant fonctionnel. Il ordonne les')
$lines.Add('lots par priorité, premier gate cible, parcours utilisateur puis identifiant stable. Le rang ne')
$lines.Add("change pas lorsqu’un lot est fermé ; les lots clos disparaissent simplement de la file ouverte.")
$lines.Add('Les dépendances déclarées viennent du catalogue source. Avant activation, elles sont')
$lines.Add('réévaluées contre le tracker : une dépendance non close passe avant le lot ou reçoit une décision')
$lines.Add('explicite. Un seul lot de une à cinq fonctions est actif à la fois.')
$lines.Add('')
$lines.Add('| Rang | Lot | Priorité | Gate(s) | Parcours | Dépendances déclarées | Progression | Contrat |')
$lines.Add('|---:|---|---|---|---|---|---:|---|')
foreach ($lot in $openLots) {
    $gateLabel = if ($lot.TargetGates.Count -gt 0) { $lot.TargetGates -join ', ' } else { '—' }
    $dependencyLabel = if ($lot.Dependencies.Count -gt 0) { $lot.Dependencies -join ', ' } else { '—' }
    $lines.Add(('| `R{0:D3}` | `{1}` | {2} | {3} | {4} | {5} | {6}/{7} `Tested` | `{8}` |' -f
        $lot.ExecutionRank, $lot.Id, $lot.Priority, $gateLabel, $lot.Journey, $dependencyLabel,
        $lot.Tested, $lot.Features.Count, $lot.Contract))
}
$lines.Add('')
$lines.Add('## Synthèse par domaine')
$lines.Add('')
$lines.Add('| Domaine | Total | Tested | À terminer/prouver | Lots |')
$lines.Add('|---|---:|---:|---:|---:|')
foreach ($domainGroup in @($tracker | Group-Object domain | Sort-Object Name)) {
    $tested = @($domainGroup.Group | Where-Object status -eq 'Tested').Count
    $domainLots = @($lots | Where-Object Domain -eq $domainGroup.Name).Count
    $lines.Add("| $($domainGroup.Name) | $($domainGroup.Count) | $tested | $($domainGroup.Count - $tested) | $domainLots |")
}
$lines.Add('')
$lines.Add('## Registre exhaustif des lots')
$lines.Add('')
$lines.Add('| Lot | Domaine | Priorité | État | Progression | Contrat | Fonctions et état actuel |')
$lines.Add('|---|---|---|---|---:|---|---|')

foreach ($lot in @($lots | Sort-Object @{ Expression = { [Array]::IndexOf($priorityOrder, $_.Priority) } }, Domain, Id)) {
    $featureLabels = @()
    foreach ($feature in $lot.Features) {
        $title = Escape-MarkdownCell -Value $feature.title
        $featureLabels += ('`{0}` — {1} (`{2}`)' -f $feature.feature_id, $title, $feature.status)
    }

    $lines.Add(('| `{0}` | {1} | {2} | {3} | {4}/{5} `Tested` | `{6}` | {7} |' -f
            $lot.Id, $lot.Domain, $lot.Priority, $lot.State, $lot.Tested, $lot.Features.Count,
            $lot.Contract, ($featureLabels -join '<br>')))
}

$lines.Add('')
$lines.Add('## Règle de complétude à 100 %')
$lines.Add('')
$lines.Add('La cible fonctionnelle initiale est atteinte uniquement lorsque les 717 lignes sont `Tested`,')
$lines.Add('les 187 lots sont `Clos`, le tracker et ce fichier sont frais, et le portail intégré reste vert.')
$lines.Add('Un nombre de gates fermés, de classes, de marqueurs ou de tests ne remplace pas cette condition.')

$expected = ($lines -join "`n") + "`n"

if ($Check) {
    if (-not (Test-Path -LiteralPath $outputPath)) {
        throw "Generated complete backlog is missing: $outputPath"
    }

    $actual = [IO.File]::ReadAllText($outputPath).Replace("`r`n", "`n")
    if ($actual -ne $expected) {
        throw "Generated complete backlog is stale: $outputPath"
    }

    Write-Output "OPENRPG_FORGE_COMPLETE_BACKLOG_CHECK_PASS features=717 lots=$($lots.Count) tested=$($statusCounts['Tested']) p0_open=$(@($openLots | Where-Object Priority -eq 'P0').Count)"
    exit 0
}

$outputDirectory = Split-Path -Parent $outputPath
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
[IO.File]::WriteAllText($outputPath, $expected, (New-Object Text.UTF8Encoding($false)))
Write-Output "OPENRPG_FORGE_COMPLETE_BACKLOG_UPDATED features=717 lots=$($lots.Count) tested=$($statusCounts['Tested']) p0_open=$(@($openLots | Where-Object Priority -eq 'P0').Count)"
