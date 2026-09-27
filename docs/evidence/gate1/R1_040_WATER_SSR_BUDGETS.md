# R1-040 — Sous-budget SSR de l'eau

2026-09-06 local, branche Rust, non publié. **Gate 1 et R1-040 restent ouverts.**

## Implémenté et limites

`WaterQualityRequest` valide la politique et l'identité de zone, conserve son hash et permet
de changer le tier demandé sans modifier la source. Le plugin eau existant consomme cette
ressource sur une caméra 3D active unique. Aucun nouveau shader ou dépendance.
Le CLI expose `--water-quality low|medium|high|ultra` dans les deux modes vallée ; omission :
comportement authoré conservé. Valeur invalide, doublon, argument absent ou mode incompatible refusés.
Les quatre options de qualité partageant ce même enum utilisent désormais un parseur commun.

| Tier livré | Projection SSR | Limite non résolue |
| --- | --- | --- |
| Low | Composant SSR retiré | Réflexion environnement non qualifiée |
| Medium | 24 étapes linéaires | Distance et réfraction écran non appliquées |
| High | 48 étapes linéaires | Distance et réfraction écran non appliquées |
| Ultra | 64 étapes linéaires | Distance et réfraction écran non appliquées |

Bevy 0.19.1 n'expose pas de distance maximale dans son composant SSR. La surface opaque/deferred
actuelle ne fournit pas la réfraction écran demandée. La teinte authorée est conservée, pas
présentée comme une réfraction implémentée. Un avertissement `water_quality_partial` est émis
une fois par tier demandé. Un mode de désactivation complète non supporté conserve la projection
précédente et ne prétend pas réussir. Le SSR est partagé à l'échelle de la caméra, pas isolé par eau.

Le ledger `water.quality` distingue budget CPU observé et optique complète :
`full_preset_applied=false` dans tous les cas. `resolved_optics` reste l'intention authorée/préparée.
Ni acquittement du contrôleur global, ni preuve GPU déduite des seuls composants CPU.
Les paramètres SSR authorés hors étapes, le TAA existant, prépasses, géométrie, matériaux et
phase d'animation restent conservés. Aucun effet partagé retiré pour simuler un preset complet.

## Vérifié

Trois tests moteur : cycle Low/Medium/Ultra/High avec identités mesh/matériau, positions,
hashes eau/terrain, phase figée et paramètres SSR conservés ; caméra inactive ou ambiguë et
politique étrangère refusées ; désactivation complète non supportée sans faux succès.
Un test CLI couvre les niveaux, deux modes, omission et entrées invalides.

- `cargo test --workspace --locked -q` : **366 PASS**.
- `cargo clippy --workspace --all-targets --all-features --locked -- -D warnings` : PASS.
- `cargo check -p forge_engine --no-default-features --features web --target wasm32-unknown-unknown --locked` : PASS.
- `cargo build -p forge_runtime --locked` : PASS.

Target `C:/Users/karen/AppData/Local/Temp/openrpg-forge-r1-064-target`, incremental=0,
`RUSTFLAGS=-C debuginfo=0`. Aucun lancement navigateur pour ce lot.

## Exécution native

Artifacts : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-quality-20260906`.
RX 7800 XT/Vulkan, Full HD, WorldSketch rev3, caméra human_bridge, kit gouverné, météo clear.
Entrées kit/preview identiques au protocole `R1_040_TERRAIN_LOD_RENDERER.md`.
Deux lancements successifs `--world-sketch-visual-valley --weather clear --water-quality low`
puis `ultra`, avec `--capture-frame 1 --metrics-warmup-frames 600 --exit-after-frames 1020`,
`--capture` et `--metrics` vers les fichiers correspondants. Sans override terrain.

Deux codes 0, aucun événement ERROR. PNG inspectés : vallée et pont présents ; eau Low plus
lisse/pâle, Ultra plus détaillée mais encore granuleuse. Les phases diffèrent : pas de comparaison
pixel exacte ou validation artistique. Low sans diagnostic SSR ; Ultra expose `render/ssr/elapsed_gpu`
et 2073600 invocations fragment. Cela prouve une passe native, pas sa qualification complète.

| Tier | FPS moyens | p95 ms | 1 % low FPS | Frames >16,7 ms |
| --- | ---: | ---: | ---: | ---: |
| Low | 123,48 | 8,869 | 94,34 | 0 |
| Ultra | 116,37 | 9,2862 | 93,57 | 0 |

420 échantillons par run, environ 3,4/3,6 s : smoke, pas benchmark long ni gain général revendiqué.
Capture Low émise/readback 270/273, 87 pipelines prêts ; Ultra 71/74, 91 pipelines ; huit
observations stables. Changement à chaud vérifié en ECS seulement, pas sur GPU.

Les captures précèdent uniquement l'ajout du warning explicite (rendu inchangé). Le binaire final
a ensuite exécuté `--visual-valley --water-quality low --exit-after-frames 60` : code 0, exactement
un événement `water_quality_partial`, zéro ERROR (`warning.log`). Ce smoke court prouve le warning,
pas la disponibilité de tous les pipelines. Il utilise la politique de la vallée historique,
distincte de la liaison rev3 des captures.

## SHA-256

- binaire captures : `b88ec163342e23d3ec2cb33dcd9798979e683c117d715acdb284d80e93e19033` ;
- binaire final/warning : `5e66224d67a71d219f86864375eb895e9a7e7be6124f2e5eecdd347d77d03cc8` ;
- `water/quality.rs` final : `1fb55215b117abc367363c667dd7403a287adc1620ca2f338f1b4bcaed52d60d` ;
- tests : `5187078e71067f30bd18ba0a218710c3fa614528f93e54447073f19c08068f21` ;
- politique liée à rev3 : `6ae47b0ce3d513683394749b625ac2a3dcbdfcd94173a6f84f4e3070f6c94871` ;
- source eau inchangée : `0a59da647fc1e806b525c4ca7005359687ec71d0a141df991bfd4e93846d7b73` ;
- Low PNG : `04b7bdacb8f89784372a8d19764a8cda14a67f01b44bce52c77e48f345cbfe3d` ;
- Low JSON : `9a97bc190f6d2ff80d7d4192bb75030421a8ffdd1d03eff7b36397f16c40517f` ;
- Ultra PNG : `cd0c9f5dc000447065b534fb4d0a8139536c7464c443b830812b7dff613d2c3f` ;
- Ultra JSON : `6e87b3290392c08d2d54e9ac73f79bab92380f1ea26e330e2b0ed9f57a1a176a`.

## Toujours prévu, non qualifié

Optique eau complète ou fallback accepté, budgets environnement/végétation, contrôleur global,
sélection Studio, cycles GPU et qualification longue/humaine restent ouverts. Preuve locale sur
arbre modifié, pas qualification finale ADR-0006 ; CI **NOT RUN**. Aucun commit/push ou asset Cadle.

## Commande de teinte de profondeur — 2026-09-06, validation logique

Le chemin commun de qualité applique désormais `QualityWaterRefraction::Disabled` au
matériau d'eau gouverné en mettant `shallow_color_surface_tint.w` à zéro. SurfaceTint
restaure la valeur authorée ; ScreenSpace restaure le fallback authoré et reste signalé
indisponible comme vraie réfraction écran. Une seule caméra active est requise. Aucun
document, mesh, collider, phase de simulation ni matériau étranger n'est réécrit.

Observation additive : `surface_tint_strength` rapporte la valeur lue dans le matériau,
`surface_tint_projected` confronte cette valeur à la demande ; matériau manquant ou surface
ambiguë ne donne pas un succès. Le contrôle des uniforms compare le profil authoré plus
la qualité effective et la phase animée. `full_preset_applied` reste false : ni la distance
SSR, ni la réfraction écran, ni la suppression complète des réflexions ne sont résolues.

Test ECS Low personnalisé/Medium/Low/High : zéro/restauration répétés, source et matériau
étranger intacts, caméra ambiguë et matériau absent refusés. 438 tests workspace PASS,
1 timing ignoré ; Clippy strict workspace/all-targets/all-features et check Studio WASM
PASS, fmt/diff-check PASS. Le dernier changement de warning ne modifie pas le comportement
contrôlé par Clippy/WASM ; la suite workspace a été rejouée après ce changement.

Preuves : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-tint-quality-20260906/`.
`tests-qualified.log` SHA-256 `1e5762406aef1cc1737bb74070fdc1a9926d0f6d21b32548a5b3913b6e86b598`.
Les erreurs initiales de compilation du test provenaient d'un `Default` inexistant pour
l'extension eau, remplacé par une projection authorée valide ; aucune dépendance ni
implémentation Default artificielle ajoutée. Refus Clippy intermédiaires corrigés.

**Reste à vérifier sur GPU** : la désactivation/restauration de la teinte. Les presets
livrés n'utilisent pas Disabled ; ils n'ont pas été modifiés pour fabriquer une preuve.
Pas de nouveau lancement applicatif, de capture ou de nouveau bundle Trunk pour ce lot.
Il s'agit d'une capacité moteur testée, pas d'une nouvelle commande déjà accessible dans
l'interface Studio ni d'une qualification visuelle. Gate 1 reste ouverte, aucun push.

## Aperçu de teinte accessible dans Studio — 2026-09-06

Suite du lot moteur précédent : trois commandes transitoires activent, désactivent ou
restaurent le suivi du profil. Le changement de qualité conserve cet aperçu ; aucun
document ni preset JSON n'est modifié. L'interface affiche la valeur observée du matériau
et distingue explicitement cette observation CPU d'une réfraction écran qualifiée.

Test ECS supplémentaire : désactivation High, passage Low, activation Low, retour au
profil High ; valeurs 0/0/0,18/0,18, hash de politique intact. 439 tests workspace PASS,
1 timing ignoré ; Clippy strict workspace et Studio WASM PASS, build Trunk final PASS,
fmt et diff-check PASS. Erreurs intermédiaires de typage numérique et Option imbriquée
corrigées sans désactiver de contrôle. Warning de future compatibilité proc-macro-error2
2.0.1 toujours présent, distinct des warnings projet.

Preuves locales : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-water-tint-20260906/`.
Logs : `tests-final.log`, `clippy.log`, `clippy-web-final.log`, `build-web-final.log`.
SHA-256 tests : `cfe055e47361f35e52a03fb534c4fef8d505bb96cec2b4a544bb1ef0af7ecb3b`.
WASM final : `5faff9fafdfc2451b4c949835a553a9b0e25b8e3c084cbb5977f8866c8872289`.

Parcours réel Chrome sur `?forge_scene=visual-valley` (vallée Rust intégrée, pas le
WorldSketch rev3 ni la forêt gouvernée) : désactiver donne 0,000 ; passer Low conserve
0,000 ; activer donne 0,180 ; suivre le profil restaure « Profil · teinte 0.180 ».
Le monde reste initialisé une seule fois. Après correction CSS de largeur, nouveau
bundle final rechargé : profil High restauré au démarrage, trois libellés lisibles sur
toute la largeur de l'inspecteur. Capture inline inspectée, viewport 2048 × 1101 ;
pas de PNG local archivé pour ce contrôle. Le WASM est identique avant/après ce CSS.

**Limites** : eau hors du champ de cette capture ; le cycle UI → matériau CPU est
prouvé mais pas la différence des pixels d'eau. Pas de nouvelle qualification native,
Tauri, performance, artistique ou de réfraction écran. Gate 1 et R1-040 restent ouverts,
CI NOT RUN sous ADR-0006 ; aucune publication Git ni copie d'asset Cadle.

## Désactivation des réflexions — 2026-09-06, projection CPU

Le mode `QualityWaterReflection::Disabled`, auparavant explicitement sans effet,
retire maintenant le composant SSR de la caméra active unique et projette zéro dans
`optical_parameters.w` du seul matériau d'eau gouverné. Environment et ScreenSpace
restaurent la réflectance authorée. Le chemin Environment retirait déjà le SSR de la
caméra : cette suppression est à l'échelle de la vue, pas un masque SSR par objet.
La réflectance, elle, n'est modifiée que pour l'eau. Les composants TAA/prepasses,
les autres matériaux et les sources restent intacts.

Fondement inspecté dans les sources locales épinglées Bevy 0.19.1, sans copie de code :
`pbr_functions.wgsl::calculate_F0_dielectric` calcule F0 à partir de la réflectance ;
`environment_map.wgsl::environment_map_light` applique une occlusion spéculaire issue
de F0, y compris au terme de Fresnel. Le shader Forge force metallic à zéro, transmet
la réflectance dans ses chemins forward/deferred, et le matériau préparé n'a pas de
clearcoat (invariants testés). Le changement ne remplace pas le matériau par un unlit
et ne retire pas l'environnement de la scène. Ce raisonnement de source ne constitue
pas une preuve de rendu GPU ni de performance.

`material_reflectance` observe la valeur de l'extension effectivement liée à la surface ;
`material_reflectance_projected` vérifie la demande avec une caméra active unique.
La présence du budget caméra SSR est rapportée séparément : un matériau absent ne
peut pas être déclaré projeté même si le composant SSR a bien été retiré.
`full_preset_applied` reste false ; distance SSR et réfraction écran restent incomplètes.

Tests : cycle Low personnalisé Disabled / Medium / Disabled / High, restauration
exacte et source/hash inchangés, matériau étranger conservé, TAA/prepasses préservés,
matériau absent et deux caméras actives. **440 tests workspace PASS, 1 timing ignoré** ;
Clippy strict workspace/all-targets/all-features et Studio WASM PASS, fmt/diff-check PASS.
Le premier Clippy a refusé quatre booléens : exception locale documentée car ces champs
sont des preuves indépendantes, pas des états exclusifs ; aucun contrôle global retiré.
La suite précède cette seule annotation sans effet exécutable. Warning futur
proc-macro-error2 2.0.1 toujours présent.

Preuves locales : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-reflection-disable-20260906/` ;
`tests.log`, `clippy.log` (refus conservé), `clippy-final.log`, `clippy-web.log`.
SHA-256 `tests.log` : `b92c14c50658566596c1e77e7d3c53b2c3e02bd2187bd27f7080ee97d2733d83`.

**Non vérifié / prochaine qualification** : cycle GPU off/restauration avec eau visible,
environnement et mêmes entrées/caméra, contrôle des autres surfaces. Aucun nouveau
contrôle de réflexion Studio, aucun preset livré modifié pour ce test et aucun nouveau
build applicatif/capture dans ce lot. Les captures précédentes ne prouvent pas ce mode.
Gate 1 reste ouverte, CI NOT RUN ; aucun commit/push ni asset Cadle.

## Aperçu des reflets dans Studio — 2026-09-06

Deux commandes désactivent les reflets ou reprennent le profil sélectionné. La requête
moteur expose une surcharge transitoire dédiée ; elle annule les budgets de ray marching
quand Disabled est demandé, persiste après changement de tier et ne modifie aucun preset.
Teinte et reflets ont des requêtes UI en attente distinctes : deux clics rapprochés sur
ces réglages indépendants ne s'écrasent pas mutuellement avant la frame suivante.

Test ECS ajouté : reflets désactivés High puis Low, restauration High avec SSR 48 pas,
teinte maintenue à zéro pendant tout le cycle, hash inchangé et aucune qualification
complète implicite. **441 tests workspace PASS, 1 timing ignoré**, Clippy workspace strict
et Studio WASM PASS, build Trunk final PASS, fmt/diff-check PASS. Refus Clippy WASM initial
sur un point-virgule corrigé ; les deux boîtes de requêtes ont ensuite remplacé le slot
commun. Clippy WASM et bundle ont été rejoués après cette dernière correction UI.
Warning de future compatibilité proc-macro-error2 2.0.1 conservé.

Racine : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-reflections-20260906/`.
Logs : `tests.log`, `clippy.log`, `clippy-web-final.log`, `build-web-final.log`.
SHA-256 tests : `1c2f61d41b61ca96976542d14f0ecd14302d6342762ab85333310bee963968d1`.
WASM final : `bcf5eb952c9bb28ace0520bc4b461c92a6fec575a3bdbf7263c0371773489bbf`.

Cycle réel Chrome, `?forge_scene=world-sketch-visual-valley`, kit externe non demandé :
High initial réflectance 0,360 ; désactiver donne 0,000 ; passer Low conserve 0,000 ;
désactiver la teinte donne également 0,000 sans changer les reflets ; suivre les reflets
restaure 0,360 sans réactiver la teinte. Retour High et suivi du profil de teinte :
0,360 / 0,180, hashes alignés et une seule caméra/initialisation du monde. Aucun message
console de niveau error dans le relevé final. Capture inline inspectée, viewport
2048 × 1101 ; libellés visibles dans l'inspecteur défilé, pas de PNG local archivé.

Le premier clic de contrôle pendant la capture du pointeur n'avait pas changé l'état ;
rechargement puis cycle hors exploration vérifié. La tentative de touche maintenue via
CDP a été refusée comme méthode non prise en charge ; aucune navigation du personnage
ni revue des gestes humains ne sont revendiquées. L'eau reste trop distante pour une
comparaison des reflets : **preuve UI → CPU, pas preuve des pixels d'eau**. Un cycle
natif capturé près de l'eau reste à établir ; ne pas prolonger les essais de navigation
navigateur pour remplacer cette preuve. Tauri, optique complète et Gate 1 restent ouverts.
Aucune publication, aucun asset Cadle, aucune source du projet sauvegardée par cet aperçu.

## Cycle natif eau visible — 2026-09-06

Domaine Water ajouté au protocole natif existant :
`--water-quality high --water-cycle-proof <dossier-inexistant>`. High reste constant ;
seules les surcharges transitoires du Studio changent. Chaque étape attend 180 observations
CPU concordantes, un readback et un PNG nouveau ; délai global 120 s, sortie anticipée
refusée. Autres automations et caméra joueur incompatibles. Test CLI ajouté : **442 tests
workspace PASS, 1 timing ignoré**, Clippy strict workspace et build runtime PASS, fmt/diff
PASS. Aucun code WASM/moteur changé dans ce lot ; pas de nouveau build Web.

Exécution terminée en environ 15 s, code 0, `runtime_errors_observed=false`, cinq étapes
completed. GPU RX 7800 XT / Vulkan, 1920 × 1080, météo clear. WorldSketch rev3 inchangé,
terrain `18bd933d37f06a6a08a34b19938a78cb8f6fe8d9667b34bd2dcd6355956fd208`,
eau `0a59da647fc1e806b525c4ca7005359687ec71d0a141df991bfd4e93846d7b73`.
Politique identique aux cinq étapes :
`561577b6b0d029fe7272307748b3acff80e2cd8e26c95b43c6090e5ddb9f203a`.

Racine : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-gpu-cycle-20260906/`.
Fixture voisine : `r1-foliage-coherent-runtime-20260906-b`, kit candidat révision 2,
636 arbres / 7 632 primitives LOD. Une copie externe du preview sélectionne seulement
la caméra canonique `human_bridge` : position [-35, 5.4, -205], cible [-40, 11.4, -90],
FOV vertical 62°. Aucun placement, asset, caméra authorée ou fichier original modifié.

Commande (remplacer racine et fixture par les chemins ci-dessus) :

```text
forge_runtime --world-sketch-visual-valley --water-quality high
  --water-cycle-proof <racine>/captures
  --asset-kit-build <fixture>/asset-kit-build.v1.json
  --asset-kit-preview <racine>/human-preview.v1.json
  --prepare-vegetation-catalog <fixture>/prototype-catalog.v1.json
  --prototype-forest-preview
```

| PNG | État observé | Inspection visuelle |
| --- | --- | --- |
| stage-0 | SSR 48, réflectance 0,36, teinte 0,18 | eau claire réfléchissante |
| stage-1 | SSR absent, réflectance 0, teinte 0,18 | reflets clairs supprimés, eau bleu sombre |
| stage-2 | SSR 48, réflectance 0,36, teinte 0,18 | aspect réfléchissant restauré |
| stage-3 | SSR 48, réflectance 0,36, teinte 0 | différence de teinte non concluante visuellement |
| stage-4 | profil restauré, 48 / 0,36 / 0,18 | aspect initial retrouvé, pas d'égalité pixel à pixel revendiquée |

Cinq images ouvertes et inspectées. Pont, terrain et arbres restent visibles et cohérents ;
animation et HUD continuent. Réserve de désactivation/restauration des reflets levée **sur
cette vue native**, pas pour tous les matériaux/angles ni pour la teinte. Les FPS glissants
du HUD (chauffe/readbacks inclus) ne sont pas un benchmark 1080p High de Gate 1.

SHA-256 :

- runtime : `659ba92ebda61c2a0a76c6af31fb35caf13d46e0f00bd0a1f901f4a99c4c3f28` ;
- tests.log : `1bab150e26e3cba9064c1a34d6c31861baafd172bff8703b14fb21f68a2285a7` ;
- stage-0 : `0aa0de15586f37bd388403a7de99fba8ecf2b4372d08c3cf079741a61bffe4cc` ;
- stage-1 : `733284258a402bb742022de997afa113897ef05e781ee559cf7915feaed6ed28` ;
- stage-2 : `df6922e03ba20c6304fe1a1d9a00495eeac7e38b34e902596d6fd16b74f0debe` ;
- stage-3 : `18631093aff24e166de57c9cea5bf316e31c47c7490da2da4d042ed2a170eb02` ;
- stage-4 : `a143bb94b2492c6d42ff47614b47e946090e35578531b5424cfbe08c2c24b7d1`.

Rejeu volontaire vers le même dossier : refus code 2 (`existing-directory.log`), cinq
hashes de captures inchangés. Aucun ancien PNG ne satisfait ce nouveau lancement.
Gate 1 reste ouverte : teinte visuelle, réfraction écran, distance SSR, parité Web/Tauri,
captures finales normalisées, performances cibles et acceptations humaines non acquises.
CI NOT RUN ; aucun push, aucune suppression et aucun asset Cadle.

## Complément — isolation SSR indépendante, temps figé (2026-09-06)

`WaterQualityRequest::preview_screen_reflections_disabled` coupe temporairement le SSR
en résolvant les reflets vers Environment, sans changer la réflectance ou la teinte.
Le drapeau reste actif lors d'un changement de niveau ; la coupure de tous les reflets
reste prioritaire. La restauration reprend le budget du preset, sans écrire ses sources.
La télémétrie expose le drapeau séparément. Aucun nouveau contrôle Studio dans ce lot.

Le cycle eau reste à sept étapes en Low. Medium/High/Ultra ajoutent les étapes 7/8,
SSR coupé/restauré après l'IBL restaurée en étape 6. Temps virtuel figé comme précédemment ;
caméra, intensité et handles d'environnement identiques exigés. Le SSR est un effet caméra :
sa coupure n'est pas limitée à un matériau. Aucun shader ni asset modifié.

Test ECS du nouveau réglage : High → Ultra → Low → High, composant SSR 48/absent/restauré,
teinte et réflectance préservées sauf coupure totale explicite, hash du document constant.
Test de fin de protocole : High incomplet à sept étapes, complet à neuf.
**479 tests workspace PASS / 1 timing préexistant ignoré**, Clippy strict workspace,
check Studio WASM, build natif, formatage et diff check PASS. Warning futur connu
`proc-macro-error2 2.0.1` conservé.

Cycle natif réalisé sur RX 7800 XT, caméra `water_detail` du snapshot C, même kit/forêt
candidats que `R1_WATER_ENVIRONMENT_IMAGE_EVIDENCE.md` : commande identique au cycle
figé, mais `--water-quality high` et sortie fraîche
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-ssr-isolation-20260906/captures`.
Neuf captures/readbacks vérifiés, sortie 0, zéro ERROR, `runtime_errors_observed=false`.
Assertions JSONL sur les étapes 6/7/8 : SSR 48/absent/48, IBL 1/1/1, réflectance 0,36,
teinte 0,18, profil complet false. Les captures de ces trois étapes sont ouvertes/inspectées.

Résultat visible : le reflet sombre de l'arbre face à la caméra, près de la rive opposée,
disparaît à l'étape 7 puis revient à l'étape 8. Les larges reflets lumineux et l'IBL restent
présents. Cela établit une contribution SSR indépendante sur cette vue native et sa
restauration. Le reflet est bruité/fragmenté : pas de validation de qualité artistique,
de stabilité en mouvement, de tous les budgets ou du plafond métrique SSR (toujours absent).
Simulation figée/readbacks : les valeurs HUD ne sont pas un benchmark. Aucun verdict Web
ou Tauri, aucune activation de l'adaptation et aucun `Supported` global ajouté dans ce lot.

SHA-256 (logs à la racine de `r1-ssr-isolation-20260906`) :

```text
forge_runtime.exe D044D51A129E9D0DD48AB76BA4F4CF00780254401E1FDEA54EB29929B2AE9F3A
tests.log 46D2E40074FB478F9EC6658F6E672D54F4C9B3E1872D2C8C190361098F269936
clippy.log 67F7B117F3D84B978589D7D772911D9AE4E45B25831B14FDE64DC2EC36F4BDD4
wasm.log BC9B077FB4AC02C92FBFBFA70E56427D87FC3438981EA9D0748F009D5D4B3F42
build.log B5EE9E25A20AB076364A1F7DF861F14FDD689820ACA0D16FE81AC218F6AA0BF5
runtime.jsonl B1A46735DEC96D680102B0DCBD466D399996BD6DF20E4EA5909BE5044D885B0D
stage-6.png 951ECDD57F7DAA1063F50270E8B05A9C6C8CC0349D97279B8992E407A846E93D
stage-7.png 3D6B6A74DA65294F30747CD0141E4136A88A1B0824399D34F63CA422009D3E81
stage-8.png 75459955F82D13E115AD55606C3D185E50482F43E97B2ED15F4C93BD5F165D3A
```

Gate 1 ouverte ; CI distante NOT RUN, aucune publication.
