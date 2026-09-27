# Terrain : matériaux source dans Bevy

## Export vérifié

`source-012` et `source-013` sont identiques : 260 fichiers de données, 65 383
instances. Normales/splat 2048² avec 12 mipmaps, détail 512² avec 10 mipmaps.
Générateur de mipmaps GPU upstream intact, mêmes entrées CPU, wrappers de
lecture séparés. Le niveau zéro est comparé par octets aux données originales.
Hashes, dimensions et nombres de niveaux vérifiés ; aucun checkout source modifié.
Cela qualifie la répétabilité sur l'adaptateur courant, pas entre tous les GPU.

## Adaptation exécutée

`terrain_surface::TerrainMaterial` utilise les expressions WGSL de la surface
source, les modules de matière et les samplers clamp, repeat et anisotrope 4×.
Les expressions d'albédo, rugosité, normale perturbée et AO alimentent le PBR Bevy.
Les fonctions source hors de cette passe (déplacement, rivage et ombre solaire)
ne sont pas remplacées par des approximations : elles restent à intégrer.
Le chemin source `HAS_WETNESS=0`, `REFRACTION_CLIP=0` est explicite.

Capture `native-island-surface-001.png` réellement inspectée : sable, sous-bois,
couverture forestière, roche et végétation sous-marine apparaissent avec le
relief et la canopée. Reliefs très sombres sous l'éclairage Bevy provisoire.
L'océan absent laisse voir tous les fonds marins.

La fixture `coast-surface-probe-001` reprend position, quaternion et FOV de
`baseline/coast.json`. Le prototype reprend aussi les plans source de
`src/engine/Engine.js` (0,06 m et 60 000 m). Les captures côtières permettent
d'inspecter les rides et le grain du sable à hauteur de caméra ; elles ne
reproduisent pas encore l'éclairage, l'océan ou le contenu complet de cette vue.
La canopée reste celle des cartes lointaines : absence des plantes proches,
palmiers, herbes et autres éléments de la scène.

## Limites de qualification

Pas encore de comparaison numérique des sorties matériau source/Bevy.
Le CDLOD source, l'humidité animée, l'ombre du relief, l'environnement lumineux,
le ciel, l'océan, le village et le rendu temporel restent ouverts. Le rendu est
celui d'un exemple natif, pas du viewport Studio. Les contrôles WASM précédents
portent sur les données projet/monde, pas sur ce shader.

L'adaptation shader reste dans les outils et l'exemple de qualification ; elle
devra rejoindre le chemin produit partagé une fois les ressources et variantes
complètes qualifiées. Aucune fermeture TW, parité globale ou publication.

## Contrôles finaux

- Clippy strict de l'exemple et du module matériau réussi
  (`island-surface-clippy-final.log`).
- Exécution côtière finale terminée avec code 0 ; capture FHD
  `native-coast-surface-003.png` inspectée. Elle est identique à la capture 002
  avant la séparation des fonctions de caméra et le contrôle de domaine :
  `731f7d8a464190eeed7613292bf55ea28fa527bef0d81bbae8585f78259347df`.
- Régression silhouette feuillu proche identique :
  `native-tree-mask-regression-002.png`, SHA
  `49be5ddcbfcb3b7c66ec0bc3a13debeb4e78a2c7f75571477e7ac556f36021ae`.
- Binaire final :
  `9a5a6fecf11c9302a4e00d7a99933cd16b621616ea7f31166a1bee0a16eb87ab`.
- Journaux des rendus : `coast-surface-native-003.log`,
  `tree-mask-regression-002.log`. Processus terminés, aucun serveur ajouté.

Les anciennes captures de diagnostic restent inchangées. Les caméras sans pose
de référence utilisent désormais un plan lointain explicite de 20 000 m ; les
captures plus anciennes gardaient le défaut Bevy. Ne pas comparer les surfaces
lointaines de ces deux réglages comme si leur frustum était identique.
