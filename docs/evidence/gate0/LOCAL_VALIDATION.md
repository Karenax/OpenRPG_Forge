# Gate 0 — Validation locale et clone propre

Date : 30 août 2026
Branche : `Rust`
Baseline complète en clone propre : `528a909738bde92a8831e5b41ba185ab35ffef7f`
Lot runtime v2 et cycle de vie : `70127992de1c96c4e704d2b62765fca840db45fd`
Lot Studio télémétrie/harness : `7286740c18fe9afa6cc207e388d5f335495fc650`
Preuve Tauri finale : `a7c506f099b50100ad49f3ae6d01d541c9b63d49`
Verdict local courant : **PASS AUTOMATISÉ LOCAL GATE 0 SOUS ADR-0004/0005/0006**

## Portée

Cette preuve conserve d'abord le replay complet du socle `528a9097`, exécuté dans un clone propre
distinct sans `upstream/cadle/` ni correction manuelle. Elle ajoute séparément les validations
ciblées du runtime v2 `70127992` et du Studio `7286740c`. Elle ne prétend pas que les 27 tests du
replay historique constituent un nouveau replay workspace au commit courant.

ADR-0004 qualifie pour Gate 0 l'indisponibilité explicitement diagnostiquée d'un compteur global de
draw calls ; aucun nombre n'est inventé. ADR-0005 confirme le Studio Tauri/Leptos/WebGPU et diffère
les revues humaines sans modifier les ledgers `uninspected`. ADR-0006 autorise une qualification
locale automatisée malgré l'absence de runner distant, mais ne qualifie jamais la CI de verte.

La preuve Tauri commit-bound de `a7c506f` est acquise depuis le clone propre : build contrôlé,
exécutable exact, télémétrie WebView2, ACL et capture `PrintWindow(PW_RENDERFULLCONTENT)` ont été
validés. L'inspection des pixels reste automatisée ; la revue humaine demeure différée et le ledger
de capture conserve donc honnêtement `qualified = false`.

## Environnement

| Élément | Valeur vérifiée |
|---|---|
| OS | Microsoft Windows 11 Professionnel `10.0.26200`, 64 bits |
| CPU | AMD Ryzen 5 7600 6-Core Processor, 12 CPU logiques |
| RAM visible | 33 463 984 128 octets |
| GPU | AMD Radeon RX 7800 XT |
| backend/pilote | Vulkan, `26.8.1 (LLPC)` |
| `rustc` | `1.98.0 (88d9e12ae 2026-08-18)` |
| `cargo` | `1.98.0 (797e8a9bc 2026-08-05)` |
| cibles | `x86_64-pc-windows-msvc`, `wasm32-unknown-unknown` |
| `cargo-deny` | `0.20.2` |
| `cargo-audit` | `0.22.2` |
| Trunk | `0.21.14` |
| Tauri CLI | `2.11.4` |
| `wasm-bindgen-cli` | `0.2.127` |

## Clone et isolation — baseline historique `528a9097`

Le clone avait été créé avec les options `--no-local --single-branch --branch Rust`, puis mis à jour
par `fetch` et fast-forward vers le commit qualifié. Les contrôles finaux ont donné :

```text
branch = Rust
HEAD = 528a909738bde92a8831e5b41ba185ab35ffef7f
git status --porcelain = vide
upstream/cadle = absent
fichiers suivis sous upstream/cadle/** = 0
```

Le test dynamique prouve que build, tests, WASM, Tauri et runtime n'ont pas besoin du snapshot Cadle.
Aucun asset ou code Cadle n'a été copié dans Forge.

## Workspace et scène partagée

`cargo metadata --locked --no-deps --format-version 1` retourne exactement huit packages :

```text
forge_assets
forge_cli
forge_core
forge_engine
forge_project
forge_runtime
forge_studio
forge_world
```

La CLI valide le document partagé :

```text
VALID schema_version=1 revision=1 entities=4 sha256=0ea53d539067f399e7e76678532144955f35e5a15e95d11becf3a174843c67df
```

Le runtime natif, le viewport WASM et le backend Studio utilisent ce même document JSON. Le cas
positif passe `SCENE_PROOF.schema.json` ; un document avec `schema_version = 2` est rejeté. Les tests
Rust vérifient aussi les IDs uniques, le quaternion, la caméra, la lumière et le round-trip sans perte.

## Validation agrégée — baseline historique `528a9097`

Commande finale exécutée dans le clone propre à `528a9097` :

```powershell
$env:CARGO_BUILD_JOBS = '1'
./scripts/Test-Rust.ps1
```

Résultat : code 0.

```text
FORGE_RUSTSEC_POLICY_PASS vulnerabilities=0 warnings=20 distribution_blockers=1
FORGE_RUST_VALIDATION_PASS wasm=True supply_chain=True
```

Le script a exécuté avec succès :

- `cargo fmt --all -- --check` ;
- `cargo clippy --workspace --all-targets --locked -- -D warnings` ;
- `cargo test --workspace --all-targets --locked` ;
- check et Clippy strict de `forge_engine`/Studio pour `wasm32-unknown-unknown` ;
- `trunk build --release --config Trunk.toml` ;
- `cargo deny --locked check licenses bans sources advisories` ;
- la politique RustSec épinglée.

Répartition exacte des 27 tests :

| Package/cible | Tests passés |
|---|---:|
| `forge_assets` | 3 |
| `forge_cli` intégration | 3 |
| `forge_core` | 5 |
| `forge_engine` | 3 |
| `forge_project` | 3 |
| `forge_runtime` | 3 |
| `forge_studio` frontend | 2 |
| `forge_studio` backend | 1 |
| `forge_world` | 4 |
| **Total** | **27** |

Résultat à `528a9097` : 27 passés, 0 échec, 0 ignoré. Ce décompte reste la qualification agrégée du
socle historique ; il n'est pas relabellisé comme replay workspace de `70127992`, `7286740c` ou
`a7c506f`.

Une tentative ciblée parallèle antérieure avait fait échouer `rustc` par épuisement LLVM de mémoire.
Le replay mono-job ci-dessus distingue ce problème de ressources d'un défaut produit et passe
intégralement.

## Correctifs de reproductibilité qualifiés

Le premier replay en clone propre avait exposé deux défauts :

1. un environnement hérité `NO_COLOR=1` faisait échouer Trunk, qui attend un booléen textuel ;
2. un parent de capture absent provoquait un échec d'écriture Bevy sans rendre le processus non nul.

Le commit `528a9097` ajoute `scripts/Invoke-StudioTrunk.ps1`, utilisé par les hooks Tauri, qui
normalise temporairement `NO_COLOR=true` puis restaure l'environnement. Le runtime crée les parents,
supprime les sorties périmées et vérifie l'existence et la taille de la capture avant de réussir.
Deux tests supplémentaires portent le total à 27.

## Validation ciblée Studio `7286740c`

Le lot de télémétrie et de preuve Studio a passé localement, en mono-job lorsque Cargo était
impliqué :

- `cargo fmt -p forge_studio -- --check` ;
- `cargo test -p forge_studio --all-targets --locked -j 1` : 11 tests passés ;
- `cargo clippy -p forge_studio --all-targets --locked -j 1 -- -D warnings` ;
- check et Clippy strict de la cible `wasm32-unknown-unknown` ;
- `trunk build --release --config Trunk.toml`.

Le bundle release réellement produit porte :

| Artefact `dist/` | Taille | SHA-256 |
|---|---:|---|
| `forge_studio_bg.wasm` | 34 989 519 octets | `4b775441cac2864618985c7824a73cb56344f2a2937e2493470e1ed2466ba5e6` |
| `forge_studio.js` | 184 651 octets | `7339dca55cff566a42ea2fa69b23b01d764587deffdd678de28fe6a6536c225e` |
| `index.html` | 1 025 octets | `7ce400ae1b55e3f621355d3fa21546ce6ed4e3be485b2811c4a4070389ce7f15` |
| `styles.css` | 3 103 octets | `c33688b71bb4cde7cbf52ee8cad9f6e0a7453c4326ff914590464aef5b72cc2d` |

Ces validations ciblées prouvent le code Studio et le bundle Chrome `7286740c`. Elles sont distinctes
de la preuve Tauri commit-bound finale `a7c506f` enregistrée ci-dessous.

## Tauri et bundle historique `528a9097`

Le test de régression a volontairement conservé la valeur problématique :

```powershell
$env:NO_COLOR = '1'
Push-Location apps/forge_studio
try {
    cargo tauri build --no-bundle
} finally {
    Pop-Location
}
```

Le hook a appelé le wrapper Trunk automatiquement. La commande a terminé avec le code 0, sans
correction externe.

| Fichier final du clone propre | Taille | SHA-256 |
|---|---:|---|
| `target/release/forge-studio.exe` | 16 304 640 octets | `4bb12eef7841b9bc4247b6cc12ea4012aa241d75c31f75dea1d43edc2a77efb0` |
| `dist/forge_studio_bg.wasm` | 34 879 512 octets | `d307e835f6aede2fb8cdc5b0939d2219a54354b79def6fb09f65805779086a28` |
| `dist/forge_studio.js` | 181 150 octets | `4751935be971a12ee33840bb93f9e39a1e92bce10d964a0e6b5c23d864399ecb` |
| `dist/index.html` après packaging Tauri | 1 037 octets | `f5693da13400410133362e9525159824ddb978a91929e66c0660ae5acfa67904` |

L'avertissement de compatibilité future de `proc-macro-error2 2.0.1` reste visible ; il ne vient pas
du code Forge et figure dans la politique RustSec.

## Tauri / WebView2 final `a7c506f`

Le replay contrôlé a terminé dans le clone propre au commit
`a7c506f099b50100ad49f3ae6d01d541c9b63d49`, arbre
`7ca544c9eac98af558a4d91390c74ae08fa2ff11`, branche `Rust` et source Git propre. La commande
commit-bound était :

```powershell
cargo tauri build --no-bundle --config apps/forge_studio/tauri.gate0-proof.conf.json
```

Le build a commencé à `2026-08-30T22:25:01.2306453+00:00` et s'est terminé avec succès à
`2026-08-30T22:27:16.7336881+00:00`. L'exécutable contrôlé, écrit à
`2026-08-30T22:27:16.3646685+00:00`, a été lancé depuis son chemin canonique puis vérifié avant et
après acquisition :

| Artefact final | Taille | SHA-256 |
|---|---:|---|
| `target/release/forge-studio.exe` | 16 116 224 octets | `2cd63455a8ba76762519a7194ea9ff66bc24b7d4fac6e262f0884d74dc77c458` |
| `studio-window-20260830T222721105Z.telemetry.json` | 1 618 octets | `de03d21de9c0ee0574e09fbc56daaf160672653e06def4fee048891cacac720e` |
| `studio-window-20260830T222721105Z.png` | 342 314 octets | `3e0f37b466a47fe75168d6a10f12caee1b82dbc127788da0d3bbd83e1820e6ed` |
| `studio-window-20260830T222721105Z.json` | 3 225 octets | `b8ae014c5c8dd2927ac306db69a6fd00a82315d4d3173dd8f373115f2147ca75` |

Le ledger de capture `forge.gate0-studio-window-capture` est en schéma v2 ; le ledger de télémétrie
`forge.gate0-studio-telemetry` reste en schéma v1. Les deux schémas et les contrôles sémantiques ont
passé. La fenêtre exacte portait le PID `30324`, le HWND `0x000000000054056a` et le titre
`OpenRPG Forge Studio · Gate 0 proof`. Elle était visible, non minimisée et liée au même processus
avant et après la capture.

L'acquisition a utilisé `PrintWindow` avec `PW_RENDERFULLCONTENT` (`flags = 2`) sur ce HWND, sans
exiger ni voler le premier plan. Win32 a réussi ; la fenêtre est restée hors foreground avant et
après. Le PNG mesure 1616 × 939, contient 192 couleurs échantillonnées et passe le contrôle non vide.

La télémétrie WebView2 enregistrée à `2026-08-30T22:27:20.998Z` confirme :

| Contrôle Studio final | Valeur observée |
|---|---|
| cible / WebGPU | `tauri_studio` / `ready`, sans injection ni raison de fallback |
| chargement WASM / WebGPU / scène | 490,9000000357628 / 1 155,300000011921 / 1 155,300000011921 ms |
| mémoire WASM / heap JS / limite JS | 50 069 504 / 9 323 224 / 4 395 630 592 octets |
| viewport | un démarrage, canvas 1088 × 786 |
| warm-up / échantillons | 60 / 120 |
| durée / moyenne | 833,3999999999997 ms / 6,944999999999998 ms |
| p50 / p95 / p99 / maximum | 6,8999999999999995 / 7,5 / 7,7 / 8,1 ms |
| moyenne / 1 % low | 143,98848092152633 / 126,58227848101264 FPS |
| hitches > 16,7 ms / > 33,3 ms | 0 / 0 |
| interaction handler-vers-post-paint | un échantillon, 316,29999999999995 ms |
| stabilité du canvas | 1 observation stable, 0 instable |
| commande Forge autorisée | `execute_studio_command = succeeded` — `SceneSummary accepté : 4 entités, source 0ea53d539067f399e7e76678532144955f35e5a15e95d11becf3a174843c67df` |
| commande Tauri refusée | `plugin:window|set_title = explicitly_denied` — `Commande Tauri échouée : Command plugin:window\|set_title not allowed by ACL` |

L'échantillon d'interaction unique valide le chemin post-paint et la stabilité, pas une latence
utilisateur représentative. L'inspection automatisée indépendante du PNG original a trouvé le shell
Studio complet à trois colonnes, le terrain vert, le cube bleu sélectionné, le canvas non noir, les
résumés WebGPU/IPC/ACL visibles et aucune occlusion grossière. Elle a aussi relevé plusieurs glyphes
« tofu » dans l'overlay Bevy du canvas, notamment pour des séparateurs et certains caractères
accentués ; cette observation demeure la preuve historique exacte de `a7c506f`.

Cette inspection n'est pas une revue humaine. Le ledger final conserve exactement
`review.status = uninspected`, `human_review_performed = false`,
`proof_status = pending_human_review` et `qualified = false`, conformément à ADR-0005.

### Correctif candidat de `G0-UI-001` — preuves automatisées locales courantes

L'analyse du code candidat confirme que la fonte Bevy utilisée par défaut pour l'overlay ne couvre
que l'ASCII, alors que les textes de diagnostic historiques contenaient des caractères Unicode. Le
correctif candidat normalise la chaîne finale de l'overlay en ASCII, en remplaçant chaque caractère
non ASCII par `?`. Il n'ajoute ni fonte ni autre asset.

La campagne exécutée sur l'état courant du worktree apporte les preuves suivantes :

| Contrôle du correctif glyphes/overlay ASCII | Résultat courant |
|---|---|
| suite `forge_engine` | **PASS — 119/119**, dont les assertions `is_ascii()` des overlays Gate 0 et Gate 1 |
| suite `forge_runtime` | **PASS — 32/32** (31 bin + 1 intégration) |
| suite `forge_studio` | **PASS — 31/31** (28 lib + 3 bin) |
| check WASM exact courant | **PASS — 1.06 s** |
| formatage et contrôle du diff | **PASS** |
| Clippy strict exact | **PASS — 12.04 s** |
| smokes du candidat courant natif / Browser WebGPU | **NOT RUN** |

Le statut probant devient donc **CORRECTIF — TESTS/FMT/DIFF/WASM/CLIPPY PASS ; REVUE HUMAINE
DIFFÉRÉE**. Ces résultats qualifient la validation automatisée obtenue sur le worktree courant ; ils
ne constituent pas un replay commit-bound. Les smokes du candidat courant natif et Browser WebGPU
restent **NOT RUN** ; aucune nouvelle capture native ou Tauri et aucune revue humaine n'ont été
acquises. Le check WASM n'est pas une preuve visuelle. Ce correctif candidat ne modifie pas les
ledgers historiques et ne lève aucune des revues humaines différées par ADR-0005.

## Runtime natif historique `528a9097`

Avant lancement, `artifacts/gate-0/final-528a909` n'existait pas. Il n'a pas été précréé.

```powershell
cargo run --release --locked -j 1 -p forge_runtime -- `
  --capture artifacts/gate-0/final-528a909/native-runtime.png `
  --capture-frame 180 `
  --metrics artifacts/gate-0/final-528a909/native-runtime-metrics.json `
  --metrics-warmup-frames 120 `
  --exit-after-frames 240 `
  --no-vsync
```

Résultat : code 0 ; le runtime a créé seul le dossier et les deux sorties.

| Artefact | Dimensions/taille | SHA-256 |
|---|---:|---|
| `forge_runtime.exe` | 73 513 984 octets | `b5654c52a27920ffec7516b36231b0ddab6ad04b1d6b47b4e452329634a5184e` |
| `native-runtime.png` | 1920 × 1080, 1 027 592 octets | `0e3a206d2f984c0b42927801df3b2d54664a6ef13da76852a6008dcb5a332061` |
| `native-runtime-metrics.json` | 3 581 octets | `225bb327f1311376d84d62c97723ebe0857b6371e6d67bc9c4b1c453563af950` |

Ledger post-warm-up :

| Mesure | Valeur |
|---|---:|
| échantillons | 120 |
| durée | 454,8433 ms |
| moyenne | 3,7904 ms |
| moyenne FPS | 263,8271 |
| p50 | 2,3067 ms |
| p95 | 8,0712 ms |
| p99 | 8,7186 ms |
| maximum | 10,5704 ms |
| 1 % low | 103,6860 FPS |
| hitches > 16,7 ms | 0 |
| hitches > 33,3 ms | 0 |

Le ledger expose quatre entités source et deux `mesh_draws_logical`. Cette dernière valeur n'est pas
un compteur des appels de rendu GPU. Les diagnostics Bevy donnent des invocations et durées de stages,
mais aucun nombre fiable de draw calls.

La capture a été inspectée automatiquement : sol vert, cube bleu centré, overlay net, aucune
corruption. L'enveloppe `NATIVE_RUNTIME.visual-proof.json` valide contre
`VISUAL_PROOF.schema.json`, porte `source_tree_clean: true` et conserve la revue humaine en
`uninspected`.

## Runtime v2 et cycle de vie `70127992`

Le lot natif postérieur ajoute la journalisation JSON Lines structurée, le redimensionnement demandé
puis confirmé par le backend, les modes de présentation VSync, la fermeture normale Bevy et le
ledger `forge.runtime-metrics` v2. Les validations ciblées ont passé 12 tests : cinq pour
`forge_engine`, six pour `forge_runtime` et un test d'intégration de journalisation structurée. Le
formatage et Clippy strict ont également passé.

Le replay post-audit local et ignoré est associé au lot ensuite commité sous
`70127992de1c96c4e704d2b62765fca840db45fd` :

| Artefact local | Taille | SHA-256 |
|---|---:|---|
| `artifacts/gate-0/native-hardening/resize-metrics-v2-post-audit.json` | 4 708 octets | `f2da892be30b1ddaf0ddd79be5b71ffcbaa3b60f360d2cfe795dc31a47014b84` |

Le ledger ne sérialise pas un commit Git et ne remplace donc pas l'enveloppe visuelle commit-bound
historique `528a9097`. Il fournit la preuve supplémentaire suivante :

| Contrôle runtime v2 | Valeur observée |
|---|---|
| document source | `0ea53d539067f399e7e76678532144955f35e5a15e95d11becf3a174843c67df` |
| fenêtre initiale | 1920 × 1080, redimensionnable |
| présentation demandée | `auto_no_vsync`, VSync demandé `false` |
| resize demandé | frame 10, 1280 × 720 |
| resize confirmé par le backend | frame 11, état final 1280 × 720 |
| événements resize observés | 3 |
| échantillons | 60 |
| durée | 319,9174 ms |
| moyenne | 5,3319567 ms / 187,5484 FPS |
| p50 / p95 / p99 | 3,4879 / 9,2502 / 11,2651 ms |
| maximum | 11,2651 ms |
| 1 % low | 88,7697 FPS |
| hitches > 16,7 ms / > 33,3 ms | 0 / 0 |
| spans de rendu chronométrés | 6 |
| compteur global de draw calls | `null` |
| statut draw calls | `unavailable_bevy_0_19_no_global_counter` |

La demande de resize à la frame 10 et sa confirmation à la frame 11 empêchent de confondre une
commande soumise avec un événement réellement renvoyé par le backend. Le chemin de fermeture
automatisé envoie un `WindowCloseRequested` normal et observe la sortie Bevy avec le code 0 ; il ne
prétend pas simuler une action physique sur la barre de titre.

Conformément à ADR-0004, `R0-025` passe pour Gate 0 par exception bornée : FPS et frame time sont
réels, tandis que l'absence de compteur exhaustif est affichée et sérialisée explicitement. Les deux
maillages source, primitives et invocations shaders ne sont jamais renommés en draw calls.

## Chaîne d'approvisionnement

```text
advisories ok, bans ok, licenses ok, sources ok
FORGE_RUSTSEC_POLICY_PASS vulnerabilities=0 warnings=20 distribution_blockers=1
```

`RUSTSEC-2024-0429` sur `glib 0.18.5` reste bloquant avant une distribution Linux. L'intention de
licence PolyForm du contexte doit également être réconciliée par le propriétaire avec les manifests
`MIT OR Apache-2.0` avant toute distribution.

## CI distante — exception ADR-0006

Le workflow `Rust gate` a été déclenché sur `origin/Rust` au commit final
`a7c506f099b50100ad49f3ae6d01d541c9b63d49` par le run GitHub Actions
[`33339077298`](https://github.com/Karenax/OpenRPG_Forge/actions/runs/33339077298). GitHub affiche une
conclusion `failure`, mais chacun des quatre jobs possède zéro étape : aucun runner, checkout, build
ou test n'a démarré. L'annotation commune indique un paiement récent échoué ou un plafond de dépense
à augmenter.

| Job distant | ID | Étapes | Statut de preuve |
|---|---:|---:|---|
| `native (windows-2025)` | `99331257296` | 0 | `NOT RUN — infrastructure billing` |
| `native (ubuntu-24.04)` | `99331257267` | 0 | `NOT RUN — infrastructure billing` |
| `wasm` | `99331257192` | 0 | `NOT RUN — infrastructure billing` |
| `supply-chain` | `99331257301` | 0 | `NOT RUN — infrastructure billing` |

Le run historique `33335683606` du commit `7286740c` présentait déjà la même absence totale
d'étapes et la même annotation de facturation. Il reste une trace historique, tandis que
`33339077298` est la preuve distante liée au HEAD final.

ADR-0006 autorise le remplacement temporaire de cette preuve distante par des replays locaux
automatisés reproductibles jusqu'à Gate 4. Il est interdit d'en déduire une CI verte ou une
compatibilité Linux prouvée. L'exception expire dès le retour des runners et, dans tous les cas,
avant distribution.

## Revues humaines différées

ADR-0005 et l'ordre explicite du propriétaire rendent ces revues non bloquantes pour la qualification
automatisée de Gate 0, sans les transformer en succès :

- `G0-HR-001` — inspecter la capture native historique et son overlay ;
- `G0-HR-002` — inspecter humainement la capture complète Tauri/WebView2 acquise ;
- `G0-HR-003` — effectuer le parcours manuel redimensionnement, sélection, picking, stabilité du
  canvas, fermeture et diagnostic WebGPU indisponible.

Les preuves automatiques conservent `uninspected`, `pending_human_review` et, lorsque le schéma le
prévoit, `qualified = false` jusqu'à cette reprise humaine.

## Verdict

Le socle historique est reproductible localement depuis un clone propre Windows, sans Cadle et sans
étape manuelle cachée. Les durcissements natifs `70127992` et Studio `7286740c` passent leurs
validations ciblées, et le replay Tauri final `a7c506f` fournit l'exécutable, la télémétrie ACL/WebGPU
et la capture commit-bound attendus. ADR-0004 borne l'absence de compteur draw calls, ADR-0005
confirme le Studio et diffère la revue humaine, et ADR-0006 classe honnêtement la CI distante comme
`NOT RUN`.

Le verdict Gate 0 est donc **PASS AUTOMATISÉ LOCAL SOUS ADR-0004/0005/0006**. Ce passage administratif
ne transforme ni le ledger visuel `qualified = false` en revue humaine, ni la CI distante en succès,
ni le replay Windows en preuve Linux.
