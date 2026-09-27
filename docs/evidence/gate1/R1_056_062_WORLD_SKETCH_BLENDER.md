# Gate 1 — Preuve R1-056 à R1-062 WorldSketch et greybox Blender

Date de validation locale : **2026-08-31**  
Branche : **`Rust`**  
Statut Git : **arbre de travail local non publié**

## Verdict borné

**PASS LOCAL POUR R1-056 À R1-059 ET R1-062.** Le pipeline prend deux références originales,
valide un `forge.world-sketch` strict, compile trois fois les mêmes documents Forge et reconstruit
deux fois une greybox Blender 4.5 avec un GLB identique et quatre captures aux pixels identiques.

Cette preuve ne ferme pas Gate 1. Elle ne qualifie ni `R1-060`, `R1-061`, `R1-063` à `R1-065`, ni
les collisions, la navigation, l'intégration Bevy, les performances ou la qualité artistique AAA.

## Entrées gouvernées

Le pilote `examples/visual_valley/world-sketch.v1.json`, révision 1, couvre 512 × 512 m : deux images,
cinq régions sémantiques, trois splines, cinq landmarks, deux zones d'incertitude, six verrous humains
et quatre caméras fixes. Les prompts et la provenance sont conservés dans
`examples/visual_valley/references/`.

| Objet canonique | SHA-256 |
|---|---|
| WorldSketch | `46f1d766a9dc35af75d172a7d1700af95dda397ba956ceba0e23318df49f3839` |
| terrain compilé | `9f16780a1de1e9dd4b65931624a8b45281713829434292bada320a578420b2ef` |
| végétation compilée | `5c7619a6ee04533accbf998e3c12077363e4e3b06c86d28e9a62b76bae80a5b8` |
| entrée Blender compilée | `66901d4f00ad3a5e2dde921e6ae705b1c0199d8fde71810530d966c9f38c1b9f` |

## Validation et compilation déterministes

Les tests ciblés observés sont :

| Contrôle | Résultat |
|---|---:|
| `cargo test -j1 -p forge_world world_sketch --locked` | **5/5 PASS** |
| tests unitaires `forge_cli` | **3/3 PASS** |
| tests de commandes `forge_cli` | **4/4 PASS** |
| replay `cargo test -j1 -p forge_world -p forge_cli --locked` | **59/59 PASS** |
| Clippy des deux paquets, toutes cibles, `-D warnings` | **PASS** |
| validation réelle des deux PNG, de leurs hashes et dimensions | **PASS** |
| compilation réelle dans trois dossiers neufs | **PASS** |

Les quatre fichiers décodés sont identiques octet par octet dans les trois dossiers :

| Fichier dérivé | SHA-256 du fichier | hashes uniques sur 3 runs |
|---|---|---:|
| `terrain.v1.json` | `38a59c26bfc2aee5d099efb2965ec8c538bfc9a850618a2e9e61d3f713f4994f` | 1 |
| `vegetation.v1.json` | `3e75bd5218fe1c14e9fdd287774cfd2f585c0b0cb2ca844691840b9281ff0d5b` | 1 |
| `blender-input.v1.json` | `a82d5a8f25babe16d6d9b4e373fc8f808cdec8aa9c9f9e143dd9a15e375ae322` | 1 |
| `world-sketch.compilation.v1.json` | `968685d9cc726c0fd07d9954fd0db5276237ab97c5d64ad9f0c74246437fae8f` | 1 |

## Reconstruction Blender 4.5

Le worker `scripts/blender/Build-VisualValleyGreybox.py`, version 3, a été lancé deux fois en mode
headless avec Blender **4.5.10 LTS**, en 1920 × 1080. Il produit une scène non canonique contenant :

- un terrain de 16 641 sommets et 16 384 polygones ;
- trois rubans de spline ;
- quatorze maisons, le pont, le keep et la cascade ;
- 260 arbres et 140 rochers procéduraux ;
- les quatre caméras `reference_match`, `human_height`, `landmark` et `overview` ;
- un `.blend`, un GLB, les captures et un manifeste de provenance/hashes.

Le GLB des deux reconstructions est identique octet par octet :
`24cf47477694a7caff1611a4f749b35c349c21bf64a7d96162390f9c0ef45696`.

Les conteneurs PNG possèdent des hashes différents à cause de métadonnées Blender. Leur contenu a
donc été décodé par Blender et comparé canal par canal : les quatre images ont une différence maximale
de **0,0** et sont strictement égales. Les `.blend` ne sont pas présentés comme binaires identiques ;
la reconstruction repose sur le WorldSketch, le worker versionné et les sorties qualifiées.

Artefacts locaux principaux, volontairement hors Git :

```text
E:\OpenRPG_Forge\.gate1-evidence-runtime\world-sketch-v1-blender-input-20260831-225328\
E:\OpenRPG_Forge\.gate1-evidence-runtime\world-sketch-v1-blender-greybox-20260831-225328-fhd\
E:\OpenRPG_Forge\.gate1-evidence-runtime\world-sketch-v1-blender-greybox-20260831-225328-fhd-run2\
```

## Inspection visuelle et limites

Les quatre captures Full HD ont été inspectées à leur résolution originale. Terrain, village, pont,
keep, rivière, ruisseau, zone humide, cascade, arbres et rochers sont lisibles. Une première passe
noire due à un volume mondial infini a été rejetée, puis le cadrage du WorldSketch et l'éclairage ont
été corrigés avant la preuve finale.

Le résultat est un **greybox procédural texturé**, pas une carte AAA terminée. Les limites visibles et
fonctionnelles restent explicites : blending de matériaux encore grossier, montagnes et architecture
de prévisualisation, eau sans simulation, splines non sculptées dans le terrain, absence de collisions
et navigation dérivées, aucun kit d'assets final, aucun LOD/HLOD et aucune preuve runtime Bevy de ces
sorties. À la date de cette preuve, ces travaux correspondaient aux items encore ouverts `R1-060`,
`R1-061` et `R1-063` à `R1-065`.

Suite qualifiée le 1er septembre 2026 : `R1-060` et `R1-061` sont couverts par
`docs/evidence/gate1/R1_060_061_SEMANTIC_MASKS_SPLINE_CARVING.md`. Le présent document reste le
snapshot historique du worker version 3 et de l'entrée Blender v1.
