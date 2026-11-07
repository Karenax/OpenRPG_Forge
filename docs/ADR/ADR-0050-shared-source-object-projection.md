# ADR-0050 — Projection et géométries d'objets source communes

Date : 26 octobre 2026. Statut : réalisation Gate 7 en cours.

## Constat et décision

Le cache de géométrie par révision et le cycle projection/collisions des bâtiments
triangulés restaient dans la crate de carte. Transférer les implémentations existantes
à `forge_engine::source_surface::{geometry,objects}`. L'adaptateur du paquet admis
construit `MeshPart` et `PreparedMeshes::from_parts` ; aucune identité de carte ni
recherche de bibliothèque de référence n'appartient à ces propriétaires moteur.
Studio et les consommateurs nomment directement les types moteur, sans réexport
des anciens types. Les autres familles d'objets restent à transférer.

Conserver le partage des handles, les clés ID/hash exactes, les budgets de triangles
et de parties, les composants d'identité auteur, la préparation avant mutation et
les vérifications Rapier du retrait comme des nouvelles formes/poses. Les documents
canoniques et leur historique ne sont pas des caches ECS. Le décodage du paquet
source reste une responsabilité d'acquisition et ne vaut pas import Studio qualifié.

## Preuves et limites

Les essais moteur indépendants d'une carte vérifient copie, transformations, partage
de géométrie, refus atomiques et synchronisation/retrait réel des collisions. Les
sondes reconstruites emploient le paquet réel de 23 bâtiments ; la fixture optionnelle
`--package-source` est annoncée comme dérivée du paquet et ne remplace pas le mode
historique de lecture d'archive auteur. Les trois générations GPU de profondeur
vérifient transformations et suppression sur trois cascades. Ce n'est pas un essai
de rendu couleur, de fidélité complète, de performances ou de clôture Gate 7.

Le bilan central `shared-source-objects-validation-001.json` conserve entrées, logs,
preuves brutes, comparaisons des mécanismes transférés et limites. L'acquisition
explicite des objets du module et l'héritage implicite de référence dans Studio
restent à corriger.
