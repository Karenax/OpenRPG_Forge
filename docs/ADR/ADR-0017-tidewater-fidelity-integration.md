# ADR-0017 — Intégration Tidewater fidèle et éditable

Date : 2026-09-24. Statut : accepté pour exécution par instruction du propriétaire
« lance ce travail complet, objectif, avoir exactement la même carte, les mêmes effets visuels ».

## Décision et périmètre

Référence : https://github.com/dgreenheck/tidewater, commit
`1438b1abfcaee3267092b75573014f4d9b4a983c`. La demande autorise le chantier
maritime précédemment différé et la reprise complète décrite dans
`docs/planning/TIDEWATER_INTEGRATION.md`. Elle ne clôt aucune gate existante et
n'autorise ni publication Git ni distribution publique implicite.

La carte, les placements, les silhouettes, matériaux, éclairages, effets et
comportements sont à reproduire puis à rendre éditables dans Forge. Une scène
inspirée, un effet désactivé, une couleur de remplacement ou un screenshot du
renderer JavaScript ne constituent pas une preuve de parité Forge.

Rust/Bevy/wgpu/WGSL restent la pile produit. Tidewater est une référence externe
épinglée ; Node/Vite sont autorisés dans les seuls outils d'acquisition,
d'extraction et de comparaison. Aucun runtime Forge ne dépend du clone ni de
JavaScript. Pas de second moteur embarqué, de fork Bevy ou de GPU device séparé.

## Sources et dérivés

- Clone canonique intact sur E:, HEAD détaché et provenance vérifiée.
- Clone de référence exécutable séparé ; overlays de mesure explicitement tracés.
- Exports intermédiaires hashés : relief APRÈS fondations du village, masques,
  placements individuels, paramètres de variation, géométrie et attributs shaders.
- Ces exports sont des sources immuables d'import, pas une nouvelle tête mutable.
- Les documents Forge et commandes d'ADR-0014/0016 portent les retouches.
- Les caches GPU, imposteurs, collisions et meshes restent dérivés.
- Les modèles réutilisables sont gouvernés et visibles dans le catalogue Studio.

Une importation GLB brute n'est pas suffisante pour les plantes : `aVeg`, `aMat`,
`aLobe`, hauteur de tige et inclinaison participent à la silhouette GPU. Les
préserver dans les sources et porter leur sémantique avant de qualifier le rendu.
Le village est regroupé par matériau ; l'extraction des bâtiments individuels
doit intervenir avant regroupement, sans modifier l'ordre de consommation du RNG.

## Évolution des contrats

Prévoir sources de hauteur/masques par blocs, instances importées à IDs stables,
paramètres de prototypes et overrides. Ne pas encoder 4 millions de hauteurs
comme corrections JSON ni une forêt comme milliers de placements manuels.
Généraliser les limites spatiales de végétation (512 m aujourd'hui), avec
migrations et budgets explicites. Les anciennes cartes restent lisibles.

Premier contrat raster : `TerrainDocument` v4 opt-in, `SampledRasterV1` et
`sampled_source` (métadonnées validées + SHA-256 des float32 LE). Le champ est
absent des sérialisations v1–v3 ; aucun rehash des anciennes cartes. Domaine
centré, dimensions et pas doivent correspondre au document, sans translation
ou remise à l'échelle implicite. La base utilise des échantillons au centre
des cellules ; les corrections auteur gardent la grille aux coins existante.
L'élévation de base est un décalage explicite ; les paramètres de bruit sans
effet sont normalisés (amplitude 0, fréquence 1, octave 1, lacunarité 2,
persistance 0,5). L'évaluateur dérivé vérifie les octets avant échantillonnage.
Les tuiles CPU sont bornées à 128 intervalles par axe, avec pas demandé
explicitement ; meshes et collisions consomment les mêmes positions/indices.
La résolution du blob dans les archives de zone, les commandes Studio et le
rendu adaptatif restent des étapes séparées à qualifier avant usage produit.

## Contrat des instances importées

`VegetationDocument` v2 référence une `forge.imported-vegetation` v1 : un bloc
par couche, identifié par SHA-256, sans chemin ni handle GPU. V1 reste réservé
aux recettes procédurales existantes et omet le nouveau champ dans sa sérialisation.
La source importée accepte des étendues multiples de 64 m jusqu'à 4096 m,
16 couches et 131072 instances au total. Le générateur procédural refuse
explicitement cette source tant qu'un résolveur d'instances n'est pas fourni.

Chaque ligne `transform_f64_le_v1` contient une clé source de 32 octets, puis
translation XYZ, quaternion XYZW, échelle XYZ et les paramètres de prototype
ordonnés, tous en f64 little-endian. Les lignes sont triées par clé unique,
sans en-tête ni remplissage. Taille, hash, domaine et valeurs sont validés
avant exposition. L'identité d'entité dépend du projet, de la zone, de la couche
et de la clé source ; elle ne dépend ni du LOD ni du hash du bloc. Une nouvelle
clé upstream ne constitue pas automatiquement la même instance lors d'un réimport.

Les paramètres Tidewater conservés sont l'azimut et l'amplitude d'inclinaison,
la hauteur de tige, la variation et l'échelle verticale de forme. Ils ne sont
pas remplacés par une transformation approximative du mesh. Leur restitution
exacte est mesurée dans le prototype natif ; leur usage dans Studio reste à qualifier.
L'export destiné au rendu ajoute `source_order`, entier ordinal du groupe amont,
afin de restituer son ordre sans changer le tri des clés source dans le bloc.
L'adaptateur conserve la lecture des cinq anciens paramètres pour les données,
mais exige cet ordinal pour reconstruire les groupes de la scène de fidélité.
Ces blocs immuables restent distincts des corrections d'auteur de la zone ;
leur archivage matériel et leur résolution dans Studio restent à implémenter.

## Licences

Code : MIT, copyright 2026 DRG Software Solutions LLC. Conserver la licence
et la provenance de toute partie dérivée. Les crédits tiers restent applicables
par fichier : Poly Haven/Freesound CC0, personnages Microsoft Rocketbox MIT,
polices Apache-2.0/OFL. La déclaration globale ne remplace pas l'inventaire.
Les assets Cadle restent exclus. Gros fichiers et caches sur E:, hors Git.

## Qualification

Même machine, résolution interne/sortie, caméra, heure, seed et état temporel.
Comparaisons fixes et animées : côte, village, canopée, récif, sous-marin,
bateau, nuit. Contrôles séparés : identité du contenu, parité visuelle,
édition/persistance, gameplay, performance native, viewport WASM/WebGPU.
Une différence de calcul GPU ne justifie pas l'abandon de l'objectif visuel :
mesurer les écarts et les corriger ; aucun seuil de tolérance ne vaut accord humain.
Les profils dégradés demeurent disponibles mais ne qualifient pas la parité.
La finalisation exige la revue comparative du propriétaire ; les travaux
indépendants et les corrections se poursuivent sans attente intermédiaire.
