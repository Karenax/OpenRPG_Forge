# Gate 1 — Pluie native et réversibilité météo

## Actualisation du 8 septembre — R1-025 clos fonctionnellement

La réversibilité n'est plus seulement headless : le cycle natif du 7 septembre
(`R1_WEATHER_NATIVE_CYCLE.md`) fournit trois captures et les extrémités
0 → 0,7799999713897705 → 0, avec sources météo/environnement constantes.
Le journal et les trois PNG ont été recontrôlés le 8 septembre : leurs SHA-256
correspondent tous au rapport. Il s'agit d'une relecture, pas d'un nouveau run.
Le cycle Tauri du 8 septembre (`R1_TAURI_WEATHER_AND_METRICS.md`) ajoute le
retour sec observé et diagnostiqué depuis les contrôles du produit, sans reload,
avec uniformes concordants, six hashes constants et une seule initialisation.
Le test partagé de cycles sans remplacement des meshes/sources reste présent
et couvert par les 616 PASS du lot de continuité d'éclairage.

Ces preuves ferment **R1-025 comme capacité de wetness réversible**, sans
attendre un nouveau benchmark ni une finition artistique pour établir cette
propriété. Le natif emploie un candidat solaire opt-in, Tauri le profil embarqué :
ce n'est pas une preuve de parité optique à entrées identiques. Reflets naturels,
météo cohérente, vidéo/fluidité et acceptation humaine restent non qualifiés.
R1-024, R1-026 et Gate 1 restent ouverts. Aucun critère global n'est supprimé.

## Historique

Date : 2026-09-05. Branche `Rust`, local non publié. R1-024 à R1-026 restent ouverts.

## Mise à jour du 6 septembre — réponse de rugosité partagée

La série `r1-twelve-views-20260906` a révélé des reflets très forts sur les collines
mouillées. Le profil fournit `full_wet_roughness_multiplier=0.32` ; l'ancien shader
multipliait directement la rugosité perceptuelle par `mix(1, 0.32, wetness)`.
Le code local Bevy 0.19.1 `bevy_pbr/src/render/pbr_lighting.wgsl`, fonction
`perceptualRoughnessToRoughness`, confirme que Bevy élève ensuite cette valeur au carré
(avec son plancher de précision). À wetness 0,78, cela réduit alpha par environ
0,2205, et non 0,4696.

L'interprétation du paramètre est désormais explicitement la rugosité microfacette alpha.
Le terrain, les berges utilisant son matériau, la végétation procédurale et le feuillage
gouverné appliquent `sqrt(mix(1, multiplier, wetness))` à la rugosité perceptuelle.
Il s'agit d'un ajustement volontaire du rendu mouillé existant, pas de la preuve que
l'ancien choix artistique violait une spécification physique. Aucun champ JSON ajouté,
aucune valeur de recette changée, aucune coordonnée de carte dans la règle. Le comportement
sec, l'albédo, l'alpha de découpe, les textures, la géométrie et les colliders ne changent pas.
Ce n'est pas une simulation de porosité, de flaques ou de film d'eau.

Vérification ciblée : 13 tests météo PASS, dont nouvelle référence CPU sur plusieurs
rugosités/facteurs et gardes du code shader. Cette référence vérifie identité à sec,
monotonie et calcul alpha avant les planchers du renderer ; elle n'exécute pas le WGSL.
Build runtime PASS. Deux lancements GPU natifs clair/pluie terminés 0, sans ERROR,
captures 1080p frame 900/sortie 1800, mêmes données/caméra `keep_landmark`/High partiel
et kit/forêt que la série de douze. Les deux images ont été ouvertes et inspectées.

Preuves : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-wet-roughness-20260906`.
Manifeste SHA : `49fa5d13a5f705a430c07b558f5fb25d4c1dfff12cfb823bc14bbbe8c42e21ff`.
Runtime SHA : `e86b403714386d3007627b7e77d81dbf2fe5cb67f1d853862aba4cc77cb11c3e`.
PNG clair : `3c3da6713573971dcae851275815b123f5d402867f9f62dba9cac5caadab0bd6`.
PNG pluie : `c2c654423f58a457338bef0885cf06cf9339fa69bf6d62cb9ccdfdc033367e2b`.

Comparaison avec les mêmes poses de `r1-twelve-views-20260906` : aspect sec conservé
visuellement, larges reflets pluie atténués mais toujours présents. Le sol n'est pas
encore qualifié naturel. Pas de comparaison pixel exacte (vent/horloge/HUD), de mesure
de gain de performance, de nouveau rendu Web/Tauri ni de validation humaine.
Les douze vues précédentes restent intactes et historiques pour l'ancienne réponse
mouillée ; ces deux corrections ne constituent pas une nouvelle série de douze.

Validation élargie terminée : `cargo test --workspace --all-features` : **461 PASS,
1 sonde CPU de timing ignorée explicitement**. Clippy workspace tous targets/features
avec `-D warnings` PASS ; Clippy Studio WASM/Web avec `-D warnings` PASS, avertissement
de compatibilité future connu `proc-macro-error2 2.0.1`. Format et diff check PASS.
Journaux dans le même dossier externe :

- `workspace-tests.log` SHA : `1fe826eddf1f6d5b82e6621e35ee81c34a81690080498e6d09b994eabce87f93` ;
- `clippy.log` SHA : `36b728a5ed166d7f5a870928917f57f29b97e13c58e23bcdb5bf9e0936e15f40` ;
- `wasm-clippy.log` SHA : `ab84e5daf9f0479c06cd6f649a84fef7b180e63111cb473b3f231fed3e39717c`.

La compilation WASM ne compile pas les pipelines WGSL sur un GPU navigateur ; cette
qualification visuelle reste ouverte. Aucun nouveau contrôle d'auteur Studio livré.
Gate 1 ouverte, CI distante NOT RUN sous ADR-0006, aucun push.

## Rendu réellement observé

### Diagnostic courant du 7 septembre : éclairage et pluie indépendants

Inspection du code actuel : le profil météo porte pluie, mouillure et post-traitement,
mais aucun réglage d'éclairage en fonction de la météo. `environment::spawn` installe
l'illuminance du document d'environnement ; les systèmes météo ne modifient pas le soleil.
L'ajustement de rugosité en espace alpha décrit plus haut est déjà présent dans les shaders
terrain/végétation : ne pas le refaire à partir des images antérieures à cette correction.

Lecture des deux métriques conservées de `r1-wet-roughness-20260906/keep_landmark-*` :

| Observation | Clair | Pluie |
|---|---:|---:|
| Soleil (lux) | 55 000 | 55 000 |
| Mouillure | 0 | 0,78 |
| Exposition du color grading (EV) | 0 | -0,35 |
| Ombres / disque solaire | actifs / actif | actifs / actif |

Couleur linéaire solaire `[1, 0.8480884, 0.5704816, 1]` et direction identiques.
Le hash terrain est identique ; la capture pluie corrigée a été réouverte et inspectée :
reflets larges et ombres directionnelles fortes restent visibles. Ces relevés sont des
preuves historiques réinspectées, **pas de nouveaux runs** ni une mesure causale isolant
la contribution exacte du soleil aux reflets. Une averse ensoleillée peut être valide.
Le manque fonctionnel est l'absence d'une réponse d'éclairage authorable pour une pluie
couverte, pas un nouveau défaut démontré de la formule de rugosité.

SHA-256 des métriques : clair
`81EC1521CCD1680B392EE3A8E2C90E29173D599695BD700DCCC67989462FF8AC`, pluie
`7AA94C841405C8E6702A66468D9CF59432922DDD5B40FAF36BA8E8AD276C8C26`.

Suite d'implémentation identifiée, **non implémentée** : réponse solaire météo explicite
et moteur-neutre, opt-in afin de conserver les anciens profils/hashes, bornée et liée à
l'identité de zone. La projection doit repartir de l'éclairage authoré (pas multiplier
la valeur précédente), conserver l'identité du soleil et restaurer exactement le clair.
La transition d'éclairage doit être distinguée du temps de séchage des matériaux ; pas
de ciel clair maintenu sombre uniquement parce que le sol reste mouillé. Le diagnostic
devra séparer source et lumière effective. Vérifier clair/pluie/clair, source invalide,
absence de météo, restauration et rendu natif/Web avant toute qualification.

Pas de correction arbitraire des sources ou des shaders dans ce diagnostic, ni de nouveau
build/test/benchmark. Aucun nuage volumétrique, masque d'abri ou réglage Studio annoncé.
Ce complément cible R1-019/R1-024 ; Gate 1 reste ouverte, aucune publication Git.

Run natif du 12:59:52 UTC, sortie 0, RX 7800 XT/Vulkan, 1920 × 1080, profil High.
Binaire identique au lot d'éclairage, SHA-256
`af1c2021490cb2454b203e313c0c7a64a82d5b56f98f100dd4c4d86d4ab5b69c`.
Reprendre la commande de `R1_ENVIRONMENT_WHITE_BALANCE_IBL.md`, ajouter `--weather rain`
et utiliser ces sorties externes (un nouveau dossier pour tout nouveau replay) :

```text
E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-weather-native-20260905\rain.png
E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-weather-native-20260905\rain-metrics.json
```

Capture frame 300, préchauffage 180, sortie 420. WorldSketch candidat non promu, kit corrigé,
aperçu, terrain et pont inchangés. La référence claire est la capture du lot d'éclairage.

Capture inspectée : traînées de pluie visibles devant le ciel et le terrain ; palette plus
froide/désaturée, terrain assombri et reflets de mouillage visibles. Le soleil et le ciel
restent ceux du profil environnement : ce n'est pas une simulation d'orage avec nuages.
Certaines traînées proches sont très longues à l'écran. Qualité artistique non acceptée.

Le ledger observe un lot de 8 192 gouttes, 65 536 sommets, 32 768 triangles, opacité 0,42,
zéro entité par goutte, suivi caméra actif. Mouillage 0,78 ; uniformes terrain/végétation
conformes. Grade : exposition -0,35 EV, température -0,006, teinte 0,001,
saturation 0,86, contraste 1,08, gamma 1.

La passe transparente expose 416 424 primitives et 55 202 invocations fragment sur la dernière
observation. Ces comptes incluent d'autres objets transparents ; ils ne sont pas attribués
exclusivement à la pluie. Le flag météo `gpu_execution_observed=false` reste honnête :
le ledger seul ne mesure pas une passe pluie isolée. La preuve visuelle est la capture externe.

239 échantillons, moyenne 8,516 ms / 117,43 FPS, p95 9,414 ms, 1 % low 49,61 FPS ;
un pic à 39,143 ms. Smoke d'environ deux secondes, pas benchmark final de gate.
Warnings Vulkan AMD et `@invariant` toujours présents. Aucun crash constaté.

SHA-256 : capture `83f0111a703daf6974e1c5ad69f1cd1f400305d84897f2a44f9cdf5db9bf9c21` ;
métriques `c8d6221c9e4a9ce52068fc0da1efe3290b54a8692e539f5df158d55b17551c63`.

## Réversibilité des systèmes Bevy

Le test headless existant de projection initiale est étendu en
`headless_plugin_cycles_weather_without_replacing_meshes_or_authored_sources`.
Il utilise les plugins, caméra, assets de matériaux et systèmes Bevy réels, sans GPU :
départ pluie, 120 pas de séchage, 60 pas de pluie, 120 pas de séchage, pas manuel de 100 ms.
À chaque palier, assertions sur l'état observé, le hash source déclaré, le lot unique,
la visibilité, les scalaires de mouillage exacts, les uniformes terrain/végétation,
les valeurs température/teinte et la conservation des IDs de meshes.
Les deux retours au sec doivent rapporter `clear_target_restored_exactly=Some(true)`.

Cette preuve ECS complète les tests existants de palettes immuables et de déterminisme.
Elle ne remplace pas un cycle animé capturé dans le runtime ou une commande Studio.
Le run natif ci-dessus démarre directement à l'état pluie, pas après une transition.

Contrôles rejoués après extension du test :

- `cargo clippy --workspace --all-targets --all-features --locked -- -D warnings` : PASS ;
- `cargo test --workspace --locked -q` : 299 PASS, aucun échec ni test ignoré ;
- `cargo fmt --all -- --check`, `git diff --check` : PASS.

Même target et variables de compilation que la preuve d'éclairage. Aucun changement de code
de production dans ce lot : le contrôle WASM précédent reste applicable ; il n'a pas été rejoué.
Première compilation du test refusée car deux métriques sont des `Option<f32>` ; assertions
corrigées pour exiger leur présence avant comparaison des bits, puis replay réussi.

## Diagnostic de profondeur conservé

Les types `VertexOutput` importés de `bevy_pbr::forward_io` et `bevy_pbr::prepass_io`
(Bevy 0.19.1, registre Cargo local) déclarent `@builtin(position)` sans `@invariant`.
Les shaders Forge les réutilisent ; le warning n'identifie pas précisément le matériau fautif.
Ce constat ne prouve pas que les pointillés végétaux viennent exclusivement de ce problème.
Aucun fork ni modification du cache Bevy, aucun assouplissement du test de profondeur,
aucun filtrage des warnings. Diagnostic renderer et transitions HLOD restent à qualifier.

## Limites et suites

Pluie native statique observée ; transition intégrée ECS testée. Animation temporelle native,
exécution Studio WebGPU et revue humaine restent dues. Collisions des gouttes, abris,
éclaboussures, flaques et normal maps mouillées sont explicitement non implémentés.
Le candidat rivière reste non promu, aucune source artistique changée, aucun commit/push.
CI distante `NOT RUN`. Gate 1 reste active ; aucune fonction Gate 2 ajoutée.
