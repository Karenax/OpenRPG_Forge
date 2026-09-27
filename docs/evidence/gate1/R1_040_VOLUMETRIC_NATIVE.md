# R1-040 — Volumétrie native expérimentale : rendu non accepté

2026-09-06, Rust, local non publié. Gate 1 ouverte, CI NOT RUN.

## Implémenté

`--volumetric-preview` exige `--quality` ou `--environment-quality`, rejette les doublons,
et reste absent par défaut. La demande active les composants dérivés bornés, puis un marqueur
`VolumetricLight` sur le soleil Forge existant si ses shadow maps sont actives. Les valeurs
authorées du soleil ne changent pas. Le marqueur ajouté est retiré sur désactivation/Low ;
un marqueur préexistant n'est pas possédé et n'est pas retiré.

La première tentative ambiante seule ne pouvait pas exécuter la volumétrie :
Bevy 0.19.1 `bevy_pbr/src/volumetric_fog/render.rs`, `extract_volumetric_fog`, retire les
composants extraits lorsqu'aucune `VolumetricLight` n'existe. Ses fichiers `high.*` restent
conservés mais ne constituent pas une preuve GPU de l'effet.

## Exécution réelle après raccordement du soleil

Répertoire local : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-volumetric-native-20260906`.
Commande : `forge_runtime --world-sketch-visual-valley --quality high --volumetric-preview
--capture <dir>/high-lit.png --capture-frame 180 --metrics <dir>/high-lit.json
--metrics-warmup-frames 300 --exit-after-frames 720`.

Code 0, zéro ERROR dans le journal, capture PNG 1920×1080 reçue et inspectée.
RX 7800 XT/Vulkan, sans kit externe. Un volume CPU et 64 étapes demandées, sources inchangées,
statut expérimental conservé. Pas de diagnostic GPU séparé portant le nom volumétrie dans
ce rapport : la différence visible ne fournit pas un timing isolé de la passe.

**Rejet visuel** : assombrissement excessif et frontière géométrique visible dans le ciel.
Le volume dur et le cumul avec le brouillard de distance ne sont pas qualifiés. La capture
ne prouve ni des rayons lumineux satisfaisants, ni la qualité Cadle, ni une performance acquise.
Les 420 échantillons sont un smoke court, pas un benchmark de qualification.

SHA-256 :

- Binaire : `c4d7c1443034d92fdfa7096607bfdb5956512dc6f30af94ec8c96c38391837ef`.
- PNG : `0e3a968bba92facc3eb497469629b585b1c4f8d4d3f2f1452bd553bc335f4330`.
- JSON : `f01443de77983c1a94da7394134668698d596efded15a90071bb0aa2a29e93f0`.
- Log : `972c7a264b099930d2dedc03fce42391a69b0b0b1d62d4e931873acff8d519ca`.

## Suite requise

Atténuation spatiale du volume et traitement explicite du cumul de brouillard, puis nouvelle
preuve native et WebGPU. Aucune activation par défaut ni validation de preset complet.
Clippy workspace PASS après ajout CLI et soleil ; tests CLI et ECS couvrent les invariants.
`cargo test --workspace --locked` : code 0, 485 tests PASS (somme des suites affichées).

## Correctif de frontière — 2026-09-06

Texture procédurale dérivée 32³ R8Unorm, 32 Kio de texels, cache partagé unique. Produit de
trois smoothsteps sur le quart extérieur de chaque axe : zéro sur les six faces, densité
authorée conservée au centre. Pas d'asset externe, pas de mutation source. Test de déterminisme,
faces, centre, monotonie et réutilisation du même handle.

La capture intermédiaire `high-soft.png` garde une ligne dans le ciel. Inspection du shader
Bevy `volumetric_fog.wgsl` : `accumulated_color` initialise un terme ambiant indépendant de la
texture de densité. La prévisualisation force désormais **ambient_intensity = 0**, explicitement,
sans modifier le document. Ce n'est donc pas une projection complète de l'intention ambiante.

Capture finale `high-soft-sun.png` inspectée : la ligne du ciel disparaît sur cette vue, mais
l'assombrissement central reste excessif. **Correction locale du bord, pas acceptation du rendu.**
Même commande et GPU que `high-lit`, noms de sortie `high-soft-sun.*`, code 0, zéro ERROR.
Hashes terrain/environnement identiques vérifiés par assertions ; un volume, preset toujours partiel.
Pas de preuve caméra mobile ou WebGPU exécuté, pas de benchmark comparatif revendiqué.

- Binaire : `88596a4db06c4a673d1ef1dcb9f5d9e1bfe43fbc8d69ab3072706a2b9dcffd92`.
- PNG : `df7061be559a761d2a79563e63e91027e9a5b87d704a9adee0334e1d30d482ed`.
- JSON : `7e38e783b49f905d8558ff1b0f11dec0c9abe3341b049e19e002146a4fb76d91`.
- Log : `b8bd0b18fcf78319e649d2ce257b56bd34f4b0b44485b981491b20857c6d7f6b`.

Clippy workspace PASS après correctif. Restent le cumul avec le brouillard de distance et la
couverture d'ombres du soleil (la volumétrie porte plus loin que les cascades High), puis les
qualifications native en mouvement et WebGPU. Aucune modification des budgets d'ombres pour
masquer ce problème, aucune activation par défaut.
Quatre tests volumétrie PASS après correctif final ; check moteur WASM avec feature `web` PASS.

## Borne des cascades et comparaison au sol — 2026-09-06

La préparation exige désormais une configuration de cascades présente, finie et positive.
Distance effective = min(distance authorée, budget environnement, dernière cascade du soleil).
Les ombres ne sont ni étendues ni réécrites. Le champ optionnel
`environment.volumetric.effective_maximum_distance_m` distingue cette borne du budget demandé.
Cette borne géométrique n'est pas une preuve de couverture de chaque texel des shadow maps.

Comparaison `ground-before.*` (binaire 88596a4d…) puis `ground-bounded.*` dans le même dossier,
avec les arguments du smoke High et, dans les deux cas :

```text
--asset-kit-build E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-063-authored-preservation-20260905-b/rebuild-1/asset-kit-build.v1.json
--asset-kit-preview E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-vegetation-normals-after-20260905/human_bridge-clear/preview.json
```

Deux captures 1920×1080 inspectées, caméra human_bridge au sol. Après correction : scène moins
sombre, volume toujours présent, 64 étapes et borne effective 128 m (budget demandé 320 m).
Pas de frontière dure visible sur cette vue. Code 0, zéro ERROR après correction. Assertions
de conservation des hashes terrain/environnement/végétation et preset toujours partiel : PASS.
Ce n'est pas une disparition de l'effet obtenue en plaçant la caméra hors du volume.
L'image reste stylisée ; mouvement, rayons lumineux, autres presets et WebGPU non qualifiés.
Les FPS affichés ne constituent pas une comparaison de performance homologuée.

- Binaire après : `6fb988fe68bb4ece15261d8d21149cb02c72d7563d6f2c6f5aca23bc54d14021`.
- PNG : `b5b58393473e514fe633e35c2ddc5d3d3a7948214daa9a210addaac8e141e982`.
- JSON : `21525f47a35d1fdbb9cb793d95c1048a07c89093275ef29d70934d28c2a90f2b`.
- Log : `29daa6cb114e8e1a0a3616f202979710b1c430b9e3651f1bc4a3ebb61bff5492`.

Clippy workspace et quatre tests volumétrie PASS. Aucun passage de gate ni activation par défaut.
Validation finale du lot : 486 tests workspace PASS, check moteur WASM/web PASS et diff sans
erreur d'espacement. Ces contrôles ne remplacent pas l'exécution graphique WebGPU restante.
