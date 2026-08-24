# Acceptation Studio — UX7-F Historique et récupération

État local au 21 août 2026 : **fonctionnel et qualifié** pour cinq routes UX7.
UX7 atteint ainsi **36/43** routes accessibles dans Studio.

La page `history` expose un workbench plein écran à sélection explicite du module. Chaque run crée une
copie temporaire du workspace, exerce cinq parcours de sauvegarde et de récupération, rend leurs
résultats navigables, puis supprime l'isolat. Le projet ouvert, son historique, sa sélection et ses
approbations restent inchangés.

## Routes couvertes

<a id="orf-shell-024"></a>
- **ORF-SHELL-024 — Sauvegarde automatique et récupération** : `RecoverySessionStore` détecte une
  session interrompue, `RecoverySnapshotStore` capture un snapshot versionné et compare la copie avec
  un brouillon. `RestoreRecoverySnapshotHandler` prouve dry-run, restauration sélective, undo et redo.
<a id="orf-game-020"></a>
- **ORF-GAME-020 — Sauvegarde et chargement** : `RuntimeSaveArchiveStore` écrit deux révisions,
  retrouve la plus récente, restaure la précédente et refuse une charge dont le contenu diverge.
<a id="orf-game-021"></a>
- **ORF-GAME-021 — Sauvegarde joueur/partie/serveur** : `GameplaySessionServices` exerce les portées
  `Player`, `Game` et `Server`, puis refuse autorité, confidentialité et révision incompatibles.
<a id="orf-game-025"></a>
- **ORF-GAME-025 — Checkpoints et respawn** : le service canonique résout checkpoint, coût, groupe,
  politique de cadavre et hooks, puis prouve quatre refus localisés d'autorité, sécurité, coût et cible.
<a id="orf-qa-028"></a>
- **ORF-QA-028 — Crash recovery** : un snapshot altéré est détecté par SHA-256, mis en quarantaine et
  retiré du journal réparé, tandis qu'un snapshot sain reste restaurable.

## Isolation, annulation et navigation

- La sélection est liée à une empreinte SHA-256 du projet sérialisé et du workspace source.
- L'annulation est exercée après le premier parcours ; les quatre suivants ne sont pas produits et la
  copie temporaire est libérée.
- Deux runs complets rendent la même empreinte fonctionnelle.
- Projet, historique, révision, journal, dirty, undo/redo, zone active, sélection, fichier source et
  approbations restent identiques.
- Toutes les sauvegardes, sessions, snapshots, corruptions et quarantaines sont créés sous la racine
  temporaire ; aucun artefact n'est écrit dans le module ouvert.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **24/24** — Domain 7, Application 9, ModuleFormat 8 ;
- smoke Godot global : marker `OPENRPG_FORGE_STUDIO_UX7_F_PASS` avec tous les sous-invariants à `true` ;
- approbations candidates, evidence et shell global : PASS ;
- portail incrémental : **294,03 s** — build 1,17 s, tests 1,49/2,52/4,00 s,
  smoke 272,98 s, capture 10,93 s et matrice 0,88 s ;
- matrice avant promotion : **717 lignes**, **36 pages**, 569 fonctionnelles / 148 planifiées, PASS ;
- capture Forward+ **1920×1080** sur Crypte des Cendres réel : cinq cartes, cinq résultats, détail de
  crash recovery, frontière, statut et actions entièrement lisibles, sans clipping.

Artefacts :

- `artifacts/verification/studio-increment/ux7-f-history-recovery.summary.json` ;
- `artifacts/verification/studio-increment/ux7-f-history-recovery-forward-plus-fhd.png` ;
- `artifacts/verification/studio-shell/studio-interactive.log`.

## Limites honnêtes

Le workbench prouve les contrats de sauvegarde et de récupération dans une copie locale. Il ne connecte
aucun cloud, serveur, socket ou pair distant. Il ne recharge pas un monde moteur, ne déplace aucun acteur
et ne remplace jamais le projet source par le snapshot de preuve. Les portées et checkpoints qualifient
les décisions canoniques, pas un transport multijoueur ou un écran runtime réellement joué. Le portail a
`full_gate=false`, `graph_check=false` et ne revendique ni `Verify.ps1` global ni contrôle du graphe.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Domain.Tests\Forge.Domain.Tests.csproj'
  'tests\Forge.Application.Tests\Forge.Application.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~OperationalReadinessProfileTests|' +
  'FullyQualifiedName~GameplaySessionProfileTests|' +
  'FullyQualifiedName~RestoreRecoverySnapshotHandlerTests|' +
  'FullyQualifiedName~GameplaySessionServicesTests|' +
  'FullyQualifiedName~GameplaySessionScriptServiceTests|' +
  'FullyQualifiedName~RecoverySnapshotStoreTests|' +
  'FullyQualifiedName~RuntimeSaveArchiveStoreTests|' +
  'FullyQualifiedName~GameplayControlPersistenceTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page history `
  -Evidence ux7-f-history-recovery `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux7-f-history-recovery-forward-plus-fhd.png'
```
