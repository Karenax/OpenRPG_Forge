# Gate 1 — Preuve R1-067 placement de pont, collider Rapier et franchissement

Date de validation locale : **2026-09-01**  
Branche : **`Rust`**  
Statut Git : **arbre de travail local non publié**

## Verdict borné

**PASS LOCAL POUR R1-067.** La compilation du `WorldSketch` produit maintenant un contrat dérivé
`forge.world-bridge-placements` v1. Le runtime refuse de le charger si son hash de terrain sculpté
ne correspond pas exactement au `TerrainDocument` fourni. L'adapter Bevy en dérive un tablier, des
parapets, deux piles, une présentation procédurale et cinq colliders Rapier stables.

Le probe borné pilote le vrai `PlayerControllerPlugin` par sa commande publique. Le personnage part
trois mètres avant le tablier, franchit le croisement `road_main` / `river_main`, touche le collider
du pont, atteint la rive opposée puis s'immobilise au sol. Les identités Bevy/Rapier du joueur, du
terrain et du pont restent inchangées pendant le test.

Ce verdict ne qualifie **ni un navmesh, ni un pathfinder, ni une carte jouable complète, ni un asset
artistique AAA**. Le fournisseur de navigation reste `not_selected` et `navigation_qualified=false`.

## Contrats matérialisés

- `WorldSketch` éditable : hash canonique
  `46f1d766a9dc35af75d172a7d1700af95dda397ba956ceba0e23318df49f3839` ;
- terrain sculpté : hash canonique
  `f47294def69f86f2d3c7e1fd52c522cab0b1a595e6e292bb7af724742e4ddb16` ;
- audit de praticabilité v2 : hash canonique
  `0bd085e851a63f6318a63c77bdc6b41e7b2ee55ce984cf9faeec0cd46875b61d` ;
- placements de pont v1 : hash canonique
  `459108c9dd50185e55000fe6f63fa234bd0c22d80b84048b2536ff9d86f43be1` ;
- entrée Blender v4 : hash canonique
  `018f72319961bda1dc9252bf6d1fc3a10f9faf62bee3316c7ed2c9fd97701a40`.

Le document de placement relie explicitement `bridge_south`, `road_main` et `river_main`. Il porte
les deux atterrissages en coordonnées monde, la largeur et l'épaisseur du tablier, la pente, les
parapets, les deux appuis, le lit et la direction locale de l'eau, ainsi qu'une forme de collision
`oriented_box`. Le `WorldSketch` reste la source éditable ; ce document et les meshes sont des
dérivés reconstructibles.

## Correction guidée par le runtime

Le premier tablier, long de 38,5 m, entrait dans la berge sculptée et bloquait le contrôleur après
12,107 m. Une extension fixe de deux mètres corrigeait l'entrée mais créait encore une paroi locale
à la sortie, après 39,212 m sur 42,5 m. Ces deux essais ont donc été rejetés.

Le compilateur sélectionne désormais la première paire d'atterrissages qui satisfait simultanément :

1. au moins 0,10 m de dégagement longitudinal au-dessus du relief sculpté ;
2. quatre mètres de terrain post-atterrissage sous la pente maximale du profil joueur ;
3. une pente de tablier inférieure à ce même contrat joueur.

Le profil retenu possède une portée horizontale de **44,000004 m**, une longueur de surface de
**44,128132 m** et une pente de **4,367186°**. Cette correction vient d'un échec physique observé,
pas d'un ajustement uniquement visuel.

## Preuve de franchissement réelle

Événement structuré final `bridge_crossing_probe_completed` :

| Mesure | Valeur observée |
|---|---:|
| code de sortie | 0 |
| ticks fixes totaux | 638 |
| ticks de franchissement observés | 613 |
| ticks au sol pendant le franchissement | 613 / 613 |
| plus longue séquence hors sol | 0 |
| ticks de contact avec le collider du tablier | 477 |
| entrée de contact sur l'axe du probe | 2,776127 m |
| sortie de contact sur l'axe du probe | 47,064854 m |
| progression cible | 50,500004 m |
| progression atteinte | 50,507721 m |
| dégagement maximal au-dessus du terrain | 5,307107 m |
| centre audité franchi | oui |
| identités terrain / joueur / pont préservées | oui / oui / oui |
| état final au sol | oui |
| fournisseur de navigation | `not_selected` |
| navigation qualifiée | non |

Le probe ne téléporte pas le personnage sur la rive opposée et n'interroge pas une approximation
géométrique maison. Il observe la sortie du contrôleur cinématique Rapier et exige des collisions
avec l'entité exacte marquée `WorldSketchBridgeDeck`.

## Présentation runtime et inspection visuelle

La scène sans asset projette : un tablier, 72 pavés, deux parapets, 18 potelets, deux piles avec
contreforts et 102 voussoirs formant trois anneaux d'arche sur chaque façade. La caméra de preuve
ajoute une surface d'eau locale de prévisualisation ; celle-ci ne prétend pas remplacer le système
hydrologique canonique.

Capture inspectée à sa résolution originale **1920 × 1080** :

```text
E:\OpenRPG_Forge\.gate1-evidence-runtime\world-sketch-v4-bridge-runtime-20260901-113305\runtime-bridge-preview-arch-v2.png
SHA-256 : c2ce2c3e6ab40b3b50ee287790963a184fe97090fdbd50728533fab916fb64f8
Taille : 1 761 047 octets
```

Une première capture a été rejetée : onze blocs espacés par travée donnaient l'impression de pierres
suspendues sous un mur plein. La version retenue utilise des secteurs elliptiques extrudés continus,
segmentés en voussoirs et raccordés aux piles. Cela qualifie la lisibilité structurelle d'un greybox
procédural, pas la finition d'un asset final texturé.

## Reproductibilité des documents

Deux compilations finales dans des dossiers neufs ont produit les mêmes octets :

| Fichier | SHA-256 brut | hashes uniques sur 2 runs |
|---|---|---:|
| `terrain.v1.json` | `38a59c26bfc2aee5d099efb2965ec8c538bfc9a850618a2e9e61d3f713f4994f` | 1 |
| `terrain-carved.v2.json` | `49f2565670afa4c5fdb782bc88cce3ce161ee1d96a3f1f2cac4a37652400c3cc` | 1 |
| `vegetation.v1.json` | `3e75bd5218fe1c14e9fdd287774cfd2f585c0b0cb2ca844691840b9281ff0d5b` | 1 |
| `semantic-masks.v1.json` | `481c520eb8e0a93d6bd0f896650f7223bb3e01c928cda004a8c78164709ccbb1` | 1 |
| `spline-carving.v1.json` | `db0dc0eec4b605dd1da4ce8dcd61b70081d0cbcc9caaf1dca8bbbfd1dfe9ffd9` | 1 |
| `traversal-audit.v2.json` | `40f4bc327c25f1bd41887fce2fed23494184ea3f22bdf4acf00e0f0fa92a224a` | 1 |
| `bridge-placements.v1.json` | `7d5f8e1b651e48738628f8bbebf8b94ea0aa4c8e6ac3f5de735544e9f91fc244` | 1 |
| `blender-input.v4.json` | `bf666bfd725b85fff976271ff10853bedb4026912eacf7277f84f63af6e4d390` | 1 |
| `world-sketch.compilation.v4.json` | `5ba7560f5c6295e63942481f45dbd7821f83bf1b4ea384aa5819e2ed33176d6b` | 1 |

## Blender 4.5 LTS

Le worker version 7 consomme le même `bridge_placements` v1, incline réellement le tablier, place
les parapets, les pavés et les deux piles, puis inscrit dans son manifeste les hashes terrain/audit,
les deux longueurs, la largeur, la pente et le nombre d'appuis.

Deux reconstructions finales donnent :

- GLB identique octet par octet :
  `dce9a28ed21ebf5d1b517d4791a7b75275c31a0452c8099da402a4a7dff08b61` ;
- image héroïque décodée identique :
  `ce4e2d2537aecb0f5f3fa4e7c4130308086df5e4987e0c31a0b389f890d3a3e3` ;
- caméra `human_bridge` décodée identique :
  `1fbf29b38b577367c741568fcfc971a3e07a07fcec4402fb6406554dc75054f7` ;
- quatre caméras Full HD : pixels décodés identiques entre les deux runs.

Les conteneurs `.blend` et PNG restent autorisés à différer par leurs métadonnées internes.

## Gates de code

| Contrôle | Résultat |
|---|---:|
| `cargo fmt --all -- --check` | **PASS** |
| Clippy sur les quatre paquets modifiés, toutes cibles, `-D warnings` | **PASS** |
| tests `forge_world` | **57/57 PASS** |
| tests unitaires + commandes `forge_cli` | **7/7 PASS** |
| tests `forge_engine` | **91/91 PASS** |
| tests unitaires + logging `forge_runtime` | **32/32 PASS** |
| worker Blender 4.5.10 LTS, deux reconstructions | **PASS** |
| probe runtime réel final | **PASS, code 0** |

## Artefacts locaux hors Git

```text
E:\OpenRPG_Forge\.gate1-evidence-runtime\world-sketch-v4-bridge-runtime-20260901-1155-run1\
E:\OpenRPG_Forge\.gate1-evidence-runtime\world-sketch-v4-bridge-runtime-20260901-1155-run2\
E:\OpenRPG_Forge\.gate1-evidence-runtime\world-sketch-v4-bridge-runtime-20260901-1154-blender-run1\
E:\OpenRPG_Forge\.gate1-evidence-runtime\world-sketch-v4-bridge-runtime-20260901-1154-blender-run2\
E:\OpenRPG_Forge\.gate1-evidence-runtime\world-sketch-v4-bridge-runtime-20260901-113305\runtime-bridge-preview-arch-v2.png
```

## Suite explicite

Le prochain incrément gameplay doit benchmarker puis sélectionner un fournisseur de navigation sur
ce terrain et ce franchissement déjà matérialisés. Le prochain incrément artistique reste `R1-063` :
un premier kit gouverné rochers/arbres/architecture avec retopologie, UV/PBR, LOD, collisions,
turntables et revue humaine. Le pont procédural de cette preuve est une base runtime détaillée, pas
le substitut de ce kit.
