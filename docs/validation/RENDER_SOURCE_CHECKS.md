# Contrôles ciblés de fidélité des sources et du rendu

Adaptation du 10 octobre 2026. Les idées viennent de l'audit de
[KingdomComeGlueMapper, commit 4bff077](https://github.com/SamG-Coder/KingdomComeGlueMapper/tree/4bff0771f11495909d2e28c8ff39a11b56cae53b) :
conservation mesurée des données, clôture des dépendances, comptabilité des
exclusions et validation indépendante. Aucun code, format, asset ni constante
CryEngine n'est repris. L'implémentation utilise les vérificateurs Forge existants.

## Raccordements

`scripts/render_verification.py` fournit des fonctions de diagnostic réutilisables,
sans CLI supplémentaire, modification d'asset ni dépendance à un nom de carte.
Les adapters de données spécifiques restent dans les outils d'acquisition.

| Outil existant | Contrôle ajouté | Défaut de rendu recherché |
|---|---|---|
| `tidewater/verify-village.py`, appelé par `verify-export.py` | Chaîne complète de mips, références uniques, chemins internes et tailles exactes | Texture correcte de près mais absente ou incohérente de loin |
| `tidewater/verify-village-package.py` | Couverture par ID source et erreur de position euclidienne en mètres | Objet remplacé malgré un compte égal, dérive diagonale, mauvais repère |
| `tidewater/verify-canonical-packing.py` | Comptabilité explicite des identités conservées | Instance manquante ou supplémentaire malgré des buffers lisibles |
| `tidewater/compare-coverage.py` | Couverture absente et ajoutée, régions, fractions et bords | Silhouette manquante, doublon, masque alpha ou décalage |
| `tidewater/compare-object-depth.py` | Couverture directionnelle et profondeur sur l'intersection seule | Caster absent distingué d'un caster placé à une profondeur différente |

Les commandes et champs historiques restent disponibles. Les rapports de comparaison
ajoutent `quality_diagnostics`; les rapports de placement/packing ajoutent
`source_coverage`. Le placement expose aussi `position_roundtrip_max_distance_m`.
Le champ historique `position_roundtrip_max_m` reste une erreur maximale par composante.

## Contrats et interprétation

- Les IDs sont comparés exactement. Une omission exige une raison explicite et
  ne peut concerner un objet également importé. Le périmètre/état source est fixé
  par l'appelant. Une raison d'exclusion ne démontre pas l'invisibilité d'un objet.
  Le paquet village comptabilise 30 sources : 27 props conservés et trois
  éclairages appartenant au paquet path-light distinct, conformément à son
  constructeur existant. Cela ne qualifie pas la présence de ces éclairages
  dans le rendu final. Le packing végétal attend toutes ses identités.
- Les mips vont du niveau zéro jusqu'à 1×1, avec réduction entière des deux axes.
  Ce contrôle concerne des fichiers non compressés distincts. Le format, la couleur,
  les canaux et le sampler viennent du contrat source, jamais d'une supposition
  sur le nom de la texture. Les vérificateurs de manifestes existants contrôlent les hashes.
  Taille et présence des mips ne prouvent ni leur upload ni leur résidence GPU.
- Le contrôle de position compose une matrice affine avec les vertices locaux et
  compare à l'oracle source en float64. La distance est euclidienne en mètres,
  avec une tolérance fournie par l'adapter. L'adapter village conserve son seuil
  de 0,1 mm ; ce n'est pas une tolérance universelle. Les rotations, échelles
  non uniformes et miroirs restent représentables ; aucune correction silencieuse.
- Les masques sont des passes de couverture explicites. Le noir d'une image
  éclairée n'est pas une preuve d'absence géométrique : `compare-albedo.py`
  n'est donc pas raccordé à ce contrôle de silhouette.
- `missing` et `additional` donnent nombres et rectangles inclusifs `[x0,y0,x1,y1]`.
  Le diagnostic au-delà d'un pixel n'efface pas le désaccord brut et ne fournit
  aucun seuil d'acceptation automatique. Des captures vides donnent `IoU=null`,
  avec un état explicite d'absence de preuve de couverture.
- Les profondeurs concernent uniquement la convention standard 0..1, fond à 1.
  Les erreurs sur l'intersection sont séparées des casters absents/ajoutés.
  Ces valeurs ne sont pas des distances en mètres. Le reverse-Z nécessite un
  adapter explicite, comme les conventions de caméra et de matrice.

## Qualité finale

Ces contrôles localisent une cause possible ; ils ne changent ni shaders ni
apparence du renderer et ne ferment aucune gate. `render_quality_qualified=false`
reste explicite. Pour conclure sur une amélioration, acquérir des images finales
appariées avec même révision, scène, caméra, viewport, profil, animation et météo,
puis inspecter les vues proches/lointaines et les transitions. Les observations
GPU, la fluidité, la collision et la revue humaine gardent leurs preuves propres.

Pour terrain/routes, conserver les conventions Forge de coordonnées et de centres
de samples. La quantification KCD à 5 cm ne s'applique pas à nos rasters. Les
vérificateurs de relief/normales existants gardent leurs oracles indépendants ;
aucun seuil de couture ou rehaussement global n'est ajouté sans contrat de source.

## Vérification locale

```powershell
python -B scripts/Test-RenderVerification.py -v
python -B scripts/tidewater/tests/test_render_verification_cli.py -v
```

La suite exerce perte/remplacement d'identité, omissions contradictoires, mips
tronqués ou réordonnés, traversée de chemin, transformations miroir/rotation,
dérive diagonale, entrées non finies, casters absents, différences de bord et
captures entièrement vides. Elle utilise aussi le véritable adapter village
sur des données de test isolées. Ces fixtures ne sont pas des livrables visuels.

NumPy est nécessaire pour les contrôles numériques, comme pour les comparateurs
existants ; les contrôles de manifestes restent utilisables avec la bibliothèque
standard. Aucun test ne lance Blender, le moteur, le navigateur ou une campagne GPU.

## Résultats exécutés le 10 octobre 2026

- 15 tests des helpers et de l'adapter village : PASS, Python 3.13 / NumPy 2.5.1.
- Deux tests des CLIs coverage/depth réels : PASS, runtime Python embarqué,
  NumPy 2.3.5 / Pillow 12.3.0. Les anciens champs sont contrôlés, ainsi que
  les diagnostics additionnels et le cas de couches entièrement vides.
- Replay de `verify-village.py` sur l'export source conservé `source-017` :
  31 nodes, sept matériaux, 14 textures et 146 mips : PASS.
- Replay de `verify-village-package.py` sur les deux paquets indépendants
  conservés : 297 fichiers identiques, 27 props, 11 949 triangles ; distance
  source/restauré maximale 0 m. Couverture : 30 sources, 27 conservées et
  trois éclairages explicitement affectés au paquet distinct : PASS.

Le paquet replay est épinglé au SHA-256
`d03cd51f1ee894a46a8e84c5e3b5b3763c52d933bac9226ee99478425e4a02da`.
Les fichiers ont été lus depuis les archives conservées, résolus sans liens
filesystem et vérifiés contre les SHA-256 source ET retenus de leurs reçus.
Seuls les payloads nécessaires ont été copiés dans
`E:/OpenRPG_Forge/.tmp/render-source-checks-20261010/` ; les deux rapports neufs
sont `village-source-replay.json` et `village-package-replay.json`.

Ces résultats valident les vérificateurs et leur compatibilité avec les données
conservées. Aucune nouvelle image finale, qualification GPU, revue humaine,
promotion d'asset, changement de gate ou publication Git n'est revendiquée.
