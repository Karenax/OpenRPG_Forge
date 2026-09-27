# Gate 1 — Candidat de transition proche des arbres gouvernés

Date : 2026-09-08. Branche Rust, HEAD de base
`f786a38aca2023f08d708687f99a47d14f34e6d4`, arbre modifié préservé,
référence distante locale en retard de 103 commits (sans fetch ni publication).
**Candidat intégré au code partagé, vérifié en natif ; Gate 1 ouverte.**

## Implémentation et compromis

`vegetation/prototype_projection.rs` applique aux seuls prototypes gouvernés
une transition LOD0/LOD1 de `near_lod_distance_m` à cette distance + 8 m,
bornée par `medium_lod_distance_m`. High passe donc de 64–72 m à **24–32 m**.
Le chemin procédural léger conserve 64–72 m. Les enveloppes herbe, intermédiaire
et lointaine restent inchangées ; mêmes sources, identités, poses, tailles,
colliders et 1 662 arbres / 19 944 primitives. Aucune retouche ponctuelle de carte,
aucun asset modifié, aucune dépendance ajoutée.

Bevy 0.19.1 filtre déjà les shadow casters selon `VisibilityRange` dans
`check_dir_light_mesh_visibility` (`bevy_light/src/lib.rs`). Pas de nouveau
culling redondant. En revanche, le chemin gouverné ne fait projeter les ombres
que par LOD0 : cette transition réduit **aussi leur portée**. Le gain ne prouve
donc pas une optimisation à qualité identique. Le futur choix d'une ombre
simplifiée indépendante du LOD visible n'est pas implémenté ici.

Le diagnostic `governed_forest_preview_projected` exporte maintenant les bornes
réelles du fondu ; 24/32 m et les mêmes comptes sont observés au lancement.
Le profil d'ombres canonique High n'est pas remplacé par celui de cet essai.

## Vérifications exécutées

- Nouveau test numérique : High 24–32 m, plafond étroit 24–28 m, enveloppes
  procédurales/herbe/lointaines inchangées : PASS.
- Cycle de qualité existant adapté aux ranges gouvernés ; le workspace complet
  passe, notamment conservation des bindings, poses, collisions et forêt dense.
- `cargo test --locked --workspace` : PASS (timing opt-in préexistant ignoré).
- `cargo clippy --locked --workspace --all-targets --all-features -- -D warnings` : PASS.
- `cargo check --locked -p forge_engine --no-default-features --features web
  --target wasm32-unknown-unknown` : PASS.
- `cargo build --locked -p forge_runtime`, fmt et diff : PASS.

Appels Cargo sur le cache E: `terrain-water-20260906`, deux jobs,
`CARGO_INCREMENTAL=0`, `RUSTFLAGS=-C debuginfo=0`. Session de contrôle 32282,
code final 0. `check.log` conserve le verdict et les timestamps, **pas le détail
des sorties natives Cargo** (limite de Start-Transcript) ; celles-ci figurent
dans les sorties de session. Pas de décompte agrégé reconstruit dans ce rapport.
Pas de nouveau lancement Studio/WebView ni de nouveau bundle Tauri pour ce LOD.
Le candidat Studio précédent contient les textures corrigées, pas ce réglage.

## Comparaison exploratoire avant/après

RX 7800 XT, Vulkan, 1920×1080 ; même kit corrigé
`r1-texture-colorspace-20260908/runtime-kit`, même profil candidat d'ombres
2048 / 3 cascades / 128 m / contact actif, autres domaines High.
Marche physique `1800:1:0.22`, 120 frames de stabilisation puis 300 samples
avec temps virtuel figé et VSync désactivée, sans compilation concurrente.
Le script vérifie absence d'ERROR, fenêtre, sol, nombre de samples et projection
des ombres. Il termine son propre processus sur succès, erreur ou délai.

| Exécutable | FPS moyens | p95 ms | 1 % low FPS |
|---|---:|---:|---:|
| Avant, fondu 64–72 m | 20,639 | 50,952 | 17,880 |
| Après, fondu 24–32 m | 31,870 | 33,001 | 29,061 |

Gain local d'environ 54 %, objectif 60 FPS non atteint. Les positions diffèrent
d'environ 3,8 mm : avant (-132,48827 ; 2,59549 ; -203,52817), après
(-132,49129 ; 2,59641 ; -203,52608). Phase d'animation au gel non normalisée.
Il s'agit de rendu statique exploratoire, pas de qualification du jeu vivant,
de GPU cible 3060/6600, ni d'une mesure complète du temps GPU.

Le premier run `before` (21,381 FPS) est **écarté de la comparaison** : la
compilation a commencé avant la fin de ses samples. Son log est conservé.
Le run `before-clean` ci-dessus a été exécuté après la fin de tous les contrôles.

## Capture et statut

Capture réelle après marche en (-132,49524 ; 2,59931 ; -203,51639), inspectée
face à la capture corrigée précédente. Grands troncs, capsule et canopée dense
conservés ; ombres proches présentes, **arrière-plan plus clair**. Pas de preuve
de continuité en mouvement ni d'acceptation artistique. L'overlay ~33,5 FPS de
la capture n'est pas le benchmark du tableau.

Cette tranche ne ferme aucune case de passage. Le candidat reste dans le
chemin forêt gouvernée opt-in ; compromis d'ombres, intégration visuelle Studio,
fluidité cible, retouche Blender humaine et revue artistique restent ouverts.
CI distante NOT RUN sous ADR-0006 ; aucune publication Git.

## Artefacts locaux

Racine : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-governed-near-lod-20260908`.
`Run.ps1` rejoue les mesures/capture ; rapports JSON dans l'événement
`walk_measurement_completed` des logs. Pas d'écrasement des preuves existantes.

| Fichier | SHA-256 |
|---|---|
| `forge_runtime.exe` | `EE4E05CC2BFB5E57DC320F6C937394E1E5994D3B54D0BE95EF204393C3D0F25A` |
| ancien exe `r1-sky-binding-integration-20260908` | `7C4EB10B3877D6B5FA600C838C355A696BBD2910014776D79BECF0C04E4BEFDC` |
| `before-clean.stderr.log` | `74673D649AC60B07070CE54C405F9222FF76F6AE1724F7ADF5A367C8EDC712C9` |
| `after.stderr.log` | `595CAF17AB1900BBD61EA23D560BD106A390E799A903C0B3E8C516413F246D04` |
| `capture.png` | `66D1A6D040F3738C9C2FE9D2855562EDE13751E7B52DC425E8FE42CB33011A62` |
| `capture.metrics.json` | `5317F2EEB6FBDE635BDC23A17EFDE159BC80EFC3946504025014D05A1A74D164` |
| `check.log` | `1FD14E91F3C3F3EC719A395D7BA3607B21BF6AE072370BCA8BE839FBF1948673` |
| source `prototype_projection.rs` | `6130317D4821F56B8923A1E145B37EB9354D7301117B8A9245FF51C069F170DF` |

Profil d'ombres SHA `484DC144D28EA6B133DBFCC7C2B5F2368B2A93A89DB7A19B1F9B6658327169EA`.
WorldSketch et qualité canoniques inchangés, hashes respectifs
`EA014301FB63835609FBF47494FD9FD87D135DB57AA82991B9C27194787AE95F` et
`D9572DBFCF51ED885967F90BCD8B7F4113D0F8C56787192CDADF3473E4AD2C06`.
Processus graphiques 38588, 34360, 39432 et 4668 terminés ; contrôle CIM final
confirme aussi la fin des processus Cargo/rustc suivis. Aucun onglet ouvert.

## Complément — intégration Studio exécutée le 8 septembre

Le statut « Studio non reconstruit » de la tranche native ci-dessus est désormais
dépassé pour **le LOD**. Nouveau frontend `trunk build --release`, puis
`Build-StudioCandidate.ps1 -GovernedKit` avec le même kit aux couleurs corrigées.
Session 6147 terminée 0, logs Trunk/Tauri conservés. Compilation WASM 2 min 50,
optimisation Binaryen sur deux cœurs, Tauri 34,15 s. Warning futur connu
`proc-macro-error2 2.0.1` non masqué. Pas de changement de dépendance ni de code
dans cette tranche de packaging ; les validations partagées ci-dessus sont
réutilisées sur la même source `prototype_projection.rs` hash-vérifiée.

Racine : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-near-lod-20260908`.
Le candidat embarque les treize fichiers épinglés du frontend et du kit.
**Le profil d'ombres High du Studio reste 4096/quatre cascades**, pas le candidat
natif 2048/trois cascades. Le chiffre de 31,870 FPS ne se transfère pas au Studio.

Lancement réel du nouveau chemin puis capture/export par Computer Use : kit
20/20, forêt 1 662 arbres / 19 944 primitives, un monde et une caméra active,
six hashes alignés, `failure_reason=null`. L'export ne contient pas un nouveau
champ numérique pour le fondu LOD ; ses bornes sont testées dans le moteur et
observées en natif, pas directement attestées par le JSON Studio.

Écran de chargement puis état transitoire noir sur capsule et végétation à nouveau
observés. État stabilisé coloré après défilement de l'inspecteur, **sans réglage
graphique**. Aucun lien causal avec le défilement démontré. PNG final inspecté,
vue depuis le spawn en prairie, forêt à droite : ce n'est ni un parcours sous
canopée ni une nouvelle revue de taille des arbres à distance comparable.
Warmup et qualité des premières frames demeurent ouverts.

Exports réels `forge-diagnostic-1788884704342.json` et
`forge-viewport-1788884718394.png` reçus dans Downloads, copiés sur E: avec hash
identique, originaux conservés. Viewport 1408×966. Ledger de démarrage : 120
samples après 60 warmup, 34,216 FPS moyens, p95 32,9 ms, 1 % low 27,933 FPS.
Pas de benchmark Full HD ni de gain qualifié ; observation CPU prête à 7 285 ms,
`gpu_presentation=not_observed` dans le ledger, distinct de l'inspection du PNG.

| Artefact | SHA-256 |
|---|---|
| `candidate/forge-studio-candidate.exe` | `BE7FDEA3E7D809AC81B5EF8707806F6E236B8F47EEE895D26EF67FEE75545BC6` |
| `frontend/forge_studio_bg.wasm` | `4F7D7BAC7D81AF9F2C18B734A2B9D40686FF087591A0F926E5B26B11D75C372C` |
| `candidate/build-proof.json` | `8C1C8920D6CF83B86DA0A9F746A8528163A6875CF8F5EF1785319ED81CF7B738` |
| `diagnostic.json` | `BE720009F3FF62791F5F65515C99004DC6EBB450D656808C34D52EDEEB786565` |
| `viewport.png` | `22513CAE6B4116AC49C11471A71B16D78C4ECA6CAB826A4A40E66E6ABDF66237` |

Studio 32200 fermé par Alt+F4, conhost 11144 et WebViews 38020, 44636, 43092,
39356, 43548, 26868 absents au contrôle CIM final. Trunk 35316 / wasm-opt 37100
terminés ; session Computer Use réinitialisée. Les premières observations de
fenêtre après fermeture étaient transitoires, la terminaison est confirmée par
les processus. Aucun navigateur utilisateur arrêté ni publication Git.

**Verdict : candidat LOD disponible et lancé dans le Studio, pas Gate 1 validée.**
Acceptation humaine, continuité en mouvement, démarrage sans noir et performance
cible restent requis ; ne pas répéter le smoke inchangé comme substitut.
