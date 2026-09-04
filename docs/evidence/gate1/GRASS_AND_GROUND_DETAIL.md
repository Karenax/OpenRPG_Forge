# Gate 1 — forme de l'herbe et détail du sol

Date : **2026-09-04**. Candidat exécuté : `68af815adf9d2beb341943dd5ba7eed8fd1f132e`,
arbre `dbdbdca63aa4ee0a19e241e9612740bd1ae2fb25`, branche `Rust`.

## Changement et budgets

L'herbe procédurale utilise un ruban inférieur et une pointe courbée : **7 sommets / 3 triangles**
par brin, contre 4 / 2. Hauteur 0,25–0,50 m, largeur 0,035–0,065 m et rayon du patch 1,55 m,
avant son échelle. Racines projetées sur le terrain, poids de vent nul aux racines, 55 % au coude
et 100 % à la pointe. Recentrage AABB, batches, identités et scatter conservés.

Le terrain reçoit une variation procédurale originale en coordonnées monde, à trois échelles
approximatives de 3 m, 60 cm et 20 cm, atténuée selon l'empreinte pixel. Modulation maximale de
l'albédo ±16 % et de la rugosité ±0,045, avant les multiplicateurs wetness existants. Palette,
couches canoniques, interfaces et binding 100 inchangés. Aucune texture, dépendance ni asset Cadle
ajouté. Les berges séparées de la rivière ne sont pas détaillées par ce shader terrain.

| Profil de démarrage | Avant : brins/patch | Après | Brins générés après |
|---|---:|---:|---:|
| Low | 12 | 24 | 121 632 |
| Medium | 20 | 40 | 202 720 |
| High | 32 | 64 | 324 352 |
| Ultra | 48 | 64 | 324 352 |

Les 5 068 patches et les **8 282 placements / 64 chunks** sont conservés exactement, test comparatif
compris. Le pire cas documentaire atteint `4096 × 4 × 64 = 1 048 576` brins, sans relever le plafond.
Le premier essai Ultra 96 a été rejeté par le garde qualité 64 ; le candidat conserve ce garde.
Goldens mis à jour, sans réécrire de preuve historique : végétation
`3c87dc39beccd22b96dba19448b72ec7dd58881a371fe5863c8039585068ce6d`, qualité
`0c4573220cc8c314e58938f617fbd7a46bdac4037a599b9a73c3be61aeb699cb`, cache végétation
`40869c0bb7cad029f966d757d6fb8046243e0e94b58477895ee8c5713d267ac6`.

**Coût assumé, pas une optimisation :** en High, l'herbe totalise 973 056 triangles et 2 270 464
sommets, soit ×3 et ×3,5 ; les nombres soumis dépendent du culling. Aucun gain FPS ni verdict de
cadence n'est déduit des overlays retardés. PresentMon reste suspendu sous ADR-0010. Les budgets de
brins sont appliqués au démarrage, pas dynamiquement lors d'un changement de tier.

## Validation locale

- `cargo fmt --all -- --check` et Clippy workspace/all-targets strict verrouillé/offline : PASS.
- `cargo test --workspace --all-targets --locked --offline -- --quiet` : **374/374 PASS**, dont
  148 moteur, 53 monde et 76 runtime. Dimensions, raccords, vent, AABB, déterminisme, identité des
  placements et round-trip sont couverts. Les tests textuels WGSL ne prétendent pas exécuter le GPU.
- Check moteur et Clippy Studio WASM `wasm32-unknown-unknown`, features `web` seules : PASS.
  Avis de compatibilité future tiers `proc-macro-error2` conservé ; aucune dépendance modifiée.
- `FORGE_SOURCE_COMMIT=<candidat>` puis build Release natif verrouillé/offline : PASS, **3 min 18 s**.
- `Test-Gate1VisualProof.ps1 -Quality high -NoVsync -SkipVideo` : PASS automatisé frames-only,
  **29 PNG 1920×1080**, 12 diagnostics de passes, stdout/stderr vides, gameplay/Rapier exercé.
- Studio exact servi par `trunk serve --release --locked --no-autoreload --config Trunk.toml`,
  avec `FORGE_SOURCE_COMMIT=<candidat>`, `CARGO_NET_OFFLINE=true`, `NO_COLOR=true` : build PASS.
- `Invoke-Gate1BrowserWeatherMatrix.ps1`, Edge 152.0.4191.53, profils temporaires Low/High :
  **PASS automatisé**, 6 checkpoints, 12 PNG, 4/4 bundles exacts ; console page 0 erreur/0 warning,
  réseau 0 échec par tier. Canvas 1408×966 : pas de comparaison pixel native/Browser à résolution égale.

Le natif et chaque tier Browser accomplissent sec → pluie → sec dans leur même instance, endpoints
CPU wetness 0 → 0,78 → 0. Les flags d'identité précise de frame, d'attribution eau/wetness GPU et de
revue humaine restent faux dans leurs reçus. Aucune nouvelle qualification par matériau n'est déduite.

Premier Browser en sandbox : **ABORT**, zéro PNG, refus d'accès du processus GPU puis socket fermée.
Reçu `68af815adf9d-20260904T083359651Z/low/abort-receipt.json`, SHA-256
`793a0ccbe0cba6c242eb40f290ae52787c4595ba8cc9d752c12ee21517b9b8cc`. Le rejeu autorisé hors sandbox
utilise le même code/driver, sans changement du pilote ou du validateur. Son stderr hôte Edge signale
encore le service de mise à jour et `GPU state invalid after WaitForGetOffsetInRange` en fin de tier.
Le PASS porte sur les contrôles du harnais, pas sur l'absence de tout message hôte ni sur leur cause.
Le serveur de ce lot est arrêté.

## Preuves conservées

Racine native : `E:\OpenRPG_Forge\.gate1-tools\evidence\ground-cover-68af815-high-release-20260904`.

- [Reçu natif](ground-cover-68af815.native-validation.json), copie JSON sémantique ; chemins relatifs
  ancrés dans cette racine externe. Original SHA-256
  `539e216cb5ef1d86eea673252d0a29a2ef8e31f239fc20367a1ad0f5788be5a1`.
- Ledger SHA-256 : `e47cae2958667e6fe533af21468f0c7c74a51e3a7c25a8eca529fd8e88d1bfd1`.
- Binaire : `E:\OpenRPG_Forge\.gate1-tools\binary-controls\ground-cover-20260904\forge_runtime-68af815.exe`,
  SHA-256 `eca4cf406f90271647a1456e582f6b4ffd5103ac32ce3a97fc92a3c31bccd936`.
- PNG copiés sans transformation : [vue humaine](native/68af815adf9d2beb341943dd5ba7eed8fd1f132e/ground-cover/human-clear.png)
  et [vue distante](native/68af815adf9d2beb341943dd5ba7eed8fd1f132e/ground-cover/distant-clear.png).

Racine Browser : `E:\OpenRPG_Forge\.gate1-tools\evidence\ground-cover-68af815-browser-20260904`.
Run PASS : `68af815adf9d-20260904T083428491Z`.

- [Manifeste](ground-cover-68af815.browser-manifest.json), copie JSON sémantique ; chemins relatifs
  ancrés dans ce run externe. Les 28 artefacts ont été revérifiés par taille/hash.
- Manifeste original SHA-256 : `3d619ba124f791dac24ef2ba9ccc44039581c1a506866dde1a5f1cc6261667a0`.
- Ledger `r1-023-025-026-browser-weather-matrix-ledger.json` SHA-256 :
  `dfaee2d196a167ee060814609c2be60ad67ef90752085d24f06b596c76dae8cb`.

## Inspection et limite

Comparaison d'agent avant/après avec le Release `99a724a` : les cinq poses fixes sont identiques,
même machine et profil High ; le temps d'animation n'est pas figé. Les 29 hashes PNG natifs et le
binaire copié ont été revérifiés. Vues humaine/distante et eau sous pluie puis restaurée inspectées ;
côté Browser, séquence High et vue Low claire également inspectées.

Couverture locale et courbure améliorées, sol moins uniforme. Une seconde inspection d'agent
concorde : pas de disparition massive ni de fissure évidente. **Limites :** rubans encore anguleux
et encombrants au premier plan, touffes régulières, grands sols nus, berges polygonales, composition
rudimentaire. La vue distante ne démontre pas un gain de couverture. Ce lot est une amélioration
locale, pas une qualité finale ou une parité Grassworks/Cadle. Les reçus humains restent non remplis.
**Gate 1 reste ouverte à 43/55 ; Gate 2 n'est pas commencée.**
