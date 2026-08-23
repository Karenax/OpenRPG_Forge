# Acceptation Forge Studio — UX6-U

Date de qualification : 21 août 2026.

UX6-U rend `ORF-GAME-022` directement accessible sur la page Runtime par la surface
**Variables & blackboard**. Elle complète l’éditeur de schéma UX0-I sans le dupliquer : création,
renommage, type et portée restent dans `Projet > Variables`, tandis qu’UX6-U traite la définition
sélectionnée et les comportements runtime du blackboard.

## ORF-GAME-022

### Définition persistante

- sélection explicite d’une variable par nom humain, type et portée, sans UUID visible ;
- nom, clé, type, portée, description, provenance et identité protégés dans cette surface ;
- valeur par défaut typée, réplication, visibilité debug et exigence runtime éditables ;
- validation par `ProjectVariableValidator`, aperçu `DryRun` et application par
  `SetModuleConfigurationHandler` ;
- garde sur révision, empreinte SHA-256 du projet et empreinte binaire préfixée du brouillon ;
- undo, redo, sauvegarde, réouverture et préservation des variables et agrégats voisins.

### Blackboard runtime copié

L’aperçu construit `ProjectBlackboardScriptService.ForRuntime` depuis le candidat et exerce :

- requête et lecture par handle typé ;
- mutation normalisée ou refus d’une valeur incompatible ;
- refus d’un handle périmé ;
- snapshot debug avec décompte des variables protégées ;
- delta filtré pour propriétaire, groupe, client ou serveur ;
- snapshot par portée puis rechargement dans une nouvelle instance ;
- frontière stricte des modules déclarés et compatibilité legacy limitée aux modules sans schéma.

Ce blackboard est une copie en mémoire. Aucun serveur, client, `GameRoot`, transport réseau,
ForgeScript ou fichier de sauvegarde actif n’est exécuté par Studio.

## Transaction et préservation

La preuve UI ouvre un workspace temporaire dérivé du module réel et y ajoute uniquement cinq
variables de démonstration. Preview, apply, undo, redo et save s’exécutent dans cet isolat. Projet,
historique, révision, journal, état dirty, zone, sélection et approbations de la session source sont
comparés puis restaurés ; le répertoire temporaire doit être libéré pour que le smoke passe.

La commande remplace uniquement `Module.Variables`. Métadonnées, distribution, localisation,
contenu, style artistique, politiques runtime, factions, fonctions monde, blueprints, zones,
dialogues et quêtes restent identiques au candidat attendu.

## Validation obtenue

- build Studio Debug : **0 avertissement, 0 erreur** ;
- tests ciblés : **56/56** — Domain 3, Application 8, ModuleFormat 45 ;
- smoke global : `OPENRPG_FORGE_STUDIO_UX6_U_PASS`, avec sélection explicite, invalidités typée et
  schéma, dry-run, mutation, debug, réplication, snapshot/reload, handle périmé, frontière legacy,
  révision et approbation obsolètes, commande canonique, undo/redo, round-trip, voisins, texte
  humain, transaction UI, libération des ressources, source inchangée et surface restaurée ;
- portail incrémental : **144,09 s**, build, tests, smoke global, capture Forward+ FHD et contrôle
  des 717 lignes de matrice ; `full_gate=false` reste explicite ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-u-blackboard.summary.json` ;
- capture Forward+ 1920×1080 inspectée :
  `artifacts/verification/studio-increment/ux6-u-blackboard-forward-plus-fhd.png`.

La sélection, les deux onglets, le résultat runtime, le diff humain et les cinq actions
transactionnelles sont visibles et lisibles sans clipping bloquant.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~ProjectVariableGovernanceTests|' +
  'FullyQualifiedName~ProjectBlackboardScriptServiceTests|' +
  'FullyQualifiedName~SetModuleConfigurationHandlerTests|' +
  'FullyQualifiedName~ProjectJsonSerializerTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page runtime `
  -Evidence ux6-u-blackboard -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux6-u-blackboard-forward-plus-fhd.png
```
