# R1 — contribution colorée de l'eau raccordée à l'éclairage

9 septembre 2026. Branche Rust, aucune publication. Gate 1 ouverte.

## Diagnostic fondé sur les sources locales

Hypothèse initiale : le prépass fournirait la profondeur de l'eau plutôt que
celle du fond. Aucun défaut établi : Bevy 0.19.1 `src/prepass/mod.rs:1155`
exclut explicitement les phases Transmissive/Transparent. Le candidat Forge
observé utilise la phase transmissive. Ce constat de code ne remplace pas une
lecture GPU exhaustive du buffer de profondeur.

Un défaut distinct est identifié dans notre contribution colorée : elle ajoutait
`exposure * deep_color * (1 - transmission)`. La palette est sans unité ; ce
calcul la traitait comme une radiance émissive faible. À EV100=12, Bevy utilise
`2^-12 / 1.2`, soit environ 0,00020345. L'environnement déclare pourtant un
soleil de 55 000 lux. Il manquait l'éclairage incident avant exposition.

Sources primaires locales épinglées :

- `bevy_camera-0.19.1/src/camera.rs:274`, formule d'exposition ;
- `bevy_pbr-0.19.1/src/render/light.rs:1731`, couleur directionnelle GPU
  prémultipliée par l'éclairement ;
- `bevy_pbr-0.19.1/src/render/pbr_functions.wgsl`, calcul de lumière, réception
  d'ombres et exposition finale ;
- `examples/visual_valley/environment.v1.json`, exposition et soleil ;
- ancienne contribution Forge conservée dans les preuves/exécutables précédents.

Ces fichiers Bevy sont lus dans le cache Cargo, pas modifiés. Aucun code ou asset
Cadle ajouté. Les appels aux fonctions publiques de shader Bevy ne changent pas
la pile de rendu.

## Correction implémentée

Seulement dans le candidat transmissif du shader eau : irradiance ambiante et
directionnelle, incidence sur un plan horizontal, visibilité par les ombres
Bevy si actives, puis palette profonde × extinction × transmission de Fresnel
× irradiance / pi. Exposition appliquée une fois à cette contribution.

C'est une approximation diffuse de volume, pas une intégration volumétrique
ni une diffusion multiple. La palette est utilisée comme coefficient coloré
du candidat ; aucun nouveau paramètre physique persistant n'est prétendu.
Pas d'IBL diffus, de lumières ponctuelles ou de caustiques ajoutés à ce terme.
Les reflets/réfraction PBR existants, les rides, le lit, les ombres configurées
et tous les documents de carte restent inchangés. Aucun éclairage constant
artificiellement indépendant de la scène ajouté.

Coût supplémentaire : calcul de lumière et lecture d'ombre par lumière
directionnelle active. Ce coût n'est pas qualifié sur les profils cibles.

Shader SHA256 : `A77D5448998A00BDB318C5A20A730503EA466D589A79878655E03B8B35468B54`.

## Vérifié en natif

37 tests eau PASS ; Clippy moteur all-targets/all-features strict, fmt et build
natif PASS. Les tests existants ne prouvent pas numériquement cette nouvelle
contribution d'éclairage. Son shader complet est exercé par les captures.

Racine : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-lit-scattering-20260909/`.

- EXE : `A483125E9FD9CD40E53F064E0A0906C5D7C60E3EE1A7AB335179D8008A8A4B9E`.
- Vue `water_close`, `after.png` :
  `078C003EDC8EE4136950F5A5283ABEEEDB528FEFDBD35E9EA7586031AAF080EC`.
- Vue `water_curve`, `curve.png` :
  `563B99BE3FF0A445F2CE1CDBF1BAEBEE5424169EF5FF8227F2D95FDEDD7F3F8F`.

Scripts `Capture.ps1` et `CaptureCurve.ps1`, journaux stdout/stderr conservés.
Même caméra, documents et temps figé que leurs témoins respectifs :
`r1-water-directional-detail-20260909/metric.png` et
`r1-studio-directional-water-20260909/curve.png`. Scènes natives sans kit gouverné.
Les deux sorties valent zéro, `capture_verified` présent et aucun ERROR.
PIDs 2024 et 26036 terminés, absence confirmée après les essais.

Inspection : contribution bleutée maintenant visible dans la partie profonde,
fond encore visible près des berges, changement observé dans les deux cadrages.
Pont et zone ombrée restent visibles ; pas de suppression des ombres pour
masquer une rupture. Rides toujours régulières et réalisme global non qualifié.

Cadence glissante affichée (hors benchmark) : vue proche moyenne 10,16 ms,
p95 17,69 ms, 1 % low 52,7 FPS ; vue élargie 11,14 ms, 18,14 ms, 51,5 FPS.
Géométrie eau/berges inchangée : 70 995 triangles. Aucune conclusion de coût
équivalent déduite de ces relevés courts.

## Limites et suite

### Complément : pluie, soleil réduit et écrasement des noirs

Comparaisons natives archivées dans la même racine, sans recompilation ni
modification de shader. `CaptureRain.ps1` épingle les deux exécutables, impose
la même caméra `water_close`, capture à la frame 300, ferme à 420 et vérifie
la sortie zéro, un événement `capture_verified` et l'absence de ERROR.
L'ancien EXE est `r1-water-directional-detail-20260909/metric.exe`, SHA256
`497E435C8F5A19F1C5ACDEEC528AB489CEC236513CE5372FB9D5CA047B551823`.

| Capture | SHA256 |
| --- | --- |
| `rain-before.png` | `809336BA94FA73C9AF9741441067FAB1A785D91E81590A44084FFD25EAEDEC73` |
| `rain-after.png` | `20271F4004DA7C48A9B150B1EFFF44251E9A6DEB1B6FBEE964D3C39D74A2F411` |
| `dim-before.png` | `3A65B9E72772B1252E9AC3F46D3243EBCD3A7EBF1883678EAEF13ADAF5464E5E` |
| `dim-after.png` | `A131AA199BC38095DA21D1B95E41BD1D3F0E1D5A54E4A67BFCAC379D090F112A` |
| `dim-neutral-contrast-after.png` | `7BDDC27D9E5BFAEA4037832AF1D0AE212C277088BBBF841C4EC841BA1B6C5B5F` |

La pluie embarquée ne réduit pas le soleil (`sunlight` absent). Elle ne suffit
donc pas à qualifier une variation d'éclairement. Le profil local
`weather-dim.json` conserve les données/seed par copie textuelle, passe en
révision 3, démarre sous pluie et demande un multiplicateur solaire de 0,25,
transition 1 seconde. SHA256 :
`64A8BD36588CFF21248E219CA37DA68A2788D2BC7BA9869D98634BA08981BD36`.
Une première entrée avec transition zéro a été correctement refusée ; journal
`refused-zero-transition.stderr.log` conservé, aucune validation affaiblie.
Le code `environment/weather_sunlight.rs` calcule l'éclairement à partir de la
valeur authorée, sans multiplication cumulative. Ces captures ne mesurent
pas directement les lux GPU.

Sous soleil réduit, **les deux shaders produisent de larges aplats noirs**,
sur le pont comme sur l'eau. Cette insuffisance précède la nouvelle diffusion.
Cause isolée ensuite : `weather.rs::color_grading_component` transmet le
contraste authoré à Bevy. Dans Bevy 0.19.1
`tonemapping/tonemapping_shared.wgsl:357`, la transformation est
`0.5 + (color - 0.5) * contrast`, suivie d'un clamp positif. Avec 1,08,
les canaux inférieurs à environ 0,037 deviennent négatifs avant ce clamp.

Preuve contrôlée : `weather-dim-neutral-contrast.json` ne change que le
contraste pluie de 1,08 à 1,00 par rapport à `weather-dim.json` ; SHA256
`93B937E99DF5254E59744750B836F7227F89C3FC6068E935A90B25BAAC3F1D69`.
Commande : `CaptureRain.ps1 -Variant after -Dim -NeutralContrast`.
Même EXE, caméra, soleil, exposition, température, saturation, humidité et
shader. Capture inspectée : détail du pont et lit visible de nouveau,
disparition des larges aplats noirs. Le rendu reste sombre, et n'est pas
artistiquement qualifié. Cadence affichée : moyenne 10,63 ms, p95 18,30 ms,
1 % low 50,3 FPS ; relevé court, pas un benchmark.

PIDs des essais 12496, 28608, 34048, 13792, 27356 et 44400 terminés ; absence
revérifiée. Aucun processus utilisateur arrêté. Aucun profil du projet modifié.

**Suite réorientée** : corriger explicitement le contrat d'adaptation du
contraste météo pour préserver les basses lumières, avec tests de valeurs
sombres et neutres, avant de retoucher encore la couleur de l'eau. Le profil
neutre ci-dessus est une isolation diagnostique, pas un correctif produit ni
une désactivation silencieuse du contraste. Pas de nouvelle pile ou dépendance.

Correction conservée, non reconstruite dans le Studio à ce stade. Les 648 tests
workspace et trois exports Studio du lot précédent restent des preuves de cet
ancien shader, pas de la nouvelle contribution lumineuse. Éclairage nul,
variations météorologiques, coût des ombres, temporalité et WebGPU restent à
qualifier. Aucun passage de R1-022/023 ou de Gate 1.
