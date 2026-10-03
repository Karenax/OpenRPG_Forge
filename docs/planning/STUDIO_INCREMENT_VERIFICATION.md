# Validation incrémentale de Forge Studio

`scripts/Verify.ps1` reste le portail complet et la preuve finale de non-régression. Il ne doit pas
être remplacé par une validation rapide. Pour les lots Studio courants, utiliser
`scripts/Verify-StudioIncrement.ps1` afin de limiter la boucle au projet, aux tests et à la page
réellement touchés.

## Boucle courante

La boucle incrémentale comprend, dans l’ordre :

1. build Debug de `Forge.Studio` avec les sorties NuGet déjà restaurées ;
2. tests des projets explicitement concernés, exécutés séquentiellement ;
3. smoke Studio headless de la route du lot (`Route` par défaut, `Wave` au jalon de vague) ;
4. une capture Forward+ Full HD lorsque la présentation change ;
5. contrôle de fraîcheur de la matrice Studio ;
6. chronométrage par étape dans `artifacts/verification/studio-increment/`.

Exemple pour un lot de placeable :

```powershell
& .\scripts\Verify-StudioIncrement.ps1 `
  -Page ux1-s `
  -Evidence ux1-s-increment `
  -TestProject @(
    'tests\Forge.Application.Tests\Forge.Application.Tests.csproj'
    'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
    'tests\Forge.Studio.Core.Tests\Forge.Studio.Core.Tests.csproj'
  ) `
  -TestFilterByProject @{
    'Forge.Application.Tests' = 'FullyQualifiedName~WorldFeature|FullyQualifiedName~Placeable'
    'Forge.ModuleFormat.Tests' = 'FullyQualifiedName~WorldFeature|FullyQualifiedName~Placeable'
    'Forge.Studio.Core.Tests' = 'FullyQualifiedName~Studio'
  } `
  -Capture 'artifacts\verification\studio-shell\ux1-s\increment-fhd.png'
```

`-Restore` est réservé à une première exécution, à un changement de projet/verrou NuGet ou à un
checkout neuf. `-CheckGraph` est réservé aux jalons qui changent la topologie du code ou des preuves.
Le script ne lance jamais `Verify.ps1` et ne promeut aucune route dans la matrice.

## Jalon complet

Lancer le portail complet après trois à cinq lots liés, après dix à vingt routes, à la fin d’une
vague UX, ou immédiatement après une modification de contrat partagé : shell, sérialisation,
runtime, moteur, assets/provenance, MCP/sécurité, réseau, build ou distribution.

```powershell
& .\scripts\Verify.ps1
```

Une route peut être préparée et testée dans la boucle incrémentale, mais elle ne doit être déclarée
`functional` qu’après la preuve spécifique et le dernier jalon complet applicable. Les captures QHD
et 4K restent obligatoires pour les jalons de layout, viewport, densité ou changement de résolution ;
elles ne sont pas répétées à chaque correction interne d’un même lot.

## Limites de sécurité

- ne pas paralléliser les builds ou les hôtes VSTest sur cette machine ; le portail impose déjà un
  plafond de parallélisme pour éviter la saturation mémoire ;
- ne pas réutiliser un smoke Godot si le build Debug, les scripts, les assets importés ou les paramètres
  de scène ont changé ;
- si un test ciblé échoue, interrompre la validation, diagnostiquer et corriger dans le périmètre
  autorisé, puis rejouer les contrôles affectés ; ne pas valider ni regrouper le lot avec le suivant
  avant résolution. Pour une demande de diagnostic seul, rapporter sans modifier ;
- ne jamais partager un filtre entre plusieurs projets : utiliser `-TestFilterByProject`, et traiter
  zéro test exécuté comme un échec de sélection ;
- si l’impact d’un fichier ne peut pas être déterminé, élargir la liste des projets de test ou lancer
  le portail complet.

## Coordination parallèle sûre

La parallélisation concerne la préparation et l'implémentation, jamais les validations exécutables
sur une même machine. Elle exige une baseline locale validée et commitée, puis un worktree distinct
par writer :

1. le coordinateur attribue à chaque writer une page, une à cinq routes, une branche locale et une
   liste de fichiers exclusive ;
2. le coordinateur conserve seul `StudioWorkspaceShell.cs`, `StudioRoot.cs`, `Open-Studio.ps1`, la
   solution, les matrices, les preuves, les documents d'état et le graphe généré ;
3. les writers n'exécutent ni build, ni test, ni smoke, ni capture pendant les mutations parallèles ;
4. chaque writer termine par un commit local centré sur son lot et signale `READY` ;
5. le coordinateur intègre les commits un par un, résout uniquement les hooks centraux, puis gèle
   toutes les sources ;
6. le build Debug, les tests ciblés, le smoke, la capture FHD et le contrôle de matrice s'exécutent
   ensuite dans cet ordre et sans autre écriture concurrente ;
7. toute modification après le build invalide `-SkipBuild` et impose de recommencer la validation du
   lot concerné.

Un troisième slot peut auditer le prochain lot en lecture seule. Il ne devient writer qu'après
attribution d'un worktree et d'une frontière de fichiers qui ne chevauche aucun lot actif.
