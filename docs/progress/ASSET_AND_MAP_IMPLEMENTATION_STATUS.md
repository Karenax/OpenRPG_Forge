# État d’implémentation — Asset Vault et carte

Dernière mise à jour : 3 août 2026.

## Terminé

- audit initial du dépôt ;
- politique CC0 du noyau ;
- enregistrement des sources Poly Haven, ambientCG et Kenney ;
- téléchargeur reproductible, reprise, MD5 Poly Haven et SHA-256 ;
- extraction ZIP sûre ambientCG ;
- Vault externe configurable ;
- deux catalogues JSON portables couvrant 555 assets unitaires et 50 packs Kenney ;
- manifeste de pack et rapport d’acquisition ;
- tests unitaires du téléchargement et de l’extraction ;
- snapshot complet de `GamesNotDeveloped/godot-forest-demo` sous `third_party/`, avec commit, hashes,
  licence CC BY 4.0 et crédits Sketchfab originaux ;
- addon `forge_forest_map` isolant le scatter amont derrière les types Forge ;
- scène de création 128 × 128 m, masque RGBA versionné et interface de peinture forêt, clairière,
  fleurs, rochers et zones interdites ;
- remplacement des trois slots visuels par une ressource Godot ou un asset installé du catalogue
  Forge, avec fiche `provenance.json` ;
- smoke test headless de génération et de peinture.
- catalogue canonique v1 de 605 entrées avec UUID stable, SHA-256, licence, provenance et état local ;
- espace Assets avec recherche, facettes, aperçu, gouvernance et dry-run d’import ;
- exposition du catalogue filtré par CLI et MCP ;
- validation visuelle aux tailles normale et maximisée, avec détails défilables et pied stable.

## Partiellement terminé

- catalogue : lecture, recherche et gouvernance disponibles dans le workspace Assets, sans encore
  installation/désinstallation, SQLite ni gestion complète des packs ;
- normalisation : originaux glTF/FBX/OBJ présents, conversion finale GLB et conventions visuelles à
  appliquer ;
- contrôle qualité : intégrité automatisée présente, revue visuelle/LOD/collisions à ajouter.

## Non commencé

- gestionnaire Asset Vault complet avec installation, mise à jour et désinstallation ;
- génération de miniatures et planches contact ;
- Blender Worker de normalisation ;
- packs artistiques curatés ;
- terrain sculpté, navigation, eau, routes et carte Forgotten Forest finale 512 × 512 m.

## Prochaine action

Après G7-17, implémenter l’import transactionnel depuis le plan G7-16, puis curater un premier pack
forêt cohérent et remplacer les trois slots du prototype par des GLB normalisés sans faire des nœuds
générés la source de vérité de la carte.
