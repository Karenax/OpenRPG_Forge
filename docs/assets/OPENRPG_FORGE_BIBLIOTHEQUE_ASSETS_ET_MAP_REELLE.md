# OpenRPG Forge - Bibliothèque d'assets libres et carte 3D de référence

## Document maître à donner à Codex

**Projet :** OpenRPG Forge  
**Objectif :** construire une bibliothèque 3D libre, traçable et extensible, puis l'utiliser pour réaliser une véritable carte RPG moderne dans Godot.  
**Date de référence :** 2 août 2026.  
**Statut :** instruction d'exécution. Ce document ne demande pas une simple étude : il demande une implémentation fonctionnelle dans le dépôt existant.

---

# 1. Mission générale

Tu agis comme ingénieur principal, technical artist, intégrateur Godot et responsable de la conformité des assets pour **OpenRPG Forge**.

La mission est de transformer le projet en une forge capable de produire des environnements 3D crédibles, denses, agréables à explorer et légalement redistribuables. Le résultat ne doit pas ressembler à un prototype voxel, à une carte vide ou à une collection incohérente d'assets téléchargés au hasard.

Tu dois livrer simultanément :

1. une **bibliothèque d'assets libres** organisée et recherchable ;
2. un **système d'acquisition** capable de rechercher, télécharger et mettre à jour les ressources autorisées ;
3. un **pipeline de normalisation** pour rendre les assets compatibles et cohérents dans Godot ;
4. une **interface de bibliothèque dans OpenRPG Forge** ;
5. une **carte démonstratrice complète**, visuellement convaincante et réellement jouable ;
6. une **traçabilité juridique exhaustive** de chaque ressource ;
7. des **outils de contrôle qualité, de déduplication et de performance** ;
8. une architecture permettant d'ajouter ensuite des milliers de ressources sans dégrader le dépôt ni l'éditeur.

Le but final est de démontrer qu'OpenRPG Forge peut devenir un équivalent moderne de l'écosystème Aurora/Neverwinter Nights pour la création de modules, tout en restant libre, extensible et compatible avec une production assistée par IA.

---

# 2. Résultat attendu

Le résultat attendu n'est pas seulement un dossier rempli de fichiers 3D.

Il faut obtenir un **Asset Vault** complet, avec :

- catalogue local ;
- recherche par texte et filtres ;
- catégories et sous-catégories ;
- styles visuels séparés ;
- prévisualisations ;
- installation à la demande ;
- contrôle des licences ;
- génération automatique des crédits ;
- conversion et optimisation ;
- gestion des variantes ;
- détection des doublons ;
- compatibilité avec le placement manuel, le drag-and-drop, les pinceaux de dispersion et la génération procédurale ;
- packs installables indépendants du dépôt principal.

La carte de référence doit constituer un **vertical slice complet** : terrain, végétation, bâtiments, éclairage, navigation, interactions, exploration, combats simples et transitions vers une zone intérieure.

Cette carte doit être suffisamment soignée pour servir :

- de démonstration publique d'OpenRPG Forge ;
- de benchmark graphique et de performance ;
- de projet exemple pour les futurs créateurs de modules ;
- de test permanent du pipeline d'assets ;
- de base pour valider la caméra, le déplacement, les dialogues, les quêtes, le combat et la navigation.

---

# 3. Règles impératives avant toute modification

## 3.1 Auditer le dépôt existant

Avant de coder :

1. lire tous les fichiers de contexte et de documentation du dépôt ;
2. identifier l'architecture existante ;
3. identifier la version de Godot réellement utilisée ;
4. identifier le langage principal du projet ;
5. repérer les systèmes déjà présents : éditeur de carte, catalogue, import, terrain, navigation, gameplay, sauvegarde, plugins ;
6. dresser la liste des fonctionnalités réutilisables ;
7. ne pas créer un deuxième système concurrent si un socle équivalent existe déjà ;
8. préserver les conventions, tests, scripts de build et principes architecturaux du projet.

Créer un rapport initial :

`docs/audits/ASSET_AND_MAP_EXISTING_STATE.md`

Ce rapport doit contenir :

- ce qui existe ;
- ce qui est incomplet ;
- ce qui doit être remplacé ;
- ce qui doit seulement être étendu ;
- les risques de régression ;
- le plan d'intégration retenu.

## 3.2 Ne pas s'arrêter à l'analyse

Après l'audit, poursuivre directement l'implémentation.

Ne pas remettre uniquement :

- une liste de liens ;
- un rapport théorique ;
- des maquettes sans code ;
- des classes vides ;
- des boutons sans fonctionnalité ;
- une carte utilisant seulement des cubes de substitution.

À la fin, le dépôt doit contenir une solution exécutable et une carte réellement ouvrable dans OpenRPG Forge.

---

# 4. Politique de licence obligatoire

## 4.1 Noyau redistribuable : CC0 uniquement pour les assets artistiques

Le pack d'assets livré directement avec OpenRPG Forge doit utiliser par défaut uniquement des ressources :

- **CC0 1.0** ;
- ou créées directement par le projet et placées sous CC0 ;
- ou dont les droits permettent explicitement la redistribution brute sans attribution, sans copyleft et sans restriction commerciale.

Dans le doute, considérer que l'asset n'est pas admissible.

Cette règle concerne :

- modèles 3D ;
- textures ;
- matériaux ;
- HDRI ;
- animations ;
- sons ;
- musiques ;
- images ;
- icônes ;
- VFX ;
- polices ;
- exemples de scènes.

## 4.2 Outils et plugins

Pour le code, les outils et plugins, privilégier :

- MIT ;
- BSD-2-Clause ;
- BSD-3-Clause ;
- Apache-2.0 ;
- ISC ;
- autres licences permissives compatibles avec la distribution du projet.

Toute dépendance doit être enregistrée avec :

- nom ;
- version ou commit ;
- dépôt source ;
- licence ;
- fichier de licence conservé ;
- raison de son utilisation.

## 4.3 Assets avec attribution

Les assets CC BY peuvent être proposés dans des **packs optionnels externes**, mais ils ne doivent pas entrer dans le noyau par défaut tant que toute la chaîne d'attribution et de redistribution n'a pas été validée.

Pour un asset CC BY accepté dans un pack optionnel :

- conserver le titre exact ;
- conserver l'auteur exact ;
- conserver l'URL source ;
- conserver la version de la licence ;
- conserver le texte d'attribution fourni ;
- indiquer toutes les modifications effectuées ;
- générer automatiquement les crédits du module exporté.

Les packs avec attribution doivent être clairement séparés des packs CC0.

## 4.4 Licences refusées pour le noyau

Refuser automatiquement :

- CC BY-NC ;
- toute clause « Non Commercial » ;
- CC BY-ND ;
- toute clause « No Derivatives » ;
- CC BY-SA pour le noyau, sauf décision juridique explicite ultérieure ;
- GPL appliquée directement à des assets artistiques ;
- licence inconnue ;
- absence de licence ;
- licence « free for personal use » ;
- licence d'une marketplace interdisant la redistribution des fichiers bruts ;
- ressource gratuite mais réservée à Unity, Unreal ou un autre moteur ;
- ressource nécessitant de conserver un compte ou un abonnement pour prouver le droit d'utilisation ;
- contenu provenant d'un jeu commercial ;
- contenu extrait, converti ou « ripé » d'un jeu ;
- fan art ou reproduction d'une propriété intellectuelle protégée ;
- contenu dont l'auteur n'a manifestement pas les droits ;
- ressource générée par IA sans provenance ni droit d'utilisation clair.

## 4.5 Interdictions spécifiques

Ne jamais intégrer directement :

- les assets de Neverwinter Nights ;
- les assets Aurora ;
- les ressources du CEP sans autorisation et licence explicites couvrant la redistribution dans OpenRPG Forge ;
- les assets BioWare, Beamdog ou Wizards of the Coast ;
- les modèles de modules NWN existants ;
- des assets Mixamo bruts dans une bibliothèque redistribuable ;
- des assets Fab, Quixel Megascans, Unreal Marketplace ou Unity Asset Store dans le pack libre ;
- des assets Sketchfab dont la licence exacte n'est pas enregistrée et vérifiée ;
- un fichier trouvé sur GitHub dont le dépôt n'a pas de licence explicite ;
- un fichier trouvé dans un agrégateur si la page de l'auteur original n'est pas identifiable.

Mixamo peut éventuellement servir à un projet fermé pour produire un jeu, mais Adobe interdit la redistribution des personnages et animations comme fichiers bruts ou pack d'assets. Il n'est donc pas adapté au noyau libre d'OpenRPG Forge.

## 4.6 Preuve de licence

Pour chaque asset téléchargé, enregistrer :

- l'URL de la page officielle ;
- l'URL du téléchargement ;
- la licence annoncée ;
- l'URL du texte de licence ;
- une copie locale du texte de licence ;
- la date de vérification ;
- le nom de l'auteur ;
- le nom de la source ;
- le hash du fichier original ;
- la version ou date de publication lorsque disponible ;
- une indication `redistributable_raw_files` ;
- une indication `commercial_use` ;
- une indication `attribution_required` ;
- une indication `modification_allowed` ;
- une indication `license_verified`.

Aucun asset ne doit passer dans un pack publié si `license_verified` est faux.

---

# 5. Sources prioritaires à exploiter

Les informations ci-dessous ont été vérifiées le 2 août 2026. Codex doit tout de même vérifier de nouveau la licence au moment du téléchargement, car les conditions d'un service peuvent évoluer.

## 5.1 Poly Haven - priorité maximale

**Site :** https://polyhaven.com/  
**Licence des assets :** CC0.  
**API :** https://polyhaven.com/our-api  
**Endpoint principal :** https://api.polyhaven.com/assets

Utiliser Poly Haven pour :

- arbres réalistes ;
- rochers ;
- troncs et souches ;
- objets naturels ;
- modèles photogrammétriques ;
- matériaux PBR ;
- sols ;
- murs ;
- bois ;
- HDRI ;
- éclairages extérieurs et intérieurs.

Contraintes API :

- envoyer un `User-Agent` propre à OpenRPG Forge ;
- indiquer dans l'interface que les assets viennent de Poly Haven lorsque l'API en direct est utilisée ;
- utiliser les endpoints publics au lieu de scraper le site ;
- respecter les limites et les conditions de service ;
- enregistrer les hashes et tailles fournis par l'API.

Le crédit demandé pour l'utilisation du service API est distinct de la licence CC0 des fichiers.

## 5.2 ambientCG - priorité maximale

**Site :** https://ambientcg.com/  
**Licence :** CC0.  
**Documentation API v3 :** https://docs.ambientcg.com/api/v3/assets/

Utiliser ambientCG pour :

- terrains PBR ;
- herbe, terre, boue, sable, gravier ;
- roche et falaise ;
- écorce ;
- bois ;
- pierre ;
- briques et maçonnerie ;
- decals ;
- modèles 3D photogrammétriques ;
- HDRI ;
- surfaces de bâtiments et de donjons.

Utiliser l'API v3 et non l'ancienne v2 pour les nouveaux développements.

## 5.3 Quaternius - priorité élevée

**Site :** https://quaternius.com/  
**Licence annoncée :** CC0 pour les packs.

Packs à analyser en priorité :

- Stylized Nature MegaKit ;
- Medieval Village MegaKit ;
- Ultimate Modular Ruins Pack ;
- Modular Dungeons Pack ;
- Fantasy Props MegaKit ;
- Universal Animation Library ;
- Universal Animation Library 2 ;
- Universal Base Characters ;
- Modular Character Outfits - Fantasy ;
- Ultimate Animated Animal Pack ;
- Ultimate Monsters ;
- Ultimate RPG Pack ;
- Medieval Village Pack ;
- RPG Character Pack ;
- Modular Medieval Building Pack ;
- Furniture Pack ;
- Crops Pack ;
- Nature packs.

Quaternius fournit généralement FBX, OBJ, glTF/GLB et parfois Blend. Vérifier la licence sur la page exacte du pack et conserver une copie.

La Universal Animation Library propose plus de 120 animations CC0 compatibles avec le retargeting vers Godot. Elle doit être évaluée comme base libre d'animation humanoïde.

## 5.4 KayKit / Kay Lousberg - priorité élevée

**Site :** https://kaylousberg.com/game-assets/  
**Itch.io :** https://kaylousberg.itch.io/

Packs à examiner :

- Forest Nature Pack ;
- Dungeon Remastered ;
- Character Pack: Adventurers ;
- Character Animations ;
- Skeletons ;
- Medieval Builder ;
- Furniture ;
- City Builder ;
- Prototype Bits ;
- packs de personnages et accessoires.

Les versions gratuites indiquées CC0 peuvent être intégrées. Les versions payantes peuvent également être CC0, mais ne doivent pas être téléchargées ou supposées accessibles sans achat. Un achat éventuel doit être laissé à la décision de l'utilisateur.

## 5.5 Kenney - priorité élevée

**Site :** https://kenney.nl/assets  
**Licence des packs gratuits :** généralement CC0, à vérifier sur chaque page.

Utiliser Kenney pour :

- prototypes propres ;
- UI ;
- icônes ;
- effets sonores ;
- objets génériques ;
- packs 3D cohérents ;
- bâtiments simples ;
- éléments de décor ;
- ressources servant aux outils de l'éditeur.

Le bundle All-in-1 regroupe plus de 60 000 assets, mais il est payant. Ne pas l'acheter automatiquement. Préparer une procédure d'import si l'utilisateur le possède, tout en utilisant d'abord les packs gratuits officiels.

## 5.6 Poly Pizza - priorité élevée pour la profondeur du catalogue

**Site :** https://poly.pizza/  
**API :** https://poly.pizza/docs/api/v1.1

Poly Pizza contient des modèles CC0 et CC BY. Pour le noyau :

- appeler l'API avec le filtre de licence CC0 uniquement ;
- ne pas prendre par défaut les résultats CC BY ;
- conserver le champ d'attribution malgré CC0 si l'API le fournit ;
- vérifier l'auteur original ;
- enregistrer le nombre de triangles et les informations d'animation ;
- ne pas exposer la clé API dans Git ;
- stocker la clé dans une variable d'environnement ou un coffre local.

Poly Pizza peut fournir une grande profondeur de petits props, animaux, bâtiments, objets et éléments de décor.

## 5.7 cgbookcase - textures supplémentaires

**Site :** https://www.cgbookcase.com/textures  
**Licence annoncée :** CC0.

Utiliser cette source pour compléter :

- roches ;
- bois ;
- murs ;
- sols ;
- structures urbaines ;
- textures PBR manquantes.

## 5.8 MakeHuman - génération de personnages optionnelle

**Projet :** https://github.com/makehumancommunity/makehuman  
**Licence du code :** AGPL.  
**Assets intégrés :** CC0.  
**Sorties générées à partir des assets intégrés :** le projet MakeHuman indique ne revendiquer aucun droit sur ces sorties.

Usage autorisé dans le pipeline :

- générer des bases humanoïdes ;
- créer plusieurs morphologies ;
- produire des PNJ génériques ;
- exporter vers Blender puis glTF ;
- enregistrer précisément les assets tiers éventuellement ajoutés.

Ne jamais supposer que les vêtements ou assets téléchargés séparément depuis des dépôts communautaires MakeHuman sont CC0. Vérifier chaque ressource tierce indépendamment.

## 5.9 OpenGameArt - source secondaire avec filtre strict

**Site :** https://opengameart.org/  
**FAQ licences :** https://opengameart.org/content/faq

N'utiliser automatiquement que les ressources explicitement CC0.

Pour les autres licences, les placer au maximum dans une file d'examen manuel. Ne pas intégrer automatiquement CC BY, CC BY-SA, OGA-BY ou GPL dans le noyau.

## 5.10 GitHub et autres dépôts publics

GitHub peut servir à découvrir des packs, mais uniquement lorsque :

- le dépôt est celui de l'auteur ou de l'organisation officielle ;
- un fichier de licence explicite est présent ;
- la licence couvre clairement les assets et pas uniquement le code ;
- l'historique n'indique pas un import de contenu tiers douteux ;
- les releases ou commits sont épinglés ;
- les fichiers sont téléchargés depuis une release ou un commit exact.

Ne jamais conclure qu'un fichier est libre uniquement parce qu'il est visible sur GitHub.

---

# 6. Séparer les styles visuels

Une bibliothèque riche devient inutilisable si tout est mélangé.

Créer au minimum les styles suivants :

1. `realistic_pbr` ;
2. `stylized_realism` ;
3. `stylized_low_poly` ;
4. `voxel` ;
5. `prototype` ;
6. `pixel_3d` ;
7. `user_custom`.

Chaque asset doit avoir :

- un style principal ;
- éventuellement des styles compatibles ;
- une palette dominante ;
- un niveau de détail ;
- une densité de texture approximative ;
- un indicateur de compatibilité avec les packs principaux.

L'éditeur doit empêcher ou au minimum avertir lorsqu'une carte mélange des styles manifestement incompatibles.

## Direction artistique de la carte de référence

La carte démonstratrice doit adopter le style :

**fantasy naturelle / stylized realism / PBR moderne**.

Elle doit éviter :

- voxels ;
- cubes sans détails ;
- couleurs plates de prototype ;
- arbres identiques répétés sur toute la carte ;
- matériaux brillants ou plastiques non voulus ;
- proportions incohérentes ;
- mélange visible entre photogrammétrie réaliste et personnages ultra low-poly ;
- végétation sans vent ;
- sol parfaitement plat ;
- éclairage uniforme ;
- scène vide entre les points d'intérêt.

Préparer les autres styles dans la bibliothèque, mais ne pas les mélanger dans la carte principale.

---

# 7. Architecture de l'Asset Vault

## 7.1 Ne pas gonfler le dépôt Git principal

Le dépôt doit contenir :

- le code des outils ;
- les manifestes ;
- les métadonnées ;
- les fichiers de licence ;
- les miniatures raisonnablement compressées ;
- un petit pack Core ;
- les assets nécessaires à la carte exemple si leur taille reste raisonnable ;
- les tests.

Les gros fichiers doivent être stockés dans un **Asset Vault externe** configurable.

Le chemin du Vault doit pouvoir être :

- local au poste ;
- sur un disque secondaire ;
- sur un partage réseau ;
- dans un cache utilisateur ;
- dans un stockage d'équipe ;
- dans Git LFS uniquement pour les packs explicitement versionnés.

Ne pas ajouter plusieurs dizaines de gigaoctets au dépôt sans stratégie.

## 7.2 Arborescence cible

Adapter les noms à l'architecture existante, mais viser une structure équivalente :

```text
OpenRPG_Forge/
  addons/
    openrpg_asset_library/
  assets_core/
    environment/
    characters/
    props/
    materials/
    vfx/
    audio/
  asset_manifests/
    sources/
    packs/
    demo_maps/
  licenses/
    sources/
    assets/
    THIRD_PARTY_ASSETS.md
  tools/
    asset_pipeline/
      adapters/
      catalog/
      download/
      normalize/
      previews/
      quality/
      licenses/
      packaging/
      blender/
  docs/
    assets/
    maps/
    audits/
  demo/
    maps/
      forgotten_forest/
  tests/
    asset_pipeline/
    asset_library/
    demo_map/
```

Vault externe :

```text
OpenRPG_AssetVault/
  originals/
    <source>/<asset_id>/
  normalized/
    <asset_id>/<variant>/
  thumbnails/
  licenses/
  metadata/
  packages/
  cache/
  quarantine/
  rejected/
  logs/
```

## 7.3 Conserver les originaux immuables

Les fichiers téléchargés doivent être conservés dans `originals` sans modification.

Toute conversion doit produire un nouveau fichier dans `normalized`.

Cela permet :

- de refaire une conversion ;
- de prouver l'origine ;
- de comparer les versions ;
- de corriger un pipeline ;
- de ne jamais perdre le fichier sous licence originale.

## 7.4 Stockage adressé par contenu

Utiliser des hashes SHA-256 pour :

- nommer ou indexer les fichiers ;
- vérifier les téléchargements ;
- détecter les doublons exacts ;
- éviter plusieurs copies d'un même fichier ;
- garantir la reproductibilité d'un pack.

Ajouter ensuite une détection des doublons proches :

- hash perceptuel des miniatures ;
- signature géométrique ;
- nombre de sommets et triangles ;
- bounding box ;
- noms et tags ;
- comparaison de textures.

Ne pas supprimer automatiquement un doublon proche. Le signaler pour validation.

---

# 8. Catalogue et schéma de métadonnées

Créer un catalogue exploitable par les outils en ligne de commande et l'éditeur Godot.

Une base SQLite est recommandée pour les recherches locales, avec export JSON pour la portabilité et le versionnement.

Chaque asset doit avoir un enregistrement proche de ce schéma :

```json
{
  "asset_id": "polyhaven_dead_tree_01",
  "title": "Dead Tree 01",
  "source": "polyhaven",
  "source_asset_id": "dead_tree_01",
  "source_page_url": "https://...",
  "download_url": "https://...",
  "author": "...",
  "license_spdx": "CC0-1.0",
  "license_url": "https://creativecommons.org/publicdomain/zero/1.0/",
  "license_verified": true,
  "license_verified_at": "2026-08-02T00:00:00Z",
  "redistributable_raw_files": true,
  "commercial_use": true,
  "modification_allowed": true,
  "attribution_required": false,
  "attribution_text": "",
  "original_sha256": "...",
  "original_size_bytes": 0,
  "original_format": "blend",
  "normalized_formats": ["glb", "tscn"],
  "category": "environment/nature/tree",
  "tags": ["forest", "dead", "tree", "temperate"],
  "style": "realistic_pbr",
  "compatible_styles": ["stylized_realism"],
  "animated": false,
  "rigged": false,
  "triangle_count_lod0": 0,
  "lod_count": 0,
  "material_count": 0,
  "texture_max_resolution": 2048,
  "texture_memory_estimate": 0,
  "dimensions_m": [0, 0, 0],
  "pivot_policy": "ground_center",
  "collision_status": "generated",
  "navigation_status": "obstacle_ready",
  "quality_score": 0,
  "technical_score": 0,
  "license_score": 100,
  "review_status": "approved",
  "installed": true,
  "pack_ids": ["forest_realistic_core"],
  "created_at": "...",
  "updated_at": "..."
}
```

## Catégories minimales

```text
environment/
  nature/
    tree/
    bush/
    grass/
    flower/
    fern/
    mushroom/
    rock/
    cliff/
    stump/
    log/
    root/
  terrain/
  water/
  road/
  bridge/
  ruin/
  village/
  castle/
  dungeon/
  cave/
  interior/
characters/
  humanoid/
  animal/
  monster/
  undead/
  construct/
  mount/
props/
  furniture/
  container/
  tool/
  weapon/
  food/
  alchemy/
  books/
  lighting/
  decoration/
  farming/
  market/
materials/
  ground/
  rock/
  wood/
  wall/
  roof/
  metal/
  fabric/
vfx/
  fire/
  smoke/
  magic/
  weather/
  particles/
audio/
  ambience/
  footsteps/
  combat/
  creatures/
  ui/
animations/
  humanoid/
  creature/
  interaction/
```

---

# 9. Outil d'acquisition automatique

Créer un outil robuste, testable et non dépendant de l'interface Godot.

Le langage doit suivre les besoins du dépôt. Un outil Python est acceptable et recommandé pour l'orchestration, les APIs et Blender en mode headless, même si le runtime principal est en C#.

## 9.1 Adaptateurs de sources

Créer une interface commune :

- recherche ;
- récupération des métadonnées ;
- récupération de la licence ;
- téléchargement ;
- vérification du hash ;
- mise à jour ;
- pagination ;
- limitation du débit ;
- gestion des erreurs ;
- reprise après interruption.

Adaptateurs initiaux :

- `PolyHavenAdapter` ;
- `AmbientCgAdapter` ;
- `PolyPizzaAdapter` ;
- `QuaterniusManualPackAdapter` ;
- `KayKitManualPackAdapter` ;
- `KenneyManualPackAdapter` ;
- `OpenGameArtCc0Adapter` ;
- `GitHubLicensedReleaseAdapter` ;
- `LocalUserAssetAdapter`.

Ne pas scraper un site si une API officielle existe.

## 9.2 Sécurité réseau

Implémenter :

- User-Agent identifiable ;
- temporisation ;
- backoff exponentiel ;
- nombre de tentatives limité ;
- reprise de téléchargement ;
- taille maximale configurable ;
- vérification du type MIME ;
- vérification d'archive ;
- protection contre les chemins `../` dans les ZIP ;
- quarantaine des fichiers suspects ;
- journalisation ;
- absence de secret dans les logs ;
- clés API uniquement dans les variables d'environnement ou le coffre local.

## 9.3 Commandes attendues

Prévoir une CLI équivalente à :

```bash
assetforge sources sync
assetforge search "temperate forest tree" --license CC0-1.0 --style realistic_pbr
assetforge catalog refresh
assetforge install-pack forest_realistic_core
assetforge install <asset_id>
assetforge normalize <asset_id>
assetforge preview <asset_id>
assetforge audit-license --all
assetforge quality-scan --pack forest_realistic_core
assetforge build-godot-pack forest_realistic_core
assetforge update --installed
assetforge report
```

Les commandes exactes peuvent varier, mais l'ensemble des fonctions doit exister.

## 9.4 Modes de téléchargement

Prévoir trois modes :

1. **catalog-only** : récupère les métadonnées et miniatures, pas les gros fichiers ;
2. **curated-pack** : télécharge uniquement les assets sélectionnés dans un manifeste ;
3. **full-source-mirror** : option avancée, désactivée par défaut, uniquement pour les sources qui l'autorisent explicitement.

La bibliothèque doit pouvoir être très riche sans imposer le téléchargement immédiat de tout le contenu.

---

# 10. Manifeste de packs

Les cartes et utilisateurs doivent installer des packs versionnés.

Exemple :

```yaml
pack_id: forest_realistic_core
name: Realistic Temperate Forest Core
version: 1.0.0
license_policy: cc0_only
style: realistic_pbr
categories:
  - environment/nature
  - materials/ground
  - materials/rock
assets:
  - polyhaven_tree_001
  - polyhaven_tree_002
  - ambientcg_ground_forest_001
  - ambientcg_rock_001
optional_assets:
  - polyhaven_hdri_forest_001
quality_requirements:
  minimum_score: 75
  max_texture_resolution_default: 2048
  require_collision_for_large_props: true
  require_lod_for_foliage: true
```

Packs initiaux à créer :

- `core_prototype_cc0` ;
- `forest_realistic_core` ;
- `forest_stylized_core` ;
- `medieval_village_stylized_core` ;
- `dungeon_stylized_core` ;
- `fantasy_props_cc0` ;
- `humanoid_characters_cc0` ;
- `humanoid_animations_cc0` ;
- `animals_cc0` ;
- `monsters_cc0` ;
- `materials_pbr_cc0` ;
- `ui_editor_cc0` ;
- `forgotten_forest_demo_dependencies`.

---

# 11. Pipeline de normalisation 3D

## 11.1 Format cible

Format d'échange privilégié : **glTF 2.0 / GLB**.

Godot recommande glTF 2.0 pour l'import de scènes 3D. Les fichiers `.blend` peuvent être utilisés comme sources de travail, mais les builds et packs doivent privilégier GLB et les ressources Godot générées.

## 11.2 Conversion Blender headless

Créer des scripts Blender en ligne de commande pour :

- ouvrir FBX, OBJ, DAE ou Blend ;
- appliquer les transformations ;
- normaliser l'échelle ;
- corriger l'orientation ;
- positionner le pivot ;
- supprimer les objets cachés inutiles ;
- nettoyer les matériaux ;
- convertir vers PBR ;
- consolider les textures ;
- générer des variantes ;
- produire des LOD si nécessaire ;
- créer des billboards ou impostors pour la végétation ;
- exporter en GLB ;
- produire un rapport JSON.

Ne pas modifier l'original.

## 11.3 Conventions spatiales

Vérifier et appliquer une convention unique :

- 1 unité Godot = 1 mètre ;
- hauteur verticale cohérente ;
- face avant cohérente ;
- pivot au centre du sol pour les props et bâtiments ;
- pivot à la base du tronc pour les arbres ;
- pivot fonctionnel pour portes, leviers et coffres ;
- origines des personnages compatibles avec le contrôleur ;
- tailles humaines de référence cohérentes.

Créer une scène de test contenant :

- grille métrique ;
- personnage étalon ;
- porte étalon ;
- escalier étalon ;
- caméra ;
- éclairage neutre.

Chaque asset doit être automatiquement photographié dans cette scène.

## 11.4 Matériaux PBR

Standardiser :

- Base Color ;
- Normal ;
- Roughness ;
- Metallic ;
- Ambient Occlusion ;
- Emission si nécessaire ;
- Alpha / cutout pour feuilles et herbe.

Vérifier correctement :

- espaces colorimétriques sRGB et linéaires ;
- normal maps ;
- orientation des normales ;
- backface culling ;
- transparence ;
- alpha scissor ;
- ombres des feuilles ;
- anisotropie si utile ;
- matériaux double face uniquement lorsque nécessaire.

Éviter les matériaux double face généralisés, car ils augmentent le coût de rendu.

## 11.5 Résolutions de textures

Valeurs par défaut :

- petits props : 512 px à 1K ;
- props courants : 1K ;
- végétation : 1K à 2K avec atlas ;
- bâtiments : 2K ;
- éléments héros : 2K à 4K ;
- terrain : textures répétables 2K, éventuellement 4K si nécessaire ;
- éviter les textures 8K dans les packs par défaut.

Créer des variantes de qualité :

- Low ;
- Medium ;
- High ;
- Source.

## 11.6 LOD et HLOD

Godot génère automatiquement des LOD de maillage pour les scènes 3D importées, mais cela doit être contrôlé visuellement.

Pour les assets importants :

- conserver ou générer LOD0 ;
- LOD1 ;
- LOD2 ;
- LOD3 si nécessaire ;
- billboard/impostor pour les arbres lointains ;
- shadow impostor si disponible ;
- HLOD pour groupes de bâtiments ou ruines.

Les personnages skinnés doivent être testés séparément, car la décimation automatique peut produire des défauts.

## 11.7 Collisions

Ne pas utiliser une collision triangle complexe pour chaque objet.

Créer :

- boîtes ;
- capsules ;
- cylindres ;
- convex hulls simples ;
- collisions composées ;
- mesh statique seulement lorsque nécessaire.

Pour la végétation :

- collision uniquement près du joueur ;
- pas de collision sur l'herbe ;
- tronc simple pour les arbres ;
- grandes branches seulement si elles influencent réellement le gameplay.

## 11.8 Navigation

Chaque asset doit pouvoir indiquer :

- s'il contribue au bake de navigation ;
- s'il doit être un obstacle ;
- s'il doit être ignoré ;
- s'il nécessite un `NavigationLink` ;
- s'il contient une porte ou un passage dynamique.

## 11.9 Animation et retargeting

Créer un squelette humanoïde canonique OpenRPG Forge.

Il doit permettre :

- retargeting Quaternius ;
- retargeting KayKit ;
- import de personnages utilisateur ;
- locomotion en huit directions ;
- marche ;
- course ;
- sprint ;
- déplacement accroupi ;
- nage ;
- saut ;
- chute ;
- attaque à une main ;
- attaque à deux mains ;
- arc ;
- magie ;
- blocage ;
- esquive ;
- dégâts ;
- mort ;
- interaction ;
- emotes ;
- conversations.

Documenter les os requis et les règles de mapping.

---

# 12. Contrôle qualité automatique

Créer un scanner produisant un score et des avertissements.

## 12.1 Vérifications minimales

- licence vérifiée ;
- fichier lisible ;
- archive sûre ;
- format importable ;
- absence de textures manquantes ;
- absence de chemins absolus ;
- taille cohérente ;
- orientation cohérente ;
- pivot cohérent ;
- normales valides ;
- absence de matériaux roses ou cassés ;
- triangle count enregistré ;
- textures inventoriées ;
- collision générée ou explicitement non nécessaire ;
- LOD disponible lorsque requis ;
- miniature produite ;
- import Godot sans erreur ;
- scène instanciable ;
- absence de script inconnu dans un asset externe ;
- aucun exécutable importé depuis un pack artistique.

## 12.2 Scores

Produire au moins :

- `license_score` ;
- `source_trust_score` ;
- `technical_score` ;
- `visual_score` ;
- `performance_score` ;
- `metadata_score` ;
- `overall_quality_score`.

Un asset avec licence inconnue reçoit automatiquement un score nul et passe en quarantaine.

## 12.3 Revue visuelle

Générer automatiquement :

- vue perspective ;
- vue face ;
- vue côté ;
- vue wireframe ;
- vue collision ;
- aperçu des LOD ;
- aperçu des matériaux ;
- comparaison avec le personnage étalon.

Produire une planche contact par pack.

---

# 13. Interface de bibliothèque dans OpenRPG Forge

Créer un dock ou panneau natif à l'éditeur.

## 13.1 Fonctions minimales

- affichage grille et liste ;
- miniatures ;
- recherche ;
- filtres ;
- catégories ;
- tags ;
- source ;
- licence ;
- style ;
- nombre de triangles ;
- animé / riggé ;
- installé / disponible à distance ;
- compatible avec la carte actuelle ;
- favoris ;
- récents ;
- packs ;
- variantes ;
- aperçu 3D ;
- informations de licence ;
- bouton d'installation ;
- bouton de mise à jour ;
- bouton de suppression locale ;
- drag-and-drop dans la scène ;
- placement par clic ;
- placement aléatoire de variantes ;
- ajout à un pinceau de scatter ;
- ajout à une recette de biome.

## 13.2 Recherche sémantique optionnelle

Préparer une couche permettant ultérieurement à l'IA de comprendre :

- « grand chêne mort » ;
- « petite maison de garde médiévale » ;
- « rochers humides pour une rivière » ;
- « décor de laboratoire d'alchimiste ».

Pour la première version, combiner :

- tags normalisés ;
- synonymes ;
- recherche plein texte ;
- catégories ;
- embeddings locaux optionnels.

Ne pas rendre un service cloud obligatoire.

## 13.3 Génération des crédits

Créer automatiquement :

- `CREDITS_ASSETS.md` ;
- `CREDITS_ASSETS.txt` ;
- crédits intégrables dans le jeu ;
- crédits par module exporté ;
- crédits uniquement pour les assets effectivement utilisés.

Les assets CC0 peuvent tout de même être crédités par courtoisie, mais ils doivent être identifiés comme attribution non obligatoire.

---

# 14. Terrain et dispersion de végétation

## 14.0 Fondation partagée

Le terrain est une fondation générique de zone et ne dépend d'aucun biome. Dans les conventions
Godot/Forge, X/Z forment le plan horizontal et Y représente l'altitude. Le même terrain doit pouvoir
recevoir des recettes de forêt, prairie, désert, marais, neige ou village sans dupliquer sa
géométrie ni ses données persistantes.

La source de vérité est un modèle C# pur, versionné et sérialisable : dimensions, découpage,
résolution, seed, données d'altitude, masques de matériaux et masque de trous. Godot reconstruit
terrain, collisions, navigation et instances par un adapter ; les nœuds et ressources générés ne
deviennent jamais l'état métier. Les outils de sculpture et de peinture passent par le bus de
commandes transactionnel et doivent être annulables et testables hors moteur.

Un champ d'altitude représente montagnes, collines, vallées et dépressions. Une grotte, un tunnel ou
un surplomb nécessite un trou dans le terrain et une géométrie 3D séparée. Les biomes consultent le
terrain par des requêtes communes d'altitude, pente, matériau, humidité, proximité de l'eau ou des
chemins et zones d'exclusion.

## 14.1 Terrain3D

Évaluer puis intégrer **Terrain3D** comme terrain principal si cela reste compatible avec l'architecture du projet.

Dépôt : https://github.com/TokisanGames/Terrain3D  
Licence : MIT.

Terrain3D fournit notamment :

- terrain éditable ;
- grandes surfaces ;
- LOD ;
- textures ;
- sculpt ;
- peinture ;
- trous ;
- instanciation de végétation ;
- plusieurs LOD de foliage ;
- shadow impostors.

Épingler une version stable ou un commit précis. Ne pas suivre une branche mouvante sans contrôle.

Si le projet possède déjà un terrain plus avancé, comparer objectivement et conserver la meilleure architecture.

## 14.2 Scatter

La végétation ne doit pas être placée nœud par nœud.

Utiliser :

- Terrain3D Instancer ;
- MultiMeshInstance3D ;
- ou un système de scatter interne.

Le plugin HungryProton Scatter est sous licence MIT, mais son âge, sa compatibilité avec la version actuelle de Godot et sa maintenance doivent être évalués avant intégration. Ne pas ajouter une dépendance abandonnée si le système natif Terrain3D ou un outil interne suffit.

## 14.3 Recettes de biome

Créer des ressources `BiomeRecipe` ou équivalent.

Une recette doit définir :

- assets autorisés ;
- densité ;
- échelle min/max ;
- rotation ;
- inclinaison maximale ;
- altitude min/max ;
- pente min/max ;
- humidité ;
- proximité de l'eau ;
- proximité d'un chemin ;
- zones d'exclusion ;
- clustering ;
- distance minimale ;
- seed ;
- LOD ;
- collisions ;
- probabilité de variantes.

Biomes initiaux :

- forêt tempérée dense ;
- forêt ancienne ;
- clairière ;
- sous-bois humide ;
- rive de rivière ;
- zone rocheuse ;
- ruines envahies ;
- bord de route ;
- prairie ;
- terrain villageois.

## 14.4 Distribution naturelle

Éviter les distributions uniformes et artificielles.

Combiner :

- masques de densité ;
- bruit multi-échelle ;
- Poisson disk ;
- clusters ;
- règles de succession végétale ;
- exclusion autour des chemins et bâtiments ;
- densité selon la pente et l'humidité ;
- variations de taille et rotation ;
- arbres morts rares ;
- souches proches de zones exploitées ;
- pierres accumulées près des pentes et rivières ;
- végétation envahissante autour des ruines.

Créer trois couches :

1. canopée ;
2. sous-bois ;
3. couverture du sol.

## 14.5 Découpage spatial

Découper la végétation en cellules ou chunks afin que :

- le frustum culling fonctionne ;
- l'occlusion culling fonctionne ;
- les MultiMeshes lointains puissent être masqués ;
- les collisions proches puissent être activées ;
- les données puissent être streamées ;
- la carte ne dépende pas d'un seul MultiMesh géant.

Tester plusieurs tailles de cellule et documenter le choix.

---

# 15. Carte de référence : La Forêt des Ruines oubliées

Nom de travail :

`Forgotten Forest` / `La Forêt des Ruines oubliées`.

## 15.1 Intention

Créer une carte fantasy naturelle dense, construite comme une vraie zone de jeu et non comme une scène de rendu statique.

Elle doit montrer :

- une forêt crédible ;
- une forte profondeur visuelle ;
- des points de repère ;
- une circulation claire ;
- plusieurs chemins ;
- des interactions ;
- une zone habitée ;
- une ruine ;
- une entrée de donjon ;
- une petite boucle de gameplay RPG.

## 15.2 Dimensions

Cible initiale : environ **512 x 512 mètres jouables**, avec paysage et végétation de fond donnant l'impression d'un monde plus vaste.

La taille exacte peut être ajustée après tests, mais la carte doit permettre au moins 10 à 15 minutes d'exploration sans courir en ligne droite dans un espace vide.

## 15.3 Organisation spatiale

Inclure au minimum :

1. une route d'arrivée ;
2. un poste de garde ou camp forestier ;
3. une forêt dense ;
4. une clairière identifiable ;
5. une rivière ou un ruisseau ;
6. un pont ;
7. une zone rocheuse ;
8. des ruines envahies ;
9. une petite implantation habitée ou une auberge isolée ;
10. une entrée de grotte ou de crypte ;
11. un point de vue en hauteur ;
12. au moins deux raccourcis ou chemins secondaires.

## 15.4 Landmarks

Le joueur doit pouvoir s'orienter grâce à :

- un grand arbre ancien ;
- une tour ruinée ;
- un pont ;
- une falaise ;
- une source lumineuse ou fumée du village ;
- une statue ou pierre dressée ;
- une cascade ou moulin si les assets le permettent.

## 15.5 Densité minimale d'assets

Le pack de la carte doit viser au moins :

- 20 variantes d'arbres réellement distinctes ;
- 15 variantes de buissons ;
- 20 variantes de plantes, herbes et fougères ;
- 15 rochers ;
- 8 troncs, branches ou souches ;
- 8 matériaux de terrain cohérents ;
- 12 modules de bâtiments ou ruines ;
- 60 props de décor ;
- 5 types de clôtures, murs ou barrières ;
- 5 types de lumière ;
- 5 effets de particules ;
- 3 ambiances météo ou atmosphériques ;
- plusieurs personnages et créatures libres.

Ces nombres sont des objectifs de variété, pas une obligation de télécharger des doublons médiocres.

## 15.6 Éclairage et atmosphère

Mettre en place :

- éclairage directionnel naturel ;
- ciel ou HDRI CC0 ;
- ombres cohérentes ;
- brouillard atmosphérique ;
- brouillard local dans les creux ;
- lumière traversant la canopée ;
- variation de lumière entre clairière et forêt ;
- eau cohérente ;
- matériaux humides près de la rivière ;
- particules discrètes : poussière, feuilles, insectes, pollen ;
- vent synchronisé entre herbe, buissons et arbres.

La scène doit rester lisible pour le gameplay. Ne pas sacrifier la visibilité à un effet cinématographique excessif.

## 15.7 Gameplay minimal intégré

La carte doit contenir :

- un point de départ ;
- un PNJ donnant une quête ;
- un dialogue à choix ;
- une porte ou barrière ;
- un coffre ;
- un objet ramassable ;
- une rencontre de combat ;
- un groupe de créatures ;
- un déclencheur de zone ;
- un objectif de quête ;
- une récompense ;
- une transition vers la grotte ou crypte ;
- une sauvegarde et un rechargement de l'état minimal.

Le contenu narratif peut être simple, mais toutes les briques doivent réellement fonctionner.

## 15.8 Caméras

Tester deux modes :

- caméra isométrique / tactique proche de Neverwinter Nights ;
- caméra troisième personne libre.

Si le projet ne supporte qu'un mode, ne pas réécrire tout le contrôleur uniquement pour cette carte. Préparer néanmoins l'architecture permettant d'ajouter l'autre mode.

## 15.9 Navigation

La carte doit être entièrement navigable par :

- joueur ;
- PNJ ;
- créatures ;
- compagnons.

Tester :

- chemins étroits ;
- pont ;
- portes ;
- pentes ;
- obstacles dynamiques ;
- poursuite autour des rochers ;
- changements de zone ;
- récupération après blocage.

Le bake de navigation ne doit pas inclure l'herbe et les petits détails inutiles.

---

# 16. Qualité visuelle attendue

La carte doit prouver que la qualité d'un projet Godot dépend du pipeline, des assets, de la direction artistique et du game feel, pas du style de RPG in a Box.

## Critères d'acceptation visuels

- aucune texture manquante ;
- aucun matériau rose ;
- pas de scintillement massif des feuilles ;
- pas de répétition évidente tous les dix mètres ;
- pas d'arbres flottants ;
- pas de rochers à moitié suspendus ;
- pas d'herbe traversant les routes ;
- pas de bâtiments sans fondations ;
- pas d'échelles incompatibles ;
- pas de pivots erronés ;
- transitions de terrain propres ;
- chemins lisibles ;
- composition visuelle volontaire ;
- points d'intérêt visibles sans minimap obligatoire ;
- LOD non distrayants ;
- absence de pop-in excessif ;
- ombres stables ;
- personnage visuellement intégré à la scène ;
- animations sans glissement de pieds majeur ;
- collisions crédibles.

## Cohérence avant quantité

Si deux packs ont des styles incompatibles, ne pas les mélanger pour atteindre un quota.

Mieux vaut :

- 60 assets cohérents et bien traités ;

que :

- 1 000 assets disparates visibles simultanément.

La bibliothèque peut contenir plusieurs styles, mais la carte doit en utiliser un seul.

---

# 17. Performance

## 17.1 Objectif général

La carte doit rester fluide sur un PC de jeu moyen, avec plusieurs profils graphiques.

Créer les presets :

- Low ;
- Medium ;
- High ;
- Ultra / Capture.

## 17.2 Mesures obligatoires

Mesurer et documenter :

- FPS moyen ;
- FPS minimum ;
- temps CPU ;
- temps GPU ;
- draw calls ;
- triangles visibles ;
- nombre d'instances visibles ;
- mémoire textures ;
- VRAM approximative ;
- temps de chargement ;
- taille du pack ;
- temps de bake navigation ;
- temps de génération du terrain et du scatter ;
- shader compilation ;
- stutters.

## 17.3 Techniques obligatoires

Utiliser selon besoin :

- MultiMeshInstance3D ;
- découpage en cellules ;
- automatic mesh LOD ;
- HLOD ;
- impostors ;
- occlusion culling ;
- distance visibility ;
- réduction des ombres lointaines ;
- shadow impostors ;
- texture compression ;
- mipmaps ;
- atlas ;
- collision activée seulement à proximité ;
- navigation simplifiée ;
- chargement asynchrone ;
- cache ;
- warm-up des shaders si nécessaire.

Godot indique que MultiMesh est adapté à de très nombreux exemplaires d'un même mesh, mais le culling ne peut pas sélectionner chaque instance individuellement. Il faut donc répartir les instances dans plusieurs MultiMeshes spatiaux.

## 17.4 Budget mémoire

Ne pas charger toutes les résolutions sources.

Les profils de qualité doivent choisir automatiquement :

- variantes de textures ;
- distances de végétation ;
- qualité des ombres ;
- densité d'herbe ;
- résolution d'eau ;
- brouillard ;
- nombre de lumières ;
- LOD.

---

# 18. Enrichissement massif de la bibliothèque

## 18.1 Stratégie

« Enrichir à fond » signifie :

- référencer beaucoup de ressources ;
- organiser les sources ;
- créer des packs cohérents ;
- permettre l'installation à la demande ;
- automatiser les mises à jour ;
- garantir les licences ;
- normaliser les formats ;
- ne pas noyer l'utilisateur sous des milliers de fichiers non triés.

## 18.2 Recherches à effectuer

Effectuer des recherches ciblées par catégories, en privilégiant les sources officielles :

- CC0 realistic forest tree glTF ;
- CC0 medieval building modular glTF ;
- CC0 dungeon modular glTF ;
- CC0 fantasy props glTF ;
- CC0 animated animals glTF ;
- CC0 fantasy monsters rigged ;
- CC0 humanoid animation library ;
- CC0 PBR terrain materials ;
- CC0 rocks photogrammetry ;
- CC0 medieval furniture ;
- CC0 weapons shields armor ;
- CC0 cave assets ;
- CC0 ruins ;
- CC0 water effects ;
- MIT Godot foliage shader ;
- MIT Godot water shader ;
- MIT Godot weather system ;
- MIT Godot day night system.

Chaque nouveau résultat doit passer par la vérification de licence complète.

## 18.3 Priorités de contenu

Ordre recommandé :

1. terrain et matériaux ;
2. arbres et végétation ;
3. rochers, falaises et eau ;
4. bâtiments et ruines ;
5. props ;
6. personnages ;
7. animations ;
8. animaux et monstres ;
9. VFX ;
10. audio ;
11. UI ;
12. autres styles et genres.

## 18.4 Rapport d'acquisition

Créer :

`docs/assets/ASSET_ACQUISITION_REPORT.md`

Le rapport doit contenir :

- sources interrogées ;
- date ;
- nombre de résultats ;
- nombre acceptés ;
- nombre refusés ;
- raisons des refus ;
- volume téléchargé ;
- volume après normalisation ;
- nombre par catégorie ;
- nombre par style ;
- nombre par licence ;
- assets utilisés dans la carte ;
- éléments encore manquants.

Créer également :

`docs/assets/REJECTED_OR_RESTRICTED_SOURCES.md`

pour éviter qu'un futur agent réintroduise des sources déjà rejetées.

---

# 19. Assets créés par le projet

Lorsque le catalogue libre ne fournit pas une ressource satisfaisante, créer l'asset dans Blender ou par génération procédurale.

Exemples :

- variantes d'arbres à partir de textures d'écorce et de feuilles CC0 ;
- modules de chemin ;
- pont simple ;
- murets ;
- ruines modulaires ;
- decals ;
- impostors ;
- collisions ;
- LOD ;
- atlas ;
- variantes de couleur ;
- kits de scatter.

Les scripts Blender créés par le projet doivent être sous licence permissive compatible avec OpenRPG Forge.

Les assets originaux produits pour le pack libre doivent être placés sous CC0 et documentés comme tels.

Ne pas présenter comme création originale une modification minimale d'un asset tiers. Conserver sa provenance.

---

# 20. Intégration IA et MCP

Préparer le système pour qu'un agent IA puisse :

- rechercher un asset ;
- proposer un pack ;
- télécharger un asset autorisé ;
- lire ses métadonnées ;
- vérifier sa licence ;
- lancer la normalisation ;
- générer une miniature ;
- ajouter des tags ;
- créer une recette de biome ;
- placer des assets dans une carte ;
- produire une zone à partir d'une description ;
- remplacer un asset incompatible ;
- générer les crédits ;
- lancer les tests de performance.

Créer des commandes MCP ou une API locale stable autour des fonctions de l'Asset Vault.

Exemples d'actions futures :

```text
search_assets(query, filters)
get_asset_metadata(asset_id)
install_asset(asset_id)
install_pack(pack_id)
normalize_asset(asset_id, preset)
create_biome_recipe(description)
place_asset(scene_id, asset_id, transform)
scatter_assets(scene_id, recipe_id, region)
audit_scene_licenses(scene_id)
build_scene_credits(scene_id)
profile_scene(scene_id)
```

Les appels IA ne doivent jamais contourner la validation de licence.

---

# 21. Tests obligatoires

## 21.1 Tests unitaires

Couvrir :

- parsing des APIs ;
- filtres de licence ;
- téléchargement ;
- reprise ;
- hashes ;
- extraction ZIP sûre ;
- catalogue ;
- recherche ;
- déduplication ;
- génération des crédits ;
- manifestes ;
- conversion des métadonnées ;
- règles de catégorisation.

## 21.2 Tests d'intégration

Tester :

- un asset de chaque source ;
- un asset FBX ;
- un OBJ ;
- un GLB ;
- un pack avec textures ;
- un asset animé ;
- un asset invalide ;
- une licence inconnue ;
- un téléchargement interrompu ;
- un hash incorrect ;
- une mise à jour de version ;
- installation dans Godot ;
- instanciation en scène ;
- suppression du cache ;
- réinstallation reproductible.

## 21.3 Test de carte

Créer un test ou outil de validation qui ouvre la carte et vérifie :

- ressources présentes ;
- aucune référence cassée ;
- navigation valide ;
- point de départ valide ;
- objectifs de quête présents ;
- transitions valides ;
- crédits générables ;
- pack de dépendances complet ;
- export du module réussi.

---

# 22. Documentation à livrer

Créer au minimum :

- `docs/assets/ASSET_LICENSE_POLICY.md` ;
- `docs/assets/ASSET_VAULT_ARCHITECTURE.md` ;
- `docs/assets/ASSET_PIPELINE_USAGE.md` ;
- `docs/assets/ADDING_A_NEW_SOURCE.md` ;
- `docs/assets/ADDING_A_MANUAL_PACK.md` ;
- `docs/assets/ASSET_METADATA_SCHEMA.md` ;
- `docs/assets/ASSET_ACQUISITION_REPORT.md` ;
- `docs/assets/REJECTED_OR_RESTRICTED_SOURCES.md` ;
- `docs/assets/THIRD_PARTY_ASSET_CREDITS.md` ;
- `docs/maps/FORGOTTEN_FOREST_DESIGN.md` ;
- `docs/maps/FORGOTTEN_FOREST_PERFORMANCE.md` ;
- `docs/maps/FORGOTTEN_FOREST_PLAYTEST.md` ;
- `docs/mcp/ASSET_LIBRARY_MCP.md` ;
- `CHANGELOG.md` mis à jour.

Ajouter des captures de l'éditeur et de la carte si le workflow du dépôt l'autorise.

---

# 23. Livrables techniques finaux

La mission est terminée seulement si les éléments suivants existent :

## Bibliothèque

- Asset Vault configurable ;
- catalogue fonctionnel ;
- recherche ;
- filtres ;
- installateur ;
- mise à jour ;
- désinstallation locale ;
- miniatures ;
- informations de licence ;
- drag-and-drop ;
- packs ;
- variantes ;
- crédits.

## Pipeline

- au moins trois adaptateurs API automatiques fonctionnels ;
- au moins trois types de packs manuels supportés ;
- conversion Blender headless ;
- normalisation GLB ;
- génération de ressources Godot ;
- collisions ;
- LOD ;
- prévisualisations ;
- audit qualité ;
- audit licence ;
- déduplication.

## Contenu

- pack forêt réaliste ou stylized realism cohérent ;
- pack médiéval ;
- pack donjon ;
- pack props ;
- personnages libres ;
- animations libres ;
- créatures ou animaux ;
- matériaux ;
- VFX de base.

## Carte

- Forgotten Forest ouvrable ;
- terrain finalisé ;
- végétation dense ;
- landmarks ;
- navigation ;
- interactions ;
- quête ;
- combat ;
- transition ;
- sauvegarde minimale ;
- profils graphiques ;
- rapport de performance.

## Qualité

- tests passants ;
- aucune licence inconnue ;
- aucun lien cassé ;
- aucun fichier brut interdit ;
- aucune dépendance non documentée ;
- aucune erreur d'import Godot ;
- documentation complète ;
- procédure de reproduction depuis un clone propre.

---

# 24. Ordre d'exécution recommandé

## Phase 0 - Audit

- lire le dépôt ;
- cartographier les systèmes existants ;
- décider où intégrer Asset Vault et carte exemple ;
- créer le rapport initial.

## Phase 1 - Politique et catalogue

- créer le schéma ;
- créer SQLite + JSON ;
- créer la politique de licence ;
- créer les tests de filtrage ;
- créer le stockage par hash.

## Phase 2 - Sources automatiques

- Poly Haven ;
- ambientCG ;
- Poly Pizza CC0 ;
- métadonnées et miniatures ;
- téléchargements contrôlés.

## Phase 3 - Pipeline de normalisation

- Blender headless ;
- GLB ;
- matériaux ;
- échelle ;
- pivots ;
- collisions ;
- LOD ;
- previews ;
- rapports.

## Phase 4 - Sources manuelles et packs

- Quaternius ;
- KayKit ;
- Kenney ;
- OpenGameArt CC0 ;
- autres dépôts vérifiés ;
- création des manifestes.

## Phase 5 - Interface Forge

- dock ;
- recherche ;
- filtres ;
- installation ;
- drag-and-drop ;
- scatter ;
- licences ;
- crédits.

## Phase 6 - Carte Forgotten Forest

- terrain ;
- composition ;
- biomes ;
- bâtiments ;
- ruines ;
- eau ;
- éclairage ;
- gameplay ;
- navigation ;
- optimisation.

## Phase 7 - Validation

- tests ;
- audit juridique ;
- profilage ;
- playtest ;
- correction ;
- documentation ;
- reproduction sur clone propre.

---

# 25. Critères d'arrêt et gestion des blocages

Ne pas bloquer toute la mission parce qu'une source :

- nécessite un compte ;
- nécessite une clé ;
- nécessite un clic manuel ;
- est temporairement indisponible ;
- ne fournit pas le format idéal.

Dans ce cas :

1. documenter le blocage ;
2. créer une procédure manuelle ;
3. continuer avec les autres sources ;
4. ne jamais contourner les protections du site ;
5. ne jamais fabriquer une licence ;
6. ne jamais télécharger depuis un miroir douteux.

Créer :

`docs/assets/MANUAL_DOWNLOADS_REQUIRED.md`

pour les packs nécessitant une action utilisateur.

---

# 26. Exigences de restitution de Codex

À la fin de chaque phase, mettre à jour un journal :

`docs/progress/ASSET_AND_MAP_IMPLEMENTATION_STATUS.md`

Ce fichier doit indiquer :

- terminé ;
- partiellement terminé ;
- non commencé ;
- tests ;
- problèmes ;
- décisions ;
- fichiers principaux modifiés ;
- prochaines actions.

Dans la réponse finale, fournir :

1. synthèse de l'architecture réalisée ;
2. sources réellement intégrées ;
3. nombre d'assets catalogués ;
4. nombre d'assets téléchargés ;
5. nombre d'assets utilisés dans la carte ;
6. taille du Vault ;
7. licences rencontrées ;
8. tests exécutés ;
9. performances mesurées ;
10. limites restantes ;
11. chemins pour lancer l'éditeur, la bibliothèque et la carte.

Ne pas annoncer qu'une fonctionnalité est terminée si elle n'est pas testée.

---

# 27. Sources techniques et juridiques vérifiées

## Licences et catalogues

- Poly Haven - licence CC0 : https://polyhaven.com/license
- Poly Haven - API publique : https://polyhaven.com/our-api
- Poly Haven - arbres : https://polyhaven.com/models/nature/trees
- ambientCG - licence CC0 : https://ambientcg.com/license
- ambientCG - API v3 : https://docs.ambientcg.com/api/v3/assets/
- Quaternius - catalogue CC0 : https://quaternius.com/
- Quaternius - Stylized Nature MegaKit : https://quaternius.com/packs/stylizednaturemegakit.html
- Quaternius - Universal Animation Library : https://quaternius.com/packs/universalanimationlibrary.html
- KayKit - Forest Nature Pack : https://kaylousberg.com/game-assets/forest-nature-pack
- KayKit - Adventurers : https://kaylousberg.itch.io/kaykit-adventurers
- Kenney - catalogue : https://kenney.nl/assets
- Kenney - All-in-1 : https://kenney.itch.io/kenney-game-assets
- Poly Pizza - catalogue : https://poly.pizza/
- Poly Pizza - API : https://poly.pizza/docs/api/v1.1
- OpenGameArt - FAQ licences : https://opengameart.org/content/faq
- cgbookcase - textures CC0 : https://www.cgbookcase.com/textures
- MakeHuman - licence : https://github.com/makehumancommunity/makehuman/blob/master/LICENSE.md
- Mixamo - restrictions de redistribution des fichiers bruts : https://community.adobe.com/questions-696/mixamo-faq-licensing-royalties-ownership-eula-and-tos-589400

## Godot et terrain

- Godot - import de scènes 3D : https://docs.godotengine.org/en/stable/tutorials/assets_pipeline/importing_scenes.html
- Godot - Mesh LOD : https://docs.godotengine.org/en/stable/tutorials/3d/mesh_lod.html
- Godot - MultiMeshInstance3D : https://docs.godotengine.org/en/stable/classes/class_multimeshinstance3d.html
- Godot - optimisation MultiMesh : https://docs.godotengine.org/en/stable/tutorials/performance/using_multimesh.html
- Godot - NavigationRegion3D : https://docs.godotengine.org/en/stable/classes/class_navigationregion3d.html
- Terrain3D : https://github.com/TokisanGames/Terrain3D
- Terrain3D - licence MIT : https://github.com/TokisanGames/Terrain3D/blob/main/LICENSE.txt
- HungryProton Scatter - licence MIT : https://github.com/HungryProton/scatter/blob/v4/LICENSE.md

---

# 28. Décision directrice

Construire d'abord une petite sélection extrêmement cohérente et une carte réussie, puis élargir massivement le catalogue grâce au système d'installation à la demande.

La carte Forgotten Forest est la preuve de qualité. L'Asset Vault est la preuve de pérennité.

OpenRPG Forge ne doit pas devenir un simple agrégateur d'assets. Il doit devenir une chaîne complète :

**recherche -> validation juridique -> téléchargement -> normalisation -> optimisation -> catalogage -> placement -> génération de carte -> export du module -> crédits.**

Exécuter cette mission jusqu'à obtenir une carte convaincante et un système reproductible.

