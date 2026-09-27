# Gate 1 — Preuve R1-060/R1-061 masques sémantiques et carving

Date de validation locale : **2026-09-01**  
Branche : **`Rust`**  
Statut Git : **arbre de travail local non publié**

## Verdict borné

**PASS LOCAL POUR R1-060 ET R1-061.** Les cinq régions du WorldSketch produisent des couches de
masque indépendantes et feathered ; route, rivière et ruisseau produisent un heightfield réellement
sculpté, des berges et une exclusion de végétation. Deux reconstructions Blender 4.5 Full HD ont été
inspectées et reproduisent le même GLB, les mêmes atlas et les mêmes pixels décodés.

Ce verdict qualifie les **données dérivées et leur matérialisation Blender**. Il ne qualifie pas les
collisions, la navigation, la traversabilité, le raccord au document `forge.water`, l'intégration
Bevy, les performances runtime ou une qualité artistique AAA.

## Contrats et frontières

Le document canonique reste `examples/visual_valley/world-sketch.v1.json`, révision 1, SHA-256
canonique `46f1d766a9dc35af75d172a7d1700af95dda397ba956ceba0e23318df49f3839`.

La compilation ajoute deux caches moteur-neutres :

- `forge.world-semantic-masks` v1 : grille `257 × 257`, une couche `u8` par région, confidence et
  feathering versionné de 6 à 12 m ;
- `forge.world-spline-carving` v1 : grille sculptée, centrelines échantillonnées, masques route,
  lit d'eau, berges et exclusion végétale, avec coupes/remblais bornés.

Les polygones et splines du WorldSketch restent les sources éditables. Le cache de carving ne
remplace pas le document `forge.water` existant et ne crée pas implicitement une rivière runtime.

| Sortie canonique | SHA-256 |
|---|---|
| terrain | `9f16780a1de1e9dd4b65931624a8b45281713829434292bada320a578420b2ef` |
| végétation | `5c7619a6ee04533accbf998e3c12077363e4e3b06c86d28e9a62b76bae80a5b8` |
| masques sémantiques | `da149cbed209de9284c950f5961bae37da9f5186fc29b781807f264b20336cec` |
| carving des splines | `7d116754c18271e7780743c028277584482aa7cf3c007abdfa95525eccf3bca5` |
| entrée Blender v2 | `007bebc79f68797af5f29418df81c009219eda6f30acfed55b01e29f1c1856c8` |

## Matérialisation mesurée

| Élément | Valeur observée |
|---|---:|
| couches sémantiques | 5 |
| échantillons de terrain modifiés | 8 722 |
| échantillons d'exclusion végétale | 9 341 |
| coupe maximale | −5,000001 m |
| remblai maximal | +1,0 m |
| route | 61 points ; 1 974 cellules affectées ; épaules 4 m |
| ruisseau | 37 points ; 617 cellules affectées ; lit 1,4 m ; berges 6 m |
| rivière | 73 points ; 6 288 cellules affectées ; lit 2,8 m ; berges 12 m |

Le worker Blender version 5 consomme la grille sculptée, peint le terrain par couleur de sommet,
place l'eau aux altitudes de surface et refuse le scatter dans le masque d'exclusion. Le contrôle
GLB confirme `COLOR_0` sur la primitive terrain.

## Reproductibilité

Trois compilations dans des dossiers neufs ont produit des fichiers identiques octet par octet :

| Fichier | SHA-256 brut | hashes uniques sur 3 runs |
|---|---|---:|
| `terrain.v1.json` | `38a59c26bfc2aee5d099efb2965ec8c538bfc9a850618a2e9e61d3f713f4994f` | 1 |
| `vegetation.v1.json` | `3e75bd5218fe1c14e9fdd287774cfd2f585c0b0cb2ca844691840b9281ff0d5b` | 1 |
| `semantic-masks.v1.json` | `481c520eb8e0a93d6bd0f896650f7223bb3e01c928cda004a8c78164709ccbb1` | 1 |
| `spline-carving.v1.json` | `4b973e2a71013b4f2bccaeb9c42a00c1e5ef0a55d9a79f2b750997766d3db462` | 1 |
| `blender-input.v2.json` | `09c5a5f29360ac5433d7ba2ee1d1d29da52a417460f92d4959404274f769314c` | 1 |
| `world-sketch.compilation.v2.json` | `5776ca5933bb65186b2bd3f03564dd31c9f912d70786334048eefba09b08d2be` | 1 |

Deux reconstructions avec Blender **4.5.10 LTS**, terrain au pas de 2 m et captures
`1920 × 1080`, ont produit :

- terrain : 66 049 sommets et 65 536 polygones ;
- GLB identique octet par octet :
  `f92779cbe7814fe17842b6444d5dc76d37fcb96d3856dd56587d2a36cb5d0ea5` ;
- atlas sémantique identique :
  `61b0997d6dbec82a4fd131ee788607d69b3e5305bb77c05535ac1c369d22d247` ;
- atlas carving identique :
  `578abb3103118b25da81da3d625388f540f053ccfc0ec480ec9318c4b5930804` ;
- quatre captures et deux atlas décodés par Blender : pixels RGBA strictement identiques,
  différence maximale `0,0`, zéro canal modifié.

Les `.blend` et conteneurs PNG ne sont pas présentés comme binaires identiques, car leurs métadonnées
internes peuvent varier. La preuve porte sur le GLB, les atlas bruts et les pixels décodés.

## Inspection visuelle

Les six images ont été inspectées à leur résolution originale. Les cinq formes de masque sont
distinctes et leur feathering ne présente pas de rupture franche. Dans les vues héroïque, overview et
hauteur humaine, la route suit un profil aplani, rivière et ruisseau occupent des lits creusés, les
bandes de berges sont visibles et les arbres restent hors des emprises.

Une première reconstruction à 4 m a été rejetée car ses bords étaient trop crénelés et la couleur de
sommet n'était pas exportée dans le GLB. La preuve finale utilise le pas de 2 m et conserve `COLOR_0`.

Les limites visibles restent assumées : eau sans simulation, montagnes et bâtiments de prévisualisation,
pas de kit PBR gouverné final, pas de LOD/HLOD et aucune preuve runtime. Ce résultat améliore le
pipeline de terrain ; il ne transforme pas le greybox en carte AAA.

## Gates de code

| Contrôle | Résultat |
|---|---:|
| `cargo fmt --all -- --check` | **PASS** |
| Clippy `forge_world` + `forge_cli`, toutes cibles, `-D warnings` | **PASS** |
| tests `forge_world` | **57/57 PASS** |
| tests unitaires + commandes `forge_cli` | **7/7 PASS** |
| total ciblé | **64/64 PASS** |
| syntaxe du worker Python | **PASS** |

## Artefacts locaux hors Git

```text
E:\OpenRPG_Forge\.gate1-evidence-runtime\world-sketch-v2-masks-carving-20260901-084103\
E:\OpenRPG_Forge\.gate1-evidence-runtime\world-sketch-v2-masks-carving-20260901-084103-run2\
E:\OpenRPG_Forge\.gate1-evidence-runtime\world-sketch-v2-masks-carving-20260901-084103-run3\
E:\OpenRPG_Forge\.gate1-evidence-runtime\world-sketch-v2-blender-20260901-084103-fhd-run2\
E:\OpenRPG_Forge\.gate1-evidence-runtime\world-sketch-v2-blender-20260901-084103-fhd-run3\
```
