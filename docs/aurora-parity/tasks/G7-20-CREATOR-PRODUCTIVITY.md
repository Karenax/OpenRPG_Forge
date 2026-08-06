# G7-20 — Productivité de la coque Creator

## Résultat

Le Creator expose désormais un **Espace de travail créateur** cohérent avec la direction sombre/braise :

- filtres combinés par type de blueprint, tags, auteur et état, avec isolation et mode fantôme ;
- placement en ligne, grille ou dispersion déterministe, appliqué par une commande atomique ;
- zones ouvertes persistées, zone active explicite et comparaison structurelle en lecture seule ;
- aide locale intégrée avec exemples, dépendances et chemins de provenance ;
- sept profils métier persistés dans `.forge/editor-profile.json`, hors contenu canonique du module.

Le filtre de types accepte une sélection multiple, est restauré fidèlement dans la surface Creator
et est réappliqué au viewport dès la réouverture du projet. Une configuration locale invalide est
refusée avec le diagnostic stable `OPENRPG_FORGE_WORKSPACE_PROFILE_LOAD_FAILED`, puis le profil sûr
par défaut reste disponible.

## Architecture et sécurité

`EditorWorkspaceProfile` et `EditorDisplayFilter` sont indépendants du moteur. La sérialisation est
versionnée et atomique. Le placement répété produit un plan déterministe puis réutilise
`PasteEntitiesHandler` : collision, révision, audit, patch inverse et historique restent donc
centralisés. La zone active demeure l’unique cible des mutations ; la comparaison ne modifie rien.

## Preuves

- `tests/Forge.Domain.Tests/Authoring/EditorWorkspaceProfileTests.cs`
- `tests/Forge.ModuleFormat.Tests/Workspaces/EditorWorkspaceProfileStoreTests.cs`
- `tests/Forge.Application.Tests/Creator/RepeatedPlacementPlannerTests.cs`
- `OPENRPG_FORGE_TOOLSET_G720_PRODUCTIVITY_PASS` vérifie le viewport réel, isolation, fantômes,
  six placements groupés, undo/redo, deux onglets, comparaison, aide sourcée et round-trip du profil ;
- build Toolset Release et Debug : 0 avertissement, 0 erreur.

## Limites explicites

Le lot ne prétend pas encore fournir des calques nommés, la peinture continue, deux viewports 3D
simultanés, une recherche documentaire globale ni des permissions de profil contraignantes. Ces
extensions n'empêchent pas les cinq flux P1 d'être utilisables dans le prototype.

Le gate P1 G7-20 est fermé. Les extensions avancées listées ci-dessus restent suivies par les lignes
de capacité concernées sans rouvrir ce lot de prototype.
