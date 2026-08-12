# P1 R073 — Tests générés et génération IA de quête

## Périmètre

Lot canonique `R073 / FC-QUEST-P1-03`, limité à deux fonctions :

- `ORF-QST-024` — tests générés ;
- `ORF-QST-025` — génération IA de quête.

Les dépendances déclarées `AI` et `QA` réutilisent le workflow assistant gouverné, la validation de
quête, les commandes transactionnelles et le runtime existants. Le lot n'ouvre ni P2, ni AV7–AV10,
ni R074.

## Audit source avant modification

Les blocs complets `ORF-QST-024` et `ORF-QST-025`, lignes 1808 à 1959 de
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/08_QUEST.md`, ont été relus avant
modification.

| Critère source | État avant R073 | Limite réelle fermée par R073 |
|---|---|---|
| Cinq chemins de test | `QuestQualityService` générait cinq scénarios dans le RPG Kit isolé | profil attaché à la quête principale avec UUID, schéma, provenance, ordre, seed, fixtures typées, nœuds, arêtes et assertions |
| Runner nominal et échec | runner isolé sans document de module | exécution déterministe du graphe canonique, état final et échecs localisés par assertion/path |
| Proposition IA | assistant déjà borné, linté et applicable après dry-run | plan persistant avec synopsis localisé, états, participants, dépendances typées, citations, style et lint |
| Gouvernance IA | handler existant avec approbation et permission | `planId`, `transactionId`, diff sémantique, niveau d'autorisation, validateur humain daté et application unique explicités dans le document |
| Surfaces produit | aucune édition principale, persistance qualité, ForgeScript, MCP ni runtime moteur | onglet transactionnel, round-trip workspace, lecture typée, ressource filtrée/dry-run et vrais nœuds moteur |

## Matrice d'acceptation

| Critère commun immuable | Preuve R073 |
|---|---|
| Domaine canonique avant moteur | `QuestQualityProfile`, `QuestQualityRuntimeService` et le validateur sont dans `Forge.Domain` sans dépendance moteur |
| Identité et déterminisme | tous profils, scénarios, fixtures, étapes, arêtes, assertions, dépendances et lint portent UUID, schéma, provenance et ordre |
| Validation structurée | codes stables pour identité, scénarios, graphes, assertions, plan IA et références, avec chemins de document et UUID |
| Interface visible, vide et erreur | onglet `TESTS & GÉNÉRATION IA`, état vide, cinq scénarios, mini-graphe, synopsis, dépendances, citations, style, validation et simulation |
| UI et commande uniques | construction du profil puis `SetQuestHandler`, dry-run/apply/reject, undo/redo et conservation au renommage |
| ForgeScript typé | snapshot qualité, scénario et plan IA lus par `QuestJournalHandle` sûr |
| MCP filtré | tests et plan structurés, filtre sur label/fixture/action/synopsis/dépendance/citation/style et mutation complète en dry-run |
| Runtime moteur | racine `QuestQualityAdapters`, scénarios, fixtures, étapes, arêtes, assertions, plan et dépendances comme vrais nœuds |
| Optionnel/requis | absence optionnelle ignorée et profil requis désactivé diagnostiqué |

## Validation exécutée

- tests ciblés : Domain R073 4/4, Application ForgeScript R073 1/1, ModuleFormat R073 1/1 et MCP R073 1/1 ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_R073_QUEST_QUALITY_PASS` ;
- smoke runtime `OPENRPG_FORGE_RUNTIME_R073_QUEST_QUALITY_PASS` avec nœuds moteur réels ;
- capture moteur `OPENRPG_FORGE_R073_QUEST_QUALITY_UI_CAPTURE_PASS` ;
- capture `docs/aurora-parity/evidence/r073-quest-quality-ui.png`, 1450×910, inspectée : les cinq
  scénarios et seeds, le résumé 11 nœuds / 6 arêtes, le plan approuvé sans lint, synopsis,
  dépendances, citations, style, mini-graphe de quête, validation et simulation sont lisibles sans
  chevauchement global ;
- suite complète .NET : 1 523/1 523 tests passants sur les 11 projets de tests ;
- suite Python : 39/39 tests passants ;
- registres régénérés : 497 fonctions `Tested`, 219 `Implemented`, 1 `Researched`, avec 127 lots
  fermés, 59 à compléter/prouver et 1 à livrer ;
- graphe Roslyn frais et déterministe : 6 908 nœuds, 43 873 relations, 0 diagnostic ;
- portail intégré `scripts/Verify.ps1` : 27 projets construits sans avertissement ni erreur, suites
  .NET/Python, smokes Toolset et Runtime dont R073, gouvernance assets, Blender, exports, cartes,
  réseau, MCP et sauvegarde/reprise, puis marqueur terminal `OPENRPG_FORGE_VERIFICATION=PASS`.

## Décision finale

Les limites fonctionnelles sont fermées, les deux promotions `Implemented` vers `Tested` sont
justifiées individuellement et le portail final passe sur l'arbre documenté. Aucune autre fonction
n'a été promue par ce lot. `R074` peut être ouvert seulement après cette clôture.
