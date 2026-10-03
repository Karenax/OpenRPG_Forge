# Tidewater — masque de surface visible dans la vue sous-marine

Référence : `dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`.

Le fragment `WaterMaterial` calcule déjà le masque RG de la source :
`mask.x` indique si la surface visible est vue depuis dessous et `mask.y`
indique qu'un pixel d'eau est présent. La cible HDR principale de Bevy n'a
qu'une attache couleur. Le port encode donc les deux états de ce masque dans
son alpha (`0,25` : eau vue depuis l'air ; `0,75` : eau vue depuis dessous ;
`1` : pas de surface d'eau). La passe sous-marine lit ce canal au pixel exact,
immédiatement après `Core3dSystems::MainPass`, puis restitue une sortie alpha
opaque. Il s'agit de la vraie surface FFT/CDLOD rendue et testée en profondeur,
avec ses plis et ses vagues côtières, pas du plan tangent précédent.

Quand aucun pixel d'eau n'est visible, le shader lit la profondeur de scène.
Pour le ciel il choisit le milieu suivant la direction du rayon. Pour un point
terminal proche, il le compare à la hauteur extrapolée depuis la requête GPU
caméra de la même frame ; au-delà de 10 m il reprend le milieu de la caméra.
La source évalue la hauteur FFT à chaque point terminal : cette partie reste
donc approximative dans le port.

## Validation native

- Tests `tidewater_underwater` : 7/7, avec parsing et validation Naga du WGSL.
- Build de `tidewater_plant_probe` : PASS.
- Clippy ciblé `--no-deps -D warnings` : PASS. Le lint global des dépendances
  échoue sur deux `needless_pass_by_value` existants dans
  `forge_project/src/zone_authoring/session.rs`.
- Parcours GPU `native-underwater-visible-mask-001` : PASS, 3 763 ticks,
  816 ticks au sol, entrée dans l'eau puis retour à pied, backend stable.
  Les trois paires `ACTIVE`/`BYPASSED` ont mêmes matrices, projection et pas
  d'océan/eau/requête à l'intérieur de chaque paire. Six captures FHD ont été
  produites ; Float reste identique, Dive et Neutral changent comme attendu.

| Pose | Pixels RGB modifiés | Première ligne modifiée | Ancien plan tangent |
|---|---:|---:|---:|
| Float | 0 % | aucune | 0 % |
| Dive | 20,017 % | 806 | 31,386 %, ligne 641 |
| Neutral | 100 % | 0 | 100 % |

La capture Dive montre désormais la surface d'eau visible sans coloration
sous-marine parasite. Le traitement se concentre sur la zone de fond vue au
travers de la ligne d'eau. La limite autour des pixels de terrain demeure
géométriquement simple ; elle ne prouve pas encore une parité avec la capture
du navigateur Tidewater au même temps et à la même pose.

Artefacts sur E: :
`E:/OpenRPG_Forge/.tidewater-integration/native-underwater-visible-mask-001.walk.json`
et les six PNG `native-underwater-visible-mask-001.{Float,Dive,Neutral}.{ACTIVE,BYPASSED}.png`.

## Travail restant

La cible source RG séparée n'existe pas encore : l'alpha HDR transporte ses
deux états utiles pour cette passe. La conservation de cet alpha sous tous les
autres matériaux transparents doit être vérifiée. Le repli sans surface visible
doit employer la vraie hauteur FFT au point terminal, puis être comparé à la
source pour les occlusions, les plis et les déferlantes. Caustiques, rayons,
gouttes, chaîne finale et WebGPU restent ouverts.
