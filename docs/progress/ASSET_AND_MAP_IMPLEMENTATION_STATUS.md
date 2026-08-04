# État d’implémentation — Asset Vault et carte

Dernière mise à jour : 4 août 2026.

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
- profils d'import texture/modèle persistés canoniquement et édités par commande transactionnelle
  avec dry-run, undo/redo et diagnostics stables ;
- préparation CLI/UI confinée au workspace avec reçu déterministe, copie directe des GLB canoniques
  et staging des sources qui nécessitent encore un worker ;
- smoke réel sur un GLB Kenney et une texture ambientCG avec preuve que les hashes du Vault restent
  inchangés.
- manifeste d'assets content-addressed avec chunks et plateformes, collision, animation, VFX et LOD,
  persistance transactionnelle, inclusion `.orpmod` et validation runtime des hashes ;
- surface Content Browser de préparation/manifeste avec état vide, VFX, résumé et undo/redo, revue
  visuellement sans mutation du module de démonstration.
- round-trip externe canonique et persistant, requête Blender typée sans code arbitraire, worker
  confiné, validation croisée C#/Python sur un OBJ réel et analyse sûre migration/désinstallation.

## Partiellement terminé

- catalogue : lecture, recherche et gouvernance disponibles dans le workspace Assets, sans encore
  installation/désinstallation, SQLite ni gestion complète des packs ;
- normalisation : profils, cache, staging, passage direct des GLB et worker Blender livré ; la preuve
  d’exécution Blender réelle et la conversion KTX2 restent à appliquer sur une machine équipée ;
- contrôle qualité : intégrité automatisée présente, revue visuelle/LOD/collisions à ajouter.

## Non commencé

- gestionnaire Asset Vault complet avec installation, mise à jour et application physique des
  migrations/désinstallations déjà analysées par le domaine ;
- génération de miniatures et planches contact ;
- service de surveillance temps réel et exécution Blender validée avec le binaire optionnel ;
- packs artistiques curatés ;
- terrain sculpté, navigation, eau, routes et carte Forgotten Forest finale 512 × 512 m.

## Prochaine action

Valider visuellement G11-C, puis curater un premier pack forêt cohérent et remplacer les trois slots
du prototype par des GLB gouvernés sans faire des nœuds générés la source de vérité de la carte.
