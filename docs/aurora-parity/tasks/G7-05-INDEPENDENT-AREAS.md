# G7-05 — Zones indépendantes et transitions

Date d’exécution : 2 août 2026.

## Capacités bornées

- `ORF-AREA-001` — assistant de création de zone ;
- `ORF-AREA-002` — document de zone indépendant ;
- `ORF-AREA-030` — transitions de zone ;
- `ORF-AREA-037` — validation de zone.

## Résultat livré

Le domaine de zone porte maintenant une version de schéma, un environnement intérieur/extérieur,
des dimensions éditables et des `PortalLink` typés. Une transition référence une entité d’ancrage
source, une zone et une entité d’arrivée, avec options bidirectionnelle et préchargement. Le
validateur produit des codes stables et des chemins localisés lorsque l’une de ces références est
absente ou dupliquée.

Les mutations passent par `create_area` et `upsert_portal_link`. Les deux handlers contrôlent la
révision, prennent en charge le dry-run, produisent patch, inverse et audit, et s’intègrent à
l’historique partagé. Le contexte `ActiveAreaContext` permet à l’explorateur, aux contrôles de
placement et au viewport de travailler sur la même zone sans prendre arbitrairement la première du
projet.

Le workspace auteur passe au format 0.7 : `project.json` devient un index et chaque zone est écrite
dans `areas/<uuid>-<hash>.json` au format 1.0. Les anciens agrégats 0.1 à 0.6 sont toujours lus et
migrés à la sauvegarde. L’export `.orpmod` conserve la séparation, puis son validateur reconstruit
l’agrégat de domaine.

La fenêtre « Zones & transitions » reprend la direction sombre/braise du Creator. Elle affiche le
monde du module, met en évidence la zone active, crée une zone vide ou jouable, configure dimensions
et environnement, relie les ancres et expose immédiatement les diagnostics de zone.

## Preuves

- `tests/Forge.Application.Tests/Areas/AreaWorkflowTests.cs` couvre dry-run, création, refus,
  transition, diagnostics et undo/redo ;
- `tests/Forge.ModuleFormat.Tests/Serialization/AreaJsonSerializerTests.cs` couvre le round-trip du
  document de zone ;
- `tests/Forge.ModuleFormat.Tests/Workspaces/ProjectSessionTests.cs` couvre séparation, sauvegarde,
  réouverture, migration legacy et document manquant ;
- `OPENRPG_FORGE_TOOLSET_G705_AREA_PASS areas=2 documents=2 transition=valid active=switched history=restored`
  prouve le workflow intégré dans l’éditeur headless ;
- 151 tests .NET sont verts avant la vérification globale.

## Limites conservées

- le runtime ne déclenche pas encore les `PortalLink` ; les portes, triggers, orientation, hooks et
  streaming seront raccordés dans un incrément RPG/runtime ultérieur ;
- l’environnement ne porte pas encore terrain, ciel, météo, éclairage, audio ou navigation ;
- la validation géométrique reste limitée aux dimensions, à l’occupation simple et aux références,
  sans raccords, navmesh, collision de forme, budgets ni profils de qualité ;
- la bidirectionnalité est une propriété canonique, pas encore la génération automatique d’un lien
  inverse ;
- aucune capture ne transforme ces capacités partielles en statut `Tested` complet dans le tracker.

Ces limites conservent les quatre capacités au statut `Implemented` partiel.
