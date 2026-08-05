# État d’implémentation — Asset Vault et carte

Dernière mise à jour : 5 août 2026.

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
- production réelle d'un GLB 2 texturé par Blender 4.5.10 depuis `ambientcg:3DApple001`, avec
  collisions, deux LOD, hash de sortie vérifié, source du Vault immuable et prévisualisation rendue.
- profil artistique canonique avec cible métrique, densité texel, shader, époque, réalisme et palette,
  avertissements par blueprint, édition transactionnelle et persistance versionnée ;
- profil éditeur v2 avec échelle UI/texte, replis de polices, onglets de zones et favoris/récents.
- profil de rendu de zone PBR/HDR avec tone mapping, éclairage par pixel, budgets de lumières et ciel
  physique/procédural/panorama, appliqué de façon identique au Creator et au runtime.
- profil de monde de zone avec terrain `Heightmap`/`Flat`, matériaux, biomes, trous, features,
  volumes de navigation, routes aériennes, collisions et streaming asynchrone partagé par Creator et
  runtime.
- pack curaté `forgotten-forest-core` de 22 GLB Kenney Mini Forest CC0-1.0, avec identité stable,
  SHA-256, profil, reçu, chunks modèle/palette, crédits et réinstallation idempotente ;
- module `forgotten-forest` 128 × 128 m authorisé depuis le domaine : relief 33 × 33, sentier,
  ruisseau, quatre biomes, 357 instances MultiMesh, 18 entités modélisées, camp, pont, tour et
  sanctuaire ;
- caméra orbitale et déplacement relatif à la caméra, HUD compact, terrain double face et échelles
  cohérentes des modèles ;
- navigation de surface construite depuis le terrain canonique : une région, 1 990 polygones,
  58 triangles de rivière exclus, un lien de pont et un chemin moteur camp-sanctuaire de 72 points ;
- ruisseau en quatre surfaces shader animées et quatre volumes physiques avec nage, flottabilité et
  courant directionnel ;
- dialogue, collecte, passage, deux éclaireurs, boss scripté, loot, quête et sortie reconstruits par
  le runtime ;
- export `.orpmod` de 75 fichiers, validation sans diagnostic et smoke depuis le cache du package ;
- capture Forward+ 1280 × 720 inspectée après correction visuelle.

## Partiellement terminé

- catalogue : lecture, recherche et gouvernance disponibles dans le workspace Assets, sans encore
  installation/désinstallation, SQLite ni gestion complète des packs ;
- normalisation : profils, cache, staging, passage direct des GLB et worker Blender exécuté ; la
  conversion KTX2, la curation visuelle de masse et les budgets par plateforme restent à livrer ;
- contrôle qualité : intégrité automatisée présente, revue visuelle/LOD/collisions à ajouter.
- Forgotten Forest : tranche jouable, navigable et revue disponible ; animations, audio, VFX et
  extension 512 × 512 restent des finitions de production.

## Non commencé

- gestionnaire Asset Vault complet avec installation, mise à jour et application physique des
  migrations/désinstallations déjà analysées par le domaine ;
- génération de miniatures et planches contact ;
- service de surveillance temps réel ;
- sculpture interactive et carte Forgotten Forest finale 512 × 512 m ; la fondation terrain
  générique, ses routes, son bake de navigation et son eau animée sont disponibles avant composition
  de production.

## Prochaine action

Exposer dans le Creator Aurora l’authoring de terrain, les recettes de biome et l’installation du
pack curaté sans faire des nœuds générés la source de vérité de la carte, puis enrichir la tranche
avec animations, ambiance audio et VFX gouvernés.
