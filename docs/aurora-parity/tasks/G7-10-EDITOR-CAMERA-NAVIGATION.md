# G7-10 — Navigation caméra d’auteur

Date d’exécution : 2 août 2026.

## Capacité bornée

- `ORF-SHELL-007` — navigation caméra d’édition.

## Résultat livré

La caméra du Creator n’est plus une position figée. `EditorCameraProfile` fournit un UUID stable,
un schéma v1, une pose indépendante du moteur, une vitesse, une sensibilité, une projection
perspective ou orthographique, des bookmarks à UUID et une provenance. La pose contient pivot,
yaw, pitch, distance, champ de vision et taille orthographique avec bornes finies explicites.

`navigate_editor_camera` applique orbite, panoramique, zoom adaptatif, focus, vues cardinales,
vitesse, sensibilité et bookmarks avec révision optimiste, dry-run, diagnostics stables, patch,
inverse et audit. `EditorCameraCommandHistory` fournit undo/redo sans inscrire les mouvements de
caméra dans l’historique du module. Les préférences sont écrites atomiquement dans
`.forge/editor-camera.json`, rechargées avec le workspace et exclues des packages `.orpmod`.

Le viewport Aurora expose une barre compacte avec cadrage, perspective 3/4, dessus, face, gauche,
vitesse et repères. Clic droit orbite, bouton central translate, molette zoome et WASD/QE pilote la
vue. Le focus utilise la sélection ou le contenu de la zone. Chaque projection est appliquée à la
caméra visible depuis le même profil canonique.

## Preuves

- `EditorCameraProfileTests` couvre valeurs par défaut, schéma, UUID, bornes et bookmarks uniques ;
- `EditorCameraNavigationTests` couvre navigation, focus, vues, réglages, dry-run, diagnostics,
  bookmarks et historique dédié ;
- `EditorCameraProfileStoreTests` couvre défaut, round-trip atomique et document invalide ;
- `OPENRPG_FORGE_TOOLSET_G710_CAMERA_PASS` prouve contrôles, projection, pilote, bookmark,
  persistance, undo/redo et révision projet inchangée ;
- `scripts/Verify.ps1` constitue le gate intégré final du lot.

## Limites conservées

- les raccourcis ne sont pas encore configurables ;
- le pilote est un déplacement relatif sur le plan, pas encore un mode libre avec élévation et
  inertie cinématique ;
- les bookmarks sont locaux au workspace, sans partage d’équipe ni collection nommée avancée ;
- transitions animées, vitesse liée à la profondeur sous curseur et vues arrière/droite dans la
  barre compacte restent disponibles dans le modèle mais pas toutes exposées comme boutons.

La capacité passe au statut `Implemented` partiel.
