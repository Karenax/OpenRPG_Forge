# Stratégie de vérification ciblée

Cette stratégie choisit le plus petit ensemble de validations capable de prouver le changement
courant. Elle ne réduit pas les exigences de clôture : elle place chaque test au bon moment.

## Frontière entre skills et code

| Processus observé | Décision | Motif |
|---|---|---|
| choisir le risque, le palier, le smoke et expliquer les limites | nouveau skill `openrpg-targeted-verification` | jugement récurrent commun à tous les lots |
| cycle Aurora ou asset | skills existants mis à jour | le métier et ses artefacts restent spécifiques |
| clôture globale et publication | skill `openrpg-delivery-gate` conservé, mais réservé aux jalons | preuve transversale distincte de la boucle quotidienne |
| mapping fichier → projet, filtres, TRX, comptes et ordre | scripts PowerShell | logique déterministe qui doit être testée et versionnée |
| inventaire des routes Studio | code C# et tests unitaires | contrat exécutable, trop précis et trop évolutif pour un prompt |
| chaque suite, profil ou route individuelle | aucun skill supplémentaire | une multiplication de skills dupliquerait la matrice et la ferait dériver |

Le skill orchestre donc le raisonnement et le compte rendu. Les scripts restent l'autorité sur les
commandes réellement exécutées, et les profils ne sont que des entrées stables du même moteur.

## Les quatre moments

| Moment | Sélection | Smoke | Graphe | Portail Full |
|---|---|---|---|---|
| `Development` | classes liées, sinon projet affecté | route exacte | contrôle rapide | non |
| `Lot` | suites des projets affectés | route et scénario du lot | contrôle rapide | non |
| `Milestone` | profils des sous-systèmes touchés | vague UX ou scénario transversal | génération puis contrôle complet | selon seuil |
| `Publication` | toutes les suites | toutes les surfaces | contrôle complet | oui |

Les seuils de jalon sont trois à cinq lots liés, dix à vingt routes, une fin de vague ou un contrat
partagé important : sérialisation, runtime, moteur, assets/provenance, MCP/sécurité, réseau, build ou
distribution.

## Planifier sans exécuter

```powershell
& .\scripts\Get-VerificationPlan.ps1 `
  -ChangedPath @(
    'src/Forge.Application/Projects/Example.cs',
    'tests/Forge.Application.Tests/Projects/ExampleTests.cs'
  ) `
  -FeatureId ORF-PROJ-001 `
  -Stage Development
```

Sans `-ChangedPath`, le planificateur utilise le diff Git et les fichiers non suivis. Un checkout
propre exige un profil explicite. La sortie JSON décrit chaque raison, coût et effet de bord. Pour un
lot transversal, utiliser `-Stage Lot` ou `Milestone`; pour ignorer volontairement les requêtes du
graphe, préciser `-NoGraph` et assumer le repli par projet.

## Exécuter le plan

```powershell
& .\scripts\Invoke-TargetedVerification.ps1 `
  -ChangedPath @('src/Forge.Application/Projects/Example.cs') `
  -Stage Development
```

`-PlanOnly` ne crée aucun artefact. Une exécution crée `plan.json`, les TRX et `summary.json` sous un
répertoire horodaté de `artifacts/verification/targeted/`. Les étapes sont toujours séquentielles et
fail-fast. Chaque filtre appartient à un seul projet ; toute sélection à zéro test échoue.

## Profils de sous-système

| Profil | Preuve principale | Exclut volontairement |
|---|---|---|
| `Core` | build solution, suites .NET/Python, trackers et graphe | smokes moteur et distributions |
| `Studio` | build Debug, tests Core, matrice et smoke Route/Wave/Full | runtime, réseau et assets externes |
| `Runtime` | contrats Domain/Application/Adapter et smoke Godot réel | Studio et distribution |
| `Assets` | contrats Domain/Application/Format et pipeline Python | Vault externe non disponible |
| `Network` | tests Network/LiveDirector/MCP et serveur réel | client Studio et distribution |
| `Distribution` | manifestes, tailles, SHA-256 et conformité existants | reconstruction et publication |
| `Full` | portail transversal historique | rien |

Exemples :

```powershell
& .\scripts\Verify.ps1 -Profile Studio -Page zones -Evidence ux2-nav-l -StudioSmokeScope Route
& .\scripts\Verify.ps1 -Profile Studio -Page scripts -Evidence ux6-o -StudioSmokeScope Wave
& .\scripts\Verify.ps1 -Profile Runtime
& .\scripts\Verify.ps1 -Profile Distribution -DistributionProduct Studio
& .\scripts\Verify.ps1 -Profile Full
```

Le profil `Distribution` utilise par défaut les distributions Windows canoniques existantes. Passer
`-DistributionDirectory` pour une autre cible. `-Restore` est réservé au checkout neuf, au changement
de verrou NuGet ou à l'absence réelle de dépendances.

## Studio incrémental historique

`Verify-StudioIncrement.ps1` reste disponible pour les documents d'acceptation existants. Il utilise
désormais `Route` par défaut, écrit un TRX par projet et refuse un `-TestFilter` global avec plusieurs
projets. Utiliser dans ce cas :

```powershell
-TestFilterByProject @{
  'Forge.Application.Tests' = 'FullyQualifiedName~ApplicationCase'
  'Forge.ModuleFormat.Tests' = 'FullyQualifiedName~FormatCase'
}
```

## Interpréter les preuves

- un test ciblé vert prouve son contrat, pas l'ensemble du dépôt ;
- un smoke `Route` ne vaut pas `Wave` ou `Full` ;
- un PNG prouve une capture, pas une revue humaine ;
- une distribution locale vérifiée n'est ni signée ni publiée ;
- un avertissement du plan doit être résolu par un périmètre plus large ou déclaré comme limite.
