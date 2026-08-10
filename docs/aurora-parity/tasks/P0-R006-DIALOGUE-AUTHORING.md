# P0 R006 — localisation, conditions, actions, simulation et linter de dialogue

Lot stable : `R006` / `FC-DIALOGUE-P0-02` — cinq fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-DLG-006` — Texte localisé.
- `ORF-DLG-009` — Conditions d’apparition.
- `ORF-DLG-011` — Actions sur nœud.
- `ORF-DLG-021` — Mode test.
- `ORF-DLG-029` — Validation structurelle.
- Source immuable relue intégralement pour ces cinq sections :
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/07_DIALOGUE.md`.
- Dépendances réévaluées : QA et build sont fermés par R001–R003. CONTENT fournit déjà le catalogue,
  le fallback et la pseudo-localisation ; QUEST, ITEM, AUDIO et SCRIPT exposent les identifiants et
  services nécessaires. Le lot n’ouvre aucun éditeur de ces domaines.

## Audit avant implémentation

| Fonction | Présent | Limite réelle à fermer |
|---|---|---|
| DLG-006 | `LocalizedText` contient clé et fallback ; catalogue multilingue et pseudo-localisation existent | aucune variante canonique langue/genre/pluriel, identité/provenance ou validation dans le dialogue |
| DLG-009 | conditions typées variable/item/quête et évaluation runtime | AND implicite seulement, pas de OR/NOT, identité/schema/binding, explication ni édition multiple |
| DLG-011 | listes ordonnées sur nœud et choix, six actions élémentaires | relations, animation, audio, séquence et script absents ; aucune transaction sandboxée ni édition visible |
| DLG-021 | parcours runtime et exploration de couverture QA | pas d’état fictif auteur, pas de sélection réellement simulée ni journal de décisions |
| DLG-029 | entrée, cibles, accessibilité, cycles, rôles et arêtes validés | fins, contradictions, localisation, identités et types de conditions/actions non contrôlés ; panneau absent |

## Contrat minimal de fermeture

- enrichir `LocalizedText`, `DialogueCondition`, `DialogueAction` et `DialogueChoice` sans créer un
  second modèle concurrent et avec migration déterministe des documents historiques ;
- permettre les variantes langue/genre/pluriel avec fallback et pseudo-localisation dans l’écran Dialogue ;
- permettre plusieurs prédicats combinés par `ALL` ou `ANY`, chaque prédicat pouvant être inversé ;
- couvrir les familles d’actions requises et les appliquer atomiquement à une copie sandboxée ;
- exécuter une conversation contre un état fictif borné et rendre chaque décision, action et état final ;
- étendre le linter avec codes/chemins stables pour fins, contradictions, localisation, conditions et actions ;
- conserver `SetDialogueCommand`, son dry-run, son patch/inverse et l’historique projet comme unique mutation ;
- enrichir les lectures ForgeScript/MCP sans exposer d’accès aux fichiers ni aux nœuds moteur ;
- conserver le runtime optionnel et produire un diagnostic explicite lorsque le dialogue requis est invalide.

## Matrice des 30 critères d’acceptation

| Fonction | Configuration sans généré | Save/close/reload | Invalide localisé | UI = commande | Undo/redo | Headless nominal/échec |
|---|---|---|---|---|---|---|
| DLG-006 | éditeur de variantes | JSON exact | clé, locale, dimension, UUID | `SetDialogueCommand` | historique projet | résolution/fallback + variante invalide |
| DLG-009 | liste + ALL/ANY/NOT | JSON exact | type, binding, schéma, contradiction | même commande | historique projet | expression vraie/fausse |
| DLG-011 | listes nœud/choix | JSON exact | type, UUID, cible et valeur | même commande | historique projet | transaction commit/rollback |
| DLG-021 | état fictif + bouton | configuration du dialogue inchangée | état/limite/branche expliqués | lecture du brouillon canonique | mutations du dialogue annulables | parcours nominal + blocage |
| DLG-029 | panneau de validation | diagnostics déterministes après reload | codes et chemins stables | validation de la même commande | restauration revalidée | graphe valide + chaque famille d’échec |

## Risques et tests exacts

- compatibilité JSON : tests de roundtrip historique et enrichi dans `Forge.ModuleFormat.Tests` ;
- sémantique : tests Domain des variantes, ALL/ANY/NOT, contradictions, fins et identités ;
- atomicité et simulation : tests Application avec état avant/après et journal déterministe ;
- commande/historique : dry-run, apply, undo et redo dans `Forge.Application.Tests` ;
- lecture agent/script : tests MCP filtrés/bornés et handle typé ;
- interface : smoke Toolset headless et capture 1440×950 inspectée ;
- intégration : marqueurs Toolset/Runtime, documentation, tracker, backlog, graphe puis `scripts/Verify.ps1`.

## Non-objectifs

- tokens DLG-007, ordre scripté DLG-010, assistant DLG-012, mémoire DLG-022 et couverture DLG-030 ;
- éditeur général de localisation CONTENT, éditeur de quête/item/audio ou compilateur ForgeScript ;
- conditions arbitraires exécutant du code non borné ;
- P1/P2, AV7–AV10, publication Git ou nouvelle voie de mutation.

## Résultat livré

- texte localisé canonique v1 avec UUID, provenance, variantes ordonnées langue/genre/pluriel,
  résolution par langue puis fallback et aperçu pseudo-localisé en mémoire ;
- conditions identifiées et versionnées avec binding/API, listes `ALL` ou `ANY`, négation par prédicat,
  explication structurée et même évaluation dans le parcours, la QA et le simulateur ;
- actions identifiées, versionnées et requises/optionnelles couvrant variables, quêtes, items, relations,
  animation, audio, séquence et script, appliquées atomiquement à une copie sandboxée ;
- simulateur borné avec état fictif, choix scriptés ou déterministes, rollback, journal de décisions,
  actions appliquées, état final et diagnostics ;
- linter étendu aux fins accessibles, identités dupliquées, localisation, types, bindings, API,
  contradictions et actions, avec correction du parcours d’accessibilité en présence de liens entrants multiples ;
- éditeur Dialogue 1440×950 avec colonne défilable, variantes, actions de nœud/choix, conditions multiples,
  état fictif, journal, validation, pseudo-localisation et boutons undo/redo ;
- lecture ForgeScript enrichie, ressource et plan MCP enrichis, mutation toujours concentrée dans
  `SetDialogueCommand` avec dry-run, patch/inverse et historique projet.

## Preuves exécutées

- Domain dialogue : 27/27 ; Application dialogue : 14/14 ; ModuleFormat dialogue : 6/6 ;
  MCP dialogue : 7/7 ; transport stdio officiel : 1/1 ;
- roundtrip JSON agrégé et workspace exact des variantes, négations, combinaisons et actions ;
- transaction commit/rollback, simulation nominale/choix indisponible et journal de décisions testés ;
- linter testé pour entrée, cible, orphelin, cycle, fin, type, référence, contradiction, localisation,
  UUID et régression des liens entrants multiples ;
- capture visible inspectée : `docs/aurora-parity/evidence/r006-dialogue-authoring-ui.png` (1440×950),
  simulation terminée sur trois nœuds et panneau de validation vert ;
- marqueurs `OPENRPG_FORGE_TOOLSET_R006_DIALOGUE_PASS` et
  `OPENRPG_FORGE_RUNTIME_R006_DIALOGUE_PASS` intégrés à `scripts/Verify.ps1` ;
- portail pré-promotion : 1 045 tests .NET, 6 tests Python généraux, 33 tests pipeline assets,
  documentation/tracker/backlog/graphe frais, smokes Toolset/Runtime/MCP, modules et prototypes 3D verts,
  `OPENRPG_FORGE_VERIFICATION=PASS` en 248 secondes.
- promotion limitée aux cinq IDs R006 : 236 fonctions `Tested`, 162 P0 non closes et 44 lots P0
  ouverts ; aucune ligne P1/P2 ou AV7–AV10 modifiée.
