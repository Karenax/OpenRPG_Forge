# État d’implémentation — Asset Vault et carte

Dernière mise à jour : 5 août 2026 — tranche AV6-D.

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
- installation à la demande d’un GLB gouverné comme blueprint ProjectContent, ajout automatique à la
  palette, placement par bouton ou glisser-déposer avec `visual.asset`, et chargement réel partagé
  entre Creator et runtime après contrôle SHA-256 du chunk ;
- recettes de biome prairie/roche reliées aux blueprints d’assets installés, brosse canonique de
  peinture matériau/biome, dispersion déterministe partagée par Creator/runtime et conversion glTF
  en maillage runtime validée par smoke dans le viewport ;
- installation IA/MCP gouvernée par plan sec, application et revert séparés, avec révision, diff,
  inverse, audit asset/SPDX, validation de licence/manifeste et contrôle physique taille/SHA-256 du
  GLB avant toute mutation du projet ;
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
- export `.orpmod` de 80 fichiers, validation sans diagnostic et smoke depuis le cache du package ;
- capture Forward+ 1280 × 720 inspectée après correction visuelle.
- benchmark runtime versionné sur Forgotten Forest 128 × 128 m : 68,806 ms de reconstruction,
  5,367 ms de collisions, 10,778 ms de navigation, 3,604 ms de sculpture, 23,649 ms de requête de
  chemin, 1 737 328 octets alloués et +17 506 304 octets de working set ;
- couverture de performance contrôlée : 1 089 échantillons/vertices, 2 048 triangles, 181 formes de
  collision, 1 990 polygones de navigation, 23 assets/67 niveaux LOD et trois groupes de culling.
- rapport AV5 reproductible et bloquant : 605 assets locaux/licenciés, 31 952 fichiers du Vault,
  23 manifestes, 45 chunks, 22 références gouvernées, intégrité SHA-256, taille `.orpmod` et seize
  budgets runtime mesurés dans une même preuve JSON/Markdown ;
- procédure complète d’ajout de sources et packs, avec plafond du catalogue figé à 605 jusqu’à une
  nouvelle revue visuelle et technique documentée ;
- ambiance Forgotten Forest reconstruite sans nouvel asset externe : vent de canopée, oiseaux et
  ruisseau PCM procéduraux/spatiaux, plus 204 particules animées réparties entre lucioles, feuilles et
  brume au sol ; provenance et hash des trois cues sont persistés dans le profil de zone.
- tranche AV6-A de personnage animé construite depuis le pack Kenney déjà gouverné
  `animated-characters-protagonists` : modèle skinné, texture et trois FBX d’animation fusionnés par
  Blender 4.5.10 en un GLB de 1 604 triangles, 58 os et clips `idle`, `jump`, `run`, sans modifier les
  sources du Vault ; requête, hashes, miniature 512 × 512 et audit 99/100 sont persistés ;
- profil `runtime.animationProfile=humanoid-basic` authorisé sur le joueur, Maëlys, les deux rôdeurs
  et le Gardien ; Creator prévisualise l’idle, le runtime bascule le joueur entre idle/course/saut et
  le smoke prouve cinq instances, trois clips, le scénario complet et une capture Forward+ ;
- rapport de livraison étendu sans augmenter le catalogue : 23 manifestes CC0, 45 chunks,
  22 références gouvernées, 16 assets distincts utilisés et 1 334 694 octets de chunks.
- admission bornée du pack officiel KayKit Adventurers 1.0 au commit
  `672074b73ba276876a19e8816ecdc5241817ab47` : archive CC0 immuable, SHA-256 vérifié, cinq GLB,
  catalogue spécialisé et manifeste de source ;
- production Blender 4.5.10 de cinq variantes fantasy dotées chacune d’un rig de 41 os, de 76 pistes
  et de 69 clips runtime de durée positive, avec cinq miniatures 512 × 512 et audits 100/100 ;
- profil `kaykit-adventurer` et propriétés sémantiques repos/course/saut/attaque/impact/mort partagés
  par le Creator et le runtime, avec trois attaques, deux impacts et une mort prouvés par le smoke ;
- export AV6-B validé de 100 fichiers, 28 manifestes/50 chunks, 22 références gouvernées, cinq
  personnages distincts, 28 assets/72 niveaux LOD et seize budgets runtime réussis.
- extension AV6-C du terrain canonique à 512 × 512 m : 129 × 129 échantillons, 64 secteurs de 64 m,
  16 641 vertices et 32 768 triangles, sans scène ni format parallèle ;
- cinq couches de biome et 5 564 instances MultiMesh, 39 modèles et quatre ensembles de landmarks,
  avec plafonds de dispersion adaptés aux grandes surfaces mais inchangés pour les petites cartes ;
- dix segments d’eau, deux ponts et 32 454 polygones navigables ; le trajet camp–sanctuaire de 70
  points, les seize budgets 512 × 512 et la capture Forward+ aérienne passent.
- shader terrain PBR procédural commun aux 64 secteurs, avec variation monde multi-échelle, pente,
  humidité, litière, roughness, specular et occlusion dérivées des couleurs vertex canoniques ;
- deux routes reconstruites comme rubans continus et texturés procéduralement depuis leurs points de
  contrôle, ciel physique, lumière rééquilibrée et brouillard plus distant ; capture aérienne et
  rapprochée inspectées, avec SSAO/SSIL explicitement retirés après artefacts visibles.

## Partiellement terminé

- catalogue : lecture, recherche, gouvernance et installation d’un modèle dans la palette disponibles
  dans le workspace Assets ; la désinstallation UI, SQLite et la gestion complète des packs restent ;
- normalisation : profils, cache, staging, passage direct des GLB et worker Blender exécuté ; la
  conversion KTX2, la curation visuelle de masse et les budgets par plateforme restent à livrer ;
- contrôle qualité : intégrité automatisée présente, revue visuelle/LOD/collisions à ajouter.
- Forgotten Forest : carte 512 × 512 jouable, navigable, sonorisée, enrichie de VFX et dotée de cinq
  personnages fantasy avec combat animé et terrain PBR procédural ; retargeting inter-rigs, textures
  scannées et habillage rapproché des secteurs restent des finitions de production.

## Non commencé

- gestionnaire Asset Vault complet avec mise à jour et application physique des
  migrations/désinstallations déjà analysées par le domaine ;
- génération de miniatures et planches contact ;
- service de surveillance temps réel ;
- finition artistique de Forgotten Forest : variations de feuillage, textures scannées gouvernées,
  eau avancée et habillage rapproché des 64 secteurs.

## Prochaine action

Enrichir l’habillage rapproché des secteurs sans casser les budgets 512 × 512. Le retargeting
inter-rigs reste un incrément séparé.

## Preuves de la tranche AV3 MCP

- builds Release de `Forge.Application`, `Forge.McpHost` et `Forge.Toolset` sans erreur ;
- 29 tests `Forge.Mcp.Tests` : dry-run sans révision, apply persistant, revert, audit SPDX, refus de
  licence discordante et refus d’un GLB gouverné absent ;
- gate global validé : 718 tests .NET, 24 tests Python généraux, 22 tests du pipeline d’assets,
  Toolset/runtime/Forward+, Blender 4.5, exports, réseau, MCP, sauvegarde/reprise et marqueur final
  `OPENRPG_FORGE_VERIFICATION=PASS` ;
- graphe déterministe validé à 3 965 nœuds et 9 386 relations.

## Décisions et fichiers principaux

- le pont éditeur par pipe reste en lecture seule ; aucune mutation parallèle n’y est ajoutée ;
- `GovernedAssetModelVerifier` centralise le contrôle physique partagé par Creator et MCP ;
- contrats MCP dans `AssetCatalogContracts.cs`, orchestration dans `ForgeProjectAgentService.cs` et
  surface officielle dans `ForgeMcpTools.cs` ;
- aucune nouvelle dépendance de production n’est introduite.
