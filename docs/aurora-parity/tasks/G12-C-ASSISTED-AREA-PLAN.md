# G12-C — Plan de zone assisté

## Portée

- exigence : `ORF-AI-014` ;
- proposer une topologie de crypte ou de village avant toute géométrie ;
- sélectionner zone, kits, contraintes, objectifs et sources de lore ;
- partager les mêmes contrats entre Core, interface Toolset et MCP.

## Résultat livré

Le plan est un document canonique indépendant de la scène 3D. Il porte UUID et schéma stables,
zone cible, archétype, seed, dimensions, blueprints de kit, nœuds de contenu, connexions, contraintes,
objectifs, sources, approbation, coût, provider et provenance. Le générateur hors ligne produit une
topologie reproductible et connectée ; crypte et village emploient des dispositions distinctes.

L’interface Aurora **Plans de zone** affiche le brief, la topologie, le diff, le coût et la preuve
`géométrie appliquée : NON`. Enregistrer utilise une commande avec révision, transaction, plan,
dry-run, validation et permission. L’approbation pour construction requiert le niveau
`ApplyApprovedPlan`. Undo/redo restaure le document canonique et la sauvegarde atomique écrit
`assistant/area-plans.json`.

MCP expose lecture, proposition hors ligne, planification, application du document et revert. La
proposition utilise le provider factice déterministe en CI et ne dépend d’aucun réseau.

## Fichiers autorisés

- `src/Forge.Domain/Assistant/AssistedArea*` ;
- `src/Forge.Application/Assistant/AssistedArea*` et `SetAssistedAreaPlans*` ;
- sérialiseur/store du document ;
- fenêtre Toolset, branchement plugin, contrats et outils MCP ;
- tests, tracker, documentation générée, graphe et gate.

## Non-objectifs

- aucune création, suppression ou transformation d’entité ;
- aucune construction de terrain, navmesh, lumière ou géométrie ;
- aucun Python libre ni modèle cloud obligatoire ;
- `ORF-AI-015` reste le périmètre de G12-D.

## Preuves

- tests Domain : schéma borné et connectivité ;
- tests Application : crypte/village reproductibles, provider offline, coût, garde dry-run,
  permissions et historique ;
- tests ModuleFormat : absence sûre, round-trip exact et diagnostic stable ;
- test MCP : propose/plan/apply/revert sans changement du nombre d’entités ;
- smoke Toolset : `OPENRPG_FORGE_TOOLSET_G12C_AREA_PLAN_PASS` ;
- `scripts/Verify.ps1` protège le marqueur dans la porte intégrée.
