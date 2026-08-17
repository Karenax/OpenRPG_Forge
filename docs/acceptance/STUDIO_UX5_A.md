# Acceptation Studio — UX5-A Fondation de dialogues

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-DLG-001` à
`ORF-DLG-005`. La page **Dialogues · lignes et choix** propose un éditeur humain direct pour
créer ou modifier un dialogue, ses lignes et ses réponses. La mini-carte avancée reste un aperçu
strictement en lecture seule.

## Preuves communes

- portail incrémental `scripts/Verify-StudioIncrement.ps1` réussi sur `dialogues` /
  `ux5-a-dialogue-foundation` en **64,03 s**, avec résumé dans
  `artifacts/verification/studio-increment/ux5-a-dialogue-foundation.summary.json` ;
- tests ciblés : Domain 8/8, Application 7/7 et ModuleFormat 1/1, soit **16/16** ;
- smoke Studio headless réussi : sélection explicite et état vide, création d'un dialogue,
  locuteur protégé, sous-dialogue protégé, diff d'identité et diff structurel, nombre de branches
  non limité, entrées multiples, cycle `Reference` intentionnel accepté, cycle `Flow` accidentel
  refusé, cible absente et priorité négative refusées, dry-run sans mutation, doubles gardes,
  commande canonique, arêtes canoniques, undo/redo, round-trip, préservation et libération des
  ressources ;
- la transaction UI exerce réellement l'alternative clavier `Alt+↑` / `Alt+↓` et le payload de
  glisser-déposer UX5-A, via le même parseur et la même voie de dispatch que `_DropData`, sans
  remplacer la preuve par un appel direct à la fonction de déplacement ;
- capture Forward+ FHD inspectée dans
  `artifacts/verification/studio-increment/ux5-a-dialogue-foundation-forward-plus-fhd.png`, puis
  captures QHD et 4K dans
  `artifacts/verification/studio-shell/ux2-5-milestone/ux5-a-qhd.png` et
  `artifacts/verification/studio-shell/ux2-5-milestone/ux5-a-4k.png` : inventaire, éditeur de
  réponse, mini-carte, diagnostics, aperçu et actions transactionnelles restent lisibles sans
  clipping bloquant ;
- le profil incrémental n'a demandé ni graphe ni portail complet (`graph_check=false`,
  `full_gate=false`). Le jalon intégré UX2/UX4/UX5 a ensuite réussi `scripts/Verify.ps1` : 1 988
  tests .NET, 39 tests Python, builds sans avertissement, graphe Roslyn sans diagnostic, puis
  smokes Studio, Toolset, Runtime, Forward+, MCP, réseau, sauvegarde et distribution.

### ORF-DLG-001

La surface ouvre un document existant seulement après sélection humaine explicite, ou initialise
un nouveau dialogue en mémoire. Nom, slug, entrée, lignes et réponses alimentent un aperçu lisible
avant l'application par `SetDialogueHandler`. Le diff couvre aussi le dialogue neuf et les champs
d'identité ; apply, undo/redo et sérialisation vérifient ensuite le round-trip canonique.

### ORF-DLG-002

Les rôles locuteur, choix joueur, narration, système, action, sous-dialogue et fin sont projetés en
libellés humains. Les locuteurs valides de type non directement éditable restent sélectionnables
sous un libellé protégé, sans exposer leur UUID ni retomber silencieusement en narration. Un nœud
`Subgraph` existant conserve son rôle et sa clé dans un état hérité protégé.

### ORF-DLG-003

Le graphe complet reste chargé et validé sans plafond de branches. La pagination de 25 éléments ne
concerne que l'inventaire visuel des lignes : elle ne tronque ni les lignes, ni les réponses, ni les
arêtes. La mini-carte avancée représente toutes les entrées et connexions, mais ne devient pas un
second éditeur concurrent.

### ORF-DLG-004

Une réponse peut cibler un nœud existant par une arête `Flow` ou par une `Reference` intentionnelle.
Le validateur accepte le retour cyclique explicitement marqué `Reference`, refuse le cycle `Flow`
accidentel et diagnostique les cibles absentes. Le smoke couvre aussi plusieurs entrées et le refus
d'une priorité négative.

### ORF-DLG-005

Chaque réponse se réordonne par les boutons **Monter/Descendre**, leur alternative clavier
`Alt+↑` / `Alt+↓`, ou le payload de glisser-déposer. Le diff décrit le changement d'ordre sans faux
positif de comparaison de listes. Après ajout, suppression ou déplacement, les arêtes canoniques
sont reconstruites depuis l'ordre, le type et la priorité des réponses ; les autres données du
dialogue restent préservées.

## Limites honnêtes

- la mini-carte est une aide de lecture avancée, pas un éditeur de graphe par manipulation directe ;
- la pagination à 25 porte uniquement sur l'inventaire des lignes et ne constitue aucune limite de
  taille du dialogue ;
- la preuve glisser-déposer traverse un vrai payload UX5-A et la voie de dispatch de `_DropData`,
  mais le smoke headless ne dépend pas de la géométrie d'un geste de pointeur du système ;
- les locuteurs hors types éditables et les sous-dialogues existants sont conservés sous forme
  protégée ; cette surface ne modifie pas leurs contrats avancés ;
- conditions, actions, variantes, profils avancés, traductions et champs hors surface sont
  préservés structurellement ; ils ne sont pas revendiqués comme édités par UX5-A ;
- l'aperçu ne simule ni acteurs, ni voix, ni caméra, ni scène runtime, ni réseau, ni Live Director ;
- la preuve apply/undo/redo et la capture utilisent une session et un workspace temporaires isolés.
  Projet, dirty state, révision, journal, piles undo/redo, sélection et approbation de la session
  source restent inchangés, puis les ressources temporaires sont nettoyées.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~DialogueValidatorTests|' +
  'FullyQualifiedName~DialogueGraphStructureTests|' +
  'FullyQualifiedName~SetDialogueHandlerTests|' +
  'FullyQualifiedName~AggregateAndWorkspaceRoundTripsPreserveCanonicalDialogueGraph'
& .\scripts\Verify-StudioIncrement.ps1 -Page dialogues -Evidence ux5-a-dialogue-foundation `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux5-a-dialogue-foundation-forward-plus-fhd.png

& .\scripts\Open-Studio.ps1 -Page dialogues -Evidence ux5-a-dialogue-foundation `
  -Resolution 2560x1440 `
  -Capture artifacts/verification/studio-shell/ux2-5-milestone/ux5-a-qhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page dialogues -Evidence ux5-a-dialogue-foundation `
  -Resolution 3840x2160 `
  -Capture artifacts/verification/studio-shell/ux2-5-milestone/ux5-a-4k.png -Wait
```
