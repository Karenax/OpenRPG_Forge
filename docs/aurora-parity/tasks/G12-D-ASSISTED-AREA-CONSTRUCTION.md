# G12-D — Construction de zone assistée

## Portée

- exigence : `ORF-AI-015` ;
- construire un plan de crypte ou de village explicitement approuvé ;
- réutiliser les commandes de placement et les validations du workflow manuel ;
- partager le même contrat transactionnel entre Core, interface Toolset et MCP.

## Résultat livré

La construction transforme le contenu d’un `AssistedAreaPlanDefinition` approuvé en un lot de
commandes `PlaceEntityCommand`. Les UUID d’entités et de commandes sont dérivés de façon stable du
plan, de ses nœuds et de ses blueprints. Chaque instance conserve l’identifiant du plan, du nœud et
la seed dans ses tags de provenance.

Avant application, le handler reconstruit le candidat en mémoire, valide la géométrie, interdit une
régression de navigation liée aux nouveaux objets et contrôle qu’un éclairage utilisable reste
actif. Le dry-run enregistre l’empreinte exacte de la révision, de la transaction et du résultat. Une
application ne peut donc ni contourner la prévisualisation, ni construire un plan modifié entre les
deux étapes.

L’ensemble des placements est ramené à une seule mutation de projet avec diff, inverse, audit et
révision. Undo/redo restaure tout le lot atomiquement. La fenêtre **Plans de zone** propose désormais
**Prévisualiser construction** puis **Construire le plan**. La sauvegarde du projet précède
l’enregistrement dans l’historique. MCP expose le même cycle plan/apply/revert et persiste le même
projet canonique.

## Fichiers autorisés

- `src/Forge.Application/Assistant/BuildAssistedAreaPlan*` et résultat associé ;
- fenêtre Toolset et branchement du plugin ;
- contrats, service et outils MCP ;
- tests, tracker, documentation générée, graphe et gate.

## Non-objectifs

- aucune exécution libre de code ou de Python ;
- aucune modification d’un plan non approuvé ;
- aucune génération arbitraire de mesh, de terrain ou de navmesh par un provider ;
- la création de dialogue assistée reste le périmètre de G12-E (`ORF-AI-016`).

## Preuves

- tests Application : dry-run exact, seed et IDs stables, approbation, permission, prévention du
  doublon, lot atomique et undo/redo ;
- test MCP : catalogue approuvé, planification, application persistée et revert du lot complet ;
- smoke Toolset : `OPENRPG_FORGE_TOOLSET_G12D_AREA_BUILD_PASS` ;
- `scripts/Verify.ps1` protège le marqueur dans la porte intégrée.
