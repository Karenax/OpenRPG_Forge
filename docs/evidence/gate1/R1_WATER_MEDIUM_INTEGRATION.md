# R1 — raccord du milieu optique RGB, candidat non qualifié

9 septembre 2026, branche Rust, aucun commit/push, Gate 1 ouverte.

## Implémenté

### Complément du 9 septembre — parcours natif vivant

La restriction historique aux captures fixes, décrite ci-dessous, est élargie
explicitement aux marches bornées avec capture séparée ou mesure vivante.
`--water-medium-input` reste une lecture stricte limitée à 1024 octets et ne
modifie que la copie chargée. WorldSketch, optique explicite et sortie bornée
restent requis ; mesure figée/stationnaire, isolation et capture pendant mesure
restent refusées. Les autres exclusions du harnais sont conservées.

Le rapport vivant ajoute `water_projection` (hash, mode et observation CPU),
afin de ne plus confondre l'ancienne eau opaque avec le milieu optique. Aucun
changement du shader, des coefficients, de la carte ou des seuils de mesure.
91 tests unitaires Runtime + une intégration PASS ; Clippy all-targets strict,
build natif, fmt et diff-check PASS. Pas de suite workspace ou rebuild Studio
répété pour ce raccord CLI natif seul.

Exécution réelle : `Run-WaterMediumLive-20260909.ps1`, sortie 0, même kit de
1662 arbres et profil clair `[0.8,0.2,0.1]` / `[0.005,0.005,0.005]`, parcours
`1800:1:0.22`, ombres candidates 2048/3. 1671 échantillons sur 29,975 s, ticks
observés 2–1799, simulation non figée et joueur au sol après freinage. Mode
`experimental_rgb_medium_transmission_not_qualified`, composants observés,
hash eau `d30f5ae8cbcda62eaee985cd53dcf2d7fa7107050cba073f0018d5087bfe9434`.
Terrain, kit et profil fichier épinglés inchangés après essai.

Cadence dev indicative : 55,75 FPS, p95 25,91 ms, 1 % low 36,67 FPS. Un seul
parcours d'intégration, **pas comparaison de coût avec l'ancienne eau**, pas
qualification release, GPU cible, Studio ou acceptation artistique. Aucun PNG
répété pour ce changement non visuel ; les preuves optiques antérieures gardent
leurs limites. PID 15968 terminé et aucun exécutable du répertoire de preuve
encore actif. Aucun navigateur ouvert.

Racine externe `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-medium-live-20260909/`,
pins, JSON, stdout/stderr conservés ; logs de validation `water-medium-live-*-20260909.log`
dans le parent. EXE `08BB31C41C0EC8E13E820F6CD6B637BEB4022270DBE9C3C8943F45E5AC9C3B3A`,
rapport `4A6232E5AE81FF5FEE8C943E9762CF3E886D189B9C580B6276052290866750FE`.

La référence [Epic Single Layer Water](https://dev.epicgames.com/documentation/en-us/unreal-engine/single-layer-water-shading-model-in-unreal-engine),
consultée le 9 septembre, confirme l'intérêt de traiter absorption, diffusion,
réflexion et réfraction ensemble. Ce principe est déjà partiellement implémenté
dans Forge ; il ne justifie ni un nouveau moteur ni l'annonce d'une eau réaliste.
La suite utile porte sur le candidat intégré, pas sur de nouvelles sinusoïdes
ou la comparaison de captures fixes présentée comme du mouvement.

### Implémentation initiale et preuves historiques

- `WaterDocument.medium` optionnel, validation stricte, sérialisation omise si
  absent. Les anciens documents restent acceptés ; les anciens lecteurs stricts
  refusent le nouveau champ. Pas de migration silencieuse des cartes.
- Uniformes absorption/diffusion en m^-1 sans conversion sRGB. Le champ présent
  active explicitement le candidat transmissif et ses prérequis caméra ; le
  statut `experimental_rgb_medium_transmission_not_qualified` le distingue.
- WGSL : `T=exp(-(a+s)*distance)`, poids de diffusion `s/(a+s)*(1-T)` ; un canal
  d'extinction nulle reste transparent sans division par zéro. Lumière incidente,
  Fresnel et approximation diffuse existants sont conservés. Ce n'est pas une
  intégration volumétrique complète ni un modèle spectral mesuré.
- Entrée native `--water-medium-input` stricte et bornée, lecture seule, limitée
  à une capture fixe optique WorldSketch avec sortie automatique. Le JSON porte
  seulement les coefficients réutilisables, pas une identité ou une géométrie
  de carte. Seule la copie chargée change et son hash d'eau la distingue.
- Aucun asset, dépendance, contrôle Studio ou chemin d'édition spécial IA ajouté.

## Vérifications

Première passe ciblée : 67 tests projet PASS ; 274 tests moteur PASS, un ignoré.
Passe finale `cargo test --locked --offline --workspace` : **655 PASS, un ignoré**,
19 résultats de suites (dont suites vides), aucun échec. Les nouveaux tests couvrent
la sérialisation absente/présente, les entrées invalides, la projection sans sRGB,
la sélection du candidat et ses composants caméra, et le chargement CLI borné.
Clippy strict projet/moteur/runtime, tous targets/features : PASS. Format et
`git diff --check` PASS. Build natif PASS ; check Studio WASM web/lib PASS.
Avertissement futur `proc-macro-error2 2.0.1` conservé ; pas de rebuild Studio.

Lecture GPU du vrai helper WGSL : **9 cas PASS**, RX 7800 XT, Vulkan, tolérance
absolue 0,0001. Chaque cas vérifie profondeur, ancien modèle, transmittance RGB
et poids de diffusion, dont canal transparent sans division par zéro. Les
résultats dépendent d'entrées en buffer, pas seulement de constantes compilées.
Le périphérique est détruit et le processus de preuve n'est plus présent.

Protocole et sorties locaux :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-medium-integration-20260909/`.
`Validate.ps1` borne les jobs à deux et utilise les caches/temp sur E:.
`Capture.ps1` possède le PID de chaque essai, attend la sortie et le ferme aussi
sur timeout/échec. Il refuse d'écraser une capture existante.

## Rendu réellement inspecté

Trois captures natives 1920×1080, même exécutable, caméra `water_close`, scène,
éclairage clair et temps fixe, capture frame 300 / sortie 420. Pas de kit importé,
ni preuve de déplacement. Aucune erreur runtime, un événement `capture_verified`
par essai ; sorties zéro et PIDs 40236, 9000, 43136 confirmés absents ensuite.
Les avertissements du chargeur Vulkan (registre/layer AMD dupliqué) restent dans
les journaux, sans empêcher le rendu.

| Variante | Observation visuelle | Cadence glissante : moyenne / p95 / 1% low |
| --- | --- | --- |
| legacy | Fond brun, eau bleue sous le pont | 10,51 ms / 16,99 ms / 34,8 FPS |
| medium, diffusion 0,08 | Eau trop laiteuse : profil non retenu visuellement | 10,76 ms / 17,08 ms / 55,7 FPS |
| clear-medium, diffusion 0,005 | Fond visible avec teinte verte, effet laiteux réduit | 10,77 ms / 17,20 ms / 56,7 FPS |

Ces valeurs lues dans les captures ne sont **pas un benchmark comparatif** ni
une qualification des performances. La variante claire est conservée comme
profil de preuve, pas comme preset officiel. La rive reste anguleuse et le
rendu encore trop uniforme ; aucun verdict artistique positif global.

Hashes SHA-256 :

- EXE : `0EEA65ECDF19B268959344C2B6398EAFA33F2BEDD84771E946DD26BCEE17F089`
- shader principal : `7F85846871FFECAD2BD4E105B87968EE06600472769D9A77ED25B77FBAC28F6F`
- helper optique : `7FF30190E1F836A174816BBA8A1B15922FA775440375D010C08F03DDC4D75C2E`
- legacy.png : `E611056FF8C9C229CC3D11145F1C184E43CC0054C8F26D1958045F25AB704798`
- medium.png : `567E2BA0855E0D5F92360FAB3F7122DA69AC863F7A8788351001D5392293CED6`
- clear-medium.png : `A82792F92306A4D9F18722532BEFEBBC4F4B312290E7986EC018154853DAC319`
- medium.json : `2606D9CA29FD72F3E45D3256506C63D1CAFBEDC5EEA08D5539C1E971A33A219D`
- clear-medium.json : `7F85A535A2E22FDBDE5D116F8619D5962A4A5106ACFC02E7C6217DCC4B1F3840`

## Limites et suite

Les profils de preuve sont synthétiques, pas des mesures d'eau réelle ni des
défauts imposés aux futures cartes. La géométrie
eau/berge, le plafond du trajet optique et la réfraction écran restent inchangés.
SSR eau désactivé et profils qualité épinglés dans le candidat. Aucune affirmation
de réalisme, de parité WebGPU, de performance ou de passage de gate sans preuve.
Suite : raccords et transitions eau/berge encore visibles, puis intégration
Studio du candidat retenu et preuve en mouvement. Les coefficients permettent
désormais ces comparaisons sans retoucher/recompiler le shader à chaque profil.

Références de conception :
[Epic — Single Layer Water](https://dev.epicgames.com/documentation/en-us/unreal-engine/single-layer-water-shading-model-in-unreal-engine),
[NVIDIA — GPU Gems](https://developer.nvidia.com/gpugems/gpugems/part-i-natural-effects/chapter-1-effective-water-simulation-physical-models).
Formules réécrites dans Forge, aucun code ou asset tiers copié.
