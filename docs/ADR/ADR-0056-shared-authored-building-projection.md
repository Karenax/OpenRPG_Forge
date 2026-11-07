# ADR-0056 — Projection des bâtiments auteur dans le moteur commun

Date : 27 octobre 2026. Statut : transfert qualifié localement, parcours produit restant dû.

## Décision

`forge_engine::authoring_buildings` possède la projection dérivée des pièces
canoniques : meshes, poses, matériaux, identité des parties, réconciliation,
aperçus, colliders Rapier et diagnostics. Le document auteur et le générateur
de disposition existants conservent leurs propriétaires et leur autorité.
Studio et jeu de modules consomment directement cette même projection moteur.
La crate de carte ne conserve ni copie ni réexport.

Les calculs et assertions existants sont conservés. Le test d'intégration du
contrôleur capsule traversant les portes, puis bloqué par les murs, accompagne
la projection dans le moteur. Il conserve ses quatre orientations et ses
conditions de stabilité au sol. Les tests de réconciliation et de transition
de matériau gardent leurs vérifications des meshes, poses et handles physiques.

La boucle existante de collecte des casters explicitement marqués rejoint
`source_shadows::casters::scene::append_marked_parts`. Elle garde tri, rejet d'un
mesh absent, suivi de hiérarchie et absence de doublons. L'adapter de carte
collecte ses familles propres puis appelle cette boucle commune. Les tests
de bâtiments utilisent le même collecteur moteur, sans dépendre de ces familles.

## Vérification et limites

Les sources 020 identifient le transfert à partir des 019 ; les originaux sont
épinglés dans `shared-buildings-transfer-001.json`. Rejouer les tests de portes,
collisions, réconciliation et matériaux dans leur nouveau propriétaire, les
tests du collecteur encore consommé par la carte, Clippy natif/Web et les
frontières. Vérifier conservation des calculs et assertions avant/après.

`shared-buildings-validation-001.json` rapproche le résultat sur les mêmes
2 528 entrées 020, vérifiées inchangées : Clippy strict natif bibliothèques/tests,
Clippy Web du Studio, Clippy du jeu de modules, format et 25 frontières passent.
Les sept tests de projection passent dans le moteur ; les deux tests du
collecteur de la référence passent également, y compris une pose hiérarchique
éditée avant propagation de `GlobalTransform`. La comparaison des sources
confirme les calculs de production, le test des portes, les autres assertions
et la boucle de collecte après seuls changements de propriétaire/formatage.

Ce transfert ne qualifie pas à lui seul un parcours de construction dans le
Studio, une image GPU de bâtiment, des gains de performances ou la clôture
de Gate 7. La projection existante est consommée par les mêmes adapters.
