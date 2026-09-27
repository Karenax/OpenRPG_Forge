# Eau Low — disponibilité des images d'environnement

2026-09-06, Rust local, Gate 1 ouverte ; aucun fallback approuvé ou preset modifié.

## Implémenté

`WaterQualityObservation.environment_textures_available` observe les images diffuse et
spéculaire liées à l'unique caméra active. Les deux doivent exister dans Assets<Image> :
extent carré non nul, six couches, dimension D2, un échantillon et usage TEXTURE_BINDING.
Caméra absente/ambiguë, composant absent, image disparue ou forme incorrecte donnent false.
Il s'agit de disponibilité main-world avec contrôles structurels, pas de validation de tous
les formats/vues/mips, du contenu des texels, de l'upload GPU ou du rendu des reflets.

La requête caméra existante porte aussi le composant de carte d'environnement. Les observations
de teinte/réflectance sont regroupées dans le module qualité ; aucun changement de shader,
source authorée, pose, collision, acquittement global ou profil complet. Les noms des états
optiques non qualifiés restent inchangés. Champ disponible dans le JSON de diagnostic.

## Vérifié

Test ECS : absence initiale ; ajout de deux images synthétiques ; caméra auxiliaire inactive
tolérée ; deux actives refusées ; image à une couche refusée ; restauration ; image supprimée
refusée. `full_preset_applied` reste false. 27 tests eau PASS avant regroupement final ;
ensuite **454 tests workspace all-features PASS / 1 timing ignoré**, dont 189 moteur PASS.
Clippy strict workspace/all-targets/all-features PASS ; check Studio WASM/web PASS (8,45 s,
avertissement préexistant proc-macro-error2). Formatage et diff check PASS.
Échecs intermédiaires corrigés : limite Bevy de paramètres système, complexité de requête et
longueur d'observateur. Aucune assertion ni règle Clippy supprimée pour obtenir le résultat.

Deux lancements natifs Low, human_bridge, clair/pluie, mêmes kit/preview/catalogue candidat
`r1-foliage-coherent-runtime-20260906-b`, capture frame 900 et sortie 1800 : sorties 0,
contrôles du collecteur PASS, deux PNG 1920×1080 inspectés. Vérification JSON supplémentaire :
images disponibles true et profil complet false dans les deux rapports. SSR absent,
teinte 0,18 et réflectance 0,36 observées ; sources inchangées.

Preuves : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-environment-images-20260906`.
Manifeste `542a8c73b87f8d303dd007885f4368f9e46dff9a56b2d83223f13db4fbed0f28`.
Runtime `cc272261eeee4a4b9b946429a04f32c2e9e94e378c281232aa30f7bd350d764e`.

L'eau et le pont sont visibles ; forêt/herbe restent tramées à distance et l'aspect reste
prototypal. Ces vues ne comparent pas reflet environnement activé/désactivé/restauré : la
contribution visuelle de ce chemin reste à isoler. Pas de performance qualifiée, de rendu Web,
de Low complet ou d'adaptation automatique déduit. Tous les processus sont terminés.
CI NOT RUN, aucune publication Git, aucun asset Cadle.

## Complément — cycle natif réellement Low, 2026-09-06

Le harness eau forçait High à chacune des cinq étapes, indépendamment de l'argument CLI.
Il transporte désormais explicitement le niveau demandé dans `Domain::Water(tier)` :
aucun changement de matériau, shader, preset, carte ou commande Studio. Test de non-régression
sur les quatre niveaux et toutes les étapes ; cycles des trois autres domaines inchangés.
Les champs de log `domain` identifient désormais `Water(Low)` dans ce lancement.

Validation ciblée : `cargo test -p forge_runtime --all-features --locked` : **65 PASS**
(64 unitaires et 1 intégration de logs). Clippy runtime tous targets/features avec `-D warnings`,
fmt et diff check PASS. Pas de nouveau verdict workspace/WASM : seuls le harness natif et
son appel sont modifiés ; les 454 tests précédents restent une preuve historique distincte.

Lancement code 0, cinq observations et cinq readbacks, zéro événement ERROR,
`runtime_errors_observed=false`. Les cinq PNG FHD ont été ouverts et inspectés. Chaque étape
confirme Low, SSR absent, images d'environnement disponibles et `full_preset_applied=false`.
Les hashes terrain/eau et politique restent identiques au rapport précédent.

| Étape | Réflectance / teinte | Résultat visible |
| --- | --- | --- |
| 0 | 0,36 / 0,18 | eau claire avec réponse réfléchissante |
| 1 | 0 / 0,18 | eau nettement bleu sombre |
| 2 | 0,36 / 0,18 | aspect clair restauré |
| 3 | 0,36 / 0 | différence de teinte non concluante |
| 4 | 0,36 / 0,18 | aspect initial retrouvé, sans égalité pixel revendiquée |

La réponse visuelle à la réflectance est donc établie sans SSR, dans cette vue native.
Attention : couper la réflectance agit sur la réponse spéculaire du matériau, pas uniquement
sur la carte d'environnement. Ce cycle ne mesure pas séparément IBL et éclairage direct.
Ce n'est pas une qualification générale des reflets, de la teinte, de Low complet ou de
l'adaptation. Les autres domaines gardent leurs réglages par défaut : **eau Low, pas global Low**.
Caméra human_bridge, météo clear, kit candidat et forêt inchangés ; aucune retouche one-shot.

Racine locale : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-low-cycle-20260906`.
Commande : celle du cycle High dans `R1_040_WATER_SSR_BUDGETS.md`, avec `--water-quality low`,
sortie `<racine>/captures` et preview réutilisé en lecture seule à
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-gpu-cycle-20260906/human-preview.v1.json`.
Fixture : `r1-foliage-coherent-runtime-20260906-b`. Runtime :
`E:/OpenRPG_Forge/.gate1-build-cache/terrain-water-20260906/debug/forge_runtime.exe`.

SHA-256 du binaire : `5e94a832caa44fd58b5e1977f73a034dd410cad05814f88e8d72c2be5d175b72`.
Log `runtime.jsonl` : `77476a504a26502397d9c45a82e446e4134388fa0a9f2206cebb239be3f42041`.

SHA-256 des captures stage-0 à stage-4 :

```text
6117eaba3c27411bb60a8e8611e8aa658190f84dc417228588783d0f7e70e345
4a5c49dd3012b714b294ac297fdcbc8c6fcd2cc93cf52b8ac413fec3d66ce69e
6b1248fa087158a5a1b653b7246a396da0e17cfc9c573ed507ae3cdfd7fc5298
a7b59c942a9f207d40e1e60499ac0b171733a021484de9f6799053e7c375464d
b07d571b23a8fe6b04dc7a7e1261761bb0875684a6a587ecb542f2bd92717ba4
```

Entrées comparées avant/après, SHA-256 des octets inchangés : build kit
`0f321ed0547f09b85a2fbdab79407b18405cca28b3749e7297971b0d6cfe3ef9`, catalogue
`7a2c04bb4fa1e53a5958154f60a81dcf6d4069e58ce1ef5a41adf2e807ce2b03`, preview
`be829963404b136c21963b58437178b9b605315982577a7e3de4ef54aaa2715b`, WorldSketch
`5b2088952cad01732a53b371462247cf386bb25141c39ff97a60fb14bd2e5fc1`, qualité
`0aaa2a17c8dfd18382bef31c912ef26884c54bff0a9f455b8529e7bd155fed2c`.
Les octets JSON et les hashes canoniques de documents ne sont pas interchangeables.

Processus terminés ; Gate 1 ouverte, CI NOT RUN, aucun push.

## Complément — isolation IBL native, 2026-09-06

Le cycle eau compte maintenant sept étapes : les cinq précédentes, puis intensité de la
carte d'environnement à zéro et restauration exacte de sa valeur initiale positive.
Caméra active unique et handles diffuse/specular identiques exigés ; la première référence
n'est retenue qu'après disponibilité des images. Une modification d'intensité doit être
observée à l'image CPU suivante avant les 180 observations stables. Les deux dernières
étapes ne changent ni réflectance, ni teinte, ni éclairage direct. Elles coupent l'IBL de
toute la scène, pas seulement de l'eau. Ce mécanisme est un outil natif de preuve réutilisable,
pas une retouche de carte ni une nouvelle commande d'auteur dans Studio.

Correction de reprise : `SystemState::get_mut` renvoie un Result dans Bevy 0.19 ; l'erreur
de compilation E0308 du nouveau test a été corrigée dans le test, sans affaiblir ses assertions.
`cargo test -p forge_runtime --all-features --locked` : **66 PASS** (65 unitaires + 1 logs).
`cargo clippy -p forge_runtime --all-targets --all-features --locked -- -D warnings`,
`cargo build -p forge_runtime --locked`, formatage et diff check : PASS.
Pas de nouveau verdict workspace ou WASM : changement limité au harness natif.

Preuve retenue : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-ibl-cycle-20260906-b`.
Le premier dossier sans suffixe est conservé : sortie 0 et sept PNG, mais filtre hérité
`RUST_LOG=warn` privant le journal des observations. Replay avec `RUST_LOG=info`, même binaire :
sortie 0, sept observations/readbacks, zéro ERROR, `runtime_errors_observed=false`.
Les assertions PowerShell sur le JSONL confirment les étapes 0..6, les intensités
1/1/1/1/1/0/1, Low, SSR absent, images disponibles et profil complet false à chaque étape ;
réflectance 0,36 et teinte 0,18 aux étapes 4/5/6. Sept PNG FHD ouverts et inspectés.

Caméra `water_detail`, snapshot caméra C, météo claire, kit et forêt candidats existants.
Commande : runtime du cache `terrain-water-20260906/debug/forge_runtime.exe`, avec
`--world-sketch-visual-valley --world-sketch-input <camera-set-C>/world-sketch.v1.json`,
`--asset-kit-build <fixture>/asset-kit-build.v1.json`,
`--asset-kit-preview <twelve-views>/water_detail-clear/preview.json`,
`--prepare-vegetation-catalog <fixture>/prototype-catalog.v1.json`,
`--prototype-forest-preview --water-quality low --water-cycle-proof <preuve>/captures`.
Tous les dossiers sont sous `E:/OpenRPG_Forge/.gate1-evidence-runtime` : camera-set-C =
`r1-camera-set-20260906-c`, fixture = `r1-foliage-coherent-runtime-20260906-b`,
twelve-views = `r1-twelve-views-20260906`. Aucune source ni aucun asset modifié par ce lot.

Résultat visible : sans IBL, l'eau s'assombrit et les ombres du terrain/de la végétation
deviennent noires ; le reflet direct lumineux reste présent. La restauration retrouve
l'aspect initial. Cela établit une contribution IBL dans cette vue native, séparée du
réglage de réflectance. Le temps des vagues continue : pas de comparaison pixel-perfect
ni de mesure photométrique. La coupure de teinte reste non concluante. Vagues très régulières,
reflets larges et bande verte de berge restent des défauts visuels non résolus par cet outil.

Le HUD glissant aux étapes 4/5/6 affiche respectivement moyenne 12,55/12,37/12,36 ms,
p95 13,61/13,23/13,17 ms et 1 % low 40,1/40,5/41,9 FPS sur RX 7800 XT. Ce cycle avec
readbacks n'est pas un benchmark ; aucun gain de performance ni matériel cible qualifié.
Pas de rendu Web rejoué, de Low global complet, d'adaptation activée ou de fallback approuvé.
Gate 1 reste ouverte ; CI NOT RUN ; aucun commit/push, aucun asset Cadle.

SHA-256 binaire : `44e240e303ebdbb8eba6e4d26bb1469498ba200f8b54a342f58641ae1323c8fa`.
JSONL : `005b07d2c9b8c86bf91717bd43d47cfd87eaa4a66d6a9e04d89ac13330e7bbe0`.
Captures stage-0 à stage-6 :

```text
ac2e31f89197a30436d11940005fd39b50629751ffd20a2df97fe5aa9994ebb3
90552f43a5f47e7197e974e62a3e8ae8b9cd86430776a8c7e08c9676edc437df
890e830da3eeb09a399e57e23d28202eb5c85773f6288f61dedfcdf10061130e
87b7898507e7acbe19c10d22ed99abe407b5d1989ab1d8fc14def3d952d813e1
bbbe069f28528dfc1568dab4b8a1b87ca732a2485240e5e3323b0fc9f5936cfa
2b524a5e4f4ede5ca53d73222bfcf52ccee459af762d9e1eeb9959da22f7b94a
f560ec12264809aba63781de7e0cd7000f0caf0fcf01eb20af3bac41d736b062
```

## Complément — teinte isolée à temps virtuel figé (2026-09-06)

Le harness `--water-cycle-proof` fige maintenant `Time<Virtual>` au démarrage, uniquement
pour ce protocole. Vagues, météo et animation utilisant ce temps restent fixes ; horloge
réelle, readbacks et deadline de 120 secondes continuent. Aucun changement du shader,
des paramètres authorés ou des cartes. Test Bevy : temps virtuel nul/pausé après quatre
updates, temps réel non nul. **67 tests runtime PASS** (66 unitaires + 1 logging), Clippy
runtime strict/all-targets/all-features et build natif PASS. Format/diff check PASS.
Pas de nouvelle qualification workspace/Web pour ce changement de harness natif.

Cause de la faible différence précédente, établie par lecture du shader : avec `d` la
réponse de profondeur et `s` la force de teinte, le poids profond vaut
`d + s*d*(1-d)`. Désactiver `s` ne supprime pas la palette de profondeur de base.
Pour `s=0,18`, l'écart vaut au plus 0,045 du contraste linéaire profond/peu profond,
et zéro aux deux extrêmes. Ce constat ne justifie pas de gonfler arbitrairement la force.

Cycle natif sur les mêmes entrées/caméra `water_detail` et kit/forêt candidats que le
protocole précédent, nouvelle sortie `r1-water-static-optics-20260906/captures` sous
`E:/OpenRPG_Forge/.gate1-evidence-runtime`. Même commande, nouveau binaire ci-dessous.
Sept captures/readbacks, un événement `water_cycle_clock_frozen`, zéro ERROR,
sortie 0 et `runtime_errors_observed=false`. Étapes 2/3/4 : teinte 0,18/0/0,18,
réflectance 0,36, IBL 1, même politique, profil complet false. Assertions JSONL PASS.
Ces trois PNG FHD sont ouverts et inspectés : géométrie/reflets fixes, variation de
couleur discrète dans l'eau, retour à l'aspect précédent. Pas de revue artistique.

Lecture seule des PNG avec `System.Drawing.Bitmap.GetPixel`, RGB 8 bits, pas de retouche :
grille de pas 4 px ; ROI eau x=[50,950[, y=[520,950[ ; témoin terrain hors HUD
x=[1000,1800[, y=[50,300[. Comparaison à stage-2, somme absolue des trois canaux :

| Comparaison | Région | Échantillons modifiés / total | Écart moyen RGB cumulé | Maximum |
| --- | --- | ---: | ---: | ---: |
| 2 → 3, teinte coupée | eau | 21719 / 24300 | 4,147407 | 14 |
| 2 → 4, restaurée | eau | 3316 / 24300 | 0,167490 | 4 |
| 2 → 3 | terrain témoin | 84 / 12600 | 0,007540 | 3 |
| 2 → 4 | terrain témoin | 46 / 12600 | 0,004524 | 3 |

La variation de l'eau à la coupure dépasse largement le résidu de restauration et le
témoin. La preuve établit l'exécution du contrôle de teinte sur cette vue native, pas
une identité pixel-perfect, une mesure photométrique ou une qualification Web. Les
valeurs HUD de cette scène figée ne sont pas un benchmark. Aucun `Supported` global
n'est ajouté : capacités restantes, installation effective et adaptation restent ouvertes.
La réserve « teinte non concluante » est levée pour cette vue native seulement.

SHA-256 (logs à la racine du dossier, captures dans `captures/`) :

```text
forge_runtime.exe 1E2CBBCE6226E47DC5FE0B53074B580F9129476859435D5E8263B4A3FDAC9C3D
visual_valley_water.wgsl 12A4A84EF36945D124A2027B38EC01A770D98293F38414B1874347F6F4C9273E
tests.log 17F029A6FBBB3DE3F26AD1A5B7348144F463D34DA7A3E6F5B8343E3347E47C91
clippy.log B900305ABF083289B3FAE65D6A6D818665626C079DA440628C533F9A3DE959B9
build.log FF93BC90A6E88A8995F69BA79AE39C6E40145A948FAD0D8C9E1F3E9E99BC7332
runtime.jsonl ABD42DA96E9815B6E73EE4994DD4C0F78F48D259DEF2D820A39D4D9F85D83FC7
stage-2.png FFCD307942A7987E4202ECFF8625F9CB7721CB402D6924A5EE2C79CB48B73AD0
stage-3.png 09C2EF71FEB3A37AC0CCB96B815EFBDFE96B9EA58DC65738B35189E1801028E2
stage-4.png 73F4D463A158CA94BB4BDE6CC988077BA68AF8EADA76A0B22E276B065BA9DEE8
```

Gate 1 ouverte, CI distante NOT RUN ; aucun commit/push ni asset Cadle.
