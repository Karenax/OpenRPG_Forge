# Tidewater — milieu sous-marin, vue mixte et ménisque

Ce document conserve la preuve de la première vue mixte par plan tangent.
Le masque de la vraie surface visible et sa nouvelle preuve native sont décrits
dans [l'incrément suivant](2026-09-27-underwater-visible-mask.md).

## Portée finale de l'incrément

La promenade native applique désormais le milieu sous-marin de Tidewater dans
les deux cas utiles : caméra entièrement immergée et plan proche partagé entre
l'air et l'eau. La vue mixte utilise la hauteur et la normale de la requête GPU
caméra de la même frame pour construire un plan tangent local. Elle ajoute la
recherche de ligne d'eau, la réfraction de ménisque, la ligne de contact et le
liseré lumineux issus de `Underwater.js`.

Référence gelée :
`dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`.
`src/post/Underwater.js` a le SHA-256
`0b026474742c560588edef84d7aa72522c0e27c42883ba8914ff00a8bd5c2c3a`.

## Port Bevy/WGSL

`tidewater_underwater.rs` installe une passe plein écran après la passe
principale et avant `Core3dSystems::EarlyPostProcess`. Le pipeline est
spécialisé pour le format de la cible de chaque vue. La couleur de scène, la
profondeur Bevy et le `WaterQueryPacket` GPU sont lus directement par
`tidewater_underwater.wgsl`.

Le slot caméra `WaterQueryPacket.results[0]` fournit la hauteur et les deux
composantes horizontales de la normale animée. Dans la bande source de
`±0,35 m`, le shader :

- reconstruit le plan proche avec la projection et la matrice monde caméra ;
- approxime la surface animée par son plan tangent au point de requête caméra ;
- classe chaque pixel du plan proche en air ou eau ;
- recherche verticalement le changement de milieu jusqu'à `16 px` ;
- applique la bande de ménisque source de `9 px`, sa courbure, sa ligne de
  contact et son liseré lumineux.

La valeur `0,35 m` est un garde d'évaluation conservateur hérité de
`LENS_REACH/STRADDLE`. Elle ne signifie pas qu'une ligne d'eau est forcément
visible. Au-delà de cette bande, la caméra au-dessus reste inchangée et la
caméra au-dessous suit la branche entièrement immergée.

La branche immergée conserve la reconstruction de distance reverse-Z bornée à
600 m, l'extinction `absorption + scattering`, la transmittance Beer-Lambert,
la phase de Henyey-Greenstein `g = 0,85` mélangée à 25 % d'isotrope, ainsi que
la diffusion analytique du soleil et du ciel. Le dénominateur de l'intégrale
est borné pour éviter les valeurs non finies aux directions limites.

`TidewaterUnderwaterControl { enabled }` permet le contournement à chaud. Même
désactivée, la passe reste encodée ; le shader renvoie alors la couleur source
sans aucun effet. Les compteurs observables séparent les encodages totaux,
actifs et contournés afin de prouver quelle branche a réellement produit
chaque capture.

## Vérifications CPU et compilation

Le module contient sept tests ciblés. Ils couvrent l'appariement du résultat
GPU avec la caméra, la validation des entrées optiques, les états air/bande de
surface/immersion complète, le contrôle runtime, les compteurs actif/contourné
et le shader de vue mixte. Ce dernier remplace l'import Bevy par une interface
de test, parse le WGSL avec Naga puis exécute le validateur Naga complet.

Validations finales sur l'état qui a produit `speed-002` :

- `cargo test -p forge_engine tidewater_underwater --lib --locked --offline -j 1` : PASS, 7 tests dont parsing et validation Naga, en 4 min 38 s de compilation puis 0,00 s d'exécution ;
- check de `tidewater_plant_probe` : PASS en 4,45 s ;
- Clippy strict `--no-deps` de `tidewater_plant_probe` : PASS en 4,90 s ;
- build natif de `tidewater_plant_probe` : PASS en 2 min 20 s.

Empreintes des validations déjà terminées :

| Journal | SHA-256 |
|---|---|
| `underwater-waterline-tests-001.log` | `8fca0f9a334ffcda6982d15eed9578c8dd7fa589ae05e1a88058d1cba59527b4` |
| `underwater-three-pair-real-pose-check-002.log` | `3891b7b6ad09373d2fe76c1d24ece103ba22c0b57b0705109321662f29003106` |
| `underwater-three-pair-real-pose-clippy-002.log` | `b1112ef81fec128a9609e22cb2ffd31eb14bd6e637f1824223569204c8e85d01` |
| `underwater-waterline-speed-build-002.log` | `7d80b8ce97cc8e44940975a55092ac839b554ea252c57db42744bea5bd5e0fad` |

## Preuve A/B stricte dans le même processus

`native-underwater-waterline-speed-002` exécute une seule promenade native et
capture successivement les variantes `ACTIVE` et `BYPASSED` à trois poses. La
simulation, l'horloge océan, la caméra et les animations sont gelées entre les
deux captures. La signature binaire enregistrée est identique pour la matrice
caméra, la projection, les pas et temps océan/eau/requête et l'échantillon
sous-marin. Chaque capture prouve aussi un nouvel encodage de la branche
attendue.

Le reçu termine sur `Done` en 3 764 ticks avec backend stable, une entrée dans
l'eau, un retour à la marche, 816 ticks au sol, aucune dérive en neutralité et
un pas vertical maximal de caméra de `0,0592022 m`. Le pipeline est prêt et les
9 170 encodages se répartissent en 9 149 actifs et 21 contournés. Les trois
paires sont valides.

| Pose | Hauteur caméra relative | Attente pixel | Résultat |
|---|---:|---|---|
| Float | `+0,09454393 m` | identique | hashes strictement identiques |
| Dive | `+0,01077643 m` | différent, vraie ligne d'eau | hashes différents |
| Neutral | `-1,52324390 m` | différent, immersion complète | hashes différents |

`Float` est le contrôle négatif attendu. Avec un plan proche de `0,05 m` et un
FOV vertical de 75°, sa demi-hauteur vaut seulement `0,03836635 m` : à environ
9,45 cm au-dessus de l'eau, tout le plan proche reste dans l'air. `Dive` est
capturé pendant la descente naturelle, sans téléportation de la caméra, dans la
tolérance de `±0,02 m` autour de la surface.

## Mesures RGB8

`native-underwater-waterline-speed-002.metrics.json` compare les PNG FHD
`1920 × 1080` à entrées gelées bit pour bit.

| Pose | Delta RGB moyen /255 | RMS /255 | Maximum canal | Pixels changés | Delta > 2 | Delta > 16 | Rectangle changé |
|---|---:|---:|---:|---:|---:|---:|---|
| Float | 0 | 0 | 0 | 0 % | 0 % | 0 % | aucun |
| Dive | 8,521644 | 18,545023 | 159 | 31,385706 % | 30,699653 % | 30,331838 % | `[0, 641]–[1919, 1079]` |
| Neutral | 24,867647 | 32,911616 | 213 | 100 % | 100 % | 85,480517 % | image entière |

La boîte de différence de `Dive` est limitée à la partie eau sous la ligne du
plan proche. `Neutral` applique logiquement le milieu à toute l'image.

## Inspection visuelle FHD

Les six captures appariées ont été inspectées à leur définition native
`1920 × 1080` :

- `Float` conserve exactement la vue au ras de l'eau, le ponton et les vagues ;
- `Dive.ACTIVE` montre une vue réellement partagée, avec la partie basse
  atténuée en bleu, la courbure près de la séparation, la ligne de contact et
  le liseré ; `Dive.BYPASSED` conserve la couleur non atténuée du fond ;
- `Neutral.ACTIVE` montre l'extinction bleue et la perte de contraste avec la
  distance sur toute la vue ; `Neutral.BYPASSED` laisse le fond et les pilotis
  non atténués.

Le relief, les pilotis et la surface restent lisibles. Aucun écran noir, pixel
non fini ou rupture de la géométrie de scène n'a été observé.

## Empreintes des artefacts finaux

| Artefact | SHA-256 |
|---|---|
| `native-underwater-waterline-speed-002.walk.json` | `a917c06d112ef8221d04e9ed9f66db18947644ca348478c6fea621a97493d379` |
| `native-underwater-waterline-speed-002.metrics.json` | `cffb562e4f2dfc43a668fbcc8e4790ad606cc4d2b9244c12b4b001dae99b3768` |
| `native-underwater-waterline-speed-002.water-query.json` | `49d1bfc9eac6d884eac6ddbbdfbde4ef3fadc10407c3a9a3432d2efcb16a0977` |
| `native-underwater-waterline-speed-002.log` | `2ad33e776ec15e08b17026a468b4515e75847e3df8aeb7fe6493eda61afa461f` |
| `Float.ACTIVE.png` et `Float.BYPASSED.png` | `7c66e0b87e44b72e7736fec4f37ca4450a4b5a39a23306470cee11bc1a61e54d` |
| `Dive.ACTIVE.png` | `8e7fde57dd734a89c4cbcd8a9f9f9886423a24089b9f156a1943e4679514cf35` |
| `Dive.BYPASSED.png` | `c8c8881214db9a1e9da6d1bf9e56052bc2272e22d73c878f48fbc5ec0ffea9b4` |
| `Neutral.ACTIVE.png` | `19c95e6c42c51e00986a48dd0af015ba2519bceabc233ac8bae78d839e7fc570` |
| `Neutral.BYPASSED.png` | `bc3648a29bbbc8d5061c716f72c6e47db81b599d7b01ebf99165b83ad5992d16` |

Captures complémentaires du parcours :

| Artefact | SHA-256 |
|---|---|
| `native-underwater-waterline-speed-002.Float.png` | `3651d4549bdab178fb383960fd1bba1a5490b2db02a6a5ba0875fe05dd14a3f9` |
| `native-underwater-waterline-speed-002.Neutral.png` | `c73979067490f1a3cdf127304b3c71475f4c8d347cc84a66afa3cc372a467f11` |
| `native-underwater-waterline-speed-002.Surface.png` | `28b27a8e693f1ccfc2b2f29234dce1d72d40e683913fff9c65423ccf401e590e` |
| `native-underwater-waterline-speed-002.png` | `0f68fcf86d660ce10a171ce274af3f387cedb258f871cad0b39239a172cd54a7` |

## Historique utile et régression sur terre

La première preuve `native-underwater-ab-*` utilisait deux processus. Elle a
établi la présence du milieu entièrement immergé, mais ses différences de
caméra et de temps interdisaient une conclusion de parité pixel. Elle est
désormais remplacée par les trois paires strictes `speed-002` ; ses artefacts
restent conservés comme historique.

La régression native sur terre `native-underwater-land-regression-002` reste
valide : 8,994282 m parcourus, 1 838 pas au sol, backend stable, 143,649 FPS de
moyenne, p95 à 7,8197 ms, 1 % low à 87,421 FPS et ratio
simulation/temps réel de 1,001558. La première tentative s'était arrêtée sur un
`DeviceLost` pilote ; la relance complète a passé. Empreintes de la relance :

- reçu : `43ddcd174cf3468f2d83503baad2150be19c5f91e9c26a28ba65d844892fb561` ;
- capture : `64743b0dd5147cb60cf485d69220492387ad17bd2c305d9150b7b56774200fd6` ;
- requête d'eau : `cbbddda6de76d9d6a858adec98efec63cf0935ffe78bae2b2d1e65a26b2c309c` ;
- journal : `199a9b6a34515490a58e66a3aa73cbe0253c588bc4125be9ca84cd7da16d48d4`.

## Limites

Dans cette première version, la ligne d'eau était une approximation locale
par plan tangent. Elle ne possédait pas encore le masque de l'eau visible et
n'utilisait pas la profondeur finale pour classifier les rayons sans surface
visible. Ces deux points ont progressé dans l'incrément suivant, mais le calcul
FFT exact aux points terminaux et la parité des occlusions restent ouverts.

Restent également ouverts les caustiques, les rayons volumétriques et la
torche, les particules et la neige marine, les gouttes sur l'objectif ainsi que
la parité de la chaîne finale TAAU/bloom/ACES. Cet incrément est compilé et
qualifié sur le runtime natif Windows/wgpu ; aucune exécution ni qualification
WebGPU navigateur n'est encore fournie.

Les artefacts sont locaux sous `E:/OpenRPG_Forge/.tidewater-integration/`.
Aucun commit ni push.
